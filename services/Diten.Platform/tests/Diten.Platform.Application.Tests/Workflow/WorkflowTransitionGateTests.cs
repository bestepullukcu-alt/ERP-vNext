using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Workflow.Queries;
using Diten.Platform.Application.Features.Workflow.Validators;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

public sealed class WorkflowTransitionGateTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private const string Correlation = "wf-gate-corr-001";
    private const string ObjectType = "PurchaseOrder";
    private const string ObjectId = "PO-1";
    private const string ObjectRef = "Purchasing|PurchaseOrder|PO-1";

    [Fact]
    public async Task No_workflow_returns_not_applicable_without_leaking()
    {
        var f = Fixture(TenantA);

        var response = await f.Handler.Handle(Query(), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(WorkflowTransitionGateDecision.NotApplicable, response.Data!.Decision);
        Assert.Equal(WorkflowTransitionGateStatus.NoWorkflow, response.Data.GateStatus);
        Assert.Equal(WorkflowReasonCodes.WorkflowNoInstance, response.Data.BlockingReasonCode);
        Assert.Null(response.Data.WorkflowInstanceId);
    }

    [Fact]
    public async Task Active_waiting_approval_blocks_with_active_task()
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedAsync(WorkflowInstanceStatus.Active, ApprovalTaskStatus.WaitingApproval);

        var response = await f.Handler.Handle(Query(), CancellationToken.None);

        Assert.Equal(WorkflowTransitionGateDecision.Blocked, response.Data!.Decision);
        Assert.Equal(WorkflowTransitionGateStatus.PendingApproval, response.Data.GateStatus);
        Assert.Equal(WorkflowReasonCodes.WorkflowPendingApproval, response.Data.BlockingReasonCode);
        Assert.Equal(runtime.Task!.Id, response.Data.ActiveTaskId);
    }

    [Fact]
    public async Task Active_waiting_evidence_blocks_with_waiting_evidence()
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedAsync(WorkflowInstanceStatus.Active, ApprovalTaskStatus.WaitingEvidence);

        var response = await f.Handler.Handle(Query(), CancellationToken.None);

        Assert.Equal(WorkflowTransitionGateDecision.Blocked, response.Data!.Decision);
        Assert.Equal(WorkflowTransitionGateStatus.WaitingEvidence, response.Data.GateStatus);
        Assert.Equal(WorkflowReasonCodes.WorkflowWaitingEvidence, response.Data.BlockingReasonCode);
        Assert.Equal(runtime.Task!.Id, response.Data.ActiveTaskId);
    }

    [Theory]
    [InlineData(WorkflowInstanceStatus.Completed)]
    [InlineData(WorkflowInstanceStatus.Approved)]
    public async Task Approved_terminal_status_allows_transition(WorkflowInstanceStatus status)
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedAsync(status);

        var response = await f.Handler.Handle(Query(), CancellationToken.None);

        Assert.Equal(WorkflowTransitionGateDecision.Allowed, response.Data!.Decision);
        Assert.Equal(WorkflowTransitionGateStatus.Approved, response.Data.GateStatus);
        Assert.Equal(WorkflowReasonCodes.WorkflowApproved, response.Data.BlockingReasonCode);
        Assert.Equal(runtime.Instance.Id, response.Data.WorkflowInstanceId);
    }

    [Fact]
    public async Task Rejected_workflow_blocks_transition()
    {
        var f = Fixture(TenantA);
        await f.SeedAsync(WorkflowInstanceStatus.Rejected);

        var response = await f.Handler.Handle(Query(), CancellationToken.None);

        Assert.Equal(WorkflowTransitionGateDecision.Blocked, response.Data!.Decision);
        Assert.Equal(WorkflowTransitionGateStatus.Rejected, response.Data.GateStatus);
        Assert.Equal(WorkflowReasonCodes.WorkflowRejected, response.Data.BlockingReasonCode);
    }

    [Fact]
    public async Task Cancelled_workflow_blocks_transition()
    {
        var f = Fixture(TenantA);
        await f.SeedAsync(WorkflowInstanceStatus.Cancelled);

        var response = await f.Handler.Handle(Query(), CancellationToken.None);

        Assert.Equal(WorkflowTransitionGateDecision.Blocked, response.Data!.Decision);
        Assert.Equal(WorkflowTransitionGateStatus.Cancelled, response.Data.GateStatus);
        Assert.Equal(WorkflowReasonCodes.WorkflowCancelled, response.Data.BlockingReasonCode);
    }

    [Theory]
    [InlineData(WorkflowInstanceStatus.TimedOut)]
    [InlineData(WorkflowInstanceStatus.Escalated)]
    [InlineData(WorkflowInstanceStatus.Pending)]
    public async Task Non_approved_non_terminal_status_blocks_safely(WorkflowInstanceStatus status)
    {
        var f = Fixture(TenantA);
        await f.SeedAsync(status);

        var response = await f.Handler.Handle(Query(), CancellationToken.None);

        Assert.Equal(WorkflowTransitionGateDecision.Blocked, response.Data!.Decision);
        Assert.Equal(WorkflowTransitionGateStatus.NotTerminalApproved, response.Data.GateStatus);
        Assert.Equal(WorkflowReasonCodes.WorkflowNotTerminalApproved, response.Data.BlockingReasonCode);
    }

    [Fact]
    public async Task Cross_tenant_object_lookup_returns_not_applicable()
    {
        var f = Fixture(TenantA);
        await f.SeedAsync(WorkflowInstanceStatus.Active, ApprovalTaskStatus.WaitingApproval);

        f.TenantContext.SetTenant(TenantB);
        var response = await f.Handler.Handle(Query(), CancellationToken.None);

        Assert.Equal(WorkflowTransitionGateDecision.NotApplicable, response.Data!.Decision);
        Assert.Equal(WorkflowReasonCodes.WorkflowNoInstance, response.Data.BlockingReasonCode);
        Assert.Null(response.Data.WorkflowInstanceId);
    }

    [Fact]
    public async Task Multiple_instances_for_object_use_latest_started_instance()
    {
        var f = Fixture(TenantA);
        await f.SeedAsync(WorkflowInstanceStatus.Completed, startedAt: DateTimeOffset.UtcNow.AddDays(-2));
        var latest = await f.SeedAsync(
            WorkflowInstanceStatus.Active,
            ApprovalTaskStatus.WaitingApproval,
            DateTimeOffset.UtcNow.AddMinutes(-1));

        var response = await f.Handler.Handle(Query(), CancellationToken.None);

        Assert.Equal(WorkflowTransitionGateDecision.Blocked, response.Data!.Decision);
        Assert.Equal(latest.Instance.Id, response.Data.WorkflowInstanceId);
        Assert.Equal(latest.Task!.Id, response.Data.ActiveTaskId);
    }

    [Fact]
    public async Task Correlation_id_is_returned_in_response_payload()
    {
        var f = Fixture(TenantA);
        await f.SeedAsync(WorkflowInstanceStatus.Completed);

        var response = await f.Handler.Handle(Query(), CancellationToken.None);

        Assert.Equal(Correlation, response.CorrelationId);
        Assert.Equal(Correlation, response.Data!.CorrelationId);
    }

    [Fact]
    public void Required_gate_fields_are_validated()
    {
        var validation = new EvaluateWorkflowTransitionGateValidator().Validate(
            Query(objectType: "", objectRef: ""));

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, x => x.PropertyName.EndsWith(nameof(EvaluateWorkflowTransitionGateRequest.ObjectType)));
        Assert.Contains(validation.Errors, x => x.PropertyName.EndsWith(nameof(EvaluateWorkflowTransitionGateRequest.ObjectRef)));
    }

    [Fact]
    public void Transition_gate_permission_constant_is_defined_without_seed_side_effect()
    {
        Assert.Equal("platform.workflow.transitions.evaluate", WorkflowPermissions.TransitionsEvaluate);
    }

    [Fact]
    public async Task Gate_evaluation_does_not_mutate_workflow_or_source_lifecycle_state()
    {
        var f = Fixture(TenantA);
        var source = new SourceObjectState("Draft");
        var runtime = await f.SeedAsync(WorkflowInstanceStatus.Active, ApprovalTaskStatus.WaitingApproval);
        var instanceVersion = runtime.Instance.Version;
        var taskVersion = runtime.Task!.Version;

        var response = await f.Handler.Handle(Query(), CancellationToken.None);

        Assert.Equal(WorkflowTransitionGateDecision.Blocked, response.Data!.Decision);
        Assert.Equal(WorkflowInstanceStatus.Active, runtime.Instance.Status);
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, runtime.Task.Status);
        Assert.Equal(instanceVersion, runtime.Instance.Version);
        Assert.Equal(taskVersion, runtime.Task.Version);
        Assert.Equal("Draft", source.LifecycleState);
    }

    private static EvaluateWorkflowTransitionGateQuery Query(
        string objectType = ObjectType,
        string objectId = ObjectId,
        string objectRef = ObjectRef) =>
        new(
            new EvaluateWorkflowTransitionGateRequest(
                objectType,
                objectId,
                objectRef,
                "submit",
                "Submitted",
                "actor-001",
                "SUBMIT"),
            Correlation);

    private static TestFixture Fixture(Guid tenantId)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId);
        var instances = new FakeWorkflowInstanceRepository(tenantContext);
        var tasks = new FakeApprovalTaskRepository(tenantContext);
        return new TestFixture(
            tenantContext,
            instances,
            tasks,
            new EvaluateWorkflowTransitionGateHandler(instances, tasks));
    }

    private sealed record RuntimeSeed(WorkflowInstance Instance, ApprovalTask? Task);
    private sealed record SourceObjectState(string LifecycleState);

    private sealed record TestFixture(
        TenantContext TenantContext,
        FakeWorkflowInstanceRepository Instances,
        FakeApprovalTaskRepository Tasks,
        EvaluateWorkflowTransitionGateHandler Handler)
    {
        public async Task<RuntimeSeed> SeedAsync(
            WorkflowInstanceStatus status,
            ApprovalTaskStatus? taskStatus = null,
            DateTimeOffset? startedAt = null)
        {
            var instance = await Instances.CreateAsync(new WorkflowInstance
            {
                TenantId = Guid.Empty,
                TemplateId = Guid.NewGuid(),
                WorkflowTemplateId = Guid.NewGuid(),
                TemplateVersionId = Guid.NewGuid(),
                ObjectType = ObjectType,
                ObjectId = ObjectId,
                ObjectRef = ObjectRef,
                Status = status,
                StartedAt = startedAt ?? DateTimeOffset.UtcNow,
                LastTransitionAt = startedAt ?? DateTimeOffset.UtcNow
            });

            ApprovalTask? task = null;
            if (taskStatus.HasValue)
            {
                task = await Tasks.CreateAsync(new ApprovalTask
                {
                    TenantId = Guid.Empty,
                    WorkflowInstanceId = instance.Id,
                    Status = taskStatus.Value,
                    AssigneeRef = "actor-001"
                });
            }

            return new RuntimeSeed(instance, task);
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

        public Task<bool> UpdateAsync(WorkflowInstance instance, int expectedVersion, CancellationToken ct = default) =>
            Task.FromResult(false);
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

        public Task<bool> UpdateAsync(ApprovalTask task, int expectedVersion, CancellationToken ct = default) =>
            Task.FromResult(false);
    }
}
