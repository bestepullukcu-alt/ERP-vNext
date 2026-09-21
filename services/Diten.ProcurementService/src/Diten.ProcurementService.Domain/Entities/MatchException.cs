namespace Diten.ProcurementService.Domain.Entities;

/// <summary>
/// Eşleştirme exception sebep kodu (invoice-match.openapi.yaml ExceptionReason). runThreeWayMatch policy toleransı
/// dışında bir sapma bulduğunda kuyruğa düşen kaydın sebebi. Tolerans eşiği policy-driven'dır (ASSUMPTION-P2P-01);
/// bu enum yalnız sebebi sınıflandırır.
/// </summary>
public enum MatchExceptionReason
{
    QtyMismatch = 0,
    PriceMismatch = 1,
    AmountMismatch = 2,
    NoReceipt = 3,
    NoPo = 4,
    DuplicateInvoice = 5,
    CurrencyMismatch = 6
}

/// <summary>Exception kuyruğu durumu (invoice-match.openapi.yaml MatchException.status). Open → (resolve) Resolved.</summary>
public enum MatchExceptionStatus
{
    Open = 0,
    Resolved = 1
}

/// <summary>
/// Match exception SoR entity (MOD-0143 §4). Eşleşmeyen fatura kuyruğu kaydı; approve/reject/tolerance-override ile
/// approval trail + audit altında çözülür (MOD-0023 seam). Tenant-owned; her sorgu TenantId + LegalEntityId +
/// IsDeleted=false ile filtrelenir. Soft-delete zorunlu. Idempotent resolve (Idempotency-Key) — replay ikinci durum
/// geçişi üretmez. Çözüm ÖDEME YÜRÜTMEZ — yalnız invoice eşleşme-durumunu günceller (AP/payment = Finance/Treasury).
/// </summary>
public sealed class MatchException : EntityBase
{
    /// <summary>Public code — server-assigned; unique/tenant+LE (ör. EXC-...).</summary>
    public string ExceptionId { get; set; } = string.Empty;

    /// <summary>İlgili fatura (MOD-0143 Invoice.InvoiceId).</summary>
    public string InvoiceId { get; set; } = string.Empty;

    /// <summary>Upstream MOD-0141 PO referansı — nullable (NoPo durumunda boş olabilir).</summary>
    public string? PoId { get; set; }

    public MatchExceptionReason ReasonCode { get; set; }

    public MatchExceptionStatus Status { get; set; } = MatchExceptionStatus.Open;

    // ── Approval trail (MOD-0023 seam) — resolve ile doldurulur ──
    /// <summary>Çözüm kararı: approve | reject | tolerance-override.</summary>
    public string? ResolutionDecision { get; set; }

    public string? ResolutionNote { get; set; }

    /// <summary>Çözen aktör (ICurrentUserContext) — approval trail.</summary>
    public string? ResolvedBy { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>Resolve idempotency anahtarı. Aynı key ile replay → ikinci geçiş YOK.</summary>
    public string? ResolveIdempotencyKey { get; set; }
}
