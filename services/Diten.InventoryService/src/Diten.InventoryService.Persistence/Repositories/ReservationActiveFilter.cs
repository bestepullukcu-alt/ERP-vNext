using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Repositories;

/// <summary>
/// MOD-0173-FU03 §5 / §8.5 "Vade kuralı" — THE one place of the read rule "this reservation still holds stock": <c>Status == ACTIVE</c>
/// and (<c>ExpiresAt == null</c> or <c>ExpiresAt &gt; now</c>). The projection's <c>Reserved</c> keeps a due reservation until
/// <c>ExpireDueAsync</c> closes it; every READ of reserved quantities (availability, scope figures, the port's free quantity, the
/// lot reader of DL-3) applies this filter instead, so a due but unclosed reservation is never counted.
/// </summary>
internal static class ReservationActiveFilter
{
    public static FilterDefinition<InventoryReservation> At(DateTimeOffset now)
    {
        var f = Builders<InventoryReservation>.Filter;
        return f.Eq(x => x.Status, ReservationStatus.Active)
               & (f.Eq(x => x.ExpiresAt, null) | f.Gt(x => x.ExpiresAt, (DateTimeOffset?)now));
    }
}
