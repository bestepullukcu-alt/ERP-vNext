namespace Diten.InventoryService.Domain.Rules;

/// <summary>
/// MOD-0173-FU03 §8.3.1 (CT-08, r3 boundary): a REVERSAL that asked for <c>restoreReservation: true</c> of a movement that consumed
/// <paramref name="ReservationId"/> by <c>reservationId</c> gives <paramref name="Quantity"/> (the original's base quantity) back to it in the
/// SAME posting transaction. <paramref name="ActorDisplayName"/> is written on the <c>InventoryReservationRestored</c> row (§4.4).
/// </summary>
public sealed record ReservationRestore(Guid ReservationId, decimal Quantity, string? ActorDisplayName);
