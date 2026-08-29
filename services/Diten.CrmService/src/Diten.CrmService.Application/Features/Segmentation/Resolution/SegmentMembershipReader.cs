using Diten.CrmService.Application.Common;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.Segmentation.Resolution;

/// <summary>
/// The in-process implementation of <see cref="ISegmentMembershipReader"/>. It is a thin, read-only adapter over
/// <see cref="SegmentMembershipResolver"/>: no second membership engine exists, so an in-process consumer and the HTTP
/// endpoint can never disagree about who is a member.
/// <para>Every failure mode degrades to <c>unknown</c> rather than to an exception or to <c>member</c>: a missing
/// tenant context, a segment that does not exist in this tenant, a candidate set that is too wide. Unknown is never
/// allowed to become membership.</para>
/// </summary>
public sealed class SegmentMembershipReader : ISegmentMembershipReader
{
    private readonly ITenantContext _tenant;
    private readonly ISegmentRepository _segments;
    private readonly SegmentMembershipResolver _resolver;

    public SegmentMembershipReader(
        ITenantContext tenant, ISegmentRepository segments, SegmentMembershipResolver resolver)
    {
        _tenant = tenant;
        _segments = segments;
        _resolver = resolver;
    }

    public async Task<SegmentMembershipVerdict> IsMemberAsync(
        Guid segmentId,
        string subjectType,
        Guid subjectId,
        DateTimeOffset effectiveAt,
        CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Unknown(segmentId, 0, subjectType, subjectId, effectiveAt,
                SegmentReasonCodes.DependencyUnavailable);
        }

        var segment = await _segments.GetByIdAsync(tenantId, segmentId, cancellationToken);
        if (segment is null)
        {
            return Unknown(segmentId, 0, subjectType, subjectId, effectiveAt,
                SegmentReasonCodes.SegmentNotActive);
        }

        if (!string.Equals(
                SegmentSubjectTypes.Normalize(subjectType), segment.SubjectType, StringComparison.Ordinal))
        {
            return Unknown(segmentId, segment.SegmentVersion, subjectType, subjectId, effectiveAt,
                SegmentReasonCodes.SubjectTypeMismatch);
        }

        var verdict = await _resolver.EvaluateAsync(
            tenantId, segment, segment.SubjectType, subjectId, effectiveAt, cancellationToken);

        return new SegmentMembershipVerdict(
            segment.Id, segment.SegmentVersion, segment.SubjectType, subjectId, verdict.Verdict,
            verdict.ReasonCodes, effectiveAt);
    }

    public async Task<SegmentResolutionResult> ResolveAsync(
        Guid segmentId,
        DateTimeOffset effectiveAt,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return new SegmentResolutionResult(
                segmentId, 0, string.Empty, false, effectiveAt, 0, 0, Array.Empty<SegmentMemberDto>());
        }

        var segment = await _segments.GetByIdAsync(tenantId, segmentId, cancellationToken);
        if (segment is null)
        {
            return new SegmentResolutionResult(
                segmentId, 0, string.Empty, false, effectiveAt, 0, 0, Array.Empty<SegmentMemberDto>());
        }

        var outcome = await _resolver.ResolveAsync(
            tenantId, segment, effectiveAt, limit, offset, includeExcluded: false, cancellationToken);

        // Over the ceiling the consumer gets NOTHING, exactly like the HTTP caller gets a 422: a partial member list
        // would be indistinguishable from a complete one.
        if (outcome.CandidateCapExceeded || outcome.Result is null)
        {
            return new SegmentResolutionResult(
                segment.Id, segment.SegmentVersion, segment.SubjectType, segment.IsSuperseded(), effectiveAt,
                0, 0, Array.Empty<SegmentMemberDto>());
        }

        return new SegmentResolutionResult(
            segment.Id,
            segment.SegmentVersion,
            segment.SubjectType,
            segment.IsSuperseded(),
            effectiveAt,
            outcome.Result.CandidateCount,
            outcome.Result.TotalMemberCount,
            outcome.Result.Members);
    }

    private static SegmentMembershipVerdict Unknown(
        Guid segmentId, int segmentVersion, string subjectType, Guid subjectId, DateTimeOffset effectiveAt,
        string reason)
        => new(segmentId, segmentVersion, SegmentSubjectTypes.Normalize(subjectType), subjectId,
            SegmentMembershipVerdicts.Unknown, new[] { reason }, effectiveAt);
}
