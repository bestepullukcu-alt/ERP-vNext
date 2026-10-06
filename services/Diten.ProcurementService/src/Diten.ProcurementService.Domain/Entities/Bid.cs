namespace Diten.ProcurementService.Domain.Entities;

/// <summary>
/// Embedded bid satırı (MOD-0145 §4 BidLine). itemId MOD-0290'dan CONSUME edilir (opaque). unitPrice Decimal string
/// (float YASAK); leadTimeDays opsiyonel gün sayısı.
/// </summary>
public sealed class BidLine
{
    /// <summary>MOD-0290 ürün referansı — opaque.</summary>
    public string ItemId { get; set; } = string.Empty;

    /// <summary>Decimal string (^-?\d+(\.\d+)?$); float YASAK.</summary>
    public string UnitPrice { get; set; } = "0";

    public int? LeadTimeDays { get; set; }
}

/// <summary>
/// Teklif (bid) SoR entity (MOD-0145). Bir RFx'e (RfxId) ve bir supplier'a (SupplierId — MOD-0140 consume, fail-closed)
/// bağlıdır. Tenant-owned; her sorgu TenantId + LegalEntityId + IsDeleted=false ile filtrelenir. Soft-delete zorunlu.
/// </summary>
public sealed class Bid : EntityBase
{
    /// <summary>Public code — server-assigned; unique/tenant+LE.</summary>
    public string BidId { get; set; } = string.Empty;

    /// <summary>Ait olduğu RFx public code'u.</summary>
    public string RfxId { get; set; } = string.Empty;

    /// <summary>Teklif veren supplier (MOD-0140 consume — fail-closed doğrulanır).</summary>
    public string SupplierId { get; set; } = string.Empty;

    public List<BidLine> Lines { get; set; } = new();

    /// <summary>Değerlendirme skoru — Decimal string; Evaluating aşamasında set edilir (okuma-yalnız yüzey). Nullable.</summary>
    public string? EvaluationScore { get; set; }

    // ── Idempotency (MOD-0145 §8 idempotent bid) — internal, DTO'da yok ──
    /// <summary>Submit-bid idempotency anahtarı. Aynı key ile replay → yeni bid yaratmaz, mevcut döner.</summary>
    public string? IdempotencyKey { get; set; }
}
