using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// Handing assigned work BACK (return) and ON (reassign).
///
/// <para>Until these existed the only way out of unwanted work was <c>cancel</c>, which means the opposite: the
/// request is destroyed rather than declined. So an assignee either did somebody else's job or killed their
/// request.</para>
///
/// <para>Assertions are on the <see cref="Response{T}"/> the controller turns verbatim into the HTTP response, and
/// each success is followed by a re-read of the STORED task — a handler that answers 204 while writing nothing
/// looks identical from the response alone.</para>
/// </summary>
public sealed class TaskHandoverTests
{
    private static readonly Guid PositionId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid UnitId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    // ── return: back to whoever asked for it ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_returned_task_lands_in_the_requesters_inbox_unaccepted()
    {
        var task = AssignedTask();
        var repository = new FakeTaskItemRepository(task);
        var events = new FakeTaskAssignmentRepository();

        var response = await Return(repository, events, task, "This needs the finance team, not me");

        Assert.Equal(204, response.StatusCode);

        var stored = repository.Items.Single();
        // The requester now holds it…
        Assert.Equal(TaskTestData.Rival, stored.AssigneeUserId);
        // …and the acceptance gate has reopened, so it appears in their INBOX rather than in their active work.
        Assert.Equal(TaskLifecycle.Open, stored.Lifecycle);
        Assert.Equal(TaskAssignmentTarget.Person, stored.AssignmentTarget);

        // Projected the way the Inbox reads it.
        var item = Assert.Single(await Provider(repository).GetWorkItemsAsync(Actor(TaskTestData.Rival), CancellationToken.None));
        Assert.Equal("pendingAcceptance", item.AdmissionState);
    }

    [Fact]
    public async Task Returning_records_who_sent_it_back_and_why()
    {
        var task = AssignedTask();
        var repository = new FakeTaskItemRepository(task);
        var events = new FakeTaskAssignmentRepository();

        await Return(repository, events, task, "Wrong team");

        var recorded = Assert.Single(events.Events);
        // There is no `Returned` event type, and adding one would change the shape of already-persisted history
        // for a distinction the note already carries. A return IS a reassignment — back to the requester.
        Assert.Equal(TaskAssignmentEventType.Reassigned, recorded.EventType);
        Assert.Equal(TaskTestData.Rival, recorded.UserId);       // who now holds it
        Assert.Equal(TaskTestData.Me, recorded.ActorUserId);     // who sent it back
        Assert.Equal("Wrong team", recorded.Note);
    }

    [Fact]
    public async Task Returning_without_saying_why_is_refused()
    {
        var task = AssignedTask();
        var repository = new FakeTaskItemRepository(task);

        var response = await Return(repository, new FakeTaskAssignmentRepository(), task, "  ");

        Assert.Equal(400, response.StatusCode);
        Assert.Equal(TaskReasonCodes.HandoverReasonRequired, response.ReasonCode);
        Assert.Equal(TaskTestData.Me, repository.Items.Single().AssigneeUserId);
    }

    [Fact]
    public async Task Only_the_assignee_may_return_a_task()
    {
        var task = AssignedTask();
        task.AssigneeUserId = TaskTestData.Rival;   // somebody else holds it
        var repository = new FakeTaskItemRepository(task);

        var response = await Return(repository, new FakeTaskAssignmentRepository(), task, "Not mine to refuse");

        Assert.Equal(403, response.StatusCode);
        Assert.Equal(TaskReasonCodes.ReturnNotAssignee, response.ReasonCode);
    }

    [Fact]
    public async Task A_self_assigned_task_offers_no_return_because_there_is_nobody_to_return_it_to()
    {
        var task = AssignedTask();
        task.CreatedByUserId = TaskTestData.Me;   // I asked for it AND I hold it

        var item = Assert.Single(await Provider(new FakeTaskItemRepository(task))
            .GetWorkItemsAsync(Actor(TaskTestData.Me), CancellationToken.None));

        Assert.DoesNotContain(item.Actions, a => a.Code == "return");
    }

    [Fact]
    public async Task Returning_a_self_assigned_task_is_refused_at_the_endpoint_too()
    {
        // The projection hides it; hiding a control is presentation, so the write is refused as well.
        var task = AssignedTask();
        task.CreatedByUserId = TaskTestData.Me;
        var repository = new FakeTaskItemRepository(task);

        var response = await Return(repository, new FakeTaskAssignmentRepository(), task, "Back to me?");

        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public async Task The_assignee_is_offered_return_when_somebody_else_asked_for_the_work()
    {
        var item = Assert.Single(await Provider(new FakeTaskItemRepository(AssignedTask()))
            .GetWorkItemsAsync(Actor(TaskTestData.Me), CancellationToken.None));

        var action = Assert.Single(item.Actions, a => a.Code == "return");
        Assert.True(action.Enabled);
        // A refusal the requester cannot understand only moves the problem, so the client must collect a reason.
        Assert.True(action.RequiresReason);
    }

    // ── reassign: on to somebody else ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_reassigned_task_lands_in_the_new_holders_inbox_unaccepted()
    {
        var task = AssignedTask();
        var repository = new FakeTaskItemRepository(task);
        var events = new FakeTaskAssignmentRepository();

        var response = await Reassign(repository, events, task, TaskTestData.Other, "Handing over before leave");

        Assert.Equal(204, response.StatusCode);

        var stored = repository.Items.Single();
        Assert.Equal(TaskTestData.Other, stored.AssigneeUserId);
        Assert.Equal(TaskLifecycle.Open, stored.Lifecycle);

        var recorded = Assert.Single(events.Events);
        Assert.Equal(TaskAssignmentEventType.Reassigned, recorded.EventType);
        Assert.Equal(TaskTestData.Other, recorded.UserId);
        Assert.Equal("Handing over before leave", recorded.Note);
    }

    [Fact]
    public async Task The_requester_may_also_reassign_to_correct_a_wrong_assignment()
    {
        var task = AssignedTask();
        var repository = new FakeTaskItemRepository(task);

        var response = await Reassign(
            repository, new FakeTaskAssignmentRepository(), task, TaskTestData.Other,
            "Assigned to the wrong person", actingAs: TaskTestData.Rival);

        Assert.Equal(204, response.StatusCode);
        Assert.Equal(TaskTestData.Other, repository.Items.Single().AssigneeUserId);
    }

    [Fact]
    public async Task A_bystander_may_not_move_work_onto_a_colleague()
    {
        var task = AssignedTask();
        var repository = new FakeTaskItemRepository(task);

        var response = await Reassign(
            repository, new FakeTaskAssignmentRepository(), task, TaskTestData.Other,
            "Not my call", actingAs: TaskTestData.Other);

        Assert.Equal(403, response.StatusCode);
        Assert.Equal(TaskReasonCodes.ReassignNotPermitted, response.ReasonCode);
        Assert.Equal(TaskTestData.Me, repository.Items.Single().AssigneeUserId);
    }

    /*
     * The target must be somebody the people picker would offer. Written twice these rules drift, and the drift
     * is invisible in the direction that matters — the picker narrows while the endpoint stays wide, and work
     * lands on somebody the product no longer considers assignable. Both call TaskAssigneeEligibility.
     */
    [Fact]
    public async Task Reassigning_to_somebody_who_holds_no_active_position_is_refused()
    {
        var task = AssignedTask();
        var repository = new FakeTaskItemRepository(task);
        var strangerWithNoPosition = Guid.Parse("12121212-1212-1212-1212-121212121212");

        var response = await Reassign(
            repository, new FakeTaskAssignmentRepository(), task, strangerWithNoPosition, "Try anyway");

        Assert.Equal(400, response.StatusCode);
        Assert.Equal(TaskReasonCodes.AssigneeNotAssignable, response.ReasonCode);
        Assert.Equal(TaskTestData.Me, repository.Items.Single().AssigneeUserId);
    }

    [Fact]
    public async Task Pooled_work_is_claimed_and_released_never_reassigned()
    {
        var task = AssignedTask();
        task.AssignmentTarget = TaskAssignmentTarget.PositionPool;
        task.PoolPositionId = PositionId;

        // Not projected…
        var item = Assert.Single(await Provider(new FakeTaskItemRepository(task), pooled: true)
            .GetWorkItemsAsync(Actor(TaskTestData.Me), CancellationToken.None));
        Assert.DoesNotContain(item.Actions, a => a.Code == "reassign");

        // …and not accepted either.
        var repository = new FakeTaskItemRepository(task);
        var response = await Reassign(
            repository, new FakeTaskAssignmentRepository(), task, TaskTestData.Other, "Hand it over");
        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public async Task Reassigning_without_saying_why_is_refused()
    {
        var task = AssignedTask();
        var repository = new FakeTaskItemRepository(task);

        var response = await Reassign(repository, new FakeTaskAssignmentRepository(), task, TaskTestData.Other, "");

        Assert.Equal(400, response.StatusCode);
        Assert.Equal(TaskReasonCodes.HandoverReasonRequired, response.ReasonCode);
    }

    // ── harness ─────────────────────────────────────────────────────────────────────────────────────────────

    private static Task<Response<NoContent>> Return(
        FakeTaskItemRepository tasks, FakeTaskAssignmentRepository events, TaskItem task, string reason)
        => new ReturnTaskItemHandler(
                tasks, events, new FakeCurrentUserContext(TaskTestData.Me), new FakeTenantContext(TaskTestData.Tenant))
            .Handle(
                new ReturnTaskItemCommand(task.Id, new ReturnTaskItemRequest(task.Version, reason), "corr-return"),
                CancellationToken.None);

    private static Task<Response<NoContent>> Reassign(
        FakeTaskItemRepository tasks,
        FakeTaskAssignmentRepository events,
        TaskItem task,
        Guid newAssignee,
        string reason,
        Guid? actingAs = null)
        => new ReassignTaskItemHandler(
                tasks,
                events,
                new FakePositionAssignmentRepository(Holder(TaskTestData.Me), Holder(TaskTestData.Other)),
                new FakePositionRepository(ActivePosition()),
                new FakeOrganizationUnitRepository(LiveUnit()),
                new FakeCurrentUserContext(actingAs ?? TaskTestData.Me),
                new FakeTenantContext(TaskTestData.Tenant))
            .Handle(
                new ReassignTaskItemCommand(
                    task.Id, new ReassignTaskItemRequest(task.Version, newAssignee, reason), "corr-reassign"),
                CancellationToken.None);

    private static TaskWorkItemProvider Provider(FakeTaskItemRepository tasks, bool pooled = false)
        => new(tasks,
            pooled ? new FakePositionAssignmentRepository(Holder(TaskTestData.Me)) : new FakePositionAssignmentRepository(),
            new TaskLifecycleService(),
            new TaskAssignmentResolver(),
            new FakeUserDisplayNameResolver(),
            new FakeChecklistRunRepository(),
            new FakeTaskApprovalService(), new FakeTaskDependencyRepository(), new FakeTaskCommentRepository(), new FakePositionRepository(), new FakeOrganizationUnitRepository(), SlaForTests.Real());

    private static WorkItemActor Actor(Guid userId) => new(userId, IsPlatformActor: true, new HashSet<string>());

    /// <summary>Assigned TO me, requested BY someone else — the case where returning means something.</summary>
    private static TaskItem AssignedTask() => new()
    {
        TenantId = TaskTestData.Tenant,
        Title = "Prepare the board pack",
        AssignmentTarget = TaskAssignmentTarget.Person,
        AssigneeUserId = TaskTestData.Me,
        CreatedByUserId = TaskTestData.Rival,
        OrganizationUnitId = UnitId,
        Lifecycle = TaskLifecycle.Open,
        Version = 1
    };

    private static PositionAssignment Holder(Guid userId) => new()
    {
        TenantId = TaskTestData.Tenant,
        PositionId = PositionId,
        UserId = userId,
        EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1)
    };

    private static Position ActivePosition() => new()
    {
        Id = PositionId,
        TenantId = TaskTestData.Tenant,
        Code = "QA",
        Name = "QA Specialist",
        OrganizationUnitId = UnitId,
        Status = PositionStatus.Active
    };

    private static OrganizationUnit LiveUnit() => new()
    {
        Id = UnitId,
        TenantId = TaskTestData.Tenant,
        Code = "OPS",
        Name = "Operations",
        LegalEntityId = Guid.NewGuid()
    };
}
