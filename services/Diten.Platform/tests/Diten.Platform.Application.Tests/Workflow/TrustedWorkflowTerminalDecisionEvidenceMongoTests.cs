using Diten.Platform.Application.Features.Workflow;
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
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            "corr-start");
        Assert.True(response.IsSuccessful);
        return response.Data!;
    }

    private async Task MakeTerminalAsync(
        TrustedWorkflowStartResult started,
        WorkflowTransitionAction action,
        string logActor,
        string? taskActor = null)
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
            SequenceNo = 2
        });
    }

    private const string ActorId = "approver-user";
}
