using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Enums.Meetings;

namespace Diten.Platform.Application.Features.Meetings.Services;

/// <summary>
/// MOD-0357 S11 — WHEN a series' NEXT occurrence is due to be generated, and what that occurrence is called.
/// Pure, mirroring <c>TaskRecurrenceSchedule</c>'s own shape and its two central decisions (UTC throughout;
/// occurrences computed from the ANCHOR by index, never by stepping from the previous one, to avoid month-end
/// drift) — copied on purpose, not reinvented (pack's own instruction for this WP).
///
/// <para><b>The one thing that is NOT copied from Tasks: the trigger direction.</b>
/// <c>TaskRecurrenceSchedule.LatestDueOccurrence</c> answers "what already began" (generate AFTER the fact).
/// A meeting series answers "what is coming up" (generate AHEAD of it, within <see cref="MeetingSeries.LeadTimeDays"/>)
/// — KS5's own requirement, because a meeting needs advance notice a background task never did.</para>
/// </summary>
public static class MeetingSeriesSchedule
{
    public const string ProcessInstancePrefix = "meeting-series";

    /// <summary>The name of ONE occurrence: series + the instant that occurrence begins. Deterministic — the
    /// same reason <c>TaskRecurrenceSchedule.ProcessInstanceId</c> is: a rerun computing the same period must
    /// produce the SAME stamp, so "already generated?" is a comparison, not a guess.</summary>
    public static string ProcessInstanceId(Guid seriesId, DateTimeOffset occurrenceStart)
        => $"{ProcessInstancePrefix}:{seriesId:N}:{occurrenceStart.ToUniversalTime():yyyyMMdd'T'HHmmss'Z'}";

    /// <summary>The instant occurrence <paramref name="index"/> begins, measured from <see cref="MeetingSeries.StartsAt"/>
    /// (the anchor). <c>AddMonths</c> clamps at short months (31 Jan → 28 Feb → 31 Mar), the same month-end
    /// behavior <c>TaskRecurrenceSchedule.OccurrenceAt</c> documents and this WP's own reading list names as the
    /// pattern to copy.</summary>
    public static DateTimeOffset OccurrenceAt(MeetingSeries series, int index)
    {
        ArgumentNullException.ThrowIfNull(series);
        var anchor = series.StartsAt.ToUniversalTime();
        var step = Math.Max(1, series.Interval) * index;

        return series.Frequency switch
        {
            MeetingSeriesFrequency.Weekly => anchor.AddDays(step * 7),
            MeetingSeriesFrequency.Monthly => anchor.AddMonths(step),
            MeetingSeriesFrequency.Quarterly => anchor.AddMonths(step * 3),
            MeetingSeriesFrequency.Yearly => anchor.AddMonths(step * 12),
            _ => anchor
        };
    }

    /// <summary>
    /// The NEXT occurrence the sweep should generate, or null when the series owes nothing right now: it is
    /// inactive/deleted (KS5), the next occurrence would fall after <see cref="MeetingSeries.EndsAt"/> (KS5), or
    /// the next occurrence's own start is still further away than <see cref="MeetingSeries.LeadTimeDays"/> (KS5).
    ///
    /// <para>Never returns an occurrence already generated: when <see cref="MeetingSeries.LastGeneratedAt"/> is
    /// set, the search starts at the occurrence AFTER it — found the same way
    /// <c>TaskRecurrenceSchedule.NextOccurrenceAfter</c> finds "the following one" (estimate the index, correct
    /// backward for month-end overshoot, take index + 1).</para>
    /// </summary>
    public static DateTimeOffset? NextDueOccurrence(MeetingSeries series, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(series);

        if (!series.IsActive || series.DeletedAt is not null)
        {
            return null;
        }

        var anchor = series.StartsAt.ToUniversalTime();
        DateTimeOffset nextOccurrence;
        if (series.LastGeneratedAt is not { } lastGenerated)
        {
            nextOccurrence = anchor;
        }
        else
        {
            var index = EstimateIndex(series, anchor, lastGenerated);
            while (index > 0 && OccurrenceAt(series, index) > lastGenerated)
            {
                index--;
            }

            nextOccurrence = OccurrenceAt(series, index + 1);
        }

        if (series.EndsAt is { } ends && nextOccurrence > ends)
        {
            return null;
        }

        var leadTimeDays = Math.Max(0, series.LeadTimeDays);
        return nextOccurrence <= nowUtc.AddDays(leadTimeDays) ? nextOccurrence : null;
    }

    private static int EstimateIndex(MeetingSeries series, DateTimeOffset anchor, DateTimeOffset at)
    {
        var interval = Math.Max(1, series.Interval);
        var elapsedDays = (at - anchor).TotalDays;
        var elapsedMonths = ((at.Year - anchor.Year) * 12) + at.Month - anchor.Month;

        var raw = series.Frequency switch
        {
            MeetingSeriesFrequency.Weekly => elapsedDays / (interval * 7d),
            MeetingSeriesFrequency.Monthly => elapsedMonths / (double)interval,
            MeetingSeriesFrequency.Quarterly => elapsedMonths / (interval * 3d),
            MeetingSeriesFrequency.Yearly => elapsedMonths / (interval * 12d),
            _ => 0d
        };

        return raw <= 0 ? 0 : (int)Math.Floor(raw);
    }
}
