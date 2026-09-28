using Microsoft.Extensions.Options;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Modules.Auth.Application.Interfaces;
using Trellis.Api.Modules.Document.Application.Interfaces;
using Trellis.Api.Modules.Document.Configuration;
using Trellis.Api.Modules.Notification.Application.Dtos;
using Trellis.Api.Modules.Notification.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;

namespace Trellis.Api.Modules.Document.Infrastructure;

/// <summary>
/// BDR-DOC-003 — the exit criterion's "document expiry alert fires end-to-end without manual
/// intervention" half. Same structural mirror as NotificationDispatchBackgroundService: own DI
/// scope per poll, SystemContext.Enter() since it reads every tenant's due documents in one
/// batch. A short poll interval is fine for dev/demo purposes — same pragmatic choice already
/// made for Notification's own dispatcher.
/// </summary>
public sealed class DocumentExpiryAlertBackgroundService(
    IServiceScopeFactory scopeFactory, IOptions<DocumentOptions> options, ILogger<DocumentExpiryAlertBackgroundService> logger)
    : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Document expiry alert poll failed");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task DispatchOnceAsync(CancellationToken cancellationToken)
    {
        // document carries the standard tenant_isolation RLS policy — this poll deliberately
        // reads across every tenant's due rows in one batch, the same legitimate "trusted
        // background job" bypass NotificationDispatchBackgroundService's own doc comment
        // explains in detail.
        using var systemScope = SystemContext.Enter();

        using var scope = scopeFactory.CreateScope();
        var documentRepository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();

        var lookaheadDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(options.Value.ExpiryAlertLookaheadDays);
        var due = await documentRepository.GetDueForExpiryAlertAsync(lookaheadDate, batchSize: 20);

        foreach (var document in due)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // The uploader is the simplest, always-resolvable recipient — routing to a linked
            // property's admin/department is a fast-follow, not needed for the exit criterion
            // (the Phase 3 plan's own stated deferral).
            var uploaderId = document.LastAlertedAt is null && document.CurrentVersionId is not null
                ? await ResolveUploaderIdAsync(scope.ServiceProvider, document.CurrentVersionId.Value)
                : null;

            if (uploaderId is not { } resolvedUploaderId)
            {
                continue;
            }

            var uploader = await authService.GetUserByIdAsync(resolvedUploaderId);
            if (uploader is null)
            {
                continue;
            }

            await notificationService.SendAsync(
                document.TenantId,
                "DOCUMENT_EXPIRING_SOON",
                uploader.Email,
                new { document.DocumentId, document.ExpiresAt },
                new RelatedEntityRef("Document", document.DocumentId));

            document.MarkAlerted();

            await auditService.RecordAsync(
                document.TenantId, document.TenantId, AuditActorType.System, AuditTypes.Document.DocumentExpiryAlerted,
                $"{{\"documentId\":\"{document.DocumentId}\"}}");
        }

        if (due.Count > 0)
        {
            await documentRepository.SaveChangesAsync();
        }
    }

    private static Task<Guid?> ResolveUploaderIdAsync(IServiceProvider scopedProvider, Guid currentVersionId) =>
        scopedProvider.GetRequiredService<IDocumentVersionRepository>().GetUploadedByAsync(currentVersionId);
}
