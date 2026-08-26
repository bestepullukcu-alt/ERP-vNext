using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

/// <summary>
/// Faz 3b — the instance-identity collision, against a REAL MongoDB.
///
/// <para><b>The defect this prevents.</b> A task's approval gate finds its instance with
/// <c>GetLatestByObjectRefAsync</c>, which returns the LATEST instance for an object reference. Review is a second
/// MOD-0023 decision on the SAME task, so if it were started under the same reference the gate would read
/// whichever ran last: an approved task would report "waiting for review", or a task awaiting review would report
/// itself approved. Silent, and it breaks the approval flow that already exists.</para>
///
/// <para><b>Why real Mongo.</b> Every other test substitutes IWorkflowInstanceRepository with a fake, so the query
/// is never issued — that is exactly how the parallel-array sort defect reached production with 1595 tests green.
/// The separation asserted here is a property of the QUERY's filter, so only the real query can prove it.</para>
/// </summary>
public sealed class TaskReviewInstanceSeparationMongoTests : IAsyncLifetime
{
    private static readonly Guid TaskId = Guid.Parse("835dc3ef-56be-437f-9a5e-7df1b1931324");

    private MongoIntegrationHarness _harness = null!;
    private WorkflowInstanceRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _harness = await MongoIntegrationHarness.CreateAsync(SchemaProfile.WorkflowWorkCenter);
        _repository = new WorkflowInstanceRepository(_harness.DbContext, _harness.TenantContext);
    }

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    // ── The regression the whole slice hangs on ──────────────────────────────

    [Fact]
    public async Task The_approval_gate_reads_the_APPROVAL_instance_even_when_a_review_is_running()
    {
        /*
         * Both decisions are live on one task, and the REVIEW started later — so "latest for this object" would
         * pick the review if the two shared a reference. The approval lookup must still find the approval.
         */
        var approval = await SeedApprovalAsync(startedAt: DateTimeOffset.UtcNow.AddHours(-2));
        await SeedReviewAsync(startedAt: DateTimeOffset.UtcNow.AddMinutes(-5));

        var found = await _repository.GetLatestByObjectRefAsync(
            TaskApprovalService.BuildObjectRef(TaskId),
            TaskApprovalService.ApprovalObjectType,
            TaskId.ToString());

        Assert.NotNull(found);
        Assert.Equal(approval.Id, found!.Id);
    }

    [Fact]
    public async Task The_review_gate_reads_the_REVIEW_instance_even_when_an_approval_is_running()
    {
        // The mirror image: the approval started later this time, and the review lookup must still find the review.
        var review = await SeedReviewAsync(startedAt: DateTimeOffset.UtcNow.AddHours(-2));
        await SeedApprovalAsync(startedAt: DateTimeOffset.UtcNow.AddMinutes(-5));

        var found = await _repository.GetLatestByObjectRefAsync(
            TaskReviewService.BuildObjectRef(TaskId),
            TaskReviewService.ReviewObjectType,
            TaskId.ToString());

        Assert.NotNull(found);
        Assert.Equal(review.Id, found!.Id);
    }

    [Fact]
    public async Task The_two_references_are_actually_different()
    {
        /*
         * Non-vacuity for both tests above: if the two builders ever produced the same reference, each lookup
         * would still "find something" and both assertions could pass by accident on a one-instance database.
         */
        Assert.NotEqual(TaskApprovalService.BuildObjectRef(TaskId), TaskReviewService.BuildObjectRef(TaskId));
        Assert.NotEqual(TaskApprovalService.ApprovalObjectType, TaskReviewService.ReviewObjectType);
    }

    [Fact]
    public async Task An_approval_lookup_finds_nothing_when_only_a_review_exists()
    {
        /*
         * The separation must be total, not merely preferential. A task that is only under review has NO approval
         * instance, and the gate has to say "no workflow" rather than borrow the review's verdict — otherwise a
         * review rejection would read as an approval rejection.
         */
        await SeedReviewAsync(startedAt: DateTimeOffset.UtcNow);

        var found = await _repository.GetLatestByObjectRefAsync(
            TaskApprovalService.BuildObjectRef(TaskId),
            TaskApprovalService.ApprovalObjectType,
            TaskId.ToString());

        Assert.Null(found);
    }

    [Fact]
    public async Task A_review_lookup_finds_nothing_when_only_an_approval_exists()
    {
        await SeedApprovalAsync(startedAt: DateTimeOffset.UtcNow);

        var found = await _repository.GetLatestByObjectRefAsync(
            TaskReviewService.BuildObjectRef(TaskId),
            TaskReviewService.ReviewObjectType,
            TaskId.ToString());

        Assert.Null(found);
    }

    // ── Backward compatibility ───────────────────────────────────────────────

    [Fact]
    public async Task An_approval_written_before_this_slice_is_still_found()
    {
        /*
         * The compatibility guarantee, stated as a test rather than as a claim. Approval keeps the EXACT
         * reference it has always used, so every instance already in the database is still reachable — this seeds
         * one with the literal historical string rather than through the builder, so a change to the builder
         * cannot quietly move the goalposts.
         */
        const string historicalObjectRef = "tasks|task|835dc3ef-56be-437f-9a5e-7df1b1931324";
        var legacy = await SeedAsync("task", historicalObjectRef, DateTimeOffset.UtcNow.AddYears(-1));

        var found = await _repository.GetLatestByObjectRefAsync(
            TaskApprovalService.BuildObjectRef(TaskId),
            TaskApprovalService.ApprovalObjectType,
            TaskId.ToString());

        Assert.NotNull(found);
        Assert.Equal(legacy.Id, found!.Id);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private Task<WorkflowInstance> SeedApprovalAsync(DateTimeOffset startedAt) => SeedAsync(
        TaskApprovalService.ApprovalObjectType, TaskApprovalService.BuildObjectRef(TaskId), startedAt);

    private Task<WorkflowInstance> SeedReviewAsync(DateTimeOffset startedAt) => SeedAsync(
        TaskReviewService.ReviewObjectType, TaskReviewService.BuildObjectRef(TaskId), startedAt);

    private async Task<WorkflowInstance> SeedAsync(string objectType, string objectRef, DateTimeOffset startedAt)
    {
        var instance = new WorkflowInstance
        {
            TenantId = _harness.TenantId,
            CreatedAt = startedAt,
            TemplateId = Guid.NewGuid(),
            WorkflowTemplateId = Guid.NewGuid(),
            ObjectType = objectType,
            ObjectId = TaskId.ToString(),
            ObjectRef = objectRef,
            Status = WorkflowInstanceStatus.Active,
            StartedAt = startedAt
        };

        await _harness.Database
            .GetCollection<WorkflowInstance>("workflow_instances")
            .InsertOneAsync(instance);

        return instance;
    }
}
