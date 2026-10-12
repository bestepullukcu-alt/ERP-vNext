using System.Globalization;
using System.Text.RegularExpressions;

namespace Diten.Platform.Application.Features.TimeEntry.Services;

/// <summary>
/// MOD-0280-FU01 (pack §8.3, D5, A9) — the week arithmetic, in ONE place.
///
/// <para><b>ISO 8601, Monday start, week-YEAR rules.</b> 2027-01-01 is a Friday and belongs to <c>2026-W53</c>, not to
/// "week 1 of 2027": <see cref="ISOWeek"/> answers that, a calendar-year week number does not (T-08).</para>
///
/// <para><b>Every "today" is the tenant's local today</b> (R3), taken from the time zone the working-hours provider
/// returns — never the UTC date. An evening entry in Istanbul (UTC+3) is already tomorrow there; a Sunday-night instant
/// in Berlin is still Sunday after the October clock change, one hour later than a fixed offset would say (T-07).</para>
/// </summary>
public static partial class WeekCalendar
{
    [GeneratedRegex(@"^(?<year>\d{4})-W(?<week>\d{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex WeekKeyPattern();

    /// <summary>Parses <c>2026-W40</c>. False for anything else, including a week 53 in a 52-week year.</summary>
    public static bool TryParse(string? weekKey, out DateOnly monday)
    {
        monday = default;
        if (string.IsNullOrWhiteSpace(weekKey))
        {
            return false;
        }

        var match = WeekKeyPattern().Match(weekKey.Trim());
        if (!match.Success)
        {
            return false;
        }

        var year = int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture);
        var week = int.Parse(match.Groups["week"].Value, CultureInfo.InvariantCulture);
        if (year < 2000 || year > 2999 || week < 1 || week > ISOWeek.GetWeeksInYear(year))
        {
            return false;
        }

        monday = DateOnly.FromDateTime(ISOWeek.ToDateTime(year, week, DayOfWeek.Monday));
        return true;
    }

    /// <summary>The ISO week key of a local date: <c>yyyy-Www</c> with the ISO week-year.</summary>
    public static string KeyOf(DateOnly date)
    {
        var dateTime = date.ToDateTime(TimeOnly.MinValue);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{ISOWeek.GetYear(dateTime):D4}-W{ISOWeek.GetWeekOfYear(dateTime):D2}");
    }

    /// <summary>The Monday of the ISO week a local date falls in.</summary>
    public static DateOnly MondayOf(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7; // Monday → 0 … Sunday → 6
        return date.AddDays(-offset);
    }

    /// <summary>The tenant-local calendar day of an instant.</summary>
    public static DateOnly LocalDateOf(DateTimeOffset instant, TimeZoneInfo zone)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);

    /// <summary>
    /// A9 — the current local week and the <see cref="TimeEntryLimits.EditWindowPreviousWeeks"/> weeks before it are
    /// editable without a reopen. A later week is "inside" too: nothing can be written into it anyway (every day of it
    /// is in the future), and refusing it here would give the wrong reason.
    /// </summary>
    public static bool IsInsideEditWindow(DateOnly weekStart, DateOnly localToday)
    {
        var oldestEditableMonday = MondayOf(localToday).AddDays(-7 * TimeEntryLimits.EditWindowPreviousWeeks);
        return weekStart >= oldestEditableMonday;
    }

    /// <summary>The seven local dates of the week starting at <paramref name="monday"/>.</summary>
    public static IReadOnlyList<DateOnly> DaysOf(DateOnly monday)
        => Enumerable.Range(0, 7).Select(monday.AddDays).ToList();
}
