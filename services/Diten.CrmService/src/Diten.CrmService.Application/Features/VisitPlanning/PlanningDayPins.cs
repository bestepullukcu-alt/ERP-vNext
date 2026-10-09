using System.Globalization;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.RouteOptimization;
using Diten.CrmService.Application.Features.VisitPlanning.Commands;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.VisitPlanning;

/// <summary>
/// WP-VP-4E — the rep's day pins: their write rules (through the EXISTING selection update — no new command) and their
/// vocabulary.
/// <list type="bullet">
/// <item><b>Week</b> — a Monday of the plan's period (400 <c>invalid_week</c>), not approved (409
/// <c>week_already_approved</c>), not over (409 <c>week_in_past</c>).</item>
/// <item><b>Pin</b> — a known target type, a known scope (absent = <c>visit</c>), a date inside that week that is a
/// working (or half) day and not already gone (400 <c>pin_not_working_day</c>).</item>
/// <item><b>Time</b> (WP-VW-W2 BE-b, optional) — only a <c>visit</c> pin carries one: <c>"HH:mm"</c> on the 15-minute grid
/// (400 <c>pin_time_invalid</c>), inside the day's working window and not in its lunch break (400
/// <c>pin_time_outside_hours</c>). Without a time the pin is a day pin, exactly as before.</item>
/// <item><b>Target</b> — not checked here: a pinned target that has no visit in the week is IGNORED by the engine and
/// reported on the preview (<c>pin_target_not_in_week</c>).</item>
/// </list>
/// The request's pins REPLACE that week's (an empty list clears them); other weeks' pins are untouched. A second pin of
/// the same target + scope keeps the last one.
/// </summary>
public static class PlanningDayPins
{
    public const string PinNotWorkingDay = "pin_not_working_day";
    public const string InvalidPin = "invalid_day_pin";

    /// <summary>WP-VW-W2 (BE-b) — a time that is not "HH:mm" on the 15-minute grid, or a time on an institution pin.</summary>
    public const string PinTimeInvalid = "pin_time_invalid";

    /// <summary>WP-VW-W2 (BE-b) — a time outside the day's working window (or in its lunch break).</summary>
    public const string PinTimeOutsideHours = "pin_time_outside_hours";

    /// <summary>Preview warnings / reasons.</summary>
    public const string PinTargetNotInWeek = "pin_target_not_in_week";
    public const string PinOverflow = "pin_overflow";
    public const string PinDayFull = "pin_day_full";
    public const string PinOutsideAvailability = "pin_outside_availability";

    /// <summary>WP-VW-W2 (BE-b) — a time-pinned visit whose time was taken (another time pin got it first): it sits at the
    /// nearest free start of the day instead (a preview move, from = to = its day).</summary>
    public const string PinTimeConflict = "pin_time_conflict";

    /// <summary>WP-VW-W2 (BE-b, CT, user 2026-10-09) — a time-pinned visit whose pinned start is valid but whose visit would
    /// end after the day's working window: it sits at the nearest start that ends in time instead (a preview move, from =
    /// to = its day). Said apart from <see cref="PinTimeConflict"/> so the rep knows the time, not another pin, was the cause.</summary>
    public const string PinTimePastDayEnd = "pin_time_past_day_end";

    private static readonly string[] PinnableTargetTypes =
        { PlannedVisitTargetType.Contact, PlannedVisitTargetType.Pharmacy, PlannedVisitTargetType.Account };

    /// <summary>Checks one week's pins and returns them in their stored form, or the refusal.</summary>
    public static (Response<T>? Refused, string WeekStart, List<PlanningDayPin> Pins) Validate<T>(
        PlanningSession session, DayPinsInput input, DateOnly periodStart, DateOnly periodEnd, DateOnly today,
        Func<DateOnly, string> kindOf,
        // WP-VW-W2 (BE-b) — a day's working window (the engine's own: PlanningDayBudget.WindowFor); null = the configured
        // route day (09:00–18:00, lunch 13:00–14:00).
        Func<DateOnly, WorkingDayHours>? windowOf = null)
    {
        var none = new List<PlanningDayPin>();
        if (!PlanningWeekCalendar.TryParseWeek(input.WeekStart, periodStart, periodEnd, out var span))
        {
            return (Response<T>.Fail(new[]
            {
                Handlers.CommandHandlers.PlanningSessionErrorCodes.InvalidWeek,
                "dayPins.weekStart must be a Monday (yyyy-MM-dd) of a week of the plan's period."
            }, 400), string.Empty, none);
        }

        if (session.WeekOf(span.WeekStart)?.IsApproved() == true)
        {
            return (Response<T>.Fail(new[]
            {
                Handlers.CommandHandlers.PlanningSessionErrorCodes.WeekAlreadyApproved,
                "An approved week cannot be re-pinned; reopen it first."
            }, 409), string.Empty, none);
        }

        if (PlanningWeekCalendar.IsPast(span, today))
        {
            return (Response<T>.Fail(new[]
            {
                Handlers.CommandHandlers.PlanningSessionErrorCodes.WeekInPast, "A past week cannot be pinned."
            }, 409), string.Empty, none);
        }

        var pins = new List<PlanningDayPin>();
        foreach (var pin in input.Pins ?? Array.Empty<DayPinInput>())
        {
            var targetType = PlannedVisitTargetType.Normalize(pin.TargetType);
            var scope = string.IsNullOrWhiteSpace(pin.Scope) ? PlanningDayPinScopes.Visit : pin.Scope.Trim().ToLowerInvariant();
            if (!PinnableTargetTypes.Contains(targetType) || pin.TargetId == Guid.Empty || !PlanningDayPinScopes.IsKnown(scope))
            {
                return (Response<T>.Fail(new[]
                {
                    InvalidPin, "A pin needs a target (contact | pharmacy | account + id) and a scope (visit | institution)."
                }, 400), string.Empty, none);
            }

            if (!DateOnly.TryParseExact((pin.Date ?? string.Empty).Trim(), PlanningWeekCalendar.DateFormat,
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                || date < span.From || date > span.To || date < today
                || kindOf(date) is not (PlanningDayKinds.Working or PlanningDayKinds.Half))
            {
                return (Response<T>.Fail(new[]
                {
                    PinNotWorkingDay, $"A pin's date must be a working day of the week {span.WeekStart} (from today on)."
                }, 400), string.Empty, none);
            }

            string? startTime = null;
            if (!string.IsNullOrWhiteSpace(pin.StartTime))
            {
                var minute = DayTimePins.ParseGridTime(pin.StartTime);
                if (scope != PlanningDayPinScopes.Visit || minute is null || minute.Value % DayTimePins.Step != 0)
                {
                    return (Response<T>.Fail(new[]
                    {
                        PinTimeInvalid, "A pin's time is \"HH:mm\" on the 15-minute grid, and only a visit pin carries one."
                    }, 400), string.Empty, none);
                }

                var window = windowOf?.Invoke(date) ?? RouteOptimizationDefaults.WorkingDay;
                var dayStart = RouteTime.ParseMinutes(window.Start) ?? 9 * 60;
                var dayEnd = RouteTime.ParseMinutes(window.End) ?? 18 * 60;
                var lunchStart = RouteTime.ParseMinutes(window.LunchStart);
                var lunchEnd = RouteTime.ParseMinutes(window.LunchEnd);
                var inLunch = lunchStart is { } ls && lunchEnd is { } le && le > ls && minute >= ls && minute < le;
                if (minute < dayStart || minute >= dayEnd || inLunch)
                {
                    return (Response<T>.Fail(new[]
                    {
                        PinTimeOutsideHours,
                        $"A pin's time must be inside the working hours of {date:yyyy-MM-dd} ({window.Start}–{window.End}), not in the lunch break."
                    }, 400), string.Empty, none);
                }

                startTime = RouteTime.Format(minute.Value);
            }

            var contactId = targetType == PlannedVisitTargetType.Contact ? pin.ContactId ?? pin.TargetId : pin.ContactId;
            pins.RemoveAll(p => p.TargetType == targetType && p.TargetId == pin.TargetId && p.Scope == scope);
            pins.Add(new PlanningDayPin
            {
                WeekStart = span.WeekStart,
                TargetType = targetType,
                TargetId = pin.TargetId,
                ContactId = contactId,
                Date = date.ToString(PlanningWeekCalendar.DateFormat, CultureInfo.InvariantCulture),
                Scope = scope,
                StartTime = startTime
            });
        }

        return (null, span.WeekStart, pins);
    }

    /// <summary>The session's pins after the request: that week's replaced, the others kept.</summary>
    public static List<PlanningDayPin> Replace(IEnumerable<PlanningDayPin> current, string weekStart, IEnumerable<PlanningDayPin> pins)
        => current.Where(p => !string.Equals(p.WeekStart, weekStart, StringComparison.Ordinal)).Concat(pins).ToList();
}
