using Diten.MdmService.Domain.Vocabulary;

namespace Diten.MdmService.Domain.Entities;

// MOD-0290-FU02 — Product master aggregate. SoR is MOD-0290 (MDM); consumers hold a ProductId REFERENCE only.
//
// BrandId is OPTIONAL by FU01 §4.1: MOD-0290 also covers item/SKU master, and generic / non-pharma products
// legitimately have no brand. A product may belong to at most ONE brand — multi-brand is FU01 F4 (closed in v1).
//
// ATCCode and TherapeuticAreaId / IndicationRefs are pointers ONLY (FU01 §5). This aggregate never opens an
// ATC master, an indication master, or a flat therapeutic-area reference set.
public sealed class Product : EntityBase
{
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductStatus { get; set; } = BrandProductVocabulary.StatusDraft;
    public string? Description { get; set; }

    /// <summary>Optional single-brand link (FU01 §4.1). Never a copy of the brand's data.</summary>
    public Guid? BrandId { get; set; }

    public string? ProductType { get; set; }
    public string? DosageForm { get; set; }
    public string? Strength { get; set; }
    public string? PackSize { get; set; }
    public string? UnitOfMeasure { get; set; }

    /// <summary>External taxonomy pointer (WHO ATC). Format-level only — no local ATC master exists.</summary>
    public string? ATCCode { get; set; }

    /// <summary>ConceptNode / controlled reference. Overrides the brand value when both are present (FU01 §4.1).</summary>
    public Guid? TherapeuticAreaId { get; set; }

    /// <summary>Reference list only — indication master is out of scope (FU01 §5).</summary>
    public List<Guid> IndicationRefs { get; set; } = [];

    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }

    public List<BrandProductExternalReference> ExternalReferences { get; set; } = [];

    public bool IsArchived { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}
