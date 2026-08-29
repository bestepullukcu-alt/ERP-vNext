namespace Diten.CrmService.Application.Features.VisitFrequencyPolicy.Resolve;

/// <summary>Outcome of a frequency resolve. Never a plan, a due/overdue verdict, a last-visit date or a consent
/// decision — only which frequency policy governs the target, if any.</summary>
public static class FrequencyStatus
{
    /// <summary>Exactly one policy governs the target (chosen deterministically).</summary>
    public const string Resolved = "resolved";

    /// <summary>No policy matches. The frequency is genuinely unknown — a default is NEVER invented.</summary>
    public const string Unknown = "unknown";

    /// <summary>Two or more policies tie in the top precedence band (priority + specificity + effectiveFrom). Still
    /// resolved deterministically by stable PolicyId, but flagged so the ambiguity is visible.</summary>
    public const string Conflict = "conflict";

    /// <summary>Resolve does not apply to the request (reserved: recognized-but-not-applicable target contexts).</summary>
    public const string NotApplicable = "not_applicable";
}

/// <summary>Canonical reason codes surfaced on a resolve (diagnostics + audit).</summary>
public static class FrequencyReasonCodes
{
    public const string FrequencyPolicyResolved = "frequency_policy_resolved";
    public const string FrequencyUnknown = "frequency_unknown";
    public const string NoMatchingPolicy = "no_matching_policy";
    public const string PolicyNotEffective = "policy_not_effective";
    public const string PolicyInactive = "policy_inactive";
    public const string PolicyArchived = "policy_archived";
    public const string PolicyConflict = "policy_conflict";
    public const string PolicySelectedByPriority = "policy_selected_by_priority";
    public const string PolicySelectedBySpecificity = "policy_selected_by_specificity";
    public const string PolicySelectedByLatestEffectiveFrom = "policy_selected_by_latest_effective_from";
    public const string CampaignContextMissing = "campaign_context_missing";
    public const string SegmentContextMissing = "segment_context_missing";
    public const string CycleContextMissing = "cycle_context_missing";
    public const string BusinessScopeMismatch = "business_scope_mismatch";
    public const string ContactLocationContextAbsent = "contact_location_context_absent";
}

/// <summary>A candidate policy considered during resolution — the selected one plus every eliminated one, each with
/// the reason it did or did not win. This is diagnostics only; no scoring beyond the deterministic tie-break order.</summary>
public sealed record FrequencyCandidatePolicy(
    Guid PolicyId,
    string PolicyCode,
    string PolicyName,
    string TargetType,
    Guid TargetId,
    int Priority,
    int Specificity,
    string FrequencyType,
    int RequiredVisitCount,
    string PeriodType,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string Source,
    string Status,
    bool Selected,
    string Reason);

/// <summary>Read-only resolve result. Deliberately carries NO route/visit/due/last-visit/consent field.</summary>
public sealed record VisitFrequencyResolveResult(
    string FrequencyStatus,
    Guid? SelectedFrequencyPolicyId,
    string? SelectedPolicyCode,
    string? SelectedPolicyName,
    string? SelectionReason,
    int? RequiredVisitCount,
    string? FrequencyType,
    string? PeriodType,
    Guid? CycleId,
    Guid? CyclePeriodId,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    int? Priority,
    string? Source,
    IReadOnlyList<FrequencyCandidatePolicy> CandidatePolicies,
    IReadOnlyList<string> ReasonCodes);
