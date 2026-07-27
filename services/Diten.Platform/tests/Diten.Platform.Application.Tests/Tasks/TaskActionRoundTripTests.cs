using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// A projected action must actually move the stored task, and the projection re-read afterwards must show it.
///
/// <para>The mock era failed exactly here: "Başlat" changed the row to "Devam ediyor" while the engine still held
/// <c>Open</c>. These tests run the whole loop — project → transition → project again — so a projected action that
/// does not persist cannot pass.</para>
/// </summary>
public sealed class TaskActionRoundTripTests
{
    [Fact]
    public async Task Start_moves_the_task_and_the_next_projection_shows_it()
    {
        var task = SelfTask();
        var repository = new FakeTaskItemRepository(task);
        var provider = Provider(repository);
        var actor = FullyPermittedActor();

        var before = Assert.Single(await provider.GetWorkItemsAsync(actor, CancellationToken.None));
        Assert.Equal("Open", before.TaskLifecycle);
        Assert.Equal("Pending", before.NormalizedStatus);
        Assert.Contains(before.Actions, a => a.Code == "start" && a.Enabled);

        var result = await Transition(repository, task.Id, TaskLifecycle.InProgress, before);

        Assert.True(result.IsSuccessful);
        // The STORED task moved — not merely the screen.
        Assert.Equal(TaskLifecycle.InProgress, repository.Items.Single().Lifecycle);

        var after = Assert.Single(await provider.GetWorkItemsAsync(actor, CancellationToken.None));
        Assert.Equal("InProgress", after.TaskLifecycle);
        Assert.Equal("InProgress", after.NormalizedStatus);
    }

    [Fact]
    public async Task A_stale_expected_version_is_refused_and_changes_nothing()
    {
        var task = SelfTask();
        var repository = new FakeTaskItemRepository(task);

        // Someone else advanced the task first.
        var firstProjection = Assert.Single(
            await Provider(repository).GetWorkItemsAsync(FullyPermittedActor(), CancellationToken.None));
        await Transition(repository, task.Id, TaskLifecycle.InProgress, firstProjection);

        // Our screen still holds the ORIGINAL token.
        var result = await Transition(repository, task.Id, TaskLifecycle.Done, firstProjection);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(TaskReasonCodes.ConcurrencyConflict, result.ReasonCode);
        // The losing write left no trace: still InProgress, not Done.
        Assert.Equal(TaskLifecycle.InProgress, repository.Items.Single().Lifecycle);
    }

    [Fact]
    public async Task Cancelling_makes_the_task_terminal_and_strips_its_actions()
    {
        var task = SelfTask();
        var repository = new FakeTaskItemRepository(task);
        var provider = Provider(repository);
        var actor = FullyPermittedActor();

        var before = Assert.Single(await provider.GetWorkItemsAsync(actor, CancellationToken.None));
        Assert.Contains(before.Actions, a => a.Code == "cancel");

        await Transition(repository, task.Id, TaskLifecycle.Cancelled, before);

        var after = Assert.Single(await provider.GetWorkItemsAsync(actor, CancellationToken.None));
        Assert.Equal("Cancelled", after.NormalizedStatus);
        // Contract rule: a terminal item offers no state-changing action, and no placement either.
        Assert.Empty(after.Actions);
        Assert.Null(after.PrimaryActionCode);
        Assert.Empty(after.OverflowActionCodes!);
    }

    // ── The Phase-1 action set and its placement ──────────────────────────────

    [Fact]
    public async Task An_open_self_task_offers_start_plan_and_cancel()
    {
        var item = await ProjectOne(SelfTask());

        Assert.Equal(new[] { "start", "plan", "cancel" }, item.Actions.Select(a => a.Code));
        Assert.Equal("start", item.PrimaryActionCode);
        // The rest populate the ··· menu, which used to be empty.
        Assert.Equal(new[] { "plan", "cancel" }, item.OverflowActionCodes);
    }

    [Fact]
    public async Task An_in_progress_task_offers_complete_as_primary()
    {
        var task = SelfTask();
        task.Lifecycle = TaskLifecycle.InProgress;

        var item = await ProjectOne(task);

        Assert.Equal("complete", item.PrimaryActionCode);
        Assert.Contains(item.Actions, a => a.Code == "cancel");
        // Planning is meaningless once the work has started.
        Assert.DoesNotContain(item.Actions, a => a.Code == "plan");
    }

    [Fact]
    public async Task A_claimed_pool_task_can_be_released_but_an_unclaimed_one_cannot()
    {
        var claimed = PoolTask();
        claimed.AssigneeUserId = TaskTestData.Me;
        var claimedItem = await ProjectOne(claimed, withHolder: true);
        Assert.Contains(claimedItem.Actions, a => a.Code == "release");

        var unclaimed = PoolTask();
        var unclaimedItem = await ProjectOne(unclaimed, withHolder: true);
        // Nothing to release — it has no holder; claiming is the primary move.
        Assert.DoesNotContain(unclaimedItem.Actions, a => a.Code == "release");
        Assert.Equal("claim", unclaimedItem.PrimaryActionCode);
    }

    [Fact]
    public async Task Placement_only_ever_references_actions_that_exist()
    {
        // The executable contract rejects a dangling primary/overflow reference, so the item would be DROPPED.
        foreach (var lifecycle in new[]
                 { TaskLifecycle.Open, TaskLifecycle.Planned, TaskLifecycle.InProgress, TaskLifecycle.Waiting })
        {
            var task = SelfTask();
            task.Lifecycle = lifecycle;
            var item = await ProjectOne(task);

            var codes = item.Actions.Select(a => a.Code).ToHashSet();
            if (item.PrimaryActionCode is not null)
            {
                Assert.Contains(item.PrimaryActionCode, codes);
            }

            Assert.All(item.OverflowActionCodes!, code => Assert.Contains(code, codes));
            Assert.DoesNotContain(item.PrimaryActionCode, item.OverflowActionCodes!);
            Assert.Equal(item.OverflowActionCodes!.Count, item.OverflowActionCodes!.Distinct().Count());
        }
    }

    [Fact]
    public async Task Phase_2_and_3_commands_are_not_projected()
    {
        // Projecting a button with no endpoint behind it is what the mock era did.
        var open = await ProjectOne(SelfTask());
        var running = SelfTask();
        running.Lifecycle = TaskLifecycle.InProgress;
        var active = await ProjectOne(running);

        var codes = open.Actions.Concat(active.Actions).Select(a => a.Code).ToHashSet();
        foreach (var absent in new[] { "pause", "resume", "logTime", "requestInfo", "signoff", "return", "resolve" })
        {
            Assert.DoesNotContain(absent, codes);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task<WorkItemProjectionDto> ProjectOne(TaskItem task, bool withHolder = false)
    {
        var repository = new FakeTaskItemRepository(task);
        var provider = withHolder ? Provider(repository, Holder()) : Provider(repository);
        return Assert.Single(await provider.GetWorkItemsAsync(FullyPermittedActor(), CancellationToken.None));
    }

    private static Task<Application.Common.Response<Application.Common.NoContent>> Transition(
        FakeTaskItemRepository repository,
        Guid id,
        TaskLifecycle target,
        WorkItemProjectionDto projection)
    {
        // The expected version comes from the PROJECTION's concurrency token, exactly as the browser sends it.
        var expectedVersion = int.Parse(projection.Concurrency.Token);
        var handler = new TransitionTaskItemHandler(
            repository, new TaskLifecycleService(), new FakeCurrentUserContext(TaskTestData.Me),
            new FakeChecklistRunRepository(), new TaskChecklistService(), new FakeWorkflowTransitionGate());

        return handler.Handle(
            new TransitionTaskItemCommand(id, target, new TaskTransitionRequest(expectedVersion, null, null), "corr"),
            CancellationToken.None);
    }

    private static TaskWorkItemProvider Provider(
        FakeTaskItemRepository repository,
        Domain.Entities.Organization.PositionAssignment? holder = null)
        => new(
            repository,
            holder is null ? new FakePositionAssignmentRepository() : new FakePositionAssignmentRepository(holder),
            new TaskLifecycleService(),
            new TaskAssignmentResolver(),
            new FakeUserDisplayNameResolver(),
            new FakeChecklistRunRepository(), new FakeTaskApprovalService());

    /// <summary>An actor holding everything the provider declares, so permissions never mask a placement bug.</summary>
    private static WorkItemActor FullyPermittedActor()
        => new(TaskTestData.Me, IsPlatformActor: false, new HashSet<string>(
            new[] { TaskPermissions.Update, TaskPermissions.Claim, TaskPermissions.Complete, TaskPermissions.Cancel },
            StringComparer.OrdinalIgnoreCase));

    private static readonly Guid PositionId = Guid.Parse("88888888-8888-8888-8888-888888888888");

    private static TaskItem SelfTask() => new()
    {
        TenantId = TaskTestData.Tenant,
        Title = "CT probe",
        AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
        AssigneeUserId = TaskTestData.Me,
        // A self-assigned task is created BY the person it is assigned to, and CreateTaskItemHandler always
        // stamps CreatedByUserId. Omitting it here made the fixture describe a task with no requester, which
        // stopped mattering only because `cancel` used to be projected for everyone.
        CreatedByUserId = TaskTestData.Me,
        OrganizationUnitId = Guid.NewGuid(),
        Lifecycle = TaskLifecycle.Open,
        Version = 1
    };

    private static TaskItem PoolTask()
    {
        var task = SelfTask();
        task.AssignmentTarget = TaskAssignmentTarget.PositionPool;
        task.AssigneeUserId = null;
        task.PoolPositionId = PositionId;
        return task;
    }

    private static Domain.Entities.Organization.PositionAssignment Holder() => new()
    {
        TenantId = TaskTestData.Tenant,
        PositionId = PositionId,
        UserId = TaskTestData.Me,
        EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1),
        EffectiveTo = null
    };
}
