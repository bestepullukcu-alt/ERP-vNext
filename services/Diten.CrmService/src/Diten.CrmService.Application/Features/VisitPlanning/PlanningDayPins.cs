using System.Globalization;
using Diten.CrmService.Application.Common.Models;
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

    /// <summary>Preview warnings / reasons.</summary>
    public const string PinTargetNotInWeek = "pin_target_not_in_week";
    public const string PinOverflow = "pin_overflow";
    public const string PinDayFull = "pin_day_full";
    public const string PinOutsideAvailability = "pin_outside_availability";

    private static readonly string[] PinnableTargetTypes =
        { PlannedVisitTargetType.Contact, PlannedVisitTargetType.Pharmacy, PlannedVisitTargetType.Account };

    /// <summary>Checks one week's pins and returns them in their stored form, or the refusal.</summary>
    public static (Response<T>? Refused, string WeekStart, List<PlanningDayPin> Pins) Validate<T>(
        PlanningSession session, DayPinsInput input, DateOnly periodStart, DateOnly periodEnd, DateOnly today,
        Func<DateOnly, string> kindOf)
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

            var contactId = targetType == PlannedVisitTargetType.Contact ? pin.ContactId ?? pin.TargetId : pin.ContactId;
            pins.RemoveAll(p => p.TargetType == targetType && p.TargetId == pin.TargetId && p.Scope == scope);
            pins.Add(new PlanningDayPin
            {
                WeekStart = span.WeekStart,
                TargetType = targetType,
                TargetId = pin.TargetId,
                ContactId = contactId,
                Date = date.ToString(PlanningWeekCalendar.DateFormat, CultureInfo.InvariantCulture),
                Scope = scope
            });
        }

        return (null, span.WeekStart, pins);
    }

    /// <summary>The session's pins after the request: that week's replaced, the others kept.</summary>
    public static List<PlanningDayPin> Replace(IEnumerable<PlanningDayPin> current, string weekStart, IEnumerable<PlanningDayPin> pins)
        => current.Where(p => !string.Equals(p.WeekStart, weekStart, StringComparison.Ordinal)).Concat(pins).ToList();
}
