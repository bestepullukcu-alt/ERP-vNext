using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Workflow.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Workflow.Queries;
using Diten.Platform.Application.Features.Workflow.Services;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

public sealed class TrustedWorkflowTerminalDecisionEvidenceMongoTests : IAsyncLifetime
{
    private MongoIntegrationHarness _harness = null!;
    private IWorkflowTemplateRepository _templates = null!;
    private IWorkflowTemplateVersionRepository _versions = null!;
    private IWorkflowInstanceRepository _instances = null!;
    private IApprovalTaskRepository _tasks = null!;
    private IRuntimeAssignmentSnapshotRepository _snapshots = null!;
    private IWorkflowTransitionLogRepository _logs = null!;
    private Guid _templateId;

    public async Task InitializeAsync()
    {
        _harness = await MongoIntegrationHarness.CreateAsync(SchemaProfile.WorkflowWorkCenter);
        _templates = new WorkflowTemplateRepository(_harness.DbContext, _harness.TenantContext);
        _versions = new WorkflowTemplateVersionRepository(_harness.DbContext, _harness.TenantContext);
        _instances = new WorkflowInstanceRepository(_harness.DbContext, _harness.TenantContext);
        _tasks = new ApprovalTaskRepository(_harness.DbContext, _harness.TenantContext);
        _snapshots = new RuntimeAssignmentSnapshotRepository(_harness.DbContext, _harness.TenantContext);
        _logs = new WorkflowTransitionLogRepository(_harness.DbContext, _harness.TenantContext);

        var template = await _templates.CreateAsync(new WorkflowTemplate
        {
            TenantId = _harness.TenantId,
            TemplateCode = $"EVIDENCE-{_harness.TenantId:N}",
            Name = "Terminal evidence",
            Status = WorkflowTemplateStatus.Published
        });
        var version = await _versions.CreateAsync(new WorkflowTemplateVersion
        {
            TenantId = _harness.TenantId,
            TemplateId = template.Id,
            VersionNumber = 1,
            DefinitionJson = "{}",
            SchemaVersion = "1.0",
            ExpressionVersion = "1.0",
            Status = WorkflowTemplateVersionStatus.Published,
            IsImmutable = true
        });
        template.ActivePublishedVersionId = version.Id;
        template.CurrentVersionId = version.Id;
        Assert.True(await _templates.UpdateAsync(template, template.Version));
        _templateId = template.Id;
    }

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Theory]
    [InlineData(WorkflowTransitionAction.Approve)]
    [InlineData(WorkflowTransitionAction.Reject)]
    public async Task Coherent_terminal_decision_returns_sanitized_evidence(WorkflowTransitionAction action)
    {
        var started = await StartAsync($"evidence-{action}");
        await MakeTerminalAsync(started, action, ActorId);
        var handler = new GetTrustedWorkflowTerminalDecisionEvidenceHandler(_instances, _tasks, _logs);

        var response = await handler.Handle(
            new GetTrustedWorkflowTerminalDecisionEvidenceQuery(
                started.WorkflowInstanceId,
                ClientId,
                "GlobalProduct",
                "GP-EVIDENCE",
                "corr-evidence"),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(action.ToString(), response.Data!.TerminalAction);
        Assert.Equal(ActorId, response.Data.ActorUserId);
        Assert.Equal(2, response.Data.TransitionSequence);
        Assert.Equal(started.WorkflowInstanceId, response.Data.WorkflowInstanceId);
        Assert.DoesNotContain("fingerprint", response.Data.ToString()!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(WorkflowTransitionAction.Approve)]
    [InlineData(WorkflowTransitionAction.Reject)]
    public async Task Native_transition_support_produces_terminal_evidence(WorkflowTransitionAction action)
    {
        var started = await StartAsync($"native-evidence-{action}");
        var request = action == WorkflowTransitionAction.Approve
            ? await new ApproveWorkflowTaskHandler(_tasks, _instances, _snapshots, _logs, _versions)
                .Handle(
                    new ApproveWorkflowTaskCommand(
                        started.ApprovalTaskId,
                        new ApproveWorkflowTaskRequest(ActorId, "APPROVED", "native-approve", null, null),
                        "corr-native-approve"),
                    CancellationToken.None)
            : await new RejectWorkflowTaskHandler(_tasks, _instances, _snapshots, _logs)
                .Handle(
                    new RejectWorkflowTaskCommand(
                        started.ApprovalTaskId,
                        new RejectWorkflowTaskRequest(ActorId, "REJECTED", "native-reject", null, null),
                        "corr-native-reject"),
                    CancellationToken.None);
        Assert.True(request.IsSuccessful);

        var response = await EvidenceAsync(started.WorkflowInstanceId, "corr-native-evidence");

        Assert.True(response.IsSuccessful);
        Assert.Equal(action.ToString(), response.Data!.TerminalAction);
        Assert.Equal(
            action == WorkflowTransitionAction.Approve
                ? WorkflowInstanceStatus.Completed.ToString()
                : WorkflowInstanceStatus.Rejected.ToString(),
            response.Data.InstanceStatus);
        Assert.Equal(ActorId, response.Data.ActorUserId);
    }

    [Fact]
    public async Task Coherent_active_workflow_returns_distinct_retryable_not_terminal()
    {
        var started = await StartAsync("evidence-not-terminal");

        var response = await EvidenceAsync(started.WorkflowInstanceId, "corr-not-terminal");

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowDecisionNotTerminal, response.ReasonCode);
    }

    [Theory]
    [InlineData(WorkflowTransitionAction.Approve)]
    [InlineData(WorkflowTransitionAction.Reject)]
    public async Task Multi_step_history_uses_final_monotonic_decision_log(WorkflowTransitionAction finalAction)
    {
        var started = await StartAsync($"evidence-multi-{finalAction}");
        var finalTask = await MakeMultiStepTerminalAsync(started, finalAction, false);

        var response = await EvidenceAsync(started.WorkflowInstanceId, "corr-multi");

        Assert.True(response.IsSuccessful);
        Assert.Equal(finalAction.ToString(), response.Data!.TerminalAction);
        Assert.Equal(3, response.Data.TransitionSequence);
        Assert.Equal(finalTask.Id, response.Data.ApprovalTaskId);
    }

    [Fact]
    public async Task Contradictory_prior_reject_before_final_approve_fails_closed()
    {
        var started = await StartAsync("evidence-contradictory-history");
        await MakeMultiStepTerminalAsync(started, WorkflowTransitionAction.Approve, true);

        var response = await EvidenceAsync(started.WorkflowInstanceId, "corr-contradictory-history");

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowTerminalEvidenceInconsistent, response.ReasonCode);
    }

    [Fact]
    public async Task Same_tenant_wrong_service_client_is_non_leaking_not_found()
    {
        var started = await StartAsync("evidence-wrong-client");
        await MakeTerminalAsync(started, WorkflowTransitionAction.Approve, ActorId);

        var response = await EvidenceAsync(
            started.WorkflowInstanceId,
            "corr-wrong-client",
            Guid.NewGuid());

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.NotFoundNonLeakage, response.ReasonCode);
    }

    [Fact]
    public async Task Generic_nontrusted_instance_is_non_leaking_not_found()
    {
        var started = await StartAsync("evidence-generic-instance");
        await MakeTerminalAsync(started, WorkflowTransitionAction.Approve, ActorId);
        var instance = await _instances.GetByIdAsync(started.WorkflowInstanceId);
        Assert.NotNull(instance);
        var version = instance!.Version;
        instance.TrustedConsumerClientId = null;
        instance.DelegatedMakerUserId = null;
        instance.StartCheckpoint = WorkflowStartCheckpoint.None;
        Assert.True(await _instances.UpdateAsync(instance, version));

        var response = await EvidenceAsync(started.WorkflowInstanceId, "corr-generic-instance");

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.NotFoundNonLeakage, response.ReasonCode);
    }

    [Fact]
    public async Task Incomplete_trusted_start_identity_is_non_leaking_not_found()
    {
        var started = await StartAsync("evidence-incomplete-start");
        await MakeTerminalAsync(started, WorkflowTransitionAction.Approve, ActorId);
        var instance = await _instances.GetByIdAsync(started.WorkflowInstanceId);
        Assert.NotNull(instance);
        var version = instance!.Version;
        instance.StartCheckpoint = WorkflowStartCheckpoint.StartLogPersisted;
        Assert.True(await _instances.UpdateAsync(instance, version));

        var response = await EvidenceAsync(started.WorkflowInstanceId, "corr-incomplete-start");

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.NotFoundNonLeakage, response.ReasonCode);
    }

    [Fact]
    public async Task Transition_sequence_gap_fails_closed()
    {
        var started = await StartAsync("evidence-sequence-gap");
        await MakeTerminalAsync(started, WorkflowTransitionAction.Approve, ActorId, sequenceNo: 3);

        var response = await EvidenceAsync(started.WorkflowInstanceId, "corr-sequence-gap");

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowTerminalEvidenceInconsistent, response.ReasonCode);
    }

    [Fact]
    public async Task Cross_tenant_evidence_read_is_non_leaking_not_found()
    {
        var started = await StartAsync("evidence-cross-tenant");
        await MakeTerminalAsync(started, WorkflowTransitionAction.Approve, ActorId);
        _harness.TenantContext.SetTenant(Guid.NewGuid());
        var handler = new GetTrustedWorkflowTerminalDecisionEvidenceHandler(_instances, _tasks, _logs);

        var response = await handler.Handle(
            new GetTrustedWorkflowTerminalDecisionEvidenceQuery(
                started.WorkflowInstanceId,
                ClientId,
                "GlobalProduct",
                "GP-EVIDENCE",
                "corr-cross"),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.NotFoundNonLeakage, response.ReasonCode);
    }

    [Fact]
    public async Task Contradictory_terminal_actor_fails_closed()
    {
        var started = await StartAsync("evidence-conflict");
        await MakeTerminalAsync(started, WorkflowTransitionAction.Approve, "different-log-actor", ActorId);
        var handler = new GetTrustedWorkflowTerminalDecisionEvidenceHandler(_instances, _tasks, _logs);

        var response = await handler.Handle(
            new GetTrustedWorkflowTerminalDecisionEvidenceQuery(
                started.WorkflowInstanceId,
                ClientId,
                "GlobalProduct",
                "GP-EVIDENCE",
                "corr-conflict"),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowTerminalEvidenceInconsistent, response.ReasonCode);
    }

    [Theory]
    [InlineData("globalproduct", "GP-EVIDENCE")]
    [InlineData("GlobalProduct", "gp-evidence")]
    [InlineData("FinishedGood", "FG-OTHER")]
    public async Task Expected_object_mismatch_is_non_leaking_not_found(
        string expectedObjectType,
        string expectedObjectId)
    {
        var started = await StartAsync($"evidence-object-mismatch-{expectedObjectType}-{expectedObjectId}");
        await MakeTerminalAsync(started, WorkflowTransitionAction.Approve, ActorId);
        var handler = new GetTrustedWorkflowTerminalDecisionEvidenceHandler(_instances, _tasks, _logs);

        var response = await handler.Handle(
            new GetTrustedWorkflowTerminalDecisionEvidenceQuery(
                started.WorkflowInstanceId,
                ClientId,
                expectedObjectType,
                expectedObjectId,
                "corr-object-mismatch"),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.NotFoundNonLeakage, response.ReasonCode);
    }

    private async Task<TrustedWorkflowStartResult> StartAsync(string key)
    {
        var coordinator = new WorkflowInstanceStartCoordinator(
            _templates,
            _versions,
            _instances,
            _tasks,
            _snapshots,
            _logs,
            _harness.TenantContext);
        var response = await coordinator.StartAsync(
            new TrustedWorkflowStartRequest(
                _templateId,
                null,
                "GlobalProduct",
                "GP-EVIDENCE",
                null,
                [ActorId],
                "SUBMIT",
                key,
                true,
                false,
                DateTimeOffset.UtcNow.AddHours(1)),
            ClientId,
            MakerId,
            "corr-start");
        Assert.True(response.IsSuccessful);
        return response.Data!;
    }

    private Task<Response<TrustedWorkflowTerminalDecisionEvidence>> EvidenceAsync(
        Guid workflowInstanceId,
        string correlationId,
        Guid? clientId = null) =>
        new GetTrustedWorkflowTerminalDecisionEvidenceHandler(_instances, _tasks, _logs).Handle(
            new GetTrustedWorkflowTerminalDecisionEvidenceQuery(
                workflowInstanceId,
                clientId ?? ClientId,
                "GlobalProduct",
                "GP-EVIDENCE",
                correlationId),
            CancellationToken.None);

    private async Task<ApprovalTask> MakeMultiStepTerminalAsync(
        TrustedWorkflowStartResult started,
        WorkflowTransitionAction finalAction,
        bool contradictoryPriorReject)
    {
        var priorTask = await _tasks.GetByIdAsync(started.ApprovalTaskId);
        Assert.NotNull(priorTask);
        var priorVersion = priorTask!.Version;
        priorTask.Status = contradictoryPriorReject
            ? ApprovalTaskStatus.Rejected
            : ApprovalTaskStatus.Approved;
        priorTask.ActionedBy = ActorId;
        priorTask.ActionReasonCode = contradictoryPriorReject ? "REJECTED" : "APPROVED";
        priorTask.CompletedAt = DateTimeOffset.UtcNow;
        Assert.True(await _tasks.UpdateAsync(priorTask, priorVersion));
        await _logs.CreateAsync(new WorkflowTransitionLog
        {
            TenantId = _harness.TenantId,
            WorkflowInstanceId = started.WorkflowInstanceId,
            ApprovalTaskId = priorTask.Id,
            Action = contradictoryPriorReject
                ? WorkflowTransitionAction.Reject
                : WorkflowTransitionAction.Approve,
            FromState = ApprovalTaskStatus.WaitingApproval.ToString(),
            ToState = priorTask.Status.ToString(),
            FromStatus = WorkflowInstanceStatus.Active.ToString(),
            ToStatus = WorkflowInstanceStatus.Active.ToString(),
            ActorId = ActorId,
            ActorRef = ActorId,
            ReasonCode = priorTask.ActionReasonCode,
            IdempotencyKey = "prior-decision",
            CorrelationId = "corr-prior",
            SequenceNo = 2
        });

        var finalStatus = finalAction == WorkflowTransitionAction.Approve
            ? ApprovalTaskStatus.Approved
            : ApprovalTaskStatus.Rejected;
        var finalTask = await _tasks.CreateAsync(new ApprovalTask
        {
            TenantId = _harness.TenantId,
            WorkflowInstanceId = started.WorkflowInstanceId,
            StageCode = "stage-2",
            StepCode = "step-2",
            Status = finalStatus,
            ActionedBy = ActorId,
            ActionReasonCode = finalAction == WorkflowTransitionAction.Approve ? "APPROVED" : "REJECTED",
            CompletedAt = DateTimeOffset.UtcNow
        });
        var instance = await _instances.GetByIdAsync(started.WorkflowInstanceId);
        Assert.NotNull(instance);
        var instanceVersion = instance!.Version;
        instance.Status = finalAction == WorkflowTransitionAction.Approve
            ? WorkflowInstanceStatus.Completed
            : WorkflowInstanceStatus.Rejected;
        instance.CompletedAt = DateTimeOffset.UtcNow;
        instance.LastTransitionAt = instance.CompletedAt;
        Assert.True(await _instances.UpdateAsync(instance, instanceVersion));
        await _logs.CreateAsync(new WorkflowTransitionLog
        {
            TenantId = _harness.TenantId,
            WorkflowInstanceId = started.WorkflowInstanceId,
            ApprovalTaskId = finalTask.Id,
            Action = finalAction,
            FromState = ApprovalTaskStatus.WaitingApproval.ToString(),
            ToState = finalTask.Status.ToString(),
            FromStatus = WorkflowInstanceStatus.Active.ToString(),
            ToStatus = instance.Status.ToString(),
            ActorId = ActorId,
            ActorRef = ActorId,
            ReasonCode = finalTask.ActionReasonCode,
            IdempotencyKey = "final-decision",
            CorrelationId = "corr-final",
            SequenceNo = 3
        });
        return finalTask;
    }

    private async Task MakeTerminalAsync(
        TrustedWorkflowStartResult started,
        WorkflowTransitionAction action,
        string logActor,
        string? taskActor = null,
        long sequenceNo = 2)
    {
        var task = await _tasks.GetByIdAsync(started.ApprovalTaskId);
        Assert.NotNull(task);
        var taskVersion = task!.Version;
        task.Status = action == WorkflowTransitionAction.Approve
            ? ApprovalTaskStatus.Approved
            : ApprovalTaskStatus.Rejected;
        task.ActionedBy = taskActor ?? logActor;
        task.ActionReasonCode = action == WorkflowTransitionAction.Approve ? "APPROVED" : "REJECTED";
        task.CompletedAt = DateTimeOffset.UtcNow;
        Assert.True(await _tasks.UpdateAsync(task, taskVersion));

        var instance = await _instances.GetByIdAsync(started.WorkflowInstanceId);
        Assert.NotNull(instance);
        var instanceVersion = instance!.Version;
        instance.Status = action == WorkflowTransitionAction.Approve
            ? WorkflowInstanceStatus.Approved
            : WorkflowInstanceStatus.Rejected;
        instance.CompletedAt = DateTimeOffset.UtcNow;
        instance.LastTransitionAt = instance.CompletedAt;
        Assert.True(await _instances.UpdateAsync(instance, instanceVersion));

        await _logs.CreateAsync(new WorkflowTransitionLog
        {
            TenantId = _harness.TenantId,
            WorkflowInstanceId = started.WorkflowInstanceId,
            ApprovalTaskId = started.ApprovalTaskId,
            Action = action,
            FromState = ApprovalTaskStatus.WaitingApproval.ToString(),
            ToState = task.Status.ToString(),
            FromStatus = WorkflowInstanceStatus.Active.ToString(),
            ToStatus = instance.Status.ToString(),
            ActorId = logActor,
            ActorRef = logActor,
            ReasonCode = task.ActionReasonCode,
            IdempotencyKey = $"decision-{action}",
            CorrelationId = "corr-decision",
            SequenceNo = sequenceNo
        });
    }

    private static readonly Guid ClientId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid MakerId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private const string ActorId = "approver-user";
}
