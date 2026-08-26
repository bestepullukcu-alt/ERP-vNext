using System.Globalization;

namespace Diten.Platform.Domain.Entities.Tasks;

/// <summary>
/// What changed between the document that was there and the one that replaced it.
///
/// <para><b>It costs nothing to compute.</b> <c>TaskItemRepository.UpdateAsync</c> already writes with
/// <c>FindOneAndReplace</c> + <c>ReturnDocument.Before</c>, so both versions are in hand in the same round trip —
/// the pre-image exists because the lifecycle log needed it. This reads the pair that is already there; it adds
/// no query, no second write path and no work to a save that changed nothing.</para>
///
/// <para><b>A CLOSED list of fields, and that is the design.</b> Reflecting over the entity would sweep in
/// <c>Version</c>, <c>UpdatedAt</c>, <c>SpentHours</c>, the reminder claim key and every other piece of
/// bookkeeping — and each one would produce a history row nobody asked for. The set below is the answer to one
/// question: what changes WHAT THE WORK IS, or WHEN IT IS EXPECTED.</para>
///
/// <para><b>Pure and static.</b> No repository, no clock, no user context: the same two documents always produce
/// the same answer, which is what lets it be exercised directly rather than through a write.</para>
/// </summary>
public static class TaskFieldDiff
{
    /// <summary>
    /// The changes between <paramref name="previous"/> and <paramref name="current"/>, in a stable order.
    ///
    /// <para>Empty when nothing in the recorded set moved — a save that only bumped the version writes no
    /// history at all.</para>
    /// </summary>
    public static IReadOnlyList<TaskFieldChange> Between(TaskItem previous, TaskItem current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);

        var changes = new List<TaskFieldChange>();

        /*
         * ORDER IS FIXED, and it is the order a sentence wants: what the work IS, then when it is expected, then
         * how it is ranked and sized. A dictionary iteration order would make the same edit read differently on
         * two machines.
         */
        Add(changes, TaskFieldChangeCodes.Title, previous.Title, current.Title);
        Add(changes, TaskFieldChangeCodes.Description, previous.Description, current.Description);
        Add(changes, TaskFieldChangeCodes.DueAt, Date(previous.DueAt), Date(current.DueAt));
        Add(changes, TaskFieldChangeCodes.StartAt, Date(previous.StartAt), Date(current.StartAt));
        Add(changes, TaskFieldChangeCodes.PlannedDate, Date(previous.PlannedDate), Date(current.PlannedDate));
        Add(changes, TaskFieldChangeCodes.Priority, previous.Priority.ToString(), current.Priority.ToString());
        /*
         * The ASSIGNEE is recorded as an id, and the screen resolves the name — the same rule the waiting-on
         * person follows, and the opposite of a comment's author snapshot. A history that froze the name would
         * keep calling somebody by a surname they no longer use.
         *
         * ⚠ It is ALSO written by the lifecycle log (a reassign is a transition of its own). That is not a
         * duplicate: `reassign` records the ACT and its mandatory reason, while an edit that happens to change
         * the assignee has no reason and no ceremony. They cannot both fire for one save — the repository
         * attaches these changes to whichever single entry that save produced.
         */
        Add(changes, TaskFieldChangeCodes.Assignee, Id(previous.AssigneeUserId), Id(current.AssigneeUserId));
        Add(changes, TaskFieldChangeCodes.EstimateHours, Number(previous.EstimateHours), Number(current.EstimateHours));
        Add(changes, TaskFieldChangeCodes.Tags, Tags(previous.Tags), Tags(current.Tags));

        AddConfigurableFields(changes, previous, current);

        return changes;
    }

    /*
     * The tenant's OWN fields. Compared by definition code rather than by position: the values are a list, and a
     * reordered list is not an edit.
     *
     * A value that APPEARED and one that VANISHED are both changes — the first has no `from`, the second no
     * `to`. Neither is a "no change", so neither is skipped.
     */
    private static void AddConfigurableFields(List<TaskFieldChange> changes, TaskItem previous, TaskItem current)
    {
        var before = previous.FieldValues.ToDictionary(v => v.DefinitionCode, StringComparer.OrdinalIgnoreCase);
        var after = current.FieldValues.ToDictionary(v => v.DefinitionCode, StringComparer.OrdinalIgnoreCase);

        foreach (var code in before.Keys.Concat(after.Keys).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
        {
            var from = before.GetValueOrDefault(code)?.Value;
            var to = after.GetValueOrDefault(code)?.Value;
            if (string.Equals(from, to, StringComparison.Ordinal))
            {
                continue;
            }

            changes.Add(Build(TaskFieldChangeCodes.CustomField, from, to, code));
        }
    }

    private static void Add(List<TaskFieldChange> changes, string field, string? from, string? to)
    {
        if (string.Equals(from, to, StringComparison.Ordinal))
        {
            return;
        }

        changes.Add(Build(field, from, to, definitionCode: null));
    }

    private static TaskFieldChange Build(string field, string? from, string? to, string? definitionCode)
    {
        // Either side being long is enough to drop BOTH: a row showing only the new value would read as though
        // the field had been empty before.
        var tooLong = (from?.Length ?? 0) > TaskFieldChangeLimits.MaxRecordedValue
                      || (to?.Length ?? 0) > TaskFieldChangeLimits.MaxRecordedValue;

        return new TaskFieldChange
        {
            Field = field,
            From = tooLong ? null : from,
            To = tooLong ? null : to,
            ValuesOmitted = tooLong,
            DefinitionCode = definitionCode
        };
    }

    // A DATE, not an instant: "moved from the 15th to the 20th" is the change a reader means, and a time zone
    // suffix in a history row is noise nobody asked for.
    private static string? Date(DateTimeOffset? value)
        => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string? Id(Guid? value) => value?.ToString();

    // Invariant, because this string is DATA rather than presentation — the screen formats it for the reader.
    private static string? Number(decimal? value)
        => value?.ToString(CultureInfo.InvariantCulture);

    /*
     * Tags compare as a SET, not as a list. Reordering the same three tags is not an edit, and recording it as
     * one would teach readers to ignore the row that says a tag was actually added.
     */
    private static string? Tags(IReadOnlyList<string>? value)
        => value is null or { Count: 0 }
            ? null
            : string.Join(", ", value.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
}
