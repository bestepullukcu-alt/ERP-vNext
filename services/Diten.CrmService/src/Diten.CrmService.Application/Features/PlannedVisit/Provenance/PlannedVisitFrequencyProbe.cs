using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Queries;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Resolve;
using Diten.CrmService.Domain.Entities;
using PlannedVisitEntity = Diten.CrmService.Domain.Entities.PlannedVisit;

namespace Diten.CrmService.Application.Features.PlannedVisit.Provenance;

/// <summary>
/// MOD-0165 read-only frequency wrapper. Calls <see cref="IVisitFrequencyPolicyResolver"/> <b>in-process via DI</b> —
/// never an HTTP self-call (§19.3/5) — and snapshots the verdict into <see cref="PlannedVisitFrequencyProvenance"/>.
/// Nothing but the decision + matched id + version + time is copied (D5): the policy's own effective window, priority and
/// record payload never cross into a PlannedVisit. A resolver that finds no policy yields <c>unknown</c>, never a
/// fabricated default, and the plan is still created (frequency is not a blocker).
/// </summary>
public sealed class PlannedVisitFrequencyProbe
{
    private readonly IVisitFrequencyPolicyResolver _resolver;

    public PlannedVisitFrequencyProbe(IVisitFrequencyPolicyResolver resolver) => _resolver = resolver;

    public async Task<PlannedVisitFrequencyProvenance> ResolveAsync(
        PlannedVisitEntity plan, Guid? segmentId, CancellationToken cancellationToken)
    {
        var query = new ResolveVisitFrequencyPolicyQuery(
            TargetType: plan.TargetType,
            TargetId: plan.TargetId,
            EffectiveAt: null,
            BusinessUnit: plan.BusinessUnit,
            TerritoryNodeId: plan.TerritoryNodeId,
            CampaignId: plan.CampaignId,
            SegmentId: segmentId);

        var result = await _resolver.ResolveAsync(query, cancellationToken);
        return Map(result);
    }

    private static PlannedVisitFrequencyProvenance Map(VisitFrequencyResolveResult r) => new()
    {
        FrequencyStatus = r.FrequencyStatus,
        SelectedFrequencyPolicyId = r.SelectedFrequencyPolicyId,
        SelectedPolicyCode = r.SelectedPolicyCode,
        SelectedPolicyName = r.SelectedPolicyName,
        FrequencyType = r.FrequencyType,
        RequiredVisitCount = r.RequiredVisitCount,
        PeriodType = r.PeriodType,
        SelectionReason = r.SelectionReason,
        ReasonCodes = r.ReasonCodes.ToList(),
        ResolvedAt = DateTimeOffset.UtcNow
    };
}
