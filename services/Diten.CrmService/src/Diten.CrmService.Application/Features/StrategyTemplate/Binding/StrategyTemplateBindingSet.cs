namespace Diten.CrmService.Application.Features.StrategyTemplate.Binding;

// ---------------------------------------------------------------------------------------------------------------
// MOD-0167 FU04 consumption seam DTOs. A consumer (MOD-0155 MicroTarget) reads a play's bindings and produces its own
// rows; nothing here is a produced row, and nothing here carries a member, a member count or a subject id.
// ---------------------------------------------------------------------------------------------------------------

/// <summary>What one active play binds at an instant. A REPORT, not an instruction: it contains ids to look up, never
/// generated targets, policies or plan lines.</summary>
public sealed record StrategyTemplateBindingSet(
    Guid TemplateId,
    string TemplateCode,
    string TemplateName,
    string SubjectType,
    int TemplateVersion,
    Guid VersionLineageId,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    IReadOnlyList<Guid> SegmentIds,
    StrategyTemplateFrequencyIntentSnapshot FrequencyIntent,
    IReadOnlyList<StrategyTemplateProductMixLine> ProductLines,
    IReadOnlyList<StrategyTemplateContentReference> ContentBindings);

/// <summary>The frequency intent as a consumer sees it. <see cref="Binding"/> is true ONLY for a policy reference: a
/// declared intent is documentation of the author's rhythm, and MOD-0165 neither reads nor honours it.</summary>
public sealed record StrategyTemplateFrequencyIntentSnapshot(
    string Mode,
    Guid? VisitFrequencyPolicyId,
    string? FrequencyType,
    int? RequiredVisitCount,
    string? PeriodType,
    bool Binding);

/// <summary>One product and its SKU split. <see cref="ContainmentVerified"/> is always false — see D-SKU-LINK.</summary>
public sealed record StrategyTemplateProductMixLine(
    Guid LineId,
    Guid GlobalProductId,
    decimal? LineWeightPercentage,
    string SkuAllocationMode,
    IReadOnlyList<StrategyTemplateSkuShare> SkuAllocations,
    decimal TotalPercentage,
    bool ContainmentVerified);

public sealed record StrategyTemplateSkuShare(Guid GskuId, decimal Percentage, int SortOrder);

public sealed record StrategyTemplateContentReference(
    string ContentRefType,
    Guid ContentRefId,
    int SortOrder);

/// <summary>A template row in the reverse question ("which plays bind this segment?").</summary>
public sealed record StrategyTemplateSummary(
    Guid TemplateId,
    string TemplateCode,
    string TemplateName,
    string TemplateStatus,
    int TemplateVersion,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo);
