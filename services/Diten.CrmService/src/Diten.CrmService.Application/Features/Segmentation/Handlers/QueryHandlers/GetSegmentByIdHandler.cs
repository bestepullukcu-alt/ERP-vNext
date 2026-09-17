using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Segmentation.Queries;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Segmentation.Handlers.QueryHandlers;

/// <summary>Segment detail with its embedded criteria tree. A segment from another tenant is a 404 — the repository
/// filter is tenant-scoped, so existence itself cannot leak across a tenant boundary.
/// <para>WP-SEG-DETAILS6: the CreatedBy/ActivatedBy/UpdatedBy provenance ids (the actor's <c>sub</c>) are resolved to
/// display names in ONE bulk AuthService call so the lifecycle timeline reads "… · S. Aydın" instead of a raw GUID. The
/// resolve is fail-closed — an unavailable AuthService or an unresolvable id leaves the corresponding <c>*ByName</c>
/// null, and the raw <c>*By</c> ids are never widened or replaced.</para></summary>
public sealed class GetSegmentByIdHandler : IRequestHandler<GetSegmentByIdQuery, Response<SegmentDetailDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ISegmentRepository _segments;
    private readonly IUserDisplayNameResolver _userDisplayNames;

    public GetSegmentByIdHandler(
        ITenantContext tenant,
        ISegmentRepository segments,
        IUserDisplayNameResolver userDisplayNames)
    {
        _tenant = tenant;
        _segments = segments;
        _userDisplayNames = userDisplayNames;
    }

    public async Task<Response<SegmentDetailDto>> Handle(
        GetSegmentByIdQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<SegmentDetailDto>.Fail("Tenant context is required.", 400);
        }

        var segment = await _segments.GetByIdAsync(tenantId, request.SegmentId, cancellationToken);
        if (segment is null)
        {
            return Response<SegmentDetailDto>.Fail("Segment not found.", 404);
        }

        var detail = SegmentMapper.ToDetail(segment);
        detail = await EnrichWithDisplayNamesAsync(detail, cancellationToken);
        return Response<SegmentDetailDto>.Success(detail);
    }

    /// <summary>Resolves the three provenance ids to display names in a SINGLE bulk call and returns an enriched copy.
    /// Ids that are absent, non-GUID or unresolved simply leave their <c>*ByName</c> null (fail-closed).</summary>
    private async Task<SegmentDetailDto> EnrichWithDisplayNamesAsync(
        SegmentDetailDto detail, CancellationToken cancellationToken)
    {
        // Distinct, non-empty, GUID-shaped provenance ids only — the ids the resolver can actually answer for.
        var ids = new[] { detail.CreatedBy, detail.ActivatedBy, detail.UpdatedBy }
            .Where(value => Guid.TryParse(value, out var id) && id != Guid.Empty)
            .Select(value => Guid.Parse(value!))
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return detail;
        }

        // ONE bulk round trip for every id; the resolver is fail-closed and returns an empty map on any failure.
        var names = await _userDisplayNames.ResolveAsync(ids, cancellationToken);

        return detail with
        {
            CreatedByName = ResolveName(detail.CreatedBy, names),
            ActivatedByName = ResolveName(detail.ActivatedBy, names),
            UpdatedByName = ResolveName(detail.UpdatedBy, names)
        };
    }

    private static string? ResolveName(string? actorId, IReadOnlyDictionary<Guid, string> names)
        => Guid.TryParse(actorId, out var id) && names.TryGetValue(id, out var name)
            && !string.IsNullOrWhiteSpace(name)
                ? name
                : null;
}
