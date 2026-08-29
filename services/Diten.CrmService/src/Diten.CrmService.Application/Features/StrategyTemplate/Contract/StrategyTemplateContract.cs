using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.StrategyTemplate.Contract;

/// <summary>
/// MOD-0167 FU04 contract surface: feature flags + in-domain vocabulary + supported filters + limits + error codes +
/// permissions + limitations. Published so a contract-driven UI needs no hardcoded list anywhere.
/// <para>The flags that are <b>false</b> matter as much as the true ones: apply/generate, MicroTarget generation,
/// cycle periods, frequency-policy writing, CampaignTarget generation, membership resolution, UCLN planning, brand and
/// Lsku binding and product/SKU containment validation are all explicitly absent.</para>
/// </summary>
public sealed record StrategyTemplateContractDto(
    string ModuleId,
    string ModuleName,
    string Service,
    string RuntimeScope,
    Guid TenantId,
    bool IsReady,
    StrategyTemplateFeatureFlags Features,
    StrategyTemplateVocabularyDto Vocabularies,
    StrategyTemplateSupportedFilters SupportedFilters,
    StrategyTemplateContractLimits Limits,
    IReadOnlyList<string> ErrorCodes,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Limitations);

/// <summary>The in-domain vocabulary exactly as the runtime enforces it. <see cref="FrequencyTypes"/> and
/// <see cref="FrequencyPeriodTypes"/> are re-published from MOD-0165's own constants, NOT redefined: the declared
/// intent is validated against MOD-0165 and a UI must offer the same values MOD-0165 accepts.</summary>
public sealed record StrategyTemplateVocabularyDto(
    IReadOnlyList<string> TemplateStatuses,
    IReadOnlyList<string> SubjectTypes,
    IReadOnlyList<string> SegmentBindingRoles,
    IReadOnlyList<string> FrequencyIntentModes,
    IReadOnlyList<string> SkuAllocationModes,
    IReadOnlyList<string> ContentRefTypes,
    IReadOnlyList<string> FrequencyTypes,
    IReadOnlyList<string> FrequencyPeriodTypes)
{
    public static StrategyTemplateVocabularyDto Current => new(
        StrategyTemplateStatuses.All,
        StrategyTemplateSubjectTypes.All,
        StrategySegmentBindingRoles.All,
        StrategyFrequencyIntentModes.All,
        StrategySkuAllocationModes.All,
        StrategyContentRefTypes.All,
        FrequencyType.All,
        FrequencyPeriodType.All);
}

/// <summary>Which list filters the runtime actually honours. A filter that is not here is not silently ignored — a UI
/// can see it is unsupported instead of showing a control that does nothing.</summary>
public sealed record StrategyTemplateSupportedFilters(IReadOnlyList<string> List)
{
    public static StrategyTemplateSupportedFilters Current => new(new[]
    {
        "templateStatus", "subjectType", "businessUnitId", "templateCode", "segmentId", "search", "includeArchived"
    });
}

/// <summary>The published ceilings. Every overflow is an explicit refusal, never a silent truncation.</summary>
public sealed record StrategyTemplateContractLimits(
    int MaxSegmentBindings,
    int MaxProductLines,
    int MaxSkuAllocationsPerLine,
    int MaxContentBindings,
    int MaxReferenceFanout,
    int MaxTemplatesPerSegment,
    int MaxRequiredVisitCount,
    decimal RequiredAllocationTotal,
    int PercentageScale)
{
    public static StrategyTemplateContractLimits Current => new(
        StrategyTemplateLimits.MaxSegmentBindings,
        StrategyTemplateLimits.MaxProductLines,
        StrategyTemplateLimits.MaxSkuAllocationsPerLine,
        StrategyTemplateLimits.MaxContentBindings,
        StrategyTemplateLimits.MaxReferenceFanout,
        StrategyTemplateLimits.MaxTemplatesPerSegment,
        StrategyTemplateLimits.MaxRequiredVisitCount,
        StrategyTemplateLimits.RequiredAllocationTotal,
        StrategyTemplateLimits.PercentageScale);
}
