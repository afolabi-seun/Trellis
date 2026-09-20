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
        // A NULL tenantId parameter (not an empty string) is deliberate: every RLS policy casts
        // this to ::uuid, and casting '' throws "invalid input syntax for type uuid" — a query
        // error, not a filtered-out row. Passing SQL NULL to set_config unsets the GUC instead,
        // so current_setting(..., true) returns NULL, and `tenant_id = NULL` is simply NULL
        // (falsy) in the policy check — no context correctly means "see nothing," not "crash."
        command.CommandText = "SELECT set_config('app.current_tenant_id', @tenantId, false), " +
                               "set_config('app.is_system', @isSystem, false);";
        command.Parameters.AddWithValue("tenantId", (object?)TenantContext.Current?.TenantId.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("isSystem", SystemContext.IsSystem ? "true" : "false");
        return command;
    }
}
