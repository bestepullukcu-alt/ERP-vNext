using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Workflow.Validators;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

public sealed class WorkflowTaskTransitionTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private const string Correlation = "wf-transition-corr-001";
    private const string AssignedActor = "approver-001";

    [Fact]
    public async Task Assigned_actor_approve_closes_task_and_completes_instance_with_log()
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeAsync();

        var response = await f.Approve.Handle(Approve(runtime.Task.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal("Approved", response.Data!.NewTaskStatus);
        Assert.Equal("Completed", response.Data.NewInstanceStatus);
        Assert.Equal(Correlation, response.Data.CorrelationId);
        Assert.Equal(2, f.Logs.Items.Single(x => x.Action == WorkflowTransitionAction.Approve).SequenceNo);
        Assert.Equal(ApprovalTaskStatus.Approved, runtime.Task.Status);
        Assert.Equal(WorkflowInstanceStatus.Completed, runtime.Instance.Status);
        Assert.Equal(AssignedActor, runtime.Task.ActionedBy);
        Assert.Equal("APPROVED", runtime.Task.ActionReasonCode);
    }

    [Fact]
    public async Task Duplicate_approve_same_idempotency_key_returns_idempotent_response_without_duplicate_log()
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeAsync();

        var first = await f.Approve.Handle(Approve(runtime.Task.Id, idempotencyKey: "idem-approve"), CancellationToken.None);
        var second = await f.Approve.Handle(Approve(runtime.Task.Id, idempotencyKey: "idem-approve"), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.True(second.IsSuccessful);
        Assert.False(first.Data!.IsIdempotent);
        Assert.True(second.Data!.IsIdempotent);
        Assert.Equal(first.Data.TransitionLogId, second.Data.TransitionLogId);
        Assert.Equal(2, f.Logs.Items.Count);
    }

    [Fact]
    public async Task Different_idempotency_key_on_closed_task_is_invalid_state_conflict()
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeAsync();

        await f.Approve.Handle(Approve(runtime.Task.Id, idempotencyKey: "idem-a"), CancellationToken.None);
        var second = await f.Approve.Handle(Approve(runtime.Task.Id, idempotencyKey: "idem-b"), CancellationToken.None);

        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowTaskInvalidState, second.ReasonCode);
        Assert.Equal(2, f.Logs.Items.Count);
    }

    [Fact]
    public async Task Assigned_actor_reject_closes_task_and_rejects_instance_with_log()
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeAsync();

        var response = await f.Reject.Handle(Reject(runtime.Task.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal("Rejected", response.Data!.NewTaskStatus);
        Assert.Equal("Rejected", response.Data.NewInstanceStatus);
        Assert.Equal(WorkflowTransitionAction.Reject, f.Logs.Items.Single(x => x.Action == WorkflowTransitionAction.Reject).Action);
        Assert.Equal(ApprovalTaskStatus.Rejected, runtime.Task.Status);
        Assert.Equal(WorkflowInstanceStatus.Rejected, runtime.Instance.Status);
    }

    [Fact]
    public async Task Duplicate_reject_same_idempotency_key_returns_idempotent_response_without_duplicate_log()
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeAsync();

        var first = await f.Reject.Handle(Reject(runtime.Task.Id, idempotencyKey: "idem-reject"), CancellationToken.None);
        var second = await f.Reject.Handle(Reject(runtime.Task.Id, idempotencyKey: "idem-reject"), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.True(second.IsSuccessful);
        Assert.True(second.Data!.IsIdempotent);
        Assert.Equal(2, f.Logs.Items.Count);
    }

    [Fact]
    public async Task Non_assigned_actor_approve_is_blocked()
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeAsync();

        var response = await f.Approve.Handle(Approve(runtime.Task.Id, actorId: "not-assigned"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(403, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowActorDenied, response.ReasonCode);
        Assert.Single(f.Logs.Items);
    }

    [Fact]
    public async Task Non_assigned_actor_reject_is_blocked()
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeAsync();

        var response = await f.Reject.Handle(Reject(runtime.Task.Id, actorId: "not-assigned"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(403, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowActorDenied, response.ReasonCode);
        Assert.Single(f.Logs.Items);
    }

    [Fact]
    public async Task Submitter_cannot_approve_own_workflow_sod_violation()
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeAsync(startedBy: AssignedActor);

        var response = await f.Approve.Handle(Approve(runtime.Task.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.SodViolation, response.ReasonCode);
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, runtime.Task.Status);
        Assert.Single(f.Logs.Items);
    }

    [Fact]
    public async Task Missing_task_returns_not_found_non_leakage()
    {
        var f = Fixture(TenantA);

        var response = await f.Approve.Handle(Approve(Guid.NewGuid()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.NotFoundNonLeakage, response.ReasonCode);
    }

    [Fact]
    public async Task Cross_tenant_task_transition_returns_not_found_non_leakage()
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeAsync();

        f.TenantContext.SetTenant(TenantB);
        var response = await f.Approve.Handle(Approve(runtime.Task.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.NotFoundNonLeakage, response.ReasonCode);
    }

    [Fact]
    public void Missing_reason_code_validation_failed()
    {
        var validation = new ApproveWorkflowTaskValidator().Validate(Approve(Guid.NewGuid(), reasonCode: ""));

        Assert.False(validation.IsValid);
    }

    [Fact]
    public void Missing_idempotency_key_validation_failed()
    {
        var validation = new RejectWorkflowTaskValidator().Validate(Reject(Guid.NewGuid(), idempotencyKey: ""));

        Assert.False(validation.IsValid);
    }

    [Theory]
    [InlineData(ApprovalTaskStatus.Approved)]
    [InlineData(ApprovalTaskStatus.Rejected)]
    [InlineData(ApprovalTaskStatus.Cancelled)]
    public async Task Closed_task_transition_is_blocked(ApprovalTaskStatus status)
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeAsync();
        runtime.Task.Status = status;

        var response = await f.Approve.Handle(Approve(runtime.Task.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowTaskInvalidState, response.ReasonCode);
        Assert.Single(f.Logs.Items);
    }

    [Fact]
    public async Task Missing_assignment_snapshot_is_blocked()
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeAsync();
        runtime.Task.AssignmentSnapshotId = Guid.NewGuid();

        var response = await f.Approve.Handle(Approve(runtime.Task.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowAssignmentSnapshotNotFound, response.ReasonCode);
    }

    [Fact]
    public async Task Missing_instance_is_blocked()
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeAsync();
        f.Instances.Items.Clear();

        var response = await f.Approve.Handle(Approve(runtime.Task.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.NotFoundNonLeakage, response.ReasonCode);
    }

    [Fact]
    public async Task Transition_log_is_append_only()
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeAsync();
        var startLog = Assert.Single(f.Logs.Items);

        await f.Approve.Handle(Approve(runtime.Task.Id), CancellationToken.None);

        Assert.Equal(2, f.Logs.Items.Count);
        Assert.Contains(f.Logs.Items, x => x.Id == startLog.Id && x.Action == WorkflowTransitionAction.Start);
        Assert.Contains(f.Logs.Items, x => x.Action == WorkflowTransitionAction.Approve);
    }

    [Fact]
    public async Task Sequence_number_continues_after_start_log()
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeAsync();

        await f.Reject.Handle(Reject(runtime.Task.Id), CancellationToken.None);

        Assert.Equal(2, f.Logs.Items.Single(x => x.Action == WorkflowTransitionAction.Reject).SequenceNo);
    }

    [Fact]
    public async Task Assigned_actor_delegate_creates_new_snapshot_and_keeps_instance_active()
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeAsync();
        var originalSnapshotId = runtime.Snapshot.Id;
        var originalResolvedPrincipal = runtime.Snapshot.ResolvedPrincipalId;

        var response = await f.Delegate.Handle(Delegate(runtime.Task.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal("Delegate", response.Data!.Action);
        Assert.Equal(WorkflowInstanceStatus.Active, runtime.Instance.Status);
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, runtime.Task.Status);
        Assert.Equal(2, f.Snapshots.Items.Count);
        Assert.Equal(originalResolvedPrincipal, f.Snapshots.Items.Single(x => x.Id == originalSnapshotId).ResolvedPrincipalId);
        Assert.NotEqual(originalSnapshotId, runtime.Task.AssignmentSnapshotId);
        Assert.Equal("delegate-001", f.Snapshots.Items.Single(x => x.Id == runtime.Task.AssignmentSnapshotId).ResolvedPrincipalId);
        Assert.Equal(2, f.Logs.Items.Single(x => x.Action == WorkflowTransitionAction.Delegate).SequenceNo);
    }

    [Fact]
    public async Task Duplicate_delegate_same_idempotency_key_does_not_duplicate_snapshot_or_log()
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeAsync();

        var first = await f.Delegate.Handle(Delegate(runtime.Task.Id, idempotencyKey: "delegate-idem"), CancellationToken.None);
        var second = await f.Delegate.Handle(Delegate(runtime.Task.Id, idempotencyKey: "delegate-idem"), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.True(second.IsSuccessful);
        Assert.True(second.Data!.IsIdempotent);
        Assert.Equal(2, f.Snapshots.Items.Count);
        Assert.Equal(2, f.Logs.Items.Count);
    }

    [Fact]
    public void Delegate_principal_required_validation_failed()
    {
        var validation = new DelegateWorkflowTaskValidator().Validate(Delegate(Guid.NewGuid(), delegatePrincipalId: ""));

        Assert.False(validation.IsValid);
    }

    [Fact]
    public async Task Delegate_same_actor_is_blocked()
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeAsync();

        var response = await f.Delegate.Handle(
            Delegate(runtime.Task.Id, delegatePrincipalId: AssignedActor),
            CancellationToken.None);
        var validation = new DelegateWorkflowTaskValidator().Validate(
            Delegate(runtime.Task.Id, delegatePrincipalId: AssignedActor));

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowDelegateSameActorInvalid, response.ReasonCode);
        Assert.False(validation.IsValid);
    }

    [Fact]
    public async Task Non_assigned_actor_delegate_is_blocked()
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeAsync();

        var response = await f.Delegate.Handle(Delegate(runtime.Task.Id, actorId: "not-assigned"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(403, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowActorDenied, response.ReasonCode);
        Assert.Single(f.Logs.Items);
    }

    [Fact]
    public async Task Cross_tenant_delegate_returns_not_found_non_leakage()
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeAsync();

        f.TenantContext.SetTenant(TenantB);
        var response = await f.Delegate.Handle(Delegate(runtime.Task.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.NotFoundNonLeakage, response.ReasonCode);
    }

    [Fact]
    public async Task Assigned_actor_request_info_moves_task_to_waiting_evidence_and_keeps_instance_active()
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeAsync();

        var response = await f.RequestInfo.Handle(RequestInfo(runtime.Task.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(ApprovalTaskStatus.WaitingEvidence, runtime.Task.Status);
        Assert.Equal(WorkflowInstanceStatus.Active, runtime.Instance.Status);
        Assert.Equal(WorkflowTransitionAction.RequestInfo, f.Logs.Items.Single(x => x.Action == WorkflowTransitionAction.RequestInfo).Action);
    }

    [Fact]
    public async Task Duplicate_request_info_same_idempotency_key_does_not_duplicate_log()
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeAsync();

        var first = await f.RequestInfo.Handle(RequestInfo(runtime.Task.Id, idempotencyKey: "ri-idem"), CancellationToken.None);
        var second = await f.RequestInfo.Handle(RequestInfo(runtime.Task.Id, idempotencyKey: "ri-idem"), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.True(second.IsSuccessful);
        Assert.True(second.Data!.IsIdempotent);
        Assert.Equal(2, f.Logs.Items.Count);
    }

    [Fact]
    public async Task Non_assigned_actor_request_info_is_blocked()
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeAsync();

        var response = await f.RequestInfo.Handle(RequestInfo(runtime.Task.Id, actorId: "not-assigned"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(403, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowActorDenied, response.ReasonCode);
    }

    [Fact]
    public async Task Cancel_closes_task_and_instance_without_assignment_requirement()
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeAsync();

        var response = await f.Cancel.Handle(Cancel(runtime.Task.Id, actorId: "workflow-admin"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(ApprovalTaskStatus.Cancelled, runtime.Task.Status);
        Assert.Equal(WorkflowInstanceStatus.Cancelled, runtime.Instance.Status);
        Assert.Equal(WorkflowTransitionAction.Cancel, f.Logs.Items.Single(x => x.Action == WorkflowTransitionAction.Cancel).Action);
    }

    [Fact]
    public async Task Duplicate_cancel_same_idempotency_key_does_not_duplicate_log()
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeAsync();

        var first = await f.Cancel.Handle(Cancel(runtime.Task.Id, idempotencyKey: "cancel-idem"), CancellationToken.None);
        var second = await f.Cancel.Handle(Cancel(runtime.Task.Id, idempotencyKey: "cancel-idem"), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.True(second.IsSuccessful);
        Assert.True(second.Data!.IsIdempotent);
        Assert.Equal(2, f.Logs.Items.Count);
    }

    [Fact]
    public async Task Cancel_terminal_task_is_invalid_state_conflict()
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeAsync();
        runtime.Task.Status = ApprovalTaskStatus.Approved;

        var response = await f.Cancel.Handle(Cancel(runtime.Task.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowTaskInvalidState, response.ReasonCode);
    }

    [Fact]
    public async Task Cancel_cross_tenant_task_returns_not_found_non_leakage()
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeAsync();

        f.TenantContext.SetTenant(TenantB);
        var response = await f.Cancel.Handle(Cancel(runtime.Task.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.NotFoundNonLeakage, response.ReasonCode);
    }

    [Fact]
    public void Batch05_missing_reason_and_idempotency_validation_failed()
    {
        Assert.False(new RequestInfoWorkflowTaskValidator().Validate(RequestInfo(Guid.NewGuid(), reasonCode: "")).IsValid);
        Assert.False(new CancelWorkflowTaskValidator().Validate(Cancel(Guid.NewGuid(), idempotencyKey: "")).IsValid);
    }

    private static ApproveWorkflowTaskCommand Approve(
        Guid taskId,
        string actorId = AssignedActor,
        string reasonCode = "APPROVED",
        string idempotencyKey = "approve-001") =>
        new(taskId, new ApproveWorkflowTaskRequest(actorId, reasonCode, idempotencyKey, "ok", null), Correlation);

    private static RejectWorkflowTaskCommand Reject(
        Guid taskId,
        string actorId = AssignedActor,
        string reasonCode = "REJECTED",
        string idempotencyKey = "reject-001") =>
        new(taskId, new RejectWorkflowTaskRequest(actorId, reasonCode, idempotencyKey, "no", null), Correlation);

    private static DelegateWorkflowTaskCommand Delegate(
        Guid taskId,
        string actorId = AssignedActor,
        string delegatePrincipalId = "delegate-001",
        string reasonCode = "DELEGATED",
        string idempotencyKey = "delegate-001") =>
        new(taskId, new DelegateWorkflowTaskRequest(actorId, delegatePrincipalId, reasonCode, idempotencyKey, "handoff"), Correlation);

    private static RequestInfoWorkflowTaskCommand RequestInfo(
        Guid taskId,
        string actorId = AssignedActor,
        string reasonCode = "NEEDS_INFO",
        string idempotencyKey = "request-info-001") =>
        new(taskId, new RequestInfoWorkflowTaskRequest(actorId, "submitter-001", reasonCode, idempotencyKey, "need docs", "evidence-ref"), Correlation);

    private static CancelWorkflowTaskCommand Cancel(
        Guid taskId,
        string actorId = "workflow-admin",
        string reasonCode = "CANCELLED",
        string idempotencyKey = "cancel-001") =>
        new(taskId, new CancelWorkflowTaskRequest(actorId, reasonCode, idempotencyKey, "stop"), Correlation);

    private static TestFixture Fixture(Guid tenantId)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId);
        var tasks = new FakeApprovalTaskRepository(tenantContext);
        var instances = new FakeWorkflowInstanceRepository(tenantContext);
        var snapshots = new FakeRuntimeAssignmentSnapshotRepository(tenantContext);
        var logs = new FakeWorkflowTransitionLogRepository(tenantContext);
        return new TestFixture(
            tenantContext,
            tasks,
            instances,
            snapshots,
            logs,
            new ApproveWorkflowTaskHandler(tasks, instances, snapshots, logs),
            new RejectWorkflowTaskHandler(tasks, instances, snapshots, logs),
            new DelegateWorkflowTaskHandler(tasks, instances, snapshots, logs),
            new RequestInfoWorkflowTaskHandler(tasks, instances, snapshots, logs),
            new CancelWorkflowTaskHandler(tasks, instances, snapshots, logs));
    }

    private sealed record RuntimeSeed(WorkflowInstance Instance, ApprovalTask Task, RuntimeAssignmentSnapshot Snapshot);

    private sealed record TestFixture(
        TenantContext TenantContext,
        FakeApprovalTaskRepository Tasks,
        FakeWorkflowInstanceRepository Instances,
        FakeRuntimeAssignmentSnapshotRepository Snapshots,
        FakeWorkflowTransitionLogRepository Logs,
        ApproveWorkflowTaskHandler Approve,
        RejectWorkflowTaskHandler Reject,
        DelegateWorkflowTaskHandler Delegate,
        RequestInfoWorkflowTaskHandler RequestInfo,
        CancelWorkflowTaskHandler Cancel)
    {
        public async Task<RuntimeSeed> SeedRuntimeAsync(string startedBy = "submitter-001")
        {
            var instance = await Instances.CreateAsync(new WorkflowInstance
            {
                TenantId = TenantContext.TenantId,
                TemplateId = Guid.NewGuid(),
                WorkflowTemplateId = Guid.NewGuid(),
                TemplateVersionId = Guid.NewGuid(),
                ObjectType = "PurchaseOrder",
                ObjectId = "PO-1",
                ObjectRef = "Purchasing|PurchaseOrder|PO-1",
                CurrentStage = "stage-1",
                CurrentStep = "step-1",
                Status = WorkflowInstanceStatus.Active,
                StartedBy = startedBy,
                StartedAt = DateTimeOffset.UtcNow,
                LastTransitionAt = DateTimeOffset.UtcNow
            });
            var task = await Tasks.CreateAsync(new ApprovalTask
            {
                TenantId = TenantContext.TenantId,
                WorkflowInstanceId = instance.Id,
                StageCode = "stage-1",
                StepCode = "step-1",
                Status = ApprovalTaskStatus.WaitingApproval,
                AssigneeRef = AssignedActor
            });
            var snapshot = await Snapshots.CreateAsync(new RuntimeAssignmentSnapshot
            {
                TenantId = TenantContext.TenantId,
                WorkflowInstanceId = instance.Id,
                ApprovalTaskId = task.Id,
                ResolverSource = "test",
                ResolvedPrincipalId = AssignedActor,
                CandidatePrincipalIds = [AssignedActor],
                ResolvedAt = DateTime.UtcNow,
                TieBreakExplanation = "single_candidate"
            });
            task.AssignmentSnapshotId = snapshot.Id;
            await Logs.CreateAsync(new WorkflowTransitionLog
            {
                TenantId = TenantContext.TenantId,
                WorkflowInstanceId = instance.Id,
                ApprovalTaskId = task.Id,
                Action = WorkflowTransitionAction.Start,
                ToState = "WaitingApproval",
                ToStatus = "Active",
                SequenceNo = 1,
                CorrelationId = Correlation
            });
            return new RuntimeSeed(instance, task, snapshot);
        }
    }

    private sealed class FakeWorkflowInstanceRepository : IWorkflowInstanceRepository
    {
        private readonly ITenantContext _tenantContext;
        public List<WorkflowInstance> Items { get; } = [];
        public FakeWorkflowInstanceRepository(ITenantContext tenantContext) => _tenantContext = tenantContext;
        public Task<WorkflowInstance> CreateAsync(WorkflowInstance instance, CancellationToken ct = default)
        {
            typeof(WorkflowInstance).GetProperty(nameof(WorkflowInstance.TenantId))!.SetValue(instance, _tenantContext.TenantId);
            Items.Add(instance);
            return Task.FromResult(instance);
        }
        public Task<WorkflowInstance?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.Id == id && x.TenantId == _tenantContext.TenantId && !x.IsDeleted));
        public Task<WorkflowInstance?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.IdempotencyKey == idempotencyKey && x.TenantId == _tenantContext.TenantId && !x.IsDeleted));

        public Task<WorkflowInstance?> GetLatestByObjectRefAsync(
            string objectRef,
            string objectType,
            string objectId,
            CancellationToken ct = default) =>
            Task.FromResult(Items
                .Where(x =>
                    x.ObjectRef == objectRef &&
                    x.ObjectType == objectType &&
                    x.ObjectId == objectId &&
                    x.TenantId == _tenantContext.TenantId &&
                    !x.IsDeleted)
                .OrderByDescending(x => x.StartedAt)
                .ThenByDescending(x => x.CreatedAt)
                .FirstOrDefault());

        public Task<IReadOnlyList<WorkflowInstance>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<WorkflowInstance>>(Items.Where(x => x.TenantId == _tenantContext.TenantId && !x.IsDeleted).ToList());
        public Task<bool> UpdateAsync(WorkflowInstance instance, int expectedVersion, CancellationToken ct = default)
        {
            var stored = Items.FirstOrDefault(x => x.Id == instance.Id && x.TenantId == _tenantContext.TenantId && x.Version == expectedVersion && !x.IsDeleted);
            if (stored is null) return Task.FromResult(false);
            instance.Version = expectedVersion + 1;
            Items[Items.IndexOf(stored)] = instance;
            return Task.FromResult(true);
        }
    }

    private sealed class FakeApprovalTaskRepository : IApprovalTaskRepository
    {
        private readonly ITenantContext _tenantContext;
        public List<ApprovalTask> Items { get; } = [];
        public FakeApprovalTaskRepository(ITenantContext tenantContext) => _tenantContext = tenantContext;
        public Task<ApprovalTask> CreateAsync(ApprovalTask task, CancellationToken ct = default)
        {
            typeof(ApprovalTask).GetProperty(nameof(ApprovalTask.TenantId))!.SetValue(task, _tenantContext.TenantId);
            Items.Add(task);
            return Task.FromResult(task);
        }
        public Task<ApprovalTask?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.Id == id && x.TenantId == _tenantContext.TenantId && !x.IsDeleted));
        public Task<ApprovalTask?> GetFirstByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.WorkflowInstanceId == workflowInstanceId && x.TenantId == _tenantContext.TenantId && !x.IsDeleted));

        public Task<ApprovalTask?> GetActiveByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            Task.FromResult(Items
                .Where(x =>
                    x.WorkflowInstanceId == workflowInstanceId &&
                    x.TenantId == _tenantContext.TenantId &&
                    !x.IsDeleted &&
                    x.Status is ApprovalTaskStatus.WaitingApproval or ApprovalTaskStatus.WaitingEvidence)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault());

        public Task<IReadOnlyList<ApprovalTask>> ListByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ApprovalTask>>(Items.Where(x => x.WorkflowInstanceId == workflowInstanceId && x.TenantId == _tenantContext.TenantId && !x.IsDeleted).ToList());
        public Task<IReadOnlyList<ApprovalTask>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ApprovalTask>>(Items.Where(x => x.TenantId == _tenantContext.TenantId && !x.IsDeleted).ToList());
        public Task<bool> UpdateAsync(ApprovalTask task, int expectedVersion, CancellationToken ct = default)
        {
            var stored = Items.FirstOrDefault(x => x.Id == task.Id && x.TenantId == _tenantContext.TenantId && x.Version == expectedVersion && !x.IsDeleted);
            if (stored is null) return Task.FromResult(false);
            task.Version = expectedVersion + 1;
            Items[Items.IndexOf(stored)] = task;
            return Task.FromResult(true);
        }
    }

    private sealed class FakeRuntimeAssignmentSnapshotRepository : IRuntimeAssignmentSnapshotRepository
    {
        private readonly ITenantContext _tenantContext;
        public List<RuntimeAssignmentSnapshot> Items { get; } = [];
        public FakeRuntimeAssignmentSnapshotRepository(ITenantContext tenantContext) => _tenantContext = tenantContext;
        public Task<RuntimeAssignmentSnapshot> CreateAsync(RuntimeAssignmentSnapshot snapshot, CancellationToken ct = default)
        {
            typeof(RuntimeAssignmentSnapshot).GetProperty(nameof(RuntimeAssignmentSnapshot.TenantId))!.SetValue(snapshot, _tenantContext.TenantId);
            Items.Add(snapshot);
            return Task.FromResult(snapshot);
        }
        public Task<RuntimeAssignmentSnapshot?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.Id == id && x.TenantId == _tenantContext.TenantId && !x.IsDeleted));
        public Task<IReadOnlyList<RuntimeAssignmentSnapshot>> ListByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RuntimeAssignmentSnapshot>>(Items.Where(x => x.WorkflowInstanceId == workflowInstanceId && x.TenantId == _tenantContext.TenantId && !x.IsDeleted).ToList());
    }

    private sealed class FakeWorkflowTransitionLogRepository : IWorkflowTransitionLogRepository
    {
        private readonly ITenantContext _tenantContext;
        public List<WorkflowTransitionLog> Items { get; } = [];
        public FakeWorkflowTransitionLogRepository(ITenantContext tenantContext) => _tenantContext = tenantContext;
        public Task<WorkflowTransitionLog> CreateAsync(WorkflowTransitionLog log, CancellationToken ct = default)
        {
            typeof(WorkflowTransitionLog).GetProperty(nameof(WorkflowTransitionLog.TenantId))!.SetValue(log, _tenantContext.TenantId);
            Items.Add(log);
            return Task.FromResult(log);
        }
        public Task<WorkflowTransitionLog?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.Id == id && x.TenantId == _tenantContext.TenantId && !x.IsDeleted));
        public Task<WorkflowTransitionLog?> GetByTaskActionIdempotencyKeyAsync(Guid approvalTaskId, WorkflowTransitionAction action, string idempotencyKey, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.ApprovalTaskId == approvalTaskId && x.Action == action && x.IdempotencyKey == idempotencyKey && x.TenantId == _tenantContext.TenantId && !x.IsDeleted));
        public Task<long> GetLatestSequenceNoAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            Task.FromResult(Items.Where(x => x.WorkflowInstanceId == workflowInstanceId && x.TenantId == _tenantContext.TenantId && !x.IsDeleted).Select(x => x.SequenceNo).DefaultIfEmpty(0).Max());
        public Task<IReadOnlyList<WorkflowTransitionLog>> ListByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<WorkflowTransitionLog>>(Items.Where(x => x.WorkflowInstanceId == workflowInstanceId && x.TenantId == _tenantContext.TenantId && !x.IsDeleted).ToList());
    }
}
