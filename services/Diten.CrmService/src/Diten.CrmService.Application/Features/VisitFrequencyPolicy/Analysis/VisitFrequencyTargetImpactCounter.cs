using Diten.CrmService.Application.Features.Segmentation.Resolution;
using Diten.CrmService.Application.Features.Territory.AccountAssignments;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Vfp = Diten.CrmService.Domain.Entities.VisitFrequencyPolicy;

namespace Diten.CrmService.Application.Features.VisitFrequencyPolicy.Analysis;

/// <summary>
/// WP-FREQ-DET-A default impact counter. Each target type is counted through the reader that already OWNS that data —
/// nothing here re-implements membership, coverage or targeting:
/// <list type="bullet">
/// <item><c>account</c> / <c>contact</c> / <c>account-contact-link</c> → 1 (a single record).</item>
/// <item><c>segment</c> → MOD-0167 <see cref="SegmentMembershipResolver"/> TotalMemberCount (count-only funnel,
/// limit 0). A candidate-cap overflow or a missing segment is reported uncomputable, never a wrong number.</item>
/// <item><c>campaign-target</c> → the campaign's live target snapshot rows (MOD-0165 FU04 repository).</item>
/// <item><c>territory-node</c> → MOD-0151 <see cref="AccountCurrentCoverageResolver"/> current coverage for the node.</item>
/// <item><c>audience-profile</c> / <c>concept-node</c> → no ready cross-service member count exists; reported
/// uncomputable with a note rather than standing up a new matching engine.</item>
/// </list>
/// Every read is tenant-scoped and write-free.
/// </summary>
public sealed class VisitFrequencyTargetImpactCounter : IVisitFrequencyTargetImpactCounter
{
    private readonly ISegmentRepository _segments;
    private readonly SegmentMembershipResolver _segmentMembership;
    private readonly ICampaignTargetRepository _campaignTargets;
    private readonly IAccountTerritoryAssignmentRepository _accountAssignments;
    private readonly ITerritoryModelRepository _territoryModels;

    public VisitFrequencyTargetImpactCounter(
        ISegmentRepository segments,
        SegmentMembershipResolver segmentMembership,
        ICampaignTargetRepository campaignTargets,
        IAccountTerritoryAssignmentRepository accountAssignments,
        ITerritoryModelRepository territoryModels)
    {
        _segments = segments;
        _segmentMembership = segmentMembership;
        _campaignTargets = campaignTargets;
        _accountAssignments = accountAssignments;
        _territoryModels = territoryModels;
    }

    public async Task<VisitFrequencyTargetImpact> CountAsync(
        Guid tenantId, Vfp policy, DateTimeOffset at, CancellationToken cancellationToken)
    {
        switch (FrequencyTargetType.Normalize(policy.TargetType))
        {
            case FrequencyTargetType.Account:
            case FrequencyTargetType.Contact:
            case FrequencyTargetType.AccountContactLink:
                return VisitFrequencyTargetImpact.Countable(1);

            case FrequencyTargetType.Segment:
                return await CountSegmentAsync(tenantId, policy.TargetId, at, cancellationToken);

            case FrequencyTargetType.CampaignTarget:
                return await CountCampaignTargetsAsync(tenantId, policy.TargetId, cancellationToken);

            case FrequencyTargetType.TerritoryNode:
                return await CountTerritoryNodeAsync(tenantId, policy.TargetId, at, cancellationToken);

            case FrequencyTargetType.AudienceProfile:
            case FrequencyTargetType.ConceptNode:
                // No lightweight member count exists for these group targets; resolving members would require a new
                // cross-service matching engine, which is deliberately out of scope for this analysis phase.
                return VisitFrequencyTargetImpact.NotCountable(
                    "bu hedef tipi için üye sayımı henüz yok");

            default:
                return VisitFrequencyTargetImpact.NotCountable("bilinmeyen hedef tipi");
        }
    }

    private async Task<VisitFrequencyTargetImpact> CountSegmentAsync(
        Guid tenantId, Guid segmentId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var segment = await _segments.GetByIdAsync(tenantId, segmentId, cancellationToken);
        if (segment is null)
        {
            return VisitFrequencyTargetImpact.NotCountable("segment bulunamadı");
        }

        // Count-only funnel: limit 0 returns no member page but the exact TotalMemberCount, and skips the display-only
        // workplace enrichment. The resolver persists nothing.
        var outcome = await _segmentMembership.ResolveAsync(
            tenantId, segment, at, limit: 0, offset: 0, includeExcluded: false, cancellationToken);

        if (outcome.CandidateCapExceeded || outcome.Result is null)
        {
            return VisitFrequencyTargetImpact.NotCountable(
                "segment aday tavanı aşıldı; üye sayısı hesaplanamadı");
        }

        return VisitFrequencyTargetImpact.Countable(outcome.Result.TotalMemberCount);
    }

    private async Task<VisitFrequencyTargetImpact> CountCampaignTargetsAsync(
        Guid tenantId, Guid campaignId, CancellationToken cancellationToken)
    {
        var targets = await _campaignTargets.ListByCampaignAsync(tenantId, campaignId, cancellationToken);
        // Live members only: an archived or explicitly excluded target is not part of the campaign's reach.
        var count = targets.Count(t =>
            t.IsActiveMembership()
            && !string.Equals(t.TargetStatus, CampaignTargetStatuses.Excluded, StringComparison.OrdinalIgnoreCase));
        return VisitFrequencyTargetImpact.Countable(count);
    }

    private async Task<VisitFrequencyTargetImpact> CountTerritoryNodeAsync(
        Guid tenantId, Guid nodeId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var covered = await AccountCurrentCoverageResolver.ResolveCoveredAccountIdsByNodesAsync(
            _accountAssignments, _territoryModels, tenantId, new[] { nodeId }, at, cancellationToken);
        return VisitFrequencyTargetImpact.Countable(covered.Count);
    }
}
