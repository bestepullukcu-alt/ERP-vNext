using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.Segmentation.Resolution;

/// <summary>
/// MOD-0167 FU02 membership engine — the two-phase, bounded, deterministic resolver (D3 + D4).
/// <para><b>It persists nothing.</b> No collection is written, no usage log is kept, no cache is populated: a segment
/// is a definition, and membership is derived every time it is asked for. Materialisation is a performance
/// optimisation, and an optimisation designed without measurement is a guess — that is FU-B.</para>
/// <para><b>Shape of the work.</b> Phase 1 is ONE Mongo pushdown for the native part of the rule (an over-approximation,
/// so it can only return a superset); Phase 1.5 and Phase 2 add one bulk read per source; Phase 3 applies the manual
/// rows; Phase 4 orders and pages. The N x M product of "every segment for every person" is never computed anywhere.</para>
/// <para><b>Determinism contract</b> for an unchanged source data set and a given
/// (TenantId, SegmentId, SegmentVersion, effectiveAt): the same member SET, in the same ORDER (SubjectId ascending —
/// never a DateTimeOffset key, which is a BSON array and a parallel-array sort trap), with the same REASON CODES, and
/// every eliminated candidate visible with its reason. Accepted + eliminated always equals the candidate count.</para>
/// <para><b>Ceilings are answers, not truncations.</b> Above MaxCandidateSet the resolver returns nothing and the
/// endpoint answers 422, because a partial member list is more dangerous than no list: nobody can tell it is partial.</para>
/// </summary>
public sealed class SegmentMembershipResolver
{
    public const string ResolverVersion = "mod-0167-fu02.v1";

    private readonly ISegmentCandidateSource _candidates;
    private readonly ISegmentAttributeSourceReader _attributes;
    private readonly ITargetCustomerRepository _targets;

    public SegmentMembershipResolver(
        ISegmentCandidateSource candidates,
        ISegmentAttributeSourceReader attributes,
        ITargetCustomerRepository targets)
    {
        _candidates = candidates;
        _attributes = attributes;
        _targets = targets;
    }

    /// <summary>A resolution outcome. <see cref="CandidateCapExceeded"/> means the rule is too wide and the caller must
    /// narrow it (422); <see cref="Result"/> is then null and NOTHING partial is handed back.</summary>
    public sealed record Outcome(bool CandidateCapExceeded, SegmentResolutionResultDto? Result);

    public async Task<Outcome> ResolveAsync(
        Guid tenantId,
        Segment segment,
        DateTimeOffset effectiveAt,
        int limit,
        int offset,
        bool includeExcluded,
        CancellationToken cancellationToken)
    {
        var notInEffect = ReasonSegmentNotInEffect(segment, effectiveAt);
        if (notInEffect is not null)
        {
            // The segment exists, it just does not apply at this instant: 200 with an empty, EXPLAINED answer, never a
            // 404 and never a silently empty list.
            return new Outcome(false, EmptyResult(segment, effectiveAt, limit, offset, notInEffect));
        }

        var manual = string.Equals(segment.SegmentType, SegmentTypes.Dynamic, StringComparison.Ordinal)
            ? Array.Empty<TargetCustomer>()
            : (await _targets.ListBySegmentAsync(tenantId, segment.Id, cancellationToken))
                .Where(t => !t.IsArchived() && t.IsEffectiveAt(effectiveAt))
                .ToArray();

        var manualIncludes = manual.Where(t => t.IsInclude()).Select(t => t.SubjectId).ToHashSet();
        var manualExcludes = manual.Where(t => t.IsExclude()).Select(t => t.SubjectId).ToHashSet();

        // Display labels for the rows that never pass through the candidate projection (a manual include/exclude for a
        // subject the rule did not return). The manual row already stores one, so this costs nothing.
        var manualNames = manual
            .Where(t => !string.IsNullOrWhiteSpace(t.SubjectDisplayName))
            .GroupBy(t => t.SubjectId)
            .ToDictionary(g => g.Key, g => g.First().SubjectDisplayName);

        var members = new List<SegmentMemberDto>();
        var excluded = new List<SegmentMemberDto>();

        if (string.Equals(segment.SegmentType, SegmentTypes.Static, StringComparison.Ordinal))
        {
            // A static segment IS its manual list. The criteria engine is not invoked at all: not "invoked and
            // ignored", not invoked. That is provable with a fake candidate source whose call count stays zero.
            AddStaticMembers(segment, manualIncludes, manualExcludes, manualNames, members, excluded);
        }
        else
        {
            var load = await _candidates.LoadCandidatesAsync(
                tenantId, segment.SubjectType, segment.Criteria, segment.MatchMode,
                SegmentLimits.MaxCandidateSet, cancellationToken);

            if (load.ExceededCap)
            {
                return new Outcome(true, null);
            }

            var context = await _attributes.LoadAsync(
                tenantId, segment, load.Candidates, effectiveAt, cancellationToken);

            EvaluateCandidates(segment, load.Candidates, context, manualExcludes, members, excluded);
            AddManualIncludes(
                segment, load.Candidates, manualIncludes, manualExcludes, manualNames, members, excluded);
        }

        // Phase 4 — one total, deterministic ordering key. Never a DateTimeOffset field.
        members = members.OrderBy(m => m.SubjectId).ToList();
        excluded = excluded.OrderBy(m => m.SubjectId).ToList();

        var page = members.Skip(Math.Max(0, offset)).Take(Math.Max(0, limit)).ToList();

        return new Outcome(false, new SegmentResolutionResultDto(
            segment.Id,
            segment.SegmentCode,
            segment.SegmentVersion,
            segment.SegmentType,
            segment.SubjectType,
            segment.IsSuperseded(),
            effectiveAt,
            SegmentEffective: true,
            CandidateCount: members.Count + excluded.Count,
            MatchedCount: members.Count,
            ExcludedCount: excluded.Count,
            TotalMemberCount: members.Count,
            Limit: limit,
            Offset: offset,
            MaxCandidateSet: SegmentLimits.MaxCandidateSet,
            Members: page,
            Excluded: includeExcluded ? excluded : Array.Empty<SegmentMemberDto>(),
            ReasonCodes: Array.Empty<string>(),
            ResolvedAt: DateTimeOffset.UtcNow,
            ResolverVersion: ResolverVersion));
    }

    /// <summary>The single-subject question (MOD-0167-FU01 section 5). One document plus, at most, one derived read;
    /// never the candidate scan.</summary>
    public async Task<SegmentMembershipVerdictDto> EvaluateAsync(
        Guid tenantId,
        Segment segment,
        string subjectType,
        Guid subjectId,
        DateTimeOffset effectiveAt,
        CancellationToken cancellationToken)
    {
        var notInEffect = ReasonSegmentNotInEffect(segment, effectiveAt);
        if (notInEffect is not null)
        {
            // There is no membership data at this instant, so the honest answer is unknown - which is never member.
            return Verdict(segment, subjectType, subjectId, null, effectiveAt,
                SegmentMembershipVerdicts.Unknown, null, notInEffect);
        }

        var manual = string.Equals(segment.SegmentType, SegmentTypes.Dynamic, StringComparison.Ordinal)
            ? Array.Empty<TargetCustomer>()
            : (await _targets.ListBySubjectAsync(tenantId, subjectType, subjectId, cancellationToken))
                .Where(t => t.SegmentId == segment.Id && !t.IsArchived() && t.IsEffectiveAt(effectiveAt))
                .ToArray();

        var manualName = manual
            .Select(t => t.SubjectDisplayName)
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));

        if (manual.Any(t => t.IsExclude()))
        {
            // A manual exclusion is absolute: it beats the rule, by design.
            return Verdict(segment, subjectType, subjectId, manualName, effectiveAt,
                SegmentMembershipVerdicts.NotMember, SegmentMembershipSources.ManualExclude,
                SegmentReasonCodes.ManualExclude);
        }

        if (manual.Any(t => t.IsInclude()))
        {
            return Verdict(segment, subjectType, subjectId, manualName, effectiveAt,
                SegmentMembershipVerdicts.Member, SegmentMembershipSources.ManualInclude,
                SegmentReasonCodes.ManualInclude);
        }

        if (string.Equals(segment.SegmentType, SegmentTypes.Static, StringComparison.Ordinal))
        {
            return Verdict(segment, subjectType, subjectId, manualName, effectiveAt,
                SegmentMembershipVerdicts.NotMember, SegmentMembershipSources.StaticList,
                SegmentReasonCodes.CriteriaNotMatched);
        }

        var snapshots = await _candidates.LoadSubjectsByIdsAsync(
            tenantId, segment.SubjectType, new[] { subjectId }, cancellationToken);
        var snapshot = snapshots.FirstOrDefault();
        if (snapshot is null)
        {
            // The subject is not visible in this tenant. No membership can be asserted - and none is invented.
            return Verdict(segment, subjectType, subjectId, null, effectiveAt,
                SegmentMembershipVerdicts.Unknown, null, SegmentReasonCodes.AttributeNotResolvable);
        }

        var context = await _attributes.LoadAsync(
            tenantId, segment, new[] { snapshot }, effectiveAt, cancellationToken);
        var outcome = SegmentCriteriaEvaluator.Evaluate(segment, context.For(subjectId, segment.SubjectType));

        var verdict = outcome.Matched switch
        {
            true => SegmentMembershipVerdicts.Member,
            false => SegmentMembershipVerdicts.NotMember,
            _ => SegmentMembershipVerdicts.Unknown
        };

        return Verdict(segment, subjectType, subjectId, snapshot.DisplayName, effectiveAt, verdict,
            outcome.Matched == true ? SegmentMembershipSources.Criteria : null,
            outcome.ReasonCodes.ToArray());
    }

    private static void AddStaticMembers(
        Segment segment,
        IReadOnlyCollection<Guid> manualIncludes,
        IReadOnlyCollection<Guid> manualExcludes,
        IReadOnlyDictionary<Guid, string?> manualNames,
        ICollection<SegmentMemberDto> members,
        ICollection<SegmentMemberDto> excluded)
    {
        foreach (var subjectId in manualIncludes)
        {
            if (manualExcludes.Contains(subjectId))
            {
                excluded.Add(new SegmentMemberDto(subjectId, segment.SubjectType,
                    manualNames.GetValueOrDefault(subjectId),
                    SegmentMembershipVerdicts.NotMember, SegmentMembershipSources.ManualExclude,
                    new[] { SegmentReasonCodes.ManualExclude }));
                continue;
            }

            members.Add(new SegmentMemberDto(subjectId, segment.SubjectType,
                manualNames.GetValueOrDefault(subjectId),
                SegmentMembershipVerdicts.Member, SegmentMembershipSources.StaticList,
                new[] { SegmentReasonCodes.ManualInclude }));
        }

        foreach (var subjectId in manualExcludes.Where(id => !manualIncludes.Contains(id)))
        {
            excluded.Add(new SegmentMemberDto(subjectId, segment.SubjectType,
                manualNames.GetValueOrDefault(subjectId),
                SegmentMembershipVerdicts.NotMember, SegmentMembershipSources.ManualExclude,
                new[] { SegmentReasonCodes.ManualExclude }));
        }
    }

    private static void EvaluateCandidates(
        Segment segment,
        IReadOnlyList<SegmentSubjectSnapshot> candidates,
        SegmentAttributeContext context,
        IReadOnlyCollection<Guid> manualExcludes,
        ICollection<SegmentMemberDto> members,
        ICollection<SegmentMemberDto> excluded)
    {
        foreach (var candidate in candidates)
        {
            var outcome = SegmentCriteriaEvaluator.Evaluate(
                segment, context.For(candidate.SubjectId, candidate.SubjectType));

            if (outcome.Matched == true)
            {
                if (manualExcludes.Contains(candidate.SubjectId))
                {
                    // hybrid: a manual exclusion takes a rule-matched subject OUT, definitively and visibly.
                    excluded.Add(new SegmentMemberDto(candidate.SubjectId, segment.SubjectType,
                        candidate.DisplayName,
                        SegmentMembershipVerdicts.NotMember, SegmentMembershipSources.ManualExclude,
                        new[] { SegmentReasonCodes.ManualExclude }));
                    continue;
                }

                members.Add(new SegmentMemberDto(candidate.SubjectId, segment.SubjectType,
                    candidate.DisplayName,
                    SegmentMembershipVerdicts.Member, SegmentMembershipSources.Criteria, outcome.ReasonCodes));
                continue;
            }

            // Eliminated candidates are reported with their reason. Nothing drops out silently.
            excluded.Add(new SegmentMemberDto(candidate.SubjectId, segment.SubjectType,
                candidate.DisplayName,
                outcome.Matched is null ? SegmentMembershipVerdicts.Unknown : SegmentMembershipVerdicts.NotMember,
                SegmentMembershipSources.Criteria,
                outcome.ReasonCodes.Count > 0
                    ? outcome.ReasonCodes
                    : new[] { SegmentReasonCodes.CriteriaNotMatched }));
        }
    }

    private static void AddManualIncludes(
        Segment segment,
        IReadOnlyList<SegmentSubjectSnapshot> candidates,
        IReadOnlyCollection<Guid> manualIncludes,
        IReadOnlyCollection<Guid> manualExcludes,
        IReadOnlyDictionary<Guid, string?> manualNames,
        ICollection<SegmentMemberDto> members,
        ICollection<SegmentMemberDto> excluded)
    {
        if (!string.Equals(segment.SegmentType, SegmentTypes.Hybrid, StringComparison.Ordinal))
        {
            return;
        }

        var known = candidates.Select(c => c.SubjectId)
            .Concat(members.Select(m => m.SubjectId))
            .Concat(excluded.Select(m => m.SubjectId))
            .ToHashSet();

        foreach (var subjectId in manualIncludes.Where(id => !known.Contains(id)))
        {
            if (manualExcludes.Contains(subjectId))
            {
                excluded.Add(new SegmentMemberDto(subjectId, segment.SubjectType,
                    manualNames.GetValueOrDefault(subjectId),
                    SegmentMembershipVerdicts.NotMember, SegmentMembershipSources.ManualExclude,
                    new[] { SegmentReasonCodes.ManualExclude }));
                continue;
            }

            members.Add(new SegmentMemberDto(subjectId, segment.SubjectType,
                manualNames.GetValueOrDefault(subjectId),
                SegmentMembershipVerdicts.Member, SegmentMembershipSources.ManualInclude,
                new[] { SegmentReasonCodes.ManualInclude }));
        }

        // A rule-matched subject that also carries a manual include keeps the rule provenance; a manual include that
        // the rule rejected is promoted OUT of the excluded list, because an explicit human decision outranks the rule.
        foreach (var subjectId in manualIncludes)
        {
            var rejected = excluded.FirstOrDefault(e => e.SubjectId == subjectId
                && e.MembershipSource == SegmentMembershipSources.Criteria);
            if (rejected is null || manualExcludes.Contains(subjectId))
            {
                continue;
            }

            excluded.Remove(rejected);
            members.Add(new SegmentMemberDto(subjectId, segment.SubjectType,
                rejected.SubjectDisplayName ?? manualNames.GetValueOrDefault(subjectId),
                SegmentMembershipVerdicts.Member, SegmentMembershipSources.ManualInclude,
                new[] { SegmentReasonCodes.ManualInclude }));
        }
    }

    /// <summary>Why the segment does not apply right now, or null when it does.</summary>
    private static string[]? ReasonSegmentNotInEffect(Segment segment, DateTimeOffset effectiveAt)
    {
        var reasons = new List<string>();
        if (!segment.IsActive())
        {
            reasons.Add(SegmentReasonCodes.SegmentNotActive);
        }

        if (!segment.IsEffectiveAt(effectiveAt))
        {
            reasons.Add(SegmentReasonCodes.OutsideEffectiveWindow);
        }

        return reasons.Count == 0 ? null : reasons.ToArray();
    }

    private static SegmentResolutionResultDto EmptyResult(
        Segment segment, DateTimeOffset effectiveAt, int limit, int offset, IReadOnlyList<string> reasons)
        => new(
            segment.Id,
            segment.SegmentCode,
            segment.SegmentVersion,
            segment.SegmentType,
            segment.SubjectType,
            segment.IsSuperseded(),
            effectiveAt,
            SegmentEffective: false,
            CandidateCount: 0,
            MatchedCount: 0,
            ExcludedCount: 0,
            TotalMemberCount: 0,
            Limit: limit,
            Offset: offset,
            MaxCandidateSet: SegmentLimits.MaxCandidateSet,
            Members: Array.Empty<SegmentMemberDto>(),
            Excluded: Array.Empty<SegmentMemberDto>(),
            ReasonCodes: reasons,
            ResolvedAt: DateTimeOffset.UtcNow,
            ResolverVersion: ResolverVersion);

    private static SegmentMembershipVerdictDto Verdict(
        Segment segment, string subjectType, Guid subjectId, string? displayName, DateTimeOffset effectiveAt,
        string verdict, string? source, params string[] reasons)
        => new(
            segment.Id,
            segment.SegmentCode,
            segment.SegmentVersion,
            subjectType,
            subjectId,
            displayName,
            verdict,
            source,
            segment.IsSuperseded(),
            effectiveAt,
            reasons.Length == 0 ? new[] { SegmentReasonCodes.CriteriaNotMatched } : reasons,
            ResolverVersion);
}
