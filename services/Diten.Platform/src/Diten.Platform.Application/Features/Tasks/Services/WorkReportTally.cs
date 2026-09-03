using Diten.Platform.Domain.Enums.Tasks;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// The thirteen fields the report needs from a task, and nothing else.
///
/// <para>Declared here rather than inside the Mongo repository so the ARITHMETIC can be tested without a
/// database. A report whose sums are only exercised against a live Mongo is a report whose sums are only
/// exercised when Mongo is up — and this repository's Mongo-backed suites are the flaky ones.</para>
/// </summary>
public sealed record WorkReportRow(
    Guid Id,
    Guid? TaskTypeId,
    /// <summary>
    /// The task's own unit. <c>required Guid</c> on the entity — a task always names one, though the unit it
    /// names may no longer resolve.
    /// </summary>
    Guid OrganizationUnitId,
    Guid? AssigneeUserId,
    Guid? CreatedByUserId,
    Guid? PoolPositionId,
    TaskPriority Priority,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? CancelledAt,
    DateTimeOffset? DueAt,
    decimal? EstimateHours,
    decimal SpentHours,
    string? ClosureReasonCode,
    TaskLifecycle Lifecycle,
    /// <summary>
    /// The company, DERIVED from the unit by the repository — a task carries none of its own.
    ///
    /// <para>Null when the unit could not be resolved against the tenant's live org tree. That row is counted
    /// under <see cref="WorkReportDto.UnassignedKey"/> rather than dropped, so the groups still add up.</para>
    /// </summary>
    Guid? LegalEntityId = null,
    /// <summary>The type's code, carried so the filter can match on the CODE a person actually types.</summary>
    string? TaskTypeCode = null);

/// <summary>
/// WHAT THE NUMBERS ARE — pure, and shared by the database path and the tests.
///
/// <para><b>Why the tally is here and the query is not.</b> The expensive half of a report is the SCAN, and that
/// stays in the database: the Mongo repository matches the period and the scope, projects to
/// <see cref="WorkReportRow"/>, counts the unattended backlog and histograms the returns, all server-side. What
/// comes back is already one period of one scope. Turning seven conditional accumulations into one <c>$group</c>
/// would buy nothing on a set that small and cost a pipeline nobody can read — so the last step is here, where
/// it can be verified against hand-computed expectations.</para>
///
/// <para><b>⚠ THE BOUND IS THE CRITERIA'S REQUIRED PERIOD.</b> That is what keeps this list small. If the period
/// ever stops being required, this tally is what has to move into the pipeline first.</para>
/// </summary>
public static class WorkReportTally
{
    /// <summary>
    /// Whether a row survives the reader's FILTER — asked only of rows the SCOPE has already admitted.
    ///
    /// <para>⚠ In production that admission happens in the DATABASE, as the scope terms
    /// <c>WorkReportRepository.BuildMatchFilter</c> ANDs into the query — never in memory here. This method sees
    /// only rows Mongo already returned, which is why it can narrow and can never widen.</para>
    ///
    /// <para><b>⚠ THE ORDER IS THE WHOLE RULE, AND IT IS ONE-WAY.</b> The scope narrows first; this narrows
    /// further; nothing here can widen anything. Naming a person outside the caller's scope produces an EMPTY
    /// report rather than that person's work — because the row was already gone before this method saw it. A
    /// filter evaluated first, or evaluated INSTEAD, would turn a query string into a way to read other
    /// people's work.</para>
    ///
    /// <para>An absent filter matches everything, so the unfiltered report is bit-for-bit the one that existed
    /// before filters did.</para>
    /// </summary>
    public static bool MatchesFilter(WorkReportFilter? filter, WorkReportRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (filter is null || filter.IsEmpty)
        {
            return true;
        }

        /*
         * A company filter against a row whose unit did not resolve is a NO. The row's company is genuinely
         * unknown, and answering "yes" would attribute unattributable work to whichever company was asked
         * about — the one direction a report must never guess in.
         */
        if (filter.LegalEntityId is { } company && row.LegalEntityId != company)
        {
            return false;
        }

        // EXACT, not a subtree: the scope already carries the resolver's pre-expanded tree, and a second
        // expansion here would be a second answer to "what is below me".
        if (filter.OrganizationUnitId is { } unit && row.OrganizationUnitId != unit)
        {
            return false;
        }

        if (filter.AssigneeUserId is { } assignee && row.AssigneeUserId != assignee)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.TaskTypeCode)
            && !string.Equals(row.TaskTypeCode, filter.TaskTypeCode.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return filter.Priority is not { } priority || row.Priority == priority;
    }

    /// <summary>Whether the row was touched in the period — opened in it, or closed in it.</summary>
    public static bool TouchedInPeriod(WorkReportRow row, DateTimeOffset from, DateTimeOffset to) =>
        In(row.CreatedAt, from, to) || In(row.CompletedAt, from, to) || In(row.CancelledAt, from, to);

    /// <summary>The whole answer, from rows already narrowed to one period and one scope.</summary>
    /// <param name="labels">
    /// Group key → the words for it, supplied by the repository from the data it owns (type names, unit names,
    /// company names). A key absent from the map keeps a NULL label and the screen shows the identity — see
    /// <see cref="WorkReportBucket"/> for why nothing is invented to fill the gap. The ASSIGNEE axis is always
    /// absent here: Platform has no user entity to ask.
    /// </param>
    public static WorkReportDto Build(
        WorkReportCriteria criteria,
        IReadOnlyList<WorkReportRow> rows,
        int unattended,
        IReadOnlyDictionary<Guid, int> returnsByTask,
        IReadOnlyDictionary<string, string>? labels = null,
        IReadOnlyList<WorkReportRow>? openAtPeriodEnd = null)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(returnsByTask);

        /*
         * ⚠ ITS OWN ROW SET, AND IT HAS TO BE. `rows` is the work TOUCHED in the period — opened or closed
         * inside it. A task raised last year and still untouched appears in NEITHER, and it is exactly the task
         * ageing exists to surface. The repository fetches these separately; a caller that passes none gets
         * zeroes rather than a wrong answer derived from the wrong set.
         */
        var open = openAtPeriodEnd ?? [];

        string? LabelFor(string? key) =>
            key is not null && labels is not null && labels.TryGetValue(key, out var found) ? found : null;

        var totals = Measure(
            null, null, rows, unattended, returnsByTask, criteria, AgeOpenWork(open, criteria.To));

        var groups = new List<WorkReportBucket>();
        var truncated = 0;

        if (criteria.GroupBy != WorkReportGroupBy.None)
        {
            /*
             * ⚠ DETERMINISTIC ORDER, WHICH THERE WAS NONE OF. Before this slice the groups came back in whatever
             * order the grouping produced — so two reads of the same period could disagree about which unit came
             * first, and a capped list would have kept an arbitrary fifty.
             *
             * Busiest first (by OPENED, the axis a reader scans for), ties broken on the key so the order is
             * total rather than merely mostly-defined.
             */
            var ordered = rows
                .GroupBy(row => GroupKey(row, criteria.GroupBy))
                .Select(group => new
                {
                    group.Key,
                    // Unattended is a tenant-level "right now" figure and is NOT split across groups:
                    // attributing today's unclaimed backlog to every group would multiply one backlog by the
                    // number of rows on screen.
                    Bucket = Measure(
                        group.Key, LabelFor(group.Key), group.ToList(), 0, returnsByTask, criteria,
                        // Ageing is split on the SAME axis, so a unit's backlog sits beside its own flow.
                        AgeOpenWork(
                            open.Where(row => GroupKey(row, criteria.GroupBy) == group.Key), criteria.To))
                })
                .OrderByDescending(entry => entry.Bucket.Flow.Opened)
                .ThenBy(entry => entry.Key, StringComparer.Ordinal)
                .ToList();

            groups.AddRange(ordered.Take(WorkReportDto.MaxGroups).Select(entry => entry.Bucket));

            /*
             * ⚠ FOLDED, NOT DROPPED — and the count is reported. A silent cut would leave a reader comparing
             * fifty units and quietly missing the rest, with the parts no longer adding up to the whole. The
             * tail is re-measured as ONE bucket from its own rows, so every number in it is real rather than a
             * sum of pre-computed averages (an average of averages is not an average).
             */
            var tail = ordered.Skip(WorkReportDto.MaxGroups).Select(entry => entry.Key).ToHashSet(StringComparer.Ordinal);
            if (tail.Count > 0)
            {
                truncated = tail.Count;
                var tailRows = rows.Where(row => tail.Contains(GroupKey(row, criteria.GroupBy))).ToList();
                // The "other" bucket is named by the SCREEN, in the reader's language — the server has no
                // sentence for it that would survive seven translations.
                groups.Add(Measure(
                    WorkReportDto.OtherKey, null, tailRows, 0, returnsByTask, criteria,
                    AgeOpenWork(open.Where(row => tail.Contains(GroupKey(row, criteria.GroupBy))), criteria.To)));
            }
        }

        return new WorkReportDto(
            criteria.From,
            criteria.To,
            criteria.Scope.TenantWide ? WorkReportDto.ScopeTenant : WorkReportDto.ScopeScoped,
            criteria.GroupBy,
            totals,
            groups,
            truncated);
    }

    private static string GroupKey(WorkReportRow row, WorkReportGroupBy groupBy) => groupBy switch
    {
        /*
         * "" is the honest key for "this row names none" — a task with no type, or one nobody is holding.
         * Dropping such rows would make the groups add up to less than the totals with nothing on screen to
         * explain the difference, which is how a report loses a reader's trust permanently.
         */
        WorkReportGroupBy.TaskType => row.TaskTypeId?.ToString() ?? string.Empty,
        WorkReportGroupBy.OrganizationUnit => row.OrganizationUnitId.ToString(),
        WorkReportGroupBy.Assignee => row.AssigneeUserId?.ToString() ?? string.Empty,
        WorkReportGroupBy.Priority => row.Priority.ToString(),
        /*
         * The DERIVED axis. A row whose unit did not resolve has no company to name — it goes to the reserved
         * "unassigned" key rather than to "", so the screen can say WHY that bucket exists instead of showing a
         * nameless one beside four named ones.
         */
        WorkReportGroupBy.LegalEntity => row.LegalEntityId?.ToString() ?? WorkReportDto.UnassignedKey,
        _ => string.Empty
    };

    private static WorkReportBucket Measure(
        string? key,
        string? label,
        IReadOnlyList<WorkReportRow> rows,
        int unattended,
        IReadOnlyDictionary<Guid, int> returnsByTask,
        WorkReportCriteria criteria,
        WorkReportAging aging)
    {
        var from = criteria.From;
        var to = criteria.To;

        var opened = rows.Count(row => In(row.CreatedAt, from, to));
        var completed = rows.Count(row => In(row.CompletedAt, from, to));
        var cancelled = rows.Count(row => In(row.CancelledAt, from, to));

        /*
         * WHICHEVER timestamp the closure actually wrote. The two are mutually exclusive by construction:
         * TaskLifecycleService.CanTransition refuses every transition out of a terminal state, so no task can
         * carry both a CompletedAt and a CancelledAt.
         */
        var closed = rows
            .Where(row => In(row.CompletedAt, from, to) || In(row.CancelledAt, from, to))
            .Select(row => (Row: row, At: (row.CompletedAt ?? row.CancelledAt)!.Value))
            .ToList();

        /*
         * ⚠ K-1 — COMPLETIONS AND CANCELLATIONS ARE MEASURED APART.
         *
         * MEASURED 2026-09-04: these were one number. A task that waited ninety days and was then abandoned was
         * reported as ninety days of "how long our work takes", which is not what anybody reads that figure to
         * mean. Oracle measures cycle time over completions alone; the cancellation span is kept because "how
         * long before we admitted this wasn't happening" is its own finding, not noise to discard.
         */
        var completedSpans = Spans(closed.Where(pair => In(pair.Row.CompletedAt, from, to)));
        var cancelledSpans = Spans(closed.Where(pair => In(pair.Row.CancelledAt, from, to)));

        var withDue = closed.Where(pair => pair.Row.DueAt is not null).ToList();

        // Both halves present, or the comparison is meaningless: an estimate nobody worked against, or work
        // nobody estimated, says nothing about whether the plan held.
        var effort = rows.Where(row => row.EstimateHours is not null && row.SpentHours > 0m).ToList();

        var outcomes = rows
            .Where(row => !string.IsNullOrWhiteSpace(row.ClosureReasonCode))
            .GroupBy(row => row.ClosureReasonCode!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new WorkReportOutcomeCount(group.Key, group.Count()))
            .ToList();

        var returns = rows
            .Select(row => returnsByTask.TryGetValue(row.Id, out var count) ? count : 0)
            .Where(count => count > 0)
            .ToList();

        return new WorkReportBucket(
            key,
            label,
            new WorkReportFlow(opened, completed + cancelled, completed, cancelled, unattended),
            Duration(completedSpans),
            Duration(cancelledSpans),
            aging,
            new WorkReportTimeliness(
                withDue.Count(pair => pair.At <= pair.Row.DueAt!.Value),
                withDue.Count(pair => pair.At > pair.Row.DueAt!.Value),
                closed.Count - withDue.Count),
            new WorkReportEffort(
                effort.Sum(row => row.EstimateHours ?? 0m),
                effort.Sum(row => row.SpentHours),
                effort.Count),
            outcomes,
            new WorkReportRework(returns.Count, returns.Sum()));
    }


    /// <summary>
    /// The day-spans of a set of closures, corrupt rows dropped.
    ///
    /// <para>A NEGATIVE span means a closure stamped before its creation — corrupt, not fast. Excluded rather
    /// than clamped to zero, because a zero silently drags the average toward a number that reads as a very
    /// efficient team.</para>
    /// </summary>
    private static List<double> Spans(IEnumerable<(WorkReportRow Row, DateTimeOffset At)> closures) =>
        closures
            .Select(pair => (pair.At - pair.Row.CreatedAt).TotalDays)
            .Where(days => days >= 0)
            .ToList();

    /// <summary>
    /// Average, median and the denominator BOTH were computed over.
    ///
    /// <para><b>⚠ K-2 — THE COUNT IS THE REAL DENOMINATOR.</b> MEASURED 2026-09-04: the report printed
    /// <c>closed.Count</c> beside an average taken over the negative-filtered list, so the two disagreed
    /// whenever a corrupt row existed and the reader could not tell. Whatever the numbers were computed from is
    /// what gets reported.</para>
    ///
    /// <para><b>The MEDIAN uses the standard definition:</b> the middle value of the sorted list, or the mean
    /// of the middle TWO when the count is even. Stated rather than assumed because "the median of an even
    /// list" is exactly the sort of thing two implementations quietly disagree about — one taking the lower
    /// middle, the other the mean — and the difference shows up as a number nobody can reproduce.</para>
    /// </summary>
    private static WorkReportDuration Duration(IReadOnlyList<double> spans)
    {
        if (spans.Count == 0)
        {
            // Absent, not zero: a zero reads as "everything closed instantly", the most flattering lie a report
            // can tell. The count beside it says why there is nothing to average.
            return new WorkReportDuration(null, null, 0);
        }

        var sorted = spans.OrderBy(value => value).ToList();
        var middle = sorted.Count / 2;
        var median = sorted.Count % 2 == 1
            ? sorted[middle]
            : (sorted[middle - 1] + sorted[middle]) / 2d;

        return new WorkReportDuration(
            Math.Round(sorted.Average(), 2),
            Math.Round(median, 2),
            sorted.Count);
    }

    /// <summary>
    /// Open work at the END OF THE PERIOD, bucketed by age.
    ///
    /// <para><b>⚠ ANCHORED TO <paramref name="periodEnd"/>, NEVER TO THE CURRENT CLOCK.</b> A report is
    /// evidence: it is opened again in a review months later, beside a decision somebody already took. Ageing
    /// measured from "now" answers differently every time the page loads, so the same period stops matching the
    /// copy that was printed. This function takes no clock and reads none — that is what makes the answer
    /// repeatable, and a test calls it twice to prove it.</para>
    ///
    /// <para>A row is open at that instant when it was created before it and had not closed by then. Work with
    /// no deadline is included: it is exactly the work the timeliness measure cannot speak about.</para>
    /// </summary>
    public static WorkReportAging AgeOpenWork(IEnumerable<WorkReportRow> openAtPeriodEnd, DateTimeOffset periodEnd)
    {
        ArgumentNullException.ThrowIfNull(openAtPeriodEnd);

        int upTo7 = 0, from8To30 = 0, older = 0;

        foreach (var row in openAtPeriodEnd)
        {
            var days = (periodEnd - row.CreatedAt).TotalDays;
            if (days <= 7)
            {
                upTo7++;
            }
            else if (days <= 30)
            {
                from8To30++;
            }
            else
            {
                older++;
            }
        }

        return new WorkReportAging(upTo7, from8To30, older);
    }

    /// <summary>Whether a row was still open at <paramref name="instant"/> — created before it, closed after it or not at all.</summary>
    public static bool OpenAt(WorkReportRow row, DateTimeOffset instant)
    {
        ArgumentNullException.ThrowIfNull(row);

        return row.CreatedAt < instant
            && (row.CompletedAt is null || row.CompletedAt >= instant)
            && (row.CancelledAt is null || row.CancelledAt >= instant);
    }

    private static bool In(DateTimeOffset? at, DateTimeOffset from, DateTimeOffset to) =>
        at is { } value && In(value, from, to);

    // Half-open [from, to), so consecutive periods cannot both claim the same instant.
    private static bool In(DateTimeOffset at, DateTimeOffset from, DateTimeOffset to) => at >= from && at < to;
}
