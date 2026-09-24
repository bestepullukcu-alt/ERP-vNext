using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.Providers;
using Diten.Platform.Application.Features.WorkAggregation.Services;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.WorkAggregation;

/// <summary>
/// CT guards (BL-437 acceptance, 2026-09-24). Four sabotages of the resolver/projection left the agent's 17 tests
/// green: the review naming the OLDEST submitter, the resolver writing the id into the name slot, IsCurrentUser
/// never true, and a whitespace deep link kept as a link. Each is pinned here over the real provider + resolver.
/// </summary>
public sealed class ApprovalSourceContextCtTests
{
    private static readonly Guid Tenant = TaskTestData.Tenant;
    private static readonly Guid Me = TaskTestData.Me;
    private static readonly Guid Holder = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid Earlier = Guid.Parse("66666666-7777-8888-9999-000000000000");
    private const string TaskTitle = "Q3 bütçe revizyonunu hazırla";

    private readonly WorkItemProjectionService _projection = new(Tasks.SlaForTests.Real());

    [Fact]
    public async Task A_review_names_the_LATEST_submitter_when_the_task_was_submitted_more_than_once()
    {
        // Earlier submitted first (and was returned); Holder submitted the round that is open now.
        var task = NewTask(creator: Me, assignee: Holder);
        var transitions = new FakeTaskTransitionRepository();
        await transitions.CreateAsync(Transition(task.Id, TaskTransitionKind.SubmittedForReview, Earlier, DateTime.UtcNow.AddMinutes(-30)));
        await transitions.CreateAsync(Transition(task.Id, TaskTransitionKind.SubmittedForReview, Holder, DateTime.UtcNow));

        var item = Assert.Single(await Provider(task, transitions, TaskReviewService.ReviewObjectType,
            new FakeUserDisplayNameResolver((Holder, "Ayşe Kaya"), (Earlier, "Eski Gönderen"))).GetWorkItemsAsync(Actor(Me)));

        Assert.Equal(Holder.ToString(), item.Requester!.Id);
        Assert.Equal("Ayşe Kaya", item.ArrivalReason!.Args!["name"]);
    }

    [Fact]
    public async Task At_the_resolver_an_unresolved_name_stays_null_and_the_sentence_is_the_nameless_one()
    {
        var task = NewTask(creator: Holder, assignee: null);

        var item = Assert.Single(await Provider(task, new FakeTaskTransitionRepository(),
            TaskApprovalService.ApprovalObjectType, new FakeUserDisplayNameResolver()).GetWorkItemsAsync(Actor(Me)));

        Assert.Equal(Holder.ToString(), item.Requester!.Id);
        Assert.Null(item.Requester.DisplayName); // never the id in the name slot
        Assert.Equal("WorkAggregation_ArrivalReason_SentForApprovalUnnamed", item.ArrivalReason!.Key);
    }

    [Fact]
    public async Task The_reader_who_submitted_the_review_themselves_is_marked_as_the_current_user()
    {
        var task = NewTask(creator: Holder, assignee: Me);
        var transitions = new FakeTaskTransitionRepository();
        await transitions.CreateAsync(Transition(task.Id, TaskTransitionKind.SubmittedForReview, Me));

        var item = Assert.Single(await Provider(task, transitions, TaskReviewService.ReviewObjectType,
            new FakeUserDisplayNameResolver((Me, "Ben"))).GetWorkItemsAsync(Actor(Me)));

        Assert.Equal(Me.ToString(), item.Requester!.Id);
        Assert.True(item.Requester.IsCurrentUser);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_deep_link_from_the_owner_is_no_link_at_all(string deepLink)
    {
        var instance = Instance(TaskApprovalService.ApprovalObjectType, Guid.NewGuid().ToString());
        var context = new ApprovalSourceContext(TaskTitle, new WorkItemPersonDto(Holder.ToString(), "Ayşe Kaya"), deepLink);

        var dto = _projection.Project(Approval(instance), instance, Actor(Me), "workflow", "1.0", context)!;

        Assert.Null(dto.Source.DeepLink);
    }

    // ── helpers (the shape of ApprovalSourceContextTests, kept private there) ─────────────────────────────

    private static WorkItemActor Actor(Guid user) => new(user, IsPlatformActor: true, new HashSet<string>());

    private static ApprovalTask Approval(WorkflowInstance instance) => new()
    {
        TenantId = Tenant,
        WorkflowInstanceId = instance.Id,
        StageCode = "stage-1",
        StepCode = "step-1",
        Status = ApprovalTaskStatus.WaitingApproval,
        AssigneeRef = Me.ToString()
    };

    private static WorkflowInstance Instance(string objectType, string objectId) => new()
    {
        TenantId = Tenant,
        TemplateId = Guid.NewGuid(),
        WorkflowTemplateId = Guid.NewGuid(),
        ObjectType = objectType,
        ObjectId = objectId,
        ObjectRef = $"tasks|{objectType}|{objectId}"
    };

    private static TaskItem NewTask(Guid creator, Guid? assignee) => new()
    {
        TenantId = Tenant,
        Title = TaskTitle,
        OrganizationUnitId = Guid.NewGuid(),
        AssignmentTarget = assignee is null ? TaskAssignmentTarget.SelfAssigned : TaskAssignmentTarget.Person,
        AssigneeUserId = assignee,
        CreatedByUserId = creator
    };

    private static TaskTransition Transition(Guid taskId, TaskTransitionKind kind, Guid actor, DateTime? createdAt = null) => new()
    {
        TenantId = Tenant,
        TaskItemId = taskId,
        Kind = kind,
        FromLifecycle = TaskLifecycle.InProgress,
        ToLifecycle = TaskLifecycle.PendingReview,
        ActorUserId = actor,
        CreatedAt = createdAt ?? DateTime.UtcNow
    };

    private WorkflowApprovalWorkItemProvider Provider(TaskItem task, FakeTaskTransitionRepository transitions, string objectType, FakeUserDisplayNameResolver names)
    {
        var instance = Instance(objectType, task.Id.ToString());
        return new WorkflowApprovalWorkItemProvider(
            new ApprovalStore(Approval(instance)),
            new NoSnapshots(),
            new InstanceStore(instance),
            _projection,
            [new TaskApprovalSourceResolver(new FakeTaskItemRepository(task), transitions, names)]);
    }

    private sealed class ApprovalStore(params ApprovalTask[] tasks) : IApprovalTaskRepository
    {
        public Task<IReadOnlyList<ApprovalTask>> GetAllForTenantAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ApprovalTask>>(tasks.ToList());
        public Task<ApprovalTask> CreateAsync(ApprovalTask task, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ApprovalTask?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ApprovalTask?> GetFirstByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ApprovalTask?> GetActiveByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ApprovalTask>> ListByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> UpdateAsync(ApprovalTask task, int expectedVersion, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> UpdateEscalationAsync(ApprovalTask task, int expectedVersion, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class NoSnapshots : IRuntimeAssignmentSnapshotRepository
    {
        public Task<RuntimeAssignmentSnapshot?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<RuntimeAssignmentSnapshot?>(null);
        public Task<RuntimeAssignmentSnapshot> CreateAsync(RuntimeAssignmentSnapshot snapshot, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RuntimeAssignmentSnapshot>> ListByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class InstanceStore(params WorkflowInstance[] instances) : IWorkflowInstanceRepository
    {
        public Task<WorkflowInstance?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(instances.FirstOrDefault(i => i.Id == id));
        public Task<WorkflowInstance> CreateAsync(WorkflowInstance instance, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<WorkflowInstance?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<WorkflowInstance?> GetLatestByObjectRefAsync(string objectRef, string objectType, string objectId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<WorkflowInstance>> GetAllForTenantAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> UpdateAsync(WorkflowInstance instance, int expectedVersion, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
