namespace Diten.CrmService.Application.Features.CyclePeriod;

// ---------------------------------------------------------------------------------------------------------------
// MOD-0165 FU06/FU07 — every DTO / read model of the CyclePeriod feature, in ONE file (the single documented exception
// to the one-public-type-per-file convention). TenantId appears in NO payload: it is server-resolved from the claim.
// Nothing here carries a MicroTarget row, a campaign, a frequency policy or a working-day count — a period says which
// period it is and where it lives, and consumers do the rest.
// FU07 adds the scope quartet (ScopeType + ScopeRef + the typed reference) to every shape that already carried
// BusinessUnitId. BusinessUnitId itself keeps its name and position: renaming an FU06 field would break every caller
// for no gain.
// ---------------------------------------------------------------------------------------------------------------

/// <summary>One row of the period grid.</summary>
public sealed record CyclePeriodListItemDto(
    Guid CyclePeriodId,
    string CycleCode,
    string CycleName,
    int Year,
    int SequenceInYear,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    string ScopeType,
    string? ScopeRef,
    string? CountryScope,
    Guid? LegalEntityId,
    string? BusinessUnitId,
    string? BusinessUnitSource,
    string? BusinessUnitCountryContext,
    string? Description,
    string CycleStatus,
    bool IsClosed,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? ClosedAt,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record CyclePeriodListDto(IReadOnlyList<CyclePeriodListItemDto> Items, int TotalCount);

/// <summary>Period detail, including the provenance stamps.</summary>
public sealed record CyclePeriodDetailDto(
    Guid CyclePeriodId,
    string CycleCode,
    string CycleName,
    int Year,
    int SequenceInYear,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    string ScopeType,
    string? ScopeRef,
    string? CountryScope,
    Guid? LegalEntityId,
    string? BusinessUnitId,
    string? BusinessUnitSource,
    string? BusinessUnitCountryContext,
    string? Description,
    string CycleStatus,
    bool IsDraft,
    bool IsActive,
    bool IsClosed,
    DateTimeOffset? ActivatedAt,
    string? ActivatedBy,
    DateTimeOffset? ClosedAt,
    string? ClosedBy,
    int Version,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);

/// <summary>A lightweight row for a consumer's period picker (MOD-0155 and friends). It is the same data the read seam
/// exposes, and never more.</summary>
public sealed record CyclePeriodSelectorItemDto(
    Guid CyclePeriodId,
    string CycleCode,
    string CycleName,
    int Year,
    int SequenceInYear,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    string CycleStatus,
    string ScopeType,
    string? ScopeRef,
    string? CountryScope,
    Guid? LegalEntityId,
    string? BusinessUnitId);

public sealed record CyclePeriodSelectorDto(IReadOnlyList<CyclePeriodSelectorItemDto> Items, int TotalCount);

/// <summary>
/// The answer of <c>resolve-active</c>. <c>Outcome</c> is one of
/// <see cref="Diten.CrmService.Domain.Entities.CyclePeriodResolutionOutcomes"/>: a consumer must branch on it and may
/// never turn <c>none</c> or <c>ambiguous</c> into a period of its own choosing.
/// <para>The request scope is echoed back so a cached or logged answer is self-describing, and
/// <c>ResolvedScopeType</c> names the level that actually answered — the difference between "my business unit has its
/// own calendar" and "my business unit is following the tenant's".</para>
/// </summary>
public sealed record CyclePeriodResolutionDto(
    string Outcome,
    DateTimeOffset At,
    string? Country,
    Guid? LegalEntityId,
    string? BusinessUnitId,
    string? ResolvedScopeType,
    CyclePeriodSelectorItemDto? Period,
    IReadOnlyList<Guid> CandidateIds,
    string? Reason);

// ── FU07 scope options (the cascading picker's one round trip) ──────────────────────────────────────────────────────

/// <summary>One selectable option. <see cref="Hint"/> carries the "why is this here?" detail — for a business unit, the
/// territory plans it came from.</summary>
public sealed record CyclePeriodScopeOptionDto(string Value, string Label, string? Hint = null);

/// <summary>
/// Everything the scope selector needs, with a separate readiness flag per source. The flags matter: an empty list
/// because a reference set is unpublished, an empty list because MDM is unreachable and an empty list because no
/// territory plan matches are three different situations, and a UI that cannot tell them apart shows the author a
/// silent empty dropdown. A hardcoded fallback list is forbidden in all three cases.
/// </summary>
public sealed record CyclePeriodScopeOptionsDto(
    IReadOnlyList<string> ScopeTypes,
    IReadOnlyList<CyclePeriodScopeOptionDto> Countries,
    bool CountryReady,
    IReadOnlyList<CyclePeriodScopeOptionDto> LegalEntities,
    bool LegalEntityReady,
    IReadOnlyList<CyclePeriodScopeOptionDto> BusinessUnits,
    bool BusinessUnitReady,

    /// <summary>
    /// True when the business-unit list was DERIVED from matching territory plans; false when it fell back to the
    /// published <c>business-unit</c> vocabulary because no plan matched. The fallback is deliberate: a period must
    /// stay authorable when its field plan does not exist yet, or when the country vocabularies of MOD-0151 and this
    /// FU have not been aligned yet (F-COUNTRY-SOT). The UI shows the difference so the author knows whether the list
    /// is a plan or an alphabet.
    /// </summary>
    bool BusinessUnitFromTerritory,
    string CountrySetCode,
    string BusinessUnitSetCode);
