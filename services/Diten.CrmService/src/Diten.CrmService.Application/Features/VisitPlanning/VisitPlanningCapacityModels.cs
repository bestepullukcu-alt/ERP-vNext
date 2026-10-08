namespace Diten.CrmService.Application.Features.VisitPlanning;

// WP-VP-3B — the day / week capacity read models of the preview (additive; kept out of VisitPlanningModels.cs so the
// parallel packages do not collide on it). Minutes everywhere: supply and demand in ONE unit (C5).

/// <summary>A visit the week could not hold, moved to a later DRAFT week (MK-6). <c>FromWeek</c> / <c>ToWeek</c> are week
/// indexes of the period (the preview's <c>weeks</c> list). <c>Reason</c>: <see cref="PlanningShiftReasons"/>.</summary>
public sealed record ShiftedVisitPreview(
    string TargetType,
    Guid TargetId,
    Guid? ContactId,
    string? DisplayName,
    int FromWeek,
    int ToWeek,
    string Reason);

/// <summary>One week's capacity (B-7). <c>CapacityMinutes</c> = Σ day budgets of the week's days inside the period
/// (working = budget, half = budget ÷ 2, holiday / weekend = 0); <c>PlannedMinutes</c> = Σ (duration + buffer) of its
/// planned and fixed visits; <c>DailyCap</c> = the full-day "at most N a day".</summary>
public sealed record WeekCapacityDto(
    string WeekStart,
    int WorkingDays,
    int HalfDays,
    int Holidays,
    int CapacityMinutes,
    int PlannedMinutes,
    int VisitCount,
    int DailyCap,
    // WP-VP-3C (K-7, additive) — per product: the week's visits telling it, and of those the promo ones.
    IReadOnlyList<ProductVisitCountDto>? ProductVisitCounts = null);

/// <summary>The period in minutes (C5): capacity vs planned, plus the day budget the run used and where it came from
/// (<c>cycle_capacity</c> / <c>default_hours</c>).</summary>
public sealed record PeriodCapacityDto(
    int CapacityMinutes,
    int PlannedMinutes,
    int DailyBudgetMinutes,
    int DailyCap,
    int HalfDayCap,
    string BudgetSource);

/// <summary>WP-VP-3B — why a visit moved to a later week: the week was full, or lost capacity to a holiday / half day.</summary>
public static class PlanningShiftReasons
{
    public const string CapacityFull = "capacity_full";
    public const string Holiday = "holiday";
    public const string HalfDay = "half_day";

    /// <summary>WP-VP-4G (F4-1) - the week had room, but no day near the visit's cluster (nor a light day) to take it.</summary>
    public const string NoNearDay = "no_near_day";
}

/// <summary>WP-VP-3B — the engine's own unscheduled reason (beside the route optimizer's) and the consent warning.</summary>
public static class PlanningVisitReasons
{
    /// <summary>The doctor's visit-channel consent is <c>blocked</c> (the campaign "blocked ⇒ excluded" rule): not planned.</summary>
    public const string ConsentBlocked = "consent_blocked";

    /// <summary>A warning on the doctor's content preview: consent is <c>unknown</c>, the visit is still planned.</summary>
    public const string ConsentUnknown = "consent_unknown";
}
