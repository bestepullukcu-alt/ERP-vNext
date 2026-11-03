using Diten.InventoryService.Domain.Entities;

namespace Diten.InventoryService.Domain.Rules;

public sealed record LedgerPostingResult(
    LedgerPostingOutcome Outcome,
    InventoryTransaction? Transaction = null,
    decimal? Available = null,
    decimal? Requested = null,
    string? RefusedLocationId = null,
    string? RefusalCode = null,
    string? RefusalMessage = null,
    IReadOnlyDictionary<string, object?>? RefusalDetails = null,
    string? Level = null)
{
    public static LedgerPostingResult Posted(InventoryTransaction transaction) => new(LedgerPostingOutcome.Posted, transaction);

    /// <param name="level">MOD-0173-FU03 §8.2: the reservation scope level whose guard refused (<c>SKU</c> | <c>WAREHOUSE</c> | <c>LOT</c>);
    /// null when a balance row itself did not cover the leg (negative-stock guard).</param>
    public static LedgerPostingResult Insufficient(decimal available, decimal requested, string? level = null)
        => new(LedgerPostingOutcome.InsufficientStock, Available: available, Requested: requested, Level: level);

    public static LedgerPostingResult KeyTaken() => new(LedgerPostingOutcome.IdempotencyKeyTaken);

    public static LedgerPostingResult Reversed() => new(LedgerPostingOutcome.AlreadyReversed);

    public static LedgerPostingResult NotPostable(string locationId)
        => new(LedgerPostingOutcome.LocationNotPostable, RefusedLocationId: locationId);

    public static LedgerPostingResult LotOrSerialRefused(string code, string message, IReadOnlyDictionary<string, object?>? details)
        => new(LedgerPostingOutcome.LotOrSerialRefused, RefusalCode: code, RefusalMessage: message, RefusalDetails: details);
}
