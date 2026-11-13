using Diten.CrmService.Application.Features.VisitEvidence;
using Microsoft.Extensions.Logging;

namespace Diten.CrmService.Infrastructure.Audit;

/// <summary>WP-VW-W3L — the structured-logging default of the visit evidence audit seam (the Account / Contact / content
/// pattern); <c>Crm:Audit:Mode=http</c> forwards the same events through <see cref="HttpCrmAuditPublisher"/>.</summary>
public sealed class LoggingVisitEvidenceAuditPublisher : IVisitEvidenceAuditPublisher
{
    private readonly ILogger<LoggingVisitEvidenceAuditPublisher> _logger;

    public LoggingVisitEvidenceAuditPublisher(ILogger<LoggingVisitEvidenceAuditPublisher> logger) => _logger = logger;

    public Task PublishAsync(string eventName, Guid tenantId, string entityType, Guid entityId, int version, string? detail,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "CRM audit event {AuditEvent} tenant={TenantId} entity={EntityType}/{EntityId} v{Version} detail={Detail}",
            eventName, tenantId, entityType, entityId, version, detail);
        return Task.CompletedTask;
    }
}
