using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Segmentation.Queries;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Segmentation.Handlers.QueryHandlers;

/// <summary>Segment detail with its embedded criteria tree. A segment from another tenant is a 404 — the repository
/// filter is tenant-scoped, so existence itself cannot leak across a tenant boundary.</summary>
public sealed class GetSegmentByIdHandler : IRequestHandler<GetSegmentByIdQuery, Response<SegmentDetailDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ISegmentRepository _segments;

    public GetSegmentByIdHandler(ITenantContext tenant, ISegmentRepository segments)
    {
        _tenant = tenant;
        _segments = segments;
    }

    public async Task<Response<SegmentDetailDto>> Handle(
        GetSegmentByIdQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<SegmentDetailDto>.Fail("Tenant context is required.", 400);
        }

        var segment = await _segments.GetByIdAsync(tenantId, request.SegmentId, cancellationToken);
        return segment is null
            ? Response<SegmentDetailDto>.Fail("Segment not found.", 404)
            : Response<SegmentDetailDto>.Success(SegmentMapper.ToDetail(segment));
    }
}
