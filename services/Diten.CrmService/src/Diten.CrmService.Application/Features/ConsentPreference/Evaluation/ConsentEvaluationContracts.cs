namespace Diten.CrmService.Application.Features.ConsentPreference.Evaluation;

/// <summary>
/// Outcome of a consent/preference evaluation. Never a plan, a target, a segment membership, a frequency verdict or a
/// content recommendation — only whether this subject may be reached on this channel for this purpose, right now.
/// <para><b>unknown is not allowed.</b> A consumer that cannot distinguish the two must treat unknown as not-ready.</para>
/// </summary>
public static class ConsentEligibilityStatus
{
    /// <summary>A granted consent governs the request and no restrictive preference applies.</summary>
    public const string Allowed = "allowed";

    /// <summary>A restrictive consent status (denied/withdrawn/restricted) or a restrictive preference applies.</summary>
    public const string Blocked = "blocked";

    /// <summary>No consent answers the question (or the governing record is explicitly <c>unknown</c>). NOT allowed —
    /// a default is never invented.</summary>
    public const string Unknown = "unknown";

    /// <summary>Reserved: the request is recognized but consent does not apply to it. The FU02 engine never emits it;
    /// it exists so a consumer can be written against the full contract.</summary>
    public const string NotApplicable = "not_applicable";
}

/// <summary>The decision axis behind <see cref="ConsentEligibilityStatus"/> — which rule produced the outcome.</summary>
public static class ConsentDecision
{
    public const string ConsentGranted = "consent_granted";
    public const string ConsentBlocked = "consent_blocked";
    public const string ConsentUnknown = "consent_unknown";
    public const string PreferenceRestricted = "preference_restricted";
    public const string NotApplicable = "not_applicable";
}

/// <summary>Canonical reason codes surfaced on an evaluation (diagnostics + audit + consumer provenance). A result is
/// never silent: every outcome carries at least one reason code and a human-readable selection reason.</summary>
public static class ConsentReasonCodes
{
    // Consent status outcomes
    public const string ConsentGranted = "consent_granted";
    public const string ConsentDenied = "consent_denied";
    public const string ConsentWithdrawn = "consent_withdrawn";
    public const string ConsentRestricted = "consent_restricted";
    public const string ConsentUnknown = "consent_unknown";
    public const string ConsentExpired = "consent_expired";
    public const string ConsentNotEffective = "consent_not_effective";
    public const string NoMatchingConsent = "no_matching_consent";

    // Preference outcomes
    public const string PreferenceDoNotContact = "preference_do_not_contact";
    public const string PreferenceDoNotVisit = "preference_do_not_visit";
    public const string PreferenceChannelBlocked = "preference_channel_blocked";
    public const string PreferenceFrequencyCap = "preference_frequency_cap";
    public const string PreferenceRestricted = "preference_restricted";

    // Selection provenance
    public const string ConsentSelectedBySpecificity = "consent_selected_by_specificity";
    public const string ConsentSelectedByLatestEffectiveFrom = "consent_selected_by_latest_effective_from";
    public const string ConsentSelectedByRestrictiveStatus = "consent_selected_by_restrictive_status";
    public const string ConsentSelectedByStableId = "consent_selected_by_stable_id";

    // Elimination / diagnostics (FU02 extensions to the FU01 code set)
    public const string ConsentScopeMismatch = "consent_scope_mismatch";
    public const string ConsentArchived = "consent_archived";
    public const string PreferenceNotEffective = "preference_not_effective";
    public const string PreferenceAdvisoryOnly = "preference_advisory_only";
    public const string ConsentAmbiguousConflict = "consent_ambiguous_conflict";

    /// <summary>Controlled error reason: the provider failed internally and returned <c>unknown</c> rather than a 500.
    /// A consumer must treat this exactly like <c>unknown</c> — never as allowed.</summary>
    public const string ConsentEvaluationError = "consent_evaluation_error";
}

/// <summary>A consent record considered during evaluation — the selected one plus every eliminated one, each with the
/// reason it did or did not govern. Diagnostics only; there is no scoring beyond the deterministic tie-break order.</summary>
public sealed record CandidateConsent(
    Guid ConsentId,
    string SubjectType,
    Guid SubjectId,
    string Channel,
    string Purpose,
    string? ScopeType,
    Guid? ScopeId,
    int ScopeSpecificity,
    string ConsentStatus,
    int StatusPrecedence,
    string LegalBasis,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string Source,
    bool Selected,
    string Reason);

/// <summary>A preference record considered during evaluation, with whether it restricted the outcome.</summary>
public sealed record CandidatePreference(
    Guid PreferenceId,
    string SubjectType,
    Guid SubjectId,
    string Channel,
    string PreferenceType,
    string PreferenceValue,
    int Priority,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string Source,
    bool Restrictive,
    string Reason);

/// <summary>
/// Read-only evaluation result. Deliberately carries NO campaign / visit-plan / route / due-status / last-visit /
/// frequency / content field — MOD-0164 answers eligibility and nothing else. <see cref="EvaluatorVersion"/> and
/// <see cref="EvaluatedAt"/> exist so a consumer (MOD-0155, MOD-0165 FU04, MOD-0167) can store provenance without
/// copying any consent data.
/// </summary>
public sealed record ConsentEvaluationResult(
    string EligibilityStatus,
    string Decision,
    string SubjectType,
    Guid SubjectId,
    string Channel,
    string Purpose,
    string? ScopeType,
    Guid? ScopeId,
    DateTimeOffset EffectiveAt,
    Guid? MatchedConsentId,
    IReadOnlyList<Guid> MatchedPreferenceIds,
    IReadOnlyList<string> ReasonCodes,
    string SelectionReason,
    IReadOnlyList<CandidateConsent> CandidateConsents,
    IReadOnlyList<CandidatePreference> CandidatePreferences,
    string EvaluatorVersion,
    DateTimeOffset EvaluatedAt)
{
    /// <summary>Bumped whenever the deterministic resolution rules change, so stored provenance stays interpretable.</summary>
    public const string CurrentEvaluatorVersion = "mod-0164-fu02.v1";
}

/// <summary>Read-only evaluation request. Every id is supplied by the caller: no subject master, availability,
/// territory, segment or campaign lookup happens during evaluation.</summary>
public sealed record ConsentEvaluationRequest(
    string SubjectType,
    Guid SubjectId,
    string Channel,
    string Purpose,
    DateTimeOffset? EffectiveAt = null,
    string? ScopeType = null,
    Guid? ScopeId = null,
    bool IncludeDiagnostics = true);
