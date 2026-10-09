using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.CRM;

/// <summary>Details page view model — the resolved template plus what the actor is allowed to do with it.</summary>
public sealed class StrategyTemplatePageViewModel
{
    public StrategyTemplateDetailViewModel Template { get; set; } = new();
    public StrategyTemplateBindingsViewModel? Bindings { get; set; }
    public bool CanManage { get; set; }
    public bool CanActivate { get; set; }
}

/// <summary>Read model bound from the gateway template detail response.</summary>
public sealed class StrategyTemplateDetailViewModel
{
    public Guid TemplateId { get; set; }
    public string TemplateCode { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string SubjectType { get; set; } = string.Empty;
    public string TemplateStatus { get; set; } = string.Empty;
    public int TemplateVersion { get; set; }
    public Guid VersionLineageId { get; set; }
    public bool Superseded { get; set; }
    public Guid? SupersededByTemplateId { get; set; }

    /// <summary>WP-E2E-FIX-3 (E5-B2) — the successor's version ("Yerini v{n} aldı"); null when unknown.</summary>
    public int? SupersededByTemplateVersion { get; set; }

    /// <summary>WP-ST-SCOPE — the play's address level as STORED (empty for a pre-scope play).</summary>
    public string ScopeType { get; set; } = string.Empty;

    /// <summary>WP-ST-SCOPE — the EFFECTIVE address level (derived from <see cref="BusinessUnitId"/> for a pre-scope
    /// play), so the editor opens on the scope the play always had rather than on an empty selector.</summary>
    public string EffectiveScopeType { get; set; } = string.Empty;

    /// <summary>WP-ST-SCOPE — the country reference, when the scope is <c>country</c>.</summary>
    public string? CountryScope { get; set; }

    /// <summary>WP-ST-SCOPE — the legal-entity reference, when the scope is <c>legal-entity</c>.</summary>
    public Guid? LegalEntityId { get; set; }

    public string? BusinessUnitId { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public List<StrategyTemplateSegmentBindingViewModel> SegmentBindings { get; set; } = new();
    public StrategyTemplateFrequencyIntentViewModel FrequencyIntent { get; set; } = new();
    public List<StrategyTemplateProductLineViewModel> ProductLines { get; set; } = new();
    public List<StrategyTemplateContentBindingViewModel> ContentBindings { get; set; } = new();
    public bool AreBindingsFrozen { get; set; }
    public DateTimeOffset? BindingsFrozenAt { get; set; }
    public DateTimeOffset? ActivatedAt { get; set; }
    public string? ActivatedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public bool IsArchived { get; set; }
    public int Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    // WP-SB-3-UIa — SB-3a product line summary (a pre-SB-3a line counts as promo).
    public int PromoLineCount { get; set; }
    public int NonPromoLineCount { get; set; }
    public int LinesWithoutJourneyCount { get; set; }
}

public sealed class StrategyTemplateSegmentBindingViewModel
{
    public Guid BindingId { get; set; }
    public Guid SegmentId { get; set; }
    public Guid SegmentLineageId { get; set; }
    public int SegmentVersionAtBinding { get; set; }
    public string? SegmentCodeDisplay { get; set; }
    public string? BindingRole { get; set; }
    public int SortOrder { get; set; }
    public string? Notes { get; set; }
}

public sealed class StrategyTemplateFrequencyIntentViewModel
{
    public string Mode { get; set; } = "none";
    public Guid? VisitFrequencyPolicyId { get; set; }
    public string? PolicyCodeDisplay { get; set; }
    public string? FrequencyType { get; set; }
    public int? RequiredVisitCount { get; set; }
    public string? PeriodType { get; set; }
    public string? IntentNote { get; set; }
}

public sealed class StrategyTemplateProductLineViewModel
{
    public Guid LineId { get; set; }
    public Guid GlobalProductId { get; set; }
    public string? GlobalProductCodeDisplay { get; set; }
    public decimal? LineWeightPercentage { get; set; }
    public string SkuAllocationMode { get; set; } = "product-only";
    public List<StrategyTemplateSkuAllocationViewModel> SkuAllocations { get; set; } = new();
    public decimal TotalPercentage { get; set; }
    public int SortOrder { get; set; }
    public string? Notes { get; set; }

    // WP-SB-3-UIa (SB-3a contract) — how the line is told and with which journey. They MUST stay on this model: the Edit
    // page seeds ProductLinesJson by serialising it, so a field missing here is silently dropped on the next save.
    public string? Role { get; set; }
    public Guid? JourneyId { get; set; }

    // Read-time journey hints (CRM fills them; the runtime ignores them on a write).
    public string? JourneyCode { get; set; }
    public string? JourneyName { get; set; }
    public string? JourneyStatus { get; set; }
    public bool JourneyMissing { get; set; }
    public List<string>? JourneyWarnings { get; set; }
}

public sealed class StrategyTemplateSkuAllocationViewModel
{
    public Guid AllocationId { get; set; }
    public Guid GskuId { get; set; }
    public string? GskuCanonicalCodeDisplay { get; set; }
    public decimal Percentage { get; set; }
    public int SortOrder { get; set; }
}

public sealed class StrategyTemplateContentBindingViewModel
{
    public Guid BindingId { get; set; }
    public string ContentRefType { get; set; } = string.Empty;
    public Guid ContentRefId { get; set; }
    public string? ContentCodeDisplay { get; set; }
    public string? ContentVersionAtBinding { get; set; }
    public int SortOrder { get; set; }
    public string? Notes { get; set; }

    /// <summary>WP-SB-3-UIa — a template-level binding of a type retired by SB-3a: kept and read, never added again.</summary>
    public bool Retired { get; set; }
}

// ----- the read-only binding view (freshness hints) -----

public sealed class StrategyTemplateBindingsViewModel
{
    public Guid TemplateId { get; set; }
    public bool IsEffectiveAt { get; set; }
    public DateTimeOffset EffectiveAt { get; set; }
    public List<StrategyTemplateSegmentBindingHintViewModel> SegmentBindings { get; set; } = new();
    public StrategyTemplateFrequencyHintViewModel FrequencyIntent { get; set; } = new();
    public List<StrategyTemplateProductLineHintViewModel> ProductLines { get; set; } = new();
    public List<StrategyTemplateContentHintViewModel> ContentBindings { get; set; } = new();
}

public sealed class StrategyTemplateSegmentBindingHintViewModel
{
    public Guid SegmentId { get; set; }
    public string? SegmentCodeDisplay { get; set; }
    public int BoundVersion { get; set; }
    public string? CurrentStatus { get; set; }
    public bool Superseded { get; set; }
    public bool Archived { get; set; }
    public bool Resolvable { get; set; }
}

public sealed class StrategyTemplateFrequencyHintViewModel
{
    public string Mode { get; set; } = "none";
    public Guid? PolicyId { get; set; }
    public string? PolicyCodeDisplay { get; set; }
    public string? PolicyStatus { get; set; }
    public bool? TargetMatchesBoundSegment { get; set; }
    public string? FrequencyType { get; set; }
    public int? RequiredVisitCount { get; set; }
    public string? PeriodType { get; set; }
    public bool Binding { get; set; }
}

public sealed class StrategyTemplateProductLineHintViewModel
{
    public Guid LineId { get; set; }
    public Guid GlobalProductId { get; set; }
    public string? GlobalProductCodeDisplay { get; set; }
    public decimal TotalPercentage { get; set; }
    /// <summary>Always false — the product-to-SKU containment is not verifiable here (D-SKU-LINK).</summary>
    public bool ContainmentVerified { get; set; }
}

public sealed class StrategyTemplateContentHintViewModel
{
    public Guid ContentRefId { get; set; }
    public string ContentRefType { get; set; } = string.Empty;
    public string? ContentCodeDisplay { get; set; }
    public string? CurrentStatus { get; set; }
    public bool Archived { get; set; }
    public bool Published { get; set; }

    /// <summary>WP-SB-3-UIa — retired type (SB-3a): read only, never added again.</summary>
    public bool Retired { get; set; }
}

/// <summary>
/// The Create/Edit form model. The four binding lists travel as JSON in hidden inputs, filled by the embedded
/// repeaters in <c>form.js</c> — the same shape the runtime accepts, so the browser never invents a payload the API
/// does not know. TemplateStatus is display-only: the lifecycle moves through activate / archive.
/// </summary>
public sealed class StrategyTemplateEditViewModel
{
    public Guid? TemplateId { get; set; }

    [Required]
    [StringLength(64)]
    public string TemplateCode { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string TemplateName { get; set; } = string.Empty;

    [Required]
    public string SubjectType { get; set; } = "contact";

    public string TemplateStatus { get; set; } = "draft";

    /// <summary>WP-ST-EDIT-A — the play's address level (tenant / country / legal-entity / business-unit). Omitted
    /// means "derive it": a business unit makes it business-unit, nothing makes it tenant — exactly what a pre-scope
    /// play already meant. Mirrors the Campaign scope contract.</summary>
    public string? ScopeType { get; set; }

    /// <summary>WP-ST-EDIT-A — the country reference, when <see cref="ScopeType"/> is <c>country</c>.</summary>
    public string? CountryScope { get; set; }

    /// <summary>WP-ST-EDIT-A — the legal-entity reference, when <see cref="ScopeType"/> is <c>legal-entity</c>.</summary>
    public Guid? LegalEntityId { get; set; }

    [StringLength(64)]
    public string? BusinessUnitId { get; set; }

    /// <summary>WP-ST-EDIT-A — the country the author is filtering business units by. Informational: it narrows the
    /// business-unit picker and is never posted as the play's scope.</summary>
    public string? BusinessUnitCountryFilter { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    [Required]
    public DateTimeOffset? EffectiveFrom { get; set; }

    public DateTimeOffset? EffectiveTo { get; set; }

    public bool IsArchived { get; set; }
    public bool AreBindingsFrozen { get; set; }
    public int TemplateVersion { get; set; }

    // ----- the four embedded repeaters, carried as JSON -----
    public string? SegmentBindingsJson { get; set; }
    public string? FrequencyIntentJson { get; set; }
    public string? ProductLinesJson { get; set; }
    public string? ContentBindingsJson { get; set; }

    /// <summary>WP-ST-EDIT-W — one-click "save + activate". When the author picks "Kaydet ve aktifleştir" the form posts
    /// this flag; the controller, after a SUCCESSFUL save and only when the actor holds the activate permission, calls the
    /// EXISTING activate endpoint. It is not part of the CrmService payload (ToCreate/ToUpdatePayload ignore it) — purely
    /// a Web-controller orchestration signal.</summary>
    public bool ActivateAfterSave { get; set; }

    // ----- contract-driven options (never hardcoded in the view or in JS) -----
    public List<string> SubjectTypes { get; set; } = new();
    public List<string> TemplateStatuses { get; set; } = new();
    public List<string> BindingRoles { get; set; } = new();
    public List<string> FrequencyIntentModes { get; set; } = new();
    public List<string> SkuAllocationModes { get; set; } = new();
    public List<string> ContentRefTypes { get; set; } = new();
    public List<string> FrequencyTypes { get; set; } = new();
    public List<string> FrequencyPeriodTypes { get; set; } = new();

    /// <summary>WP-SB-3-UIa — the SB-3a product line roles (CRM <c>StrategyProductLineRoles</c>; the strategy template
    /// contract does not republish them). The first one is the default of a new line.</summary>
    public List<string> ProductLineRoles { get; set; } = ["promo", "non-promo"];

    public int MaxSegmentBindings { get; set; }
    public int MaxProductLines { get; set; }
    public int MaxSkuAllocationsPerLine { get; set; }
    public int MaxContentBindings { get; set; }
    public decimal RequiredAllocationTotal { get; set; } = 100m;

    /// <summary>Which value pickers the actor may actually browse. A picker that is not here is DISABLED with a stated
    /// reason rather than degrading to a free-text GUID field.</summary>
    public List<string> AvailablePickers { get; set; } = new();

    public bool CanPickGlobalProducts { get; set; }
    public bool CanPickGskus { get; set; }

    /// <summary>Set when the contract could not be read; the view shows it instead of a half-configured form.</summary>
    public string? ContractError { get; set; }

    /// <summary>WP-SB-3-UIa — the runtime's refusals that belong to a place on the form (a product line, the retired
    /// binding list), as <see cref="StrategyTemplateFormError"/> JSON; form.js renders each one, localised, under that
    /// place. Server-rendered only — never trusted from a post (the controller overwrites it on every render).</summary>
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public string? FormErrorsJson { get; set; }
}

/// <summary>WP-SB-3-UIa — one runtime refusal anchored to a place on the form. <see cref="Key"/> is the L10n key
/// (<c>Err_{code}</c>), so the raw code is never shown. <see cref="LineIndex"/> is the posted product line the refusal
/// names, or null when no line can be told apart (shown at the head of the product section).</summary>
public sealed class StrategyTemplateFormError
{
    public string Code { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Scope { get; set; } = StrategyTemplateErrorMap.ScopeForm;
    public int? LineIndex { get; set; }
}

/// <summary>
/// WP-SB-3-UIa — the SB-3a refusal codes this form understands and where each one is shown. A code not listed here
/// falls back to the generic summary with the runtime's own text, so a new code is never swallowed.
/// </summary>
public static class StrategyTemplateErrorMap
{
    public const string ScopeLine = "line";
    public const string ScopeBindings = "bindings";
    public const string ScopeForm = "form";

    public static readonly IReadOnlyDictionary<string, string> Scopes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["product_line_role_required"] = ScopeLine,
        ["product_line_role_invalid"] = ScopeLine,
        ["product_line_journey_required"] = ScopeLine,
        ["journey_not_published"] = ScopeLine,
        ["journey_product_mismatch"] = ScopeLine,
        ["content_binding_type_retired"] = ScopeBindings,
        ["bindings_frozen"] = ScopeForm
    };

    public static string KeyFor(string code) => "Err_" + code;
}

/// <summary>WP-SB-3-UIa — one option of a product line's journey picker (<c>api/line-journeys</c>).</summary>
public sealed class StrategyTemplateLineJourneyOption
{
    public Guid JourneyId { get; set; }
    public string JourneyCode { get; set; } = string.Empty;
    public string JourneyName { get; set; } = string.Empty;
    public string? LanguageCode { get; set; }
    public string? JourneyVersion { get; set; }
}

/// <summary>WP-SB-3-UIa — the slice of the CRM journey list the line-journey filter reads.</summary>
public sealed class StrategyTemplateJourneyApiModel
{
    public Guid JourneyId { get; set; }
    public string JourneyCode { get; set; } = string.Empty;
    public string JourneyName { get; set; } = string.Empty;
    public Guid SubjectId { get; set; }
    public string? LanguageCode { get; set; }
    public string? JourneyVersion { get; set; }
    public string JourneyStatus { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
}

/// <summary>WP-SB-3-UIa — the slice of the CRM subject list the line-journey filter reads (MOD-0162 SUBJECT-UI link).</summary>
public sealed class StrategyTemplateSubjectApiModel
{
    public Guid SubjectId { get; set; }
    public List<KnowledgeExternalReferenceViewModel> ExternalReferences { get; set; } = new();
}

public sealed class StrategyTemplateItemsApiModel<T>
{
    public List<T> Items { get; set; } = new();
}

// ----- gateway envelopes / contract -----

public sealed class StrategyTemplateGatewayResponse<T>
{
    public T? Data { get; set; }
    public List<string> Errors { get; set; } = new();
    public int StatusCode { get; set; }
    public bool IsSuccessful { get; set; }
}

public sealed class StrategyTemplateContractViewModel
{
    public bool IsReady { get; set; }
    public StrategyTemplateContractFeatures Features { get; set; } = new();
    public StrategyTemplateContractVocabularies Vocabularies { get; set; } = new();
    public StrategyTemplateContractLimitsViewModel Limits { get; set; } = new();
}

public sealed class StrategyTemplateContractFeatures
{
    public bool SupportsStrategyTemplateDefinition { get; set; }
    public bool SupportsSegmentBinding { get; set; }
    public bool SupportsProductSkuMix { get; set; }
    public bool SupportsContentBindingKnowledgePath { get; set; }
    public bool SupportsContentBindingEngagementJourney { get; set; }
    /// <summary>Always false: applying a play to a period is MOD-0155, not this page.</summary>
    public bool SupportsStrategyApply { get; set; }
    /// <summary>Always false: whether a SKU belongs to the product is not verified (D-SKU-LINK).</summary>
    public bool SupportsProductSkuContainmentValidation { get; set; }
}

public sealed class StrategyTemplateContractVocabularies
{
    public List<string> TemplateStatuses { get; set; } = new();
    public List<string> SubjectTypes { get; set; } = new();
    public List<string> SegmentBindingRoles { get; set; } = new();
    public List<string> FrequencyIntentModes { get; set; } = new();
    public List<string> SkuAllocationModes { get; set; } = new();
    public List<string> ContentRefTypes { get; set; } = new();
    public List<string> FrequencyTypes { get; set; } = new();
    public List<string> FrequencyPeriodTypes { get; set; } = new();
}

public sealed class StrategyTemplateContractLimitsViewModel
{
    public int MaxSegmentBindings { get; set; }
    public int MaxProductLines { get; set; }
    public int MaxSkuAllocationsPerLine { get; set; }
    public int MaxContentBindings { get; set; }
    public int MaxReferenceFanout { get; set; }
    public int MaxTemplatesPerSegment { get; set; }
    public int MaxRequiredVisitCount { get; set; }
    public decimal RequiredAllocationTotal { get; set; }
    public int PercentageScale { get; set; }
}
