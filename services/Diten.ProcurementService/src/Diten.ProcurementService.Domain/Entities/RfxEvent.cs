namespace Diten.ProcurementService.Domain.Entities;

/// <summary>RFx tipi (MOD-0145 §4 / sourcing.openapi.yaml RfxType).</summary>
public enum RfxType
{
    RFQ = 0,
    RFP = 1,
    RFI = 2
}

/// <summary>
/// RFx yaşam döngüsü durumu (MOD-0145 §4 / sourcing.openapi.yaml RfxStatus).
/// Sunucu-güdümlü geçiş: Draft→Published→(Evaluating)→Awarded; Closed/Cancelled follow-up.
/// </summary>
public enum RfxStatus
{
    Draft = 0,
    Published = 1,
    Evaluating = 2,
    Awarded = 3,
    Cancelled = 4,
    Closed = 5
}

/// <summary>
/// Embedded RFx satırı (MOD-0145 §4 RfxLine). itemId/uomId MOD-0290'dan CONSUME edilir (opaque referans; lokal
/// ürün/UoM kimliği yaratılmaz). quantity Decimal string (float YASAK).
/// </summary>
public sealed class RfxLine
{
    /// <summary>MOD-0290 ürün referansı — opaque (kimlik burada yaratılmaz).</summary>
    public string ItemId { get; set; } = string.Empty;

    /// <summary>Decimal string (^-?\d+(\.\d+)?$); float YASAK.</summary>
    public string Quantity { get; set; } = "0";

    /// <summary>MOD-0290 UoM referansı — opaque.</summary>
    public string UomId { get; set; } = string.Empty;
}

/// <summary>
/// Award kararı (MOD-0145 §4 AwardDecision) — RfxEvent'e bağlı embedded 1-1 değer. awardedSupplierId sunucu tarafından
/// kazanan bid'den çözülür (payload'dan alınmaz). Award çıktısı MOD-0141/0144'e downstream beslenir.
/// </summary>
public sealed class AwardDecision
{
    public string AwardedBidId { get; set; } = string.Empty;

    /// <summary>Server-resolved (kazanan bid'in SupplierId'si).</summary>
    public string AwardedSupplierId { get; set; } = string.Empty;

    public string? Rationale { get; set; }

    public DateTimeOffset DecidedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// RFx (RFQ/RFP/RFI) SoR entity (MOD-0145). Tenant-owned; her sorgu TenantId + LegalEntityId + IsDeleted=false ile
/// filtrelenir (multi-tenancy.md). Soft-delete zorunlu; hard delete YOK. Supplier (0140) ve ürün/UoM (0290) CONSUME
/// edilir — bu modül onların kimliğini yaratmaz. Stok TUTMAZ (shadow stock yasak).
/// </summary>
public sealed class RfxEvent : EntityBase
{
    /// <summary>Public code — server-assigned; unique/tenant+LE.</summary>
    public string RfxId { get; set; } = string.Empty;

    public RfxType Type { get; set; } = RfxType.RFQ;

    public string Title { get; set; } = string.Empty;

    public RfxStatus Status { get; set; } = RfxStatus.Draft;

    public DateTimeOffset? ClosesAt { get; set; }

    /// <summary>Davetli supplier public id'leri (MOD-0140 consume; her id fail-closed doğrulanır). Nullable.</summary>
    public List<string> InvitedSupplierIds { get; set; } = new();

    public List<RfxLine> Lines { get; set; } = new();

    /// <summary>Award kararı (Awarded durumunda set; embedded 1-1).</summary>
    public AwardDecision? Award { get; set; }

    // ── Dış besleme (DEC-INV-19) ────────────────────────────────────────────────
    public string? SourceSystem { get; set; }
    public string? ExternalRef { get; set; }

    // ── Idempotency (MOD-0145 §8 idempotent create/publish/award) — internal, DTO'da yok ──
    /// <summary>Create idempotency anahtarı (Idempotency-Key header). Aynı key ile replay → yan etki yok.</summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>Publish idempotency anahtarı. Aynı key ile replay → yeniden yayınlanmaz.</summary>
    public string? PublishIdempotencyKey { get; set; }

    /// <summary>Award idempotency anahtarı. Aynı key ile replay → yeniden award edilmez.</summary>
    public string? AwardIdempotencyKey { get; set; }
}
