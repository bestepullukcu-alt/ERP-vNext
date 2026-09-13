using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// BL-349 — the read-access rule itself, exhaustively: every leg that admits a reader (assignee, pool holder,
/// creator, watcher, the PARENT task's assignee/pool, the actor's assignment scope, and the ReadAll permission),
/// the baseline refusal for someone with none of those, and the documented narrowing that the scope and ReadAll
/// legs answer only for the CURRENT caller. Handler-level wiring (byte-identical 404, the right reason code) is
/// proven separately, once per endpoint, against a policy double — this file is the one place the RULE itself is
/// measured, so nothing here is re-derived at any call site.
/// </summary>
public sealed class TaskReadAccessPolicyTests
{
    private static readonly Guid Me = TaskTestData.Me;
    private static readonly Guid Rival = TaskTestData.Rival;
    private static readonly Guid Other = TaskTestData.Other;
    private static readonly Guid Watcher = TaskTestData.Watcher;

    private sealed class Harness
    {
        public FakeTaskItemRepository Tasks { get; } = new();
        public FakeTaskWatcherRepository Watchers { get; } = new();
        public FakeTaskNotificationService Notifications { get; } = new();
        public FakeOrganizationUnitRepository OrganizationUnits { get; init; } = new();
        public ITaskAssignmentScopeResolver Scope { get; init; } = new EmptyScopeResolver();
        public FakeActorPermissions Permissions { get; init; } = TaskActors.None();
        public Guid CurrentUser { get; init; } = Me;

        public ITaskReadAccessPolicy Build() => new TaskReadAccessPolicy(
            Tasks, Watchers, Notifications, OrganizationUnits, Scope, Permissions,
            new FakeCurrentUserContext(CurrentUser));
    }

    private sealed class EmptyScopeResolver : ITaskAssignmentScopeResolver
    {
        public Task<TaskAssignmentScope> ResolveAsync(CancellationToken ct) => Task.FromResult(TaskAssignmentScope.Empty);
    }

    private static TaskItem NewTask(
        Guid? assignee = null, Guid? createdBy = null, Guid? parentId = null,
        TaskAssignmentTarget target = TaskAssignmentTarget.Person, Guid? poolPositionId = null) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TaskTestData.Tenant,
        Title = "Read access probe",
        Lifecycle = TaskLifecycle.Open,
        AssignmentTarget = target,
        AssigneeUserId = assignee,
        PoolPositionId = poolPositionId,
        CreatedByUserId = createdBy ?? Other,
        ParentTaskItemId = parentId,
        OrganizationUnitId = Guid.NewGuid(),
        CreatedBy = "tester"
    };

    // ── the direct legs ─────────────────────────────────────────────────

    [Fact]
    public async Task The_assignee_can_read_the_task()
    {
        var h = new Harness();
        var task = NewTask(assignee: Me, createdBy: Other);
        h.Tasks.CreateAsync(task, CancellationToken.None).GetAwaiter().GetResult();

        Assert.True(await h.Build().CanReadAsync(task, Me, CancellationToken.None));
    }

    [Fact]
    public async Task A_current_pool_holder_can_read_the_task()
    {
        var h = new Harness();
        var task = NewTask(target: TaskAssignmentTarget.PositionPool, poolPositionId: Guid.NewGuid());
        h.Notifications.PoolHoldersByTaskId[task.Id] = [Me];

        Assert.True(await h.Build().CanReadAsync(task, Me, CancellationToken.None));
    }

    [Fact]
    public async Task The_creator_can_read_the_task_even_when_not_the_assignee()
    {
        var h = new Harness();
        var task = NewTask(assignee: Rival, createdBy: Me);

        Assert.True(await h.Build().CanReadAsync(task, Me, CancellationToken.None));
    }

    [Fact]
    public async Task A_watcher_can_read_the_task()
    {
        var h = new Harness();
        var task = NewTask(assignee: Rival, createdBy: Other);
        h.Watchers.CreateAsync(new TaskWatcher { TenantId = TaskTestData.Tenant, TaskItemId = task.Id, UserId = Me }, CancellationToken.None)
            .GetAwaiter().GetResult();

        Assert.True(await h.Build().CanReadAsync(task, Me, CancellationToken.None));
    }

    // ── the parent leg (WCN subtask panel: app.js:5444/5853, subtask-add: 7756) ────────────────────

    [Fact]
    public async Task The_parent_tasks_assignee_can_read_the_subtask()
    {
        var h = new Harness();
        var parent = NewTask(assignee: Me, createdBy: Other);
        h.Tasks.CreateAsync(parent, CancellationToken.None).GetAwaiter().GetResult();
        var subtask = NewTask(assignee: Rival, createdBy: Other, parentId: parent.Id);

        Assert.True(await h.Build().CanReadAsync(subtask, Me, CancellationToken.None));
    }

    [Fact]
    public async Task The_parent_tasks_pool_holder_can_read_the_subtask()
    {
        var h = new Harness();
        var parent = NewTask(target: TaskAssignmentTarget.PositionPool, poolPositionId: Guid.NewGuid());
        h.Tasks.CreateAsync(parent, CancellationToken.None).GetAwaiter().GetResult();
        h.Notifications.PoolHoldersByTaskId[parent.Id] = [Me];
        var subtask = NewTask(assignee: Rival, createdBy: Other, parentId: parent.Id);

        Assert.True(await h.Build().CanReadAsync(subtask, Me, CancellationToken.None));
    }

    [Fact]
    public async Task The_parents_creator_does_NOT_extend_to_the_subtask()
    {
        // Narrower on purpose (SOP): the prompt names only the parent's assignee and pool, not its creator or
        // watchers. A subtask must not become readable through a leg the rule was never given for the parent.
        var h = new Harness();
        var parent = NewTask(assignee: Rival, createdBy: Me);
        h.Tasks.CreateAsync(parent, CancellationToken.None).GetAwaiter().GetResult();
        var subtask = NewTask(assignee: Rival, createdBy: Other, parentId: parent.Id);

        Assert.False(await h.Build().CanReadAsync(subtask, Me, CancellationToken.None));
    }

    [Fact]
    public async Task The_parents_watcher_does_NOT_extend_to_the_subtask()
    {
        var h = new Harness();
        var parent = NewTask(assignee: Rival, createdBy: Other);
        h.Tasks.CreateAsync(parent, CancellationToken.None).GetAwaiter().GetResult();
        h.Watchers.CreateAsync(new TaskWatcher { TenantId = TaskTestData.Tenant, TaskItemId = parent.Id, UserId = Me }, CancellationToken.None)
            .GetAwaiter().GetResult();
        var subtask = NewTask(assignee: Rival, createdBy: Other, parentId: parent.Id);

        Assert.False(await h.Build().CanReadAsync(subtask, Me, CancellationToken.None));
    }

    [Fact]
    public async Task A_dangling_ParentTaskItemId_that_resolves_to_nothing_grants_no_extra_access()
    {
        // The parent id points at a row the tenant-scoped repository will not return (deleted, or simply gone) —
        // the leg must fail closed, not throw and not silently admit.
        var h = new Harness();
        var subtask = NewTask(assignee: Rival, createdBy: Other, parentId: Guid.NewGuid());

        Assert.False(await h.Build().CanReadAsync(subtask, Me, CancellationToken.None));
    }

    // ── the scope leg (BL-057's AllowsUnit, asked for CURRENT caller only) ─────────────────

    [Fact]
    public async Task An_actor_whose_scope_covers_the_tasks_unit_can_read_it()
    {
        var unit = new OrganizationUnit
        {
            Id = Guid.NewGuid(), TenantId = TaskTestData.Tenant, Code = "U1", Name = "Unit 1",
            LegalEntityId = Guid.NewGuid()
        };
        var h = new Harness
        {
            OrganizationUnits = new FakeOrganizationUnitRepository(unit),
            Scope = new TaskAssignmentScopeResolver(
                new FakeDataScopeResolver(new EntitlementDataScope(EntitlementDataScopeKind.OrgUnit, unit.Id, "granted")),
                new FakePositionRepository(),
                new FakeOrganizationUnitRepository(unit),
                new FakeTenantContext(TaskTestData.Tenant),
                new FakeCurrentUserContext(Me))
        };
        var task = NewTask(assignee: Rival, createdBy: Other);
        task.OrganizationUnitId = unit.Id;

        Assert.True(await h.Build().CanReadAsync(task, Me, CancellationToken.None));
    }

    [Fact]
    public async Task An_actor_whose_scope_covers_a_DIFFERENT_unit_cannot_read_the_task()
    {
        var granted = new OrganizationUnit
        {
            Id = Guid.NewGuid(), TenantId = TaskTestData.Tenant, Code = "GRANTED", Name = "Granted",
            LegalEntityId = Guid.NewGuid()
        };
        var taskUnit = new OrganizationUnit
        {
            Id = Guid.NewGuid(), TenantId = TaskTestData.Tenant, Code = "OTHER", Name = "Other unit",
            LegalEntityId = Guid.NewGuid()
        };
        var h = new Harness
        {
            OrganizationUnits = new FakeOrganizationUnitRepository(granted, taskUnit),
            Scope = new TaskAssignmentScopeResolver(
                new FakeDataScopeResolver(new EntitlementDataScope(EntitlementDataScopeKind.OrgUnit, granted.Id, "granted")),
                new FakePositionRepository(),
                new FakeOrganizationUnitRepository(granted, taskUnit),
                new FakeTenantContext(TaskTestData.Tenant),
                new FakeCurrentUserContext(Me))
        };
        var task = NewTask(assignee: Rival, createdBy: Other);
        task.OrganizationUnitId = taskUnit.Id;

        Assert.False(await h.Build().CanReadAsync(task, Me, CancellationToken.None));
    }

    // ── the ReadAll leg ──────────────────────────────────────────────────

    [Fact]
    public async Task A_holder_of_ReadAll_can_read_any_task()
    {
        var h = new Harness { Permissions = TaskActors.Holding(TaskPermissions.ReadAll) };
        var task = NewTask(assignee: Rival, createdBy: Other);

        Assert.True(await h.Build().CanReadAsync(task, Me, CancellationToken.None));
    }

    // ── the baseline refusal ─────────────────────────────────────────────

    [Fact]
    public async Task An_unrelated_person_with_no_scope_and_no_ReadAll_cannot_read_the_task()
    {
        var h = new Harness();
        var task = NewTask(assignee: Rival, createdBy: Other);

        Assert.False(await h.Build().CanReadAsync(task, Me, CancellationToken.None));
        Assert.False(await h.Build().CanReadAsync(task, Watcher, CancellationToken.None));
    }

    // ── the documented narrowing: scope and ReadAll answer only for the CURRENT caller ────

    [Fact]
    public async Task ReadAll_does_not_extend_to_an_actor_other_than_the_current_caller()
    {
        // The caller (Me) holds ReadAll, but the policy is asked about Rival's access — a future @mention-style
        // caller must not get a false "yes" just because the REQUEST happens to be authenticated as somebody
        // who holds ReadAll. Only the data legs generalize to an arbitrary actorUserId; see the interface note.
        var h = new Harness { CurrentUser = Me, Permissions = TaskActors.Holding(TaskPermissions.ReadAll) };
        var task = NewTask(assignee: Other, createdBy: Other);

        Assert.False(await h.Build().CanReadAsync(task, Rival, CancellationToken.None));
    }

    [Fact]
    public async Task Scope_does_not_extend_to_an_actor_other_than_the_current_caller()
    {
        var unit = new OrganizationUnit
        {
            Id = Guid.NewGuid(), TenantId = TaskTestData.Tenant, Code = "U2", Name = "Unit 2",
            LegalEntityId = Guid.NewGuid()
        };
        var h = new Harness
        {
            CurrentUser = Me,
            OrganizationUnits = new FakeOrganizationUnitRepository(unit),
            Scope = new TaskAssignmentScopeResolver(
                new FakeDataScopeResolver(new EntitlementDataScope(EntitlementDataScopeKind.OrgUnit, unit.Id, "granted")),
                new FakePositionRepository(),
                new FakeOrganizationUnitRepository(unit),
                new FakeTenantContext(TaskTestData.Tenant),
                new FakeCurrentUserContext(Me))
        };
        var task = NewTask(assignee: Other, createdBy: Other);
        task.OrganizationUnitId = unit.Id;

        Assert.False(await h.Build().CanReadAsync(task, Rival, CancellationToken.None));
    }
}
