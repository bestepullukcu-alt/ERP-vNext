using Diten.CrmService.Domain.Entities;
using PlannedVisitEntity = Diten.CrmService.Domain.Entities.PlannedVisit;

namespace Diten.CrmService.Application.Features.VisitPlanning;

/// <summary>
/// WP-VP-4A (F3-2) — an OLD plan that was applied for the whole period at once (status <c>committed</c>, before the 3A
/// week model) fitted onto the week model, READ-ONLY (nothing is stored or migrated):
/// <list type="bullet">
/// <item>its <see cref="PlanningSession.CommittedPlannedVisitIds"/> that are not cancelled / archived are FIXED visits —
/// exactly like an approved week's (counted, shown as written, never re-generated);</item>
/// <item>a week holding one of them reads as approved with <c>storedStatus = legacy</c> and no history;</item>
/// <item>every other week is <c>empty</c>: the plan was applied whole, so no draft is generated for it (new visits need a
/// new-model plan; an old plan has no week reopen).</item>
/// </list>
/// A <c>generated</c> / <c>draft</c> old plan is NOT legacy: it behaves exactly like a 3A plan.
/// </summary>
public static class LegacyCommittedPlan
{
    public static bool IsLegacy(PlanningSession session) => session.IsCommitted();

    /// <summary>The legacy plan's fixed visits among <paramref name="plans"/> (empty for a non-legacy plan).</summary>
    public static IReadOnlyList<PlannedVisitEntity> FixedVisits(PlanningSession session, IEnumerable<PlannedVisitEntity> plans)
    {
        if (!IsLegacy(session) || session.CommittedPlannedVisitIds.Count == 0)
        {
            return Array.Empty<PlannedVisitEntity>();
        }

        var ids = session.CommittedPlannedVisitIds.ToHashSet();
        return plans.Where(p => ids.Contains(p.Id) && !p.IsCancelled() && !p.IsArchived()).ToList();
    }

    /// <summary>The week as the read model sees it: the stored one, else — for a legacy plan whose fixed visits fall in it —
    /// a synthetic <c>legacy</c> week carrying those visits; else null.</summary>
    public static PlanningWeek? WeekOf(
        PlanningSession session, PlanningWeekSpan week, IReadOnlyCollection<PlannedVisitEntity> legacyFixed)
    {
        if (session.WeekOf(week.WeekStart) is { } stored)
        {
            return stored;
        }

        var inWeek = legacyFixed.Where(v => PlanningWeekCalendar.MondayOf(v.PlannedDate) == week.Monday).Select(v => v.Id).ToList();
        return inWeek.Count == 0
            ? null
            : new PlanningWeek { WeekStart = week.WeekStart, Status = PlanningWeekStatus.Legacy, PlannedVisitIds = inWeek };
    }
}
