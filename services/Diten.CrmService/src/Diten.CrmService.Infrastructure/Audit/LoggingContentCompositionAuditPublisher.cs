using Diten.CrmService.Application.Features.ContentComposition;
using Microsoft.Extensions.Logging;

namespace Diten.CrmService.Infrastructure.Audit;

/// <summary>
/// SCMM-12 (MOD-0363, formerly CAND-CAP-0011) — default audit seam for ContentComposition (claim / composition) actions. CRM owns no audit
/// store; this implementation emits a structured log entry (SourceModule MOD-0363, formerly CAND-CAP-0011, conceptually). The HTTP forwarder
/// (<see cref="HttpCrmAuditPublisher"/>) is used when Crm:Audit:Mode=http.
/// </summary>
public sealed class LoggingContentCompositionAuditPublisher : IContentCompositionAuditPublisher
{
    private readonly ILogger<LoggingContentCompositionAuditPublisher> _logger;

    public LoggingContentCompositionAuditPublisher(ILogger<LoggingContentCompositionAuditPublisher> logger)
        => _logger = logger;

    public Task PublishAsync(
        string eventName, Guid tenantId, string entityType, Guid entityId, int version, string? detail,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "CRM content-composition audit event {AuditEvent} module=MOD-0363 tenant={TenantId} type={EntityType} "
            + "id={EntityId} version={Version} detail={Detail}",
            eventName, tenantId, entityType, entityId, version, detail);
        return Task.CompletedTask;
    }
}
