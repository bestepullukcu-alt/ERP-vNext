using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.VisitPlanning;

/// <summary>Read-model mapping for the PlanningSession staging aggregate. Provenance / generation state are surfaced;
/// TenantId is never exposed.</summary>
internal static class PlanningSessionMapper
{
    public static PlanningSessionDto ToDto(PlanningSession s) => new(
        s.Id,
        s.CyclePeriodId,
        s.ResourceId,
        s.ResourceType,
        s.ResourceDisplayName,
        s.Status,
        s.Selection.SelectedAccountIds.ToList(),
        s.Selection.SelectedPharmacyIds.ToList(),
        s.Selection.SelectedContacts
            .Select(c => new PlanningSessionContactDto(c.ContactId, c.AccountId, c.AccountContactLinkId)).ToList(),
        s.Selection.SegmentId,
        s.Selection.CampaignId,
        s.GenerationState.LastGeneratedAt,
        s.GenerationState.ScheduledCount,
        s.GenerationState.UnscheduledCount,
        s.GenerationState.SupplyDemandStatus,
        s.CommittedPlannedVisitIds.ToList(),
        s.ManualVisitOrder.ToList(),
        s.TargetWeekStart,
        s.Version,
        s.CreatedAt,
        s.CreatedBy,
        s.UpdatedAt,
        s.UpdatedBy);

    public static PlanningSessionListItemDto ToListItem(PlanningSession s) => new(
        s.Id,
        s.CyclePeriodId,
        s.ResourceId,
        s.ResourceDisplayName,
        s.Status,
        s.Selection.SelectedContacts.Count,
        s.GenerationState.ScheduledCount,
        s.GenerationState.SupplyDemandStatus,
        s.Version,
        s.CreatedAt,
        s.UpdatedAt,
        s.TargetWeekStart);
}
