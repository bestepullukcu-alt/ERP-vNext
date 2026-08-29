using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Segmentation.Queries;
using Diten.CrmService.Application.Features.Segmentation.Resolution;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Segmentation.Handlers.QueryHandlers;

/// <summary>
/// "Who is in this segment right now?" A pure read that persists nothing.
/// <para>Two answers are deliberately NOT errors: a segment that is not in effect right now returns 200 with an empty,
/// reason-coded result (it exists, it just does not apply — a 404 would be a lie), and an eliminated candidate is
/// returned WITH its reason when the caller asks, so nothing ever disappears silently.</para>
/// <para>One answer deliberately IS an error: past the candidate ceiling this returns <b>422</b> and no members at all,
/// because handing back a partial list that looks complete is the more dangerous failure.</para>
/// </summary>
public sealed class ResolveSegmentMembershipHandler
    : IRequestHandler<ResolveSegmentMembershipQuery, Response<SegmentResolutionResultDto>>
{
    private const int DefaultLimit = 200;
    private const int MaxLimit = 1000;

    private readonly ITenantContext _tenant;
    private readonly ISegmentRepository _segments;
    private readonly SegmentMembershipResolver _resolver;

    public ResolveSegmentMembershipHandler(
        ITenantContext tenant, ISegmentRepository segments, SegmentMembershipResolver resolver)
    {
        _tenant = tenant;
        _segments = segments;
        _resolver = resolver;
    }

    public async Task<Response<SegmentResolutionResultDto>> Handle(
        ResolveSegmentMembershipQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<SegmentResolutionResultDto>.Fail("Tenant context is required.", 400);
        }

        var segment = await _segments.GetByIdAsync(tenantId, request.SegmentId, cancellationToken);
        if (segment is null)
        {
            return Response<SegmentResolutionResultDto>.Fail("Segment not found.", 404);
        }

        var effectiveAt = request.EffectiveAt ?? DateTimeOffset.UtcNow;
        var limit = Math.Clamp(request.Limit ?? DefaultLimit, 1, MaxLimit);
        var offset = Math.Max(0, request.Offset ?? 0);

        var outcome = await _resolver.ResolveAsync(
            tenantId, segment, effectiveAt, limit, offset, request.IncludeExcluded, cancellationToken);

        if (outcome.CandidateCapExceeded || outcome.Result is null)
        {
            return Response<SegmentResolutionResultDto>.Fail(
                new[]
                {
                    SegmentErrorCodes.CandidateSetTooLarge,
                    $"The rule matches more than {SegmentLimits.MaxCandidateSet} candidates. Narrow the criteria: "
                    + "no partial member list is returned, because it would be indistinguishable from a complete one."
                },
                422);
        }

        return Response<SegmentResolutionResultDto>.Success(outcome.Result);
    }
}
