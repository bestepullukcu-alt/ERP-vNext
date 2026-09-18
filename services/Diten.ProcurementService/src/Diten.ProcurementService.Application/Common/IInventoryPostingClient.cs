namespace Diten.ProcurementService.Application.Common;

/// <summary>
/// INVENTORY (MOD-0173) consume seam'ine gönderilen hareket isteği — frozen INVENTORY-BUNDLE
/// <c>POST /api/inventory/movements</c> (MovementRequest) ŞEKLİNE map edilir. CONSUMED tip (MovementType/MovementRequest)
/// burada REDEFINE EDİLMEZ (K16): <see cref="MovementType"/> canonical string olarak taşınır (GOODS_RECEIPT_PO /
/// SUPPLIER_RETURN / REVERSAL), enum kopyalanmaz. quantity Decimal string (float YASAK).
/// </summary>
public sealed record InventoryMovementRequest(
    string IdempotencyKey,
    string MovementType,
    string ItemId,
    string SkuId,
    string SkuLevel,
    string? WarehouseId,
    string? LocationId,
    string Quantity,
    string UomId,
    string? LotNumber,
    IReadOnlyList<string>? SerialIds,
    string ToStockStatus,
    string SourceModule,
    string SourceType,
    string? SourceDocumentId,
    string? SourceLineId);

/// <summary>
/// INVENTORY <c>POST /movements</c> sonucu (MovementResponse) — GRN yalnız <see cref="TransactionId"/> ve
/// <see cref="LotId"/> referanslarını saklar; balance/ledger DEĞİL (no-shadow-stock).
/// </summary>
public sealed record InventoryMovementResult(
    string TransactionId,
    string? LotId,
    bool IdempotentReplay);

/// <summary>
/// INVENTORY (MOD-0173) posting seam'i — stok değiştiren TEK yol frozen INVENTORY-BUNDLE <c>POST /movements</c>'tır
/// (append-only, idempotent). GRN her satırı bu seam üzerinden post eder (GOODS_RECEIPT_PO / reverse için
/// REVERSAL|SUPPLIER_RETURN) ve dönen <c>transactionId</c>'yi SALT REFERANS olarak saklar. SHADOW STOCK YASAK: GRN
/// kendi stok balance'ını tutmaz; envanter gerçeği MOD-0173'te kalır (MOD-0142 §2/§8). MOD-0173 bu dilimde bağlı
/// değil: entegrasyon progressive (mock şimdi, gerçek HTTP client sonra — GRN kodu değişmez, contract sınırı AD-2/§8).
/// IProductReferenceValidator seam deseninin aynısı (interface + varsayılan implementasyon; DI'da register edilir).
/// </summary>
public interface IInventoryPostingClient
{
    Task<InventoryMovementResult> PostMovementAsync(InventoryMovementRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Varsayılan (progressive-integration) implementasyon: MOD-0173 bu dilimde bağlı DEĞİL. Çalışan bir 0173 OLMADAN
/// deterministik bir <c>inventoryTransactionId</c> üretir (aynı idempotencyKey → aynı id; replay-safe). Gerçek
/// MOD-0173 HTTP client'ı SONRAKİ bir wiring'dir — bu kayıt değiştirilir, GRN kodu DEĞİŞMEZ (contract sınırı).
/// Bu mock hiçbir stok balance TUTMAZ; yalnız frozen MovementResponse şeklinin transactionId/lotId alanlarını taklit
/// eder (no-shadow-stock korunur — mock bile balance sahibi değildir).
/// </summary>
public sealed class MockInventoryPostingClient : IInventoryPostingClient
{
    public Task<InventoryMovementResult> PostMovementAsync(InventoryMovementRequest request, CancellationToken cancellationToken = default)
    {
        // Deterministik id: aynı idempotencyKey → aynı transactionId (0173'ün idempotent replay davranışını taklit eder).
        var transactionId = "txn-" + Deterministic(request.IdempotencyKey);
        // lotNumber verildiyse deterministik lot referansı (TRACE createLot sonucunu taklit); yoksa null.
        var lotId = string.IsNullOrWhiteSpace(request.LotNumber) ? null : "lot-" + Deterministic(request.LotNumber!);
        return Task.FromResult(new InventoryMovementResult(transactionId, lotId, IdempotentReplay: false));
    }

    private static string Deterministic(string seed)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(seed));
        return Convert.ToHexString(bytes)[..12].ToLowerInvariant();
    }
}
