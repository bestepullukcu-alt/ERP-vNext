using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Segmentation.Queries;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Segmentation.Handlers.QueryHandlers;

/// <summary>Segment grid. Filtering and ordering happen in memory over the tenant rows, so no DateTimeOffset field ever
/// becomes a Mongo sort key (they are stored as BSON arrays and sorting two of them together is the parallel-array
/// trap). Ordering is (code, business version), which is stable and reads the way an author thinks.</summary>
public sealed class ListSegmentsHandler : IRequestHandler<ListSegmentsQuery, Response<SegmentListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ISegmentRepository _segments;

    public ListSegmentsHandler(ITenantContext tenant, ISegmentRepository segments)
    {
        _tenant = tenant;
        _segments = segments;
    }

    public async Task<Response<SegmentListDto>> Handle(
        ListSegmentsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<SegmentListDto>.Fail("Tenant context is required.", 400);
        }

        var rows = await _segments.ListAsync(tenantId, cancellationToken);

        var filtered = rows.Where(s => request.IncludeArchived || !s.IsArchived());

        if (!string.IsNullOrWhiteSpace(request.SegmentType))
        {
            var type = SegmentTypes.Normalize(request.SegmentType);
            filtered = filtered.Where(s => string.Equals(s.SegmentType, type, StringComparison.Ordinal));
        }

        if (!string.IsNullOrWhiteSpace(request.SegmentStatus))
        {
            var status = SegmentStatuses.Normalize(request.SegmentStatus);
            filtered = filtered.Where(s => string.Equals(s.SegmentStatus, status, StringComparison.Ordinal));
        }

        if (!string.IsNullOrWhiteSpace(request.SubjectType))
        {
            var subject = SegmentSubjectTypes.Normalize(request.SubjectType);
            filtered = filtered.Where(s => string.Equals(s.SubjectType, subject, StringComparison.Ordinal));
        }

        if (!string.IsNullOrWhiteSpace(request.BusinessUnitId))
        {
            filtered = filtered.Where(s => string.Equals(
                s.BusinessUnitId, request.BusinessUnitId.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.SegmentCode))
        {
            filtered = filtered.Where(s => string.Equals(
                s.SegmentCode, request.SegmentCode.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            filtered = filtered.Where(s =>
                s.SegmentCode.Contains(search, StringComparison.OrdinalIgnoreCase)
                || s.SegmentName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (s.Description ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var items = filtered
            .OrderBy(s => s.SegmentCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.SegmentVersion)
            .Select(SegmentMapper.ToListItem)
            .ToList();

        return Response<SegmentListDto>.Success(new SegmentListDto(items, items.Count));
    }
}
