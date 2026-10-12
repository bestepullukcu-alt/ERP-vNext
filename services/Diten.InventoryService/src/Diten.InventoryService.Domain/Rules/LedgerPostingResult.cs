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
    IReadOnlyDictionary<string, object?>? RefusalDetails = null)
{
    public static LedgerPostingResult Posted(InventoryTransaction transaction) => new(LedgerPostingOutcome.Posted, transaction);

    public static LedgerPostingResult Insufficient(decimal available, decimal requested)
        => new(LedgerPostingOutcome.InsufficientStock, Available: available, Requested: requested);

    public static LedgerPostingResult KeyTaken() => new(LedgerPostingOutcome.IdempotencyKeyTaken);

    public static LedgerPostingResult Reversed() => new(LedgerPostingOutcome.AlreadyReversed);

    public static LedgerPostingResult NotPostable(string locationId)
        => new(LedgerPostingOutcome.LocationNotPostable, RefusedLocationId: locationId);

    public static LedgerPostingResult LotOrSerialRefused(string code, string message, IReadOnlyDictionary<string, object?>? details)
        => new(LedgerPostingOutcome.LotOrSerialRefused, RefusalCode: code, RefusalMessage: message, RefusalDetails: details);
}
