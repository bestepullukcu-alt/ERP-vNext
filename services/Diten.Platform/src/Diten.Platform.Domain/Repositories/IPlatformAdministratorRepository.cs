using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Repositories;

public interface IPlatformAdministratorRepository
{
    Task<PlatformAdministrator> CreateAsync(PlatformAdministrator administrator, CancellationToken ct = default);
    Task<PlatformAdministrator?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PlatformAdministrator?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(string normalizedEmail, Guid? excludeId = null, CancellationToken ct = default);
    Task<bool> ExistsByUserNameAsync(string normalizedUserName, Guid? excludeId = null, CancellationToken ct = default);
    Task<bool> UpdateAsync(PlatformAdministrator administrator, int expectedVersion, CancellationToken ct = default);
    Task<bool> SoftDeleteAsync(Guid id, int expectedVersion, string actorName, CancellationToken ct = default);
    Task<(IReadOnlyList<PlatformAdministrator> Items, long TotalCount)> QueryAsync(PlatformAdministratorQuery query, CancellationToken ct = default);
    Task<PlatformAdministratorStatsSnapshot> GetStatsAsync(CancellationToken ct = default);

    /// <summary>
    /// Counts active administrators with the SuperAdmin role, excluding soft-deleted records
    /// and optionally excluding a single record by id. Used by the Admin Safety Guard
    /// (master-plan §7.21) to enforce the "last SuperAdmin" invariant.
    /// </summary>
    Task<long> CountActiveSuperAdminsAsync(Guid? excludeId = null, CancellationToken ct = default);

    /// <summary>
    /// BL-521 FIX3 item 3 — THE write for a change that takes an active platform super administrator out of the set
    /// (demote, drop SuperAdmin, suspend / disable). The count of the OTHER active platform super administrators and the
    /// write happen in ONE transaction that first claims a single shared guard document, so two such writes running at once
    /// cannot both pass on a count the other is about to invalidate (A demotes B while B demotes A): the second one's claim
    /// conflicts and it is refused (<see cref="SuperAdminGuardedWriteResult.ConcurrentChange"/>), or — when the first has
    /// already committed — its count sees the result (<see cref="SuperAdminGuardedWriteResult.LastActiveSuperAdmin"/>).
    /// Conditional on <paramref name="expectedVersion"/>, like <see cref="UpdateAsync"/>. FIX4 item 4 — a commit whose
    /// result never came back is decided by reading the record again.
    /// </summary>
    Task<SuperAdminGuardedWriteResult> UpdateGuardedAsync(PlatformAdministrator administrator, int expectedVersion, CancellationToken ct = default);

    /// <summary>The soft delete counterpart of <see cref="UpdateGuardedAsync"/>.</summary>
    Task<SuperAdminGuardedWriteResult> SoftDeleteGuardedAsync(Guid id, int expectedVersion, string actorName, CancellationToken ct = default);

    /// <summary>
    /// BL-521 FIX4 item 2 — sets (<paramref name="pending"/> true: keeps the first moment) or clears
    /// <see cref="PlatformAdministrator.AuthSyncPendingSinceUtc"/>. Does not move the record's version.
    /// </summary>
    Task MarkAuthSyncPendingAsync(Guid id, bool pending, CancellationToken ct = default);
}

/// <summary>BL-521 FIX3 item 3 — the outcome of a write under the super-admin guard. Every outcome but
/// <see cref="Written"/> wrote nothing.</summary>
public enum SuperAdminGuardedWriteResult
{
    /// <summary>Committed.</summary>
    Written = 1,

    /// <summary>The record is not at the expected version any more (or is gone).</summary>
    VersionConflict = 2,

    /// <summary>No other active platform super administrator would remain.</summary>
    LastActiveSuperAdmin = 3,

    /// <summary>Another write under the guard was in flight at the same moment; reload and retry.</summary>
    ConcurrentChange = 4,

    /// <summary>The transaction could not run (no replica set, the database unreachable), or its commit result stayed
    /// unknown and the record read again does not show the write (FIX4 item 4).</summary>
    Unavailable = 5
}

public sealed record PlatformAdministratorQuery(
    string? Search,
    IReadOnlyCollection<AdministratorStatus>? Statuses,
    IReadOnlyCollection<ActorType>? ActorTypes,
    IReadOnlyCollection<AdministratorRole>? Roles,
    IReadOnlyCollection<AdministratorInvitationStatus>? InvitationStatuses,
    Guid? PartnerId,
    int Page,
    int PageSize,
    string Sort);

public sealed record PlatformAdministratorStatsSnapshot(
    long Total,
    long Active,
    long Suspended,
    long Disabled,
    long PendingInvitation);
