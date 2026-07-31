using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// WC-4 — the four events that now dispatch, each from the handler that owns the act.
///
/// <para>The manifest declared five events and exactly one was ever dispatched. These pin the other three, and
/// re-pin the first — because the first went out addressed to a GUID, which is the same as not going out.</para>
/// </summary>
public sealed class TaskNotificationDispatchPointsTests
{
    private static readonly Guid Me = TaskTestData.Me;
    private static readonly Guid Rival = TaskTestData.Rival;
    private static readonly Guid Unit = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid PositionId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    // ── assigned ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Assigning_a_task_to_a_PERSON_notifies_them()
    {
        var harness = new Harness();

        await harness.CreateAsync(TaskAssignmentTarget.Person, assignee: Rival);

        var sent = Assert.Single(harness.Notifications.Notifications);
        Assert.Equal(TaskNotificationEvents.Assigned, sent.EventCode);
        Assert.Equal([Rival], sent.Candidates);
    }

    [Fact]
    public async Task Creating_a_task_for_MYSELF_notifies_nobody()
    {
        // The actor rule. "You assigned yourself a task" is the purest form of the noise this rule prevents.
        var harness = new Harness();

        await harness.CreateAsync(TaskAssignmentTarget.SelfAssigned);

        Assert.Empty(harness.Notifications.Notifications);
    }

    [Fact]
    public async Task Pooling_a_task_notifies_every_holder_of_the_position()
    {
        var harness = new Harness();
        harness.Notifications.PoolHolders.AddRange([Rival, Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")]);

        await harness.CreateAsync(TaskAssignmentTarget.PositionPool, poolPositionId: PositionId);

        var sent = Assert.Single(harness.Notifications.Notifications);
        Assert.Equal(TaskNotificationEvents.Assigned, sent.EventCode);
        Assert.Equal(2, sent.Candidates.Count);
    }

    // ── approvalrequested ────────────────────────────────────────────────────

    [Fact]
    public async Task A_task_needing_APPROVAL_notifies_the_manager()
    {
        /*
         * Declared in the manifest since Phase 1 with the comment "dispatched in Phase 3 when the MOD-0023
         * handoff lands". It landed; the dispatch did not.
         */
        var harness = new Harness();

        await harness.CreateAsync(
            TaskAssignmentTarget.Person, assignee: Rival, approvalManager: Rival);

        Assert.Contains(TaskNotificationEvents.ApprovalRequested, harness.Notifications.EventCodes);
        var approval = harness.Notifications.Notifications
            .Single(n => n.EventCode == TaskNotificationEvents.ApprovalRequested);
        Assert.Equal([Rival], approval.Candidates);
    }

    [Fact]
    public async Task A_task_needing_NO_approval_sends_no_approval_notification()
    {
        // Non-vacuity: an unconditional dispatch would tell a manager who was never asked for anything.
        var harness = new Harness();

        await harness.CreateAsync(TaskAssignmentTarget.Person, assignee: Rival);

        Assert.DoesNotContain(TaskNotificationEvents.ApprovalRequested, harness.Notifications.EventCodes);
    }

    // ── claimed ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Claiming_a_pooled_task_notifies_the_REQUESTER()
    {
        // They asked for the work and cannot otherwise know who picked it up.
        var harness = new Harness();
        var task = PoolTask(createdBy: Rival);
        var tasks = new FakeTaskItemRepository(task);

        await harness.ClaimHandler(tasks).Handle(
            new ClaimTaskItemCommand(task.Id, new ClaimTaskItemRequest(1), "corr"), CancellationToken.None);

        var sent = Assert.Single(harness.Notifications.Notifications);
        Assert.Equal(TaskNotificationEvents.Claimed, sent.EventCode);
        Assert.Equal([Rival], sent.Candidates);
    }

    [Fact]
    public async Task Claiming_a_task_I_created_MYSELF_notifies_nobody()
    {
        var harness = new Harness();
        var task = PoolTask(createdBy: Me);
        var tasks = new FakeTaskItemRepository(task);

        await harness.ClaimHandler(tasks).Handle(
            new ClaimTaskItemCommand(task.Id, new ClaimTaskItemRequest(1), "corr"), CancellationToken.None);

        Assert.Empty(harness.Notifications.Notifications);
    }

    [Fact]
    public async Task A_FAILED_claim_notifies_nobody()
    {
        // The notification belongs to the act, not to the attempt. A stale-version claim changed nothing, so
        // telling the requester their work was taken would be a lie.
        var harness = new Harness();
        var task = PoolTask(createdBy: Rival);
        task.AssigneeUserId = Rival;   // already claimed
        var tasks = new FakeTaskItemRepository(task);

        var result = await harness.ClaimHandler(tasks).Handle(
            new ClaimTaskItemCommand(task.Id, new ClaimTaskItemRequest(1), "corr"), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Empty(harness.Notifications.Notifications);
    }

    // ── completed ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Completing_a_task_notifies_the_REQUESTER()
    {
        var harness = new Harness();
        var task = OwnedTask(createdBy: Rival);
        var tasks = new FakeTaskItemRepository(task);

        var result = await harness.TransitionHandler(tasks).Handle(
            new TransitionTaskItemCommand(task.Id, TaskLifecycle.Done, new TaskTransitionRequest(1, null, null), "corr"),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        var sent = Assert.Single(harness.Notifications.Notifications);
        Assert.Equal(TaskNotificationEvents.Completed, sent.EventCode);
        Assert.Equal([Rival], sent.Candidates);
    }

    [Fact]
    public async Task Completing_MY_OWN_task_notifies_nobody()
    {
        var harness = new Harness();
        var task = OwnedTask(createdBy: Me);
        var tasks = new FakeTaskItemRepository(task);

        await harness.TransitionHandler(tasks).Handle(
            new TransitionTaskItemCommand(task.Id, TaskLifecycle.Done, new TaskTransitionRequest(1, null, null), "corr"),
            CancellationToken.None);

        Assert.Empty(harness.Notifications.Notifications);
    }

    [Fact]
    public async Task Any_OTHER_transition_notifies_nobody()
    {
        // Non-vacuity for the completion tests: a dispatch on every transition would tell the requester about
        // every pause, start and comment.
        var harness = new Harness();
        var task = OwnedTask(createdBy: Rival);
        task.Lifecycle = TaskLifecycle.Open;
        var tasks = new FakeTaskItemRepository(task);

        await harness.TransitionHandler(tasks).Handle(
            new TransitionTaskItemCommand(task.Id, TaskLifecycle.InProgress, new TaskTransitionRequest(1, null, null), "corr"),
            CancellationToken.None);

        Assert.Empty(harness.Notifications.Notifications);
    }

    // ── the opt-out, and the never-fail rule, at handler level ───────────────

    [Fact]
    public async Task A_task_with_notifications_OFF_dispatches_nothing_anywhere()
    {
        var harness = new Harness();
        var task = OwnedTask(createdBy: Rival);
        task.EmailNotificationsEnabled = false;
        var tasks = new FakeTaskItemRepository(task);

        await harness.TransitionHandler(tasks).Handle(
            new TransitionTaskItemCommand(task.Id, TaskLifecycle.Done, new TaskTransitionRequest(1, null, null), "corr"),
            CancellationToken.None);

        Assert.Empty(harness.Notifications.Notifications);
    }

    [Fact]
    public async Task A_notification_that_THROWS_does_not_fail_the_write()
    {
        /*
         * The rule the whole feature hangs on. A task the user completed must stay completed even if the mail
         * system is on fire — anything else makes the notification more important than the work.
         */
        var harness = new Harness();
        harness.Notifications.Throws = true;
        var task = OwnedTask(createdBy: Rival);
        var tasks = new FakeTaskItemRepository(task);

        var result = await harness.TransitionHandler(tasks).Handle(
            new TransitionTaskItemCommand(task.Id, TaskLifecycle.Done, new TaskTransitionRequest(1, null, null), "corr"),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(TaskLifecycle.Done, tasks.Items.Single().Lifecycle);
    }

    [Fact]
    public async Task And_a_throwing_notification_does_not_fail_a_CREATE_either()
    {
        var harness = new Harness();
        harness.Notifications.Throws = true;

        var result = await harness.CreateAsync(TaskAssignmentTarget.Person, assignee: Rival);

        Assert.True(result.IsSuccessful);
        Assert.Single(harness.Tasks.Items);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static TaskItem PoolTask(Guid createdBy) => new()
    {
        TenantId = TaskTestData.Tenant,
        Title = "Havuz görevi",
        AssignmentTarget = TaskAssignmentTarget.PositionPool,
        PoolPositionId = PositionId,
        AssigneeUserId = null,
        CreatedByUserId = createdBy,
        OrganizationUnitId = Unit,
        Lifecycle = TaskLifecycle.Open,
        EmailNotificationsEnabled = true,
        Version = 1
    };

    private static TaskItem OwnedTask(Guid createdBy) => new()
    {
        TenantId = TaskTestData.Tenant,
        Title = "Benim görevim",
        AssignmentTarget = TaskAssignmentTarget.Person,
        AssigneeUserId = Me,
        CreatedByUserId = createdBy,
        OrganizationUnitId = Unit,
        Lifecycle = TaskLifecycle.InProgress,
        EmailNotificationsEnabled = true,
        Version = 1
    };

    private sealed class Harness
    {
        public Harness()
        {
            Tasks = new FakeTaskItemRepository();
            Notifications = new FakeTaskNotificationService();
        }

        public FakeTaskItemRepository Tasks { get; }

        public FakeTaskNotificationService Notifications { get; }

        public Task<Response<Guid>> CreateAsync(
            TaskAssignmentTarget target,
            Guid? assignee = null,
            Guid? poolPositionId = null,
            Guid? approvalManager = null)
            => new CreateTaskItemHandler(
                    Tasks,
                    new FakeTaskAssignmentRepository(),
                    new FakeTaskWatcherRepository(),
                    new FakePositionRepository(new Position
                    {
                        Id = PositionId,
                        TenantId = TaskTestData.Tenant,
                        Code = "OPS",
                        Name = "Operasyon",
                        Status = PositionStatus.Active,
                        OrganizationUnitId = Unit
                    }),
                    new FakeOrganizationUnitRepository(new OrganizationUnit
                    {
                        Id = Unit,
                        TenantId = TaskTestData.Tenant,
                        Code = "HQ",
                        Name = "Genel Merkez",
                        LegalEntityId = Guid.NewGuid()
                    }),
                    new FakePositionAssignmentRepository(),
                    new TaskFieldDefinitionService(new FakeTaskFieldDefinitionRepository()),
                    new TaskLifecycleService(),
                    new FakeTaskApprovalService(),
                    new FakeChecklistTemplateRepository(),
                    new FakeChecklistRunRepository(),
                    new TaskChecklistService(),
                    Notifications,
                    new FakeCurrentUserContext(Me),
                    new FakeTenantContext(TaskTestData.Tenant),
                    NullLogger<CreateTaskItemHandler>.Instance)
                .Handle(
                    new CreateTaskItemCommand(
                        new CreateTaskItemRequest(
                            Title: "Yeni görev",
                            Description: null,
                            Priority: TaskPriority.Medium,
                            AssignmentTarget: target,
                            AssigneeUserId: assignee,
                            PoolPositionId: poolPositionId,
                            OrganizationUnitId: Unit,
                            DueAt: DateTimeOffset.UtcNow.AddDays(3),
                            StartAt: null,
                            PlannedDate: null,
                            EstimateHours: null,
                            Tags: null,
                            ReviewRequired: false,
                            ApprovalRequired: approvalManager is not null,
                            ApprovalManagerUserId: approvalManager,
                            EmailNotificationsEnabled: true,
                            DelegationAllowed: false,
                            FieldValues: null,
                            Watchers: null),
                        "corr"),
                    CancellationToken.None);

        public ClaimTaskItemHandler ClaimHandler(FakeTaskItemRepository tasks)
            => new(
                tasks,
                new FakeTaskAssignmentRepository(),
                new FakePositionAssignmentRepository(new PositionAssignment
                {
                    TenantId = TaskTestData.Tenant,
                    PositionId = PositionId,
                    UserId = Me,
                    EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1),
                    EffectiveTo = null
                }),
                new FakeCurrentUserContext(Me),
                new FakeTenantContext(TaskTestData.Tenant),
                Notifications,
                NullLogger<ClaimTaskItemHandler>.Instance);

        public TransitionTaskItemHandler TransitionHandler(FakeTaskItemRepository tasks)
            => new(
                tasks,
                new TaskLifecycleService(),
                new FakeCurrentUserContext(Me),
                new FakeChecklistRunRepository(),
                new TaskChecklistService(),
                new PassingWorkflowGate(),
                new FakeTaskDependencyRepository(),
                Notifications,
                NullLogger<TransitionTaskItemHandler>.Instance);
    }
}
