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

    // ── BL-046: a finished task is measured from when it closed, not from today ──────────────────────────
    //
    // TWO HALVES, and they only work together. The STATE (overdue / due-soon / on-track / no-sla) froze first,
    // and shipping that alone made the screen read "-2 days LEFT" — the client had no instant to measure the day
    // COUNT from, so it went on subtracting from today. The closing instant now travels with the state, which is
    // what lets the count stop moving. Sending one without the other is what turned this item into a two-round
    // regression, so the emission is asserted here rather than assumed downstream.

    [Fact]
    public async Task Work_finished_ON_TIME_does_not_drift_into_overdue_as_the_calendar_moves()
    {
        /*
         * The sharp end of BL-046. A task closed two days BEFORE its deadline was still measured against today,
         * so once today passed the deadline it began reporting overdue — History accusing someone of missing a
         * deadline they actually beat. The clock now stops when the task closes.
         */
        var task = AssignedTask();
        task.DueAt = DateTimeOffset.UtcNow.AddDays(-30);
        task.CompletedAt = DateTimeOffset.UtcNow.AddDays(-32);   // two days early, a month ago
        task.Lifecycle = TaskLifecycle.Done;
        var repository = new FakeTaskItemRepository(task);

        var item = Assert.Single(await Provider(repository).GetWorkItemsAsync(Actor(TaskTestData.Me)));

        Assert.NotEqual("overdue", item.SlaState);
    }

    [Fact]
    public async Task Work_that_closed_LATE_still_reports_overdue_because_that_is_worth_recording()
    {
        // The badge is frozen, not deleted: closing late is exactly what reporting wants to see. A fix that made
        // every closed task read "on track" would hide the thing the badge exists for.
        var task = AssignedTask();
        task.DueAt = DateTimeOffset.UtcNow.AddDays(-40);
        task.CompletedAt = DateTimeOffset.UtcNow.AddDays(-39);   // closed one day late
        task.Lifecycle = TaskLifecycle.Done;
        var repository = new FakeTaskItemRepository(task);

        var item = Assert.Single(await Provider(repository).GetWorkItemsAsync(Actor(TaskTestData.Me)));

        Assert.Equal("overdue", item.SlaState);
    }

    [Fact]
    public async Task LIVE_work_is_still_measured_against_today()
    {
        /*
         * Non-vacuity, and the regression the freeze could easily have caused: an open task past its due date must
         * go on counting. If this passed while the two above did too, the fix would have frozen everything.
         */
        var task = AssignedTask();
        task.DueAt = DateTimeOffset.UtcNow.AddDays(-5);
        task.Lifecycle = TaskLifecycle.InProgress;
        var repository = new FakeTaskItemRepository(task);

        var item = Assert.Single(await Provider(repository).GetWorkItemsAsync(Actor(TaskTestData.Me)));

        Assert.Equal("overdue", item.SlaState);
    }

    [Fact]
    public async Task A_terminal_task_with_no_closing_timestamp_falls_back_to_today_rather_than_vanishing()
    {
        // Old data has no CompletedAt. Measuring it from today is no worse than the state it was already in,
        // whereas throwing would take the whole item off the surface — the contract drops what it cannot validate.
        var task = AssignedTask();
        task.DueAt = DateTimeOffset.UtcNow.AddDays(-10);
        task.Lifecycle = TaskLifecycle.Done;
        var repository = new FakeTaskItemRepository(task);

        var item = Assert.Single(await Provider(repository).GetWorkItemsAsync(Actor(TaskTestData.Me)));

        Assert.Equal("overdue", item.SlaState);
    }

    [Fact]
    public async Task A_closed_task_SENDS_the_instant_it_closed()
    {
        /*
         * The delivery half. A state frozen on the server and a count still measured from today is precisely the
         * combination that put "-2 days left" on a live screen: the client cannot freeze a number it has nothing
         * to subtract. So the instant is asserted on the wire, not inferred from the state beside it.
         */
        var closed = DateTimeOffset.UtcNow.AddDays(-39);
        var task = AssignedTask();
        task.DueAt = DateTimeOffset.UtcNow.AddDays(-40);
        task.CompletedAt = closed;
        task.Lifecycle = TaskLifecycle.Done;
        var repository = new FakeTaskItemRepository(task);

        var item = Assert.Single(await Provider(repository).GetWorkItemsAsync(Actor(TaskTestData.Me)));

        Assert.Equal(closed, item.ClosedAt);
    }

    [Fact]
    public async Task A_CANCELLED_task_sends_the_instant_it_was_called_off()
    {
        // Cancelled is terminal too — "finished" is the claim, not "succeeded" — and it carries its own
        // timestamp. Reading only CompletedAt would have left every cancelled item drifting.
        var cancelled = DateTimeOffset.UtcNow.AddDays(-3);
        var task = AssignedTask();
        task.DueAt = DateTimeOffset.UtcNow.AddDays(-5);
        task.CancelledAt = cancelled;
        task.Lifecycle = TaskLifecycle.Cancelled;
        var repository = new FakeTaskItemRepository(task);

        var item = Assert.Single(await Provider(repository).GetWorkItemsAsync(Actor(TaskTestData.Me)));

        Assert.Equal(cancelled, item.ClosedAt);
    }

    [Fact]
    public async Task LIVE_work_sends_no_closing_instant_at_all()
    {
        /*
         * Non-vacuity for the two above, and a contract rule: open work has not closed. A timestamp on a running
         * task would freeze its count while it is still running — the defect, inverted.
         */
        var task = AssignedTask();
        task.DueAt = DateTimeOffset.UtcNow.AddDays(-5);
        task.Lifecycle = TaskLifecycle.InProgress;
        var repository = new FakeTaskItemRepository(task);

        var item = Assert.Single(await Provider(repository).GetWorkItemsAsync(Actor(TaskTestData.Me)));

        Assert.Null(item.ClosedAt);
    }

    [Fact]
    public async Task A_terminal_task_with_no_closing_timestamp_sends_NOTHING_rather_than_today()
    {
        /*
         * The honest gap. The STATE falls back to now (better than dropping the item), but the INSTANT must not:
         * a fabricated closing time would freeze a lie, and the client can only tell the difference if the field
         * is genuinely absent — it then says "closed late" without a number.
         */
        var task = AssignedTask();
        task.DueAt = DateTimeOffset.UtcNow.AddDays(-10);
        task.Lifecycle = TaskLifecycle.Done;
        var repository = new FakeTaskItemRepository(task);

        var item = Assert.Single(await Provider(repository).GetWorkItemsAsync(Actor(TaskTestData.Me)));

        Assert.Null(item.ClosedAt);
    }

    // ── BL-051: the acceptance gate must REOPEN when work changes hands ──────────────────────────────────
    //
    // The two tests above named "…lands in the … inbox unaccepted" pass without proving anything about this:
    // they start from a task nobody ever accepted, so "still unaccepted afterwards" is true by default. Every
    // test below starts from an ACCEPTED task, which is the only state in which the gate has something to reopen.
    //
    // Why no test caught BL-051: TaskAssignmentResolverTests measures the RESOLVER, and the JS contract test
    // measures REQUEST BODIES. Nothing asked whether the HANDLER writes the gate correctly, and that is the whole
    // question. BL-042 moved acceptance off the lifecycle and updated only the writer; these three handlers went
    // on clearing the old signal while their comments claimed to reopen the gate.

    [Fact]
    public async Task An_ACCEPTED_task_that_is_reassigned_waits_in_the_new_holders_inbox()
    {
        /*
         * The live defect. A task accepted by Me, handed to Other, appeared as owned/admitted in Other's My Work —
         * straight past the Inbox. Accepting is the moment responsibility transfers, so work must never enter
         * somebody's active list without it.
         */
        var task = AssignedTask();
        task.CloseAcceptanceGate(TaskTestData.Me);
        var repository = new FakeTaskItemRepository(task);

        await Reassign(repository, new FakeTaskAssignmentRepository(), task, TaskTestData.Other, "Handing over");

        var stored = repository.Items.Single();
        Assert.Null(stored.AcceptedByUserId);

        // Asserted through the PROJECTION, because that is what the user sees — the field alone could be right
        // while the surface still said admitted.
        var item = Assert.Single(await Provider(repository).GetWorkItemsAsync(Actor(TaskTestData.Other)));
        Assert.Equal("pendingAcceptance", item.AdmissionState);
        Assert.Equal("assigned", item.OwnershipState);
    }

    [Fact]
    public async Task An_ACCEPTED_task_that_is_returned_waits_in_the_requesters_inbox()
    {
        // Same rule from the other direction: handing work back does not put it into the requester's active list
        // either. They asked for it; they did not agree to do it.
        var task = AssignedTask();
        task.CloseAcceptanceGate(TaskTestData.Me);
        var repository = new FakeTaskItemRepository(task);

        await Return(repository, new FakeTaskAssignmentRepository(), task, "Not mine to finish");

        var stored = repository.Items.Single();
        Assert.Null(stored.AcceptedByUserId);

        var item = Assert.Single(await Provider(repository).GetWorkItemsAsync(Actor(task.CreatedByUserId!.Value)));
        Assert.Equal("pendingAcceptance", item.AdmissionState);
    }

    [Fact]
    public async Task An_ACCEPTED_task_released_to_the_pool_leaves_no_acceptance_behind()
    {
        /*
         * This one produces no visible breakage today — the pool branch projects from a null assignee, so a stale
         * mark cannot be seen. That is exactly why it is asserted: an invisible stale field is a defect waiting
         * for the next projection change, and "we could not see it" is not a reason to leave it.
         */
        var task = AssignedTask();
        task.AssignmentTarget = TaskAssignmentTarget.PositionPool;
        task.AssigneeUserId = TaskTestData.Me;
        task.PoolPositionId = Guid.NewGuid();
        task.CloseAcceptanceGate(TaskTestData.Me);
        var repository = new FakeTaskItemRepository(task);

        var response = await new ReleaseTaskItemHandler(
                repository,
                new FakeTaskAssignmentRepository(),
                new FakeCurrentUserContext(TaskTestData.Me),
                new FakeTenantContext(TaskTestData.Tenant))
            .Handle(
                new ReleaseTaskItemCommand(task.Id, new TaskTransitionRequest(task.Version, null, null), "corr-release"),
                CancellationToken.None);

        Assert.Equal(204, response.StatusCode);
        Assert.Null(repository.Items.Single().AcceptedByUserId);
    }

    [Fact]
    public async Task Accepting_is_not_undone_by_anything_else_the_task_does()
    {
        /*
         * Non-vacuity, and the opposite failure mode: a reopen that fires too eagerly would drop accepted work
         * back into the Inbox for no reason — the same size of defect, pointing the other way. Only the three
         * hand-over transitions may reopen the gate.
         */
        var task = AssignedTask();
        task.CloseAcceptanceGate(TaskTestData.Me);
        var repository = new FakeTaskItemRepository(task);

        var item = Assert.Single(await Provider(repository).GetWorkItemsAsync(Actor(TaskTestData.Me)));
        Assert.Equal("admitted", item.AdmissionState);
        Assert.Equal(TaskTestData.Me, repository.Items.Single().AcceptedByUserId);
    }

    [Fact]
    public void NOTHING_outside_the_entity_can_move_the_acceptance_gate()
    {
        /*
         * The structural half. BL-042 and BL-051 are the same defect twice: one fact, several writers, one of them
         * forgotten. The setter is private, so this is compiler-enforced — but assert it anyway, because a future
         * "just make it public for a moment" is exactly how the third repeat would arrive.
         */
        var setter = typeof(TaskItem).GetProperty(nameof(TaskItem.AcceptedByUserId))!.SetMethod;

        Assert.NotNull(setter);
        Assert.False(setter!.IsPublic, "AcceptedByUserId is publicly settable again — the gate has more than one owner.");
    }

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
            new FakeTaskApprovalService(), new FakeTaskDependencyRepository(), new FakeTaskCommentRepository(), new FakePositionRepository(), new FakeOrganizationUnitRepository(), SlaForTests.Real(), new FakeTaskFieldDefinitionRepository());

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
