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

    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    [InlineData("requestInfo")]
    [InlineData("delegate")]
    public async Task Stale_expected_version_is_rejected_before_any_mutation(string action)
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeAsync();
        var originalStatus = runtime.Task.Status;
        var originalInstanceStatus = runtime.Instance.Status;
        var originalSnapshotCount = f.Snapshots.Items.Count;

        var response = action switch
        {
            "approve" => await f.Approve.Handle(
                Approve(runtime.Task.Id, expectedVersion: runtime.Task.Version + 1), CancellationToken.None),
            "reject" => await f.Reject.Handle(
                Reject(runtime.Task.Id, expectedVersion: runtime.Task.Version + 1), CancellationToken.None),
            "requestInfo" => await f.RequestInfo.Handle(
                RequestInfo(runtime.Task.Id, expectedVersion: runtime.Task.Version + 1), CancellationToken.None),
            "delegate" => await f.Delegate.Handle(
                Delegate(runtime.Task.Id, expectedVersion: runtime.Task.Version + 1), CancellationToken.None),
            _ => throw new InvalidOperationException(action)
        };

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowTransitionConflict, response.ReasonCode);
        Assert.Equal(originalStatus, runtime.Task.Status);
        Assert.Equal(originalInstanceStatus, runtime.Instance.Status);
        Assert.Equal(originalSnapshotCount, f.Snapshots.Items.Count);
        Assert.Single(f.Logs.Items);
    }

    [Fact]
    public async Task Exact_replay_is_returned_before_the_now_stale_version_is_checked()
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeAsync();
        var projectedVersion = runtime.Task.Version;

        var first = await f.Approve.Handle(
            Approve(runtime.Task.Id, idempotencyKey: "lost-response", expectedVersion: projectedVersion),
            CancellationToken.None);
        var replay = await f.Approve.Handle(
            Approve(runtime.Task.Id, idempotencyKey: "lost-response", expectedVersion: projectedVersion),
            CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.True(replay.IsSuccessful);
        Assert.True(replay.Data!.IsIdempotent);
        Assert.Equal(first.Data!.TransitionLogId, replay.Data.TransitionLogId);
        Assert.Equal(2, f.Logs.Items.Count);
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    [InlineData("requestInfo")]
    [InlineData("delegate")]
    public async Task Exact_replay_is_bound_to_the_actor_that_created_the_transition(string action)
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeAsync();
        const string key = "actor-bound-replay";

        var first = action switch
        {
            "approve" => await f.Approve.Handle(Approve(runtime.Task.Id, idempotencyKey: key), CancellationToken.None),
            "reject" => await f.Reject.Handle(Reject(runtime.Task.Id, idempotencyKey: key), CancellationToken.None),
            "requestInfo" => await f.RequestInfo.Handle(RequestInfo(runtime.Task.Id, idempotencyKey: key), CancellationToken.None),
            "delegate" => await f.Delegate.Handle(Delegate(runtime.Task.Id, idempotencyKey: key), CancellationToken.None),
            _ => throw new InvalidOperationException(action)
        };
        var differentActor = action switch
        {
            "approve" => await f.Approve.Handle(Approve(runtime.Task.Id, actorId: "different-actor", idempotencyKey: key), CancellationToken.None),
            "reject" => await f.Reject.Handle(Reject(runtime.Task.Id, actorId: "different-actor", idempotencyKey: key), CancellationToken.None),
            "requestInfo" => await f.RequestInfo.Handle(RequestInfo(runtime.Task.Id, actorId: "different-actor", idempotencyKey: key), CancellationToken.None),
            "delegate" => await f.Delegate.Handle(Delegate(runtime.Task.Id, actorId: "different-actor", idempotencyKey: key), CancellationToken.None),
            _ => throw new InvalidOperationException(action)
        };

        Assert.True(first.IsSuccessful);
        Assert.False(differentActor.IsSuccessful);
        Assert.Equal(403, differentActor.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowActorDenied, differentActor.ReasonCode);
        Assert.Single(f.Logs.Items, log => log.Action.ToString().Equals(action, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WorkCenter_idempotency_identity_is_compared_and_persisted_ordinal_exact()
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeAsync();
        const string exactKey = "  bounded-ordinal-key  ";

        var response = await f.Approve.Handle(
            Approve(runtime.Task.Id, idempotencyKey: exactKey, expectedVersion: runtime.Task.Version),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(exactKey, f.Logs.Items.Single(x => x.Action == WorkflowTransitionAction.Approve).IdempotencyKey);
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
    public async Task Concurrent_delegate_same_key_has_one_snapshot_business_effect_and_log()
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeAsync();
        f.Tasks.AtomicCloneMode = true;
        f.Instances.AtomicCloneMode = true;
        f.Snapshots.CoordinateNextEnsures(2);

        var first = f.Delegate.Handle(
            Delegate(runtime.Task.Id, idempotencyKey: "concurrent-delegate", expectedVersion: runtime.Task.Version),
            CancellationToken.None);
        var second = f.Delegate.Handle(
            Delegate(runtime.Task.Id, idempotencyKey: "concurrent-delegate", expectedVersion: runtime.Task.Version),
            CancellationToken.None);
        var results = await Task.WhenAll(first, second);

        Assert.Single(results, result => result.IsSuccessful);
        Assert.Single(results, result => !result.IsSuccessful && result.StatusCode == 409);
        Assert.Equal(2, f.Snapshots.Items.Count);
        Assert.Single(f.Logs.Items, log => log.Action == WorkflowTransitionAction.Delegate);
        var storedTask = Assert.Single(f.Tasks.Items);
        var delegatedSnapshot = Assert.Single(f.Snapshots.Items, snapshot => snapshot.Id == storedTask.AssignmentSnapshotId);
        Assert.Equal("delegate-001", storedTask.AssigneeRef);
        Assert.Equal("delegate-001", delegatedSnapshot.ResolvedPrincipalId);
    }

    [Fact]
    public async Task Concurrent_delegate_same_key_with_payload_drift_fails_closed_before_loser_cas()
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeAsync();
        f.Tasks.AtomicCloneMode = true;
        f.Instances.AtomicCloneMode = true;
        f.Snapshots.CoordinateNextEnsures(2);

        var first = f.Delegate.Handle(
            Delegate(runtime.Task.Id, delegatePrincipalId: "delegate-a", idempotencyKey: "drift-key", expectedVersion: runtime.Task.Version),
            CancellationToken.None);
        var second = f.Delegate.Handle(
            Delegate(runtime.Task.Id, delegatePrincipalId: "delegate-b", idempotencyKey: "drift-key", expectedVersion: runtime.Task.Version),
            CancellationToken.None);
        var results = await Task.WhenAll(first, second);

        Assert.Single(results, result => result.IsSuccessful);
        Assert.Single(results, result => !result.IsSuccessful &&
            result.StatusCode == 409 &&
            result.ReasonCode == WorkflowReasonCodes.WorkflowTransitionConflict);
        Assert.Equal(2, f.Snapshots.Items.Count);
        Assert.Single(f.Logs.Items, log => log.Action == WorkflowTransitionAction.Delegate);
        var storedTask = Assert.Single(f.Tasks.Items);
        var delegatedSnapshot = Assert.Single(f.Snapshots.Items, snapshot => snapshot.Id == storedTask.AssignmentSnapshotId);
        Assert.Equal(delegatedSnapshot.ResolvedPrincipalId, storedTask.AssigneeRef);
        Assert.Contains(storedTask.AssigneeRef, new[] { "delegate-a", "delegate-b" });
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
        string idempotencyKey = "approve-001",
        int? expectedVersion = null) =>
        new(taskId, new ApproveWorkflowTaskRequest(
            actorId, reasonCode, idempotencyKey, "ok", null, expectedVersion), Correlation);

    private static RejectWorkflowTaskCommand Reject(
        Guid taskId,
        string actorId = AssignedActor,
        string reasonCode = "REJECTED",
        string idempotencyKey = "reject-001",
        int? expectedVersion = null) =>
        new(taskId, new RejectWorkflowTaskRequest(
            actorId, reasonCode, idempotencyKey, "no", null, expectedVersion), Correlation);

    private static DelegateWorkflowTaskCommand Delegate(
        Guid taskId,
        string actorId = AssignedActor,
        string delegatePrincipalId = "delegate-001",
        string reasonCode = "DELEGATED",
        string idempotencyKey = "delegate-001",
        int? expectedVersion = null) =>
        new(taskId, new DelegateWorkflowTaskRequest(
            actorId, delegatePrincipalId, reasonCode, idempotencyKey, "handoff", expectedVersion), Correlation);

    private static RequestInfoWorkflowTaskCommand RequestInfo(
        Guid taskId,
        string actorId = AssignedActor,
        string reasonCode = "NEEDS_INFO",
        string idempotencyKey = "request-info-001",
        int? expectedVersion = null) =>
        new(taskId, new RequestInfoWorkflowTaskRequest(
            actorId, "submitter-001", reasonCode, idempotencyKey, "need docs", "evidence-ref", expectedVersion), Correlation);

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
        private readonly object _sync = new();
        public List<WorkflowInstance> Items { get; } = [];
        public bool AtomicCloneMode { get; set; }
        public FakeWorkflowInstanceRepository(ITenantContext tenantContext) => _tenantContext = tenantContext;
        public Task<WorkflowInstance> CreateAsync(WorkflowInstance instance, CancellationToken ct = default)
        {
            typeof(WorkflowInstance).GetProperty(nameof(WorkflowInstance.TenantId))!.SetValue(instance, _tenantContext.TenantId);
            lock (_sync) Items.Add(instance);
            return Task.FromResult(instance);
        }
        public Task<WorkflowInstance?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            lock (_sync)
            {
                var item = Items.FirstOrDefault(x => x.Id == id && x.TenantId == _tenantContext.TenantId && !x.IsDeleted);
                return Task.FromResult(item is null || !AtomicCloneMode ? item : Clone(item));
            }
        }
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
            lock (_sync)
            {
                var stored = Items.FirstOrDefault(x => x.Id == instance.Id && x.TenantId == _tenantContext.TenantId && x.Version == expectedVersion && !x.IsDeleted);
                if (stored is null) return Task.FromResult(false);
                instance.Version = expectedVersion + 1;
                Items[Items.IndexOf(stored)] = instance;
                return Task.FromResult(true);
            }
        }

        private static WorkflowInstance Clone(WorkflowInstance value) => new()
        {
            Id = value.Id,
            TenantId = value.TenantId,
            CreatedAt = value.CreatedAt,
            CreatedBy = value.CreatedBy,
            UpdatedAt = value.UpdatedAt,
            UpdatedBy = value.UpdatedBy,
            IsDeleted = value.IsDeleted,
            Version = value.Version,
            TemplateId = value.TemplateId,
            WorkflowTemplateId = value.WorkflowTemplateId,
            TemplateVersionId = value.TemplateVersionId,
            ObjectType = value.ObjectType,
            ObjectId = value.ObjectId,
            ObjectRef = value.ObjectRef,
            CurrentStage = value.CurrentStage,
            CurrentStep = value.CurrentStep,
            Status = value.Status,
            CorrelationId = value.CorrelationId,
            IdempotencyKey = value.IdempotencyKey,
            TrustedConsumerClientId = value.TrustedConsumerClientId,
            DelegatedMakerUserId = value.DelegatedMakerUserId,
            StartRequestFingerprint = value.StartRequestFingerprint,
            StartCheckpoint = value.StartCheckpoint,
            InitialApprovalTaskId = value.InitialApprovalTaskId,
            InitialAssignmentSnapshotId = value.InitialAssignmentSnapshotId,
            StartTransitionLogId = value.StartTransitionLogId,
            StartedBy = value.StartedBy,
            StartedAt = value.StartedAt,
            DueAt = value.DueAt,
            CompletedAt = value.CompletedAt,
            LastTransitionAt = value.LastTransitionAt,
            DeletedAt = value.DeletedAt
        };
    }

    private sealed class FakeApprovalTaskRepository : IApprovalTaskRepository
    {
        private readonly ITenantContext _tenantContext;
        private readonly object _sync = new();
        public List<ApprovalTask> Items { get; } = [];
        public bool AtomicCloneMode { get; set; }
        public FakeApprovalTaskRepository(ITenantContext tenantContext) => _tenantContext = tenantContext;
        public Task<ApprovalTask> CreateAsync(ApprovalTask task, CancellationToken ct = default)
        {
            typeof(ApprovalTask).GetProperty(nameof(ApprovalTask.TenantId))!.SetValue(task, _tenantContext.TenantId);
            lock (_sync) Items.Add(task);
            return Task.FromResult(task);
        }
        public Task<ApprovalTask?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            lock (_sync)
            {
                var item = Items.FirstOrDefault(x => x.Id == id && x.TenantId == _tenantContext.TenantId && !x.IsDeleted);
                return Task.FromResult(item is null || !AtomicCloneMode ? item : Clone(item));
            }
        }
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
            lock (_sync)
            {
                var stored = Items.FirstOrDefault(x => x.Id == task.Id && x.TenantId == _tenantContext.TenantId && x.Version == expectedVersion && !x.IsDeleted);
                if (stored is null) return Task.FromResult(false);
                task.Version = expectedVersion + 1;
                Items[Items.IndexOf(stored)] = task;
                return Task.FromResult(true);
            }
        }

        private static ApprovalTask Clone(ApprovalTask value) => new()
        {
            Id = value.Id,
            TenantId = value.TenantId,
            CreatedAt = value.CreatedAt,
            CreatedBy = value.CreatedBy,
            UpdatedAt = value.UpdatedAt,
            UpdatedBy = value.UpdatedBy,
            IsDeleted = value.IsDeleted,
            Version = value.Version,
            WorkflowInstanceId = value.WorkflowInstanceId,
            StageCode = value.StageCode,
            StepCode = value.StepCode,
            Status = value.Status,
            AssignmentSnapshotId = value.AssignmentSnapshotId,
            AssigneeRef = value.AssigneeRef,
            ReasonCode = value.ReasonCode,
            IdempotencyKey = value.IdempotencyKey,
            CommentRequired = value.CommentRequired,
            EvidenceRequired = value.EvidenceRequired,
            DueAt = value.DueAt,
            EscalatedAt = value.EscalatedAt,
            EscalationLevel = value.EscalationLevel,
            LastEscalationReasonCode = value.LastEscalationReasonCode,
            TimedOutAt = value.TimedOutAt,
            CompletedAt = value.CompletedAt,
            ActionedBy = value.ActionedBy,
            ActionReasonCode = value.ActionReasonCode,
            DeletedAt = value.DeletedAt
        };
    }

    private sealed class FakeRuntimeAssignmentSnapshotRepository : IRuntimeAssignmentSnapshotRepository
    {
        private readonly ITenantContext _tenantContext;
        private readonly object _sync = new();
        private TaskCompletionSource? _ensureGate;
        private int _expectedEnsures;
        private int _arrivedEnsures;
        public List<RuntimeAssignmentSnapshot> Items { get; } = [];
        public FakeRuntimeAssignmentSnapshotRepository(ITenantContext tenantContext) => _tenantContext = tenantContext;
        public Task<RuntimeAssignmentSnapshot> CreateAsync(RuntimeAssignmentSnapshot snapshot, CancellationToken ct = default)
        {
            typeof(RuntimeAssignmentSnapshot).GetProperty(nameof(RuntimeAssignmentSnapshot.TenantId))!.SetValue(snapshot, _tenantContext.TenantId);
            lock (_sync) Items.Add(snapshot);
            return Task.FromResult(snapshot);
        }
        public void CoordinateNextEnsures(int participants)
        {
            _expectedEnsures = participants;
            _arrivedEnsures = 0;
            _ensureGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        public async Task<RuntimeAssignmentSnapshot> EnsureTrustedStartSnapshotAsync(
            RuntimeAssignmentSnapshot snapshot,
            CancellationToken ct = default)
        {
            var gate = _ensureGate;
            if (gate is not null)
            {
                if (Interlocked.Increment(ref _arrivedEnsures) == _expectedEnsures)
                {
                    gate.TrySetResult();
                }
                await gate.Task.WaitAsync(ct);
            }

            lock (_sync)
            {
                var existing = Items.FirstOrDefault(x => x.Id == snapshot.Id && x.TenantId == _tenantContext.TenantId && !x.IsDeleted);
                if (existing is not null) return existing;
                typeof(RuntimeAssignmentSnapshot).GetProperty(nameof(RuntimeAssignmentSnapshot.TenantId))!.SetValue(snapshot, _tenantContext.TenantId);
                Items.Add(snapshot);
                return snapshot;
            }
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
