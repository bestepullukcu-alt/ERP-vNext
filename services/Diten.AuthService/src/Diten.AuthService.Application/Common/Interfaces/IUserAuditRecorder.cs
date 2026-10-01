namespace Diten.AuthService.Application.Common.Interfaces;

/// <summary>
/// BL-456 — records one user-lifecycle event in BOTH logs: the local <c>authAuditLogs</c> row (through
/// <see cref="IRbacAuditRecorder"/>, the same writer the role events already use) and Platform's central audit log
/// (through <see cref="IPlatformAuditForwarder"/>, category IdentityAccess, entity "User", entity id = the user the
/// event is about). Local first, then Platform: the local row does not depend on Platform being up.
/// <para><paramref name="eventName"/> must be one of <c>UserAuditEvents</c>; <paramref name="metadata"/> carries ids,
/// keys and changed-field names only — no e-mail, no name (the recorder's no-PII rule). NEVER throws.</para>
/// </summary>
public interface IUserAuditRecorder
{
    Task RecordAsync(
        string eventName,
        Guid tenantId,
        Guid targetUserId,
        IReadOnlyDictionary<string, object?> metadata,
        CancellationToken ct = default);
}
