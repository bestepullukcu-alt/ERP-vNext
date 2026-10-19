using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

// MOD-0290-FU04 S2 (S1 carry-over c): the IT- codes held with no item row. Read-only; the reservations' own collection
// and the items' — a reservation is an orphan only when no item (deleted or not) carries its id.
public sealed class ItemOrphanCodeCensus(IMongoDatabase database) : IItemOrphanCodeCensus
{
    // A census reads at most this many candidates per call (the gauge says "at least" beyond it — never unbounded).
    public const int CandidateScanLimit = 5000;

    private readonly IMongoCollection<CodeReservation> _reservations = database.GetCollection<CodeReservation>("mdm_code_reservations");
    private readonly IMongoCollection<Item> _items = database.GetCollection<Item>(ItemRepository.CollectionName);

    private static FilterDefinition<CodeReservation> Held(DateTimeOffset olderThan)
    {
        var f = Builders<CodeReservation>.Filter;
        return f.Eq(x => x.EntityType, CodeBearingEntityType.Item) & f.Ne(x => x.IsDeleted, true)
               & (f.Eq(x => x.BindingState, CodeReservationBindingState.PendingIdentityWrite)
                  | f.Eq(x => x.BindingState, CodeReservationBindingState.None)
                    & f.Eq(x => x.ReservationState, CodeReservationState.Reserved))
               & f.Lt(x => x.ReservedAt, olderThan);
    }

    public async Task<long> CountOrphanItemCodesAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default)
    {
        var candidates = await _reservations.Find(Held(olderThan)).Limit(CandidateScanLimit)
            .Project(x => new { x.Id, x.TenantId }).ToListAsync(cancellationToken);
        if (candidates.Count == 0) return 0;
        var bound = await BoundAsync(candidates.Select(x => x.Id).ToArray(), cancellationToken);
        return candidates.LongCount(x => !bound.Contains(x.Id));
    }

    public async Task<IReadOnlyList<CodeReservation>> FindOrphanItemCodesAsync(Guid tenantId, DateTimeOffset olderThan,
        int limit, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        var candidates = await _reservations.Find(Held(olderThan) & Builders<CodeReservation>.Filter.Eq(x => x.TenantId, tenantId))
            .SortBy(x => x.ReservedAt).ThenBy(x => x.Id).Limit(limit * 4).ToListAsync(cancellationToken);
        if (candidates.Count == 0) return [];
        var bound = await BoundAsync(candidates.Select(x => x.Id).ToArray(), cancellationToken);
        return candidates.Where(x => !bound.Contains(x.Id)).Take(limit).ToList();
    }

    private async Task<HashSet<Guid>> BoundAsync(Guid[] reservationIds, CancellationToken cancellationToken) =>
        (await _items.Find(Builders<Item>.Filter.In(x => x.CodeReservationId, reservationIds))
            .Project(x => x.CodeReservationId).ToListAsync(cancellationToken)).ToHashSet();
}
