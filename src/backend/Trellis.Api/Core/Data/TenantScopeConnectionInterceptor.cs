using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Trellis.Api.Core.Middleware;

namespace Trellis.Api.Core.Data;

/// <summary>
/// Sets the Postgres session variables every RLS policy in data_model.md reads
/// (<c>app.current_tenant_id</c>, <c>app.is_system</c>) the moment a connection is opened, from
/// <see cref="TenantContext.Current"/> / <see cref="VendorContext.Current"/> /
/// <see cref="SystemContext.IsSystem"/> — layer 1 of the 3-layer isolation model
/// (multi_tenant_design.md) depends on this running before any query, reads included, not just
/// writes, which is why this hooks connection-open rather than <c>SaveChanges</c>. Npgsql resets
/// session state when a pooled connection is returned, so a freshly opened connection always
/// starts clean and reflects only the current request's context, never a stale previous one.
/// </summary>
public sealed class TenantScopeConnectionInterceptor : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ApplySessionContext(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await ApplySessionContextAsync(connection, cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private static void ApplySessionContext(DbConnection connection)
    {
        using var command = BuildCommand(connection);
        command.ExecuteNonQuery();
    }

    private static async Task ApplySessionContextAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = BuildCommand(connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static NpgsqlCommand BuildCommand(DbConnection connection)
    {
        var command = ((NpgsqlConnection)connection).CreateCommand();
        // set_config(..., false) = session-scoped, not transaction-local — a plain read query
        // (no explicit transaction) still needs the value visible to its RLS check.
        //
        // Found the hard way: passing SQL NULL to set_config does NOT unset the GUC back to
        // "never set" the way RESET does elsewhere — Postgres stores it as an empty string, and
        // current_setting(..., true) still reports it as set (to ''), not NULL. RESET has the
        // same problem once a custom GUC has been set at all in that session. This is why every
        // RLS policy in this codebase compares tenant_id AS TEXT (`tenant_id::text = ...`)
        // rather than casting the session variable to ::uuid — an empty string then simply
        // fails to match any real tenant_id (fail-closed, correctly), instead of the ::uuid cast
        // throwing "invalid input syntax for type uuid" on every query issued with no tenant
        // context, which is exactly what broke NotificationDispatchBackgroundService the first
        // time this ran for real (a background service with no tenant/vendor/system context at
        // all). Plain empty string here now — no more DBNull.Value, since text comparison never
        // needed the NULL semantics that turned out not to work anyway.
        command.CommandText = "SELECT set_config('app.current_tenant_id', @tenantId, false), " +
                               "set_config('app.is_system', @isSystem, false);";
        command.Parameters.AddWithValue("tenantId", TenantContext.Current?.TenantId.ToString() ?? string.Empty);
        command.Parameters.AddWithValue("isSystem", SystemContext.IsSystem ? "true" : "false");
        return command;
    }
}
