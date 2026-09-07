using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Services;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

public sealed class TrustedWorkflowCancellationMongoTests : IAsyncLifetime
{
    private MongoIntegrationHarness _harness = null!;
    private IWorkflowInstanceRepository _instances = null!;
    private IApprovalTaskRepository _tasks = null!;
    private IWorkflowTransitionLogRepository _logs = null!;
    private IWorkflowTemplateRepository _templates = null!;
    private IWorkflowTemplateVersionRepository _versions = null!;
    private IRuntimeAssignmentSnapshotRepository _snapshots = null!;
    private Guid _templateId;

    public async Task InitializeAsync()
    {
        _harness = await MongoIntegrationHarness.CreateAsync(SchemaProfile.WorkflowWorkCenter);
        _instances = new WorkflowInstanceRepository(_harness.DbContext, _harness.TenantContext);
        _tasks = new ApprovalTaskRepository(_harness.DbContext, _harness.TenantContext);
        _logs = new WorkflowTransitionLogRepository(_harness.DbContext, _harness.TenantContext);
        _templates = new WorkflowTemplateRepository(_harness.DbContext, _harness.TenantContext);
        _versions = new WorkflowTemplateVersionRepository(_harness.DbContext, _harness.TenantContext);
        _snapshots = new RuntimeAssignmentSnapshotRepository(_harness.DbContext, _harness.TenantContext);

        var template = await _templates.CreateAsync(new WorkflowTemplate
        {
            TenantId = _harness.TenantId,
            TemplateCode = $"CANCEL-{_harness.TenantId:N}",
            Name = "Trusted cancellation",
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

    [Fact]
    public async Task Preflight_cancel_and_same_key_replay_persist_one_atomic_terminal_graph()
    {
        var started = await StartAsync("cancel-start-1");
        var coordinator = CancellationCoordinator();
        var preflight = await coordinator.PreflightAsync(
            ClientId, started.WorkflowInstanceId, started.ApprovalTaskId,
            "GlobalProduct", "GP-1", MakerId, "corr-preflight", default);
        Assert.True(preflight.IsSuccessful);

        var request = new TrustedWorkflowCancellationRequest(
            started.WorkflowInstanceId,
            started.ApprovalTaskId,
            "GlobalProduct",
            "GP-1",
            MakerId,
            preflight.Data!.WorkflowInstanceVersion,
            preflight.Data.ApprovalTaskVersion,
            "WITHDRAW",
            "Requester withdrew before decision.",
            "cancel-operation-1");
        var first = await coordinator.CancelAsync(ClientId, MakerId, request, "corr-cancel", default);
        var replay = await coordinator.CancelAsync(ClientId, MakerId, request, "corr-replay", default);

        Assert.True(first.IsSuccessful);
        Assert.True(replay.IsSuccessful);
        Assert.False(first.Data!.IsReplay);
        Assert.True(replay.Data!.IsReplay);
        Assert.Equal(first.Data.TransitionLogId, replay.Data.TransitionLogId);
        Assert.Equal(ApprovalTaskStatus.Cancelled, (await _tasks.GetByIdAsync(started.ApprovalTaskId))!.Status);
        Assert.Equal(WorkflowInstanceStatus.Cancelled, (await _instances.GetByIdAsync(started.WorkflowInstanceId))!.Status);
        Assert.Single((await _logs.ListByInstanceIdAsync(started.WorkflowInstanceId))
            .Where(x => x.Action == WorkflowTransitionAction.Cancel));
    }

    [Fact]
    public async Task Wrong_requester_and_stale_versions_fail_without_terminal_mutation()
    {
        var started = await StartAsync("cancel-start-2");
        var coordinator = CancellationCoordinator();
        var preflight = (await coordinator.PreflightAsync(
            ClientId, started.WorkflowInstanceId, started.ApprovalTaskId,
            "GlobalProduct", "GP-1", MakerId, "corr-preflight", default)).Data!;
        var request = new TrustedWorkflowCancellationRequest(
            started.WorkflowInstanceId, started.ApprovalTaskId, "GlobalProduct", "GP-1", MakerId,
            preflight.WorkflowInstanceVersion, preflight.ApprovalTaskVersion,
            "WITHDRAW", null, "cancel-operation-2");

        var forbidden = await coordinator.CancelAsync(ClientId, Guid.NewGuid(), request, "corr-forbidden", default);
        var stale = await coordinator.CancelAsync(
            ClientId, MakerId,
            request with { ExpectedApprovalTaskVersion = preflight.ApprovalTaskVersion + 1 },
            "corr-stale", default);

        Assert.Equal(403, forbidden.StatusCode);
        Assert.Equal(409, stale.StatusCode);
        Assert.Equal(WorkflowInstanceStatus.Active, (await _instances.GetByIdAsync(started.WorkflowInstanceId))!.Status);
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, (await _tasks.GetByIdAsync(started.ApprovalTaskId))!.Status);
        Assert.DoesNotContain(await _logs.ListByInstanceIdAsync(started.WorkflowInstanceId),
            x => x.Action == WorkflowTransitionAction.Cancel);
    }

    [Fact]
    public async Task Cross_tenant_and_object_or_client_drift_are_non_disclosing()
    {
        var started = await StartAsync("cancel-start-3");
        var coordinator = CancellationCoordinator();
        var wrongObject = await coordinator.PreflightAsync(
            ClientId, started.WorkflowInstanceId, started.ApprovalTaskId,
            "GlobalProduct", "GP-OTHER", MakerId, "corr-object", default);
        var wrongClient = await coordinator.PreflightAsync(
            Guid.NewGuid(), started.WorkflowInstanceId, started.ApprovalTaskId,
            "GlobalProduct", "GP-1", MakerId, "corr-client", default);
        _harness.TenantContext.SetTenant(Guid.NewGuid());
        var crossTenant = await coordinator.PreflightAsync(
            ClientId, started.WorkflowInstanceId, started.ApprovalTaskId,
            "GlobalProduct", "GP-1", MakerId, "corr-tenant", default);
        _harness.TenantContext.SetTenant(_harness.TenantId);

        Assert.All(new[] { wrongObject, wrongClient, crossTenant }, x => Assert.Equal(404, x.StatusCode));
    }

    [Fact]
    public async Task Concurrent_same_key_and_facts_converge_to_one_stable_replay()
    {
        var started = await StartAsync("cancel-start-concurrent");
        var inner = new PlatformTransactionExecutor(_harness.DbContext);
        var coordinator = new TrustedWorkflowCancellationCoordinator(
            _instances,
            _tasks,
            _logs,
            new TwoCallerBarrierTransactionExecutor(inner));
        var preflight = (await coordinator.PreflightAsync(
            ClientId, started.WorkflowInstanceId, started.ApprovalTaskId,
            "GlobalProduct", "GP-1", MakerId, "corr-preflight", default)).Data!;
        var request = new TrustedWorkflowCancellationRequest(
            started.WorkflowInstanceId,
            started.ApprovalTaskId,
            "GlobalProduct",
            "GP-1",
            MakerId,
            preflight.WorkflowInstanceVersion,
            preflight.ApprovalTaskVersion,
            "WITHDRAW",
            "Concurrent requester replay.",
            "cancel-operation-concurrent");

        var responses = await Task.WhenAll(
            coordinator.CancelAsync(ClientId, MakerId, request, "corr-concurrent-1", default),
            coordinator.CancelAsync(ClientId, MakerId, request, "corr-concurrent-2", default));

        Assert.All(responses, response => Assert.True(response.IsSuccessful));
        Assert.Single(responses.Where(response => response.Data!.IsReplay));
        Assert.Single(responses.Where(response => !response.Data!.IsReplay));
        Assert.Single(responses.Select(response => response.Data!.TransitionLogId).Distinct());
        Assert.Single((await _logs.ListByInstanceIdAsync(started.WorkflowInstanceId))
            .Where(x => x.Action == WorkflowTransitionAction.Cancel));
    }

    private async Task<TrustedWorkflowStartResult> StartAsync(string key)
    {
        var result = await new WorkflowInstanceStartCoordinator(
            _templates, _versions, _instances, _tasks, _snapshots, _logs, _harness.TenantContext)
            .StartAsync(new TrustedWorkflowStartRequest(
                _templateId, null, "GlobalProduct", "GP-1", null,
                ["approver"], "SUBMIT", key, false, false, null),
                ClientId, MakerId, "corr-start");
        Assert.True(result.IsSuccessful);
        return result.Data!;
    }

    private TrustedWorkflowCancellationCoordinator CancellationCoordinator() => new(
        _instances,
        _tasks,
        _logs,
        new PlatformTransactionExecutor(_harness.DbContext));

    private static readonly Guid ClientId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MakerId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private sealed class TwoCallerBarrierTransactionExecutor : IPlatformTransactionExecutor
    {
        private readonly IPlatformTransactionExecutor _inner;
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;

        public TwoCallerBarrierTransactionExecutor(IPlatformTransactionExecutor inner) => _inner = inner;

        public async Task<T> ExecuteAsync<T>(
            Func<IPlatformTransactionSession, CancellationToken, Task<T>> body,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _arrivals) == 2)
            {
                _release.TrySetResult();
            }

            await _release.Task.WaitAsync(cancellationToken);
            return await _inner.ExecuteAsync(body, cancellationToken);
        }
    }
}
