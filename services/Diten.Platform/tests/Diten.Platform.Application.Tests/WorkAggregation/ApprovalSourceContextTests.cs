using System.Text.Json;
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
/// BL-437 — an approval says WHAT it is about. The owner's words: "a task I assigned to someone comes back to me for
/// approval, the title is nonsense, and I cannot tell why it came back." The row read "Onay: task-review 3f2c…".
///
/// <para>Two layers are measured. The PROJECTION places what it is given (title, requester, link, reason) and falls
/// back to today's shape when given nothing. The PROVIDER + MOD-0024's resolver produce those answers from the real
/// task — including WHO sent it, which differs by approval type: a review names the holder who submitted it, never
/// the task's creator, because the creator is usually the very person reading the row.</para>
/// </summary>
public sealed class ApprovalSourceContextTests
{
    private static readonly Guid Tenant = TaskTestData.Tenant;
    private static readonly Guid Me = TaskTestData.Me;
    private static readonly Guid Holder = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private const string HolderName = "Ayşe Kaya";
    private const string TaskTitle = "Q3 bütçe revizyonunu hazırla";

    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    private readonly WorkItemProjectionService _projection = new(Tasks.SlaForTests.Real());

    // ── Projection ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void With_a_source_context_the_title_is_the_source_objects_own_title()
    {
        var instance = Instance(TaskReviewService.ReviewObjectType, Guid.NewGuid().ToString());

        var dto = _projection.Project(Approval(instance), instance, Actor(), "workflow", "1.0", Context())!;

        Assert.Equal(WorkItemContract.LabelDisplay, dto.Title.Kind);
        Assert.Equal(TaskTitle, dto.Title.Text);
        Assert.Null(dto.Title.Key);
    }

    [Fact]
    public void The_title_never_carries_the_source_guid_or_the_object_type()
    {
        var objectId = Guid.NewGuid().ToString();
        var instance = Instance(TaskReviewService.ReviewObjectType, objectId);

        var dto = _projection.Project(Approval(instance), instance, Actor(), "workflow", "1.0", Context())!;

        var titleJson = JsonSerializer.Serialize(dto.Title, WebOptions);
        Assert.DoesNotContain(objectId, titleJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(TaskReviewService.ReviewObjectType, titleJson, StringComparison.Ordinal);
    }

    [Fact]
    public void The_requester_is_the_person_the_source_owner_named()
    {
        var instance = Instance(TaskApprovalService.ApprovalObjectType, Guid.NewGuid().ToString());

        var dto = _projection.Project(Approval(instance), instance, Actor(), "workflow", "1.0", Context())!;

        Assert.NotNull(dto.Requester);
        Assert.Equal(Holder.ToString(), dto.Requester!.Id);
        Assert.Equal(HolderName, dto.Requester.DisplayName);
    }

    [Fact]
    public void The_source_carries_the_owners_deep_link()
    {
        var taskId = Guid.NewGuid();
        var instance = Instance(TaskApprovalService.ApprovalObjectType, taskId.ToString());

        var dto = _projection.Project(
            Approval(instance), instance, Actor(), "workflow", "1.0",
            Context(deepLink: TaskLinks.Record(taskId)))!;

        Assert.Equal($"/Tasks/{taskId}", dto.Source.DeepLink);
        // The decision is still taken on the row: a link to READ the task does not turn the item into a deep-link item.
        Assert.Equal(WorkItemContract.DepthInline, dto.ActionDepth);
    }

    [Fact]
    public void The_reason_names_who_sent_it_as_one_translated_sentence()
    {
        var instance = Instance(TaskReviewService.ReviewObjectType, Guid.NewGuid().ToString());

        var dto = _projection.Project(Approval(instance), instance, Actor(), "workflow", "1.0", Context())!;

        Assert.NotNull(dto.ArrivalReason);
        Assert.Equal(WorkItemContract.LabelResource, dto.ArrivalReason!.Kind);
        Assert.Equal("WorkAggregation_ArrivalReason_SentForApproval", dto.ArrivalReason.Key);
        Assert.Equal(HolderName, dto.ArrivalReason.Args!["name"]);
    }

    [Fact]
    public void An_unresolved_name_gets_the_whole_nameless_sentence_never_the_id()
    {
        var instance = Instance(TaskReviewService.ReviewObjectType, Guid.NewGuid().ToString());
        var nameless = new ApprovalSourceContext(TaskTitle, new WorkItemPersonDto(Holder.ToString()), "/Tasks/x");

        var dto = _projection.Project(Approval(instance), instance, Actor(), "workflow", "1.0", nameless)!;

        Assert.Equal("WorkAggregation_ArrivalReason_SentForApprovalUnnamed", dto.ArrivalReason!.Key);
        Assert.Null(dto.ArrivalReason.Args);
        Assert.DoesNotContain(Holder.ToString(), JsonSerializer.Serialize(dto.ArrivalReason, WebOptions));
    }

    [Fact]
    public void Without_a_source_context_the_item_keeps_todays_shape()
    {
        var instance = Instance("invoice", "INV-1");

        var dto = _projection.Project(Approval(instance), instance, Actor(), "workflow", "1.0")!;

        Assert.Equal("WorkAggregation_Title_Approval", dto.Title.Key);
        Assert.Null(dto.Requester);
        Assert.Null(dto.Source.DeepLink);
        Assert.Null(dto.ArrivalReason);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(dto, WebOptions));
        Assert.False(json.RootElement.TryGetProperty("arrivalReason", out _), "an absent reason must be omitted, not null");
        Assert.False(json.RootElement.TryGetProperty("requester", out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_source_without_a_title_falls_back_to_the_generic_title(string? title)
    {
        var instance = Instance(TaskApprovalService.ApprovalObjectType, Guid.NewGuid().ToString());

        var dto = _projection.Project(
            Approval(instance), instance, Actor(), "workflow", "1.0", Context(title: title))!;

        Assert.Equal(WorkItemContract.LabelResource, dto.Title.Kind);
        Assert.Equal("WorkAggregation_Title_Approval", dto.Title.Key);
        // Everything else the owner answered still arrives.
        Assert.NotNull(dto.Requester);
        Assert.NotNull(dto.ArrivalReason);
    }

    [Fact]
    public void The_reason_serializes_as_a_resource_label_the_contract_accepts()
    {
        var instance = Instance(TaskReviewService.ReviewObjectType, Guid.NewGuid().ToString());
        var dto = _projection.Project(Approval(instance), instance, Actor(), "workflow", "1.0", Context())!;

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(dto, WebOptions));
        var reason = json.RootElement.GetProperty("arrivalReason");

        Assert.Equal("resource", reason.GetProperty("kind").GetString());
        Assert.Equal("WorkAggregation_ArrivalReason_SentForApproval", reason.GetProperty("key").GetString());
        Assert.Equal(HolderName, reason.GetProperty("args").GetProperty("name").GetString());
        // fixture-contract.js isLabel: a resource label must have NO `text` member at all.
        Assert.False(reason.TryGetProperty("text", out _));
    }

    // ── Provider + MOD-0024 resolver, end to end ──────────────────────────────────────────────────────────

    [Fact]
    public async Task A_review_names_the_holder_who_submitted_it_not_the_creator_reading_it()
    {
        // The owner's own scenario: Me created the task and assigned it to Holder; Holder submitted it for review;
        // the review lands with Me. "Me sent this to you" would be exactly the confusion being fixed.
        var task = NewTask(creator: Me, assignee: Holder);
        var transitions = new FakeTaskTransitionRepository();
        await transitions.CreateAsync(Transition(task.Id, TaskTransitionKind.SubmittedForReview, Holder));

        var (provider, _) = ProviderOver(task, transitions, TaskReviewService.ReviewObjectType);

        var item = Assert.Single(await provider.GetWorkItemsAsync(Actor()));

        Assert.Equal(TaskTitle, item.Title.Text);
        Assert.Equal(Holder.ToString(), item.Requester!.Id);
        Assert.Equal(HolderName, item.Requester.DisplayName);
        Assert.False(item.Requester.IsCurrentUser);
        Assert.Equal(TaskLinks.Record(task.Id), item.Source.DeepLink);
        Assert.Equal(HolderName, item.ArrivalReason!.Args!["name"]);
    }

    [Fact]
    public async Task A_review_with_no_submit_entry_falls_back_to_the_current_holder()
    {
        var task = NewTask(creator: Me, assignee: Holder);

        var (provider, _) = ProviderOver(task, new FakeTaskTransitionRepository(), TaskReviewService.ReviewObjectType);

        var item = Assert.Single(await provider.GetWorkItemsAsync(Actor()));
        Assert.Equal(Holder.ToString(), item.Requester!.Id);
    }

    [Fact]
    public async Task An_approval_names_the_tasks_creator()
    {
        var task = NewTask(creator: Holder, assignee: null);

        var (provider, transitions) = ProviderOver(
            task, new FakeTaskTransitionRepository(), TaskApprovalService.ApprovalObjectType);

        var item = Assert.Single(await provider.GetWorkItemsAsync(Actor()));
        Assert.Equal(Holder.ToString(), item.Requester!.Id);
        Assert.Equal(TaskTitle, item.Title.Text);
        Assert.Equal(TaskLinks.Record(task.Id), item.Source.DeepLink);
        // Approval does not need the history, so it does not read it.
        Assert.Equal(0, transitions.ListByTaskIdsCalls);
    }

    [Fact]
    public async Task A_work_request_names_the_tasks_creator()
    {
        var task = NewTask(creator: Holder, assignee: Me);

        var (provider, _) = ProviderOver(
            task, new FakeTaskTransitionRepository(), TaskUpwardRequestService.RequestObjectType);

        var item = Assert.Single(await provider.GetWorkItemsAsync(Actor()));
        Assert.Equal(Holder.ToString(), item.Requester!.Id);
    }

    [Fact]
    public async Task A_missing_source_task_keeps_the_generic_title_and_no_reason()
    {
        var instance = Instance(TaskApprovalService.ApprovalObjectType, Guid.NewGuid().ToString());
        var provider = new WorkflowApprovalWorkItemProvider(
            new ApprovalStore(Approval(instance)),
            new NoSnapshots(),
            new InstanceStore(instance),
            _projection,
            [Resolver(new FakeTaskItemRepository(), new FakeTaskTransitionRepository())]);

        var item = Assert.Single(await provider.GetWorkItemsAsync(Actor()));
        Assert.Equal("WorkAggregation_Title_Approval", item.Title.Key);
        Assert.Null(item.ArrivalReason);
        Assert.Null(item.Source.DeepLink);
    }

    [Fact]
    public async Task A_failing_resolver_does_not_take_the_approval_off_the_board()
    {
        var instance = Instance(TaskApprovalService.ApprovalObjectType, Guid.NewGuid().ToString());
        var provider = new WorkflowApprovalWorkItemProvider(
            new ApprovalStore(Approval(instance)),
            new NoSnapshots(),
            new InstanceStore(instance),
            _projection,
            [new ThrowingResolver()]);

        var item = Assert.Single(await provider.GetWorkItemsAsync(Actor()));
        Assert.Equal("WorkAggregation_Title_Approval", item.Title.Key);
        Assert.NotEmpty(item.Actions); // the decision is still there to take
    }

    [Fact]
    public async Task Objects_of_another_module_are_not_answered_by_the_task_resolver()
    {
        var instance = Instance("invoice", Guid.NewGuid().ToString());
        var provider = new WorkflowApprovalWorkItemProvider(
            new ApprovalStore(Approval(instance)),
            new NoSnapshots(),
            new InstanceStore(instance),
            _projection,
            [Resolver(new FakeTaskItemRepository(), new FakeTaskTransitionRepository())]);

        var item = Assert.Single(await provider.GetWorkItemsAsync(Actor()));
        Assert.Equal("WorkAggregation_Title_Approval", item.Title.Key);
    }

    [Fact]
    public async Task A_page_of_reviews_is_answered_with_one_read_of_each_collection()
    {
        var first = NewTask(creator: Me, assignee: Holder);
        var second = NewTask(creator: Me, assignee: Holder);
        var tasks = new FakeTaskItemRepository(first, second);
        var transitions = new FakeTaskTransitionRepository();
        var names = new FakeUserDisplayNameResolver((Holder, HolderName));

        var i1 = Instance(TaskReviewService.ReviewObjectType, first.Id.ToString());
        var i2 = Instance(TaskReviewService.ReviewObjectType, second.Id.ToString());
        var provider = new WorkflowApprovalWorkItemProvider(
            new ApprovalStore(Approval(i1), Approval(i2)),
            new NoSnapshots(),
            new InstanceStore(i1, i2),
            _projection,
            [new TaskApprovalSourceResolver(tasks, transitions, names)]);

        Assert.Equal(2, (await provider.GetWorkItemsAsync(Actor())).Count);
        Assert.Equal(1, transitions.ListByTaskIdsCalls);
        Assert.Equal(0, transitions.ListByTaskIdCalls);
        Assert.Single(names.Calls);
    }

    // ── helpers ───────────────────────────────────────────────────────────────────────────────────────────

    private static WorkItemActor Actor() => new(Me, IsPlatformActor: true, new HashSet<string>());

    private static ApprovalSourceContext Context(string? title = TaskTitle, string? deepLink = "/Tasks/any")
        => new(title, new WorkItemPersonDto(Holder.ToString(), HolderName), deepLink);

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

    private static TaskTransition Transition(Guid taskId, TaskTransitionKind kind, Guid actor) => new()
    {
        TenantId = Tenant,
        TaskItemId = taskId,
        Kind = kind,
        FromLifecycle = TaskLifecycle.InProgress,
        ToLifecycle = TaskLifecycle.PendingReview,
        ActorUserId = actor
    };

    private static TaskApprovalSourceResolver Resolver(ITaskItemRepository tasks, ITaskTransitionRepository transitions)
        => new(tasks, transitions, new FakeUserDisplayNameResolver((Holder, HolderName)));

    private (WorkflowApprovalWorkItemProvider Provider, FakeTaskTransitionRepository Transitions) ProviderOver(
        TaskItem task, FakeTaskTransitionRepository transitions, string objectType)
    {
        var instance = Instance(objectType, task.Id.ToString());
        var provider = new WorkflowApprovalWorkItemProvider(
            new ApprovalStore(Approval(instance)),
            new NoSnapshots(),
            new InstanceStore(instance),
            _projection,
            [Resolver(new FakeTaskItemRepository(task), transitions)]);
        return (provider, transitions);
    }

    private sealed class ThrowingResolver : IApprovalSourceResolver
    {
        public bool Handles(string objectType) => true;

        public Task<IReadOnlyDictionary<Guid, ApprovalSourceContext>> ResolveAsync(
            IReadOnlyCollection<WorkflowInstance> instances, WorkItemActor actor, CancellationToken ct = default)
            => throw new InvalidOperationException("source owner unavailable");
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
        public Task<RuntimeAssignmentSnapshot?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<RuntimeAssignmentSnapshot?>(null);

        public Task<RuntimeAssignmentSnapshot> CreateAsync(RuntimeAssignmentSnapshot snapshot, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RuntimeAssignmentSnapshot>> ListByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class InstanceStore(params WorkflowInstance[] instances) : IWorkflowInstanceRepository
    {
        public Task<WorkflowInstance?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(instances.FirstOrDefault(i => i.Id == id));

        public Task<WorkflowInstance> CreateAsync(WorkflowInstance instance, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<WorkflowInstance?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<WorkflowInstance?> GetLatestByObjectRefAsync(string objectRef, string objectType, string objectId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<WorkflowInstance>> GetAllForTenantAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> UpdateAsync(WorkflowInstance instance, int expectedVersion, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
