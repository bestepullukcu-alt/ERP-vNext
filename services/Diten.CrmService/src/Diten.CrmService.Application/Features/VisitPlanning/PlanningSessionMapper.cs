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
                c.ContactId, c.AccountId, c.AccountContactLinkId, names.Contact(c.ContactId), names.Account(c.AccountId),
                ProductsOf(c)))
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
        ApprovedWeekCount: s.Weeks.Count(w => w.IsApproved()),
        DoctorCount: s.Selection.SelectedContacts.Select(c => c.ContactId).Distinct().Count(),
        PharmacyCount: s.Selection.SelectedPharmacyIds.Distinct().Count());

    /// <summary>WP-VP-4A (E4-3C-B1) — a doctor's stored product pick for the reads (no MDM call: the stored code is the
    /// display, the name stays null). A pick without a role reads promo (K-7d).</summary>
    public static IReadOnlyList<PlanningSessionProductDto> ProductsOf(PlanningSessionSelectedContact contact)
        => contact.Products
            .Select(p => new PlanningSessionProductDto(
                p.ProductId, p.ProductCode, null,
                string.Equals(p.Role, StrategyProductLineRoles.NonPromo, StringComparison.OrdinalIgnoreCase)
                    ? StrategyProductLineRoles.NonPromo
                    : StrategyProductLineRoles.Promo))
            .ToList();

    /// <summary>
    /// WP-VP-4A (brief §1) — the list's draft weeks WITHOUT generating: the period's weeks that are neither past nor
    /// approved ("draft" here means "not approved and not over"; the detail's preview knows the exact split). An old
    /// committed plan and an archived plan have none.
    /// </summary>
    public static int DraftWeekCount(PlanningSession s, DateOnly periodStart, DateOnly periodEnd, DateOnly today)
        => LegacyCommittedPlan.IsLegacy(s) || s.IsArchived()
            ? 0
            : PlanningWeekCalendar.PeriodWeeks(periodStart, periodEnd)
                .Count(w => !PlanningWeekCalendar.IsPast(w, today) && s.WeekOf(w.WeekStart)?.IsApproved() != true);

    /// <summary>WP-VP-4A — today's week when today falls inside the period, else null.</summary>
    public static string? CurrentWeekStart(DateOnly periodStart, DateOnly periodEnd, DateOnly today)
        => today < periodStart || today > periodEnd
            ? null
            : PlanningWeekCalendar.MondayOf(today).ToString(PlanningWeekCalendar.DateFormat, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>WP-VP-4A — the "open the next week" target: the first week of the period AFTER today's week (its Monday
    /// later than today's Monday) that is not approved. Null when none, and always null for an old committed plan (no
    /// drafts; no week reopen there).</summary>
    public static string? NextDraftWeekStart(PlanningSession s, DateOnly periodStart, DateOnly periodEnd, DateOnly today)
        => LegacyCommittedPlan.IsLegacy(s)
            ? null
            : PlanningWeekCalendar.PeriodWeeks(periodStart, periodEnd)
                .FirstOrDefault(w => w.Monday > PlanningWeekCalendar.MondayOf(today) && s.WeekOf(w.WeekStart)?.IsApproved() != true)
                ?.WeekStart;

    /// <summary>WP-VP-3A — every week of the period for the detail: approved weeks with their visit count; the others
    /// draft (the plan has targets) or empty (it has none) — the exact split needs a generation (the preview).</summary>
    /// <para>WP-VP-4A — an old committed plan: a week holding its written visits (<paramref name="legacyFixed"/>) is
    /// approved with storedStatus legacy; every other week is empty (nothing is generated for it).</para>
    public static IReadOnlyList<PlanningWeekDto> DetailWeeks(
        PlanningSession s, DateOnly periodStart, DateOnly periodEnd, DateOnly today,
        IReadOnlyCollection<Domain.Entities.PlannedVisit>? legacyFixed = null)
        => PlanningWeekCalendar.PeriodWeeks(periodStart, periodEnd)
            .Select(w =>
            {
                var legacy = LegacyCommittedPlan.IsLegacy(s);
                var stored = LegacyCommittedPlan.WeekOf(s, w, legacyFixed ?? Array.Empty<Domain.Entities.PlannedVisit>());
                int? count = stored is not null && (stored.IsApproved() || stored.IsLegacy()) ? stored.PlannedVisitIds.Count : null;
                var status = PlanningWeekCalendar.Derive(w, today, stored, count ?? (s.HasTargets() && !legacy ? 1 : 0));
                return PlanningWeekCalendar.ToDto(w, status, count, stored);
            })
            .ToList();
}
