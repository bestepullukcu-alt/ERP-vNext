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
/// EVERYTHING ONE REPORT WAS COMPUTED FROM — the three row sets and the return histogram, in one object.
///
/// <para><b>⚠ IT EXISTS SO A NUMBER AND ITS LIST CANNOT DRIFT (Dilim 1c).</b> Before it, the repository handed
/// the tally three loose arguments and kept the queries to itself; a list endpoint written beside that would
/// have had to REBUILD the same sets, and two hand-written queries agree only on the day they are written. Now
/// there is one object, produced once by <c>WorkReportRepository.RowSetAsync</c>, and both the counting and the
/// listing read it through <see cref="WorkReportTally.Select"/>.</para>
///
/// <para><b>Why three sets and not one.</b> They answer three different questions and no filter turns any of
/// them into another: <see cref="Touched"/> is the work the PERIOD saw; <see cref="OpenAtPeriodEnd"/> is what
/// was still waiting when the period ended — a task raised last year and never touched appears in the first set
/// not at all, and it is exactly what ageing exists to surface; <see cref="Unattended"/> is what nobody is
/// holding RIGHT NOW, which is a question about today rather than about the period.</para>
/// </summary>
/// <param name="Touched">Work created, completed or cancelled inside the period.</param>
/// <param name="OpenAtPeriodEnd">Work created before the period ended and not closed by then.</param>
/// <param name="Unattended">Open work with no assignee, as of now.</param>
/// <param name="ReturnsByTask">How many times each touched task was returned (Faz 4).</param>
public sealed record WorkReportRowSet(
    IReadOnlyList<WorkReportRow> Touched,
    IReadOnlyList<WorkReportRow> OpenAtPeriodEnd,
    IReadOnlyList<WorkReportRow> Unattended,
    IReadOnlyDictionary<Guid, int> ReturnsByTask)
{
    public static WorkReportRowSet Empty { get; } =
        new([], [], [], new Dictionary<Guid, int>());

    /// <summary>
    /// The same sets, narrowed to one group of the breakdown.
    ///
    /// <para><b>⚠ <see cref="Unattended"/> IS DELIBERATELY EMPTIED, not narrowed.</b> It is a tenant-level
    /// "right now" figure; splitting today's unclaimed backlog across the groups would attribute one backlog to
    /// every row on screen. The totals row is the only place it is reported, and the group buckets have carried
    /// a zero there since Faz 5a — this keeps the LIST honest about the same thing.</para>
    /// </summary>
    public WorkReportRowSet ForGroup(Func<WorkReportRow, bool> inGroup)
    {
        ArgumentNullException.ThrowIfNull(inGroup);

        return new WorkReportRowSet(
            Touched.Where(inGroup).ToList(),
            OpenAtPeriodEnd.Where(inGroup).ToList(),
            [],
            ReturnsByTask);
    }
}

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
        WorkReportRowSet set,
        IReadOnlyDictionary<string, string>? labels = null)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(set);

        var rows = set.Touched;

        string? LabelFor(string? key) =>
            key is not null && labels is not null && labels.TryGetValue(key, out var found) ? found : null;

        var totals = Measure(null, null, set, criteria);

        var groups = new List<WorkReportBucket>();
        var truncated = 0;

        if (criteria.GroupBy != WorkReportGroupBy.None)
        {
            var ordered = OrderedGroupKeys(criteria, set);

            groups.AddRange(ordered
                .Take(WorkReportDto.MaxGroups)
                .Select(key => Measure(key, LabelFor(key), GroupSet(criteria, set, key), criteria)));

            /*
             * ⚠ FOLDED, NOT DROPPED — and the count is reported. A silent cut would leave a reader comparing
             * fifty units and quietly missing the rest, with the parts no longer adding up to the whole. The
             * tail is re-measured as ONE bucket from its own rows, so every number in it is real rather than a
             * sum of pre-computed averages (an average of averages is not an average).
             */
            truncated = Math.Max(0, ordered.Count - WorkReportDto.MaxGroups);
            if (truncated > 0)
            {
                // The "other" bucket is named by the SCREEN, in the reader's language — the server has no
                // sentence for it that would survive seven translations.
                groups.Add(Measure(
                    WorkReportDto.OtherKey,
                    null,
                    GroupSet(criteria, set, WorkReportDto.OtherKey),
                    criteria));
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

    /// <summary>
    /// EVERY GROUP KEY ON THE AXIS, in the order the cap is applied in — busiest first.
    ///
    /// <para><b>⚠ ONE DEFINITION OF "WHICH FIFTY SURVIVED" (Dilim 1c).</b> The chart draws the first fifty and
    /// folds the rest into <see cref="WorkReportDto.OtherKey"/>; a click on that folded bar has to open exactly
    /// the rows that were folded. If the list worked the ordering out separately, one edit to the sort would
    /// silently make the "all other groups" list a different set from the "all other groups" bar.</para>
    ///
    /// <para><b>Deterministic, and it was not always.</b> Before Dilim 1a the groups came back in whatever order
    /// the grouping produced, so two reads of the same period could disagree about which unit came first and a
    /// capped list would have kept an arbitrary fifty. Busiest first by OPENED — the axis a reader scans for —
    /// with ties broken on the key so the order is total rather than merely mostly-defined.</para>
    /// </summary>
    private static List<string> OrderedGroupKeys(WorkReportCriteria criteria, WorkReportRowSet set) =>
        set.Touched
            .GroupBy(row => GroupKey(row, criteria.GroupBy))
            .Select(group => new
            {
                group.Key,
                Opened = group.Count(row => In(row.CreatedAt, criteria.From, criteria.To))
            })
            .OrderByDescending(entry => entry.Opened)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => entry.Key)
            .ToList();

    /// <summary>
    /// The rows behind ONE row of the breakdown — the totals' set narrowed to that group.
    ///
    /// <para><b>⚠ THIS IS WHAT MAKES A GROUP BAR CLICKABLE (Dilim 1c).</b> The bucket the chart drew was
    /// measured from exactly this set, so a list built from it has, cell by cell, the number of rows the bar
    /// reported. <see cref="WorkReportDto.OtherKey"/> resolves through <see cref="OrderedGroupKeys"/> to the
    /// tail the cap folded away, which is the only key whose membership is not a property of a single row.</para>
    /// </summary>
    public static WorkReportRowSet RestrictToGroup(
        WorkReportCriteria criteria,
        WorkReportRowSet set,
        string groupKey)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(set);

        return GroupSet(criteria, set, groupKey);
    }

    private static WorkReportRowSet GroupSet(
        WorkReportCriteria criteria,
        WorkReportRowSet set,
        string groupKey)
    {
        if (!string.Equals(groupKey, WorkReportDto.OtherKey, StringComparison.Ordinal))
        {
            return set.ForGroup(row => GroupKey(row, criteria.GroupBy) == groupKey);
        }

        // The folded tail: everything the cap pushed past fifty, resolved through the SAME ordering the chart
        // drew from rather than through a second idea of which groups were busiest.
        var tail = OrderedGroupKeys(criteria, set)
            .Skip(WorkReportDto.MaxGroups)
            .ToHashSet(StringComparer.Ordinal);

        return set.ForGroup(row => tail.Contains(GroupKey(row, criteria.GroupBy)));
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

    /// <summary>
    /// ⚠ EVERY COUNT BELOW IS <see cref="Select"/>.<c>Count</c> — that is the point of this method's shape.
    ///
    /// <para>Dilim 1c made each of these numbers clickable, and the acceptance criterion was that the list a
    /// click opens has exactly as many rows as the number said. The only way to keep that true through later
    /// edits is for the count and the list to BE the same code, so <c>Measure</c> no longer has predicates of
    /// its own: it asks <see cref="Select"/> for each cell and counts what comes back. A change to who belongs
    /// in a cell now moves the number and the list together, because there is nothing else to change.</para>
    ///
    /// <para>The two that are not counts — the durations and the effort variance — are computed from the rows
    /// <see cref="Select"/> returns rather than from a fourth, private notion of "closed".</para>
    /// </summary>
    private static WorkReportBucket Measure(
        string? key,
        string? label,
        WorkReportRowSet set,
        WorkReportCriteria criteria)
    {
        IReadOnlyList<WorkReportRow> Cell(WorkReportBucketKind kind, string? argument = null) =>
            Select(criteria, set, kind, argument);

        var completed = Cell(WorkReportBucketKind.Completed);
        var cancelled = Cell(WorkReportBucketKind.Cancelled);

        /*
         * ⚠ K-1 — COMPLETIONS AND CANCELLATIONS ARE MEASURED APART (Dilim 1b).
         *
         * A task that waited ninety days and was then abandoned used to be reported as ninety days of "how long
         * our work takes", which is not what anybody reads that figure to mean. Oracle measures cycle time over
         * completions alone; the cancellation span is kept because "how long before we admitted this wasn't
         * happening" is its own finding, not noise to discard.
         */
        var completedSpans = Spans(completed.Select(row => (Row: row, At: row.CompletedAt!.Value)));
        var cancelledSpans = Spans(cancelled.Select(row => (Row: row, At: row.CancelledAt!.Value)));

        // Both halves present, or the comparison is meaningless: an estimate nobody worked against, or work
        // nobody estimated, says nothing about whether the plan held.
        var effort = set.Touched.Where(row => row.EstimateHours is not null && row.SpentHours > 0m).ToList();

        /*
         * The outcome CODES present, then each one's cell — rather than one grouping pass. A grouping would be
         * a second definition of "which tasks carry this outcome", and the donut's slices would then be able to
         * disagree with the list a slice opens.
         */
        var outcomes = set.Touched
            .Where(row => !string.IsNullOrWhiteSpace(row.ClosureReasonCode))
            .Select(row => row.ClosureReasonCode!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(code => new WorkReportOutcomeCount(code, Cell(WorkReportBucketKind.Outcome, code).Count))
            .OrderByDescending(outcome => outcome.Count)
            .ThenBy(outcome => outcome.Code, StringComparer.Ordinal)
            .ToList();

        var returned = Cell(WorkReportBucketKind.Returned);

        return new WorkReportBucket(
            key,
            label,
            new WorkReportFlow(
                Cell(WorkReportBucketKind.Opened).Count,
                Cell(WorkReportBucketKind.Closed).Count,
                completed.Count,
                cancelled.Count,
                Cell(WorkReportBucketKind.Unattended).Count),
            Duration(completedSpans),
            Duration(cancelledSpans),
            new WorkReportAging(
                Cell(WorkReportBucketKind.AgingUpTo7Days).Count,
                Cell(WorkReportBucketKind.AgingFrom8To30Days).Count,
                Cell(WorkReportBucketKind.AgingOlderThan30Days).Count),
            new WorkReportTimeliness(
                Cell(WorkReportBucketKind.OnTime).Count,
                Cell(WorkReportBucketKind.Late).Count,
                Cell(WorkReportBucketKind.WithoutDueDate).Count),
            new WorkReportEffort(
                effort.Sum(row => row.EstimateHours ?? 0m),
                effort.Sum(row => row.SpentHours),
                effort.Count),
            outcomes,
            new WorkReportRework(
                returned.Count,
                returned.Sum(row => set.ReturnsByTask.TryGetValue(row.Id, out var count) ? count : 0)));
    }

    /// <summary>
    /// THE WORK BEHIND ONE CELL — the single definition of who belongs in each of the report's numbers.
    ///
    /// <para><b>⚠ THIS IS BOTH THE COUNTER AND THE LISTER.</b> <c>Measure</c> calls it and takes
    /// <c>.Count</c>; the items endpoint calls it and takes a page. There is no second predicate anywhere, which
    /// is what makes "the list has as many rows as the number said" a property of the code rather than a
    /// coincidence two tests happen to agree on.</para>
    ///
    /// <para><b>It cannot widen anything.</b> Every branch reads one of the three sets in
    /// <paramref name="set"/>, and those were produced by the report's own scoped, filtered queries. There is no
    /// argument to this method that reaches a row the report did not already count — a click is a way to SEE a
    /// number's contents, never a way to ask a different question.</para>
    ///
    /// <para><b>Ordering is total and deterministic:</b> newest first, ties broken on the id. A page of an
    /// unordered set is an arbitrary page, and pressing "more" on one would show rows the first page had
    /// already shown while hiding others entirely.</para>
    /// </summary>
    /// <param name="argument">The outcome code, for <see cref="WorkReportBucketKind.Outcome"/>. Ignored by every
    /// other kind; an Outcome cell asked without one lists NOTHING rather than listing every outcome at once.</param>
    public static IReadOnlyList<WorkReportRow> Select(
        WorkReportCriteria criteria,
        WorkReportRowSet set,
        WorkReportBucketKind kind,
        string? argument = null)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(set);

        var from = criteria.From;
        var to = criteria.To;

        /*
         * WHICHEVER terminal timestamp the closure actually wrote. The two are mutually exclusive by
         * construction — TaskLifecycleService.CanTransition refuses every transition out of a terminal state —
         * so no task can carry both a CompletedAt and a CancelledAt.
         */
        static DateTimeOffset ClosedAt(WorkReportRow row) => (row.CompletedAt ?? row.CancelledAt)!.Value;

        IEnumerable<WorkReportRow> closedInPeriod = set.Touched
            .Where(row => In(row.CompletedAt, from, to) || In(row.CancelledAt, from, to));

        IEnumerable<WorkReportRow> chosen = kind switch
        {
            WorkReportBucketKind.Opened => set.Touched.Where(row => In(row.CreatedAt, from, to)),
            WorkReportBucketKind.Closed => closedInPeriod,
            WorkReportBucketKind.Completed => set.Touched.Where(row => In(row.CompletedAt, from, to)),
            WorkReportBucketKind.Cancelled => set.Touched.Where(row => In(row.CancelledAt, from, to)),

            // As of NOW, not over the period — the list inherits the number's question. See WorkReportFlow.
            WorkReportBucketKind.Unattended => set.Unattended,

            WorkReportBucketKind.OnTime =>
                closedInPeriod.Where(row => row.DueAt is { } due && ClosedAt(row) <= due),
            WorkReportBucketKind.Late =>
                closedInPeriod.Where(row => row.DueAt is { } due && ClosedAt(row) > due),
            // Work nobody set a date for was not early. It is reported apart for that reason, and it is
            // clickable because it is precisely what the punctuality figure cannot speak about.
            WorkReportBucketKind.WithoutDueDate => closedInPeriod.Where(row => row.DueAt is null),

            WorkReportBucketKind.AgingUpTo7Days =>
                set.OpenAtPeriodEnd.Where(row => AgeBucket(row, to) == 0),
            WorkReportBucketKind.AgingFrom8To30Days =>
                set.OpenAtPeriodEnd.Where(row => AgeBucket(row, to) == 1),
            WorkReportBucketKind.AgingOlderThan30Days =>
                set.OpenAtPeriodEnd.Where(row => AgeBucket(row, to) == 2),

            WorkReportBucketKind.Returned =>
                set.Touched.Where(row => set.ReturnsByTask.TryGetValue(row.Id, out var count) && count > 0),

            /*
             * ⚠ MEASURED 2026-09-05, and the list inherits it rather than correcting it: this asks whether the
             * row CARRIES the code, not whether the closure happened inside the period. A task created in the
             * period and closed after it therefore appears in the outcome chart. That is the behaviour the
             * donut has published since Faz 5a; changing it here would move a number in a slice that is about
             * clickability, and would move it without anybody having decided to. Recorded in the pack instead.
             */
            WorkReportBucketKind.Outcome => string.IsNullOrWhiteSpace(argument)
                ? []
                : set.Touched.Where(row =>
                    string.Equals(row.ClosureReasonCode, argument, StringComparison.OrdinalIgnoreCase)),

            _ => []
        };

        return chosen
            .OrderByDescending(row => row.CreatedAt)
            .ThenBy(row => row.Id)
            .ToList();
    }

    /// <summary>
    /// ONE PAGE of a cell, and the cell's OWN total — Dilim 1c.
    ///
    /// <para><b>⚠ THE TOTAL IS NOT THE PAGE'S LENGTH, and separating the two is the whole reason this is a
    /// named function rather than three lines inside the repository.</b> The number the reader clicked is
    /// <paramref name="selected"/><c>.Count</c>; what comes back is at most
    /// <see cref="WorkReportItemsDto.PageSize"/> of it. A list that reported its own length as the total would
    /// silently rewrite "83 opened" to "50 opened" the moment the cap bit — a report contradicting itself on
    /// one screen.</para>
    ///
    /// <para><b><c>HasMore</c> is computed, never assumed.</b> A silent cut leaves a reader counting fifty rows
    /// under a number that said eighty-three and concluding the report is wrong.</para>
    /// </summary>
    public static (IReadOnlyList<WorkReportRow> Rows, int Total, bool HasMore) Page(
        IReadOnlyList<WorkReportRow> selected,
        int skip)
    {
        ArgumentNullException.ThrowIfNull(selected);

        // A negative offset is a client bug, not a request to read backwards from the end.
        var from = Math.Max(0, skip);
        var rows = selected.Skip(from).Take(WorkReportItemsDto.PageSize).ToList();

        return (rows, selected.Count, from + rows.Count < selected.Count);
    }

    /// <summary>Which of the three ageing bands a row falls in at <paramref name="periodEnd"/>: 0, 1 or 2.</summary>
    private static int AgeBucket(WorkReportRow row, DateTimeOffset periodEnd)
    {
        var days = (periodEnd - row.CreatedAt).TotalDays;
        return days <= 7 ? 0 : days <= 30 ? 1 : 2;
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

        // ⚠ The boundaries live in AgeBucket alone. Repeating "<= 7" here is how the chart and the list a click
        // opens would one day disagree about which band a six-day-old task belongs to.
        foreach (var row in openAtPeriodEnd)
        {
            switch (AgeBucket(row, periodEnd))
            {
                case 0: upTo7++; break;
                case 1: from8To30++; break;
                default: older++; break;
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
