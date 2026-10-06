namespace Diten.ProcurementService.Domain.Entities;

/// <summary>
/// Purchase Order yaşam döngüsü durumu (MOD-0141 §4 / requisition-po.openapi.yaml PoStatus). Sunucu-güdümlü geçiş:
/// Draft→Approved→(PartiallyReceived/Received via GRN 0142)→Closed; Cancelled follow-up. Bu dilimde yalnız
/// Draft→Approved yazılır; Received* geçişleri MOD-0142 goods-receipt event'i ile tetiklenir (§20 follow-up).
/// </summary>
public enum PoStatus
{
    Draft = 0,
    Approved = 1,
    PartiallyReceived = 2,
    Received = 3,
    Closed = 4,
    Cancelled = 5
}

/// <summary>
/// Embedded PO satırı (MOD-0141 §4 PoLine). itemId/skuId/uomId MOD-0290/0048'den CONSUME edilir (opaque). quantity
/// ve unitPrice Decimal string (float YASAK). poLineId server-assigned — GRN 0142 poLineId ile eşleşir. lineAmount
/// SERVER-COMPUTED (quantity × unitPrice); kullanıcı girmez.
/// </summary>
public sealed class PoLine
{
    /// <summary>Server-assigned; GRN 0142 poLineId ile eşleşir.</summary>
    public string PoLineId { get; set; } = string.Empty;

    /// <summary>MOD-0290 ürün referansı — opaque.</summary>
    public string ItemId { get; set; } = string.Empty;

    /// <summary>MOD-0290 SKU referansı — opaque; nullable.</summary>
    public string? SkuId { get; set; }

    /// <summary>Decimal string (^-?\d+(\.\d+)?$); float YASAK; > 0.</summary>
    public string Quantity { get; set; } = "0";

    /// <summary>MOD-0048 UoM referansı — opaque.</summary>
    public string UomId { get; set; } = string.Empty;

    /// <summary>Decimal string; float YASAK; ≥ 0.</summary>
    public string UnitPrice { get; set; } = "0";

    /// <summary>SERVER-COMPUTED (quantity × unitPrice) Decimal string; kullanıcı girmez.</summary>
    public string LineAmount { get; set; } = "0";
}

/// <summary>
/// Purchase Order SoR entity (MOD-0141). G2A golden flow'un upstream belgesi: MOD-0142 GRN ve MOD-0143 Invoice-Match
/// bunu poId/poLineId ile okur. Tenant-owned; her sorgu TenantId + LegalEntityId + IsDeleted=false ile filtrelenir.
/// Soft-delete zorunlu. supplierId MOD-0140 (fail-closed), itemId MOD-0290 (seam) CONSUME edilir. Stok TUTMAZ.
/// totalAmount SERVER-COMPUTED (Σ lineAmount). Approve → GRN'e açık (Draft→Approved).
/// </summary>
public sealed class PurchaseOrder : EntityBase
{
    /// <summary>Public code — server-assigned; unique/tenant+LE (ASSUMPTION-P2P-0141-01).</summary>
    public string PoId { get; set; } = string.Empty;

    /// <summary>MOD-0140 SUPPLIER consume — fail-closed doğrulanır (bilinmeyen → 404 UNKNOWN_REFERENCE).</summary>
    public string SupplierId { get; set; } = string.Empty;

    /// <summary>Onaylı requisition referansı (opsiyonel; verilirse Approved olmalı).</summary>
    public string? RequisitionId { get; set; }

    public PoStatus Status { get; set; } = PoStatus.Draft;

    /// <summary>LE base currency.</summary>
    public string Currency { get; set; } = string.Empty;

    public List<PoLine> Lines { get; set; } = new();

    /// <summary>SERVER-COMPUTED (Σ lineAmount) Decimal string; kullanıcı girmez.</summary>
    public string TotalAmount { get; set; } = "0";

    /// <summary>Approve'da MOD-0023 workflow seam'inden atanır (bu dilimde server-side üretilir; nullable).</summary>
    public string? WorkflowInstanceId { get; set; }

    // ── Dış besleme (DEC-INV-19) ────────────────────────────────────────────────
    public string? SourceSystem { get; set; }
    public string? ExternalRef { get; set; }

    // ── Idempotency (MOD-0141 §8 idempotent create/approve) — internal, DTO'da yok ──
    /// <summary>Create idempotency anahtarı. Aynı key ile replay → yan etki yok.</summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>Approve idempotency anahtarı. Aynı key ile replay → yeniden onaylanmaz.</summary>
    public string? ApproveIdempotencyKey { get; set; }
}
