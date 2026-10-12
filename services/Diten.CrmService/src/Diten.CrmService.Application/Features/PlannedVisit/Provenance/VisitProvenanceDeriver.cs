using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.Segmentation.Resolution;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.PlannedVisit.Provenance;

/// <summary>
/// WP-VP-2 (B-3, K-3 / K-4) — the play / campaign / segment of a visit are DERIVED on the server from the doctor; the
/// client never chooses them (a rep never sees or selects a play or a campaign). Used by the planner and by the manual
/// planned-visit create / update. Reads only; persists nothing.
/// <para><b>Segments</b> — the doctor's active memberships (<see cref="ContactSegmentMemberships"/>, the same reader the
/// frequency resolution uses).</para>
/// <para><b>Play</b> — the ACTIVE strategy templates bound to those segments at the instant. More than one ⇒ the first
/// by template code (ordinal), then version (reason <see cref="MultiplePlays"/>); none ⇒ null
/// (reason <see cref="NoStrategy"/>).</para>
/// <para><b>Campaign</b> — an ACTIVE, non-archived campaign effective on the visit date whose target snapshot holds the
/// doctor (or, when no doctor / no doctor target, the account) as a live, effective member. More than one ⇒ the
/// earliest start date, then campaign code; none ⇒ null. Provenance only.</para>
/// </summary>
public sealed class VisitProvenanceDeriver : IVisitProvenanceDeriver
{
    public const string MultiplePlays = "multiple-plays";
    public const string NoStrategy = "no-strategy";

    private readonly ITenantContext _tenant;
    private readonly ISegmentRepository _segments;
    private readonly ISegmentMembershipReader _membership;
    private readonly IStrategyTemplateReader _strategies;
    private readonly ICampaignRepository _campaigns;
    private readonly ICampaignTargetRepository _campaignTargets;

    // Per-scope caches: one planning run asks for many doctors / visit dates of the same tenant.
    private readonly Dictionary<Guid, DerivedPlay> _plays = new();
    private IReadOnlyList<Domain.Entities.Campaign>? _activeCampaigns;

    public VisitProvenanceDeriver(
        ITenantContext tenant,
        ISegmentRepository segments,
        ISegmentMembershipReader membership,
        IStrategyTemplateReader strategies,
        ICampaignRepository campaigns,
        ICampaignTargetRepository campaignTargets)
    {
        _tenant = tenant;
        _segments = segments;
        _membership = membership;
        _strategies = strategies;
        _campaigns = campaigns;
        _campaignTargets = campaignTargets;
    }

    /// <summary>The doctor's play at <paramref name="at"/> (see class note). A non-doctor visit has no play.</summary>
    public async Task<DerivedPlay> DerivePlayAsync(Guid? contactId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId || contactId is not { } cid || cid == Guid.Empty)
        {
            return DerivedPlay.None(Array.Empty<Guid>());
        }

        if (_plays.TryGetValue(cid, out var cached))
        {
            return cached;
        }

        var segmentIds = await ContactSegmentMemberships.DeriveAsync(
            _segments, _membership, tenantId, cid, at, cancellationToken);

        var bound = new List<(StrategyTemplateSummary Play, Guid SegmentId)>();
        foreach (var segmentId in segmentIds)
        {
            foreach (var play in await _strategies.ListBySegmentAsync(segmentId, at, cancellationToken))
            {
                bound.Add((play, segmentId));
            }
        }

        // WP-E2E-FIX-3 (E5-B2) — across the doctor's segments, the reader's single effective-version rule decides.
        var firstSegment = bound
            .GroupBy(b => b.Play.TemplateId)
            .ToDictionary(g => g.Key, g => g.First().SegmentId);
        var ordered = StrategyTemplateReader
            .InPreferenceOrder(bound.Select(b => b.Play).DistinctBy(p => p.TemplateId))
            .Select(p => (Play: p, SegmentId: firstSegment[p.TemplateId]))
            .ToList();

        var result = ordered.Count == 0
            ? DerivedPlay.None(segmentIds)
            : new DerivedPlay(
                ordered[0].Play.TemplateId,
                ordered[0].SegmentId,
                segmentIds,
                ordered.Count > 1 ? MultiplePlays : null);

        _plays[cid] = result;
        return result;
    }

    /// <summary>The campaign a visit to <paramref name="contactId"/> / <paramref name="accountId"/> on
    /// <paramref name="date"/> belongs to, or null (see class note).</summary>
    public async Task<Guid?> DeriveCampaignAsync(
        Guid? contactId, Guid? accountId, DateOnly date, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return null;
        }

        var at = new DateTimeOffset(date.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);
        _activeCampaigns ??= (await _campaigns.ListAsync(tenantId, cancellationToken))
            .Where(c => !c.IsArchived()
                        && string.Equals(c.CampaignStatus, CampaignStatuses.Active, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.StartDate)
            .ThenBy(c => c.CampaignCode, StringComparer.Ordinal)
            .ToList();

        foreach (var campaign in _activeCampaigns.Where(c => c.IsEffectiveAt(at)))
        {
            if (contactId is { } cid && cid != Guid.Empty
                && await IsLiveTargetAsync(tenantId, campaign.Id, CampaignTargetTypes.Contact, cid, at, cancellationToken))
            {
                return campaign.Id;
            }

            if (accountId is { } aid && aid != Guid.Empty
                && await IsLiveTargetAsync(tenantId, campaign.Id, CampaignTargetTypes.Account, aid, at, cancellationToken))
            {
                return campaign.Id;
            }
        }

        return null;
    }

    private async Task<bool> IsLiveTargetAsync(
        Guid tenantId, Guid campaignId, string targetType, Guid targetId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var target = await _campaignTargets.FindActiveByTargetAsync(tenantId, campaignId, targetType, targetId, cancellationToken);
        return target is not null
               && target.IsActiveMembership()
               && !string.Equals(target.TargetStatus, CampaignTargetStatuses.Excluded, StringComparison.OrdinalIgnoreCase)
               && target.IsEffectiveAt(at);
    }
}

/// <summary>WP-VP-2 (B-3) — the derivation seam (tests substitute it).</summary>
public interface IVisitProvenanceDeriver
{
    Task<DerivedPlay> DerivePlayAsync(Guid? contactId, DateTimeOffset at, CancellationToken cancellationToken);

    Task<Guid?> DeriveCampaignAsync(Guid? contactId, Guid? accountId, DateOnly date, CancellationToken cancellationToken);
}

/// <summary>The derived play of one doctor: the chosen template (null ⇒ no play), the segment it was bound through, all
/// of the doctor's active segments, and <see cref="VisitProvenanceDeriver.MultiplePlays"/> when the choice was not unique.</summary>
public sealed record DerivedPlay(
    Guid? StrategyTemplateId,
    Guid? SegmentId,
    IReadOnlyList<Guid> SegmentIds,
    string? ReasonCode)
{
    public static DerivedPlay None(IReadOnlyList<Guid> segmentIds)
        => new(null, null, segmentIds, VisitProvenanceDeriver.NoStrategy);
}
