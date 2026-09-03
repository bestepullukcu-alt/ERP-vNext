using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// (d) THE NUMBERS — checked against a hand-computed data set.
///
/// <para><b>One fixture, arithmetic done on paper first.</b> Every expectation below is stated as a number a
/// person worked out from the table in <see cref="Rows"/>, not as an expression that re-derives it — a test that
/// recomputes the sum the same way the code does agrees with the code about a shared mistake.</para>
///
/// <para><b>This is the REAL tally, not a stand-in.</b> <c>WorkReportRepository</c> matches and projects in the
/// database and then calls exactly this. Extracting it is what lets the sums be verified without a live Mongo —
/// and this module's Mongo-backed suites are precisely the flaky ones, so sums verified only there would be sums
/// verified only sometimes.</para>
/// </summary>
public sealed class WorkReportTallyTests
{
    // Period: the whole of June 2026, half-open.
    private static readonly DateTimeOffset From = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly Guid UnitA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid UnitB = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid TypeX = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");

    private static readonly Guid T1 = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid T2 = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
    private static readonly Guid T3 = Guid.Parse("cccccccc-0000-0000-0000-000000000003");
    private static readonly Guid T4 = Guid.Parse("cccccccc-0000-0000-0000-000000000004");
    private static readonly Guid T5 = Guid.Parse("cccccccc-0000-0000-0000-000000000005");

    private static DateTimeOffset June(int day, int hour = 0) => new(2026, 6, day, hour, 0, 0, TimeSpan.Zero);

    /*
     * THE FIXTURE, worked out on paper.
     *
     *  id │ unit │ type  │ created │ completed │ cancelled │ due     │ est │ spent │ outcome
     * ────┼──────┼───────┼─────────┼───────────┼───────────┼─────────┼─────┼───────┼──────────
     *  T1 │  A   │ TypeX │ Jun  1  │  Jun  5   │     -     │ Jun  6  │  8  │  10   │ RESOLVED   → on time, 4 days
     *  T2 │  A   │ TypeX │ Jun  2  │  Jun 12   │     -     │ Jun 10  │  4  │   4   │ RESOLVED   → LATE,    10 days
     *  T3 │  B   │ null  │ Jun  3  │     -     │  Jun  4   │    -    │  -  │   0   │ SUPERSEDED → no due,   1 day
     *  T4 │  B   │ TypeX │ Jun  4  │     -     │     -     │ Jun 20  │  2  │   0   │ (open)
     *  T5 │  A   │ null  │ May 20  │  Jun  9   │     -     │    -    │  -  │   0   │ RESOLVED   → no due,  20 days
     *
     * Opened in June:   T1 T2 T3 T4                       = 4   (T5 was opened in May)
     * Completed:        T1 T2 T5                          = 3
     * Cancelled:        T3                                = 1
     * Closed:           4
     * Cycle days:       4 + 10 + 1 + 20 = 35  ⇒ avg 8.75  over 4 closed
     * Timeliness:       on time T1; late T2; no due T3, T5 ⇒ 1 / 1 / 2
     * Effort (both):    T1 (8/10) and T2 (4/4)            ⇒ 12 est, 14 spent, 2 tasks
     *                   T4 has an estimate but zero spent ⇒ excluded
     * Outcomes:         RESOLVED ×3, SUPERSEDED ×1
     * Returns:          T2 twice, T3 once                 ⇒ 2 tasks, 3 returns
     */
    private static IReadOnlyList<WorkReportRow> Rows() =>
    [
        Row(T1, UnitA, TypeX, June(1), completed: June(5), due: June(6), est: 8m, spent: 10m, outcome: "RESOLVED"),
        Row(T2, UnitA, TypeX, June(2), completed: June(12), due: June(10), est: 4m, spent: 4m, outcome: "RESOLVED"),
        Row(T3, UnitB, null, June(3), cancelled: June(4), outcome: "SUPERSEDED"),
        Row(T4, UnitB, TypeX, June(4), due: June(20), est: 2m),
        Row(T5, UnitA, null, new DateTimeOffset(2026, 5, 20, 0, 0, 0, TimeSpan.Zero),
            completed: June(9), outcome: "RESOLVED")
    ];

    private static readonly IReadOnlyDictionary<Guid, int> Returns =
        new Dictionary<Guid, int> { [T2] = 2, [T3] = 1 };

    private static WorkReportRow Row(
        Guid id,
        Guid unit,
        Guid? type,
        DateTimeOffset created,
        DateTimeOffset? completed = null,
        DateTimeOffset? cancelled = null,
        DateTimeOffset? due = null,
        decimal? est = null,
        decimal spent = 0m,
        string? outcome = null,
        TaskPriority priority = TaskPriority.Medium,
        Guid? assignee = null) => new(
        id, type, unit, assignee, CreatedByUserId: null, PoolPositionId: null, priority,
        created, completed, cancelled, due, est, spent, outcome,
        completed is not null ? TaskLifecycle.Done
            : cancelled is not null ? TaskLifecycle.Cancelled
            : TaskLifecycle.Open);

    private static WorkReportCriteria Criteria(WorkReportGroupBy groupBy = WorkReportGroupBy.None) =>
        new(From, To, WorkReportScope.TenantWideScope(), groupBy);

    private static WorkReportBucket Totals(WorkReportGroupBy groupBy = WorkReportGroupBy.None, int unattended = 0) =>
        WorkReportTally.Build(Criteria(groupBy), Rows(), unattended, Returns).Totals;

    // ── Flow ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Opened_counts_only_what_was_CREATED_in_the_period()
    {
        // T5 was opened in May and closed in June: it is a June CLOSURE, never a June opening. Counting it as
        // both is how an "opened vs closed" chart stops balancing and nobody can say why.
        Assert.Equal(4, Totals().Flow.Opened);
    }

    [Fact]
    public void Closed_is_completed_plus_cancelled_and_the_two_are_reported_separately()
    {
        var flow = Totals().Flow;

        Assert.Equal(3, flow.Completed);
        Assert.Equal(1, flow.Cancelled);
        Assert.Equal(4, flow.Closed);
        // Called-off work is not finished work. A single "closed" number would let a month of cancellations
        // read as a month of delivery.
        Assert.Equal(flow.Completed + flow.Cancelled, flow.Closed);
    }

    [Fact]
    public void Unattended_is_passed_through_untouched_because_it_is_a_question_about_NOW()
    {
        // Oracle's Unattended asks how much work is sitting unclaimed today, not how much was unclaimed during
        // a window — so it is counted by its own query and never recomputed from the period's rows.
        Assert.Equal(7, Totals(unattended: 7).Flow.Unattended);
    }

    // ── Cycle time ───────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Cycle_time_measures_COMPLETIONS_ONLY_and_reports_a_median_beside_the_average()
    {
        /*
         * ⚠ THIS EXPECTATION CHANGED, AND THE CHANGE IS THE FIX (K-1).
         *
         * It used to be 8.75 over 4 — completions AND the cancellation averaged together, so a task somebody
         * abandoned counted as "how long our work takes". Completions only now: T1=4, T2=10, T5=20.
         *
         *   average = (4 + 10 + 20) / 3 = 11.33
         *   median  = middle of [4, 10, 20] = 10
         *
         * T5 spans the period boundary and still counts in full — the work took twenty days regardless of
         * which month it started in.
         */
        var cycle = Totals().CycleTime;

        Assert.Equal(11.33, cycle.AverageDays);
        Assert.Equal(10, cycle.MedianDays);
        Assert.Equal(3, cycle.Count);
    }

    [Fact]
    public void Cancellations_get_their_OWN_duration_rather_than_being_averaged_in_or_thrown_away()
    {
        /*
         * "How long before we admitted this wasn't happening" is a finding, not noise. T3 was called off after
         * one day — the only cancellation in the fixture.
         */
        var cancelled = Totals().CancellationTime;

        Assert.Equal(1, cancelled.AverageDays);
        Assert.Equal(1, cancelled.MedianDays);
        Assert.Equal(1, cancelled.Count);

        // NON-VACUITY: the two are genuinely different numbers, so this is a split and not a copy.
        Assert.NotEqual(Totals().CycleTime.AverageDays, cancelled.AverageDays);
    }

    [Fact]
    public void The_median_is_the_MIDDLE_and_the_average_is_dragged_by_the_tail()
    {
        /*
         * The reason both travel. Durations are skewed: one parked task moves a mean nobody recognises, while
         * the median says what a typical task actually took. The gap between them IS the finding.
         *
         *   1 · 2 · 3 · 4 · 90   →   average 20   ·   median 3
         */
        /*
         * ⚠ A WIDE WINDOW ON PURPOSE. June alone cannot hold a ninety-day span, and the first version of this
         * test built `June(91)` and threw — the closure has to land INSIDE the period or it is not a closure
         * the period counts, which is the very rule the flow tests pin.
         */
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var wide = new WorkReportCriteria(start, start.AddYears(1), WorkReportScope.TenantWideScope());

        var spans = new[] { 1, 2, 3, 4, 90 }
            .Select(d => Row(Guid.NewGuid(), UnitA, TypeX, start, completed: start.AddDays(d)))
            .ToArray();

        var cycle = WorkReportTally.Build(wide, spans, 0, Returns).Totals.CycleTime;

        Assert.Equal(20, cycle.AverageDays);
        Assert.Equal(3, cycle.MedianDays);
        Assert.Equal(5, cycle.Count);
    }

    [Fact]
    public void An_EVEN_number_of_durations_takes_the_mean_of_the_middle_two()
    {
        /*
         * The standard definition, stated because it is exactly what two implementations quietly disagree
         * about — one taking the lower middle, the other the mean — and the difference shows up later as a
         * number nobody can reproduce. [2, 4, 6, 10] → middle two are 4 and 6 → 5.
         */
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var wide = new WorkReportCriteria(start, start.AddYears(1), WorkReportScope.TenantWideScope());

        var spans = new[] { 2, 4, 6, 10 }
            .Select(d => Row(Guid.NewGuid(), UnitA, TypeX, start, completed: start.AddDays(d)))
            .ToArray();

        Assert.Equal(5, WorkReportTally.Build(wide, spans, 0, Returns).Totals.CycleTime.MedianDays);
    }

    [Fact]
    public void With_nothing_closed_the_average_is_ABSENT_rather_than_zero()
    {
        /*
         * A zero would read as "everything closed instantly", which is the most flattering possible lie a
         * report can tell. Null says there is nothing to average, and the count beside it says why.
         */
        var open = new[] { Row(T4, UnitB, TypeX, June(4), due: June(20)) };

        var cycle = WorkReportTally.Build(Criteria(), open, 0, Returns).Totals.CycleTime;

        Assert.Null(cycle.AverageDays);
        Assert.Null(cycle.MedianDays);
        Assert.Equal(0, cycle.Count);
    }

    [Fact]
    public void A_closure_stamped_before_its_creation_is_excluded_AND_the_count_says_so()
    {
        /*
         * Corrupt, not fast. Clamping to zero would drag the average toward a number that reads as a very
         * efficient team, so the row is dropped from the average entirely.
         *
         * ⚠ AND THE COUNT DROPS WITH IT — THIS EXPECTATION CHANGED (K-2). It used to assert 1: the report said
         * "over 1 closed" beside an average computed over nothing. The stated denominator is now the one the
         * numbers were actually computed over, because an average whose denominator is not its own cannot be
         * checked by the person reading it. The row still counts as CLOSED in the flow — that number is
         * untouched, and the two are allowed to differ precisely because each says what it measured.
         */
        var corrupt = new[] { Row(T1, UnitA, TypeX, June(10), completed: June(5)) };
        var report = WorkReportTally.Build(Criteria(), corrupt, 0, Returns);

        Assert.Null(report.Totals.CycleTime.AverageDays);
        Assert.Equal(0, report.Totals.CycleTime.Count);

        // The closure itself is not denied — only its impossible duration is.
        Assert.Equal(1, report.Totals.Flow.Closed);
    }

    // ── Timeliness ───────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Punctuality_counts_only_work_that_HAD_a_deadline()
    {
        /*
         * T1 landed before its due date, T2 after it, and T3 and T5 had none. Folding the undated pair into
         * "on time" would report 3/1 — a punctuality figure improved by nobody having set a date, which is
         * exactly backwards.
         */
        var timeliness = Totals().Timeliness;

        Assert.Equal(1, timeliness.OnTime);
        Assert.Equal(1, timeliness.Late);
        Assert.Equal(2, timeliness.WithoutDueDate);
    }

    // ── Effort ───────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Effort_sums_only_tasks_carrying_BOTH_an_estimate_and_recorded_work()
    {
        /*
         * T1 (8/10) and T2 (4/4) qualify. T4 has an estimate and no work done, so including it would report a
         * plan that came in massively under budget purely because nobody has started.
         *
         * ⚠ Hours and a task count, NEVER a ratio. Pack §8 excludes an efficiency percentage outright: a
         * per-person estimate-vs-actual score makes people inflate estimates, which corrupts the only planning
         * input the system has. The variance is a fact about the PLAN, which is why it is grouped by type/unit.
         */
        var effort = Totals().Effort;

        Assert.Equal(12m, effort.EstimatedHours);
        Assert.Equal(14m, effort.SpentHours);
        Assert.Equal(2, effort.TaskCount);
    }

    [Fact]
    public void There_is_no_efficiency_percentage_anywhere_in_the_contract()
    {
        // The exclusion, asserted as an absence so it cannot creep back as a "convenience" on the DTO.
        var properties = typeof(WorkReportEffort).GetProperties().Select(p => p.Name).ToList();

        Assert.Equal(["EstimatedHours", "SpentHours", "TaskCount"], properties);
        Assert.DoesNotContain(
            typeof(WorkReportBucket).GetProperties(),
            p => p.Name.Contains("Efficiency", StringComparison.OrdinalIgnoreCase)
                 || p.Name.Contains("Score", StringComparison.OrdinalIgnoreCase));
    }

    // ── Outcomes and rework ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Outcomes_histogram_the_closure_codes_the_type_dictionary_supplied()
    {
        // Faz 3's ClosureReasonCode, counted. Ordered by frequency so the screen does not have to sort it.
        var outcomes = Totals().Outcomes;

        Assert.Equal(2, outcomes.Count);
        Assert.Equal("RESOLVED", outcomes[0].Code);
        Assert.Equal(3, outcomes[0].Count);
        Assert.Equal("SUPERSEDED", outcomes[1].Code);
        Assert.Equal(1, outcomes[1].Count);
    }

    [Fact]
    public void Rework_reports_HOW_MANY_TASKS_and_HOW_MANY_RETURNS_separately()
    {
        /*
         * Faz 4's signal, aggregated. Two numbers because they answer different questions: three returns across
         * two tasks is a team under pressure; three returns on one task is a task nobody can finish. A single
         * number hides which.
         *
         * ⚠ COUNTS, never a rate — a rate needs a denominator the reader agrees with, and choosing one here
         * would put a second answer beside whatever the screen (5b) divides by.
         */
        var rework = Totals().Rework;

        Assert.Equal(2, rework.TasksReturned);
        Assert.Equal(3, rework.TotalReturns);
    }

    // ── Grouping ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Grouping_by_unit_splits_the_totals_without_losing_a_row()
    {
        /*
         * Unit A holds T1, T2, T5; unit B holds T3, T4. The groups have to add back up to the totals for
         * everything that is a plain count — a report whose parts do not sum to its whole is one nobody can
         * argue from.
         */
        var report = WorkReportTally.Build(
            Criteria(WorkReportGroupBy.OrganizationUnit), Rows(), 0, Returns);

        Assert.Equal(2, report.Groups.Count);
        Assert.Equal(report.Totals.Flow.Opened, report.Groups.Sum(g => g.Flow.Opened));
        Assert.Equal(report.Totals.Flow.Closed, report.Groups.Sum(g => g.Flow.Closed));
        Assert.Equal(report.Totals.Rework.TotalReturns, report.Groups.Sum(g => g.Rework.TotalReturns));

        var unitA = report.Groups.Single(g => g.Key == UnitA.ToString());
        Assert.Equal(2, unitA.Flow.Opened);      // T1, T2 (T5 opened in May)
        Assert.Equal(3, unitA.Flow.Completed);   // T1, T2, T5
    }

    [Fact]
    public void A_row_that_names_no_group_keeps_an_empty_key_rather_than_disappearing()
    {
        /*
         * T3 and T5 carry no task type. Dropping them would make the groups add up to less than the totals with
         * nothing on screen to explain the gap — the fastest way to lose a reader's trust in a report.
         */
        var report = WorkReportTally.Build(Criteria(WorkReportGroupBy.TaskType), Rows(), 0, Returns);

        Assert.Contains(report.Groups, g => g.Key == string.Empty);
        Assert.Equal(report.Totals.Flow.Opened, report.Groups.Sum(g => g.Flow.Opened));
    }

    [Fact]
    public void Unattended_is_NOT_split_across_groups()
    {
        // It is a tenant-level "right now" figure. Repeating it per group would multiply one backlog by the
        // number of units on screen and attribute all of it to each.
        var report = WorkReportTally.Build(
            Criteria(WorkReportGroupBy.OrganizationUnit), Rows(), 9, Returns);

        Assert.Equal(9, report.Totals.Flow.Unattended);
        Assert.All(report.Groups, group => Assert.Equal(0, group.Flow.Unattended));
    }

    // ── The period boundary ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_period_is_half_open_so_two_consecutive_months_cannot_both_claim_a_day()
    {
        // A task created at the exact instant the period ends belongs to the NEXT period, not this one.
        var onTheBoundary = new[] { Row(T1, UnitA, TypeX, To) };

        Assert.False(WorkReportTally.TouchedInPeriod(onTheBoundary[0], From, To));
        Assert.Equal(0, WorkReportTally.Build(Criteria(), onTheBoundary, 0, Returns).Totals.Flow.Opened);

        // …and one created at the instant it starts belongs to this one.
        var atTheStart = new[] { Row(T1, UnitA, TypeX, From) };
        Assert.True(WorkReportTally.TouchedInPeriod(atTheStart[0], From, To));
    }

    [Fact]
    public void The_report_states_WHICH_scope_produced_it()
    {
        /*
         * So a reader can tell "there is no work" from "there is no work I may see" — two very different
         * sentences that produce the same empty chart.
         */
        Assert.Equal(WorkReportDto.ScopeTenant, WorkReportTally.Build(Criteria(), Rows(), 0, Returns).ScopeApplied);

        var scoped = new WorkReportCriteria(
            From, To,
            WorkReportScope.FromDataScopes(
                [new EntitlementDataScope(EntitlementDataScopeKind.OrgUnit, UnitA, null)], Guid.NewGuid()));

        Assert.Equal(
            WorkReportDto.ScopeScoped,
            WorkReportTally.Build(scoped, Rows(), 0, Returns).ScopeApplied);
    }
}
