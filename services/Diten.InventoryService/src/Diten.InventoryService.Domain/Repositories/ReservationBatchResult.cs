using Diten.InventoryService.Domain.Entities;

namespace Diten.InventoryService.Domain.Repositories;

/// <summary>
/// MOD-0173-FU03 §8.7 <c>CreateBatchAsync</c>: all lines in ONE Mongo transaction, all or nothing. <see cref="Created"/> → every
/// reservation committed (<see cref="Reservations"/> in line order); <see cref="ReservationCreateOutcome.InsufficientAvailable"/> →
/// nothing written, <see cref="RefusedIndex"/> names the first line whose scope did not cover it; <see cref="ReservationCreateOutcome.IdempotencyKeyTaken"/>
/// → a concurrent request committed one of the keys first, nothing written.
/// </summary>
public sealed record ReservationBatchResult(
    ReservationCreateOutcome Outcome,
    IReadOnlyList<InventoryReservation> Reservations,
    int? RefusedIndex = null,
    decimal? Available = null,
    decimal? Requested = null,
    string? Level = null)
{
    public bool Created => Outcome == ReservationCreateOutcome.Created;
}
