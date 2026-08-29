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

    [StringLength(64)]
    public string? BusinessUnitId { get; set; }

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

    // ----- contract-driven options (never hardcoded in the view or in JS) -----
    public List<string> SubjectTypes { get; set; } = new();
    public List<string> TemplateStatuses { get; set; } = new();
    public List<string> BindingRoles { get; set; } = new();
    public List<string> FrequencyIntentModes { get; set; } = new();
    public List<string> SkuAllocationModes { get; set; } = new();
    public List<string> ContentRefTypes { get; set; } = new();
    public List<string> FrequencyTypes { get; set; } = new();
    public List<string> FrequencyPeriodTypes { get; set; } = new();

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
