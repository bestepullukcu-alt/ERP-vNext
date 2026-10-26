using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Repositories;

namespace Diten.InventoryService.Application.Features.Traceability.Ports;

/// <summary>TRACE 1.4.0 <c>dateOverride</c> of createLot / updateLot: the prepared override (FEFO-BUNDLE createDateOverride) + its signature.</summary>
public sealed record LotDateOverrideRef(string? OverrideId, string? SignatureId);

/// <summary>The lot dates a createLot / Draft updateLot is about to write, with the SKU's day values (getSku).</summary>
public sealed record LotDateGateRequest(
    Guid ItemId,
    Guid SkuId,
    SkuLevel SkuLevel,
    string LotNumber,
    DateOnly? Manufacturing,
    DateOnly? Expiry,
    DateOnly? Retest,
    int? ShelfLifeDays,
    int? RetestDays,
    LotDateOverrideRef? Override);

public sealed record LotDateGateRefusal(string Code, string Message, int Status, IReadOnlyDictionary<string, object?>? Details = null);

/// <summary>
/// The gate's verdict: refused, or the suggestion (stored on the lot) and — when the dates differ from it — the override to consume in the
/// lot transaction.
/// </summary>
public sealed record LotDateGateDecision(
    LotDateGateRefusal? Refusal,
    DateOnly? SuggestedExpiry,
    DateOnly? SuggestedRetest,
    Guid? OverrideId,
    string? SignatureId,
    string? Reason)
{
    public static readonly LotDateGateDecision Free = new(null, null, null, null, null, null);
}

/// <summary>The override was consumed / expired between the check and the lot transaction (the transaction is aborted, nothing written).</summary>
public sealed class LotDateOverrideConflictException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>
/// MOD-0176 F2 (KF-D, CT-F6, CT-F7) — the lot-date override gate of MOD-0174 createLot and Draft updateLot (the <see cref="ILotStatusChangeGate"/>
/// pattern): MOD-0174 registers <see cref="NoLotDateOverrideGate"/>, MOD-0176 replaces it.
/// <list type="number">
/// <item><see cref="CheckAsync"/> — before the lot transaction: suggestion = manufacturing date + <c>shelfLifeDays</c> / <c>retestDays</c>; a date
/// that differs needs a usable override (422 / 409) and, in mode ReasonAndSignature, a verified signature (400 / 409).</item>
/// <item><see cref="ApplyAsync"/> — inside the lot transaction, after the lot write: consumes the override and opens EXPIRY_DATE_OVERRIDE in the
/// same transaction; a lost race throws <see cref="LotDateOverrideConflictException"/> (aborts).</item>
/// </list>
/// </summary>
public interface ILotDateOverrideGate
{
    Task<LotDateGateDecision> CheckAsync(LotDateGateRequest request, CancellationToken cancellationToken = default);

    Task ApplyAsync(ILotTransaction transaction, Lot lot, LotDateGateDecision decision, CancellationToken cancellationToken = default);
}

/// <summary>MOD-0174's default: no suggestion, no override rule.</summary>
public sealed class NoLotDateOverrideGate : ILotDateOverrideGate
{
    public Task<LotDateGateDecision> CheckAsync(LotDateGateRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(LotDateGateDecision.Free);

    public Task ApplyAsync(ILotTransaction transaction, Lot lot, LotDateGateDecision decision, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
