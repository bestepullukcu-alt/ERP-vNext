using Diten.HcmService.Application.Features.CoreHrEmployeeMaster;
using Microsoft.Extensions.Logging;

namespace Diten.HcmService.Infrastructure.Audit;

public sealed class DraftAuditService : IDraftAuditService
{
    private readonly ILogger<DraftAuditService> _logger;

    public DraftAuditService(ILogger<DraftAuditService> logger)
    {
        _logger = logger;
    }

    public Task EmitAsync(DraftAuditEvent auditEvent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var appendRequest = EmployeeAuditAdapterMapper.MapDraftEvent(auditEvent);

        _logger.LogInformation(
            "HCM non-authoritative audit fallback {RequestType} tenant={TenantId} entity={EntityType}/{EntityId} operation={Operation} outcome={Outcome} metadata={Metadata}",
            appendRequest.RequestType,
            appendRequest.TargetTenantId,
            appendRequest.EntityType,
            appendRequest.EntityId,
            appendRequest.Operation,
            appendRequest.Outcome,
            appendRequest.Metadata);

        return Task.CompletedTask;
    }
}
