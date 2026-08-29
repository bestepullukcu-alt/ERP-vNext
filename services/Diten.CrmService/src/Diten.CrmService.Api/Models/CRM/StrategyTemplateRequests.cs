using Diten.CrmService.Application.Features.StrategyTemplate;

namespace Diten.CrmService.Api.Models.CRM;

// ---------------------------------------------------------------------------------------------------------------
// MOD-0167 FU04 request bodies. TenantId appears in NONE of them: it is resolved server-side from the claim, so a
// caller can neither choose nor leak a tenant. Nothing here accepts a generated child id, a member, a policy to write
// or a target to produce - a template BINDS and never produces.
// ---------------------------------------------------------------------------------------------------------------

/// <summary>Creates a strategy template. Status is absent: a play is always born draft, because putting one live is a
/// separate act with its own permission.</summary>
public sealed record CreateStrategyTemplateRequest(
    string TemplateCode,
    string TemplateName,
    string SubjectType,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? BusinessUnitId,
    string? Description,
    string? Notes,
    List<StrategyTemplateSegmentBindingRequest>? SegmentBindings,
    StrategyTemplateFrequencyIntentRequest? FrequencyIntent,
    List<StrategyTemplateProductLineRequest>? ProductLines,
    List<StrategyTemplateContentBindingRequest>? ContentBindings);

/// <summary>
/// Updates a strategy template. <c>TemplateCode</c> and <c>SubjectType</c> are absent because they are immutable, and
/// <c>TemplateStatus</c> is absent because the lifecycle moves only through activate / archive.
/// <para>Omitting a binding list entirely leaves it untouched — which is how the metadata of an ACTIVE (frozen) play
/// can be edited without tripping the freeze guard. Sending the same bindings back is also fine: the guard compares
/// what the play BINDS, not the ids the payload arrived with.</para>
/// </summary>
public sealed record UpdateStrategyTemplateRequest(
    string TemplateName,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? BusinessUnitId,
    string? Description,
    string? Notes,
    List<StrategyTemplateSegmentBindingRequest>? SegmentBindings,
    StrategyTemplateFrequencyIntentRequest? FrequencyIntent,
    List<StrategyTemplateProductLineRequest>? ProductLines,
    List<StrategyTemplateContentBindingRequest>? ContentBindings,
    int? ExpectedVersion);

/// <summary>One bound segment. The lineage id, the version stamp and the display code are NOT accepted here: the
/// runtime reads them from the segment itself, so they can neither be forged nor become a second source of truth.</summary>
public sealed record StrategyTemplateSegmentBindingRequest(
    Guid SegmentId,
    string? BindingRole,
    int SortOrder,
    string? Notes)
{
    public StrategyTemplateSegmentBindingInput ToInput() => new(SegmentId, BindingRole, SortOrder, Notes);
}

/// <summary>The frequency intent. Exactly one shape is valid, and NO shape writes a MOD-0165 policy.</summary>
public sealed record StrategyTemplateFrequencyIntentRequest(
    string Mode,
    Guid? VisitFrequencyPolicyId,
    string? FrequencyType,
    int? RequiredVisitCount,
    string? PeriodType,
    string? IntentNote)
{
    public StrategyTemplateFrequencyIntentInput ToInput() =>
        new(Mode, VisitFrequencyPolicyId, FrequencyType, RequiredVisitCount, PeriodType, IntentNote);
}

/// <summary>One promoted MDM global product and, for a sku-allocated line, its SKU split.</summary>
public sealed record StrategyTemplateProductLineRequest(
    Guid GlobalProductId,
    string? GlobalProductCodeDisplay,
    decimal? LineWeightPercentage,
    string SkuAllocationMode,
    List<StrategyTemplateSkuAllocationRequest>? SkuAllocations,
    int SortOrder,
    string? Notes)
{
    public StrategyTemplateProductLineInput ToInput() => new(
        GlobalProductId, GlobalProductCodeDisplay, LineWeightPercentage, SkuAllocationMode,
        SkuAllocations?.Select(a => a.ToInput()).ToList(), SortOrder, Notes);
}

/// <summary>One SKU share. Percentages are stored exactly as sent — the runtime never normalises them.</summary>
public sealed record StrategyTemplateSkuAllocationRequest(
    Guid GskuId,
    string? GskuCanonicalCodeDisplay,
    decimal Percentage,
    int SortOrder)
{
    public StrategyTemplateSkuAllocationInput ToInput() =>
        new(GskuId, GskuCanonicalCodeDisplay, Percentage, SortOrder);
}

/// <summary>One bound MOD-0162 presentation. The type is required: a bare id cannot be resolved to an aggregate.</summary>
public sealed record StrategyTemplateContentBindingRequest(
    string ContentRefType,
    Guid ContentRefId,
    int SortOrder,
    string? Notes)
{
    public StrategyTemplateContentBindingInput ToInput() => new(ContentRefType, ContentRefId, SortOrder, Notes);
}
