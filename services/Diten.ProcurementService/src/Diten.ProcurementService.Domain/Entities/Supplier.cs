namespace Diten.ProcurementService.Domain.Entities;

/// <summary>
/// Supplier lifecycle durumu (MOD-0140 §4 Status enum).
/// </summary>
public enum SupplierStatus
{
    Active = 0,
    OnHold = 1,
    Blocked = 2,
    Inactive = 3
}

/// <summary>Onboarding case durumu (MOD-0140 §4).</summary>
public enum OnboardingStatus
{
    Draft = 0,
    InReview = 1,
    Approved = 2,
    Rejected = 3
}

/// <summary>KYC sonucu (MOD-0140 §4).</summary>
public enum KycOutcome
{
    Pending = 0,
    Passed = 1,
    Failed = 2,
    ManualReview = 3
}

/// <summary>Sanctions taraması sonucu (MOD-0140 §4).</summary>
public enum SanctionsOutcome
{
    Pending = 0,
    Clear = 1,
    Hit = 2,
    Override = 3
}

/// <summary>
/// Embedded supplier iletişim satırı (MOD-0140 §4 Contacts[]). type ∈ {primary,billing,quality,logistics}.
/// </summary>
public sealed class SupplierContact
{
    public string Type { get; set; } = "primary";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Name { get; set; }
}

/// <summary>
/// Onboarding doküman referansı (MOD-0140 §4 Documents[]). Binary SAKLANMAZ — yalnız evidenceRef (MOD-0029/0031).
/// </summary>
public sealed class SupplierDocument
{
    public string Type { get; set; } = string.Empty;
    public string EvidenceRef { get; set; } = string.Empty;
}

/// <summary>
/// Onboarding case (KYC / sanctions / docs / approval) — Supplier'a bağlı embedded değer (MOD-0140 §3).
/// </summary>
public sealed class OnboardingCase
{
    public OnboardingStatus OnboardingStatus { get; set; } = OnboardingStatus.Draft;
    public KycOutcome KycOutcome { get; set; } = KycOutcome.Pending;
    public SanctionsOutcome SanctionsOutcome { get; set; } = SanctionsOutcome.Pending;
    public List<SupplierDocument> Documents { get; set; } = new();
}

/// <summary>
/// Onboarding onay bağlantısı (MOD-0140 §4 / contract OnboardingCase.approval). Onay motoru MOD-0023'tür;
/// burada yalnız workflow instance referansı + karar metadatası tutulur (SoR onay motorunda).
/// </summary>
public sealed class OnboardingApproval
{
    /// <summary>MOD-0023 Workflow/Approvals instance id (referans).</summary>
    public string? WorkflowInstanceId { get; set; }
    public string? DecidedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
}

/// <summary>
/// Supplier master + onboarding SoR entity (MOD-0140). Tenant-owned; her sorgu TenantId + LegalEntityId + IsDeleted=false
/// ile filtrelenir. Alanlar MOD-0140 §4 tablosundan türetilmiştir. Stok/envanter bakiyesi TUTMAZ (shadow stock yasak).
/// </summary>
public sealed class Supplier : EntityBase
{
    /// <summary>Public code — server-assigned; unique/tenant+LE.</summary>
    public string SupplierId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public SupplierStatus Status { get; set; } = SupplierStatus.Active;

    /// <summary>Lowercase ülke kodu, nullable.</summary>
    public string? Country { get; set; }

    /// <summary>Aktif supplier'da tenant+LE bazında unique (partial).</summary>
    public string? TaxId { get; set; }

    public List<SupplierContact> Contacts { get; set; } = new();

    // ── Onboarding (denormalize edilmiş durum alanları + case) ──────────────────
    public OnboardingStatus OnboardingStatus { get; set; } = OnboardingStatus.Draft;
    public KycOutcome KycOutcome { get; set; } = KycOutcome.Pending;
    public SanctionsOutcome SanctionsOutcome { get; set; } = SanctionsOutcome.Pending;
    public List<SupplierDocument> Documents { get; set; } = new();
    public OnboardingCase? OnboardingCase { get; set; }

    /// <summary>Onboarding onay bağlantısı (MOD-0023 workflow instance + karar metadatası).</summary>
    public OnboardingApproval? Approval { get; set; }

    // ── Dış besleme (DEC-INV-19) ────────────────────────────────────────────────
    public string? SourceSystem { get; set; }
    public string? ExternalRef { get; set; }

    // ── Idempotency (MOD-0140 §8 idempotent create/onboarding) — internal, DTO'da yok ──
    /// <summary>Create idempotency anahtarı (Idempotency-Key header). Aynı key ile replay → yan etki yok.</summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>Onboarding submit idempotency anahtarı. Aynı key ile replay → yeniden işlenmez.</summary>
    public string? OnboardingIdempotencyKey { get; set; }
}
