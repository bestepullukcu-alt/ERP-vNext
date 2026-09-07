using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// Dilim 1a — FILTERING, the company axis, readable labels, and the group cap.
///
/// <para><b>⚠ THE RULE EVERYTHING HERE PROTECTS: A FILTER INTERSECTS THE SCOPE, IT NEVER REPLACES IT.</b> The
/// scope comes from the caller's data entitlements (MOD-0018-FU15); the filter is what the caller typed into a
/// query string. Evaluate the filter first, or instead, and a reporting parameter becomes a way to read other
/// people's work — a failure that renders perfectly and is never reported as a bug.</para>
///
/// <para>The second rule, quieter and just as costly: an unfiltered report must still be EXACTLY the report
/// that existed before filters did. "Additive" is a claim about the past that only a test keeps true.</para>
/// </summary>
public sealed class WorkReportFilterTests
{
    private static readonly DateTimeOffset From = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly Guid Me = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid Stranger = Guid.Parse("11111111-0000-0000-0000-000000000002");
    private static readonly Guid MyUnit = Guid.Parse("22222222-0000-0000-0000-000000000001");
    private static readonly Guid OtherUnit = Guid.Parse("22222222-0000-0000-0000-000000000002");
    private static readonly Guid CompanyA = Guid.Parse("33333333-0000-0000-0000-000000000001");
    private static readonly Guid CompanyB = Guid.Parse("33333333-0000-0000-0000-000000000002");
    private static readonly Guid TypeX = Guid.Parse("44444444-0000-0000-0000-000000000001");

    private static DateTimeOffset June(int day) => new(2026, 6, day, 0, 0, 0, TimeSpan.Zero);

    private static WorkReportRow Row(
        Guid? unit = null,
        Guid? assignee = null,
        Guid? company = null,
        string? typeCode = null,
        Guid? typeId = null,
        TaskPriority priority = TaskPriority.Medium,
        int openedOn = 5) => new(
        Guid.NewGuid(),
        typeId,
        unit ?? MyUnit,
        assignee,
        CreatedByUserId: null,
        PoolPositionId: null,
        priority,
        June(openedOn),
        CompletedAt: null,
        CancelledAt: null,
        DueAt: null,
        EstimateHours: null,
        SpentHours: 0m,
        ClosureReasonCode: null,
        Lifecycle: TaskLifecycle.Open,
        LegalEntityId: company,
        TaskTypeCode: typeCode);

    private static WorkReportScope ScopeOf(params Guid[] unitIds) =>
        WorkReportScope.FromDataScopes(
            unitIds.Select(id => new EntitlementDataScope(EntitlementDataScopeKind.OrgUnit, id, null)).ToList(),
            Me);

    private static WorkReportCriteria Criteria(
        WorkReportFilter? filter = null,
        WorkReportGroupBy groupBy = WorkReportGroupBy.None,
        WorkReportScope? scope = null) =>
        new(From, To, scope ?? WorkReportScope.TenantWideScope(), groupBy, filter);

    /// <summary>The pipeline as the repository runs it: SCOPE first, then the filter. Order is the point.</summary>
    private static IReadOnlyList<WorkReportRow> Pipeline(
        WorkReportScope scope, WorkReportFilter? filter, IEnumerable<WorkReportRow> rows) =>
        rows.Where(row => WorkReportScopeMirror.InScope(scope, row))
            .Where(row => WorkReportTally.MatchesFilter(filter, row))
            .ToList();

    // ── (1) THE INTERSECTION RULE ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Filtering_on_someone_OUTSIDE_my_scope_returns_nothing_rather_than_their_work()
    {
        /*
         * ⚠ THE ONE THAT MUST NEVER GO RED.
         *
         * A reader entitled to their own unit types a colleague's id into `assigneeUserId`. The honest answer is
         * an EMPTY report: the stranger's rows were never in scope, so there is nothing for the filter to keep.
         * If this ever passes rows through, a query string has become a way to read other people's work.
         */
        var mine = Row(unit: MyUnit, assignee: Me);
        var theirs = Row(unit: OtherUnit, assignee: Stranger);
        var scope = ScopeOf(MyUnit);

        var result = Pipeline(scope, new WorkReportFilter(AssigneeUserId: Stranger), [mine, theirs]);

        Assert.Empty(result);

        /*
         * ⚠ NON-VACUITY, and it is what makes the assertion above a measurement rather than a coincidence. The
         * stranger's row IS visible to a caller whose scope covers it — so the emptiness came from the SCOPE,
         * not from a predicate that rejects everything.
         */
        var wider = Pipeline(ScopeOf(MyUnit, OtherUnit), new WorkReportFilter(AssigneeUserId: Stranger), [mine, theirs]);
        Assert.Single(wider);
        Assert.Equal(Stranger, wider[0].AssigneeUserId);
    }

    [Fact]
    public void The_same_holds_for_a_unit_a_company_and_a_type_outside_my_scope()
    {
        // One rule, four dimensions. A filter that leaked on any one of them would leak on all of them the day
        // somebody copied the working branch.
        var scope = ScopeOf(MyUnit);
        var mine = Row(unit: MyUnit, company: CompanyA, typeCode: "DEV");
        var theirs = Row(unit: OtherUnit, company: CompanyB, typeCode: "AUDIT");

        Assert.Empty(Pipeline(scope, new WorkReportFilter(OrganizationUnitId: OtherUnit), [mine, theirs]));
        Assert.Empty(Pipeline(scope, new WorkReportFilter(LegalEntityId: CompanyB), [mine, theirs]));
        Assert.Empty(Pipeline(scope, new WorkReportFilter(TaskTypeCode: "AUDIT"), [mine, theirs]));

        // …and each of the three DOES match inside the scope, so none of the emptiness above is a dead predicate.
        Assert.Single(Pipeline(scope, new WorkReportFilter(OrganizationUnitId: MyUnit), [mine, theirs]));
        Assert.Single(Pipeline(scope, new WorkReportFilter(LegalEntityId: CompanyA), [mine, theirs]));
        Assert.Single(Pipeline(scope, new WorkReportFilter(TaskTypeCode: "DEV"), [mine, theirs]));
    }

    [Fact]
    public void A_filter_can_only_ever_REMOVE_rows_from_what_the_scope_allowed()
    {
        /*
         * The property stated directly, over every filter this contract accepts: the filtered set is always a
         * SUBSET of the scoped set. A filter that added a row would be a widening, and there is no such thing
         * in this design.
         */
        var scope = ScopeOf(MyUnit);
        var rows = new[]
        {
            Row(unit: MyUnit, assignee: Me, company: CompanyA, typeCode: "DEV", priority: TaskPriority.High),
            Row(unit: MyUnit, assignee: Stranger, company: CompanyA, typeCode: "AUDIT"),
            Row(unit: OtherUnit, assignee: Stranger, company: CompanyB, typeCode: "DEV")
        };

        var scoped = rows.Where(row => WorkReportScopeMirror.InScope(scope, row)).Select(row => row.Id).ToHashSet();
        Assert.NotEmpty(scoped);

        WorkReportFilter[] filters =
        [
            new(AssigneeUserId: Me),
            new(AssigneeUserId: Stranger),
            new(OrganizationUnitId: OtherUnit),
            new(LegalEntityId: CompanyB),
            new(TaskTypeCode: "DEV"),
            new(Priority: TaskPriority.High),
            new(LegalEntityId: CompanyA, TaskTypeCode: "DEV", Priority: TaskPriority.High)
        ];

        foreach (var filter in filters)
        {
            var got = Pipeline(scope, filter, rows).Select(row => row.Id).ToList();
            Assert.All(got, id => Assert.Contains(id, scoped));
        }
    }

    // ── (2) THE UNFILTERED REPORT IS UNCHANGED ───────────────────────────────────────────────────────────

    [Fact]
    public void No_filter_produces_EXACTLY_the_report_that_existed_before_filters()
    {
        /*
         * ⚠ "Additive" is a claim about the past, and only a test keeps it true. Every shape of "nothing was
         * asked for" — a null filter, an all-null filter, a blank type code — has to be the identity.
         */
        var rows = new[]
        {
            Row(unit: MyUnit, assignee: Me, company: CompanyA, typeCode: "DEV"),
            Row(unit: MyUnit, assignee: Stranger, company: CompanyA, typeCode: "AUDIT")
        };

        /*
         * ⚠ THROUGH THE PIPELINE, NOT THROUGH `Build` ALONE — and this correction is worth recording.
         *
         * The first version of this test called `Build` directly with a filter on the criteria and asserted the
         * result was unchanged. It passed for the wrong reason: `Build` MEASURES rows, it never filters them —
         * the filtering happens in the repository, between the read and the tally. So the test agreed with the
         * code about a step neither of them was taking, and a sabotage that broke blank-code handling did not
         * move it at all. Caught by that sabotage returning green.
         */
        var scope = WorkReportScope.TenantWideScope();
        var baseline = WorkReportTally.Build(
            Criteria(),
            WorkReportSets.Of(Pipeline(scope, null, rows), unattended: 3, returns: new Dictionary<Guid, int>()));

        foreach (var filter in new WorkReportFilter?[] { null, new WorkReportFilter(), new WorkReportFilter(TaskTypeCode: "   ") })
        {
            var got = WorkReportTally.Build(
            Criteria(filter),
            WorkReportSets.Of(Pipeline(scope, filter, rows), unattended: 3, returns: new Dictionary<Guid, int>()));

            Assert.Equal(baseline.Totals.Flow, got.Totals.Flow);
            Assert.Equal(baseline.Totals.CycleTime, got.Totals.CycleTime);
            Assert.Equal(baseline.Totals.Rework, got.Totals.Rework);
            Assert.Equal(baseline.Groups.Count, got.Groups.Count);
            Assert.Equal(baseline.GroupsTruncated, got.GroupsTruncated);
        }

        // Non-vacuity: the fixture has work in it, so "identical" is not two empty reports agreeing.
        Assert.Equal(2, baseline.Totals.Flow.Opened);

        // …and the pipeline DOES drop rows when a real filter is given, so the identity above is a property of
        // the empty filter rather than of a pipeline that never filters.
        Assert.Single(Pipeline(scope, new WorkReportFilter(TaskTypeCode: "DEV"), rows));
    }

    [Fact]
    public void An_empty_filter_declares_itself_empty()
    {
        // The property the whole additive claim rests on, asserted where it is decided.
        Assert.True(new WorkReportFilter().IsEmpty);
        Assert.True(new WorkReportFilter(TaskTypeCode: "  ").IsEmpty);
        Assert.False(new WorkReportFilter(Priority: TaskPriority.Low).IsEmpty);
        Assert.False(new WorkReportFilter(AssigneeUserId: Me).IsEmpty);
    }

    // ── (3) THE COMPANY AXIS AND THE UNASSIGNED BUCKET ───────────────────────────────────────────────────

    [Fact]
    public void Work_whose_unit_could_not_be_resolved_is_COUNTED_as_unassigned_not_dropped()
    {
        /*
         * ⚠ THE SILENT-LOSS GUARD.
         *
         * MEASURED 2026-09-04 and worth stating because the brief assumed otherwise:
         * `TaskItem.OrganizationUnitId` is `required Guid` — NOT nullable — and `OrganizationUnit.LegalEntityId`
         * is required too. So a task never simply "has no unit". What it can have is a unit id that no longer
         * RESOLVES (deleted, or pointing at nothing), and that row has no company.
         *
         * Dropping it would make the groups fail to add up to the totals with nothing on screen to explain the
         * difference — the fastest way to lose a reader's trust in a report permanently.
         */
        var rows = new[]
        {
            Row(company: CompanyA),
            Row(company: CompanyA),
            Row(company: null)   // its unit did not resolve
        };

        var report = WorkReportTally.Build(
            Criteria(groupBy: WorkReportGroupBy.LegalEntity),
            WorkReportSets.Of(rows, returns: new Dictionary<Guid, int>()));

        var unassigned = Assert.Single(report.Groups, g => g.Key == WorkReportDto.UnassignedKey);
        Assert.Equal(1, unassigned.Flow.Opened);

        // THE PARTS ADD UP TO THE WHOLE. That is the invariant the bucket exists to preserve.
        Assert.Equal(report.Totals.Flow.Opened, report.Groups.Sum(g => g.Flow.Opened));
        Assert.Equal(3, report.Totals.Flow.Opened);
    }

    [Fact]
    public void The_company_axis_groups_by_the_DERIVED_company()
    {
        var rows = new[] { Row(company: CompanyA), Row(company: CompanyA), Row(company: CompanyB) };

        var report = WorkReportTally.Build(
            Criteria(groupBy: WorkReportGroupBy.LegalEntity),
            WorkReportSets.Of(rows, returns: new Dictionary<Guid, int>()));

        Assert.Equal(2, report.Groups.Count);
        Assert.Equal(2, report.Groups.Single(g => g.Key == CompanyA.ToString()).Flow.Opened);
        Assert.Equal(1, report.Groups.Single(g => g.Key == CompanyB.ToString()).Flow.Opened);
    }

    [Fact]
    public void A_company_filter_excludes_work_whose_company_is_unknown()
    {
        /*
         * A row whose unit did not resolve has an UNKNOWN company, not a matching one. Answering "yes" would
         * attribute unattributable work to whichever company was asked about — the one direction a report must
         * never guess in.
         */
        Assert.False(WorkReportTally.MatchesFilter(new WorkReportFilter(LegalEntityId: CompanyA), Row(company: null)));
        Assert.True(WorkReportTally.MatchesFilter(new WorkReportFilter(LegalEntityId: CompanyA), Row(company: CompanyA)));
    }

    // ── (4) LABELS ───────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_group_carries_the_WORDS_when_the_server_can_supply_them()
    {
        var rows = new[] { Row(company: CompanyA), Row(company: CompanyB) };
        var labels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompanyA.ToString()] = "Grand Medical Poland"
        };

        var report = WorkReportTally.Build(
            Criteria(groupBy: WorkReportGroupBy.LegalEntity),
            WorkReportSets.Of(rows, returns: new Dictionary<Guid, int>()),
            labels);

        Assert.Equal("Grand Medical Poland", report.Groups.Single(g => g.Key == CompanyA.ToString()).Label);

        /*
         * ⚠ NULL, NEVER A FABRICATION. A company MDM could not name keeps a null label and the screen shows the
         * identity. Inventing a placeholder would put a word on screen that matches nothing anybody can search
         * for — and the same null is what tells the screen to resolve a PERSON from its own lookup, because
         * Platform has no user entity to ask.
         */
        Assert.Null(report.Groups.Single(g => g.Key == CompanyB.ToString()).Label);
    }

    [Fact]
    public void The_key_survives_beside_the_label_rather_than_being_replaced_by_it()
    {
        // A label is for reading; the key is what a follow-up query is built from. Replacing one with the other
        // would make a drill-down impossible the moment a name is ambiguous.
        var report = WorkReportTally.Build(
            Criteria(groupBy: WorkReportGroupBy.LegalEntity),
            WorkReportSets.Of([Row(company: CompanyA)], returns: new Dictionary<Guid, int>()),
            new Dictionary<string, string>(StringComparer.Ordinal) { [CompanyA.ToString()] = "Grand Medical" });

        var group = Assert.Single(report.Groups);
        Assert.Equal(CompanyA.ToString(), group.Key);
        Assert.Equal("Grand Medical", group.Label);
    }

    // ── (5) ORDER AND THE CAP ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Groups_come_back_busiest_first_with_a_total_order()
    {
        /*
         * MEASURED: before this slice there was no `OrderBy` at all, so two reads of the same period could
         * disagree about which unit came first. Busiest first is the axis a reader scans; the key breaks ties so
         * the order is total rather than merely mostly-defined.
         */
        var rows = new List<WorkReportRow>();
        rows.AddRange(Enumerable.Range(0, 3).Select(_ => Row(unit: MyUnit)));
        rows.Add(Row(unit: OtherUnit));

        var report = WorkReportTally.Build(
            Criteria(groupBy: WorkReportGroupBy.OrganizationUnit),
            WorkReportSets.Of(rows, returns: new Dictionary<Guid, int>()));

        Assert.Equal(MyUnit.ToString(), report.Groups[0].Key);
        Assert.Equal(3, report.Groups[0].Flow.Opened);
        Assert.Equal(1, report.Groups[1].Flow.Opened);
    }

    [Fact]
    public void Beyond_the_cap_the_tail_is_FOLDED_and_the_count_is_reported()
    {
        /*
         * ⚠ NEVER A SILENT CUT. A cap alone is worse than none: a reader comparing units would be comparing the
         * arbitrary fifty that survived, with the parts no longer adding up to the whole. So the tail becomes
         * ONE bucket, the number of folded groups travels, and the totals still reconcile.
         */
        var rows = new List<WorkReportRow>();
        for (var i = 0; i < WorkReportDto.MaxGroups + 7; i++)
        {
            var unit = Guid.Parse($"55555555-0000-0000-0000-{i:D12}");
            // Descending volume, so the cap has a defensible edge rather than an arbitrary one.
            rows.AddRange(Enumerable.Range(0, WorkReportDto.MaxGroups + 7 - i).Select(_ => Row(unit: unit)));
        }

        var report = WorkReportTally.Build(
            Criteria(groupBy: WorkReportGroupBy.OrganizationUnit),
            WorkReportSets.Of(rows, returns: new Dictionary<Guid, int>()));

        Assert.Equal(WorkReportDto.MaxGroups + 1, report.Groups.Count);   // 50 + the "other" bucket
        Assert.Equal(7, report.GroupsTruncated);
        Assert.Equal(WorkReportDto.OtherKey, report.Groups[^1].Key);

        // THE PARTS STILL ADD UP TO THE WHOLE — the property a silent cut destroys.
        Assert.Equal(report.Totals.Flow.Opened, report.Groups.Sum(g => g.Flow.Opened));
    }

    [Fact]
    public void Under_the_cap_nothing_is_folded_and_the_truncation_count_is_zero()
    {
        // Non-vacuity for the test above: the fold is conditional, not always-on.
        var rows = new[] { Row(unit: MyUnit), Row(unit: OtherUnit) };

        var report = WorkReportTally.Build(
            Criteria(groupBy: WorkReportGroupBy.OrganizationUnit),
            WorkReportSets.Of(rows, returns: new Dictionary<Guid, int>()));

        Assert.Equal(2, report.Groups.Count);
        Assert.Equal(0, report.GroupsTruncated);
        Assert.DoesNotContain(report.Groups, g => g.Key == WorkReportDto.OtherKey);
    }
}
