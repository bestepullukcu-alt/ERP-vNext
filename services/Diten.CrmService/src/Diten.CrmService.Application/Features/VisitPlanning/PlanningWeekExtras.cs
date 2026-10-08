using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.VisitPlanning.Commands;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.VisitPlanning;

/// <summary>
/// WP-VP-4L (2) — the rep's per-week EXTRA visits: "visit this doctor in this week too", over and above the frequency.
/// Written through the EXISTING selection update (<c>weekExtras { weekStart, targets }</c> — the day pins pattern: absent =
/// kept, <c>targets: []</c> = that week's cleared, other weeks untouched). No new command.
/// <list type="bullet">
/// <item><b>Week</b> — a Monday of the plan's period (400 <c>invalid_week</c>), not approved (409
/// <c>week_already_approved</c>), not over (409 <c>week_in_past</c>): only a draft / empty week takes extras.</item>
/// <item><b>Target</b> — a doctor / pharmacy / institution of the plan's selection (after this request's own selection
/// change), else 400 <c>extra_target_not_in_plan</c>; the same target once per week.</item>
/// <item><b>Engine</b> — an extra joins ITS week's visits (same day rules, day pins apply); it never moves to another week
/// (<c>extra_no_room</c> when the week cannot hold it); a target that already has a frequency visit that week takes no
/// extra (<c>extra_already_planned</c> on the preview — never a silent second visit); a consent-blocked doctor is not
/// planned at all (the 3B rule).</item>
/// </list>
/// </summary>
public static class PlanningWeekExtras
{
    public const string ExtraTargetNotInPlan = "extra_target_not_in_plan";
    public const string InvalidExtra = "invalid_week_extra";

    /// <summary>Preview reasons / warnings.</summary>
    public const string ExtraNoRoom = "extra_no_room";
    public const string ExtraAlreadyPlanned = "extra_already_planned";

    private static readonly string[] ExtraTargetTypes =
        { PlannedVisitTargetType.Contact, PlannedVisitTargetType.Pharmacy, PlannedVisitTargetType.Account };

    /// <summary>Checks one week's extras against the session (its weeks + its selection) and returns them in stored form,
    /// or the refusal.</summary>
    public static (Response<T>? Refused, string WeekStart, List<PlanningWeekExtra> Extras) Validate<T>(
        PlanningSession session, WeekExtrasInput input, DateOnly periodStart, DateOnly periodEnd, DateOnly today)
    {
        var none = new List<PlanningWeekExtra>();
        if (!PlanningWeekCalendar.TryParseWeek(input.WeekStart, periodStart, periodEnd, out var span))
        {
            return (Response<T>.Fail(new[]
            {
                Handlers.CommandHandlers.PlanningSessionErrorCodes.InvalidWeek,
                "weekExtras.weekStart must be a Monday (yyyy-MM-dd) of a week of the plan's period."
            }, 400), string.Empty, none);
        }

        if (session.WeekOf(span.WeekStart)?.IsApproved() == true)
        {
            return (Response<T>.Fail(new[]
            {
                Handlers.CommandHandlers.PlanningSessionErrorCodes.WeekAlreadyApproved,
                "An approved week takes no extra visit; reopen it first."
            }, 409), string.Empty, none);
        }

        if (PlanningWeekCalendar.IsPast(span, today))
        {
            return (Response<T>.Fail(new[]
            {
                Handlers.CommandHandlers.PlanningSessionErrorCodes.WeekInPast, "A past week takes no extra visit."
            }, 409), string.Empty, none);
        }

        var selection = session.Selection;
        var extras = new List<PlanningWeekExtra>();
        foreach (var target in input.Targets ?? Array.Empty<WeekExtraInput>())
        {
            var targetType = PlannedVisitTargetType.Normalize(target.TargetType);
            if (!ExtraTargetTypes.Contains(targetType) || target.TargetId == Guid.Empty)
            {
                return (Response<T>.Fail(new[]
                {
                    InvalidExtra, "An extra visit needs a target (contact | pharmacy | account + id)."
                }, 400), string.Empty, none);
            }

            var contactId = targetType == PlannedVisitTargetType.Contact ? target.ContactId ?? target.TargetId : (Guid?)null;
            var selected = targetType switch
            {
                PlannedVisitTargetType.Contact => selection.SelectedContacts.FirstOrDefault(c => c.ContactId == contactId),
                _ => null
            };
            var inPlan = targetType switch
            {
                PlannedVisitTargetType.Contact => selected is not null,
                PlannedVisitTargetType.Pharmacy => selection.SelectedPharmacyIds.Contains(target.TargetId),
                _ => selection.SelectedAccountIds.Contains(target.TargetId)
            };
            if (!inPlan)
            {
                return (Response<T>.Fail(new[]
                {
                    ExtraTargetNotInPlan, "An extra visit's target must be one of the plan's targets; add it to the plan first."
                }, 400), string.Empty, none);
            }

            if (extras.Any(e => e.TargetType == targetType && e.TargetId == target.TargetId))
            {
                continue; // the same target once per week
            }

            extras.Add(new PlanningWeekExtra
            {
                WeekStart = span.WeekStart,
                TargetType = targetType,
                TargetId = target.TargetId,
                ContactId = contactId,
                AccountId = targetType == PlannedVisitTargetType.Contact ? selected?.AccountId ?? target.AccountId : target.TargetId
            });
        }

        return (null, span.WeekStart, extras);
    }

    /// <summary>The session's extras after the request: that week's replaced, the others kept.</summary>
    public static List<PlanningWeekExtra> Replace(IEnumerable<PlanningWeekExtra> current, string weekStart, IEnumerable<PlanningWeekExtra> extras)
        => current.Where(e => !string.Equals(e.WeekStart, weekStart, StringComparison.Ordinal)).Concat(extras).ToList();
}
