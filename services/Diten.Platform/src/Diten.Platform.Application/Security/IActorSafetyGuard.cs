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
    /// <summary>403 — an administrator tried to change its own actor type, status, e-mail or (FIX3 item 1) roles.</summary>
    public const string SelfChangeForbidden = "PLATFORM_ADMINISTRATOR_SELF_CHANGE_FORBIDDEN";

    /// <summary>409 — the change would leave no active platform administrator holding SuperAdmin.</summary>
    public const string LastActiveSuperAdmin = "PLATFORM_ADMINISTRATOR_LAST_ACTIVE_SUPER_ADMIN";

    /// <summary>409 — FIX3 item 3: another change that could take a platform super administrator out of the set was being
    /// written at the same moment; nothing was written, reload and retry.</summary>
    public const string ConcurrentSuperAdminChange = "PLATFORM_ADMINISTRATOR_CONCURRENT_SUPER_ADMIN_CHANGE";

    /// <summary>409 — FIX3 item 2(c): the system seed administrator is not suspended, deleted or moved to another e-mail.</summary>
    public const string SystemSeedProtected = "PLATFORM_ADMINISTRATOR_SYSTEM_SEED_PROTECTED";

    /// <summary>503 — FIX3 item 3: the guarded write could not run (no transaction support, the database unreachable);
    /// nothing was written.</summary>
    public const string SafetyCheckUnavailable = "PLATFORM_ADMINISTRATOR_SAFETY_CHECK_UNAVAILABLE";
}

/// <summary>
/// The English source sentences of the administrator safety refusals. The administrators screen shows them in the user's
/// language by matching the sentence (Web AdministratorsController.LocalizeErrorMessage for the edit form; index.js
/// localizeServerError for the list actions — the two FIX3 sentences are mapped in the controller only), so a sentence
/// here is changed only together with that mapping.
/// </summary>
public static class AdminSafetyMessages
{
    public const string LastActiveSuperAdmin = "At least one active Super Admin must remain in the system.";
    public const string ChangedByAnotherUser = "The administrator was changed by another user. Reload and try again.";
    public const string SeedCannotBeSuspended = "The system seed administrator cannot be suspended.";
    public const string SeedCannotBeDeleted = "The system seed administrator cannot be deleted.";

    /// <summary>FIX3 item 2(c) — new; the screen's en + tr text: SharedResource AdminSeedEmailChangeDenied.</summary>
    public const string SeedEmailCannotChange = "The system seed administrator's e-mail cannot be changed.";

    /// <summary>FIX3 item 3 — new; the screen's en + tr text: SharedResource AdminChangeUnavailable.</summary>
    public const string SafetyCheckUnavailable = "The administrator change could not be saved right now. Nothing was written. Please try again.";
}
