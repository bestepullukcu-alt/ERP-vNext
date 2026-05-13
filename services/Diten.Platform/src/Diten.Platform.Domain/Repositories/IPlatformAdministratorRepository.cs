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
