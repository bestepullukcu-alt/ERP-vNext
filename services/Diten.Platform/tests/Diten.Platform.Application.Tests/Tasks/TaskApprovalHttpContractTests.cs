using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Workflow.Queries;
using Diten.Platform.Application.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// The MOD-0024 → gate → MOD-0023 chain with NOTHING faked in the middle: the real
/// <see cref="WorkflowTransitionGate"/> and the real <see cref="EvaluateWorkflowTransitionGateHandler"/>.
///
/// <para>Why this exists: every previous test replaced the gate with a double, so it proved MOD-0024 reacts
/// correctly to a blocked verdict but never that the two modules produce one together. A live blocked `start`
/// returned HTTP 500 while all of those tests stayed green — the gap was exactly here, between the modules.</para>
///
/// <para>What the WIRE must carry: 409 (not 500, not 400), and a reason code the frontend bridge can translate.
/// The status and reason code are asserted on the actual <see cref="Response{T}"/> that the controller turns
/// verbatim into the HTTP response (<c>CreateActionResultInstance</c> copies <c>StatusCode</c>).</para>
/// </summary>
public sealed class TaskApprovalHttpContractTests
{
    private static readonly Guid Tenant = TaskTestData.Tenant;

    [Theory]
    // A running approval, a rejected one, a cancelled one, and a non-terminal instance: every state MOD-0023 can
    // block a task with must arrive as 409, never as a server error.
    [InlineData(WorkflowInstanceStatus.Active, WorkflowReasonCodes.WorkflowPendingApproval)]
    [InlineData(WorkflowInstanceStatus.Rejected, WorkflowReasonCodes.WorkflowRejected)]
    [InlineData(WorkflowInstanceStatus.Cancelled, WorkflowReasonCodes.WorkflowCancelled)]
    [InlineData(WorkflowInstanceStatus.Pending, WorkflowReasonCodes.WorkflowNotTerminalApproved)]
    [InlineData(WorkflowInstanceStatus.Escalated, WorkflowReasonCodes.WorkflowNotTerminalApproved)]
    [InlineData(WorkflowInstanceStatus.TimedOut, WorkflowReasonCodes.WorkflowNotTerminalApproved)]
    public async Task A_blocked_start_answers_409_with_the_reason_code_on_the_wire(
        WorkflowInstanceStatus instanceStatus, string expectedReasonCode)
    {
        var fixture = new Fixture(instanceStatus);

        var response = await fixture.StartAsync();

        Assert.Equal(409, response.StatusCode);
        Assert.Equal(expectedReasonCode, response.ReasonCode);
        Assert.False(response.IsSuccessful);
        // And the task did not move — the gate is consulted before the commit.
        Assert.Equal(TaskLifecycle.Open, fixture.Task.Lifecycle);
    }

    [Fact]
    public async Task A_blocked_complete_also_answers_409_rather_than_a_server_error()
    {
        var fixture = new Fixture(WorkflowInstanceStatus.Active, TaskLifecycle.InProgress);

        var response = await fixture.CompleteAsync();

        Assert.Equal(409, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowPendingApproval, response.ReasonCode);
    }

    [Fact]
    public async Task An_APPROVED_workflow_lets_the_start_through_the_same_chain()
    {
        // The positive case through the real gate: without it, "always blocked" would pass every test above.
        var fixture = new Fixture(WorkflowInstanceStatus.Approved);

        var response = await fixture.StartAsync();

        Assert.Equal(204, response.StatusCode);
        Assert.Equal(TaskLifecycle.InProgress, fixture.Task.Lifecycle);
    }

    /*
     * RESUMING out of Waiting goes through the SAME approval gate as a first start.
     *
     * This is the contract, not an observation. TransitionTaskItemHandler keys its gate on the TARGET lifecycle
     * (InProgress) and never on the source, so today a Waiting → InProgress transition is gated for free. That is
     * a property of one boolean expression: narrowing it to "only from Open/Planned" — a plausible optimisation,
     * since Waiting was an unreachable state until the resume action was projected — would silently let a task
     * whose approval is still outstanding be resumed straight into InProgress.
     *
     * The projection alone cannot protect this: it disables the button, and a caller can POST to /start anyway.
     */
    [Fact]
    public async Task A_task_waiting_on_information_still_cannot_RESUME_while_approval_is_outstanding()
    {
        var fixture = new Fixture(WorkflowInstanceStatus.Active, TaskLifecycle.Waiting);

        // The resume action projects the code "start", so this is the exact request the button sends.
        var response = await fixture.StartAsync();

        Assert.Equal(409, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowPendingApproval, response.ReasonCode);
        // And it did not move: the gate is consulted before the commit, from Waiting just as from Open.
        Assert.Equal(TaskLifecycle.Waiting, fixture.Task.Lifecycle);
    }

    [Fact]
    public async Task An_APPROVED_workflow_lets_a_waiting_task_resume()
    {
        // The positive half: without it, a gate that blocked everything would pass the test above.
        var fixture = new Fixture(WorkflowInstanceStatus.Approved, TaskLifecycle.Waiting);

        var response = await fixture.StartAsync();

        Assert.Equal(204, response.StatusCode);
        Assert.Equal(TaskLifecycle.InProgress, fixture.Task.Lifecycle);
    }

    [Fact]
    public async Task A_gate_that_THROWS_is_treated_as_blocked_not_as_a_server_error()
    {
        // Fail-closed must not mean "500". A workflow module that blows up is still a business refusal from the
        // caller's point of view, and the wire has to say so.
        var fixture = new Fixture(WorkflowInstanceStatus.Active, mediatorThrows: true);

        var response = await fixture.StartAsync();

        Assert.Equal(409, response.StatusCode);
        Assert.NotNull(response.ReasonCode);
        Assert.Equal(TaskLifecycle.Open, fixture.Task.Lifecycle);
    }

    // ── Fixture: the real chain ──────────────────────────────────────────────

    private sealed class Fixture
    {
        private readonly FakeTaskItemRepository _tasks;
        private readonly TransitionTaskItemHandler _handler;

        public TaskItem Task { get; }

        public Fixture(
            WorkflowInstanceStatus instanceStatus,
            TaskLifecycle lifecycle = TaskLifecycle.Open,
            bool mediatorThrows = false)
        {
            var tenantContext = new FakeTenantContext(Tenant);
            var instanceId = Guid.Parse("beeff00d-9999-9999-9999-999999999999");

            Task = new TaskItem
            {
                Id = Guid.Parse("835dc3ef-56be-437f-9a5e-7df1b1931324"),
                TenantId = Tenant,
                Title = "Approval-gated task",
                Lifecycle = lifecycle,
                AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
                AssigneeUserId = TaskTestData.Me,
                OrganizationUnitId = Guid.NewGuid(),
                ApprovalRequired = true,
                ApprovalManagerUserId = TaskTestData.Rival,
                WorkflowInstanceId = instanceId,
                Version = 2
            };
            _tasks = new FakeTaskItemRepository(Task);

            var instances = new WorkflowInstanceStore(tenantContext);
            var instance = new WorkflowInstance
            {
                Id = instanceId,
                TenantId = Tenant,
                TemplateId = Guid.NewGuid(),
                WorkflowTemplateId = Guid.NewGuid(),
                ObjectType = TaskApprovalService.ApprovalObjectType,
                ObjectId = Task.Id.ToString(),
                ObjectRef = TaskApprovalService.BuildObjectRef(Task.Id),
                Status = instanceStatus,
                StartedAt = DateTimeOffset.UtcNow
            };
            instances.Seed(instance, Tenant);

            var approvalTasks = new ApprovalTaskStore(tenantContext);
            if (instanceStatus == WorkflowInstanceStatus.Active)
            {
                approvalTasks.Seed(new ApprovalTask
                {
                    TenantId = Tenant,
                    WorkflowInstanceId = instanceId,
                    Status = ApprovalTaskStatus.WaitingApproval
                }, Tenant);
            }

            // The REAL evaluate handler, reached through the REAL gate, over a mediator that routes the query
            // exactly as the runtime pipeline does.
            var evaluateHandler = new EvaluateWorkflowTransitionGateHandler(instances, approvalTasks);
            IMediator mediator = mediatorThrows
                ? new ThrowingMediator()
                : new GateRoutingMediator(evaluateHandler);

            _handler = new TransitionTaskItemHandler(
                _tasks,
                new TaskLifecycleService(),
                new FakeCurrentUserContext(TaskTestData.Me),
                new FakeChecklistRunRepository(),
                new TaskChecklistService(),
                new WorkflowTransitionGate(mediator, NullLogger<WorkflowTransitionGate>.Instance),
                new FakeTaskDependencyRepository(), new FakeTaskNotificationService(),
                NullLogger<TransitionTaskItemHandler>.Instance);
        }

        public Task<Response<NoContent>> StartAsync() => Send(TaskLifecycle.InProgress);

        public Task<Response<NoContent>> CompleteAsync() => Send(TaskLifecycle.Done);

        private Task<Response<NoContent>> Send(TaskLifecycle target)
            => _handler.Handle(
                new TransitionTaskItemCommand(
                    Task.Id, target, new TaskTransitionRequest(Task.Version, null, null), "corr-http-contract"),
                CancellationToken.None);
    }

    /// <summary>Routes only the gate query, so an unexpected extra dependency shows up as a failure, not a silent no-op.</summary>
    private sealed class GateRoutingMediator : IMediator
    {
        private readonly EvaluateWorkflowTransitionGateHandler _handler;

        public GateRoutingMediator(EvaluateWorkflowTransitionGateHandler handler) => _handler = handler;

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            if (request is EvaluateWorkflowTransitionGateQuery query)
            {
                return (TResponse)(object)await _handler.Handle(query, ct);
            }

            throw new NotSupportedException($"Unexpected request in the gate chain: {request.GetType().Name}");
        }

        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> r, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object r, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken ct = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : INotification => Task.CompletedTask;
    }

    private sealed class ThrowingMediator : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
            => throw new InvalidOperationException("the workflow module is unavailable");

        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> r, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object r, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken ct = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : INotification => Task.CompletedTask;
    }

    // ── Minimal MOD-0023 stores (read paths only; MOD-0023's own files are untouched) ──

    private sealed class WorkflowInstanceStore : IWorkflowInstanceRepository
    {
        private readonly ITenantContext _tenant;
        private readonly List<WorkflowInstance> _items = [];

        public WorkflowInstanceStore(ITenantContext tenant) => _tenant = tenant;

        public void Seed(WorkflowInstance instance, Guid tenantId)
        {
            typeof(WorkflowInstance).GetProperty(nameof(WorkflowInstance.TenantId))!.SetValue(instance, tenantId);
            _items.Add(instance);
        }

        public Task<WorkflowInstance> CreateAsync(WorkflowInstance instance, CancellationToken ct = default)
            => throw new NotSupportedException("the gate never writes");

        public Task<WorkflowInstance?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_items.FirstOrDefault(x => x.Id == id && x.TenantId == _tenant.TenantId));

        public Task<WorkflowInstance?> GetByIdempotencyKeyAsync(string key, CancellationToken ct = default)
            => Task.FromResult<WorkflowInstance?>(null);

        public Task<WorkflowInstance?> GetLatestByObjectRefAsync(
            string objectRef, string objectType, string objectId, CancellationToken ct = default)
            => Task.FromResult(_items
                .Where(x => x.ObjectRef == objectRef && x.ObjectType == objectType && x.ObjectId == objectId
                            && x.TenantId == _tenant.TenantId && !x.IsDeleted)
                .OrderByDescending(x => x.StartedAt)
                .FirstOrDefault());

        public Task<IReadOnlyList<WorkflowInstance>> GetAllForTenantAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<WorkflowInstance>>(
                _items.Where(x => x.TenantId == _tenant.TenantId && !x.IsDeleted).ToList());

        public Task<bool> UpdateAsync(WorkflowInstance instance, int expectedVersion, CancellationToken ct = default)
            => throw new NotSupportedException("the gate never writes");
    }

    private sealed class ApprovalTaskStore : IApprovalTaskRepository
    {
        private readonly ITenantContext _tenant;
        private readonly List<ApprovalTask> _items = [];

        public ApprovalTaskStore(ITenantContext tenant) => _tenant = tenant;

        public void Seed(ApprovalTask task, Guid tenantId)
        {
            typeof(ApprovalTask).GetProperty(nameof(ApprovalTask.TenantId))!.SetValue(task, tenantId);
            _items.Add(task);
        }

        public Task<ApprovalTask> CreateAsync(ApprovalTask task, CancellationToken ct = default)
            => throw new NotSupportedException("the gate never writes");

        public Task<ApprovalTask?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_items.FirstOrDefault(x => x.Id == id && x.TenantId == _tenant.TenantId));

        public Task<ApprovalTask?> GetFirstByInstanceIdAsync(Guid instanceId, CancellationToken ct = default)
            => Task.FromResult(_items.FirstOrDefault(x => x.WorkflowInstanceId == instanceId && x.TenantId == _tenant.TenantId));

        public Task<ApprovalTask?> GetActiveByInstanceIdAsync(Guid instanceId, CancellationToken ct = default)
            => Task.FromResult(_items.FirstOrDefault(x =>
                x.WorkflowInstanceId == instanceId
                && x.TenantId == _tenant.TenantId
                && x.Status is ApprovalTaskStatus.WaitingApproval or ApprovalTaskStatus.WaitingEvidence));

        public Task<IReadOnlyList<ApprovalTask>> ListByInstanceIdAsync(Guid instanceId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ApprovalTask>>(
                _items.Where(x => x.WorkflowInstanceId == instanceId && x.TenantId == _tenant.TenantId).ToList());

        public Task<IReadOnlyList<ApprovalTask>> GetAllForTenantAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ApprovalTask>>(
                _items.Where(x => x.TenantId == _tenant.TenantId).ToList());

        public Task<bool> UpdateAsync(ApprovalTask task, int expectedVersion, CancellationToken ct = default)
            => throw new NotSupportedException("the gate never writes");
    }
}
