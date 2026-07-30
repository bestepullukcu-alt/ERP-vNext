using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// Faz 3b — what the surface REPORTS about a review, and where that answer comes from.
///
/// <para>The rule these tests hold is the one approval already learned the hard way: a gate's status must be READ
/// from MOD-0023's instance, never inferred from a field MOD-0024 happens to keep. Review's projection used to be
/// <c>lifecycle == PendingReview ? "pending" : "required"</c>, which records only that the work was HANDED OVER —
/// so a released review and a refused one were both reported as "pending", and neither could ever be told from the
/// other.</para>
/// </summary>
public sealed class TaskReviewProjectionTests
{
    private static readonly Guid ReviewInstance = Guid.Parse("beefbeef-beef-beef-beef-beefbeefbeef");
    private static readonly Guid ApprovalInstance = Guid.Parse("abcdabcd-abcd-abcd-abcd-abcdabcdabcd");

    // ── The gate status is read, not inferred ────────────────────────────────

    [Fact]
    public async Task A_review_nobody_has_been_asked_for_yet_reads_as_required()
    {
        // Declared on the task, but the work has not been submitted: no instance, so nobody is holding it.
        var item = await ProjectAsync(ReviewTask(TaskLifecycle.InProgress), new FakeTaskApprovalService());

        Assert.True(item.Gates.Review.Required);
        Assert.Equal("required", item.Gates.Review.Status);
        // Not Waiting: nothing is being waited on, and the contract pairs Waiting with a waitingContext.
        Assert.Equal("InProgress", item.NormalizedStatus);
        Assert.Null(item.WaitingContext);
    }

    [Fact]
    public async Task A_review_a_reviewer_is_holding_reads_as_pending()
    {
        var task = ReviewTask(TaskLifecycle.PendingReview);
        task.ReviewWorkflowInstanceId = ReviewInstance;
        var states = new FakeTaskApprovalService();
        states.States[ReviewInstance] = new TaskApprovalState(IsPending: true, IsApproved: false, IsRejected: false);

        var item = await ProjectAsync(task, states);

        Assert.Equal("pending", item.Gates.Review.Status);
        Assert.Equal("Waiting", item.NormalizedStatus);
        Assert.Equal("review", item.WaitingContext!.Type);
    }

    [Fact]
    public async Task A_RELEASED_review_stops_reading_as_pending()
    {
        /*
         * The test the old lifecycle-derived projection could not pass. The task is still in PendingReview on the
         * record — nothing writes it back — and only MOD-0023's answer distinguishes "released" from "waiting".
         */
        var task = ReviewTask(TaskLifecycle.PendingReview);
        task.ReviewWorkflowInstanceId = ReviewInstance;
        var states = new FakeTaskApprovalService();
        states.States[ReviewInstance] = new TaskApprovalState(IsPending: false, IsApproved: true, IsRejected: false);

        var item = await ProjectAsync(task, states);

        Assert.Equal("approved", item.Gates.Review.Status);
        // No longer waiting on anyone: the work is back with its holder to close.
        Assert.Equal("InProgress", item.NormalizedStatus);
        Assert.Null(item.WaitingContext);
        Assert.Contains(item.Actions, a => a.Code == "complete" && a.Enabled);
    }

    [Fact]
    public async Task A_REFUSED_review_sends_the_work_back_rather_than_killing_it()
    {
        /*
         * The one place review deliberately does NOT mirror approval. A refused approval kills the request and the
         * task reads Cancelled; a refused review hands the WORK back to the person holding it — they still have
         * it, and it is still theirs to fix.
         */
        var task = ReviewTask(TaskLifecycle.PendingReview);
        task.ReviewWorkflowInstanceId = ReviewInstance;
        var states = new FakeTaskApprovalService();
        states.States[ReviewInstance] = new TaskApprovalState(IsPending: false, IsApproved: false, IsRejected: true);

        var item = await ProjectAsync(task, states);

        Assert.Equal("rejected", item.Gates.Review.Status);
        Assert.Equal("InProgress", item.NormalizedStatus);
        Assert.NotEqual("Cancelled", item.NormalizedStatus);
        // Work that came back with nothing to press would be a trap.
        Assert.Contains(item.Actions, a => a.Code == "submitReview" && a.Enabled);
    }

    [Fact]
    public async Task An_unreadable_review_instance_fails_CLOSED()
    {
        // The instance exists but MOD-0023 says nothing about it. Reporting "released" would let unreviewed work
        // close, so silence counts as outstanding — the same rule approval follows.
        var task = ReviewTask(TaskLifecycle.PendingReview);
        task.ReviewWorkflowInstanceId = ReviewInstance;

        var item = await ProjectAsync(task, new FakeTaskApprovalService());

        Assert.Equal("pending", item.Gates.Review.Status);
    }

    [Fact]
    public async Task A_task_with_no_review_requirement_reports_none()
    {
        var task = ReviewTask(TaskLifecycle.InProgress);
        task.ReviewRequired = false;

        var item = await ProjectAsync(task, new FakeTaskApprovalService());

        Assert.False(item.Gates.Review.Required);
        Assert.Equal("notRequired", item.Gates.Review.Status);
    }

    // ── The action set follows the gate ──────────────────────────────────────

    [Fact]
    public async Task Review_gated_work_offers_submitReview_instead_of_complete()
    {
        // Two "I am finished" buttons where only one can work would be worse than one: `complete` would 409.
        var item = await ProjectAsync(ReviewTask(TaskLifecycle.InProgress), new FakeTaskApprovalService());

        Assert.Contains(item.Actions, a => a.Code == "submitReview" && a.Enabled);
        Assert.DoesNotContain(item.Actions, a => a.Code == "complete");
        Assert.Equal("submitReview", item.PrimaryActionCode);
    }

    [Fact]
    public async Task While_a_reviewer_holds_the_work_complete_is_VISIBLE_but_disabled()
    {
        /*
         * Not hidden. The whole point of the state is that someone else is holding the work, and a vanished button
         * teaches the reader nothing about why — the reason has to be readable beside it.
         */
        var task = ReviewTask(TaskLifecycle.PendingReview);
        task.ReviewWorkflowInstanceId = ReviewInstance;
        var states = new FakeTaskApprovalService();
        states.States[ReviewInstance] = new TaskApprovalState(IsPending: true, IsApproved: false, IsRejected: false);

        var item = await ProjectAsync(task, states);

        var complete = Assert.Single(item.Actions, a => a.Code == "complete");
        Assert.False(complete.Enabled);
        Assert.Equal(TaskReasonCodes.ReviewPending, complete.DisabledReasonCode);
        // The reason is a resource key, never a sentence baked into the projection.
        Assert.Equal(WorkItemContract.LabelResource, complete.DisabledReason!.Kind);
    }

    [Fact]
    public async Task Work_that_needs_no_review_still_offers_complete()
    {
        // Non-vacuity for the two tests above: if `complete` had simply been dropped from the provider they would
        // both pass while every ordinary task lost its finish button.
        var task = ReviewTask(TaskLifecycle.InProgress);
        task.ReviewRequired = false;

        var item = await ProjectAsync(task, new FakeTaskApprovalService());

        Assert.Contains(item.Actions, a => a.Code == "complete" && a.Enabled);
        Assert.DoesNotContain(item.Actions, a => a.Code == "submitReview");
    }

    // ── One read for both decisions ──────────────────────────────────────────

    [Fact]
    public async Task Approval_and_review_are_answered_by_ONE_batched_read()
    {
        /*
         * A per-item read would be an N+1 across every gated row on the surface, and a second batched call would
         * be a second round-trip for one page. The lookup is keyed by INSTANCE id and never asks what the instance
         * decides, so it serves both decisions unchanged.
         */
        var task = ReviewTask(TaskLifecycle.PendingReview);
        task.ApprovalRequired = true;
        task.WorkflowInstanceId = ApprovalInstance;
        task.ReviewWorkflowInstanceId = ReviewInstance;
        var second = ReviewTask(TaskLifecycle.PendingReview);
        second.Title = "İkinci iş";
        second.ReviewWorkflowInstanceId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        var states = new FakeTaskApprovalService();
        await ProviderFor([task, second], states).GetWorkItemsAsync(Actor(), CancellationToken.None);

        // ONE call, carrying all three instance ids — two reviews and one approval.
        Assert.Equal(3, Assert.Single(states.StateReadSizes));
    }

    [Fact]
    public async Task A_page_with_no_gated_work_asks_for_nothing()
    {
        // Non-vacuity for the count above: a batch size that grows with the page regardless of gating would make
        // the assertion meaningless.
        var task = ReviewTask(TaskLifecycle.InProgress);
        task.ReviewRequired = false;

        var states = new FakeTaskApprovalService();
        await ProviderFor([task], states).GetWorkItemsAsync(Actor(), CancellationToken.None);

        Assert.All(states.StateReadSizes, size => Assert.Equal(0, size));
    }

    // ── The decider is a candidate hint, never a verdict ─────────────────────

    [Fact]
    public async Task The_reviews_decider_is_the_SUGGESTED_reviewer()
    {
        /*
         * On the same terms as approval's manager: it is who the requester suggested, and MOD-0023/MOD-0018 decide
         * who may actually act. It is never who DID review — that answer belongs to the instance, and a copy here
         * would go stale the moment a reviewer decides.
         */
        var task = ReviewTask(TaskLifecycle.InProgress);
        task.ReviewerCandidateUserId = TaskTestData.Rival;

        var item = await ProjectAsync(task, new FakeTaskApprovalService());

        Assert.NotNull(item.Gates.Review.Decider);
        Assert.Equal(TaskTestData.Rival.ToString(), item.Gates.Review.Decider!.Id);
    }

    [Fact]
    public async Task With_no_suggestion_the_decider_is_absent_rather_than_invented()
    {
        // Filling it with the approval manager — a different person answering a different question — is exactly
        // the invented-identity defect this codebase has shipped before.
        var task = ReviewTask(TaskLifecycle.InProgress);
        task.ApprovalManagerUserId = TaskTestData.Rival;

        var item = await ProjectAsync(task, new FakeTaskApprovalService());

        Assert.Null(item.Gates.Review.Decider);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static async Task<WorkItemProjectionDto> ProjectAsync(TaskItem task, FakeTaskApprovalService states)
        => Assert.Single(await ProviderFor([task], states).GetWorkItemsAsync(Actor(), CancellationToken.None));

    private static TaskWorkItemProvider ProviderFor(TaskItem[] tasks, FakeTaskApprovalService states)
        => new(
            new FakeTaskItemRepository(tasks),
            new FakePositionAssignmentRepository(),
            new TaskLifecycleService(),
            new TaskAssignmentResolver(),
            new FakeUserDisplayNameResolver(),
            new FakeChecklistRunRepository(),
            states,
            new FakeTaskDependencyRepository(),
            new FakeTaskCommentRepository(),
            new FakePositionRepository(),
            new FakeOrganizationUnitRepository(),
            SlaForTests.Real());

    private static WorkItemActor Actor()
        => new(TaskTestData.Me, IsPlatformActor: true, new HashSet<string>());

    private static TaskItem ReviewTask(TaskLifecycle lifecycle) => new()
    {
        TenantId = TaskTestData.Tenant,
        Title = "İncelenecek iş",
        Lifecycle = lifecycle,
        AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
        AssigneeUserId = TaskTestData.Me,
        OrganizationUnitId = Guid.NewGuid(),
        ReviewRequired = true,
        Version = 1
    };
}
