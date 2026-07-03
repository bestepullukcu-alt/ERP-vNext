namespace Diten.AuthService.Application.Common.Interfaces;

/// <summary>
/// FEAT-AUDIT-RBAC — records an RBAC-mutation audit event (who changed which role / permission / assignment) to
/// <c>authAuditLogs</c>. The actor is resolved from the current request; <paramref name="metadata"/> is serialized
/// to JSON with an added <c>actorId</c> (IDs + role/permission keys only — no PII). NEVER throws: a failed audit
/// write is logged (error) and the mutation stands — auditing must not break the operation, nor be silently swallowed.
/// </summary>
public interface IRbacAuditRecorder
{
    Task RecordAsync(string eventName, Guid tenantId, object metadata, CancellationToken ct = default);
}
