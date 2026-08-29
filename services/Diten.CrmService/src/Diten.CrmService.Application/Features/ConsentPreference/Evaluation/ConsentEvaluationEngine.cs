using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.ConsentPreference.Evaluation;

/// <summary>
/// MOD-0164 FU02 deterministic, fail-closed consent/preference evaluation. A pure function of (request, candidate
/// consents, candidate preferences, now) — it performs no I/O and <b>no writes</b>, so it is unit-testable in isolation
/// and the GET endpoint that hosts it can never mutate data.
/// <para><b>Resolution order (FU02 §E):</b></para>
/// <list type="number">
/// <item>tenant match (applied by the repository — never widened here)</item>
/// <item>subject match (exact SubjectType + SubjectId; a consent is never inherited from a broader subject)</item>
/// <item>channel match (exact; a channel permission is never transferable)</item>
/// <item>purpose match (exact; a purpose permission is never transferable)</item>
/// <item>scope specificity (scope instance &gt; scope kind &gt; general)</item>
/// <item>effective window (not-yet-effective and expired records are eliminated, visibly)</item>
/// <item>restrictive status wins (denied/withdrawn/restricted &gt; granted &gt; unknown)</item>
/// <item>latest EffectiveFrom</item>
/// <item>stable ConsentId tie-breaker</item>
/// </list>
/// <para>
/// <b>Fail-closed invariants.</b> No matching consent ⇒ <see cref="ConsentEligibilityStatus.Unknown"/>, never allowed.
/// An explicitly authored <c>unknown</c> consent ⇒ unknown, never allowed. Expired / out-of-window records are never
/// allowed but stay visible as reason codes. A restrictive preference blocks even a granted consent. An absent
/// preference changes nothing — no default preference is invented. Nothing is chosen silently: every outcome carries
/// reason codes, a selection reason and (when requested) full candidate diagnostics.
/// </para>
/// </summary>
public static class ConsentEvaluationEngine
{
    public static ConsentEvaluationResult Evaluate(
        ConsentEvaluationRequest request,
        IReadOnlyCollection<ConsentRecord> consents,
        IReadOnlyCollection<PreferenceRecord> preferences,
        DateTimeOffset now)
    {
        var effectiveAt = request.EffectiveAt ?? now;
        var subjectType = ConsentSubjectType.Normalize(request.SubjectType);
        var channel = ConsentChannel.Normalize(request.Channel);
        var purpose = ConsentPurpose.Normalize(request.Purpose);
        var requestScopeType = string.IsNullOrWhiteSpace(request.ScopeType)
            ? null
            : ConsentScopeType.Normalize(request.ScopeType);
        var requestScopeId = request.ScopeId is { } sid && sid != Guid.Empty ? sid : (Guid?)null;

        var reasonCodes = new List<string>();
        var eliminated = new List<CandidateConsent>();
        var eligible = new List<ConsentRecord>();

        // Steps 2–4 — the question dimensions. A record answering a different subject/channel/purpose question is not
        // a candidate at all (it is not "eliminated": it was never about this question).
        var questionMatched = consents.Where(c =>
            ConsentSubjectType.Normalize(c.SubjectType) == subjectType
            && c.SubjectId == request.SubjectId
            && ConsentChannel.Normalize(c.Channel) == channel
            && ConsentPurpose.Normalize(c.Purpose) == purpose);

        // Steps 5–6 — scope applicability and the effective window (each elimination stays visible).
        foreach (var record in questionMatched)
        {
            var elimination = EliminateConsent(record, requestScopeType, requestScopeId, effectiveAt);
            if (elimination is null)
            {
                eligible.Add(record);
            }
            else
            {
                AddReason(reasonCodes, elimination);
                eliminated.Add(ToCandidate(record, selected: false, elimination));
            }
        }

        // Steps 7–9 — deterministic ordering. Scope specificity first (a scope-pinned record governs its scope), then
        // the fail-closed status precedence, then recency, then the stable id.
        var ordered = eligible
            .OrderBy(c => ConsentScopeType.Specificity(c.ScopeType, c.ScopeId))
            .ThenBy(c => ConsentStatuses.Precedence(c.ConsentStatus))
            .ThenByDescending(c => c.EffectiveFrom)
            .ThenBy(c => c.Id)
            .ToList();

        var selected = ordered.FirstOrDefault();
        var runnerUp = ordered.Count > 1 ? ordered[1] : null;

        string eligibility;
        string decision;
        string discriminator;

        if (selected is null)
        {
            eligibility = ConsentEligibilityStatus.Unknown;
            decision = ConsentDecision.ConsentUnknown;
            discriminator = ConsentReasonCodes.NoMatchingConsent;
            AddReason(reasonCodes, ConsentReasonCodes.ConsentUnknown);
            AddReason(reasonCodes, ConsentReasonCodes.NoMatchingConsent);
        }
        else
        {
            discriminator = Discriminator(selected, runnerUp);
            var statusReason = StatusReason(selected.ConsentStatus);
            AddReason(reasonCodes, statusReason);
            AddReason(reasonCodes, discriminator);

            (eligibility, decision) = ConsentStatuses.Normalize(selected.ConsentStatus) switch
            {
                ConsentStatuses.Granted => (ConsentEligibilityStatus.Allowed, ConsentDecision.ConsentGranted),
                ConsentStatuses.Unknown => (ConsentEligibilityStatus.Unknown, ConsentDecision.ConsentUnknown),
                _ => (ConsentEligibilityStatus.Blocked, ConsentDecision.ConsentBlocked)
            };

            // A same-band tie is still resolved deterministically (stable id) but the ambiguity must be visible.
            var sameBandTie = ordered.Count(c =>
                ConsentScopeType.Specificity(c.ScopeType, c.ScopeId)
                    == ConsentScopeType.Specificity(selected.ScopeType, selected.ScopeId)
                && ConsentStatuses.Precedence(c.ConsentStatus) == ConsentStatuses.Precedence(selected.ConsentStatus)
                && c.EffectiveFrom == selected.EffectiveFrom) > 1;
            if (sameBandTie)
            {
                AddReason(reasonCodes, ConsentReasonCodes.ConsentAmbiguousConflict);
            }
        }

        // ---- Preference overlay ----
        // A preference NEVER grants: it can only turn allowed/unknown into blocked. It is applied in every branch, so a
        // restrictive preference is honoured even when consent is absent (fail-closed).
        var applicable = new List<PreferenceRecord>();
        var preferenceCandidates = new List<CandidatePreference>();
        var blockingPreferences = new List<PreferenceRecord>();

        foreach (var preference in preferences)
        {
            if (ConsentSubjectType.Normalize(preference.SubjectType) != subjectType
                || preference.SubjectId != request.SubjectId
                || !preference.AppliesToChannel(channel))
            {
                continue; // a preference for another subject/channel is not about this question
            }

            if (preference.IsArchived())
            {
                preferenceCandidates.Add(ToCandidate(preference, restrictive: false, ConsentReasonCodes.ConsentArchived));
                continue;
            }

            if (!preference.IsEffectiveAt(effectiveAt))
            {
                AddReason(reasonCodes, ConsentReasonCodes.PreferenceNotEffective);
                preferenceCandidates.Add(
                    ToCandidate(preference, restrictive: false, ConsentReasonCodes.PreferenceNotEffective));
                continue;
            }

            applicable.Add(preference);

            var restriction = RestrictionReason(preference, channel);
            if (restriction is null)
            {
                var advisory = AdvisoryReason(preference);
                AddReason(reasonCodes, advisory);
                preferenceCandidates.Add(ToCandidate(preference, restrictive: false, advisory));
                continue;
            }

            blockingPreferences.Add(preference);
            AddReason(reasonCodes, restriction);
            AddReason(reasonCodes, ConsentReasonCodes.PreferenceRestricted);
            if (!string.Equals(preference.Channel, PreferenceChannel.AnyChannel, StringComparison.OrdinalIgnoreCase))
            {
                AddReason(reasonCodes, ConsentReasonCodes.PreferenceChannelBlocked);
            }

            preferenceCandidates.Add(ToCandidate(preference, restrictive: true, restriction));
        }

        if (blockingPreferences.Count > 0)
        {
            eligibility = ConsentEligibilityStatus.Blocked;
            decision = ConsentDecision.PreferenceRestricted;
        }

        var matchedPreferenceIds = applicable
            .OrderBy(p => p.Priority)
            .ThenBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();

        var consentCandidates = new List<CandidateConsent>();
        if (request.IncludeDiagnostics)
        {
            if (selected is not null)
            {
                consentCandidates.Add(ToCandidate(selected, selected: true, discriminator));
                for (var i = 1; i < ordered.Count; i++)
                {
                    consentCandidates.Add(ToCandidate(ordered[i], selected: false, LoserReason(selected, ordered[i])));
                }
            }

            consentCandidates.AddRange(eliminated);
        }

        return new ConsentEvaluationResult(
            eligibility,
            decision,
            subjectType,
            request.SubjectId,
            channel,
            purpose,
            requestScopeType,
            requestScopeId,
            effectiveAt,
            selected?.Id,
            matchedPreferenceIds,
            reasonCodes,
            SelectionReasonText(eligibility, decision, selected, blockingPreferences, discriminator),
            consentCandidates,
            request.IncludeDiagnostics ? preferenceCandidates : Array.Empty<CandidatePreference>(),
            ConsentEvaluationResult.CurrentEvaluatorVersion,
            now);
    }

    /// <summary>Controlled failure result: the provider never throws into a consumer and never returns 500 — it returns
    /// <c>unknown</c> with an explicit error reason code, which a consumer must treat exactly like unknown.</summary>
    public static ConsentEvaluationResult ControlledUnknown(
        ConsentEvaluationRequest request, DateTimeOffset now, string reason)
        => new(
            ConsentEligibilityStatus.Unknown,
            ConsentDecision.ConsentUnknown,
            ConsentSubjectType.Normalize(request.SubjectType),
            request.SubjectId,
            ConsentChannel.Normalize(request.Channel),
            ConsentPurpose.Normalize(request.Purpose),
            string.IsNullOrWhiteSpace(request.ScopeType) ? null : ConsentScopeType.Normalize(request.ScopeType),
            request.ScopeId is { } id && id != Guid.Empty ? id : null,
            request.EffectiveAt ?? now,
            MatchedConsentId: null,
            MatchedPreferenceIds: Array.Empty<Guid>(),
            ReasonCodes: new[] { ConsentReasonCodes.ConsentUnknown, ConsentReasonCodes.ConsentEvaluationError },
            SelectionReason: reason,
            CandidateConsents: Array.Empty<CandidateConsent>(),
            CandidatePreferences: Array.Empty<CandidatePreference>(),
            ConsentEvaluationResult.CurrentEvaluatorVersion,
            now);

    /// <summary>Returns the elimination reason code, or null when the record governs the request. An archived record is
    /// excluded from evaluation but stays readable through the CRUD reads.</summary>
    private static string? EliminateConsent(
        ConsentRecord record, string? requestScopeType, Guid? requestScopeId, DateTimeOffset effectiveAt)
    {
        if (record.IsArchived())
        {
            return ConsentReasonCodes.ConsentArchived;
        }

        // A scope-bound record answers only its own scope. Asking the general question does not consume a scoped
        // record, and asking about scope X never consumes a record bound to scope Y.
        if (!string.IsNullOrWhiteSpace(record.ScopeType))
        {
            var recordScopeType = ConsentScopeType.Normalize(record.ScopeType);
            if (requestScopeType is null || recordScopeType != requestScopeType)
            {
                return ConsentReasonCodes.ConsentScopeMismatch;
            }

            if (record.ScopeId is { } recordScopeId && recordScopeId != Guid.Empty && recordScopeId != requestScopeId)
            {
                return ConsentReasonCodes.ConsentScopeMismatch;
            }
        }

        if (record.IsNotYetEffectiveAt(effectiveAt))
        {
            return ConsentReasonCodes.ConsentNotEffective;
        }

        if (record.HasExpiredAt(effectiveAt))
        {
            return ConsentReasonCodes.ConsentExpired;
        }

        // An explicitly authored 'expired' status is treated exactly like a closed window: visible, never allowed.
        return ConsentStatuses.Normalize(record.ConsentStatus) == ConsentStatuses.Expired
            ? ConsentReasonCodes.ConsentExpired
            : null;
    }

    private static string StatusReason(string? status) => ConsentStatuses.Normalize(status) switch
    {
        ConsentStatuses.Granted => ConsentReasonCodes.ConsentGranted,
        ConsentStatuses.Denied => ConsentReasonCodes.ConsentDenied,
        ConsentStatuses.Withdrawn => ConsentReasonCodes.ConsentWithdrawn,
        ConsentStatuses.Restricted => ConsentReasonCodes.ConsentRestricted,
        _ => ConsentReasonCodes.ConsentUnknown
    };

    /// <summary>Which rule made the selected record win over the runner-up (visible provenance, never a silent choice).</summary>
    private static string Discriminator(ConsentRecord selected, ConsentRecord? runnerUp)
    {
        if (runnerUp is null)
        {
            return ConsentScopeType.Specificity(selected.ScopeType, selected.ScopeId) < 3
                ? ConsentReasonCodes.ConsentSelectedBySpecificity
                : ConsentReasonCodes.ConsentSelectedByStableId;
        }

        if (ConsentScopeType.Specificity(runnerUp.ScopeType, runnerUp.ScopeId)
            > ConsentScopeType.Specificity(selected.ScopeType, selected.ScopeId))
        {
            return ConsentReasonCodes.ConsentSelectedBySpecificity;
        }

        if (ConsentStatuses.Precedence(runnerUp.ConsentStatus) > ConsentStatuses.Precedence(selected.ConsentStatus))
        {
            return ConsentReasonCodes.ConsentSelectedByRestrictiveStatus;
        }

        return runnerUp.EffectiveFrom < selected.EffectiveFrom
            ? ConsentReasonCodes.ConsentSelectedByLatestEffectiveFrom
            : ConsentReasonCodes.ConsentSelectedByStableId;
    }

    private static string LoserReason(ConsentRecord winner, ConsentRecord loser)
    {
        if (ConsentScopeType.Specificity(loser.ScopeType, loser.ScopeId)
            > ConsentScopeType.Specificity(winner.ScopeType, winner.ScopeId))
        {
            return ConsentReasonCodes.ConsentSelectedBySpecificity;
        }

        if (ConsentStatuses.Precedence(loser.ConsentStatus) > ConsentStatuses.Precedence(winner.ConsentStatus))
        {
            return ConsentReasonCodes.ConsentSelectedByRestrictiveStatus;
        }

        return loser.EffectiveFrom < winner.EffectiveFrom
            ? ConsentReasonCodes.ConsentSelectedByLatestEffectiveFrom
            : ConsentReasonCodes.ConsentSelectedByStableId;
    }

    /// <summary>
    /// The restriction reason a preference imposes, or null when it is advisory. Only the boolean restriction types
    /// with value <c>true</c> restrict — and <c>do-not-visit</c> restricts the <c>visit</c> channel only, because a
    /// visit restriction is not an e-mail restriction. A <c>frequency-cap</c> is an advisory upper-bound signal: it is
    /// surfaced but never blocks, since the frequency policy SoR is MOD-0165 and no frequency runtime is opened here.
    /// </summary>
    private static string? RestrictionReason(PreferenceRecord preference, string channel)
    {
        var type = PreferenceType.Normalize(preference.PreferenceType);
        if (!PreferenceType.IsBooleanRestriction(type) || !preference.IsRestrictiveValueTrue())
        {
            return null;
        }

        return type switch
        {
            PreferenceType.DoNotVisit => channel == ConsentChannel.Visit
                ? ConsentReasonCodes.PreferenceDoNotVisit
                : null,
            PreferenceType.DoNotContact => ConsentReasonCodes.PreferenceDoNotContact,
            _ => null
        };
    }

    private static string AdvisoryReason(PreferenceRecord preference)
        => PreferenceType.Normalize(preference.PreferenceType) == PreferenceType.FrequencyCap
            ? ConsentReasonCodes.PreferenceFrequencyCap
            : ConsentReasonCodes.PreferenceAdvisoryOnly;

    private static string SelectionReasonText(
        string eligibility,
        string decision,
        ConsentRecord? selected,
        IReadOnlyList<PreferenceRecord> blockingPreferences,
        string discriminator)
    {
        if (decision == ConsentDecision.PreferenceRestricted)
        {
            var types = string.Join(", ", blockingPreferences
                .Select(p => PreferenceType.Normalize(p.PreferenceType))
                .Distinct());
            var consentPart = selected is null
                ? "no consent record governs the request"
                : $"consent {selected.Id} is '{ConsentStatuses.Normalize(selected.ConsentStatus)}'";
            return $"Blocked by restrictive preference ({types}); {consentPart}. " +
                   "A preference can only restrict further — it never grants.";
        }

        if (selected is null)
        {
            return "No consent record matches this subject × channel × purpose × scope × time. " +
                   "Eligibility is unknown — unknown is NOT allowed and no default is assumed.";
        }

        var basis = discriminator switch
        {
            ConsentReasonCodes.ConsentSelectedBySpecificity => "the most specific scope",
            ConsentReasonCodes.ConsentSelectedByRestrictiveStatus => "restrictive status precedence at equal scope specificity",
            ConsentReasonCodes.ConsentSelectedByLatestEffectiveFrom => "the latest effective-from",
            _ => "the stable consent id (same-band tie)"
        };

        return $"Selected consent {selected.Id} ('{ConsentStatuses.Normalize(selected.ConsentStatus)}') by {basis} " +
               $"→ eligibility '{eligibility}'.";
    }

    private static void AddReason(ICollection<string> reasonCodes, string reason)
    {
        if (!reasonCodes.Contains(reason))
        {
            reasonCodes.Add(reason);
        }
    }

    private static CandidateConsent ToCandidate(ConsentRecord c, bool selected, string reason) => new(
        c.Id,
        c.SubjectType,
        c.SubjectId,
        c.Channel,
        c.Purpose,
        c.ScopeType,
        c.ScopeId,
        ConsentScopeType.Specificity(c.ScopeType, c.ScopeId),
        c.ConsentStatus,
        ConsentStatuses.Precedence(c.ConsentStatus),
        c.LegalBasis,
        c.EffectiveFrom,
        c.EffectiveTo,
        c.Source,
        selected,
        reason);

    private static CandidatePreference ToCandidate(PreferenceRecord p, bool restrictive, string reason) => new(
        p.Id,
        p.SubjectType,
        p.SubjectId,
        p.Channel,
        p.PreferenceType,
        p.PreferenceValue,
        p.Priority,
        p.EffectiveFrom,
        p.EffectiveTo,
        p.Source,
        restrictive,
        reason);
}
