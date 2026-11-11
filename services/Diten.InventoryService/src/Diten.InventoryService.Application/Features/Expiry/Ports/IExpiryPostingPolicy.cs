using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Expiry.Entities;
using Diten.InventoryService.Domain.Expiry.Rules;
using Diten.InventoryService.Domain.Traceability.Entities;

namespace Diten.InventoryService.Application.Features.Expiry.Ports;

/// <summary>One posting as the expiry hooks see it at pre-check (a registered lot is known; REVERSAL never gets here).</summary>
public sealed record ExpiryPostingRequest(
    MovementType MovementType,
    StockStatus? FromStockStatus,
    StockStatus? ToStockStatus,
    string? FromWarehouseId,
    string? ToWarehouseId,
    Guid ItemId,
    Guid SkuId,
    SkuLevel SkuLevel,
    string SourceModule,
    Lot Lot,
    DateTimeOffset PostingDate,
    string? FefoBypassReason,
    int? MinRemainingShelfLifeDays);

/// <summary>A hook refusal: INVENTORY 1.3.0 422 code + message + details.</summary>
public sealed record ExpiryRefusal(string Code, string Message, IReadOnlyDictionary<string, object?>? Details);

/// <summary>The pre-check verdict: refused, or the input the in-transaction re-check works from (null = no hook).</summary>
public sealed record ExpiryPreCheck(ExpiryRefusal? Refusal, ExpiryPostingInput? Input)
{
    public static readonly ExpiryPreCheck None = new(null, null);
}

/// <summary>The authoritative in-transaction verdict: refused, or the checks the response carries (null = no hook).</summary>
public sealed record ExpiryReCheck(ExpiryRefusal? Refusal, ExpiryChecks? Checks)
{
    public static readonly ExpiryReCheck None = new(null, null);
}

/// <summary>
/// MOD-0176 F2 (CT-F2) — THE port of the expiry hooks in the MOD-0173 posting path (the <c>ILotStatusChangeGate</c> pattern): the
/// ledger registers <see cref="AllowAllExpiryPostingPolicy"/>, MOD-0176 replaces it.
/// <list type="number">
/// <item><see cref="PreCheckAsync"/> — outside the transaction, in <c>MovementMasterDataValidator</c> right after the lot rules: a
/// refusal answers 422 before anything is written; otherwise the decided input travels on <c>LotPostingContext.Expiry</c>.</item>
/// <item><see cref="ReCheckAsync"/> — INSIDE the posting transaction (the ledger's session is bound), after the lot is re-read: the
/// AUTHORITATIVE decision on the committed state; it writes the FEFO snapshot and the exception in that transaction, so they commit
/// with the movement or not at all. A refusal aborts the transaction (same 422).</item>
/// <item><see cref="FlushRefusalAuditAsync"/> — after a refused posting (pre-check or re-check): the refusal audit row
/// (<c>expiry.fefo.refused</c> / <c>expiry.receipt.refused</c>), outside the aborted transaction.</item>
/// </list>
/// </summary>
public interface IExpiryPostingPolicy
{
    Task<ExpiryPreCheck> PreCheckAsync(ExpiryPostingRequest request, CancellationToken cancellationToken = default);

    Task<ExpiryReCheck> ReCheckAsync(InventoryTransaction transaction, Lot? lot, ExpiryPostingInput? input, CancellationToken cancellationToken = default);

    Task FlushRefusalAuditAsync(CancellationToken cancellationToken = default);
}

/// <summary>The ledger's default: no expiry hook (every posting passes; no checks on the response).</summary>
public sealed class AllowAllExpiryPostingPolicy : IExpiryPostingPolicy
{
    public Task<ExpiryPreCheck> PreCheckAsync(ExpiryPostingRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(ExpiryPreCheck.None);

    public Task<ExpiryReCheck> ReCheckAsync(InventoryTransaction transaction, Lot? lot, ExpiryPostingInput? input, CancellationToken cancellationToken = default)
        => Task.FromResult(ExpiryReCheck.None);

    public Task FlushRefusalAuditAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
