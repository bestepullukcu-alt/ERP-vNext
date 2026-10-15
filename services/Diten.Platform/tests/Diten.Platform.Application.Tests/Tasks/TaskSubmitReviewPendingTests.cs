using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// WP-WCN-TASK-REQUEST-01 (merge into the engine takeover) — submitting a task for review while its held review is
/// STILL OPEN answers <see cref="TaskReasonCodes.ReviewPending"/> in PendingReview too. It answered the lifecycle's
/// TASK_INVALID_STATE there, so the screen's own sentence for it (<c>reasonOverridesFor('submitReview')</c> →
/// errorReviewAlreadyOpen) was never reached. A review sent back is still the resubmission, and a CLOSED review in
/// PendingReview still gets the lifecycle's answer — both pinned here so the fix moves one case only.
/// </summary>
public sealed class TaskSubmitReviewPendingTests
{
    private static readonly Guid ReviewInstance = Guid.Parse("cafecafe-cafe-cafe-cafe-cafecafecafe");

    [Fact]
    public async Task An_open_review_in_PendingReview_answers_REVIEW_PENDING_and_starts_nothing()
    {
        var (tasks, reviews, result) = await SubmitWith(new TaskApprovalState(IsPending: true, IsApproved: false, IsRejected: false));

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(TaskReasonCodes.ReviewPending, result.ReasonCode);
        Assert.Empty(reviews.Started);
        Assert.Equal(ReviewInstance, tasks.Items.Single().ReviewWorkflowInstanceId);
    }

    [Fact]
    public async Task A_review_sent_back_in_PendingReview_is_still_the_resubmission()
    {
        var (_, reviews, result) = await SubmitWith(new TaskApprovalState(IsPending: false, IsApproved: false, IsRejected: true));

        Assert.True(result.IsSuccessful);
        Assert.Single(reviews.Started);
    }

    [Fact]
    public async Task A_closed_review_in_PendingReview_still_gets_the_lifecycles_answer()
    {
        var (_, reviews, result) = await SubmitWith(new TaskApprovalState(IsPending: false, IsApproved: true, IsRejected: false));

        Assert.False(result.IsSuccessful);
        Assert.Equal(TaskReasonCodes.InvalidState, result.ReasonCode);
        Assert.Empty(reviews.Started);
    }

    private static async Task<(FakeTaskItemRepository Tasks, FakeTaskReviewService Reviews, Response<NoContent> Result)> SubmitWith(
        TaskApprovalState held)
    {
        var task = new TaskItem
        {
            TenantId = TaskTestData.Tenant,
            Title = "İncelenecek iş",
            AssignmentTarget = TaskAssignmentTarget.Person,
            AssigneeUserId = TaskTestData.Me,
            CreatedByUserId = TaskTestData.Me,
            OrganizationUnitId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Lifecycle = TaskLifecycle.PendingReview,
            ReviewRequired = true,
            ReviewWorkflowInstanceId = ReviewInstance,
            Version = 1
        };
        var tasks = new FakeTaskItemRepository(task);
        var reviews = new FakeTaskReviewService { InstanceId = Guid.NewGuid() };
        var states = new FakeTaskApprovalService();
        states.States[ReviewInstance] = held;

        var result = await new SubmitTaskForReviewHandler(
                tasks,
                new TaskLifecycleService(),
                new FakeCurrentUserContext(TaskTestData.Me),
                reviews,
                states,
                NullLogger<SubmitTaskForReviewHandler>.Instance, writeGate: TaskWriteGateDoubles.Open)
            .Handle(
                new SubmitTaskForReviewCommand(task.Id, new TaskTransitionRequest(task.Version, null, null), "corr-submit"),
                CancellationToken.None);
        return (tasks, reviews, result);
    }
}
