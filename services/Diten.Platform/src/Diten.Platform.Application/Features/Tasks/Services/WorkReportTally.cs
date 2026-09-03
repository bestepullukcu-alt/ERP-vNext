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
    TaskLifecycle Lifecycle);

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
    /// Whether a row is inside a scope — the SAME question the Mongo filter asks, in the form the tests can ask
    /// it too.
    ///
    /// <para>⚠ It exists so "out of scope" can be tested against real counting rather than against an empty
    /// result set. An assertion that a foreign row is absent from a report that counted nothing measures
    /// nothing at all.</para>
    /// </summary>
    public static bool InScope(WorkReportScope scope, WorkReportRow row)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(row);

        if (scope.TenantWide)
        {
            return true;
        }

        // Fail-closed: an empty scope matches nothing. An OR over zero branches is TRUE in every query language
        // there is, which is precisely the accident this guard exists to prevent.
        if (scope.MatchesNothing)
        {
            return false;
        }

        return scope.OrganizationUnitIds.Contains(row.OrganizationUnitId)
            || (row.PoolPositionId is { } pool && scope.PositionIds.Contains(pool))
            || (row.AssigneeUserId is { } assignee && scope.UserIds.Contains(assignee))
            || (row.CreatedByUserId is { } requester && scope.UserIds.Contains(requester));
    }

    /// <summary>Whether the row was touched in the period — opened in it, or closed in it.</summary>
    public static bool TouchedInPeriod(WorkReportRow row, DateTimeOffset from, DateTimeOffset to) =>
        In(row.CreatedAt, from, to) || In(row.CompletedAt, from, to) || In(row.CancelledAt, from, to);

    /// <summary>The whole answer, from rows already narrowed to one period and one scope.</summary>
    public static WorkReportDto Build(
        WorkReportCriteria criteria,
        IReadOnlyList<WorkReportRow> rows,
        int unattended,
        IReadOnlyDictionary<Guid, int> returnsByTask)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(returnsByTask);

        var totals = Measure(null, rows, unattended, returnsByTask, criteria);

        var groups = criteria.GroupBy == WorkReportGroupBy.None
            ? []
            : rows
                .GroupBy(row => GroupKey(row, criteria.GroupBy))
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                /*
                 * Unattended is a tenant-level "right now" figure, so it is NOT split across groups: attributing
                 * today's unclaimed backlog to whichever month is on screen would double-count it across every
                 * group and misattribute all of it.
                 */
                .Select(group => Measure(group.Key, group.ToList(), 0, returnsByTask, criteria))
                .ToList();

        return new WorkReportDto(
            criteria.From,
            criteria.To,
            criteria.Scope.TenantWide ? WorkReportDto.ScopeTenant : WorkReportDto.ScopeScoped,
            criteria.GroupBy,
            totals,
            groups);
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
        _ => string.Empty
    };

    private static WorkReportBucket Measure(
        string? key,
        IReadOnlyList<WorkReportRow> rows,
        int unattended,
        IReadOnlyDictionary<Guid, int> returnsByTask,
        WorkReportCriteria criteria)
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
            .Select(row => new { Row = row, At = (row.CompletedAt ?? row.CancelledAt)!.Value })
            .ToList();

        var cycleDays = closed
            .Select(pair => (pair.At - pair.Row.CreatedAt).TotalDays)
            /*
             * A negative span means a closure stamped before its creation — corrupt, not fast. Excluded rather
             * than clamped to zero, because a zero silently drags the average toward a number that reads as a
             * very efficient team.
             */
            .Where(days => days >= 0)
            .ToList();

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
            new WorkReportFlow(opened, completed + cancelled, completed, cancelled, unattended),
            new WorkReportCycleTime(
                cycleDays.Count == 0 ? null : Math.Round(cycleDays.Average(), 2),
                closed.Count),
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

    private static bool In(DateTimeOffset? at, DateTimeOffset from, DateTimeOffset to) =>
        at is { } value && In(value, from, to);

    // Half-open [from, to), so consecutive periods cannot both claim the same instant.
    private static bool In(DateTimeOffset at, DateTimeOffset from, DateTimeOffset to) => at >= from && at < to;
}
