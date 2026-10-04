using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Application.Features.Workflow.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Workflow.Queries;
using Diten.Platform.Application.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// WP-WF-ENGINE-CANDIDATES-01 · D2 fix round 2, item 1 — a task that HOLDS a link to a MOD-0023 instance is gated by it,
/// whatever its requirement flag says, and cancelling the task withdraws what it started.
///
/// <para>The chain is real from the edit to the engine's gate: <see cref="UpdateTaskItemHandler"/>,
/// <see cref="TransitionTaskItemHandler"/>, the real <see cref="TaskApprovalService"/> / <see cref="TaskReviewService"/>
/// withdraws, the real <see cref="WorkflowTransitionGate"/> and the real <see cref="EvaluateWorkflowTransitionGateHandler"/>
/// over in-memory MOD-0023 stores. Only MOD-0023's own cancel command is answered here — "unavailable" (503) or done —
/// because what is under test is what Tasks does with that answer.</para>
/// </summary>
public sealed partial class TaskWorkflowLinkGateTests
{
    private static readonly Guid Unit = Guid.Parse("0f0f0f0f-0f0f-0f0f-0f0f-0f0f0f0f0f0f");

    // ── a withdraw that failed: the link holds the gate until the instance closes ─────────────────────────────

    [Fact]
    public async Task An_approval_whose_withdraw_failed_holds_start_until_saving_again_withdraws_it()
    {
        var f = new Fixture(TaskLifecycle.Open, approval: WorkflowInstanceStatus.Active);
        f.Engine.Down = true;

        var edit = await f.EditAsync(approvalRequired: false, expectedVersion: f.Task.Version);

        Assert.Equal(409, edit.StatusCode);
        Assert.Equal(TaskReasonCodes.ApprovalCancelFailed, edit.ReasonCode);
        var stored = f.Stored();
        Assert.False(stored.ApprovalRequired);                 // the edit landed …
        Assert.NotNull(stored.WorkflowInstanceId);              // … the link did not go
        Assert.Equal(stored.Version, edit.Data!.Version);       // … and the refusal says at which version

        var start = await f.MoveAsync(TaskLifecycle.InProgress);
        Assert.Equal(409, start.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowPendingApproval, start.ReasonCode);
        Assert.Equal(TaskLifecycle.Open, f.Stored().Lifecycle);

        // "Save again" — exactly what the form sends: the version the refusal carried.
        f.Engine.Down = false;
        var again = await f.EditAsync(approvalRequired: false, expectedVersion: edit.Data.Version);
        Assert.Equal(204, again.StatusCode);
        Assert.Null(f.Stored().WorkflowInstanceId);

        Assert.Equal(204, (await f.MoveAsync(TaskLifecycle.InProgress)).StatusCode);
        Assert.Equal(204, (await f.MoveAsync(TaskLifecycle.Done)).StatusCode);
    }

    [Fact]
    public async Task A_running_task_whose_approval_withdraw_failed_cannot_be_completed_until_the_approver_closes_it()
    {
        // Approval switched on while the work was running, then off again — and the withdraw fell over (503).
        var f = new Fixture(TaskLifecycle.InProgress, approval: WorkflowInstanceStatus.Active);
        f.Engine.Down = true;
        Assert.Equal(409, (await f.EditAsync(approvalRequired: false, expectedVersion: f.Task.Version)).StatusCode);

        var complete = await f.MoveAsync(TaskLifecycle.Done);

        Assert.Equal(409, complete.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowPendingApproval, complete.ReasonCode);
        Assert.Null(f.Stored().CompletedAt);

        // The approver decides the instance nobody withdrew: it is closed, so the task — whose requirement is off — goes.
        f.Engine.Decide(f.ApprovalInstance!, WorkflowInstanceStatus.Completed, ApprovalTaskStatus.Approved);
        Assert.Equal(204, (await f.MoveAsync(TaskLifecycle.Done)).StatusCode);
        Assert.Equal(TaskLifecycle.Done, f.Stored().Lifecycle);
    }

    [Theory]
    [InlineData(WorkflowInstanceStatus.Cancelled)]
    [InlineData(WorkflowInstanceStatus.Rejected)]
    public async Task A_link_to_an_instance_the_engine_closed_holds_nothing_once_the_requirement_is_off(WorkflowInstanceStatus closed)
    {
        var withdrawn = new Fixture(TaskLifecycle.InProgress, approval: closed, approvalRequired: false);
        Assert.Equal(204, (await withdrawn.MoveAsync(TaskLifecycle.Done)).StatusCode);

        // With the requirement ON the same instance still holds the task — that rule is unchanged.
        var required = new Fixture(TaskLifecycle.InProgress, approval: closed, approvalRequired: true);
        Assert.Equal(409, (await required.MoveAsync(TaskLifecycle.Done)).StatusCode);
    }

    [Fact]
    public async Task A_review_whose_withdraw_failed_holds_completion_until_the_review_is_closed()
    {
        var f = new Fixture(TaskLifecycle.PendingReview, review: WorkflowInstanceStatus.Active);
        f.Engine.Down = true;

        var edit = await f.EditAsync(approvalRequired: null, expectedVersion: f.Task.Version, reviewRequired: false);

        Assert.Equal(409, edit.StatusCode);
        Assert.Equal(TaskReasonCodes.ReviewCancelFailed, edit.ReasonCode);
        Assert.Equal(f.Stored().Version, edit.Data!.Version);
        Assert.False(f.Stored().ReviewRequired);

        // PendingReview → Done is a legal move; the open review is what refuses it.
        var complete = await f.MoveAsync(TaskLifecycle.Done);
        Assert.Equal(409, complete.StatusCode);
        Assert.Equal(TaskReasonCodes.ReviewPending, complete.ReasonCode);

        f.Engine.Decide(f.ReviewInstance!, WorkflowInstanceStatus.Completed, ApprovalTaskStatus.Approved);
        Assert.Equal(204, (await f.MoveAsync(TaskLifecycle.Done)).StatusCode);
    }

    // ── cancelling the task withdraws what it started ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Cancelling_a_task_withdraws_its_running_approval_and_a_withdraw_that_fails_refuses_the_cancel()
    {
        var f = new Fixture(TaskLifecycle.Open, approval: WorkflowInstanceStatus.Active);
        f.Engine.Down = true;

        var refused = await f.MoveAsync(TaskLifecycle.Cancelled);

        Assert.Equal(409, refused.StatusCode);
        Assert.Equal(TaskReasonCodes.ApprovalCancelFailed, refused.ReasonCode);
        Assert.Null(f.Stored().CancelledAt);
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, f.Engine.TaskOf(f.ApprovalInstance!).Status);

        f.Engine.Down = false;
        var cancelled = await f.MoveAsync(TaskLifecycle.Cancelled);

        Assert.Equal(204, cancelled.StatusCode);
        Assert.Equal(TaskLifecycle.Cancelled, f.Stored().Lifecycle);
        Assert.Equal(ApprovalTaskStatus.Cancelled, f.Engine.TaskOf(f.ApprovalInstance!).Status);
        Assert.Equal(WorkflowInstanceStatus.Cancelled, f.ApprovalInstance!.Status);
    }

    [Fact]
    public async Task Cancelling_a_task_withdraws_its_running_review_and_a_withdraw_that_fails_refuses_the_cancel()
    {
        var f = new Fixture(TaskLifecycle.PendingReview, review: WorkflowInstanceStatus.Active);
        f.Engine.Down = true;

        var refused = await f.MoveAsync(TaskLifecycle.Cancelled);

        Assert.Equal(409, refused.StatusCode);
        Assert.Equal(TaskReasonCodes.ReviewCancelFailed, refused.ReasonCode);
        Assert.Equal(TaskLifecycle.PendingReview, f.Stored().Lifecycle);

        f.Engine.Down = false;
        Assert.Equal(204, (await f.MoveAsync(TaskLifecycle.Cancelled)).StatusCode);
        Assert.Equal(ApprovalTaskStatus.Cancelled, f.Engine.TaskOf(f.ReviewInstance!).Status);
    }

    [Fact]
    public async Task A_cancel_on_a_stale_version_withdraws_nothing()
    {
        var f = new Fixture(TaskLifecycle.Open, approval: WorkflowInstanceStatus.Active);

        var stale = await f.MoveAsync(TaskLifecycle.Cancelled, expectedVersion: f.Task.Version - 1);

        Assert.Equal(409, stale.StatusCode);
        Assert.Equal(TaskReasonCodes.ConcurrencyConflict, stale.ReasonCode);
        Assert.Empty(f.Engine.CancelCalls);
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, f.Engine.TaskOf(f.ApprovalInstance!).Status);
    }

    [Fact]
    public async Task A_handler_built_without_the_withdraw_services_refuses_to_cancel_a_linked_task()
    {
        // Fail-closed: a cancel that cannot prove the withdraw does not close the task. An unlinked task is unaffected.
        var linked = new Fixture(TaskLifecycle.Open, approval: WorkflowInstanceStatus.Active, withWithdrawServices: false);
        var plain = new Fixture(TaskLifecycle.Open, withWithdrawServices: false);

        var refused = await linked.MoveAsync(TaskLifecycle.Cancelled);

        Assert.Equal(409, refused.StatusCode);
        Assert.Equal(TaskReasonCodes.ApprovalCancelFailed, refused.ReasonCode);
        Assert.Equal(204, (await plain.MoveAsync(TaskLifecycle.Cancelled)).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancelling_a_parent_withdraws_a_subtasks_approval_or_leaves_that_subtask_open(bool engineUp)
    {
        var f = new Fixture(TaskLifecycle.Open);
        var child = f.AddSubtaskWithApproval();
        f.Engine.Down = !engineUp;

        var cancelled = await f.MoveAsync(TaskLifecycle.Cancelled);

        Assert.Equal(204, cancelled.StatusCode); // the parent's own cancel is not held hostage by a child
        var storedChild = f.Stored(child.Task.Id);
        if (engineUp)
        {
            Assert.Equal(TaskLifecycle.Cancelled, storedChild.Lifecycle);
            Assert.Equal(ApprovalTaskStatus.Cancelled, f.Engine.TaskOf(child.Instance).Status);
        }
        else
        {
            // Its approval is still decidable: the child is not closed around it.
            Assert.Equal(TaskLifecycle.Open, storedChild.Lifecycle);
            Assert.Equal(ApprovalTaskStatus.WaitingApproval, f.Engine.TaskOf(child.Instance).Status);
        }
    }

    // ── fixture ───────────────────────────────────────────────────────────────────────────────────────────────

    private sealed partial class Fixture
    {
        private readonly FakeTaskItemRepository _tasks;
        private readonly UpdateTaskItemHandler _edit;
        private readonly TransitionTaskItemHandler _transition;

        public TaskItem Task { get; }
        public Engine Engine { get; }
        public WorkflowInstance? ApprovalInstance { get; }
        public WorkflowInstance? ReviewInstance { get; }

        public Fixture(
            TaskLifecycle lifecycle,
            WorkflowInstanceStatus? approval = null,
            WorkflowInstanceStatus? review = null,
            bool? approvalRequired = null,
            bool withWithdrawServices = true,
            bool? reviewRequired = null,
            Guid? createdBy = null)
        {
            var tenant = new FakeTenantContext(TaskTestData.Tenant);
            Tenant = tenant;
            Engine = new Engine(tenant);
            Task = new TaskItem
            {
                TenantId = TaskTestData.Tenant,
                Title = "Linked task",
                Lifecycle = lifecycle,
                AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
                AssigneeUserId = TaskTestData.Me,
                CreatedByUserId = createdBy ?? TaskTestData.Me,
                OrganizationUnitId = Unit,
                Version = 3
            };

            if (approval is { } approvalStatus)
            {
                ApprovalInstance = Engine.Seed(TaskApprovalService.ApprovalObjectType,
                    TaskApprovalService.BuildObjectRef(Task.Id), Task.Id, approvalStatus);
                Task.ApprovalRequired = approvalRequired ?? true;
                Task.ApprovalManagerUserId = TaskTestData.Rival;
                Task.WorkflowInstanceId = ApprovalInstance.Id;
            }

            if (review is { } reviewStatus)
            {
                ReviewInstance = Engine.Seed(TaskReviewService.ReviewObjectType,
                    TaskReviewService.BuildObjectRef(Task.Id), Task.Id, reviewStatus);
                Task.ReviewRequired = reviewRequired ?? true;
                Task.ReviewerCandidateUserId = TaskTestData.Rival;
                Task.ReviewWorkflowInstanceId = ReviewInstance.Id;
            }

            _tasks = new FakeTaskItemRepository(Task);
            var currentUser = new FakeCurrentUserContext(TaskTestData.Me);
            CurrentUser = currentUser;
            WithdrawServices = withWithdrawServices;
            var approvals = new TaskApprovalService(Engine, new PublishedTemplates(), Engine.Instances, Engine.Tasks, tenant,
                currentUser, Options.Create(new TaskApprovalOptions()), NullLogger<TaskApprovalService>.Instance);
            var reviews = new TaskReviewService(Engine, new PublishedTemplates(), Engine.Instances, Engine.Tasks, tenant, currentUser,
                Options.Create(new TaskReviewOptions()), NullLogger<TaskReviewService>.Instance);
            var fields = new TaskFieldDefinitionService(
                new FakeTaskFieldDefinitionRepository(), TaskRecordSourceDoubles.None, TaskActors.PermitAll());
            Approvals = approvals;
            Reviews = reviews;
            Fields = fields;

            _edit = new UpdateTaskItemHandler(
                _tasks,
                new FakeOrganizationUnitRepository(new OrganizationUnit
                {
                    Id = Unit, TenantId = TaskTestData.Tenant, Name = "HQ", Code = "HQ", LegalEntityId = Guid.NewGuid()
                }),
                fields, currentUser, approvals, reviews, NullLogger<UpdateTaskItemHandler>.Instance,
                TaskDocumentFreezerDoubles.OverAnEmptyRegister());
            _transition = new TransitionTaskItemHandler(
                _tasks, new TaskLifecycleService(), currentUser, new FakeChecklistRunRepository(), new TaskChecklistService(),
                new WorkflowTransitionGate(Engine, NullLogger<WorkflowTransitionGate>.Instance),
                new FakeTaskDependencyRepository(), new FakeTaskTypeRepository(), new FakeTaskNotificationService(),
                fields, new FakeTaskAttachmentRepository(), NullLogger<TransitionTaskItemHandler>.Instance,
                approvals: withWithdrawServices ? approvals : null,
                reviews: withWithdrawServices ? reviews : null);
        }

        public TaskItem Stored(Guid? id = null) => _tasks.GetByIdAsync(id ?? Task.Id).GetAwaiter().GetResult()!;

        public (TaskItem Task, WorkflowInstance Instance) AddSubtaskWithApproval()
        {
            var child = new TaskItem
            {
                TenantId = TaskTestData.Tenant,
                Title = "Subtask with its own approval",
                Lifecycle = TaskLifecycle.Open,
                AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
                AssigneeUserId = TaskTestData.Me,
                CreatedByUserId = TaskTestData.Me,
                OrganizationUnitId = Unit,
                ParentTaskItemId = Task.Id,
                ApprovalRequired = true,
                ApprovalManagerUserId = TaskTestData.Rival
            };
            var instance = Engine.Seed(TaskApprovalService.ApprovalObjectType,
                TaskApprovalService.BuildObjectRef(child.Id), child.Id, WorkflowInstanceStatus.Active);
            child.WorkflowInstanceId = instance.Id;
            _tasks.CreateAsync(child).GetAwaiter().GetResult();
            return (child, instance);
        }

        public Task<Response<TaskItemEditResultDto>> EditAsync(bool? approvalRequired, int expectedVersion, bool? reviewRequired = null)
        {
            var current = Stored();
            return _edit.Handle(new UpdateTaskItemCommand(current.Id, new UpdateTaskItemRequest(
                Title: current.Title,
                Description: null,
                Priority: TaskPriority.Medium,
                OrganizationUnitId: null,
                DueAt: null,
                StartAt: null,
                PlannedDate: null,
                EstimateHours: null,
                Tags: null,
                ReviewRequired: reviewRequired ?? current.ReviewRequired,
                EmailNotificationsEnabled: false,
                DelegationAllowed: false,
                FieldValues: null,
                ExpectedVersion: expectedVersion,
                ApprovalRequired: approvalRequired,
                ApprovalManagerUserId: null,
                ReviewerCandidateUserId: current.ReviewerCandidateUserId), "corr-edit"), CancellationToken.None);
        }

        public Task<Response<NoContent>> MoveAsync(TaskLifecycle target, int? expectedVersion = null) =>
            _transition.Handle(new TransitionTaskItemCommand(
                Task.Id, target, new TaskTransitionRequest(expectedVersion ?? Stored().Version, null, null), "corr-move"),
                CancellationToken.None);
    }

    /// <summary>
    /// MOD-0023 in memory: the REAL gate evaluation over its stores, and its cancel command answered as the engine would —
    /// "unavailable" (503) while <see cref="Down"/>, otherwise the open task and its instance are cancelled.
    /// </summary>
    private sealed class Engine : IMediator
    {
        private readonly EvaluateWorkflowTransitionGateHandler _evaluate;

        public Engine(ITenantContext tenant)
        {
            Instances = new InstanceStore(tenant);
            Tasks = new ApprovalTaskStore(tenant);
            _evaluate = new EvaluateWorkflowTransitionGateHandler(Instances, Tasks);
        }

        public InstanceStore Instances { get; }
        public ApprovalTaskStore Tasks { get; }
        public bool Down { get; set; }

        /// <summary>D2-FIX3 — instances whose withdraw answers "unavailable" while the others go through.</summary>
        public HashSet<Guid> FailFor { get; } = [];
        public List<CancelWorkflowTaskCommand> CancelCalls { get; } = [];

        /// <summary>D2-FIX4 — runs when MOD-0023's cancel command arrives, before it is answered: somebody else touches the task
        /// or the engine while the withdraw is under way (a start landing between the withdraw and a delete).</summary>
        public Action? WhileCancelling { get; set; }

        /// <summary>D2-FIX5 — every start command the engine was sent (a start by the services under test).</summary>
        public List<StartWorkflowInstanceCommand> StartCalls { get; } = [];

        /// <summary>D2-FIX5 — the instances the engine OPENED for a start (a replayed key opens none).</summary>
        public List<WorkflowInstance> Started { get; } = [];

        /// <summary>D2-FIX5 — the status a NEW instance is opened with.</summary>
        public WorkflowInstanceStatus StartStatus { get; set; } = WorkflowInstanceStatus.Active;

        /// <summary>D2-FIX5 — runs right after the engine opened a new instance and before it answers: a rival writer saves the
        /// task between MOD-0023's start and the caller's own task write.</summary>
        public Action? AfterStart { get; set; }

        public WorkflowInstance Seed(
            string objectType, string objectRef, Guid taskId, WorkflowInstanceStatus status, DateTimeOffset? startedAt = null,
            string? idempotencyKey = null)
        {
            var instance = new WorkflowInstance
            {
                TenantId = TaskTestData.Tenant,
                TemplateId = Guid.NewGuid(),
                WorkflowTemplateId = Guid.NewGuid(),
                ObjectType = objectType,
                ObjectId = taskId.ToString(),
                ObjectRef = objectRef,
                Status = status,
                StartedAt = startedAt ?? DateTimeOffset.UtcNow,
                IdempotencyKey = idempotencyKey
            };
            Instances.Items.Add(instance);
            Tasks.Items.Add(new ApprovalTask
            {
                TenantId = TaskTestData.Tenant,
                WorkflowInstanceId = instance.Id,
                Status = status is WorkflowInstanceStatus.Active ? ApprovalTaskStatus.WaitingApproval : ApprovalTaskStatus.Cancelled
            });
            return instance;
        }

        public ApprovalTask TaskOf(WorkflowInstance instance) => Tasks.Items.Single(t => t.WorkflowInstanceId == instance.Id);

        /// <summary>Somebody in MOD-0023 decides the instance (the approver, the reviewer).</summary>
        public void Decide(WorkflowInstance instance, WorkflowInstanceStatus instanceStatus, ApprovalTaskStatus taskStatus)
        {
            instance.Status = instanceStatus;
            TaskOf(instance).Status = taskStatus;
        }

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            switch (request)
            {
                case EvaluateWorkflowTransitionGateQuery query:
                    return (TResponse)(object)await _evaluate.Handle(query, ct);
                case StartWorkflowInstanceCommand start:
                    StartCalls.Add(start);
                    // The engine's idempotent start: a key that already names an instance answers THAT instance, whatever its status.
                    var key = start.Request.IdempotencyKey;
                    var opened = key is null ? null : Instances.Items.FirstOrDefault(i => i.IdempotencyKey == key);
                    var created = opened is null;
                    opened ??= Seed(start.Request.ObjectType, start.Request.ObjectRef!, Guid.Parse(start.Request.ObjectId), StartStatus,
                        startedAt: DateTimeOffset.UtcNow.AddMinutes(StartCalls.Count), idempotencyKey: key);
                    if (created)
                    {
                        Started.Add(opened);
                        AfterStart?.Invoke();
                    }

                    var firstTask = Tasks.Items.First(t => t.WorkflowInstanceId == opened.Id);
                    return (TResponse)(object)Response<StartWorkflowInstanceResponse>.Success(
                        new StartWorkflowInstanceResponse(opened.Id, opened.TemplateId, Guid.NewGuid(), firstTask.Id, Guid.NewGuid(),
                            opened.ObjectRef, opened.Status.ToString(), "stage-1", "step-1", opened.StartedAt, null, start.CorrelationId),
                        correlationId: start.CorrelationId);
                case CancelWorkflowTaskCommand cancel:
                    CancelCalls.Add(cancel);
                    WhileCancelling?.Invoke();
                    var task = Tasks.Items.Single(t => t.Id == cancel.TaskId);
                    if (Down || FailFor.Contains(task.WorkflowInstanceId))
                    {
                        return (TResponse)(object)Response<WorkflowTaskTransitionResponse>.Fail(
                            "The workflow transaction is unavailable.", 503,
                            WorkflowReasonCodes.WorkflowTransactionUnavailable, cancel.CorrelationId);
                    }

                    var instance = Instances.Items.Single(i => i.Id == task.WorkflowInstanceId);
                    task.Status = ApprovalTaskStatus.Cancelled;
                    instance.Status = WorkflowInstanceStatus.Cancelled;
                    return (TResponse)(object)Response<WorkflowTaskTransitionResponse>.Success(
                        new WorkflowTaskTransitionResponse(instance.Id, task.Id, "WaitingApproval", "Cancelled", "Active",
                            "Cancelled", "Cancel", false, Guid.NewGuid(), cancel.CorrelationId),
                        correlationId: cancel.CorrelationId);
                default:
                    throw new NotSupportedException($"Unexpected request in the engine: {request.GetType().Name}");
            }
        }

        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> r, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object r, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken ct = default) => System.Threading.Tasks.Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : INotification =>
            System.Threading.Tasks.Task.CompletedTask;
    }

    private sealed class InstanceStore(ITenantContext tenant) : IWorkflowInstanceRepository
    {
        public List<WorkflowInstance> Items { get; } = [];

        /// <summary>D2-FIX4 — the engine's instance store cannot be read for the object-reference lookup.</summary>
        public bool ThrowOnLatest { get; set; }

        /// <summary>D2-FIX4 — the engine's instance store cannot be read for the state read (the batched read).</summary>
        public bool ThrowOnList { get; set; }

        /// <summary>D2-FIX4 — how many object-reference lookups were made: whether a withdraw was ASKED is readable here.</summary>
        public int LatestReads { get; private set; }

        /// <summary>D2-FIX4 — runs once, right after the FIRST object-reference lookup answered: something starts in the engine
        /// between that read and the next one.</summary>
        public Action? AfterFirstLatest { get; set; }

        public Task<WorkflowInstance> CreateAsync(WorkflowInstance instance, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<WorkflowInstance?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            System.Threading.Tasks.Task.FromResult(Items.FirstOrDefault(x => x.Id == id && x.TenantId == tenant.TenantId));
        public Task<WorkflowInstance?> GetByIdempotencyKeyAsync(string key, CancellationToken ct = default) =>
            System.Threading.Tasks.Task.FromResult<WorkflowInstance?>(null);
        public Task<WorkflowInstance?> GetLatestByObjectRefAsync(string objectRef, string objectType, string objectId, CancellationToken ct = default)
        {
            LatestReads++;
            if (ThrowOnLatest)
            {
                throw new TimeoutException("The instance store could not be read.");
            }

            var latest = Items
                .Where(x => x.ObjectRef == objectRef && x.ObjectType == objectType && x.ObjectId == objectId && x.TenantId == tenant.TenantId)
                .OrderByDescending(x => x.StartedAt)
                .FirstOrDefault();
            if (LatestReads == 1)
            {
                AfterFirstLatest?.Invoke();
            }

            return System.Threading.Tasks.Task.FromResult(latest);
        }
        public Task<IReadOnlyList<WorkflowInstance>> GetAllForTenantAsync(CancellationToken ct = default) =>
            ThrowOnList
                ? throw new TimeoutException("The instance store could not be read.")
                : System.Threading.Tasks.Task.FromResult<IReadOnlyList<WorkflowInstance>>(Items.Where(x => x.TenantId == tenant.TenantId).ToList());
        public Task<bool> UpdateAsync(WorkflowInstance instance, int expectedVersion, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class ApprovalTaskStore(ITenantContext tenant) : IApprovalTaskRepository
    {
        public List<ApprovalTask> Items { get; } = [];

        private IEnumerable<ApprovalTask> Mine => Items.Where(x => x.TenantId == tenant.TenantId);

        public Task<ApprovalTask> CreateAsync(ApprovalTask task, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ApprovalTask?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            System.Threading.Tasks.Task.FromResult(Mine.FirstOrDefault(x => x.Id == id));
        public Task<ApprovalTask?> GetFirstByInstanceIdAsync(Guid instanceId, CancellationToken ct = default) =>
            System.Threading.Tasks.Task.FromResult(Mine.FirstOrDefault(x => x.WorkflowInstanceId == instanceId));
        public Task<ApprovalTask?> GetActiveByInstanceIdAsync(Guid instanceId, CancellationToken ct = default) =>
            System.Threading.Tasks.Task.FromResult(Mine.FirstOrDefault(x => x.WorkflowInstanceId == instanceId
                && x.Status is ApprovalTaskStatus.WaitingApproval or ApprovalTaskStatus.WaitingEvidence));
        public Task<IReadOnlyList<ApprovalTask>> ListByInstanceIdAsync(Guid instanceId, CancellationToken ct = default) =>
            System.Threading.Tasks.Task.FromResult<IReadOnlyList<ApprovalTask>>(Mine.Where(x => x.WorkflowInstanceId == instanceId).ToList());
        public Task<IReadOnlyList<ApprovalTask>> GetAllForTenantAsync(CancellationToken ct = default) =>
            System.Threading.Tasks.Task.FromResult<IReadOnlyList<ApprovalTask>>(Mine.ToList());
        public Task<bool> UpdateAsync(ApprovalTask task, int expectedVersion, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    /// <summary>
    /// D2-FIX5 — every template code resolves to ONE published template, so a start through the services under test reaches the
    /// engine's start command (answered by <see cref="Engine"/>) instead of installing or publishing anything. A withdraw never
    /// asks it; the tests that must not start assert the engine's <c>StartCalls</c> are empty.
    /// </summary>
    private sealed class PublishedTemplates : IWorkflowTemplateRepository
    {
        private static readonly WorkflowTemplate Published = new()
        {
            TenantId = TaskTestData.Tenant,
            TemplateCode = "published",
            Name = "Published",
            Status = WorkflowTemplateStatus.Published,
            ActivePublishedVersionId = Guid.NewGuid()
        };

        public Task<WorkflowTemplate> CreateAsync(WorkflowTemplate template, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<WorkflowTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<WorkflowTemplate?> GetByTemplateCodeAsync(string templateCode, CancellationToken ct = default) =>
            System.Threading.Tasks.Task.FromResult<WorkflowTemplate?>(Published);
        public Task<IReadOnlyList<WorkflowTemplate>> GetAllForTenantAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> UpdateAsync(WorkflowTemplate template, int expectedVersion, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
