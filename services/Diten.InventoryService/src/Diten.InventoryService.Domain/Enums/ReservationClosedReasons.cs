namespace Diten.InventoryService.Domain.Enums;

/// <summary>
/// MOD-0173-FU03 §4.1 — why a reservation is no longer ACTIVE. The wire <c>status</c> enum is unchanged: a full consumption
/// stays RELEASED and says <see cref="Consumed"/> here (ASSUMPTION-0173-FU03-02).
/// </summary>
public static class ReservationClosedReasons
{
    /// <summary>Explicit <c>releaseReservation</c> of the remaining quantity.</summary>
    public const string Released = "Released";

    /// <summary>The remaining quantity was issued (full consumption).</summary>
    public const string Consumed = "Consumed";

    /// <summary><c>expiresAt</c> passed (sweeper or in-transaction expiry).</summary>
    public const string Expired = "Expired";
}
