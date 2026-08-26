using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// Faz 3b — the REAL <see cref="TaskReviewService"/> against a chain that runs MOD-0023's own validators.
///
/// <para><b>Why this file exists, stated plainly.</b> The review slice shipped a defect that every one of its
/// thirty-four tests missed: <c>TryStartReviewAsync</c> sent an EMPTY candidate list, and MOD-0023's
/// <c>StartWorkflowInstanceValidator</c> refuses one. Live, a task created with review switched on and no
/// reviewer could never be submitted — 409 forever, with nothing the user could do.</para>
///
/// <para><b>Why it was missed.</b> Not because the fixtures always carried a reviewer — they never did. Because
/// <c>FakeTaskReviewService</c> stands in for the whole handoff and returns an instance id unconditionally, so
/// MOD-0023's validator was never on the path. A chain that ends in a fake proves nothing about the request the
/// real service sends. <c>FakeWorkflowMediator</c> now runs those validators, and the tests below drive the real
/// service through it — including, first, the case with NO candidate.</para>
/// </summary>
public sealed class TaskReviewHandoffTests
{
    // ── The missed case, first ───────────────────────────────────────────────

    [Fact]
    public async Task A_review_with_NO_candidate_is_refused_by_MOD0023()
    {
        /*
         * The defect, pinned. This is what live did: the workflow was refused, TryStartReviewAsync returned null,
         * and the task stayed where it was. The service's behaviour here is correct and unchanged — what was
         * missing is that nothing stopped a task from reaching this state, which the write-path validator now
         * does (TaskReviewRules).
         */
        var harness = new Handoff();

        var instanceId = await harness.Service.TryStartReviewAsync(ReviewTask(reviewer: null), CancellationToken.None);

        Assert.Null(instanceId);
        // Refused by MOD-0023's validator, so no instance was ever opened.
        Assert.Empty(harness.Mediator.Starts);
    }

    [Fact]
    public async Task A_review_WITH_a_candidate_starts()
    {
        // Non-vacuity for the test above: same service, same chain, and only the candidate differs.
        var harness = new Handoff();

        var instanceId = await harness.Service.TryStartReviewAsync(
            ReviewTask(reviewer: TaskTestData.Rival), CancellationToken.None);

        Assert.NotNull(instanceId);
        var start = Assert.Single(harness.Mediator.Starts);
        Assert.Equal([TaskTestData.Rival.ToString()], start.CandidatePrincipalIds);
    }

    [Fact]
    public async Task An_EMPTY_guid_counts_as_no_candidate()
    {
        // Guid.Empty is what an unfilled form field deserializes to, and it is not a principal MOD-0023 can route
        // to. Passing it through would trade a clear refusal for an instance assigned to nobody.
        var harness = new Handoff();

        var instanceId = await harness.Service.TryStartReviewAsync(
            ReviewTask(reviewer: Guid.Empty), CancellationToken.None);

        Assert.Null(instanceId);
        Assert.Empty(harness.Mediator.Starts);
    }

    // ── The request MOD-0024 actually sends ──────────────────────────────────

    [Fact]
    public async Task The_review_is_started_under_its_OWN_object_identity()
    {
        var harness = new Handoff();
        var task = ReviewTask(reviewer: TaskTestData.Rival);

        await harness.Service.TryStartReviewAsync(task, CancellationToken.None);

        var start = Assert.Single(harness.Mediator.Starts);
        Assert.Equal(TaskReviewService.ReviewObjectType, start.ObjectType);
        Assert.Equal(TaskReviewService.BuildObjectRef(task.Id), start.ObjectRef);
        // Never approval's — that is the collision the whole slice exists to avoid.
        Assert.NotEqual(TaskApprovalService.BuildObjectRef(task.Id), start.ObjectRef);
    }

    [Fact]
    public async Task A_retry_of_the_SAME_round_reuses_one_idempotency_key()
    {
        /*
         * A crash between starting the workflow and storing the link must resolve to ONE instance, so the retry
         * has to present the same key. The key names the instance being replaced, which for a first submission
         * has not changed.
         */
        var harness = new Handoff();
        var task = ReviewTask(reviewer: TaskTestData.Rival);

        await harness.Service.TryStartReviewAsync(task, CancellationToken.None);
        await harness.Service.TryStartReviewAsync(task, CancellationToken.None);

        Assert.Equal(2, harness.Mediator.Starts.Count);
        Assert.Equal(harness.Mediator.Starts[0].IdempotencyKey, harness.Mediator.Starts[1].IdempotencyKey);
    }

    [Fact]
    public async Task A_SECOND_round_after_a_refusal_gets_its_own_key()
    {
        // Otherwise the retry would be handed the refused instance back and the second round could never start.
        var harness = new Handoff();
        var task = ReviewTask(reviewer: TaskTestData.Rival);

        await harness.Service.TryStartReviewAsync(task, CancellationToken.None);
        task.ReviewWorkflowInstanceId = Guid.Parse("beefbeef-beef-beef-beef-beefbeefbeef");
        await harness.Service.TryStartReviewAsync(task, CancellationToken.None);

        Assert.NotEqual(harness.Mediator.Starts[0].IdempotencyKey, harness.Mediator.Starts[1].IdempotencyKey);
    }

    // ── The template, installed once ─────────────────────────────────────────

    [Fact]
    public async Task Review_installs_its_OWN_template_rather_than_borrowing_approvals()
    {
        /*
         * Two decisions, two flows a tenant can design independently. Sharing one template code would mean an
         * operator re-pointing their approval flow silently re-pointed review too.
         */
        var harness = new Handoff();

        await harness.Service.TryStartReviewAsync(ReviewTask(TaskTestData.Rival), CancellationToken.None);

        var created = Assert.Single(harness.Mediator.Created);
        Assert.Equal("task-review", created.TemplateCode);
        Assert.NotEqual("task-approval", created.TemplateCode);
    }

    [Fact]
    public async Task A_second_review_in_the_same_tenant_REUSES_the_template()
    {
        var harness = new Handoff();

        await harness.Service.TryStartReviewAsync(ReviewTask(TaskTestData.Rival), CancellationToken.None);
        await harness.Service.TryStartReviewAsync(ReviewTask(TaskTestData.Rival), CancellationToken.None);

        Assert.Single(harness.Mediator.Created);
        Assert.Equal(2, harness.Mediator.Starts.Count);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static TaskItem ReviewTask(Guid? reviewer) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TaskTestData.Tenant,
        Title = "İncelenecek iş",
        Lifecycle = TaskLifecycle.InProgress,
        AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
        AssigneeUserId = TaskTestData.Me,
        OrganizationUnitId = Guid.NewGuid(),
        ReviewRequired = true,
        ReviewerCandidateUserId = reviewer,
        Version = 1
    };

    /// <summary>
    /// The real service wired to the MOD-0023 doubles — the same shape the approval installer harness uses, and
    /// the same doubles, so both decisions are exercised against one engine stand-in.
    /// </summary>
    private sealed class Handoff
    {
        public Handoff()
        {
            Tenant = new FakeTenantContext(TaskTestData.Tenant);
            Templates = new FakeWorkflowTemplateStore(Tenant);
            Mediator = new FakeWorkflowMediator(Templates, Tenant);
            Service = new TaskReviewService(
                Mediator,
                Templates,
                new UnreadApprovalTaskRepository(),
                Tenant,
                new FakeCurrentUserContext(TaskTestData.Me),
                Options.Create(new TaskReviewOptions()),
                NullLogger<TaskReviewService>.Instance);
        }

        public FakeTenantContext Tenant { get; }
        public FakeWorkflowTemplateStore Templates { get; }
        public FakeWorkflowMediator Mediator { get; }
        public TaskReviewService Service { get; }
    }
}
