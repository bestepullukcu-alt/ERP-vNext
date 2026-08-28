using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// BL-023 PART A — the "My team" scope selector.
///
/// <para><b>Three concepts that must not blur.</b> Work assigned TO me is <i>İşlerim</i> (already there); work I
/// handed to somebody else is the Outbox (BL-016, not this round); my subordinates' OWN tasks — the ones I never
/// assigned — are this. A manager cannot see the third today at all.</para>
///
/// <para><b>Not a tab.</b> The axis law is locked: tab = OWNERSHIP, segment = STATE, chip = TYPE+SIGNAL. "My
/// team" changes whose ownership is being listed, so it is a SCOPE selector in the header and the tabs are
/// untouched (the SAP My Inbox shape).</para>
///
/// <para><b>The descent is not re-derived.</b> BL-057 already walks Position.ReportsToPositionId downward, once,
/// cycle-safe, in <see cref="TaskAssignmentScopeResolver"/>. A second walk here would be a second truth about the
/// same field, and the two would drift.</para>
/// </summary>
public sealed class TaskTeamScopeTests
{
    private static readonly Guid HomeLegalEntity = Guid.Parse("0a000000-0000-0000-0000-00000000000a");
    private static readonly Guid ForeignLegalEntity = Guid.Parse("0b000000-0000-0000-0000-00000000000b");
    private static readonly Guid HomeUnit = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ForeignUnit = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static readonly Guid MyPosition = Guid.Parse("31111111-1111-1111-1111-111111111111");
    private static readonly Guid ReportPosition = Guid.Parse("32222222-2222-2222-2222-222222222222");
    private static readonly Guid ForeignReportPosition = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid StrangerPosition = Guid.Parse("34444444-4444-4444-4444-444444444444");

    private static readonly Guid Report = Guid.Parse("c0000000-0000-0000-0000-00000000000c");
    private static readonly Guid ForeignReport = Guid.Parse("f1000000-0000-0000-0000-000000000001");
    private static readonly Guid Stranger = Guid.Parse("f0000000-0000-0000-0000-00000000000f");

    [Fact]
    public async Task My_team_shows_a_subordinates_OWN_task_that_I_never_assigned()
    {
        // The whole point of the item: the manager did not create it and is not the assignee.
        var team = await TeamUserIds();

        Assert.Contains(Report, team);
    }

    [Fact]
    public async Task A_subordinate_in_ANOTHER_company_is_in_my_team_when_the_chain_reaches_them()
    {
        // BL-057 leg (2): Position.ReportsToPositionId crosses the legal-entity boundary on purpose, and a
        // manager's team is defined by that chain rather than by the unit tree (which cannot cross it).
        var team = await TeamUserIds();

        Assert.Contains(ForeignReport, team);
    }

    [Fact]
    public async Task Somebody_outside_my_scope_is_NEVER_in_my_team()
    {
        /*
         * The same Allows() the assignment pickers use — not a second filter. A stranger in another company with
         * no chain to me is invisible here for exactly the reason they are unassignable.
         */
        var team = await TeamUserIds();

        Assert.DoesNotContain(Stranger, team);
    }

    [Fact]
    public async Task I_am_not_a_member_of_my_own_team()
    {
        // "My team" answers whose work I am supervising. My own work is the Ben scope, and listing it twice
        // would double every row the moment the two scopes are merged in the UI.
        var team = await TeamUserIds();

        Assert.DoesNotContain(TaskTestData.Me, team);
    }

    [Fact]
    public async Task A_user_with_no_subordinates_gets_an_EMPTY_team_and_the_caller_can_TELL()
    {
        /*
         * EMPTY-STATE DECISION, recorded here rather than left to the UI to invent.
         *
         * A silent empty list is the defect this project has corrected five times, so the resolver reports
         * "you have no team" as a distinguishable answer rather than as an empty list that looks like a
         * filtering accident. The UI then DISABLES the selector and says why — it is not hidden, because a
         * hidden control cannot explain itself and the user who expects a team would conclude the feature is
         * missing rather than that their org chart has no reports under them.
         */
        var world = World();
        var resolver = Resolver(world, scopes: MyScopes());

        // Nobody reports to a position I do not hold.
        var alone = await new TaskTeamResolver(resolver, new FakePositionAssignmentRepository(world.Assignments))
            .ResolveTeamAsync(CancellationToken.None);

        var noTeam = await new TaskTeamResolver(
                Resolver(world, scopes: [new EntitlementDataScope(
                    EntitlementDataScopeKind.Position, StrangerPosition, "STRANGER")]),
                new FakePositionAssignmentRepository(world.Assignments))
            .ResolveTeamAsync(CancellationToken.None);

        Assert.True(alone.HasTeam);
        Assert.False(noTeam.HasTeam);
        Assert.Empty(noTeam.UserIds);
    }

    // ── the provider honours the scope ───────────────────────────────────────

    [Fact]
    public async Task The_Ben_scope_still_returns_exactly_what_it_returned_before()
    {
        // Regression pin: adding a scope must not change the default answer by a single row.
        var mine = Task(assignee: TaskTestData.Me, title: "benim işim");
        var theirs = Task(assignee: Report, title: "astımın kendi işi");

        var items = await Project(WorkItemScope.Self, mine, theirs);

        Assert.Single(items);
        Assert.Equal("benim işim", items[0].Title.Text);
    }

    [Fact]
    public async Task The_Ekibim_scope_returns_the_subordinates_work_and_not_my_own()
    {
        var mine = Task(assignee: TaskTestData.Me, title: "benim işim");
        var theirs = Task(assignee: Report, title: "astımın kendi işi");

        var items = await Project(WorkItemScope.Team, mine, theirs);

        Assert.Single(items);
        Assert.Equal("astımın kendi işi", items[0].Title.Text);
    }

    [Fact]
    public async Task The_Ekibim_scope_does_not_leak_a_task_belonging_to_somebody_outside_my_scope()
    {
        var theirs = Task(assignee: Report, title: "astımın işi");
        var outside = Task(assignee: Stranger, title: "başka şirketin işi");

        var items = await Project(WorkItemScope.Team, theirs, outside);

        Assert.Single(items);
        Assert.DoesNotContain(items, i => i.Title.Text == "başka şirketin işi");
    }

    // ── the walk lives in ONE place ──────────────────────────────────────────

    [Fact]
    public void The_team_resolver_consumes_the_EXISTING_descent_rather_than_walking_the_chain_again()
    {
        // A second walk over Position.ReportsToPositionId would be a second truth about the same field.
        Assert.Contains(
            typeof(TaskTeamResolver).GetConstructors().Single().GetParameters(),
            p => p.ParameterType == typeof(ITaskAssignmentScopeResolver));
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static async Task<IReadOnlyCollection<Guid>> TeamUserIds()
    {
        var world = World();
        var team = await new TaskTeamResolver(
                Resolver(world, MyScopes()),
                new FakePositionAssignmentRepository(world.Assignments))
            .ResolveTeamAsync(CancellationToken.None);
        return team.UserIds;
    }

    private static async Task<IReadOnlyList<WorkItemProjectionDto>> Project(
        WorkItemScope scope, params TaskItem[] tasks)
    {
        var world = World();
        var provider = TaskWorkItemProviderHarness.Create(world.Positions, world.Units, world.Assignments, tasks);

        return await provider.GetWorkItemsAsync(
            new WorkItemActor(TaskTestData.Me, false, new HashSet<string>()) { Scope = scope },
            CancellationToken.None);
    }

    private static ITaskAssignmentScopeResolver Resolver(OrgWorld world, EntitlementDataScope[] scopes)
        => new TaskAssignmentScopeResolver(
            new FakeDataScopeResolver(scopes),
            new FakePositionRepository(world.Positions),
            new FakeOrganizationUnitRepository(world.Units),
            new FakeTenantContext(TaskTestData.Tenant),
            new FakeCurrentUserContext(TaskTestData.Me));

    private static EntitlementDataScope[] MyScopes() =>
    [
        new(EntitlementDataScopeKind.Position, MyPosition, "BOSS"),
        new(EntitlementDataScopeKind.OrgUnit, HomeUnit, "FAC-A"),
        new(EntitlementDataScopeKind.LegalEntity, HomeLegalEntity, "HOME")
    ];

    private static OrgWorld World() => new(
        Units:
        [
            Unit(HomeUnit, "FAC-A", HomeLegalEntity),
            Unit(ForeignUnit, "FAC-B", ForeignLegalEntity)
        ],
        Positions:
        [
            Position(MyPosition, HomeUnit, "Boss"),
            Position(ReportPosition, HomeUnit, "Analyst", reportsTo: MyPosition),
            Position(ForeignReportPosition, ForeignUnit, "Plant Manager", reportsTo: MyPosition),
            Position(StrangerPosition, ForeignUnit, "Stranger")
        ],
        Assignments:
        [
            Holder(TaskTestData.Me, MyPosition),
            Holder(Report, ReportPosition),
            Holder(ForeignReport, ForeignReportPosition),
            Holder(Stranger, StrangerPosition)
        ]);

    internal sealed record OrgWorld(
        OrganizationUnit[] Units, Position[] Positions, PositionAssignment[] Assignments);

    private static TaskItem Task(Guid assignee, string title) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TaskTestData.Tenant,
        Title = title,
        Lifecycle = TaskLifecycle.Open,
        Priority = TaskPriority.Medium,
        AssignmentTarget = TaskAssignmentTarget.Person,
        AssigneeUserId = assignee,
        // Deliberately created by the assignee themselves: this is their OWN work, not something handed down.
        CreatedByUserId = assignee,
        OrganizationUnitId = Guid.NewGuid()
    };

    private static PositionAssignment Holder(Guid userId, Guid positionId) => new()
    {
        TenantId = TaskTestData.Tenant,
        PositionId = positionId,
        UserId = userId,
        EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-30)
    };

    private static Position Position(Guid id, Guid unitId, string name, Guid? reportsTo = null) => new()
    {
        Id = id,
        TenantId = TaskTestData.Tenant,
        Code = name.Replace(' ', '-').ToUpperInvariant(),
        Name = name,
        OrganizationUnitId = unitId,
        ReportsToPositionId = reportsTo,
        Status = PositionStatus.Active
    };

    private static OrganizationUnit Unit(Guid id, string code, Guid legalEntityId) => new()
    {
        Id = id,
        TenantId = TaskTestData.Tenant,
        Code = code,
        Name = code,
        LegalEntityId = legalEntityId,
        Status = OrgUnitStatus.Active
    };
}
