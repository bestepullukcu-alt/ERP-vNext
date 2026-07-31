using Diten.BuildingBlocks.Eventing;
using Diten.PpmService.Application.Events;
using Diten.PpmService.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.PpmService.Infrastructure.Audit;

public sealed class PpmAuditIntentDispatcher(
    IAuditIntentRepository auditIntents,
    IEventBus eventBus,
    IOptions<PpmAuditProducerOptions> options,
    ILogger<PpmAuditIntentDispatcher> logger)
{
    private readonly PpmAuditProducerOptions _options = options.Value;

    public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled || !_options.WorkerEnabled)
        {
            return 0;
        }

        var candidates = await auditIntents.GetDispatchCandidatesAsync(
            _options.BatchSize,
            cancellationToken);
        var completed = 0;
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (candidate.DispatchMetadata is not null
                && !PpmAuditTrustedTransportMetadataProvider.IsValidSigningMetadata(
                    candidate.DispatchMetadata))
            {
                await auditIntents.MarkDispatchQuarantinedAsync(
                    candidate.Id,
                    "ppm.audit-intent.signing-metadata-invalid",
                    DateTime.UtcNow,
                    cancellationToken);
                logger.LogWarning(
                    "ppm.audit.intent.dispatch_quarantined EventId={EventId} TenantId={TenantId} ReasonCode={ReasonCode}",
                    candidate.Id,
                    candidate.TenantId,
                    "ppm.audit-intent.signing-metadata-invalid");
                continue;
            }

            if (candidate.CorrelationId == Guid.Empty)
            {
                await auditIntents.MarkDispatchQuarantinedAsync(
                    candidate.Id,
                    "ppm.audit-intent.correlation-missing",
                    DateTime.UtcNow,
                    cancellationToken);
                logger.LogWarning(
                    "ppm.audit.intent.dispatch_quarantined EventId={EventId} TenantId={TenantId} ReasonCode={ReasonCode}",
                    candidate.Id,
                    candidate.TenantId,
                    "ppm.audit-intent.correlation-missing");
                continue;
            }

            PpmAuditIntentSubmittedV1 @event;
            try
            {
                @event = new PpmAuditIntentSubmittedV1(candidate);
            }
            catch (EventValidationException)
            {
                await auditIntents.MarkDispatchQuarantinedAsync(
                    candidate.Id,
                    "ppm.audit-intent.contract-invalid",
                    DateTime.UtcNow,
                    cancellationToken);
                logger.LogWarning(
                    "ppm.audit.intent.dispatch_quarantined EventId={EventId} TenantId={TenantId} ReasonCode={ReasonCode}",
                    candidate.Id,
                    candidate.TenantId,
                    "ppm.audit-intent.contract-invalid");
                continue;
            }

            logger.LogInformation(
                "ppm.audit.intent.dispatch_started EventId={EventId} TenantId={TenantId} CorrelationId={CorrelationId}",
                candidate.Id,
                candidate.TenantId,
                candidate.CorrelationId);

            await eventBus.PublishAsync(
                @event,
                new EventPublishOptions
                {
                    EventId = candidate.Id,
                    CorrelationId = candidate.CorrelationId,
                    CausationId = null,
                    TenantId = candidate.TenantId,
                    Producer = "Diten.PpmService",
                    OccurredAtUtc = new DateTimeOffset(candidate.OccurredAtUtc)
                },
                cancellationToken);

            await auditIntents.MarkOutboxEnqueuedAsync(
                candidate.Id,
                DateTime.UtcNow,
                cancellationToken);
            logger.LogInformation(
                "ppm.audit.intent.outbox_enqueued EventId={EventId} TenantId={TenantId} CorrelationId={CorrelationId}",
                candidate.Id,
                candidate.TenantId,
                candidate.CorrelationId);
            completed++;
        }

        return completed;
    }
}
