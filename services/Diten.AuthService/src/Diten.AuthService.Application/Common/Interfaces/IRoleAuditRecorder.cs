namespace Diten.AuthService.Application.Common.Interfaces;

/// <summary>
/// WP-ROLES-CLOSE-01 — records one role event in BOTH logs: the local <c>authAuditLogs</c> row (through
/// <see cref="IRbacAuditRecorder"/>, unchanged — same event name, same fields) and Platform's central audit log (through
/// <see cref="IPlatformAuditForwarder"/>, category IdentityAccess, entity "Role", entity id = the role the event is
/// about). Local first, then Platform: the local row does not depend on Platform being up.
/// <para><paramref name="eventName"/> must be one of <c>RoleAuditEvents</c>; <paramref name="metadata"/> carries ids,
/// role names, permission keys and before/after of the role's own fields — no secret, no token, no personal data.
/// NEVER throws.</para>
/// </summary>
public interface IRoleAuditRecorder
{
    Task RecordAsync(
        string eventName,
        Guid tenantId,
        Guid roleId,
        IReadOnlyDictionary<string, object?> metadata,
        CancellationToken ct = default);
}
