using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Segmentation.Queries;
using Diten.CrmService.Application.Features.Segmentation.Resolution;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Segmentation.Handlers.QueryHandlers;

/// <summary>
/// The single-subject question (MOD-0167-FU01 section 5). One document read plus, at most, one derived read — never the
/// candidate scan, which is why this is the 300 ms path and resolve is the 5 second one.
/// <para>Answers member / not-member / unknown with reason codes. <c>unknown</c> is an answer, not an error, and it is
/// never <c>member</c>: a subject that is invisible in this tenant, or a segment that is not in effect, produces
/// unknown rather than a fabricated default.</para>
/// </summary>
public sealed class EvaluateSegmentMembershipHandler
    : IRequestHandler<EvaluateSegmentMembershipQuery, Response<SegmentMembershipVerdictDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ISegmentRepository _segments;
    private readonly SegmentMembershipResolver _resolver;

    public EvaluateSegmentMembershipHandler(
        ITenantContext tenant, ISegmentRepository segments, SegmentMembershipResolver resolver)
    {
        _tenant = tenant;
        _segments = segments;
        _resolver = resolver;
    }

    public async Task<Response<SegmentMembershipVerdictDto>> Handle(
        EvaluateSegmentMembershipQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<SegmentMembershipVerdictDto>.Fail("Tenant context is required.", 400);
        }

        if (!SegmentSubjectTypes.IsValid(request.SubjectType))
        {
            return Response<SegmentMembershipVerdictDto>.Fail(
                $"SubjectType must be one of: {string.Join(", ", SegmentSubjectTypes.All)}.", 400);
        }

        if (request.SubjectId == Guid.Empty)
        {
            return Response<SegmentMembershipVerdictDto>.Fail("SubjectId is required.", 400);
        }

        var segment = await _segments.GetByIdAsync(tenantId, request.SegmentId, cancellationToken);
        if (segment is null)
        {
            return Response<SegmentMembershipVerdictDto>.Fail("Segment not found.", 404);
        }

        var subjectType = SegmentSubjectTypes.Normalize(request.SubjectType);
        if (!string.Equals(subjectType, segment.SubjectType, StringComparison.Ordinal))
        {
            // Asking an account question of a contact segment is a caller mistake, not an uncertain answer.
            return Response<SegmentMembershipVerdictDto>.Fail(
                new[]
                {
                    SegmentErrorCodes.SubjectTypeMismatch,
                    $"This segment groups '{segment.SubjectType}' subjects."
                },
                400);
        }

        var effectiveAt = request.EffectiveAt ?? DateTimeOffset.UtcNow;
        var verdict = await _resolver.EvaluateAsync(
            tenantId, segment, subjectType, request.SubjectId, effectiveAt, cancellationToken);

        return Response<SegmentMembershipVerdictDto>.Success(verdict);
    }
}
