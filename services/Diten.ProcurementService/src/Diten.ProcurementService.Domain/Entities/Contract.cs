namespace Diten.ProcurementService.Domain.Entities;

/// <summary>
/// Sözleşme yaşam döngüsü durumu (MOD-0144 §4 / contracting.openapi.yaml ContractStatus). Sunucu-güdümlü:
/// Draft → (submit/InReview) → (activate) Active → Expired (EffectiveTo geçince) | Terminated (additive terminate).
/// Geçiş kuralı state machine ile korunur (geçersiz geçiş → 409 INVALID_STATE); sessiz overwrite YOK.
/// </summary>
public enum ContractStatus
{
    Draft = 0,
    InReview = 1,
    Active = 2,
    Expired = 3,
    Terminated = 4
}

/// <summary>
/// Embedded sözleşme clause referansı (contracting.openapi.yaml ClauseRef {clauseId, deviation?, deviationText?}).
/// clauseId clause library (Clause) referansıdır — burada clause GÖVDESİ saklanmaz, yalnız referans + sapma bilgisi.
/// deviation=true ise standart clause'tan sapma vardır ve deviationText önerilir (onay/approval trail deviation'ı kapsar).
/// </summary>
public sealed class ClauseRef
{
    /// <summary>Clause library (Clause.ClauseId) referansı — var olması doğrulanır (yoksa 422 VALIDATION_FAILED).</summary>
    public string ClauseId { get; set; } = string.Empty;

    /// <summary>Standart clause'tan sapma var mı (nullable).</summary>
    public bool? Deviation { get; set; }

    /// <summary>Sapma açıklaması (deviation=true ise önerilir; nullable).</summary>
    public string? DeviationText { get; set; }
}

/// <summary>
/// Contract SoR entity (MOD-0144). Procurement/sourcing sözleşmesini kimlik + yaşam döngüsü + supplier/award bağı +
/// clause set + deviation + onay (approval trail) ile sahiplenir. Tenant-owned; her sorgu TenantId + LegalEntityId +
/// IsDeleted=false ile filtrelenir. Soft-delete zorunlu. Idempotent create/activate (Idempotency-Key) — replay yan
/// etki üretmez. Supplier (MOD-0140) ve award/rfx (MOD-0145) CONSUME edilir (bilinmeyen → 404 UNKNOWN_REFERENCE,
/// fail-closed); lokal supplier/rfx kimliği YARATILMAZ. Doküman BINARY saklanmaz — yalnız <see cref="EvidenceRefs"/>
/// (MOD-0029/0031 Evidence referansları). Para/currency Decimal politikası: currency LE base currency (boşsa server
/// default; ASSUMPTION-0144-04). Approval MOD-0023 üzerinden; <see cref="WorkflowInstanceId"/> activate'te doldurulur
/// (ASSUMPTION-0144-05).
/// </summary>
public sealed class Contract : EntityBase
{
    /// <summary>Public code — server-assigned; unique/tenant+LE (ör. CTR-...).</summary>
    public string ContractId { get; set; } = string.Empty;

    /// <summary>MOD-0140 SUPPLIER consume — fail-closed (bilinmeyen → 404 UNKNOWN_REFERENCE).</summary>
    public string SupplierId { get; set; } = string.Empty;

    /// <summary>MOD-0145 award/rfx consume — nullable; verilirse fail-closed (bilinmeyen → 404 UNKNOWN_REFERENCE).</summary>
    public string? RfxId { get; set; }

    /// <summary>Trim; max 200.</summary>
    public string Title { get; set; } = string.Empty;

    public ContractStatus Status { get; set; } = ContractStatus.Draft;

    /// <summary>ISO date string (yyyy-MM-dd); zorunlu (contract format: date).</summary>
    public string EffectiveFrom { get; set; } = string.Empty;

    /// <summary>ISO date string; nullable; ≥ EffectiveFrom.</summary>
    public string? EffectiveTo { get; set; }

    /// <summary>LE base currency; boşsa server default doldurur (ASSUMPTION-0144-04). float YASAK — currency salt kod.</summary>
    public string? Currency { get; set; }

    /// <summary>Embedded clause set (ClauseRef); her clauseId clause library'de var olmalı.</summary>
    public List<ClauseRef> Clauses { get; set; } = new();

    /// <summary>MOD-0023 approval instance (activate'te set edilir; ASSUMPTION-0144-05). nullable.</summary>
    public string? WorkflowInstanceId { get; set; }

    /// <summary>MOD-0029/0031 Evidence referansları — BINARY burada değil (yalnız referans). nullable.</summary>
    public List<string> EvidenceRefs { get; set; } = new();

    /// <summary>Create idempotency anahtarı (Idempotency-Key header). Aynı key ile replay → yan etki yok.</summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>Activate idempotency anahtarı. Aynı key ile replay → ikinci geçiş YOK, aynı sözleşme döner.</summary>
    public string? ActivateIdempotencyKey { get; set; }
}
