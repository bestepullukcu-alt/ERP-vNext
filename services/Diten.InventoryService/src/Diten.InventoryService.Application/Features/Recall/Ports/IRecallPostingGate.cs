using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Traceability.Entities;

namespace Diten.InventoryService.Application.Features.Recall.Ports;

/// <summary>
/// One posting as the recall exit block sees it at pre-check (pack §8.5). There is deliberately NO <c>sourceModule</c>: the exemption is
/// decided by the movement TYPE only (CT-R11, 0176 O-F2-2) — caller data never opens the block.
/// <paramref name="LotId"/> is the movement's lot text: a registered lot (<paramref name="Lot"/> set) or a typed lot (matched by number
/// within the same SKU, ASSUMPTION-0177-06).
/// </summary>
public sealed record RecallPostingRequest(
    MovementType MovementType,
    StockStatus? FromStockStatus,
    StockStatus? ToStockStatus,
    string LotId,
    Lot? Lot,
    Guid SkuId,
    SkuLevel SkuLevel,
    string IdempotencyKey);

/// <summary>A recall refusal: INVENTORY 1.4.0 422 <c>LOT_UNDER_RECALL</c> + message + details.</summary>
public sealed record RecallPostingRefusal(string Code, string Message, IReadOnlyDictionary<string, object?> Details);

/// <summary>
/// MOD-0177 R3 (pack §8.5, KR-C, CT-R11) — THE port of the recall exit block in the MOD-0173 posting path, separate from 0176's
/// <c>IExpiryPostingPolicy</c> and evaluated BEFORE it (a refused posting opens no FEFO exception). The ledger registers
/// <see cref="AllowAllRecallPostingGate"/>; MOD-0177 replaces it.
/// <list type="number">
/// <item><see cref="PreCheckAsync"/> — outside the transaction, in <c>MovementMasterDataValidator</c> right after the lot rules: a refusal
/// answers 422 before anything is written.</item>
/// <item><see cref="ReCheckAsync"/> — INSIDE the posting transaction (the ledger's session is bound), right after the lot is re-read: the
/// AUTHORITATIVE decision on the committed blocks; a refusal aborts the transaction (same 422).</item>
/// </list>
/// Each refusal is recorded by the gate itself, outside the posting transaction (case timeline <c>ExitRefused</c> + audit
/// <c>recall.exit.refused</c>).
/// </summary>
public interface IRecallPostingGate
{
    Task<RecallPostingRefusal?> PreCheckAsync(RecallPostingRequest request, CancellationToken cancellationToken = default);

    Task<RecallPostingRefusal?> ReCheckAsync(InventoryTransaction transaction, Lot? lot, CancellationToken cancellationToken = default);
}

/// <summary>The ledger's default: no recall module — every posting passes.</summary>
public sealed class AllowAllRecallPostingGate : IRecallPostingGate
{
    public Task<RecallPostingRefusal?> PreCheckAsync(RecallPostingRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult<RecallPostingRefusal?>(null);

    public Task<RecallPostingRefusal?> ReCheckAsync(InventoryTransaction transaction, Lot? lot, CancellationToken cancellationToken = default)
        => Task.FromResult<RecallPostingRefusal?>(null);
}
