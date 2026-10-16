using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Domain.Repositories;

// MOD-0290-FU04 S2 (S1 carry-over c): IT- code reservations held for an item that was never written — consumed for an
// identity (PendingIdentityWrite) or reserved and never consumed, older than the cutoff, with no item row bound to them.
public interface IItemOrphanCodeCensus
{
    // Over all tenants, a number only (the ITEM_ORPHAN_CODE gauge).
    Task<long> CountOrphanItemCodesAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default);

    // The tenant's own, oldest first (the operator repair).
    Task<IReadOnlyList<CodeReservation>> FindOrphanItemCodesAsync(Guid tenantId, DateTimeOffset olderThan, int limit,
        CancellationToken cancellationToken = default);
}
