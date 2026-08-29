using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.CyclePeriod.Contract;

/// <summary>
/// MOD-0165 FU06 contract surface: feature flags + in-domain vocabulary + supported filters + limits + error codes +
/// permissions + limitations. Published so a contract-driven UI needs no hardcoded status, no hardcoded ceiling and no
/// hardcoded outcome name anywhere — a hardcoded list is a second source of truth, and it drifts silently.
/// </summary>
public sealed record CyclePeriodContractDto(
    string ModuleId,
    string ModuleName,
    string Service,
    string RuntimeScope,
    Guid TenantId,
    bool IsReady,
    CyclePeriodFeatureFlags Features,
    CyclePeriodVocabularyDto Vocabularies,
    CyclePeriodSupportedFilters SupportedFilters,
    CyclePeriodContractLimits Limits,
    IReadOnlyList<string> ErrorCodes,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Limitations);

/// <summary>The in-domain vocabulary exactly as the runtime enforces it (D-VOCAB = A / D-VOCAB-SCOPE = A).
/// <para><see cref="ScopeTypes"/> is published in PRECEDENCE order (most specific first) — the same array the resolver
/// walks — so a UI can render the levels in the order they actually win, without hardcoding an order that could drift
/// away from the engine's.</para></summary>
public sealed record CyclePeriodVocabularyDto(
    IReadOnlyList<string> CycleStatuses,
    IReadOnlyList<string> ResolutionOutcomes,
    IReadOnlyList<string> ScopeTypes,
    IReadOnlyList<string> BusinessUnitSources)
{
    public static CyclePeriodVocabularyDto Current => new(
        CyclePeriodStatuses.All,
        CyclePeriodResolutionOutcomes.All,
        CyclePeriodScopeTypes.ByPrecedence,
        CyclePeriodBusinessUnitSources.All);
}

/// <summary>Which list filters the runtime actually honours. A filter that is not here is not silently ignored — a UI
/// can see it is unsupported instead of showing a control that does nothing.</summary>
public sealed record CyclePeriodSupportedFilters(IReadOnlyList<string> List)
{
    public static CyclePeriodSupportedFilters Current => new(new[]
    {
        "cycleStatus", "year", "businessUnitId", "cycleCode", "coversDate", "search",
        // FU07 scope filters. businessUnitId keeps its FU06 name and meaning on purpose: renaming it would break
        // every caller written against FU06 for no gain.
        "scopeType", "country", "legalEntityId"
    });
}

/// <summary>Published ceilings, so the editor enforces the same numbers the runtime does.</summary>
public sealed record CyclePeriodContractLimits(
    int MaxCycleCodeLength,
    int MaxCycleNameLength,
    int MaxDescriptionLength,
    int MaxBusinessUnitIdLength,
    int CountryScopeLength,
    int MinYear,
    int MaxYear,
    int MinSequenceInYear,
    int MaxSequenceInYear)
{
    public static CyclePeriodContractLimits Current => new(
        CyclePeriodLimits.MaxCycleCodeLength,
        CyclePeriodLimits.MaxCycleNameLength,
        CyclePeriodLimits.MaxDescriptionLength,
        CyclePeriodLimits.MaxBusinessUnitIdLength,
        CyclePeriodLimits.CountryScopeLength,
        CyclePeriodLimits.MinYear,
        CyclePeriodLimits.MaxYear,
        CyclePeriodLimits.MinSequenceInYear,
        CyclePeriodLimits.MaxSequenceInYear);
}

/// <summary>Machine-readable refusal codes, so a UI and a smoke script can branch without parsing prose.</summary>
public static class CyclePeriodErrorCodes
{
    public const string CodeRequired = "cycle_period_code_required";
    public const string CodeInvalid = "cycle_period_code_invalid";
    public const string CodeTaken = "cycle_period_code_taken";
    public const string NameRequired = "cycle_period_name_required";
    public const string NameInvalid = "cycle_period_name_invalid";
    public const string YearInvalid = "cycle_period_year_invalid";
    public const string SequenceInvalid = "cycle_period_sequence_invalid";
    public const string SequenceTaken = "cycle_period_sequence_taken";
    public const string WindowInvalid = "cycle_period_window_invalid";

    /// <summary>The period does not start in the planning year it claims. Only the START is anchored; the end
    /// date may cross into the next year, which is the reason Year is a field of its own.</summary>
    public const string StartYearMismatch = "cycle_period_start_year_mismatch";
    public const string BusinessUnitInvalid = "cycle_period_business_unit_invalid";
    public const string DescriptionInvalid = "cycle_period_description_invalid";
    public const string StatusUnknown = "cycle_period_status_unknown";
    public const string Overlap = "cycle_period_overlap";
    public const string Closed = "cycle_period_closed";
    public const string DatesImmutable = "cycle_period_dates_immutable";
    public const string AlreadyActive = "cycle_period_already_active";
    public const string ConcurrencyConflict = "cycle_period_concurrency_conflict";

    // ── FU07 scope codes ───────────────────────────────────────────────────────────────────────────────────────────
    public const string ScopeTypeUnknown = "cycle_period_scope_type_unknown";

    /// <summary>References were supplied that do not belong to the chosen ScopeType. Refused rather than cleared: an
    /// author who filled a field meant something by it.</summary>
    public const string ScopeAmbiguous = "cycle_period_scope_ambiguous";

    public const string ScopeReferenceRequired = "cycle_period_scope_reference_required";

    /// <summary>ScopeType is half of the identity, so it cannot be edited — close the period and open a new one.</summary>
    public const string ScopeImmutable = "cycle_period_scope_immutable";

    public const string CountryInvalid = "cycle_period_country_invalid";

    /// <summary>The code is not a published value of the governed country reference set.</summary>
    public const string CountryUnknown = "cycle_period_country_unknown";

    public const string BusinessUnitUnknown = "cycle_period_business_unit_unknown";

    /// <summary>The reference set itself is not published yet — a different fact from "the value is wrong", and the
    /// author needs to be told which one it is (one is fixed by an operator, the other by retyping).</summary>
    public const string ReferenceSetUnpublished = "cycle_period_reference_set_unpublished";

    /// <summary>MDM answered: no such legal entity, or it is not ACTIVE/referenceable. A 400 — the dependency spoke.</summary>
    public const string LegalEntityNotReferenceable = "cycle_period_legal_entity_not_referenceable";

    /// <summary>MDM did not answer (timeout, 5xx, auth rejection, malformed body). A 503 with NOTHING persisted — we do
    /// not know, so we must not pretend the input was wrong.</summary>
    public const string LegalEntityDependencyUnavailable = "cycle_period_legal_entity_dependency_unavailable";

    public static readonly IReadOnlyList<string> All = new[]
    {
        CodeRequired, CodeInvalid, CodeTaken, NameRequired, NameInvalid, YearInvalid, SequenceInvalid, SequenceTaken,
        WindowInvalid, StartYearMismatch, BusinessUnitInvalid, DescriptionInvalid, StatusUnknown, Overlap, Closed,
        DatesImmutable, AlreadyActive, ConcurrencyConflict,
        ScopeTypeUnknown, ScopeAmbiguous, ScopeReferenceRequired, ScopeImmutable, CountryInvalid, CountryUnknown,
        BusinessUnitUnknown, ReferenceSetUnpublished, LegalEntityNotReferenceable, LegalEntityDependencyUnavailable
    };
}
