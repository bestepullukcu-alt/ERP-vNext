namespace Diten.InventoryService.Domain.Repositories;

public enum ReservationReleaseOutcome
{
    Released,

    /// <summary>No reservation with this id in the caller's Tenant + LE.</summary>
    NotFound,

    /// <summary>ETag mismatch, or the reservation is no longer ACTIVE (released / expired).</summary>
    Conflict
}
