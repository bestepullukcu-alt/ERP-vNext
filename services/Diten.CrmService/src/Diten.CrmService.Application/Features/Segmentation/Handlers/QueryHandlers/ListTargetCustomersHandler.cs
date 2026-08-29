using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Segmentation.Queries;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Segmentation.Handlers.QueryHandlers;

/// <summary>The hand-written membership rows of one segment. Ordered by (mode, subject id) so the list is stable across
/// reads; never by a DateTimeOffset field, which is a BSON array.</summary>
public sealed class ListTargetCustomersHandler
    : IRequestHandler<ListTargetCustomersQuery, Response<TargetCustomerListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ISegmentRepository _segments;
    private readonly ITargetCustomerRepository _targets;

    public ListTargetCustomersHandler(
        ITenantContext tenant, ISegmentRepository segments, ITargetCustomerRepository targets)
    {
        _tenant = tenant;
        _segments = segments;
        _targets = targets;
    }

    public async Task<Response<TargetCustomerListDto>> Handle(
        ListTargetCustomersQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<TargetCustomerListDto>.Fail("Tenant context is required.", 400);
        }

        var segment = await _segments.GetByIdAsync(tenantId, request.SegmentId, cancellationToken);
        if (segment is null)
        {
            return Response<TargetCustomerListDto>.Fail("Segment not found.", 404);
        }

        var rows = await _targets.ListBySegmentAsync(tenantId, request.SegmentId, cancellationToken);
        var filtered = rows.Where(t => request.IncludeArchived || !t.IsArchived());

        if (!string.IsNullOrWhiteSpace(request.MembershipMode))
        {
            var mode = SegmentMembershipModes.Normalize(request.MembershipMode);
            filtered = filtered.Where(t => string.Equals(t.MembershipMode, mode, StringComparison.Ordinal));
        }

        var items = filtered
            .OrderBy(t => t.MembershipMode, StringComparer.Ordinal)
            .ThenBy(t => t.SubjectId)
            .Select(SegmentMapper.ToTargetCustomer)
            .ToList();

        return Response<TargetCustomerListDto>.Success(new TargetCustomerListDto(items, items.Count));
    }
}
