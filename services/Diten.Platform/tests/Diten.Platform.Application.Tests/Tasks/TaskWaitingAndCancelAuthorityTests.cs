using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// Two rules that the PROJECTION alone cannot enforce, asserted on the <see cref="Response{T}"/> the controller
/// turns verbatim into the HTTP response — status code and reason code are the wire.
///
/// <para>Entering Waiting (<c>inquire</c>) and refusing someone else's <c>cancel</c> are both cases where hiding
/// or showing a button is presentation and the refusal is the rule. Each is followed by a re-read of the stored
/// task, because a handler that returns 204 while writing nothing looks identical from the response alone.</para>
/// </summary>
public sealed class TaskWaitingAndCancelAuthorityTests
{
    // ── inquire: the ENTRY to Waiting ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Inquire_parks_the_task_in_waiting_and_stores_what_it_waits_for()
    {
        var task = OwnedTask(TaskLifecycle.InProgress);
        var repository = new FakeTaskItemRepository(task);

        var response = await Inquire(repository, task, "Waiting on the supplier's revised quote");

        Assert.Equal(204, response.StatusCode);

        // Re-read the STORED task: the response says it worked, this says something actually changed.
        var stored = repository.Items.Single();
        Assert.Equal(TaskLifecycle.Waiting, stored.Lifecycle);
        Assert.Equal("Waiting on the supplier's revised quote", stored.WaitingReason);
    }

    [Fact]
    public async Task The_stored_wait_is_projected_so_the_Bekleyen_segment_can_fill()
    {
        var task = OwnedTask(TaskLifecycle.InProgress);
        // A due date the projection could WRONGLY reuse as the wait's end. Without one here the ExpectedUntil
        // assertion below passes whether or not the bug is present.
        task.DueAt = new DateTimeOffset(2026, 7, 22, 17, 0, 0, TimeSpan.Zero);
        var repository = new FakeTaskItemRepository(task);
        await Inquire(repository, task, "Blocked on legal review");

        var item = Assert.Single(await Provider(repository).GetWorkItemsAsync(Actor(), CancellationToken.None));

        // The Task Center's "Bekleyen" segment keys off normalizedStatus, so this is what revives the dead state.
        Assert.Equal("Waiting", item.NormalizedStatus);
        Assert.Equal("Waiting", item.TaskLifecycle);
        Assert.NotNull(item.WaitingContext);
        // The contract requires waitingContext ⇔ Waiting in BOTH directions.
        Assert.Equal("externalInformation", item.WaitingContext!.Type);

        /*
         * WHY goes in `reason`, as the user's own text. It used to be sent as `waitingOn`, where the client reads
         * `.displayName` off what it expects to be a person — so the sentence was on the wire and rendered as
         * nothing. `waitingOn` answers WHO, and nothing resolves an identity yet, so it is null rather than
         * carrying something that is not one.
         */
        Assert.Null(item.WaitingContext.WaitingOn);
        Assert.NotNull(item.WaitingContext.Reason);
        Assert.Equal("display", item.WaitingContext.Reason!.Kind);
        Assert.Equal("Blocked on legal review", item.WaitingContext.Reason.Text);
        // Nothing collects when the WAIT ends. Copying the task's own due date announced a deadline already past.
        Assert.Null(item.WaitingContext.ExpectedUntil);
    }

    [Fact]
    public async Task Waiting_without_saying_what_for_is_refused()
    {
        var task = OwnedTask(TaskLifecycle.InProgress);
        var repository = new FakeTaskItemRepository(task);

        var response = await Inquire(repository, task, "   ");

        Assert.Equal(400, response.StatusCode);
        Assert.Equal(TaskReasonCodes.WaitingReasonRequired, response.ReasonCode);
        // Nothing moved.
        Assert.Equal(TaskLifecycle.InProgress, repository.Items.Single().Lifecycle);
    }

    [Fact]
    public async Task Only_the_holder_may_declare_a_wait()
    {
        var task = OwnedTask(TaskLifecycle.InProgress);
        task.AssigneeUserId = TaskTestData.Rival;
        var repository = new FakeTaskItemRepository(task);

        var response = await Inquire(repository, task, "Not my work to park");

        Assert.Equal(403, response.StatusCode);
        Assert.Equal(TaskLifecycle.InProgress, repository.Items.Single().Lifecycle);
    }

    /*
     * Waiting is NOT progress, so it is deliberately not gated on approval: a task whose approval is outstanding
     * may still be reported as blocked on something else. Leaving Waiting is the gated direction, and
     * TaskApprovalHttpContractTests pins that half.
     */
    [Fact]
    public async Task An_approval_gated_task_may_still_be_parked_in_waiting()
    {
        var task = OwnedTask(TaskLifecycle.InProgress);
        task.ApprovalRequired = true;
        task.WorkflowInstanceId = Guid.NewGuid();
        var repository = new FakeTaskItemRepository(task);

        var response = await Inquire(repository, task, "Waiting on the vendor while approval runs");

        Assert.Equal(204, response.StatusCode);
        Assert.Equal(TaskLifecycle.Waiting, repository.Items.Single().Lifecycle);
    }

    [Fact]
    public async Task Inquire_is_offered_on_work_the_actor_holds()
    {
        var item = Assert.Single(await Provider(new FakeTaskItemRepository(OwnedTask(TaskLifecycle.InProgress)))
            .GetWorkItemsAsync(Actor(), CancellationToken.None));

        var inquire = Assert.Single(item.Actions, a => a.Code == "inquire");
        Assert.True(inquire.Enabled);
        // The reason is mandatory server-side, so the client must collect one.
        Assert.True(inquire.RequiresReason);
        // The code IS the endpoint segment (POST {id}/inquire) — they cannot drift apart.
        Assert.Equal("inquire", inquire.SemanticType);
    }

    // ── cancel: enforced, not merely hidden ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_requester_can_cancel()
    {
        var task = OwnedTask(TaskLifecycle.InProgress);
        task.CreatedByUserId = TaskTestData.Me;
        var repository = new FakeTaskItemRepository(task);

        var response = await Cancel(repository, task, mayCancelAnyTask: false);

        Assert.Equal(204, response.StatusCode);
        Assert.Equal(TaskLifecycle.Cancelled, repository.Items.Single().Lifecycle);
    }

    /*
     * The defect this closes: slice 1 stopped PROJECTING cancel for a non-requester, but /cancel still accepted a
     * direct POST from anyone holding platform.tasks.cancel. A hidden control is presentation; this is the rule.
     */
    [Fact]
    public async Task An_assignee_who_did_not_request_the_task_is_refused_403_with_the_reason_code()
    {
        var task = OwnedTask(TaskLifecycle.InProgress);
        task.CreatedByUserId = TaskTestData.Rival;
        var repository = new FakeTaskItemRepository(task);

        var response = await Cancel(repository, task, mayCancelAnyTask: false);

        // 403, not 409: a refusal of AUTHORITY, not a state conflict — "reload and retry" would never help.
        Assert.Equal(403, response.StatusCode);
        Assert.Equal(TaskReasonCodes.CancelNotRequester, response.ReasonCode);
        Assert.Equal(TaskLifecycle.InProgress, repository.Items.Single().Lifecycle);
    }

    [Fact]
    public async Task Administrative_authority_may_cancel_someone_elses_task()
    {
        var task = OwnedTask(TaskLifecycle.InProgress);
        task.CreatedByUserId = TaskTestData.Rival;
        var repository = new FakeTaskItemRepository(task);

        var response = await Cancel(repository, task, mayCancelAnyTask: true);

        Assert.Equal(204, response.StatusCode);
        Assert.Equal(TaskLifecycle.Cancelled, repository.Items.Single().Lifecycle);
    }

    [Fact]
    public async Task The_authority_flag_defaults_to_false_so_a_caller_that_forgets_it_fails_closed()
    {
        var task = OwnedTask(TaskLifecycle.InProgress);
        task.CreatedByUserId = TaskTestData.Rival;
        var repository = new FakeTaskItemRepository(task);

        // Built WITHOUT the flag — the shape any existing or future caller gets by default.
        var command = new TransitionTaskItemCommand(
            task.Id, TaskLifecycle.Cancelled, new TaskTransitionRequest(task.Version, null, null), "corr");

        var response = await TransitionHandler(repository).Handle(command, CancellationToken.None);

        Assert.Equal(403, response.StatusCode);
        Assert.Equal(TaskReasonCodes.CancelNotRequester, response.ReasonCode);
    }

    // ── harness ─────────────────────────────────────────────────────────────────────────────────────────────

    private static Task<Response<NoContent>> Inquire(FakeTaskItemRepository repository, TaskItem task, string reason)
        => new InquireTaskItemHandler(
                repository, new TaskLifecycleService(), new FakeCurrentUserContext(TaskTestData.Me))
            .Handle(
                new InquireTaskItemCommand(task.Id, new InquireTaskItemRequest(task.Version, reason), "corr-inquire"),
                CancellationToken.None);

    private static Task<Response<NoContent>> Cancel(
        FakeTaskItemRepository repository, TaskItem task, bool mayCancelAnyTask)
        => TransitionHandler(repository).Handle(
            new TransitionTaskItemCommand(
                task.Id,
                TaskLifecycle.Cancelled,
                new TaskTransitionRequest(task.Version, null, null),
                "corr-cancel",
                mayCancelAnyTask),
            CancellationToken.None);

    private static TransitionTaskItemHandler TransitionHandler(FakeTaskItemRepository repository)
        => new(
            repository,
            new TaskLifecycleService(),
            new FakeCurrentUserContext(TaskTestData.Me),
            new FakeChecklistRunRepository(),
            new TaskChecklistService(),
            // Not blocked: these tests are about the cancel-authority rule, not the approval gate, and cancelling
            // never consults the gate anyway (it is not "this work proceeds").
            new FakeWorkflowTransitionGate(), new FakeTaskDependencyRepository());

    private static TaskWorkItemProvider Provider(FakeTaskItemRepository tasks)
        => new(tasks,
            new FakePositionAssignmentRepository(),
            new TaskLifecycleService(),
            new TaskAssignmentResolver(),
            new FakeUserDisplayNameResolver(),
            new FakeChecklistRunRepository(),
            new FakeTaskApprovalService(), new FakeTaskDependencyRepository(), new FakeTaskCommentRepository());

    private static WorkItemActor Actor() => new(TaskTestData.Me, IsPlatformActor: true, new HashSet<string>());

    private static TaskItem OwnedTask(TaskLifecycle lifecycle) => new()
    {
        TenantId = TaskTestData.Tenant,
        Title = "Prepare the financial summary",
        AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
        AssigneeUserId = TaskTestData.Me,
        CreatedByUserId = TaskTestData.Me,
        OrganizationUnitId = Guid.NewGuid(),
        Lifecycle = lifecycle,
        Version = 1
    };
}
