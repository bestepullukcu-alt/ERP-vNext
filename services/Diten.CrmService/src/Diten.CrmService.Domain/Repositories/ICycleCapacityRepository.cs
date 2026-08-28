using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>
/// MOD-0155 FU06 CycleCapacity store — ONE collection, tenant scoped, soft-delete aware. There is deliberately
/// <b>no delete method</b>: retiring a capacity is the soft archive, so the inputs an old estimate was made from stay
/// readable.
/// <para>The aggregate is a SINGLE document (the month rows are embedded), so every write is a single-document
/// operation and no multi-document transaction — and therefore no compensation on a standalone dev Mongo — is ever
/// needed.</para>
/// </summary>
public interface ICycleCapacityRepository
{
    Task<CycleCapacity?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    /// <summary>The capacity pinned to one period, or null. This is the 1:1 lookup the deep-link resolver and the
    /// duplicate guard both use, so "is there already one?" is decided in exactly one place.</summary>
    Task<CycleCapacity?> GetByCyclePeriodAsync(
        Guid tenantId, Guid cyclePeriodId, CancellationToken cancellationToken);

    /// <summary>Every non-deleted capacity of the tenant, archived ones included — the caller filters, because
    /// "archived" is a view choice rather than a storage one.</summary>
    Task<IReadOnlyList<CycleCapacity>> ListAsync(Guid tenantId, CancellationToken cancellationToken);

    Task InsertAsync(CycleCapacity entity, CancellationToken cancellationToken);

    /// <summary>Optimistic replace: matches on (Id, TenantId, Version == expectedVersion) and bumps the token. Returns
    /// false on a concurrency mismatch so the handler can answer 409 instead of overwriting silently.</summary>
    Task<bool> ReplaceAsync(CycleCapacity entity, int expectedVersion, CancellationToken cancellationToken);
}
