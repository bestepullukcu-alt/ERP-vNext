using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Segmentation.Catalog;
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
    private readonly ITerritoryNodeRepository _territoryNodes;

    public GetSegmentByIdHandler(
        ITenantContext tenant,
        ISegmentRepository segments,
        IUserDisplayNameResolver userDisplayNames,
        ITerritoryNodeRepository territoryNodes)
    {
        _tenant = tenant;
        _segments = segments;
        _userDisplayNames = userDisplayNames;
        _territoryNodes = territoryNodes;
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
        detail = await EnrichTerritoryNodeLabelsAsync(detail, tenantId, cancellationToken);
        return Response<SegmentDetailDto>.Success(detail);
    }

    /// <summary>WP-SEG-DETAILS8: resolves the raw node ids stored on <c>territory.node</c> criteria to node display names
    /// in ONE bulk read, and hangs them off each node's ADDITIVE <see cref="SegmentCriteriaNodeDto.ValueLabels"/> map so a
    /// reader can show "Territory is any of Marmara" instead of a GUID. The stored <c>Values</c> are never touched, and
    /// reference-set / enum values (already human-readable) are left alone. Fail-closed: any failure or an unresolved id
    /// simply leaves the label absent, so the reader falls back to hiding the raw id.</summary>
    private async Task<SegmentDetailDto> EnrichTerritoryNodeLabelsAsync(
        SegmentDetailDto detail, Guid tenantId, CancellationToken cancellationToken)
    {
        // Distinct, GUID-shaped values on territory-node criteria only — the ids the node lookup can answer for.
        var nodeIds = detail.Criteria
            .Where(node => IsTerritoryNode(node.AttributeCode))
            .SelectMany(node => node.Values)
            .Where(value => Guid.TryParse(value, out var id) && id != Guid.Empty)
            .Select(value => Guid.Parse(value))
            .Distinct()
            .ToList();

        if (nodeIds.Count == 0)
        {
            return detail;
        }

        IReadOnlyDictionary<Guid, string> names;
        try
        {
            // ONE bulk read across models; fail-closed on any error (the lookup is a nicety, never load-bearing).
            var nodes = await _territoryNodes.ListByIdsAsync(tenantId, nodeIds, cancellationToken);
            names = nodes
                .Where(node => !string.IsNullOrWhiteSpace(node.Name))
                .GroupBy(node => node.Id)
                .ToDictionary(group => group.Key, group => group.First().Name);
        }
        catch
        {
            return detail;
        }

        if (names.Count == 0)
        {
            return detail;
        }

        var enriched = detail.Criteria.Select(node =>
        {
            if (!IsTerritoryNode(node.AttributeCode))
            {
                return node;
            }

            var labels = node.Values
                .Where(value => Guid.TryParse(value, out var id) && names.ContainsKey(id))
                .GroupBy(value => value)
                .ToDictionary(group => group.Key, group => names[Guid.Parse(group.Key)]);

            return labels.Count == 0
                ? node
                : node with { ValueLabels = labels };
        }).ToList();

        return detail with { Criteria = enriched };
    }

    private static bool IsTerritoryNode(string? attributeCode)
        => string.Equals(attributeCode, SegmentAttributeCatalog.TerritoryNode, StringComparison.OrdinalIgnoreCase);

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
