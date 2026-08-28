using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.Campaign.Read;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// MOD-0165 FU10 — the one implementation of <see cref="ICampaignSegmentCatalog"/>.
///
/// <para>It composes the tenant context with the segment repository's READ methods and adds nothing of its own. It
/// deliberately exposes no way to reach the repository's write methods: the campaign side receives this narrow
/// interface, never <c>ISegmentRepository</c> itself.</para>
///
/// <para>Both methods narrow the tenant's segments in memory rather than introducing a new query shape, for the same
/// reason the cycle-period batch read does: a tenant's segment catalogue is a governed list of tens of rows, and a
/// new index on another module's collection is a change to that module.</para>
/// </summary>
public sealed class CampaignSegmentCatalog : ICampaignSegmentCatalog
{
    private readonly ITenantContext _tenant;
    private readonly ISegmentRepository _segments;

    public CampaignSegmentCatalog(ITenantContext tenant, ISegmentRepository segments)
    {
        _tenant = tenant;
        _segments = segments;
    }

    public async Task<IReadOnlyList<CampaignSegmentRef>> GetByIdsAsync(
        IReadOnlyCollection<Guid> segmentIds, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId || segmentIds is null || segmentIds.Count == 0)
        {
            return Array.Empty<CampaignSegmentRef>();
        }

        var wanted = segmentIds.Where(id => id != Guid.Empty).ToHashSet();
        if (wanted.Count == 0)
        {
            return Array.Empty<CampaignSegmentRef>();
        }

        var rows = await _segments.ListAsync(tenantId, cancellationToken);
        return rows.Where(s => wanted.Contains(s.Id)).Select(Map).ToList();
    }

    public async Task<IReadOnlyList<CampaignSegmentRef>> ListSelectableAsync(CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Array.Empty<CampaignSegmentRef>();
        }

        var rows = await _segments.ListAsync(tenantId, cancellationToken);
        return rows
            .Where(s => string.Equals(s.SegmentStatus, SegmentStatuses.Active, StringComparison.Ordinal))
            .Select(Map)
            .OrderBy(s => s.SegmentName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Only what a campaign may know — see <see cref="CampaignSegmentRef"/>.</summary>
    private static CampaignSegmentRef Map(Segment segment) => new(
        segment.Id,
        segment.SegmentCode,
        segment.SegmentName,
        segment.SubjectType,
        segment.SegmentStatus,
        segment.SupersededBySegmentId is not null,
        segment.VersionLineageId,
        segment.SegmentVersion);
}
