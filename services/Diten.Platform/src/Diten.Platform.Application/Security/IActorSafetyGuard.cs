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
    /// Rule 4 — silently removes the current user id from a bulk target list.
    /// Result.EffectiveTargets contains the ids the handler should still act on;
    /// Result.SkippedSelfIds contains what was filtered out (always 0 or 1 entries).
    /// </summary>
    Task<BulkSafetyResult> FilterSelfFromBulkAsync(
        IReadOnlyCollection<Guid> targetActorIds,
        CancellationToken ct = default);
}
