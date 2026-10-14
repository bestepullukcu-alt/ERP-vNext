namespace Diten.InventoryService.Domain.Enums;

/// <summary>Frozen <c>ReservationResponse.status</c> values (ACTIVE / RELEASED / EXPIRED). There is no CONSUMED value:
/// a fully consumed reservation is RELEASED (pack ASSUMPTION-0173-15).</summary>
public enum ReservationStatus
{
    Active,
    Released,
    Expired
}
