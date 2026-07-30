using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// Phase 4 — WHEN a recurrence rule is due, and what that occurrence is CALLED. Pure: no clock read beyond what
/// is handed in, no I/O, so the same rule and the same instant always produce the same answer.
///
/// <para><b>Everything is UTC, and that is a decision rather than an oversight.</b> Nothing in the platform
/// records a tenant's time zone — there is no such field on the tenant registry — so "the 31st" would have to be
/// guessed against some invented zone. A guess would be wrong for most tenants and invisible to all of them.
/// UTC is stated instead: the job runs UTC, the anchor is stored as an instant, and every occurrence is derived
/// from that instant. When tenant time zones exist, this class is where they land.</para>
///
/// <para><b>Occurrences are computed from the ANCHOR, never by stepping.</b> Stepping drifts: 31 January plus a
/// month is 28 February, and stepping again gives 28 March — the rule would silently walk off the month end and
/// never return. Computing occurrence N as <c>StartsAt.AddMonths(N × Interval)</c> gives
/// 31 Jan → 28 Feb → 31 Mar, which is the month-end answer this project chose (see
/// <see cref="OccurrenceAt"/>).</para>
/// </summary>
public static class TaskRecurrenceSchedule
{
    /// <summary>Namespaces the stamp so a <c>ProcessInstanceId</c> can be recognised for what it is.</summary>
    public const string ProcessInstancePrefix = "task-recurrence";

    /// <summary>
    /// The name of ONE occurrence: rule + the instant that occurrence began.
    ///
    /// <para><b>Deterministic on purpose.</b> A random id would distinguish nothing — two runs of the same period
    /// would produce two different ids and two tasks, and the duplicate would be hidden rather than prevented.
    /// This string is the same on every rerun, in every process, so "have I already made this one?" is a
    /// comparison rather than a guess.</para>
    ///
    /// <para>Second precision, in UTC, with a sortable layout — the stamp is read by humans in support and by
    /// range queries alike.</para>
    /// </summary>
    public static string ProcessInstanceId(Guid ruleId, DateTimeOffset occurrenceStart)
        => $"{ProcessInstancePrefix}:{ruleId:N}:{occurrenceStart.ToUniversalTime():yyyyMMdd'T'HHmmss'Z'}";

    /// <summary>
    /// The instant occurrence <paramref name="index"/> begins, always measured from the rule's anchor.
    ///
    /// <para><b>Month-end: the date CLAMPS, and the anchor is remembered.</b> A rule anchored on the 31st runs on
    /// the 28th (or 29th) in February and returns to the 31st in March. The alternative — skipping months that
    /// are too short — silently loses a period of work, and a monthly task that simply does not appear in
    /// February is the kind of absence nobody notices until an audit.</para>
    /// </summary>
    public static DateTimeOffset OccurrenceAt(TaskRecurrenceRule rule, int index)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var anchor = Anchor(rule);
        var step = Math.Max(1, rule.Interval) * index;

        return rule.Frequency switch
        {
            TaskRecurrenceFrequency.Daily => anchor.AddDays(step),
            TaskRecurrenceFrequency.Weekly => anchor.AddDays(step * 7),
            // AddMonths is what clamps, and it clamps from the ANCHOR — see the type summary.
            TaskRecurrenceFrequency.Monthly => anchor.AddMonths(step),
            TaskRecurrenceFrequency.Quarterly => anchor.AddMonths(step * 3),
            TaskRecurrenceFrequency.Yearly => anchor.AddMonths(step * 12),
            _ => anchor
        };
    }

    /// <summary>
    /// The most recent occurrence that has already begun at <paramref name="nowUtc"/>, or null when the rule owes
    /// nothing — it is inactive, deleted, has no frequency, has not started yet, or has ended.
    ///
    /// <para><b>Only the LATEST, never the backlog.</b> A daily rule dormant for three weeks does not produce
    /// twenty-one tasks the moment the sweep notices: a daily task from three weeks ago is not work anyone wants
    /// appearing now, and a flood is how a recovery becomes an incident. One occurrence per rule per sweep, and
    /// the sweep runs often enough that a live rule never falls behind.</para>
    /// </summary>
    public static DateTimeOffset? LatestDueOccurrence(TaskRecurrenceRule rule, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(rule);

        // Three separate reasons a rule owes nothing, checked separately because each is its own way for a
        // cancelled rule to keep producing work forever.
        if (!rule.IsActive || rule.DeletedAt is not null || rule.Frequency == TaskRecurrenceFrequency.None)
        {
            return null;
        }

        var anchor = Anchor(rule);
        if (nowUtc < anchor)
        {
            return null;
        }

        if (rule.EndsAt is { } endsAt && nowUtc > endsAt)
        {
            return null;
        }

        var index = EstimateIndex(rule, anchor, nowUtc);

        // Walk back to the first occurrence that has actually begun. The estimate can overshoot by one when a
        // month clamps (31 Jan → 28 Feb is 28 days, not 31), so it is corrected rather than trusted.
        while (index > 0 && OccurrenceAt(rule, index) > nowUtc)
        {
            index--;
        }

        var occurrence = OccurrenceAt(rule, index);
        if (occurrence > nowUtc)
        {
            return null;
        }

        // An occurrence AFTER the rule's end is not owed, even when the end has not been reached yet by `now`.
        return rule.EndsAt is { } end && occurrence > end ? null : occurrence;
    }

    /// <summary>
    /// When the occurrence after <paramref name="occurrence"/> begins — the deadline a generated task inherits:
    /// recurring work is due before the next one arrives.
    /// </summary>
    public static DateTimeOffset NextOccurrenceAfter(TaskRecurrenceRule rule, DateTimeOffset occurrence)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var index = EstimateIndex(rule, Anchor(rule), occurrence);

        // Find the index that actually produced this occurrence, then take the following one.
        while (index > 0 && OccurrenceAt(rule, index) > occurrence)
        {
            index--;
        }

        return OccurrenceAt(rule, index + 1);
    }

    /// <summary>
    /// The instant the rule is measured from. <c>StartsAt</c> when set; otherwise the rule's creation, because a
    /// rule with no explicit start still has to be anchored to SOMETHING stable — anchoring to "now" would move
    /// the whole schedule on every sweep and no occurrence would ever repeat its name.
    /// </summary>
    private static DateTimeOffset Anchor(TaskRecurrenceRule rule)
        => (rule.StartsAt ?? rule.CreatedAt).ToUniversalTime();

    private static int EstimateIndex(TaskRecurrenceRule rule, DateTimeOffset anchor, DateTimeOffset at)
    {
        var interval = Math.Max(1, rule.Interval);
        var elapsedDays = (at - anchor).TotalDays;
        var elapsedMonths = ((at.Year - anchor.Year) * 12) + at.Month - anchor.Month;

        var raw = rule.Frequency switch
        {
            TaskRecurrenceFrequency.Daily => elapsedDays / interval,
            TaskRecurrenceFrequency.Weekly => elapsedDays / (interval * 7d),
            TaskRecurrenceFrequency.Monthly => elapsedMonths / (double)interval,
            TaskRecurrenceFrequency.Quarterly => elapsedMonths / (interval * 3d),
            TaskRecurrenceFrequency.Yearly => elapsedMonths / (interval * 12d),
            _ => 0d
        };

        return raw <= 0 ? 0 : (int)Math.Floor(raw);
    }
}
