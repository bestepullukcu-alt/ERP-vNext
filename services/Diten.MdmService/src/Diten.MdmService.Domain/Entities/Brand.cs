using Diten.MdmService.Domain.Vocabulary;

namespace Diten.MdmService.Domain.Entities;

// MOD-0290-FU02 — Brand master aggregate. SoR is MOD-0290 (MDM); Campaign / Knowledge / Frequency /
// Visit Planning hold a BrandId REFERENCE only and never open a local or duplicate brand master.
//
// Lifecycle (FU01 §11): draft · active · inactive · archived. There is NO hard delete and no DELETE endpoint.
// IsArchived is the BUSINESS lifecycle flag; EntityBase.IsDeleted stays a technical soft-delete and is never
// set by this feature. Archiving a brand does NOT cascade to its products (silent cascade is forbidden) —
// existing products stay readable, only NEW links are refused.
public sealed class Brand : EntityBase
{
    public string BrandCode { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string BrandStatus { get; set; } = BrandProductVocabulary.StatusDraft;
    public string? Description { get; set; }

    // Format-level references only — no master is resolved here (FU01 §10).
    public Guid? OwnerCompanyId { get; set; }
    public Guid? BusinessUnitId { get; set; }

    // ConceptNode / controlled reference (MOD-0162-FU01C). NEVER a flat local reference set.
    public Guid? TherapeuticAreaId { get; set; }

    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }

    public List<BrandProductExternalReference> ExternalReferences { get; set; } = [];

    public bool IsArchived { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// WP-MDM-BRAND-ACCEPT-01 FIX2 (item 2, CT decision) — a NEW product link goes only to a brand that is ACTIVE and inside
    /// its validity window on <paramref name="asOf"/> (compared by date, as the window is validated). A draft, inactive,
    /// archived or expired / not-yet-valid brand is closed to new links; products already linked keep their link.
    /// </summary>
    public bool IsLinkableAt(DateTimeOffset asOf)
        => !IsArchived
           && !IsDeleted
           && string.Equals(BrandStatus, BrandProductVocabulary.StatusActive, StringComparison.Ordinal)
           && EffectiveFrom.UtcDateTime.Date <= asOf.UtcDateTime.Date
           && (EffectiveTo is null || EffectiveTo.Value.UtcDateTime.Date >= asOf.UtcDateTime.Date);
}
