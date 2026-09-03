using System.Reflection;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// DILIM 1c — FROM A NUMBER TO THE WORK ITSELF.
///
/// <para><b>The gap this closes.</b> The report could say "10 late" and had no way to say WHICH ten. A manager
/// read a figure and could act on nothing, so the report was a dead end rather than a route into the work.</para>
///
/// <para><b>⚠ THE ACCEPTANCE CRITERION IS AN IDENTITY, NOT AN AGREEMENT.</b> "The list should have as many rows
/// as the number said" is a property two separately-written queries can satisfy on the day they are written and
/// quietly stop satisfying later — and when they disagree, nobody on the screen can tell which of the two
/// numbers is the true one. So the count and the list are the SAME CODE: <c>WorkReportTally.Select</c> produces
/// the rows, <c>Measure</c> counts them and the items endpoint pages them. The tests below assert that identity
/// cell by cell, and the sabotages break the shared method rather than a copy of it.</para>
/// </summary>
public sealed class WorkReportItemsTests
{
    private static readonly DateTimeOffset From = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly Guid UnitA = Guid.Parse("aaaaaaaa-0000-0000-0000-00000000000a");
    private static readonly Guid UnitB = Guid.Parse("bbbbbbbb-0000-0000-0000-00000000000b");

    private static WorkReportRow Row(
        int id,
        DateTimeOffset created,
        DateTimeOffset? completed = null,
        DateTimeOffset? cancelled = null,
        DateTimeOffset? due = null,
        string? outcome = null,
        Guid? unit = null) => new(
        Guid.Parse($"00000000-0000-0000-0000-{id:D12}"),
        TaskTypeId: null,
        OrganizationUnitId: unit ?? UnitA,
        AssigneeUserId: null,
        CreatedByUserId: null,
        PoolPositionId: null,
        Priority: TaskPriority.Medium,
        created,
        completed,
        cancelled,
        due,
        EstimateHours: null,
        SpentHours: 0m,
        outcome,
        completed is not null ? TaskLifecycle.Done
            : cancelled is not null ? TaskLifecycle.Cancelled
            : TaskLifecycle.Open);

    private static WorkReportCriteria Criteria(WorkReportGroupBy groupBy = WorkReportGroupBy.None) =>
        new(From, To, WorkReportScope.TenantWideScope(), groupBy);

    /*
     * ONE PERIOD WITH SOMETHING IN EVERY CELL, so no assertion below can pass because its cell was empty.
     *
     *   #1  opened 2 Jun, completed 10 Jun, due 15 Jun   → opened · closed · completed · on time · CORRECTED
     *   #2  opened 3 Jun, completed 20 Jun, due 12 Jun   → opened · closed · completed · LATE · CORRECTED
     *   #3  opened 4 Jun, cancelled 9 Jun,  no deadline  → opened · closed · cancelled · without due · DROPPED
     *   #4  opened 5 Jun, still open                     → opened, and returned twice
     *   #5  opened in MAY, completed 6 Jun, due 1 Jun    → NOT opened in the period; closed and LATE in it
     */
    private static readonly WorkReportRow[] Touched =
    [
        Row(1, From.AddDays(1), completed: From.AddDays(9), due: From.AddDays(14), outcome: "CORRECTED"),
        Row(2, From.AddDays(2), completed: From.AddDays(19), due: From.AddDays(11), outcome: "CORRECTED"),
        Row(3, From.AddDays(3), cancelled: From.AddDays(8), outcome: "DROPPED"),
        Row(4, From.AddDays(4)),
        Row(5, From.AddDays(-20), completed: From.AddDays(5), due: From.AddDays(0), unit: UnitB)
    ];

    /// <summary>Open at the period's end — one in each ageing band, so all three are non-empty.</summary>
    private static readonly WorkReportRow[] OpenAtEnd =
    [
        Row(10, To.AddDays(-3)),                     //   3 days → 0-7
        Row(11, To.AddDays(-20)),                    //  20 days → 8-30
        Row(12, To.AddDays(-200), unit: UnitB)       // 200 days → 30+
    ];

    /// <summary>Two unclaimed tasks, so the tile's number is not zero and its list is not vacuously right.</summary>
    private static readonly WorkReportRow[] Unclaimed = [Row(20, From.AddDays(6)), Row(21, From.AddDays(7))];

    private static WorkReportRowSet Set() => new(
        Touched,
        OpenAtEnd,
        Unclaimed,
        new Dictionary<Guid, int> { [Touched[3].Id] = 2 });

    /// <summary>Every cell the report publishes, paired with the number it publishes for it.</summary>
    private static IEnumerable<(WorkReportBucketKind Kind, string? Argument, int Published)> Cells(
        WorkReportBucket bucket)
    {
        yield return (WorkReportBucketKind.Opened, null, bucket.Flow.Opened);
        yield return (WorkReportBucketKind.Closed, null, bucket.Flow.Closed);
        yield return (WorkReportBucketKind.Completed, null, bucket.Flow.Completed);
        yield return (WorkReportBucketKind.Cancelled, null, bucket.Flow.Cancelled);
        yield return (WorkReportBucketKind.Unattended, null, bucket.Flow.Unattended);
        yield return (WorkReportBucketKind.OnTime, null, bucket.Timeliness.OnTime);
        yield return (WorkReportBucketKind.Late, null, bucket.Timeliness.Late);
        yield return (WorkReportBucketKind.WithoutDueDate, null, bucket.Timeliness.WithoutDueDate);
        yield return (WorkReportBucketKind.AgingUpTo7Days, null, bucket.Aging.UpTo7Days);
        yield return (WorkReportBucketKind.AgingFrom8To30Days, null, bucket.Aging.From8To30Days);
        yield return (WorkReportBucketKind.AgingOlderThan30Days, null, bucket.Aging.OlderThan30Days);
        yield return (WorkReportBucketKind.Returned, null, bucket.Rework.TasksReturned);

        foreach (var outcome in bucket.Outcomes)
        {
            yield return (WorkReportBucketKind.Outcome, outcome.Code, outcome.Count);
        }
    }

    // ── (4) THE NUMBER AND THE LIST MUST AGREE ───────────────────────────────────────────────────────────

    [Fact]
    public void EVERY_cell_lists_exactly_as_many_tasks_as_its_number_claims()
    {
        /*
         * ⚠ THE SLICE'S ACCEPTANCE CRITERION, ASSERTED CELL BY CELL.
         *
         * Both sides come from production: the left from `WorkReportTally.Build`, which is what the endpoint
         * returns, and the right from `WorkReportTally.Select`, which is what the items endpoint pages. There
         * is no test-side reconstruction of either — the lesson of Dilim 1a, where a helper rebuilt the
         * production order inside the test and a scope-dropping sabotage stayed green.
         */
        var criteria = Criteria();
        var set = Set();
        var report = WorkReportTally.Build(criteria, set);

        foreach (var (kind, argument, published) in Cells(report.Totals))
        {
            var listed = WorkReportTally.Select(criteria, set, kind, argument);

            Assert.Equal(published, listed.Count);

            // ⚠ NON-VACUITY. An identity between two zeroes proves nothing, and eleven of these cells would
            // read as green on a report that counted nothing at all.
            Assert.True(published > 0, $"{kind}{(argument is null ? "" : " " + argument)} has nothing in it — "
                + "the fixture stopped covering this cell and the assertion above became vacuous");
        }
    }

    [Fact]
    public void The_cells_are_the_right_TASKS_and_not_merely_the_right_count()
    {
        /*
         * A count can be right while the membership is wrong — swap one late task for one on-time task and both
         * numbers survive. So the identities are pinned for the cells where that swap is easiest to make.
         */
        var set = Set();
        IEnumerable<int> Ids(WorkReportBucketKind kind, string? argument = null) =>
            WorkReportTally.Select(Criteria(), set, kind, argument)
                .Select(row => int.Parse(row.Id.ToString()[^12..]))
                .OrderBy(id => id);

        Assert.Equal([1, 2, 3, 4], Ids(WorkReportBucketKind.Opened));      // #5 opened in May
        Assert.Equal([1, 2, 3, 5], Ids(WorkReportBucketKind.Closed));      // #4 is still open
        Assert.Equal([1], Ids(WorkReportBucketKind.OnTime));               // #2 and #5 missed their dates
        Assert.Equal([2, 5], Ids(WorkReportBucketKind.Late));
        Assert.Equal([3], Ids(WorkReportBucketKind.WithoutDueDate));       // nobody promised a date for it
        Assert.Equal([4], Ids(WorkReportBucketKind.Returned));
        Assert.Equal([1, 2], Ids(WorkReportBucketKind.Outcome, "CORRECTED"));
        Assert.Equal([12], Ids(WorkReportBucketKind.AgingOlderThan30Days));
        Assert.Equal([20, 21], Ids(WorkReportBucketKind.Unattended));
    }

    [Fact]
    public void A_GROUP_row_lists_exactly_the_work_that_row_was_measured_from()
    {
        /*
         * The breakdown's bars are clickable too, and the set behind a bar is the set the bar was measured
         * from — `RestrictToGroup` is the same narrowing `Build` used to produce the bucket. A second reading
         * of the axis here would let a bar say five and its list show four.
         */
        var criteria = Criteria(WorkReportGroupBy.OrganizationUnit);
        var set = Set();
        var report = WorkReportTally.Build(criteria, set);

        Assert.Equal(2, report.Groups.Count);

        foreach (var group in report.Groups)
        {
            var groupSet = WorkReportTally.RestrictToGroup(criteria, set, group.Key!);

            foreach (var (kind, argument, published) in Cells(group))
            {
                Assert.Equal(published, WorkReportTally.Select(criteria, groupSet, kind, argument).Count);
            }
        }

        // Non-vacuity for the loop above: the two units really do hold different work.
        var b = report.Groups.Single(g => g.Key == UnitB.ToString());
        Assert.Equal(0, b.Flow.Opened);          // #5 opened in May
        Assert.Equal(1, b.Flow.Closed);
        Assert.Equal(1, b.Aging.OlderThan30Days);
    }

    [Fact]
    public void The_folded_OTHER_bar_lists_the_groups_the_cap_folded_and_nothing_else()
    {
        /*
         * ⚠ THE ONE KEY WHOSE MEMBERSHIP IS NOT A PROPERTY OF A SINGLE ROW. "All other groups" means
         * "everything past the busiest fifty", so the list has to resolve it through the SAME ordering the
         * chart drew from. Two orderings would make the folded BAR and the folded LIST different sets, and the
         * mismatch would only appear on tenants big enough to trip the cap — the ones least able to check.
         */
        /*
         * ⚠ THE ROWS ARRIVE IN THE OPPOSITE ORDER TO THEIR BUSY-NESS, AND THAT IS THE WHOLE FIXTURE.
         *
         * The first version of this test appended the busiest unit first, so the insertion order happened to
         * equal the busy-ness order — and a sabotage that resolved the folded tail by first-seen order instead
         * of by the cap's ordering produced the identical set and stayed GREEN. Unit n now holds n tasks and is
         * appended n-th, so first-seen order is the exact REVERSE of the cap's: the two answers cannot coincide.
         */
        var rows = Enumerable.Range(1, WorkReportDto.MaxGroups + 7)
            .SelectMany(n => Enumerable.Range(0, n)
                .Select(k => Row(n * 1000 + k, From.AddDays(1), unit: Guid.Parse($"cccccccc-0000-0000-0000-{n:D12}"))))
            .ToList();

        var criteria = Criteria(WorkReportGroupBy.OrganizationUnit);
        var set = new WorkReportRowSet(rows, [], [], new Dictionary<Guid, int>());
        var report = WorkReportTally.Build(criteria, set);

        Assert.Equal(7, report.GroupsTruncated);

        var other = report.Groups.Single(g => g.Key == WorkReportDto.OtherKey);
        var listed = WorkReportTally.Select(
            criteria, WorkReportTally.RestrictToGroup(criteria, set, WorkReportDto.OtherKey),
            WorkReportBucketKind.Opened);

        Assert.Equal(other.Flow.Opened, listed.Count);

        // Hand-computed: the seven LEAST busy units hold 1+2+…+7 = 28 tasks. A tail resolved by first-seen
        // order would fold the seven BUSIEST instead, and this number would be 51+52+…+57 = 378.
        Assert.Equal(28, other.Flow.Opened);

        // …and it is really the TAIL: none of the fifty drawn bars appears in the folded list.
        var drawn = report.Groups
            .Where(g => g.Key != WorkReportDto.OtherKey)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);
        Assert.All(listed, row => Assert.DoesNotContain(row.OrganizationUnitId.ToString(), drawn));
    }

    // ── (3) PAGING ───────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_total_is_the_CELLS_number_and_never_the_pages_length()
    {
        /*
         * ⚠ A LIST THAT REPORTED ITS OWN LENGTH would silently rewrite "83 opened" as "50 opened" the moment
         * the cap bit — a report contradicting itself on one screen, with nothing to say which half was right.
         */
        var many = Enumerable.Range(1, 83).Select(n => Row(n, From.AddDays(1))).ToList();

        var (rows, total, hasMore) = WorkReportTally.Page(many, 0);

        Assert.Equal(WorkReportItemsDto.PageSize, rows.Count);
        Assert.Equal(83, total);
        Assert.True(hasMore, "the cut was made silently");
    }

    [Fact]
    public void The_last_page_says_there_is_no_more_and_the_pages_partition_the_cell()
    {
        // Every row appears on exactly one page: no repeats when "more" is pressed, and nothing skipped.
        var many = Enumerable.Range(1, 83).Select(n => Row(n, From.AddDays(n % 20))).ToList();
        var selected = WorkReportTally.Select(
            Criteria(), new WorkReportRowSet(many, [], [], new Dictionary<Guid, int>()),
            WorkReportBucketKind.Opened);

        var first = WorkReportTally.Page(selected, 0);
        var second = WorkReportTally.Page(selected, WorkReportItemsDto.PageSize);

        Assert.True(first.HasMore);
        Assert.False(second.HasMore, "the last page still claimed there was more");
        Assert.Equal(33, second.Rows.Count);

        var seen = first.Rows.Concat(second.Rows).Select(row => row.Id).ToList();
        Assert.Equal(83, seen.Distinct().Count());
        Assert.Equal(selected.Select(row => row.Id), seen);
    }

    [Fact]
    public void The_order_is_TOTAL_so_two_reads_of_a_page_agree()
    {
        /*
         * Paging an unordered set hands out an arbitrary page: pressing "more" would show rows the first page
         * already showed while hiding others entirely. Newest first, ties broken on the id — rows 30..34 below
         * share a creation instant precisely so the tie-break is exercised.
         */
        var tied = Enumerable.Range(30, 5).Select(n => Row(n, From.AddDays(3))).ToList();
        var set = new WorkReportRowSet(tied, [], [], new Dictionary<Guid, int>());

        var first = WorkReportTally.Select(Criteria(), set, WorkReportBucketKind.Opened);
        var again = WorkReportTally.Select(
            Criteria(), set with { Touched = tied.AsEnumerable().Reverse().ToList() },
            WorkReportBucketKind.Opened);

        Assert.Equal(first.Select(row => row.Id), again.Select(row => row.Id));
    }

    [Fact]
    public void An_empty_cell_lists_nothing_rather_than_everything()
    {
        /*
         * ⚠ THE DIRECTION THAT MATTERS. A cell with no work must produce NO rows — a fallback that showed the
         * whole set when a selection came back empty would turn "0 cancelled" into a list of every task in the
         * period, and it would look like a feature.
         */
        var noCancellations = new WorkReportRowSet(
            [Row(1, From.AddDays(1), completed: From.AddDays(2))], [], [], new Dictionary<Guid, int>());

        Assert.Empty(WorkReportTally.Select(Criteria(), noCancellations, WorkReportBucketKind.Cancelled));

        // …while the same set DOES list its completions, so the assertion above is about the cell and not
        // about a selector that returns nothing for everything.
        Assert.Single(WorkReportTally.Select(Criteria(), noCancellations, WorkReportBucketKind.Completed));
    }

    [Fact]
    public void An_OUTCOME_cell_with_no_code_lists_NOTHING_rather_than_every_outcome_at_once()
    {
        // The only kind that needs an argument. Falling back to "all outcomes" would answer a question nobody
        // asked, under a heading naming one particular outcome.
        var set = Set();

        Assert.Empty(WorkReportTally.Select(Criteria(), set, WorkReportBucketKind.Outcome));
        Assert.Empty(WorkReportTally.Select(Criteria(), set, WorkReportBucketKind.Outcome, "   "));
        Assert.NotEmpty(WorkReportTally.Select(Criteria(), set, WorkReportBucketKind.Outcome, "CORRECTED"));
    }

    // ── SCOPE ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_click_can_reach_NOTHING_the_report_did_not_already_count()
    {
        /*
         * ⚠ THE SECURITY PROPERTY OF THIS SLICE, and it is structural rather than checked.
         *
         * `Select` reads only the three sets in the row set it is given, and those were produced by the
         * report's own scoped, filtered queries. There is no argument — no bucket, no group key, no outcome
         * code — that reaches a row the report did not already admit. The strongest statement of that is that
         * every cell of an EMPTY set is empty, whatever it is asked.
         */
        var nothing = WorkReportRowSet.Empty;

        foreach (var kind in Enum.GetValues<WorkReportBucketKind>())
        {
            Assert.Empty(WorkReportTally.Select(Criteria(), nothing, kind, "CORRECTED"));
        }

        // Non-vacuity: the very same kinds DO return rows when the set has any.
        Assert.NotEmpty(WorkReportTally.Select(Criteria(), Set(), WorkReportBucketKind.Opened));
    }

    [Fact]
    public void The_items_criteria_carry_the_REPORTS_criteria_rather_than_restating_them()
    {
        /*
         * The period, the scope and the five filters are one object shared with the numbers. A list request
         * that carried its own copies could be asked under a period or a filter the report never ran, and the
         * rows would then answer about a different set than the number that was clicked.
         */
        var report = new WorkReportCriteria(
            From, To, WorkReportScope.TenantWideScope(), WorkReportGroupBy.OrganizationUnit,
            new WorkReportFilter(Priority: TaskPriority.High));

        var items = new WorkReportItemsCriteria(report, WorkReportBucketKind.Late);

        Assert.Same(report, items.Report);
        Assert.Same(report.Scope, items.Report.Scope);

        // And nothing scope-shaped, period-shaped or filter-shaped is declared a SECOND time on the items
        // criteria — a duplicate field is how the two would eventually disagree.
        var own = typeof(WorkReportItemsCriteria)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToList();

        Assert.Equal(["Report", "Bucket", "Argument", "GroupKey", "Skip"], own.Where(n => n != "EqualityContract"));
    }

    // ── DRIFT GUARDS ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_items_read_issues_NO_QUERY_OF_ITS_OWN()
    {
        /*
         * ⚠ THE GUARD FOR THE ONE THING THAT WOULD MAKE ALL OF THE ABOVE MEANINGLESS.
         *
         * Everything in this file proves that `Select` and `Measure` agree. None of it would notice if
         * `ItemsAsync` stopped calling `Select` and built its own Mongo query instead — and that is the most
         * natural change for somebody to make, because "just query the late ones directly" reads as an
         * optimisation. It would also be the exact shape CONTROL TOWER's Dilim 1a sabotage exploited: a
         * production path nothing was watching.
         *
         * So this reads the shipped method's own body and requires that it goes through the shared read and the
         * shared selector, and that it builds no filter and opens no aggregation of its own.
         */
        var body = MethodBody(
            "services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/WorkReportRepository.cs",
            "public async Task<WorkReportItemsDto> ItemsAsync(");

        Assert.Contains("ReadAsync(criteria.Report", body);
        Assert.Contains("WorkReportTally.Select(", body);
        Assert.Contains("WorkReportTally.Page(", body);

        Assert.DoesNotContain("Builders<TaskItem>", body);
        Assert.DoesNotContain(".Aggregate()", body);
        Assert.DoesNotContain("BuildMatchFilter", body);   // through ReadAsync, never re-composed here
    }

    [Fact]
    public void The_shared_read_really_MATCHES_ON_the_composed_filter()
    {
        /*
         * ⚠ WRITTEN BECAUSE A SABOTAGE STAYED GREEN — the same gap, one level up, that CONTROL TOWER found in
         * Dilim 1a.
         *
         * `WorkReportQueryCompositionTests` renders `BuildMatchFilter` and proves the composition is right. It
         * says nothing about whether anything CALLS it. Replacing the call in `ReadAsync` with a bare period
         * filter — dropping the scope from every number and every list at once — left all 80 tests green,
         * because the guard was watching a method rather than the path that runs it.
         *
         * The gap is structural: a rendered-filter guard can only ever prove what a filter says, never that the
         * query uses that filter. So this reads the shipped read's own body and requires the composition to be
         * what its `.Match` is given.
         */
        var body = MethodBody(
            "services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/WorkReportRepository.cs",
            "private async Task<ReportReadout?> ReadAsync(");

        Assert.Contains("var inPeriod = BuildMatchFilter(_tenantContext.TenantId, criteria);", body);
        Assert.Contains(".Match(inPeriod)", body);

        // The fail-closed check has to stay ahead of the query, not beside it.
        Assert.Contains("criteria.Scope.MatchesNothing", body);
        Assert.True(
            body.IndexOf("criteria.Scope.MatchesNothing", StringComparison.Ordinal)
                < body.IndexOf("_tasks.Aggregate()", StringComparison.Ordinal),
            "the scope check no longer runs before the query it is supposed to prevent");

        // And the two reads that DON'T go through BuildMatchFilter still carry the scope explicitly, or the
        // ageing buckets and the unattended tile would be the unscoped numbers on a scoped page.
        foreach (var read in new[] { "private async Task<IReadOnlyList<WorkReportRow>> OpenAtPeriodEndAsync(",
                                     "private async Task<IReadOnlyList<WorkReportRow>> UnattendedAsync(" })
        {
            var other = MethodBody(
                "services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/WorkReportRepository.cs",
                read);

            Assert.Contains("scoped,", other);
            Assert.Contains("DirectFilter(criteria.Filter)", other);
        }
    }

    [Fact]
    public void The_report_and_the_items_endpoint_resolve_scope_through_ONE_implementation()
    {
        /*
         * The list needs exactly the scope the numbers were computed under. The obvious way to give it one is
         * to write the same twenty lines again in the second handler — and a rule living in two places is a
         * rule that will one day be enforced in only one of them. A stale copy would not crash and would not
         * log; it would render a convincing list of somebody else's work.
         */
        var implementations = typeof(WorkReportScopeSource).Assembly
            .GetTypes()
            .Where(type => typeof(IWorkReportScopeSource).IsAssignableFrom(type)
                && type is { IsInterface: false, IsAbstract: false })
            .ToList();

        Assert.Single(implementations);

        // Neither handler may keep a private resolution of its own beside the shared one.
        foreach (var handler in new[] { typeof(Application.Features.Tasks.Handlers.QueryHandlers.WorkReportQueryHandler),
                                        typeof(Application.Features.Tasks.Handlers.QueryHandlers.WorkReportItemsQueryHandler) })
        {
            Assert.DoesNotContain(
                handler.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Select(m => m.Name),
                name => name.Contains("ResolveScope", StringComparison.Ordinal));

            // …and each really does hold the shared one, so the absence above is not simply an empty class.
            Assert.Contains(
                handler.GetFields(BindingFlags.NonPublic | BindingFlags.Instance),
                field => field.FieldType == typeof(IWorkReportScopeSource));
        }
    }

    [Fact]
    public void An_ITEM_carries_its_lifecycle_as_a_STRING_never_the_bare_enum()
    {
        /*
         * ⚠ MEASURED: `TaskLifecycle` carries no `JsonStringEnumConverter` — its own header explains why
         * (existing responses map it explicitly rather than change the wire format of every module that touches
         * it). A record that stored the enum on `WorkReportItem` would therefore serialize `Lifecycle` as a bare
         * NUMBER, the exact defect `WorkReportGroupBy`'s converter exists to prevent and this module has already
         * shipped twice. Checked by REFLECTION rather than by exercising a serializer, so it fails at the type
         * declaration rather than requiring a live round trip to notice.
         */
        var property = typeof(WorkReportItem).GetProperty("Lifecycle");
        Assert.NotNull(property);
        Assert.Equal(typeof(string), property!.PropertyType);
    }

    [Fact]
    public void Every_bucket_kind_names_a_number_the_report_actually_publishes()
    {
        /*
         * A kind with no number behind it is a link to a list nobody can reconcile; a number with no kind is a
         * dead end this slice was supposed to remove. The pairing lives in `Cells` above, and this pins that it
         * is complete in both directions.
         */
        var covered = Cells(WorkReportTally.Build(Criteria(), Set()).Totals)
            .Select(cell => cell.Kind)
            .ToHashSet();

        Assert.Equal(Enum.GetValues<WorkReportBucketKind>().ToHashSet(), covered);
    }

    /// <summary>Reads one method's body out of the shipped source, by brace matching.</summary>
    private static string MethodBody(string relativePath, string signature)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", ".."));
        var source = File.ReadAllText(Path.Combine(root, relativePath));

        var at = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(at >= 0, $"{signature} was not found in {relativePath} — the guard is pointing at nothing");

        var open = source.IndexOf('{', at);
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                depth++;
            }
            else if (source[i] == '}' && --depth == 0)
            {
                return source[open..(i + 1)];
            }
        }

        throw new InvalidOperationException($"Unbalanced braces after {signature}");
    }
}
