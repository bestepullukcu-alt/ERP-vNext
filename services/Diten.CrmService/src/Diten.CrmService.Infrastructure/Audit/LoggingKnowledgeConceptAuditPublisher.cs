using Diten.CrmService.Application.Features.Knowledge.Concept;
using Microsoft.Extensions.Logging;

namespace Diten.CrmService.Infrastructure.Audit;

/// <summary>
/// SCMM-09 (audit bundle) — MOD-0021 audit seam for concept-graph actions (①②). CRM owns no audit store; this default
/// implementation emits a structured audit log entry (SourceModule MOD-0162 conceptually), keeping handlers decoupled
/// from the transport. The HTTP forwarder (<see cref="HttpCrmAuditPublisher"/>) is used when Crm:Audit:Mode=http.
/// </summary>
public sealed class LoggingKnowledgeConceptAuditPublisher : IKnowledgeConceptAuditPublisher
{
    private readonly ILogger<LoggingKnowledgeConceptAuditPublisher> _logger;

    public LoggingKnowledgeConceptAuditPublisher(ILogger<LoggingKnowledgeConceptAuditPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync(
        string eventName, Guid tenantId, string entityType, Guid entityId, int version, string? detail,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "CRM concept audit event {AuditEvent} module=MOD-0162 tenant={TenantId} type={EntityType} id={EntityId} version={Version} detail={Detail}",
            eventName, tenantId, entityType, entityId, version, detail);
        return Task.CompletedTask;
    }
}
