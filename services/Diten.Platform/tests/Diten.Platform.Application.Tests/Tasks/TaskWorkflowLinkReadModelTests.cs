using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// WP-WF-ENGINE-CANDIDATES-01 · D2 fix round 3 (CT-WF-D2-FIX3), item 3 — the READ model asks the gates' own question.
///
/// <para>Since round 2 the server asks a task's gate while it HOLDS a link, requirement on or off
/// (<see cref="TaskWorkflowLinkGate"/>). The read model still looked at the requirement flag only, so a task whose withdraw
/// had failed showed `start` / `complete` enabled and the server answered "pending". Every read (the Task Center
/// projection, the list, the detail) now reads a held link and resolves it by the gate's rule: a withdrawn requirement is
/// held until MOD-0023 reports the instance closed (refused or cancelled); an unreadable one holds (fail-closed).</para>
/// </summary>
public sealed class TaskWorkflowLinkReadModelTests
{
    private static readonly Guid Held = Guid.Parse("0a0a0a0a-1111-4111-8111-0a0a0a0a0a0a");

    // ── approval: requirement off, link held ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_held_approval_link_the_engine_has_not_closed_keeps_start_disabled_though_the_requirement_is_off()
    {
        var task = Withdrawn(TaskLifecycle.Open, approval: true);
        var states = new FakeTaskApprovalService().Pending(Held);

        var item = await ProjectAsync(task, states);

        var start = Assert.Single(item.Actions, a => a.Code == "start");
        Assert.False(start.Enabled);
        Assert.Equal(TaskReasonCodes.ApprovalPending, start.DisabledReasonCode);
        Assert.Equal("Waiting", item.NormalizedStatus);
        Assert.Equal(1, Assert.Single(states.StateReadSizes));   // the held link was read, in the page's one batched read
    }

    [Theory]
    [InlineData("cancelled")]
    [InlineData("rejected")]
    public async Task A_held_approval_link_the_engine_closed_holds_nothing_once_the_requirement_is_off(string closed)
    {
        var task = Withdrawn(TaskLifecycle.Open, approval: true);
        var states = new FakeTaskApprovalService();
        states.States[Held] = closed == "cancelled"
            ? new TaskApprovalState(IsPending: false, IsApproved: false, IsRejected: false, IsCancelled: true)
            : new TaskApprovalState(IsPending: false, IsApproved: false, IsRejected: true);

        var item = await ProjectAsync(task, states);

        Assert.True(Assert.Single(item.Actions, a => a.Code == "start").Enabled);
        // A refusal no longer kills work nobody requires approval for.
        Assert.Equal("Pending", item.NormalizedStatus);
    }

    [Fact]
    public void An_unreadable_held_approval_link_holds_the_task_fail_closed()
    {
        var task = Withdrawn(TaskLifecycle.Open, approval: true);

        Assert.Equal((true, false), TaskApprovalView.Resolve(task, new Dictionary<Guid, TaskApprovalState>()));
    }

    [Theory]
    [InlineData(false, "Waiting")]
    [InlineData(true, "Pending")]   // read, not assumed: an unread link would hold it (fail-closed) and say Waiting
    public async Task The_task_list_reads_a_held_approval_link(bool closed, string expected)
    {
        var task = Withdrawn(TaskLifecycle.Open, approval: true);
        var states = HeldState(closed);
        var handler = new GetTaskItemListHandler(new FakeTaskItemRepository(task), new FakePositionAssignmentRepository(),
            new TaskLifecycleService(), states, new FakeCurrentUserContext(TaskTestData.Me));

        var list = await handler.Handle(new GetTaskItemListQuery("corr-list"), CancellationToken.None);

        Assert.Equal(expected, Assert.Single(list.Data!).NormalizedStatus);
    }

    [Theory]
    [InlineData(false, "Waiting")]
    [InlineData(true, "Pending")]
    public async Task The_task_detail_reads_a_held_approval_link(bool closed, string expected)
    {
        var task = Withdrawn(TaskLifecycle.Open, approval: true);
        var states = HeldState(closed);
        var handler = new GetTaskItemByIdHandler(new FakeTaskItemRepository(task), new FakeTaskWatcherRepository(),
            new FakeTaskDependencyRepository(), new TaskLifecycleService(), states, new FakeTaskFieldDefinitionRepository(),
            TaskActors.PermitAll(), new AlwaysAdmitReadAccessPolicy(), new FakeCurrentUserContext(TaskTestData.Me));

        var detail = await handler.Handle(new GetTaskItemByIdQuery(task.Id, "corr-detail"), CancellationToken.None);

        Assert.Equal(expected, detail.Data!.NormalizedStatus);
    }

    // ── review: requirement off, link held ───────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(TaskLifecycle.PendingReview)]
    [InlineData(TaskLifecycle.InProgress)]
    public async Task A_held_review_link_the_engine_has_not_closed_keeps_complete_disabled_though_the_requirement_is_off(
        TaskLifecycle lifecycle)
    {
        var task = Withdrawn(lifecycle, approval: false);
        var states = new FakeTaskApprovalService().Pending(Held);

        var item = await ProjectAsync(task, states);

        var complete = Assert.Single(item.Actions, a => a.Code == "complete");
        Assert.False(complete.Enabled);
        Assert.Equal(TaskReasonCodes.ReviewPending, complete.DisabledReasonCode);
    }

    [Fact]
    public async Task A_held_review_link_the_engine_closed_lets_the_work_complete_once_the_requirement_is_off()
    {
        var task = Withdrawn(TaskLifecycle.PendingReview, approval: false);
        var states = new FakeTaskApprovalService();
        states.States[Held] = new TaskApprovalState(IsPending: false, IsApproved: false, IsRejected: false, IsCancelled: true);

        var item = await ProjectAsync(task, states);

        Assert.True(Assert.Single(item.Actions, a => a.Code == "complete").Enabled);
    }

    // ── review: requirement on, the linked review withdrawn (a hand-over took it back) ───────────────────────────

    [Fact]
    public async Task Work_whose_review_was_withdrawn_offers_submitReview_again_and_its_gate_reads_required_not_rejected()
    {
        var task = new TaskItem
        {
            TenantId = TaskTestData.Tenant,
            Title = "Returned to me",
            Lifecycle = TaskLifecycle.InProgress,
            AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
            AssigneeUserId = TaskTestData.Me,
            OrganizationUnitId = Guid.NewGuid(),
            ReviewRequired = true,
            ReviewerCandidateUserId = TaskTestData.Rival,
            ReviewWorkflowInstanceId = Held,
            Version = 4
        };
        var states = new FakeTaskApprovalService();
        states.States[Held] = new TaskApprovalState(IsPending: false, IsApproved: false, IsRejected: false, IsCancelled: true);

        var item = await ProjectAsync(task, states);

        Assert.True(Assert.Single(item.Actions, a => a.Code == "submitReview").Enabled);
        Assert.DoesNotContain(item.Actions, a => a.Code == "complete");
        Assert.Equal("required", item.Gates.Review.Status);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>MOD-0023's answer for <see cref="Held"/>: still pending, or withdrawn (cancelled).</summary>
    private static FakeTaskApprovalService HeldState(bool closed)
    {
        var states = new FakeTaskApprovalService();
        states.States[Held] = closed
            ? new TaskApprovalState(IsPending: false, IsApproved: false, IsRejected: false, IsCancelled: true)
            : new TaskApprovalState(IsPending: true, IsApproved: false, IsRejected: false);
        return states;
    }

    /// <summary>A task whose requirement was switched off while the withdraw failed: flag off, link to <see cref="Held"/>.</summary>
    private static TaskItem Withdrawn(TaskLifecycle lifecycle, bool approval) => new()
    {
        TenantId = TaskTestData.Tenant,
        Title = "Withdraw failed",
        Lifecycle = lifecycle,
        AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
        AssigneeUserId = TaskTestData.Me,
        OrganizationUnitId = Guid.NewGuid(),
        ApprovalRequired = false,
        ReviewRequired = false,
        WorkflowInstanceId = approval ? Held : null,
        ReviewWorkflowInstanceId = approval ? null : Held,
        Version = 2
    };

    private static async Task<WorkItemProjectionDto> ProjectAsync(TaskItem task, FakeTaskApprovalService states)
        => Assert.Single(await new TaskWorkItemProvider(
                new FakeTaskItemRepository(task),
                new FakePositionAssignmentRepository(),
                new TaskLifecycleService(),
                new TaskAssignmentResolver(),
                new FakeUserDisplayNameResolver(),
                new FakeChecklistRunRepository(),
                states,
                new FakeTaskDependencyRepository(),
                new FakeTaskCommentRepository(), new FakeTaskTransitionRepository(), new FakeTaskPersonalOverlayRepository(),
                new FakeTaskWatcherRepository(),
                TaskActors.PermitAll(), new FakePositionRepository(),
                new FakeOrganizationUnitRepository(),
                SlaForTests.Real(), new FakeTaskFieldDefinitionRepository(), new FakeTaskTypeRepository())
            .GetWorkItemsAsync(new WorkItemActor(TaskTestData.Me, IsPlatformActor: true, new HashSet<string>()),
                CancellationToken.None));
}
