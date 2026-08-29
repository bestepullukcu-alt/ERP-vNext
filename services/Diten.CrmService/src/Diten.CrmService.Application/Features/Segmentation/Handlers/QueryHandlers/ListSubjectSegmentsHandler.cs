using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Segmentation.Queries;
using Diten.CrmService.Application.Features.Segmentation.Resolution;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Segmentation.Handlers.QueryHandlers;

/// <summary>
/// The reverse question: which ACTIVE segments does this one subject belong to? Each segment is answered with the
/// single-subject path, so the cost is one subject times M segments — never the N times M product this FU refuses to
/// compute anywhere.
/// <para>Bounded by the published ceiling: past it the answer is a <b>422</b>, never a quietly shortened list. Only
/// segments whose SubjectType matches the question are considered, and a superseded version is still included — it may
/// well be the version that explains a past decision.</para>
/// </summary>
public sealed class ListSubjectSegmentsHandler
    : IRequestHandler<ListSubjectSegmentsQuery, Response<SubjectSegmentListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ISegmentRepository _segments;
    private readonly SegmentMembershipResolver _resolver;

    public ListSubjectSegmentsHandler(
        ITenantContext tenant, ISegmentRepository segments, SegmentMembershipResolver resolver)
    {
        _tenant = tenant;
        _segments = segments;
        _resolver = resolver;
    }

    public async Task<Response<SubjectSegmentListDto>> Handle(
        ListSubjectSegmentsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<SubjectSegmentListDto>.Fail("Tenant context is required.", 400);
        }

        if (!SegmentSubjectTypes.IsValid(request.SubjectType))
        {
            return Response<SubjectSegmentListDto>.Fail(
                $"SubjectType must be one of: {string.Join(", ", SegmentSubjectTypes.All)}.", 400);
        }

        if (request.SubjectId == Guid.Empty)
        {
            return Response<SubjectSegmentListDto>.Fail("SubjectId is required.", 400);
        }

        var subjectType = SegmentSubjectTypes.Normalize(request.SubjectType);
        var effectiveAt = request.EffectiveAt ?? DateTimeOffset.UtcNow;

        var candidates = (await _segments.ListAsync(tenantId, cancellationToken))
            .Where(s => s.IsActive()
                        && string.Equals(s.SubjectType, subjectType, StringComparison.Ordinal)
                        && s.IsEffectiveAt(effectiveAt))
            .OrderBy(s => s.SegmentCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.SegmentVersion)
            .ToList();

        if (candidates.Count > SegmentLimits.MaxSegmentsPerSubject)
        {
            return Response<SubjectSegmentListDto>.Fail(
                new[]
                {
                    SegmentErrorCodes.SubjectSegmentsTooMany,
                    $"This tenant has more than {SegmentLimits.MaxSegmentsPerSubject} active segments for "
                    + $"'{subjectType}'. No truncated list is returned."
                },
                422);
        }

        var items = new List<SubjectSegmentDto>(candidates.Count);
        foreach (var segment in candidates)
        {
            var verdict = await _resolver.EvaluateAsync(
                tenantId, segment, subjectType, request.SubjectId, effectiveAt, cancellationToken);

            if (string.Equals(verdict.Verdict, SegmentMembershipVerdicts.Member, StringComparison.Ordinal))
            {
                items.Add(new SubjectSegmentDto(
                    segment.Id, segment.SegmentCode, segment.SegmentName, segment.SegmentVersion,
                    segment.SegmentType, verdict.Verdict, verdict.MembershipSource, verdict.ReasonCodes));
            }
        }

        return Response<SubjectSegmentListDto>.Success(new SubjectSegmentListDto(
            subjectType, request.SubjectId, effectiveAt, candidates.Count,
            SegmentLimits.MaxSegmentsPerSubject, items));
    }
}
