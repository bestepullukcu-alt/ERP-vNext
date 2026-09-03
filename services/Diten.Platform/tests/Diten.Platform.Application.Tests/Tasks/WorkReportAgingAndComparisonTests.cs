using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// Dilim 1b — AGEING and the PREVIOUS-PERIOD COMPARISON.
///
/// <para><b>⚠ THE PROPERTY BOTH OF THESE TURN ON IS REPEATABILITY.</b> A report is evidence: it gets opened
/// again in a review months later, beside a decision somebody already took, and it has to say the same thing.
/// Ageing measured from the current clock, or a "previous period" the client works out for itself, both break
/// that quietly — the page still renders, the numbers just stop matching the copy that was printed.</para>
/// </summary>
public sealed class WorkReportAgingAndComparisonTests
{
    private static readonly Guid UnitA = Guid.Parse("aaaaaaaa-1111-0000-0000-000000000001");
    private static readonly Guid UnitB = Guid.Parse("aaaaaaaa-1111-0000-0000-000000000002");

    // The whole of June 2026, half-open — the period every case below asks about.
    private static readonly DateTimeOffset From = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private static WorkReportRow Row(
        DateTimeOffset created,
        DateTimeOffset? completed = null,
        DateTimeOffset? cancelled = null,
        Guid? unit = null) => new(
        Guid.NewGuid(),
        TaskTypeId: null,
        OrganizationUnitId: unit ?? UnitA,
        AssigneeUserId: null,
        CreatedByUserId: null,
        PoolPositionId: null,
        Priority: TaskPriority.Medium,
        created,
        completed,
        cancelled,
        DueAt: null,
        EstimateHours: null,
        SpentHours: 0m,
        ClosureReasonCode: null,
        Lifecycle: TaskLifecycle.Open);

    private static WorkReportCriteria Criteria(WorkReportGroupBy groupBy = WorkReportGroupBy.None) =>
        new(From, To, WorkReportScope.TenantWideScope(), groupBy);

    // ── AGEING: THE THREE BUCKETS ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Open_work_is_bucketed_by_its_age_AT_THE_PERIODS_END()
    {
        /*
         * Period ends 1 July. Ages measured to THAT instant, worked out on paper:
         *   created 28 Jun →  3 days  → 0–7
         *   created 25 Jun →  6 days  → 0–7
         *   created 10 Jun → 21 days  → 8–30
         *   created 20 May → 42 days  → older than 30
         *   created  1 Jan →182 days  → older than 30
         */
        var open = new[]
        {
            Row(new DateTimeOffset(2026, 6, 28, 0, 0, 0, TimeSpan.Zero)),
            Row(new DateTimeOffset(2026, 6, 25, 0, 0, 0, TimeSpan.Zero)),
            Row(new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero)),
            Row(new DateTimeOffset(2026, 5, 20, 0, 0, 0, TimeSpan.Zero)),
            Row(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero))
        };

        var aging = WorkReportTally.AgeOpenWork(open, To);

        Assert.Equal(2, aging.UpTo7Days);
        Assert.Equal(1, aging.From8To30Days);
        Assert.Equal(2, aging.OlderThan30Days);

        // The three buckets PARTITION the set — every open item counted once, none twice, none lost.
        Assert.Equal(open.Length, aging.Total);
    }

    [Fact]
    public void The_SAME_period_gives_the_SAME_ageing_however_long_ago_it_ended()
    {
        /*
         * ⚠ THE ONE THAT MUST NEVER GO RED — the property that makes this report evidence rather than a
         * dashboard.
         *
         * Called twice with the same arguments and compared. If the implementation ever reads the clock, the
         * two answers still agree in the same millisecond — so the real proof is the SECOND pair: the same
         * period measured as though it ended long ago must give the same buckets as one measured "recently".
         * A clock-anchored implementation drifts every task across the 7- and 30-day boundaries as time passes,
         * and this pins that it cannot.
         */
        var open = new[]
        {
            Row(new DateTimeOffset(2026, 6, 28, 0, 0, 0, TimeSpan.Zero)),
            Row(new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero)),
            Row(new DateTimeOffset(2026, 5, 20, 0, 0, 0, TimeSpan.Zero))
        };

        var first = WorkReportTally.AgeOpenWork(open, To);
        var second = WorkReportTally.AgeOpenWork(open, To);
        Assert.Equal(first, second);

        // Hand-computed against 1 July, and it must stay this whatever "today" happens to be:
        //   3 days · 21 days · 42 days  ⇒  1 / 1 / 1
        Assert.Equal(new WorkReportAging(1, 1, 1), first);

        /*
         * And measured against a LATER instant the same rows age — proving the function really does anchor to
         * the instant it is given rather than ignoring it. Against 1 September: 65 · 83 · 104 days ⇒ all older
         * than 30. So the June report keeps saying 1/1/1 precisely because it is not asked about September.
         */
        var laterInstant = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        Assert.Equal(new WorkReportAging(0, 0, 3), WorkReportTally.AgeOpenWork(open, laterInstant));
    }

    [Fact]
    public void Work_with_no_deadline_is_counted_here_because_timeliness_cannot_see_it()
    {
        /*
         * `WorkReportTimeliness.WithoutDueDate` is a blind spot by construction — work nobody promised a date
         * for cannot be early or late. Ageing is the measure that sees it: every open task has an AGE even when
         * it has no PROMISE. None of the rows in this file carry a due date, and all of them are counted.
         */
        var open = new[] { Row(new DateTimeOffset(2026, 6, 20, 0, 0, 0, TimeSpan.Zero)) };

        Assert.Null(open[0].DueAt);
        Assert.Equal(1, WorkReportTally.AgeOpenWork(open, To).Total);
    }

    [Fact]
    public void Open_at_an_instant_means_created_before_it_and_not_closed_by_then()
    {
        /*
         * The predicate the repository's query expresses in Mongo, stated here in one place. A task closed
         * AFTER the period was OPEN during it — which is why this asks the timestamps rather than the lifecycle
         * enum, since the enum only knows about today.
         */
        var stillOpen = Row(From.AddDays(1));
        var closedInside = Row(From.AddDays(1), completed: From.AddDays(5));
        var closedAfter = Row(From.AddDays(1), completed: To.AddDays(5));
        var createdAfter = Row(To.AddDays(1));
        var cancelledInside = Row(From.AddDays(1), cancelled: From.AddDays(3));

        Assert.True(WorkReportTally.OpenAt(stillOpen, To));
        Assert.False(WorkReportTally.OpenAt(closedInside, To));
        Assert.True(WorkReportTally.OpenAt(closedAfter, To), "work closed after the period was open during it");
        Assert.False(WorkReportTally.OpenAt(createdAfter, To));
        Assert.False(WorkReportTally.OpenAt(cancelledInside, To));
    }

    [Fact]
    public void Ageing_is_split_on_the_SAME_axis_as_the_flow()
    {
        // A unit's backlog belongs beside its own flow. Sharing one tenant-wide figure across groups would
        // attribute every unit's backlog to all of them, the way `unattended` deliberately is not split.
        var open = new[]
        {
            Row(new DateTimeOffset(2026, 6, 28, 0, 0, 0, TimeSpan.Zero), unit: UnitA),
            Row(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), unit: UnitB)
        };

        var report = WorkReportTally.Build(
            Criteria(WorkReportGroupBy.OrganizationUnit),
            [Row(From.AddDays(1), unit: UnitA), Row(From.AddDays(2), unit: UnitB)],
            0,
            new Dictionary<Guid, int>(),
            null,
            open);

        Assert.Equal(new WorkReportAging(1, 0, 0), report.Groups.Single(g => g.Key == UnitA.ToString()).Aging);
        Assert.Equal(new WorkReportAging(0, 0, 1), report.Groups.Single(g => g.Key == UnitB.ToString()).Aging);
        Assert.Equal(2, report.Totals.Aging.Total);
    }

    [Fact]
    public void A_caller_that_supplies_no_open_rows_gets_zeroes_rather_than_a_wrong_answer()
    {
        /*
         * Ageing needs its OWN row set: the report's rows are the work TOUCHED in the period, and a task raised
         * last year and still untouched appears in neither the created nor the closed clause. Deriving ageing
         * from those rows would show a clean backlog on the tenant with the worst one — so an absent set is
         * zeroes, not a guess.
         */
        var report = WorkReportTally.Build(
            Criteria(), [Row(From.AddDays(1))], 0, new Dictionary<Guid, int>());

        Assert.Equal(new WorkReportAging(0, 0, 0), report.Totals.Aging);
    }

    // ── THE COMPARISON PERIOD ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_previous_period_is_the_SAME_LENGTH_immediately_before_and_shares_no_day()
    {
        /*
         * ⚠ THE DEFINITION LIVES IN ONE PLACE, AND THIS IS WHAT IT SAYS. 1–30 September compares against
         * 2–31 August: thirty days, ending exactly where September begins. Two implementations of this — one on
         * the server, one in the browser — drift apart the first time somebody reasons about month lengths, and
         * then two figures on the same page disagree with no way to tell which is right.
         *
         * Asserted through the arithmetic the repository performs (`[From − length, From)`) rather than by
         * re-deriving it a second way, which would just be the second implementation again.
         */
        var september = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var october = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

        var asked = new WorkReportCriteria(september, october, WorkReportScope.TenantWideScope());
        var previous = WorkReportRepository.PreviousPeriod(asked);

        Assert.Equal(new DateTimeOffset(2026, 8, 2, 0, 0, 0, TimeSpan.Zero), previous.From);
        Assert.Equal(september, previous.To);

        // Same length, and touching none of the compared period's days — the two halves are comparable and
        // disjoint, which is the whole reason a direction drawn from them means anything.
        Assert.Equal(october - september, previous.To - previous.From);
        Assert.True(previous.To <= asked.From);

        // One level only, and no group work: a comparison that asked for its own comparison would recurse a
        // period at a time until the epoch, and fifty groups measured twice buys one arrow.
        Assert.False(previous.ComparePrevious);
        Assert.Equal(WorkReportGroupBy.None, previous.GroupBy);

        // The scope and the filter ride along UNCHANGED — comparing your own work against somebody else's
        // would be the widening this whole report refuses.
        Assert.Same(asked.Scope, previous.Scope);
        Assert.Equal(asked.Filter, previous.Filter);
    }

    [Fact]
    public void The_comparison_is_OFF_unless_it_was_asked_for()
    {
        // It doubles the reads. A report that always paid for a comparison nobody displayed would be slower for
        // every caller to serve the few who compare.
        Assert.False(new WorkReportCriteria(From, To, WorkReportScope.TenantWideScope()).ComparePrevious);
    }

    [Fact]
    public void A_report_with_no_comparison_carries_null_rather_than_a_bucket_of_zeroes()
    {
        /*
         * Zeroes would read as "the previous period had no work", which is a claim. Null says "nobody asked" —
         * and the screen can then draw no arrow at all rather than a misleading downward one.
         */
        var report = WorkReportTally.Build(Criteria(), [Row(From.AddDays(1))], 0, new Dictionary<Guid, int>());

        Assert.Null(report.Previous);
    }
}
