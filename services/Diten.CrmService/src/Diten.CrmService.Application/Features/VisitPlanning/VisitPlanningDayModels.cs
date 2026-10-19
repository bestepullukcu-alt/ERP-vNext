using CapacityEntity = Diten.CrmService.Domain.Entities.CycleCapacity;

namespace Diten.CrmService.Application.Features.VisitPlanning;

// WP-VP-4E — the read models of geography-aware days and the rep's day pins (additive; kept out of VisitPlanningModels.cs).

/// <summary>One working day of a draft week in the preview: its visiting budget, the minutes planned on it (visits +
/// between-visit buffer, fixed ones included), the minutes left IDLE (nothing near enough fits — a far group never fills
/// a day) and whether the plan exceeds the budget (only possible through visits written earlier).</summary>
public sealed record PlanningDayPreview(
    string Date, string WeekStart, string Kind, int BudgetMinutes, int PlannedMinutes, int IdleMinutes, bool OverCapacity);

/// <summary>A pinned visit that did not fit its pinned day: moved to <see cref="ToDate"/> (the next working day of the
/// week with room — auto-pinned), or to a later draft week (<c>pin_overflow</c>; <see cref="ToDate"/> where it landed,
/// null when it could not be placed). <see cref="Reason"/>: <c>pin_day_full</c> · <c>pin_outside_availability</c> ·
/// <c>pin_overflow</c>.</summary>
public sealed record PinOverflowPreview(
    string TargetType, Guid TargetId, Guid? ContactId, string? DisplayName, string FromDate, string? ToDate, string Reason);

/// <summary>A stored pin the run ignored: <c>pin_target_not_in_week</c> (no visit of the target that week) or
/// <c>pin_not_working_day</c> (the day is no longer a working day of the week, e.g. a new holiday, or already over).</summary>
public sealed record PinWarningPreview(string WeekStart, string TargetType, Guid TargetId, string Date, string Code);

/// <summary>One stored day pin (detail).</summary>
public sealed record PlanningDayPinDto(string TargetType, Guid TargetId, Guid? ContactId, string Date, string Scope, string? StartTime = null);

/// <summary>WP-VP-4E (4C §37) — the period capacity's per-visit model, so the product picker shows the limits and the
/// visit time without a capacity read key: max promo / non-promo products, minutes per product by role, the report
/// minutes of one visit (typical model: per-visit report; legacy: the report duration). <see cref="Source"/>
/// <c>cycle_capacity</c>, or <c>none</c> (every value null) when the period has no capacity.</summary>
public sealed record VisitModelDto(
    int? MaxPromo, int? MaxNonPromo, int? PromoMinutes, int? NonPromoMinutes, int? ReportMinutes, string Source)
{
    public const string FromCycleCapacity = "cycle_capacity";
    public const string None = "none";

    public static VisitModelDto From(CapacityEntity? capacity)
        => capacity is null
            ? new VisitModelDto(null, null, null, null, null, None)
            : new VisitModelDto(
                capacity.EffectiveMaxPromoProducts(), capacity.EffectiveMaxNonPromoProducts(),
                capacity.PromoProductTime, capacity.NonPromoProductTime, capacity.ReportMinutesForVisit(), FromCycleCapacity);
}
