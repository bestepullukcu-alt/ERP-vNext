using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Queries;
using Diten.CrmService.Domain.Entities;
using Vfp = Diten.CrmService.Domain.Entities.VisitFrequencyPolicy;

namespace Diten.CrmService.Application.Features.VisitFrequencyPolicy.Resolve;

/// <summary>
/// MOD-0165 FU03 deterministic frequency resolution. Pure function of (request, active candidate policies, now) — it
/// performs no I/O and no writes, so it is unit-testable in isolation. Conflict resolution order (pack §G):
/// <list type="number">
/// <item>active + effective policy filter</item>
/// <item>target match (primary target + caller-supplied context ids; NO membership/traversal)</item>
/// <item>lowest Priority</item>
/// <item>most specific TargetType</item>
/// <item>latest EffectiveFrom</item>
/// <item>stable PolicyId tie-breaker</item>
/// </list>
/// No matching policy ⇒ <see cref="FrequencyStatus.Unknown"/> (a default frequency is never invented). A same-band
/// tie is still resolved deterministically by PolicyId but flagged <see cref="FrequencyStatus.Conflict"/>.
/// </summary>
public static class VisitFrequencyResolveEngine
{
    public static VisitFrequencyResolveResult Resolve(
        ResolveVisitFrequencyPolicyQuery request,
        IReadOnlyCollection<Vfp> activeCandidates,
        DateTimeOffset now)
    {
        var effectiveAt = request.EffectiveAt ?? now;
        var acceptedTargets = BuildAcceptedTargets(request);
        var reasonCodes = new List<string>();

        // Step 2 — target match. Only policies whose (TargetType, TargetId) is a requested target pair are considered.
        var targetMatched = activeCandidates
            .Where(p => acceptedTargets.Contains((FrequencyTargetType.Normalize(p.TargetType), p.TargetId)))
            .ToList();

        // Steps 1 + business scope — eliminate ineffective / context-mismatched policies (kept as diagnostics).
        var eliminated = new List<FrequencyCandidatePolicy>();
        var eligible = new List<Vfp>();
        foreach (var policy in targetMatched)
        {
            var elimination = Eliminate(policy, request, effectiveAt);
            if (elimination is null)
            {
                eligible.Add(policy);
            }
            else
            {
                if (!reasonCodes.Contains(elimination)) reasonCodes.Add(elimination);
                eliminated.Add(ToCandidate(policy, selected: false, elimination));
            }
        }

        // contact target without a location context — surfaced, never treated as a failure (pack §D).
        if (FrequencyTargetType.Normalize(request.TargetType) == FrequencyTargetType.Contact
            && request.TerritoryNodeId is null)
        {
            reasonCodes.Add(FrequencyReasonCodes.ContactLocationContextAbsent);
        }

        if (eligible.Count == 0)
        {
            reasonCodes.Insert(0, FrequencyReasonCodes.FrequencyUnknown);
            if (!reasonCodes.Contains(FrequencyReasonCodes.NoMatchingPolicy))
            {
                reasonCodes.Add(FrequencyReasonCodes.NoMatchingPolicy);
            }

            return new VisitFrequencyResolveResult(
                FrequencyStatus.Unknown, null, null, null, "No active, effective policy matches the requested target.",
                null, null, null, null, null, null, null, null, null,
                request.IncludeDiagnostics ? eliminated : Array.Empty<FrequencyCandidatePolicy>(),
                reasonCodes);
        }

        // Steps 3–6 — deterministic ordering.
        var ordered = eligible
            .OrderBy(p => p.Priority)
            .ThenBy(p => FrequencyTargetType.Specificity(p.TargetType))
            .ThenByDescending(p => p.EffectiveFrom)
            .ThenBy(p => p.Id)
            .ToList();

        var selected = ordered[0];
        var runnerUp = ordered.Count > 1 ? ordered[1] : null;

        var sameBandTie = ordered.Count(p =>
            p.Priority == selected.Priority
            && FrequencyTargetType.Specificity(p.TargetType) == FrequencyTargetType.Specificity(selected.TargetType)
            && p.EffectiveFrom == selected.EffectiveFrom) > 1;

        var discriminator = Discriminator(selected, runnerUp);
        reasonCodes.Insert(0, FrequencyReasonCodes.FrequencyPolicyResolved);
        reasonCodes.Add(discriminator);

        var status = sameBandTie ? FrequencyStatus.Conflict : FrequencyStatus.Resolved;
        if (sameBandTie && !reasonCodes.Contains(FrequencyReasonCodes.PolicyConflict))
        {
            reasonCodes.Add(FrequencyReasonCodes.PolicyConflict);
        }

        var candidates = new List<FrequencyCandidatePolicy>();
        if (request.IncludeDiagnostics)
        {
            candidates.Add(ToCandidate(selected, selected: true, discriminator));
            for (var i = 1; i < ordered.Count; i++)
            {
                candidates.Add(ToCandidate(ordered[i], selected: false, LoserReason(selected, ordered[i])));
            }

            candidates.AddRange(eliminated);
        }

        return new VisitFrequencyResolveResult(
            status,
            selected.Id,
            selected.PolicyCode,
            selected.PolicyName,
            SelectionReasonText(status, discriminator, selected),
            selected.RequiredVisitCount,
            selected.FrequencyType,
            selected.PeriodType,
            selected.CycleId,
            selected.CyclePeriodId,
            selected.EffectiveFrom,
            selected.EffectiveTo,
            selected.Priority,
            selected.Source,
            candidates,
            reasonCodes);
    }

    /// <summary>The set of (targetType, targetId) pairs the request is willing to match: the primary target plus any
    /// caller-supplied context id interpreted as its own target type. Nothing is derived from the account/contact
    /// master or a segment membership — the caller supplies the ids.</summary>
    private static HashSet<(string, Guid)> BuildAcceptedTargets(ResolveVisitFrequencyPolicyQuery request)
    {
        var set = new HashSet<(string, Guid)>
        {
            (FrequencyTargetType.Normalize(request.TargetType), request.TargetId)
        };

        void Add(string type, Guid? id)
        {
            if (id is { } value && value != Guid.Empty)
            {
                set.Add((type, value));
            }
        }

        Add(FrequencyTargetType.Segment, request.SegmentId);
        Add(FrequencyTargetType.TerritoryNode, request.TerritoryNodeId);
        Add(FrequencyTargetType.CampaignTarget, request.CampaignId);
        Add(FrequencyTargetType.ConceptNode, request.ConceptNodeId);
        Add(FrequencyTargetType.AudienceProfile, request.AudienceProfileId);
        return set;
    }

    /// <summary>Returns the elimination reason code, or null if the policy is eligible. Effective window first, then
    /// each business-scope constraint the policy declares (a null policy field imposes no constraint).</summary>
    private static string? Eliminate(Vfp policy, ResolveVisitFrequencyPolicyQuery request, DateTimeOffset effectiveAt)
    {
        if (!policy.IsEffectiveAt(effectiveAt))
        {
            return FrequencyReasonCodes.PolicyNotEffective;
        }

        if (policy.BusinessUnit is { } bu && !string.Equals(bu, request.BusinessUnit, StringComparison.OrdinalIgnoreCase))
        {
            return FrequencyReasonCodes.BusinessScopeMismatch;
        }

        if (policy.CampaignId is { } cid && request.CampaignId != cid)
        {
            return FrequencyReasonCodes.CampaignContextMissing;
        }

        if (policy.SegmentId is { } sid && request.SegmentId != sid)
        {
            return FrequencyReasonCodes.SegmentContextMissing;
        }

        if (policy.BrandId is { } brand && request.BrandId != brand)
        {
            return FrequencyReasonCodes.BusinessScopeMismatch;
        }

        if (policy.ProductId is { } product && request.ProductId != product)
        {
            return FrequencyReasonCodes.BusinessScopeMismatch;
        }

        return null;
    }

    private static string Discriminator(Vfp selected, Vfp? runnerUp)
    {
        if (runnerUp is null)
        {
            return FrequencyReasonCodes.PolicySelectedByPriority;
        }

        if (runnerUp.Priority > selected.Priority)
        {
            return FrequencyReasonCodes.PolicySelectedByPriority;
        }

        if (FrequencyTargetType.Specificity(runnerUp.TargetType) > FrequencyTargetType.Specificity(selected.TargetType))
        {
            return FrequencyReasonCodes.PolicySelectedBySpecificity;
        }

        if (runnerUp.EffectiveFrom < selected.EffectiveFrom)
        {
            return FrequencyReasonCodes.PolicySelectedByLatestEffectiveFrom;
        }

        return FrequencyReasonCodes.PolicyConflict;
    }

    private static string LoserReason(Vfp winner, Vfp loser)
    {
        if (loser.Priority > winner.Priority)
        {
            return FrequencyReasonCodes.PolicySelectedByPriority;
        }

        if (FrequencyTargetType.Specificity(loser.TargetType) > FrequencyTargetType.Specificity(winner.TargetType))
        {
            return FrequencyReasonCodes.PolicySelectedBySpecificity;
        }

        if (loser.EffectiveFrom < winner.EffectiveFrom)
        {
            return FrequencyReasonCodes.PolicySelectedByLatestEffectiveFrom;
        }

        return FrequencyReasonCodes.PolicyConflict;
    }

    private static string SelectionReasonText(string status, string discriminator, Vfp selected)
    {
        var basis = discriminator switch
        {
            FrequencyReasonCodes.PolicySelectedBySpecificity => "most specific target type",
            FrequencyReasonCodes.PolicySelectedByLatestEffectiveFrom => "latest effective-from",
            FrequencyReasonCodes.PolicyConflict => "stable policy id (same-band tie)",
            _ => "lowest priority"
        };

        return status == FrequencyStatus.Conflict
            ? $"Multiple policies tie in the top band; resolved deterministically by {basis} → {selected.PolicyCode}."
            : $"Selected {selected.PolicyCode} by {basis}.";
    }

    private static FrequencyCandidatePolicy ToCandidate(Vfp p, bool selected, string reason) => new(
        p.Id,
        p.PolicyCode,
        p.PolicyName,
        p.TargetType,
        p.TargetId,
        p.Priority,
        FrequencyTargetType.Specificity(p.TargetType),
        p.FrequencyType,
        p.RequiredVisitCount,
        p.PeriodType,
        p.EffectiveFrom,
        p.EffectiveTo,
        p.Source,
        p.Status,
        selected,
        reason);
}
