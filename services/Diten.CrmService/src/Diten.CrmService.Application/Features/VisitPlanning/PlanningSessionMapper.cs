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

    /// <summary>WP-VP-2 (B-8) — the detail with the selection's read-time names.</summary>
    public static PlanningSessionDto ToDto(PlanningSession s, Features.PlannedVisit.VisitTargetNames names) => ToDto(s) with
    {
        SelectedContacts = s.Selection.SelectedContacts
            .Select(c => new PlanningSessionContactDto(
                c.ContactId, c.AccountId, c.AccountContactLinkId, names.Contact(c.ContactId), names.Account(c.AccountId)))
            .ToList(),
        SelectedAccounts = s.Selection.SelectedAccountIds.Select(id => new PlanningSessionNamedRefDto(id, names.Account(id))).ToList(),
        SelectedPharmacies = s.Selection.SelectedPharmacyIds.Select(id => new PlanningSessionNamedRefDto(id, names.Account(id))).ToList()
    };

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
        s.TargetWeekStart,
        s.Selection.SelectedPharmacyIds.Count,
        IsEmpty: !s.HasTargets(),
        ApprovedWeekCount: s.Weeks.Count(w => w.IsApproved()));

    /// <summary>WP-VP-3A — every week of the period for the detail: approved weeks with their visit count; the others
    /// draft (the plan has targets) or empty (it has none) — the exact split needs a generation (the preview).</summary>
    public static IReadOnlyList<PlanningWeekDto> DetailWeeks(PlanningSession s, DateOnly periodStart, DateOnly periodEnd, DateOnly today)
        => PlanningWeekCalendar.PeriodWeeks(periodStart, periodEnd)
            .Select(w =>
            {
                var stored = s.WeekOf(w.WeekStart);
                int? count = stored is not null && stored.IsApproved() ? stored.PlannedVisitIds.Count : null;
                var status = PlanningWeekCalendar.Derive(w, today, stored, count ?? (s.HasTargets() ? 1 : 0));
                return PlanningWeekCalendar.ToDto(w, status, count, stored);
            })
            .ToList();
}
