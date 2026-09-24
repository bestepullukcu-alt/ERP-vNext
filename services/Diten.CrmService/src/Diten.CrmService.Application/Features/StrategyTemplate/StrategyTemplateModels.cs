namespace Diten.CrmService.Application.Features.StrategyTemplate;

// ---------------------------------------------------------------------------------------------------------------
// MOD-0167 FU04 — every DTO / read model of the StrategyTemplate feature, in ONE file (the single documented exception
// to the one-public-type-per-file convention). TenantId appears in NO payload: it is server-resolved from the claim.
// Nothing here carries a member, a member count, a generated policy id or a generated target id — a template BINDS and
// never produces, so there is no output to report.
// ---------------------------------------------------------------------------------------------------------------

/// <summary>One row of the template grid. The binding lists are projected OUT (only counters are exposed) so the list
/// stays cheap; the detail endpoint returns the bindings.</summary>
public sealed record StrategyTemplateListItemDto(
    Guid TemplateId,
    string TemplateCode,
    string TemplateName,
    string SubjectType,
    string TemplateStatus,
    int TemplateVersion,
    Guid VersionLineageId,
    bool Superseded,
    Guid? SupersededByTemplateId,
    string ScopeType,
    string EffectiveScopeType,
    string? CountryScope,
    Guid? LegalEntityId,
    string? BusinessUnitId,
    string? ScopeRef,
    string? Description,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    int SegmentBindingCount,
    string FrequencyIntentMode,
    int ProductLineCount,
    int SkuAllocationCount,
    // WP-ST-LIST2 — Σ of the product lines' LineWeightPercentage (the line-among-lines weighting that totals 100.00 when
    // present). Null when there is no product line or when none carries a weight, so the grid shows only a count and
    // never a misleading "0%". This is additive: the detail/create/update DTOs are untouched.
    decimal? ProductAllocationTotalPercentage,
    int ContentBindingCount,
    bool AreBindingsFrozen,
    DateTimeOffset? BindingsFrozenAt,
    DateTimeOffset? ActivatedAt,
    bool IsArchived,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record StrategyTemplateListDto(
    IReadOnlyList<StrategyTemplateListItemDto> Items,
    int TotalCount);

/// <summary>WP-ST-DETAIL-1 — one row of a play's version lineage for the Detay "Sürüm geçmişi" panel. A READ projection
/// of an existing lineage member (archived versions included); it decides nothing and persists nothing.</summary>
public sealed record StrategyTemplateVersionDto(
    Guid TemplateId,
    int TemplateVersion,
    string TemplateStatus,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? ArchivedAt,
    DateTimeOffset CreatedAt,
    bool IsCurrent);

public sealed record StrategyTemplateVersionsDto(
    IReadOnlyList<StrategyTemplateVersionDto> Versions);

/// <summary>Template detail, including all four embedded binding lists exactly as stored.</summary>
public sealed record StrategyTemplateDetailDto(
    Guid TemplateId,
    string TemplateCode,
    string TemplateName,
    string SubjectType,
    string TemplateStatus,
    int TemplateVersion,
    Guid VersionLineageId,
    bool Superseded,
    Guid? SupersededByTemplateId,
    string ScopeType,
    string EffectiveScopeType,
    string? CountryScope,
    Guid? LegalEntityId,
    string? BusinessUnitId,
    string? ScopeRef,
    string? Description,
    string? Notes,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    IReadOnlyList<StrategyTemplateSegmentBindingDto> SegmentBindings,
    StrategyTemplateFrequencyIntentDto FrequencyIntent,
    IReadOnlyList<StrategyTemplateProductLineDto> ProductLines,
    IReadOnlyList<StrategyTemplateContentBindingDto> ContentBindings,
    bool AreBindingsFrozen,
    DateTimeOffset? BindingsFrozenAt,
    DateTimeOffset? ActivatedAt,
    string? ActivatedBy,
    DateTimeOffset? ArchivedAt,
    string? ArchivedBy,
    bool IsArchived,
    int Version,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);

public sealed record StrategyTemplateSegmentBindingDto(
    Guid BindingId,
    Guid SegmentId,
    Guid SegmentLineageId,
    int SegmentVersionAtBinding,
    string? SegmentCodeDisplay,
    string? BindingRole,
    int SortOrder,
    string? Notes);

public sealed record StrategyTemplateFrequencyIntentDto(
    string Mode,
    Guid? VisitFrequencyPolicyId,
    string? PolicyCodeDisplay,
    string? FrequencyType,
    int? RequiredVisitCount,
    string? PeriodType,
    string? IntentNote);

public sealed record StrategyTemplateProductLineDto(
    Guid LineId,
    Guid GlobalProductId,
    string? GlobalProductCodeDisplay,
    decimal? LineWeightPercentage,
    string SkuAllocationMode,
    IReadOnlyList<StrategyTemplateSkuAllocationDto> SkuAllocations,
    decimal TotalPercentage,
    int SortOrder,
    string? Notes);

public sealed record StrategyTemplateSkuAllocationDto(
    Guid AllocationId,
    Guid GskuId,
    string? GskuCanonicalCodeDisplay,
    decimal Percentage,
    int SortOrder);

public sealed record StrategyTemplateContentBindingDto(
    Guid BindingId,
    string ContentRefType,
    Guid ContentRefId,
    string? ContentCodeDisplay,
    string? ContentVersionAtBinding,
    int SortOrder,
    string? Notes);

// ---------------------------------------------------------------------------------------------------------------
// Write inputs. Child ids are optional on input: the runtime assigns them, so an id belonging to another template can
// never be smuggled in.
// ---------------------------------------------------------------------------------------------------------------

public sealed record StrategyTemplateSegmentBindingInput(
    Guid SegmentId,
    string? BindingRole,
    int SortOrder,
    string? Notes);

public sealed record StrategyTemplateFrequencyIntentInput(
    string Mode,
    Guid? VisitFrequencyPolicyId,
    string? FrequencyType,
    int? RequiredVisitCount,
    string? PeriodType,
    string? IntentNote);

public sealed record StrategyTemplateProductLineInput(
    Guid GlobalProductId,
    string? GlobalProductCodeDisplay,
    decimal? LineWeightPercentage,
    string SkuAllocationMode,
    IReadOnlyList<StrategyTemplateSkuAllocationInput>? SkuAllocations,
    int SortOrder,
    string? Notes);

public sealed record StrategyTemplateSkuAllocationInput(
    Guid GskuId,
    string? GskuCanonicalCodeDisplay,
    decimal Percentage,
    int SortOrder);

public sealed record StrategyTemplateContentBindingInput(
    string ContentRefType,
    Guid ContentRefId,
    int SortOrder,
    string? Notes);

// ---------------------------------------------------------------------------------------------------------------
// The read-only binding view (§8.5). It reports the bindings PLUS derived, never-persisted freshness hints. Hints are
// warnings, not blocks: an active template whose content was later archived does NOT become invalid, because the past
// must stay explainable. And there is no member, no member count and no subject id anywhere in here — reading a
// template must never imply reading the people inside its segments (that stays crm.segment.resolve).
// ---------------------------------------------------------------------------------------------------------------

public sealed record StrategyTemplateBindingsDto(
    Guid TemplateId,
    string TemplateCode,
    string TemplateStatus,
    int TemplateVersion,
    bool IsEffectiveAt,
    DateTimeOffset EffectiveAt,
    IReadOnlyList<StrategyTemplateSegmentBindingViewDto> SegmentBindings,
    StrategyTemplateFrequencyIntentViewDto FrequencyIntent,
    IReadOnlyList<StrategyTemplateProductLineViewDto> ProductLines,
    IReadOnlyList<StrategyTemplateContentBindingViewDto> ContentBindings);

public sealed record StrategyTemplateSegmentBindingViewDto(
    Guid BindingId,
    Guid SegmentId,
    Guid BoundLineageId,
    int BoundVersion,
    string? SegmentCodeDisplay,
    string? BindingRole,
    string? CurrentStatus,
    bool Superseded,
    bool Archived,
    bool Resolvable,
    int SortOrder);

public sealed record StrategyTemplateFrequencyIntentViewDto(
    string Mode,
    Guid? PolicyId,
    string? PolicyCodeDisplay,
    string? PolicyStatus,
    bool? TargetMatchesBoundSegment,
    string? FrequencyType,
    int? RequiredVisitCount,
    string? PeriodType,
    string? IntentNote,
    bool Binding);

/// <summary>One product line as reported by the read-only binding view. <c>ContainmentVerified</c> is ALWAYS false
/// (D-SKU-LINK): whether the SKU belongs to the product is not verifiable with the MDM read surface this FU is allowed
/// to use, so the contract says so out loud rather than implying a check that never runs.</summary>
public sealed record StrategyTemplateProductLineViewDto(
    Guid LineId,
    Guid GlobalProductId,
    string? GlobalProductCodeDisplay,
    decimal? LineWeightPercentage,
    string SkuAllocationMode,
    IReadOnlyList<StrategyTemplateSkuAllocationDto> SkuAllocations,
    decimal TotalPercentage,
    bool ContainmentVerified,
    int SortOrder);

public sealed record StrategyTemplateContentBindingViewDto(
    Guid BindingId,
    string ContentRefType,
    Guid ContentRefId,
    string? ContentCodeDisplay,
    string? ContentVersionAtBinding,
    string? CurrentStatus,
    bool Archived,
    bool Published,
    int SortOrder);

// ---------------------------------------------------------------------------------------------------------------
// WP-ST-SCOPE — the cascading scope selector's read model. A deliberate mirror of the campaign's, three feeds and three
// readiness flags, so an author is never shown a silent empty dropdown with no way to act. A hardcoded fallback list is
// forbidden in all three cases.
// ---------------------------------------------------------------------------------------------------------------

/// <summary>WP-ST-SCOPE — one selectable value for a scope level.</summary>
public sealed record StrategyTemplateScopeOptionDto(string Value, string Label);

/// <summary>
/// WP-ST-SCOPE — one round trip for the cascading scope selector: the levels, the governed country values, the tenant's
/// referenceable legal entities, and the business units its territory plans cover.
/// <para><b>Three sources, three separate readiness flags.</b> An empty list because a reference set is unpublished, an
/// empty list because MDM is unreachable, and an empty list because no plan matches are three different situations. A UI
/// that cannot tell them apart shows the author a silent empty dropdown and no way to act, so each is reported on its
/// own.</para>
/// <para><c>BusinessUnitFromTerritory</c> says whether the business-unit list is the Territory-derived narrowing or the
/// full published vocabulary it falls back to when no plan matches — which keeps business-unit plays authorable before
/// their field plan exists.</para>
/// </summary>
public sealed record StrategyTemplateScopeOptionsDto(
    IReadOnlyList<string> ScopeTypes,
    IReadOnlyList<StrategyTemplateScopeOptionDto> Countries,
    bool CountrySetPublished,
    IReadOnlyList<StrategyTemplateScopeOptionDto> LegalEntities,
    bool LegalEntityLookupAvailable,
    IReadOnlyList<StrategyTemplateScopeOptionDto> BusinessUnits,
    bool BusinessUnitSetPublished,
    bool BusinessUnitFromTerritory);
