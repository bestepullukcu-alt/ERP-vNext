namespace Diten.ProcurementService.Domain.Entities;

/// <summary>
/// Invoice yaşam döngüsü durumu (MOD-0143 §4 / invoice-match.openapi.yaml InvoiceStatus). Sunucu-güdümlü:
/// Captured → (runThreeWayMatch) Matched | MatchedWithinTolerance | Exception → (resolveMatchException)
/// Rejected | ClearedForPayment. <c>ClearedForPayment</c> yalnız bir EŞLEŞME-DURUMU işaretidir — ÖDEME YÜRÜTMEZ
/// (AP/payment SoR = Finance/Treasury; MOD-0146 kapsam dışı, domain-config boundary).
/// </summary>
public enum InvoiceStatus
{
    Captured = 0,
    Matched = 1,
    MatchedWithinTolerance = 2,
    Exception = 3,
    Rejected = 4,
    ClearedForPayment = 5
}

/// <summary>
/// 3-yönlü eşleştirme sonucu (invoice-match.openapi.yaml MatchOutcome.result). PO ↔ GRN ↔ Invoice karşılaştırmasının
/// nihai kararı. Tolerans policy-driven çözülür (ASSUMPTION-P2P-01) — bu enum yalnız kararı taşır, sayısal eşik TAŞIMAZ.
/// </summary>
public enum InvoiceMatchResult
{
    Matched = 0,
    MatchedWithinTolerance = 1,
    Exception = 2
}

/// <summary>
/// Embedded Invoice satırı (MOD-0143 §4 InvoiceLine). itemId MOD-0290'dan CONSUME edilir (opaque, fail-closed).
/// quantity/unitPrice/lineAmount Decimal string (float YASAK). poLineId upstream MOD-0141 PO satırı — nullable; yaratılmaz.
/// </summary>
public sealed class InvoiceLine
{
    /// <summary>Upstream MOD-0141 PO satır referansı — nullable; yaratılmaz.</summary>
    public string? PoLineId { get; set; }

    /// <summary>MOD-0290 ürün referansı (uuid) — opaque (fail-closed doğrulanır).</summary>
    public string ItemId { get; set; } = string.Empty;

    /// <summary>Decimal string (^-?\d+(\.\d+)?$); float YASAK; > 0.</summary>
    public string Quantity { get; set; } = "0";

    /// <summary>Decimal string; float YASAK; ≥ 0.</summary>
    public string UnitPrice { get; set; } = "0";

    /// <summary>Decimal string; qty×unitPrice tutarlılık kontrolü. Verilmezse server-hesaplı.</summary>
    public string LineAmount { get; set; } = "0";
}

/// <summary>
/// Eşleştirme sapması (invoice-match.openapi.yaml MatchOutcome.variances[]). PO/GRN beklenen değeri ile fatura fiili
/// değeri arasındaki fark. <see cref="WithinTolerance"/> policy-driven tolerans profiline göre hesaplanır
/// (ASSUMPTION-P2P-01) — eşik <see cref="IMatchTolerancePolicy"/> seam'inden gelir, match mantığına GÖMÜLMEZ.
/// </summary>
public sealed class MatchVariance
{
    public string? PoLineId { get; set; }

    /// <summary>Contract enum: quantity | price | amount (lowercase, birebir).</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>Beklenen değer (PO/GRN) — Decimal string.</summary>
    public string Expected { get; set; } = "0";

    /// <summary>Fiili değer (Invoice) — Decimal string.</summary>
    public string Actual { get; set; } = "0";

    /// <summary>Sapma policy toleransı içinde mi (seam'den; gömülü sayı YOK).</summary>
    public bool WithinTolerance { get; set; }
}

/// <summary>
/// Invoice SoR entity (MOD-0143). Supplier faturasını PO-referanslı yakalar; runThreeWayMatch ile PO (0141) ↔
/// GRN (0142) ↔ Invoice eşleştirmesini policy-driven tolerans profiline göre yürütür ve sonucu (Matched/
/// MatchedWithinTolerance/Exception) + variances persist eder. Tenant-owned; her sorgu TenantId + LegalEntityId +
/// IsDeleted=false ile filtrelenir. Soft-delete zorunlu. Idempotent capture/match (Idempotency-Key) — replay yan
/// etki üretmez. ÖDEME YÜRÜTMEZ (AP/payment = Finance/Treasury); yalnız match outcome üretir. Kimlik CONSUME edilir
/// (supplier/PO/item uydurulmaz — fail-closed). Shadow stock YASAK (envanter SoR MOD-0173).
/// </summary>
public sealed class Invoice : EntityBase
{
    /// <summary>Public code — server-assigned; unique/tenant+LE (ASSUMPTION-P2P-02; ör. INV-...).</summary>
    public string InvoiceId { get; set; } = string.Empty;

    /// <summary>MOD-0140 SUPPLIER consume — fail-closed (bilinmeyen → 404 UNKNOWN_REFERENCE).</summary>
    public string SupplierId { get; set; } = string.Empty;

    /// <summary>MOD-0141 PO consume — fail-closed (bilinmeyen → 404 UNKNOWN_REFERENCE).</summary>
    public string PoId { get; set; } = string.Empty;

    /// <summary>Trim; (SupplierId+InvoiceNumber) unique/tenant+LE → 409 DUPLICATE_INVOICE.</summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>LE base currency; PO currency ile eşleşmeli (uyumsuz → 422 CURRENCY_MISMATCH).</summary>
    public string Currency { get; set; } = string.Empty;

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Captured;

    public List<InvoiceLine> Lines { get; set; } = new();

    /// <summary>SERVER-COMPUTED (Σ lineAmount) Decimal string; float YASAK.</summary>
    public string TotalAmount { get; set; } = "0";

    /// <summary>policy-driven; verilmezse LE/tenant default profil uygulamada çözülür (ASSUMPTION-P2P-01; sayı gömmez).</summary>
    public string? ToleranceProfileId { get; set; }

    // ── Son 3-yönlü eşleştirme sonucu (persist; getInvoice + idempotent match replay için) ──
    /// <summary>Son runThreeWayMatch sonucu; null → henüz eşleşmemiş (Captured).</summary>
    public InvoiceMatchResult? LastMatchResult { get; set; }

    /// <summary>Son eşleştirmenin variance listesi (MatchOutcome.variances projeksiyonu).</summary>
    public List<MatchVariance> Variances { get; set; } = new();

    /// <summary>Eşleşen GRN(ler) — MOD-0142 (MatchOutcome.grnIds).</summary>
    public List<string> GrnIds { get; set; } = new();

    /// <summary>Açık exception referansı (result=Exception ise); resolve ile kapanır.</summary>
    public string? ExceptionId { get; set; }

    // ── Dış besleme (DEC-INV-19) ────────────────────────────────────────────────
    public string? SourceSystem { get; set; }
    public string? ExternalRef { get; set; }

    /// <summary>Create idempotency anahtarı (Idempotency-Key header). Aynı key ile replay → yan etki yok.</summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>Match idempotency anahtarı. Aynı key ile replay → ikinci exception YOK, aynı outcome döner.</summary>
    public string? MatchIdempotencyKey { get; set; }
}
