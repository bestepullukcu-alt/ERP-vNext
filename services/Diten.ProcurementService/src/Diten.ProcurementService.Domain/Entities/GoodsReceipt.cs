namespace Diten.ProcurementService.Domain.Entities;

/// <summary>
/// GRN yaşam döngüsü durumu (MOD-0142 §4 / grn-event.openapi.yaml GrnResponse.status). Sunucu-güdümlü:
/// Draft → (başarılı INVENTORY post sonrası) Posted → (reverse) Reversed. GRN kaydı EDIT edilmez; düzeltme
/// yalnız yeni reverse hareketiyle (append-only, DEC-INV-07).
/// </summary>
public enum GrnStatus
{
    Draft = 0,
    Posted = 1,
    Reversed = 2
}

/// <summary>
/// SKU seviyesi (grn-event.openapi.yaml SkuLevel). MOD-0290'dan CONSUME edilir — opaque.
/// </summary>
public enum GrnSkuLevel
{
    Gsku = 0,
    Lsku = 1,
    FinishedGood = 2
}

/// <summary>
/// Hedef stok statüsü (grn-event.openapi.yaml GrnLine.toStockStatus). INVENTORY'ye map edilir; GRN yaratmaz.
/// </summary>
public enum GrnStockStatus
{
    AVAILABLE = 0,
    QUALITY_INSPECTION = 1,
    QUARANTINE = 2
}

/// <summary>
/// Embedded GRN satırı (MOD-0142 §4 GrnLine). itemId/skuId/uomId MOD-0290/0048'den CONSUME edilir (opaque).
/// quantity Decimal string (float YASAK) — bu, mal kabul DOKÜMANININ satır miktarıdır (ne teslim alındı), stok
/// balance DEĞİL. <see cref="InventoryTransactionId"/> ve <see cref="LotId"/> INVENTORY/TRACE post SONUCU salt
/// referanslardır; GRN burada hiçbir stok balance/ledger tutmaz (SHADOW STOCK YASAK — envanter tek SoR MOD-0173).
/// </summary>
public sealed class GrnLine
{
    /// <summary>Upstream MOD-0141 PO satır referansı — nullable; yaratılmaz.</summary>
    public string? PoLineId { get; set; }

    /// <summary>MOD-0290 ürün referansı — opaque (fail-closed doğrulanır).</summary>
    public string ItemId { get; set; } = string.Empty;

    /// <summary>MOD-0290 SKU referansı — opaque.</summary>
    public string SkuId { get; set; } = string.Empty;

    public GrnSkuLevel SkuLevel { get; set; } = GrnSkuLevel.Lsku;

    /// <summary>Lot MOD-0174 TRACE createLot ile oluşur; GRN yalnız numarayı verir (lot master'ı saklamaz).</summary>
    public string? LotNumber { get; set; }

    public List<string>? SerialIds { get; set; }

    /// <summary>Decimal string (^-?\d+(\.\d+)?$); float YASAK; > 0. Mal kabul DOKÜMANI miktarı — stok balance DEĞİL.</summary>
    public string Quantity { get; set; } = "0";

    /// <summary>MOD-0048 UoM referansı — opaque.</summary>
    public string UomId { get; set; } = string.Empty;

    public GrnStockStatus ToStockStatus { get; set; } = GrnStockStatus.QUALITY_INSPECTION;

    /// <summary>
    /// INVENTORY <c>POST /movements</c> (GOODS_RECEIPT_PO) SONUCU — SALT REFERANS, balance DEĞİL. Envanter gerçeği
    /// MOD-0173'te; GRN yalnız dönen transaction kimliğini saklar (no-shadow-stock, MOD-0142 §8 / rapor §21.1 rule 3).
    /// </summary>
    public string InventoryTransactionId { get; set; } = string.Empty;

    /// <summary>INVENTORY/TRACE post sonucu lot referansı — nullable; salt referans.</summary>
    public string? LotId { get; set; }
}

/// <summary>
/// İş kuralı ihlali kaydı (MOD-0142 §3 receiving exception; ASSUMPTION-GRN-01). Contract Error kodlarından türetilir
/// (UNKNOWN_ITEM / UNKNOWN_LOCATION / PO mismatch). Kalıcılık modeli freeze kapısında netleşir (additive-safe).
/// </summary>
public sealed class ReceivingException
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
}

/// <summary>
/// GoodsReceived event kaydı (grn-event.openapi.yaml GoodsReceivedEvent). Servis bir event-bus'a bağlı OLMADIĞINDAN
/// (ASSUMPTION-GRN-EVENT) event, GRN dokümanına gömülü append-only outbox kaydı olarak persist edilir; gerçek
/// MOD-0035 event-bus bağlanınca bu kayıtlar publish edilir (kod değişmez, contract sınırı). 0143 Invoice-Match /
/// 0175 QC tüketir. Bu bir stok balance DEĞİL — yalnız yayınlanacak event yükü (inventoryTransactionId referansı dahil).
/// </summary>
public sealed class GoodsReceivedEventRecord
{
    public string GrnId { get; set; } = string.Empty;
    public string? PoId { get; set; }
    public string ItemId { get; set; } = string.Empty;
    public string SkuId { get; set; } = string.Empty;
    public string Quantity { get; set; } = "0";
    public string InventoryTransactionId { get; set; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; set; }
}

/// <summary>
/// Goods Receipt (GRN) SoR entity (MOD-0142). Mal kabul DOKÜMANINI + her satır için INVENTORY'den dönen
/// <see cref="GrnLine.InventoryTransactionId"/> referansını sahiplenir — STOK GERÇEĞİNİ DEĞİL. Envanter tek SoR
/// MOD-0173; GRN ikinci bir balance/ledger AÇMAZ (SHADOW STOCK YASAK, MOD-0142 §2/§8, domain-config boundary).
/// Tenant-owned; her sorgu TenantId + LegalEntityId + IsDeleted=false ile filtrelenir. Soft-delete zorunlu.
/// Idempotent (Idempotency-Key) — replay mükerrer INVENTORY hareketi üretmez.
/// </summary>
public sealed class GoodsReceipt : EntityBase
{
    /// <summary>Public code — server-assigned; unique/tenant+LE (ör. GRN-10001).</summary>
    public string GrnId { get; set; } = string.Empty;

    /// <summary>Upstream MOD-0141 PO referansı — nullable; yaratılmaz.</summary>
    public string? PoId { get; set; }

    /// <summary>consumed LOCATION (MOD-0173/SCE) — required; yaratılmaz.</summary>
    public string WarehouseId { get; set; } = string.Empty;

    /// <summary>consumed LOCATION — nullable.</summary>
    public string? LocationId { get; set; }

    public GrnStatus Status { get; set; } = GrnStatus.Draft;

    public List<GrnLine> Lines { get; set; } = new();

    /// <summary>Receiving exception kayıtları (ASSUMPTION-GRN-01).</summary>
    public List<ReceivingException> ReceivingExceptions { get; set; } = new();

    /// <summary>Yayınlanacak GoodsReceived event'leri — gömülü append-only outbox (ASSUMPTION-GRN-EVENT).</summary>
    public List<GoodsReceivedEventRecord> EmittedEvents { get; set; } = new();

    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;

    // ── Dış besleme (DEC-INV-19) ────────────────────────────────────────────────
    public string? SourceSystem { get; set; }
    public string? ExternalRef { get; set; }

    /// <summary>Create idempotency anahtarı (Idempotency-Key header). Aynı key ile replay → yan etki yok.</summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>Reverse idempotency anahtarı. Aynı key ile replay → ikinci REVERSAL hareketi yok.</summary>
    public string? ReverseIdempotencyKey { get; set; }
}
