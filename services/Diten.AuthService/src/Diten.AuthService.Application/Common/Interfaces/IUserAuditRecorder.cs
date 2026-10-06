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

    /// <summary>
    /// BL-529 — the same row, with its real outcome for the central log: <paramref name="succeeded"/> false is filed as
    /// Failed (a reset that conflicted or failed mid-way), not as Succeeded. A recorder that does not distinguish
    /// outcomes records it as before.
    /// </summary>
    Task RecordAsync(
        string eventName,
        Guid tenantId,
        Guid targetUserId,
        IReadOnlyDictionary<string, object?> metadata,
        bool succeeded,
        CancellationToken ct)
        => RecordAsync(eventName, tenantId, targetUserId, metadata, ct);
}
