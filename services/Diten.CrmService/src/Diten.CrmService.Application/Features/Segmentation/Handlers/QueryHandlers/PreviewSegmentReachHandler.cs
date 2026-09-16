using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Segmentation.Handlers.CommandHandlers;
using Diten.CrmService.Application.Features.Segmentation.Queries;
using Diten.CrmService.Application.Features.Segmentation.Resolution;
using Diten.CrmService.Domain.Entities;
using MediatR;

namespace Diten.CrmService.Application.Features.Segmentation.Handlers.QueryHandlers;

/// <summary>
/// "If I saved this rule now, who would it reach?" — the live reach preview for a DRAFT (unsaved) segment rule.
/// <para>It reuses <see cref="SegmentMembershipResolver"/> unchanged, wrapping the DRAFT criteria in a transient, never
/// persisted <see cref="Segment"/>: the resolver cannot tell it apart from a saved dynamic segment, so the number it
/// returns is byte-for-byte the number the same rule would resolve to once saved and activated. Nothing is written.</para>
/// <para>The draft rule is validated with exactly the write-path structural rules (catalog conformance, operator arity,
/// tree/limit ceilings) — an unknown attribute or an empty rule is a 400 before any resolve runs. The cross-service
/// value PROOF (class X, the 503 path) is deliberately NOT run here: it is a persistence guard, and a preview persists
/// nothing; an unprovable reference simply eliminates its candidates with a reason, the same as any other unresolved
/// attribute.</para>
/// <para><b>Bounded by construction.</b> One resolve produces the total and the sample; then one resolve per predicate
/// produces the funnel — a count is read off <see cref="SegmentResolutionResultDto.MatchedCount"/> and the members are
/// never returned (limit 0), so a condition costs a count and nothing more. Predicate fan-out is capped by the same
/// <see cref="SegmentLimits.MaxChildrenPerGroup"/>/<see cref="SegmentLimits.MaxCriteriaNodes"/> ceilings the validator
/// already enforces.</para>
/// </summary>
public sealed class PreviewSegmentReachHandler
    : IRequestHandler<PreviewSegmentReachQuery, Response<SegmentReachPreviewDto>>
{
    /// <summary>How many sample members the reach rail shows. The full, paged list is a saved segment's /resolve.</summary>
    private const int SampleLimit = 50;

    private readonly ITenantContext _tenant;
    private readonly SegmentMembershipResolver _resolver;

    public PreviewSegmentReachHandler(ITenantContext tenant, SegmentMembershipResolver resolver)
    {
        _tenant = tenant;
        _resolver = resolver;
    }

    public async Task<Response<SegmentReachPreviewDto>> Handle(
        PreviewSegmentReachQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<SegmentReachPreviewDto>.Fail("Tenant context is required.", 400);
        }

        var subjectFailure = SegmentValidation.ValidateSubjectType(request.SubjectType);
        if (subjectFailure is not null)
        {
            return Response<SegmentReachPreviewDto>.Fail(
                SegmentWriteGuards.ToErrors(subjectFailure), subjectFailure.StatusCode);
        }

        var matchFailure = SegmentValidation.ValidateMatchMode(request.MatchMode);
        if (matchFailure is not null)
        {
            return Response<SegmentReachPreviewDto>.Fail(
                SegmentWriteGuards.ToErrors(matchFailure), matchFailure.StatusCode);
        }

        var subjectType = SegmentSubjectTypes.Normalize(request.SubjectType);
        var matchMode = SegmentMatchModes.Normalize(request.MatchMode);

        // Same materialisation the write path uses (normalises operators/values, assigns node ids). A preview is a
        // DYNAMIC rule by definition — a static segment has no criteria to preview and its reach is just its manual list.
        var criteria = SegmentMapper.ToCriteria(request.Criteria);

        // The full write-path structural validation, minus the cross-service proof (a persistence guard): an unknown
        // attribute, a bad operator arity or a ceiling breach is a 400 here, before any resolve runs.
        var criteriaFailure = SegmentValidation.ValidateCriteria(SegmentTypes.Dynamic, subjectType, criteria);
        if (criteriaFailure is not null)
        {
            return Response<SegmentReachPreviewDto>.Fail(
                SegmentWriteGuards.ToErrors(criteriaFailure), criteriaFailure.StatusCode);
        }

        var effectiveAt = request.EffectiveAt ?? DateTimeOffset.UtcNow;

        // One resolve for the total and the sample. The resolver's MatchedCount is the whole reach (computed before
        // paging), so a single call with a small page yields both.
        var fullOutcome = await _resolver.ResolveAsync(
            tenantId, DraftSegment(tenantId, subjectType, matchMode, criteria),
            effectiveAt, SampleLimit, 0, includeExcluded: false, cancellationToken);

        if (fullOutcome.CandidateCapExceeded || fullOutcome.Result is null)
        {
            // A rule too wide to count is narrowed, never partially answered — identical to /resolve.
            return Response<SegmentReachPreviewDto>.Fail(
                new[]
                {
                    SegmentErrorCodes.CandidateSetTooLarge,
                    $"The rule matches more than {SegmentLimits.MaxCandidateSet} candidates. Narrow the criteria: "
                    + "no partial reach is returned, because it would be indistinguishable from a complete one."
                },
                422);
        }

        var full = fullOutcome.Result;

        // Each predicate counted on its own — the funnel a matchMode=all rule narrows down from. Groups are not counted;
        // only leaf predicates carry a "reach on its own" meaning, and the editor rail shows one row per predicate.
        var conditionCounts = new List<SegmentReachConditionDto>();
        var inputNodes = request.Criteria ?? Array.Empty<SegmentCriteriaNodeInput>();
        for (var i = 0; i < criteria.Count; i++)
        {
            var node = criteria[i];
            if (!node.IsPredicate())
            {
                continue;
            }

            // Echo the caller's own node id when it sent one, so the rail lines the count up with its editor row; fall
            // back to the runtime-assigned id otherwise (order is preserved 1:1 by the mapper).
            var reportedNodeId = inputNodes.Count == criteria.Count && inputNodes[i].NodeId is { } incoming
                ? incoming
                : node.NodeId;

            var singleOutcome = await _resolver.ResolveAsync(
                tenantId, DraftSegment(tenantId, subjectType, matchMode, new List<SegmentCriteriaNode> { Solo(node) }),
                effectiveAt, 0, 0, includeExcluded: false, cancellationToken);

            var capped = singleOutcome.CandidateCapExceeded || singleOutcome.Result is null;
            conditionCounts.Add(new SegmentReachConditionDto(
                reportedNodeId,
                node.AttributeCode,
                node.Label,
                capped ? SegmentLimits.MaxCandidateSet : singleOutcome.Result!.MatchedCount,
                capped));
        }

        var sampleMembers = full.Members
            .Select(m => new SegmentReachSampleMemberDto(
                m.SubjectId, m.SubjectType, m.SubjectDisplayName, m.SubjectSecondaryLabel))
            .ToList();

        return Response<SegmentReachPreviewDto>.Success(new SegmentReachPreviewDto(
            subjectType,
            matchMode,
            effectiveAt,
            TotalCount: full.MatchedCount,
            SampleLimit,
            SegmentLimits.MaxCandidateSet,
            conditionCounts,
            sampleMembers,
            full.ResolvedAt,
            full.ResolverVersion));
    }

    /// <summary>A transient, NEVER persisted segment the resolver treats exactly like a saved dynamic one: active and
    /// unconditionally in effect, so the only thing that shapes the answer is the rule itself.</summary>
    private static Segment DraftSegment(
        Guid tenantId, string subjectType, string matchMode, List<SegmentCriteriaNode> criteria)
        => new()
        {
            Id = Guid.Empty,
            TenantId = tenantId,
            SegmentCode = "draft-preview",
            SegmentName = "draft-preview",
            SegmentType = SegmentTypes.Dynamic,
            SubjectType = subjectType,
            SegmentStatus = SegmentStatuses.Active,
            SegmentVersion = 0,
            MatchMode = matchMode,
            EffectiveFrom = DateTimeOffset.MinValue,
            EffectiveTo = null,
            Criteria = criteria
        };

    /// <summary>Lifts one predicate out of the tree into a standalone root predicate, so it can be counted alone. Parent
    /// links and sort position are dropped; the question the predicate asks is copied verbatim.</summary>
    private static SegmentCriteriaNode Solo(SegmentCriteriaNode node)
        => new()
        {
            NodeId = node.NodeId,
            ParentNodeId = null,
            NodeKind = SegmentCriteriaNodeKinds.Predicate,
            AttributeCode = node.AttributeCode,
            Operator = node.Operator,
            Values = new List<string>(node.Values),
            ValueType = node.ValueType,
            Parameters = new Dictionary<string, string>(node.Parameters),
            Negate = node.Negate,
            SortOrder = 0
        };
}
