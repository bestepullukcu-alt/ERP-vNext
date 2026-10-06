using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// BL-417 option (a), DCP-004 "Decision amendment 2026-09-15" — THE TEAM LIST AND THE READ RULE GIVE ONE ANSWER.
///
/// <para><b>What was wrong.</b> Ekibim (<c>scope=team</c>) listed a subordinate's task; the single read
/// (<see cref="TaskReadAccessPolicy"/>) admitted the manager only through the same legal entity, a granted unit,
/// ReadAll or a direct relationship. A subordinate in ANOTHER legal entity with no unit grant showed on the list and
/// answered "not found" on the detail page.</para>
///
/// <para><b>How this is measured.</b> The REAL <see cref="TaskAssignmentScopeResolver"/> and
/// <see cref="TaskTeamResolver"/> over one org world, the REAL <c>TaskWorkItemProvider</c> building the Ekibim list,
/// and the REAL policy asked about every row on it. Only the stores are doubles.</para>
/// </summary>
public sealed class TaskTeamReadParityTests
{
    private static readonly Guid Me = TaskTestData.Me;

    private static readonly Guid HomeLegalEntity = Guid.Parse("41700000-0000-0000-0000-00000000000a");
    private static readonly Guid ForeignLegalEntity = Guid.Parse("41700000-0000-0000-0000-00000000000b");
    private static readonly Guid HomeUnit = Guid.Parse("41700000-0000-0000-0000-0000000000a1");
    private static readonly Guid ForeignUnit = Guid.Parse("41700000-0000-0000-0000-0000000000b1");

    private static readonly Guid MyPosition = Guid.Parse("41700000-0000-0000-0000-000000000b05");
    private static readonly Guid ReportPosition = Guid.Parse("41700000-0000-0000-0000-000000000a11");
    private static readonly Guid ForeignReportPosition = Guid.Parse("41700000-0000-0000-0000-000000000f11");
    private static readonly Guid StrangerPosition = Guid.Parse("41700000-0000-0000-0000-000000000057");

    private static readonly Guid Report = Guid.Parse("41700000-0000-0000-0000-00000000c0de");
    private static readonly Guid ForeignReport = Guid.Parse("41700000-0000-0000-0000-00000000f0de");
    private static readonly Guid Stranger = Guid.Parse("41700000-0000-0000-0000-00000000dead");

    [Fact]
    public async Task Every_task_on_the_team_list_is_readable_by_its_manager_across_two_legal_entities()
    {
        var world = new World();

        var list = await world.TeamListAsync();

        // Non-vacuity: the list really holds a subordinate from EACH legal entity, or parity proves nothing.
        var ids = list.Select(i => Guid.Parse(i.Id)).ToHashSet();
        Assert.Contains(world.ReportTask.Id, ids);
        Assert.Contains(world.ForeignReportTask.Id, ids);
        Assert.Equal(2, ids.Count);

        var policy = world.Policy();
        foreach (var id in ids)
        {
            var task = world.Tasks.Single(t => t.Id == id);
            Assert.True(await policy.CanReadAsync(task, Me, CancellationToken.None),
                $"'{task.Title}' is on the Ekibim list but the read rule refuses it.");
        }
    }

    [Fact]
    public async Task A_task_that_is_not_on_the_team_list_is_not_readable_through_it()
    {
        var world = new World();

        var ids = (await world.TeamListAsync()).Select(i => Guid.Parse(i.Id)).ToHashSet();

        Assert.DoesNotContain(world.StrangerTask.Id, ids);
        Assert.False(await world.Policy().CanReadAsync(world.StrangerTask, Me, CancellationToken.None));
    }

    [Fact]
    public async Task The_foreign_subordinates_task_is_admitted_by_the_subordinate_leg_and_by_nothing_else()
    {
        // Without a team, every other leg still runs over the same world — and refuses. So the admission above is
        // the subordinate leg's, which is what a sabotage of that leg has to turn red.
        var world = new World();

        Assert.False(await world.Policy(team: new FakeTaskTeamResolver())
            .CanReadAsync(world.ForeignReportTask, Me, CancellationToken.None));
        Assert.True(await world.Policy()
            .CanReadAsync(world.ForeignReportTask, Me, CancellationToken.None));
    }

    [Fact]
    public async Task The_subordinate_leg_answers_only_for_the_caller()
    {
        var world = new World();

        Assert.False(await world.Policy()
            .CanReadAsync(world.ForeignReportTask, TaskTestData.Rival, CancellationToken.None));
    }

    [Fact]
    public async Task Mention_validation_and_the_read_rule_agree_about_the_manager()
    {
        /*
         * The subordinate writes a comment on their own task and @mentions their manager. Validation admits a
         * candidate that the data legs list, else asks CanReadAsync for that candidate (TaskCommentHandlers). The
         * subordinate leg is caller-only, so BOTH say no: the manager is not a data-leg candidate, and a request
         * made by the subordinate cannot admit the manager through the manager's team. One rule.
         */
        var world = new World();
        var asSubordinate = world.Policy(caller: ForeignReport, team: new FakeTaskTeamResolver());

        var candidates = await asSubordinate.ResolveDataLegCandidatesAsync(world.ForeignReportTask, CancellationToken.None);

        Assert.DoesNotContain(Me, candidates);
        Assert.False(await asSubordinate.CanReadAsync(world.ForeignReportTask, Me, CancellationToken.None));
    }

    // ── the world ────────────────────────────────────────────────────────────────────────────────────────────

    private sealed class World
    {
        public OrganizationUnit[] Units { get; } =
        [
            Unit(HomeUnit, "FAC-A", HomeLegalEntity),
            Unit(ForeignUnit, "FAC-B", ForeignLegalEntity)
        ];

        public Position[] Positions { get; } =
        [
            Position(MyPosition, HomeUnit, "Boss"),
            Position(ReportPosition, HomeUnit, "Analyst", reportsTo: MyPosition),
            Position(ForeignReportPosition, ForeignUnit, "Plant Manager", reportsTo: MyPosition),
            Position(StrangerPosition, ForeignUnit, "Stranger")
        ];

        public PositionAssignment[] Assignments { get; } =
        [
            Holder(Me, MyPosition),
            Holder(Report, ReportPosition),
            Holder(ForeignReport, ForeignReportPosition),
            Holder(Stranger, StrangerPosition)
        ];

        public TaskItem ReportTask { get; } = Task(Report, HomeUnit, "home subordinate's own task");

        /// <summary>In the OTHER legal entity, in a unit nobody granted Me — the BL-417 case.</summary>
        public TaskItem ForeignReportTask { get; } = Task(ForeignReport, ForeignUnit, "foreign subordinate's own task");

        public TaskItem StrangerTask { get; } = Task(Stranger, ForeignUnit, "a stranger's task");

        public TaskItem MyTask { get; } = Task(Me, HomeUnit, "my own task");

        public TaskItem[] Tasks => [ReportTask, ForeignReportTask, StrangerTask, MyTask];

        /// <summary>Me's org scope, as MOD-0018-FU15 emits it: my position, my unit, my legal entity. No grant abroad.</summary>
        private static EntitlementDataScope[] MyScopes =>
        [
            new(EntitlementDataScopeKind.Position, MyPosition, "BOSS"),
            new(EntitlementDataScopeKind.OrgUnit, HomeUnit, "FAC-A"),
            new(EntitlementDataScopeKind.LegalEntity, HomeLegalEntity, "HOME")
        ];

        public Task<IReadOnlyList<WorkItemProjectionDto>> TeamListAsync()
            => TaskWorkItemProviderHarness
                .Create(Positions, Units, Assignments, Tasks, MyScopes)
                .GetWorkItemsAsync(
                    new WorkItemActor(Me, false, new HashSet<string>()) { Scope = WorkItemScope.Team },
                    CancellationToken.None);

        public ITaskReadAccessPolicy Policy(Guid? caller = null, ITaskTeamResolver? team = null)
        {
            var who = caller ?? Me;
            var scopes = new TaskAssignmentScopeResolver(
                new FakeDataScopeResolver(who == Me ? MyScopes : []),
                new FakePositionRepository(Positions),
                new FakeOrganizationUnitRepository(Units),
                new FakeTenantContext(TaskTestData.Tenant),
                new FakeCurrentUserContext(who));

            return new TaskReadAccessPolicy(
                new FakeTaskItemRepository(Tasks),
                new FakeTaskWatcherRepository(),
                new FakeTaskNotificationService(),
                new FakeOrganizationUnitRepository(Units),
                scopes,
                team ?? new TaskTeamResolver(scopes, new FakePositionAssignmentRepository(Assignments)),
                TaskActors.None(),
                new FakeCurrentUserContext(who));
        }

        private static TaskItem Task(Guid assignee, Guid unit, string title) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = TaskTestData.Tenant,
            Title = title,
            Lifecycle = TaskLifecycle.Open,
            Priority = TaskPriority.Medium,
            AssignmentTarget = TaskAssignmentTarget.Person,
            AssigneeUserId = assignee,
            CreatedByUserId = assignee,
            OrganizationUnitId = unit,
            CreatedBy = "tester"
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
}
