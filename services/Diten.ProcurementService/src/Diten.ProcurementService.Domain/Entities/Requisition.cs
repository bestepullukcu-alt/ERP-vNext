namespace Diten.ProcurementService.Domain.Entities;

/// <summary>
/// Requisition (satın alma talebi) yaşam döngüsü durumu (MOD-0141 §4 / requisition-po.openapi.yaml RequisitionStatus).
/// Sunucu-güdümlü geçiş: Draft→Submitted→(Approved/Rejected); Converted (PO'ya dönüştü) / Cancelled follow-up.
/// </summary>
public enum RequisitionStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
    Converted = 4,
    Cancelled = 5
}

/// <summary>
/// Embedded requisition satırı (MOD-0141 §4 RequisitionLine). itemId/skuId/uomId MOD-0290/0048'den CONSUME edilir
/// (opaque referans; lokal ürün/UoM kimliği yaratılmaz). quantity Decimal string (float YASAK). needBy opsiyonel
/// ISO tarih (opaque string; serileştirmede tarih tipi sürprizinden kaçınılır).
/// </summary>
public sealed class RequisitionLine
{
    /// <summary>MOD-0290 ürün referansı — opaque (kimlik burada yaratılmaz).</summary>
    public string ItemId { get; set; } = string.Empty;

    /// <summary>MOD-0290 SKU referansı — opaque; nullable.</summary>
    public string? SkuId { get; set; }

    /// <summary>Decimal string (^-?\d+(\.\d+)?$); float YASAK; > 0.</summary>
    public string Quantity { get; set; } = "0";

    /// <summary>MOD-0048 UoM referansı — opaque.</summary>
    public string UomId { get; set; } = string.Empty;

    /// <summary>Opsiyonel ihtiyaç tarihi (ISO yyyy-MM-dd; opaque).</summary>
    public string? NeedBy { get; set; }
}

/// <summary>
/// Requisition SoR entity (MOD-0141). Tenant-owned; her sorgu TenantId + LegalEntityId + IsDeleted=false ile
/// filtrelenir (multi-tenancy.md). Soft-delete zorunlu; hard delete YOK. Ürün/UoM (0290/0048) CONSUME edilir —
/// bu modül onların kimliğini yaratmaz. Stok TUTMAZ (shadow stock yasak). Submit → onaya gönderir (MOD-0023 seam).
/// </summary>
public sealed class Requisition : EntityBase
{
    /// <summary>Public code — server-assigned; unique/tenant+LE (ASSUMPTION-P2P-0141-01).</summary>
    public string RequisitionId { get; set; } = string.Empty;

    public RequisitionStatus Status { get; set; } = RequisitionStatus.Draft;

    public List<RequisitionLine> Lines { get; set; } = new();

    public string? Justification { get; set; }

    /// <summary>Submit'te MOD-0023 workflow seam'inden atanır (bu dilimde server-side üretilir; nullable).</summary>
    public string? WorkflowInstanceId { get; set; }

    // ── Dış besleme (DEC-INV-19) ────────────────────────────────────────────────
    public string? SourceSystem { get; set; }
    public string? ExternalRef { get; set; }

    // ── Idempotency (MOD-0141 §8 idempotent create/submit) — internal, DTO'da yok ──
    /// <summary>Create idempotency anahtarı (Idempotency-Key header). Aynı key ile replay → yan etki yok.</summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>Submit idempotency anahtarı. Aynı key ile replay → yeniden gönderilmez.</summary>
    public string? SubmitIdempotencyKey { get; set; }
}
