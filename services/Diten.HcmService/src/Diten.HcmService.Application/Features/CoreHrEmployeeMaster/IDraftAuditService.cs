namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster;

public interface IDraftAuditService
{
    Task EmitAsync(DraftAuditEvent auditEvent, CancellationToken cancellationToken);
}

public sealed record DraftAuditEvent(
    string EventName,
    Guid TenantId,
    string? ActorId,
    Guid DraftSessionId,
    string? CorrelationId,
    string IdempotencyKeyHash,
    IReadOnlyDictionary<string, string> Metadata);
