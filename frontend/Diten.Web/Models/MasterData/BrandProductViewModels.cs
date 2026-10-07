using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.MasterData;

// MOD-0290-FU02 — view models for the Master Data → Brands / Products surfaces.
//
// TenantId appears NOWHERE in this file on purpose: it is resolved server-side from the JWT/tenant context and
// must never be modelled, bound, or posted from the browser.

#region Contract

public sealed class BrandProductContractViewModel
{
    public bool IsReady { get; set; }
    public BrandProductFeaturesViewModel Features { get; set; } = new();
    public BrandProductVocabularyViewModel Vocabulary { get; set; } = new();
    public IReadOnlyList<string> ReasonCodes { get; set; } = [];
    public IReadOnlyList<string> Permissions { get; set; } = [];
    public IReadOnlyList<string> Limitations { get; set; } = [];
}

/// <summary>
/// Only the eight capabilities MOD-0290-FU02 publishes. Campaign / knowledge / visit / route / frequency /
/// recommendation / workflow / ATC-local-master / flat-therapeutic-area flags are absent from the API and are
/// therefore absent here too — the UI cannot accidentally light up a capability that does not exist.
/// </summary>
public sealed class BrandProductFeaturesViewModel
{
    public bool SupportsBrandManagement { get; set; }
    public bool SupportsProductManagement { get; set; }
    public bool SupportsBrandProductReference { get; set; }
    public bool SupportsBrandProductHierarchy { get; set; }
    public bool SupportsExternalReferences { get; set; }
    public bool SupportsArchiveLifecycle { get; set; }
    public bool SupportsEffectiveDating { get; set; }
    public bool SupportsContractDrivenUi { get; set; }
}

public sealed class BrandProductVocabularyViewModel
{
    public IReadOnlyList<string> BrandStatuses { get; set; } = [];
    public IReadOnlyList<string> ProductStatuses { get; set; } = [];
    public IReadOnlyList<string> ProductTypes { get; set; } = [];
    public IReadOnlyList<string> DosageForms { get; set; } = [];
    public IReadOnlyList<string> UnitsOfMeasure { get; set; } = [];
}

public sealed class BrandProductGatewayResponse<T>
{
    public T? Data { get; set; }
    public int StatusCode { get; set; }
    public bool IsSuccessful { get; set; }
    public List<string> Errors { get; set; } = [];
}

public sealed class BrandProductExternalReferenceViewModel
{
    [StringLength(100)] public string SourceSystem { get; set; } = string.Empty;
    [StringLength(200)] public string ExternalId { get; set; } = string.Empty;
    [StringLength(100)] public string? ExternalCode { get; set; }
    [StringLength(200)] public string? ExternalName { get; set; }
    public DateTimeOffset? ImportedAt { get; set; }
    public bool IsPrimary { get; set; }
}

#endregion

#region Brand

public sealed class BrandEditViewModel : IValidatableObject
{
    public Guid? BrandId { get; set; }

    [Required, StringLength(64)]
    [RegularExpression("^[A-Za-z0-9._-]+$")]
    public string BrandCode { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string BrandName { get; set; } = string.Empty;

    [Required]
    public string BrandStatus { get; set; } = "draft";

    [StringLength(2000)] public string? Description { get; set; }

    // Format-level references — no master lookup exists for these yet, so they are plain GUID inputs.
    public Guid? OwnerCompanyId { get; set; }
    public Guid? BusinessUnitId { get; set; }
    public Guid? TherapeuticAreaId { get; set; }

    [Required] public DateTimeOffset? EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }

    public List<BrandProductExternalReferenceViewModel> ExternalReferences { get; set; } = [];

    /// <summary>Populated from the capability contract — never a hardcoded list.</summary>
    public IReadOnlyList<string> BrandStatuses { get; set; } = [];

    public string? ContractError { get; set; }
    public bool IsArchived { get; set; }

    // WP-MDM-BRAND-ACCEPT-01 FIX1 (item 2) — the version this edit was made on; posted back with the form and sent as
    // ExpectedVersion, so an edit made on an older read is refused (409) instead of overwriting a newer one.
    public int? ExpectedVersion { get; set; }

    // FIX1 (item 8) — the owner company is picked by NAME; the GUID never reaches the reader.
    public IReadOnlyList<LegalEntityOptionViewModel> OwnerCompanies { get; set; } = [];
    public bool OwnerCompaniesUnavailable { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // FIX4 (K4) — the calendar days entered, compared once through the web's calendar-day rule.
        if (CalendarDay.EndsBeforeStart(EffectiveFrom, EffectiveTo))
        {
            yield return new ValidationResult("EffectiveToBeforeEffectiveFrom", [nameof(EffectiveTo)]);
        }
    }
}

public sealed class BrandDetailViewModel
{
    public Guid BrandId { get; set; }
    public string BrandCode { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string BrandStatus { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? OwnerCompanyId { get; set; }
    public Guid? BusinessUnitId { get; set; }
    public Guid? TherapeuticAreaId { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public List<BrandProductExternalReferenceViewModel> ExternalReferences { get; set; } = [];
    public bool IsArchived { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public int Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>FIX1 (item 3) — products of this brand that are not archived (detail read only).</summary>
    public int? LiveProductCount { get; set; }

    /// <summary>
    /// P-2026-10-07-BRAND-FIX3 (R-5) — computed by MDM (active + inside its validity window today, UTC). The web never
    /// recomputes it; null = an MDM build that does not send it yet, and such a brand is not offered as a new link.
    /// </summary>
    public bool? IsLinkable { get; set; }
}

/// <summary>FIX1 (item 8) — one legal entity of the owner-company picker: shown as code and name, never as its id.</summary>
public sealed record LegalEntityOptionViewModel(Guid LegalEntityId, string Code, string Name);

public sealed class BrandListResultViewModel
{
    public List<BrandDetailViewModel> Items { get; set; } = [];
    public int TotalCount { get; set; }
}

public sealed class BrandPageViewModel
{
    public BrandDetailViewModel Brand { get; set; } = new();
    public BrandProductContractViewModel Contract { get; set; } = new();
    public bool CanManage { get; set; }
    public bool CanReadProducts { get; set; }

    // WP-MDM-BRAND-ACCEPT-01 FIX1 (item 13) — decided by the controller through PermissionClaims, the same gate the
    // proxies enforce, so the page never offers an action the proxy then refuses.
    public bool CanUpdate { get; set; }
    public bool CanArchive { get; set; }

    /// <summary>FIX1 (item 8) — the owner company's code and name; null when it could not be resolved.</summary>
    public string? OwnerCompanyName { get; set; }
}

#endregion

#region Product

public sealed class ProductEditViewModel : IValidatableObject
{
    public Guid? ProductId { get; set; }

    [Required, StringLength(64)]
    [RegularExpression("^[A-Za-z0-9._-]+$")]
    public string ProductCode { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string ProductName { get; set; } = string.Empty;

    [Required]
    public string ProductStatus { get; set; } = "draft";

    /// <summary>Optional by design (FU01 §4.1) — generic and non-pharma products have no brand.</summary>
    public Guid? BrandId { get; set; }

    public string? ProductType { get; set; }
    public string? DosageForm { get; set; }
    [StringLength(100)] public string? Strength { get; set; }
    [StringLength(100)] public string? PackSize { get; set; }
    public string? UnitOfMeasure { get; set; }

    [StringLength(16)]
    [RegularExpression("^[A-Za-z0-9]*$")]
    public string? ATCCode { get; set; }

    public Guid? TherapeuticAreaId { get; set; }

    /// <summary>Comma/space separated GUID list — flattened for form binding, parsed back on submit.</summary>
    [StringLength(2000)]
    public string? IndicationRefsRaw { get; set; }

    [StringLength(2000)] public string? Description { get; set; }

    [Required] public DateTimeOffset? EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }

    public List<BrandProductExternalReferenceViewModel> ExternalReferences { get; set; } = [];

    // All populated from the capability contract / the brands endpoint — never hardcoded.
    public IReadOnlyList<string> ProductStatuses { get; set; } = [];
    public IReadOnlyList<string> ProductTypes { get; set; } = [];
    public IReadOnlyList<string> DosageForms { get; set; } = [];
    public IReadOnlyList<string> UnitsOfMeasure { get; set; } = [];

    /// <summary>
    /// The brands MDM would link (its isLinkable) plus — on edit — the product's current brand even when it is not
    /// linkable any more, so saving never drops it (P-2026-10-07-BRAND-FIX3 R-4).
    /// </summary>
    public IReadOnlyList<BrandOptionViewModel> BrandOptions { get; set; } = [];

    /// <summary>
    /// R-4 — the editor may not read brands: the picker shows only the current brand, disabled, and a hidden input carries
    /// BrandId so the save keeps it. Decided by the controller, never bound from the form.
    /// </summary>
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public bool BrandReadOnly { get; set; }

    /// <summary>R-3 — the version this edit was made on; sent as expectedVersion so a stale save is refused (409).</summary>
    public int? ExpectedVersion { get; set; }

    public string? ContractError { get; set; }
    public bool IsArchived { get; set; }

    public IReadOnlyList<Guid> ParseIndicationRefs()
        => (IndicationRefsRaw ?? string.Empty)
            .Split([',', ';', ' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => Guid.TryParse(x, out var parsed) ? parsed : Guid.Empty)
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // FIX4 (K4) — the calendar days entered, compared once through the web's calendar-day rule.
        if (CalendarDay.EndsBeforeStart(EffectiveFrom, EffectiveTo))
        {
            yield return new ValidationResult("EffectiveToBeforeEffectiveFrom", [nameof(EffectiveTo)]);
        }

        var raw = (IndicationRefsRaw ?? string.Empty)
            .Split([',', ';', ' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (raw.Any(x => !Guid.TryParse(x, out _)))
        {
            yield return new ValidationResult("IndicationRefsInvalid", [nameof(IndicationRefsRaw)]);
        }
    }
}

// WP-MDM-BRAND-ACCEPT-01 FIX1 (item 3) — IsArchived lets the product page say the brand was archived.
// P-2026-10-07-BRAND-FIX3 (R-4) — IsLinkable false marks the product's current brand that MDM would not link any more.
// P-2026-10-07-BRAND-FIX4 (F3) — NameUnreadable marks a kept brand whose name could not be read: shown as its id plus a
// localized "(could not be read)", still selected, so a save posts the same BrandId.
public sealed record BrandOptionViewModel(
    Guid BrandId, string BrandCode, string BrandName, bool IsArchived = false, bool IsLinkable = true, bool NameUnreadable = false);

/// <summary>R-4 — MDM's ProductDetailDto.linkedBrand: the product's current brand, readable with mdm.products.read alone.</summary>
public sealed class LinkedBrandViewModel
{
    public Guid BrandId { get; set; }
    public string BrandCode { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public bool IsLinkable { get; set; }
}

public sealed class ProductDetailViewModel
{
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductStatus { get; set; } = string.Empty;
    public Guid? BrandId { get; set; }
    public string? ProductType { get; set; }
    public string? DosageForm { get; set; }
    public string? Strength { get; set; }
    public string? PackSize { get; set; }
    public string? UnitOfMeasure { get; set; }
    public string? ATCCode { get; set; }
    public Guid? TherapeuticAreaId { get; set; }
    public List<Guid> IndicationRefs { get; set; } = [];
    public string? Description { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public List<BrandProductExternalReferenceViewModel> ExternalReferences { get; set; } = [];
    public bool IsArchived { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public int Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>R-4 — the current brand resolved by MDM; null when the product has none or MDM does not send it yet.</summary>
    public LinkedBrandViewModel? LinkedBrand { get; set; }
}

public sealed class ProductListResultViewModel
{
    public List<ProductDetailViewModel> Items { get; set; } = [];
    public int TotalCount { get; set; }
}

public sealed class ProductPageViewModel
{
    public ProductDetailViewModel Product { get; set; } = new();
    public BrandProductContractViewModel Contract { get; set; } = new();
    public bool CanManage { get; set; }

    /// <summary>
    /// Resolved brand summary when the linked brand is readable; null otherwise. When null the detail page
    /// shows the raw BrandId — it never invents a display name.
    /// </summary>
    public BrandOptionViewModel? Brand { get; set; }
}

#endregion
