using Diten.Platform.Application.Common;

namespace Diten.Platform.Application.Security;

/// <summary>
/// Admin Safety Guardrails (master-plan §7.21). Enforces self-action and
/// last-SuperAdmin invariants for commands that mutate platform administrator
/// records, roles, permissions, or tenant scope.
/// </summary>
public interface IActorSafetyGuard
{
    /// <summary>
    /// Rule 1, 3, 5, 6 — refuses when the current user is the target.
    /// Returns <c>null</c> when the action is permitted.
    /// </summary>
    Task<Response<NoContent>?> EnsureNotSelfAsync(
        Guid targetActorId,
        AdminSafetyAction action,
        CancellationToken ct = default);

    /// <summary>
    /// Rule 2 — refuses delete/suspend/role-remove that would leave the system
    /// with zero active SuperAdmins. Returns <c>null</c> when the action is permitted.
    /// </summary>
    Task<Response<NoContent>?> EnsureNotLastActiveSuperAdminAsync(
        Guid targetActorId,
        AdminSafetyAction action,
        CancellationToken ct = default);

    /// <summary>
    /// The current user's own administrator record is the target (resolved the way <see cref="EnsureNotSelfAsync"/>
    /// resolves it). BL-521 FIX2 item 5 — for the update path, which refuses only SOME self changes.
    /// </summary>
    Task<bool> IsSelfAsync(Guid targetActorId, CancellationToken ct = default);

    /// <summary>
    /// Rule 4 — silently removes the current user id from a bulk target list.
    /// Result.EffectiveTargets contains the ids the handler should still act on;
    /// Result.SkippedSelfIds contains what was filtered out (always 0 or 1 entries).
    /// </summary>
    Task<BulkSafetyResult> FilterSelfFromBulkAsync(
        IReadOnlyCollection<Guid> targetActorIds,
        CancellationToken ct = default);
}

/// <summary>BL-521 FIX2 item 5 — the stable reason codes of the administrator safety refusals.</summary>
public static class AdminSafetyReasonCodes
{
    /// <summary>403 — an administrator tried to change its own actor type, status or e-mail.</summary>
    public const string SelfChangeForbidden = "PLATFORM_ADMINISTRATOR_SELF_CHANGE_FORBIDDEN";

    /// <summary>409 — the change would leave no active platform administrator holding SuperAdmin.</summary>
    public const string LastActiveSuperAdmin = "PLATFORM_ADMINISTRATOR_LAST_ACTIVE_SUPER_ADMIN";
}
