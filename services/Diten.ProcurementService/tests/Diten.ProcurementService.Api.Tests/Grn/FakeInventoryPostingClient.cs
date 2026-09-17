using Diten.ProcurementService.Application.Common;

namespace Diten.ProcurementService.Api.Tests.Grn;

/// <summary>
/// Test double for the INVENTORY (MOD-0173) posting seam. Records every posted MovementRequest so tests can pin the
/// G2A / no-shadow-stock rule: recordGrn MUST post each line to the INVENTORY seam with movementType GOODS_RECEIPT_PO
/// and persist the returned transactionId — the GRN itself holds NO stock balance (inventory truth stays in 0173).
/// The returned transactionId is DETERMINISTIC per idempotencyKey (mirrors the production MockInventoryPostingClient
/// and 0173's idempotent-replay semantics), so an idempotency replay that re-posted would be visible as a second
/// call with the same key. <see cref="Calls"/> lets tests assert "called once per unique Idempotency-Key".
/// </summary>
public sealed class FakeInventoryPostingClient : IInventoryPostingClient
{
    public List<InventoryMovementRequest> Calls { get; } = new();

    public int CallCount => Calls.Count;

    public Task<InventoryMovementResult> PostMovementAsync(InventoryMovementRequest request, CancellationToken cancellationToken = default)
    {
        Calls.Add(request);
        var transactionId = "txn-" + Deterministic(request.IdempotencyKey);
        var lotId = string.IsNullOrWhiteSpace(request.LotNumber) ? null : "lot-" + Deterministic(request.LotNumber!);
        return Task.FromResult(new InventoryMovementResult(transactionId, lotId, IdempotentReplay: false));
    }

    private static string Deterministic(string seed)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(seed));
        return Convert.ToHexString(bytes)[..12].ToLowerInvariant();
    }
}
