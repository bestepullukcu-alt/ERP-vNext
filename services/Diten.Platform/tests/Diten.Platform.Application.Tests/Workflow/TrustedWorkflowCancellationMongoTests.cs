using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Services;
using Diten.Platform.Application.Tests.Audit;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

public sealed class TrustedWorkflowCancellationMongoTests(
    AuditOutboxTemporalReplicaSetFixture replicaSet) :
    IAsyncLifetime,
    IClassFixture<AuditOutboxTemporalReplicaSetFixture>
{
    private CancellationStore _store = null!;

    public async Task InitializeAsync()
    {
        _store = await CreateStoreAsync(replicaSet.ConnectionString, "diten_platform_trusted_workflow_cancellation");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Transaction_unavailable_fails_closed_without_terminal_mutation()
    {
        var standalone = await CreateStoreAsync(
            replicaSet.StandaloneConnectionString,
            "diten_platform_trusted_workflow_cancellation_standalone");
        var started = await StartAsync(standalone, "cancel-standalone-1");
        var coordinator = CreateCancellationCoordinator(standalone);
        var preflight = await coordinator.PreflightAsync(
            ClientId, started.WorkflowInstanceId, started.ApprovalTaskId,
            "GlobalProduct", "GP-1", MakerId, "corr-preflight", default);
        Assert.True(preflight.IsSuccessful);

        var result = await coordinator.CancelAsync(
            ClientId,
            MakerId,
            new TrustedWorkflowCancellationRequest(
                started.WorkflowInstanceId,
                started.ApprovalTaskId,
                "GlobalProduct",
                "GP-1",
                MakerId,
                preflight.Data!.WorkflowInstanceVersion,
                preflight.Data.ApprovalTaskVersion,
                "WITHDRAW",
                null,
                "cancel-standalone-1"),
            "corr-cancel",
            default);

        Assert.False(result.IsSuccessful);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal("WORKFLOW_TRUSTED_CANCEL_UNAVAILABLE", result.ReasonCode);
        Assert.Equal(ApprovalTaskStatus.WaitingApproval,
            (await standalone.Tasks.GetByIdAsync(started.ApprovalTaskId))!.Status);
        Assert.Equal(WorkflowInstanceStatus.Active,
            (await standalone.Instances.GetByIdAsync(started.WorkflowInstanceId))!.Status);
        Assert.DoesNotContain(await standalone.Logs.ListByInstanceIdAsync(started.WorkflowInstanceId),
            x => x.Action == WorkflowTransitionAction.Cancel);
    }

    private static async Task<CancellationStore> CreateStoreAsync(string connectionString, string databaseName)
    {
        var settings = MongoClientSettings.FromConnectionString(connectionString);
#pragma warning disable CS0618
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        var client = new MongoClient(settings);
        var database = client.GetDatabase(databaseName);
        await PlatformSchemaManifest.ApplyAsync(database, [SchemaProfile.WorkflowWorkCenter]);

        var tenantId = Guid.NewGuid();
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId);
        var context = new PlatformDbContext(client, database);
        var instances = new WorkflowInstanceRepository(context, tenantContext);
        var tasks = new ApprovalTaskRepository(context, tenantContext);
        var logs = new WorkflowTransitionLogRepository(context, tenantContext);
        var templates = new WorkflowTemplateRepository(context, tenantContext);
        var versions = new WorkflowTemplateVersionRepository(context, tenantContext);
        var snapshots = new RuntimeAssignmentSnapshotRepository(context, tenantContext);

        var template = await templates.CreateAsync(new WorkflowTemplate
        {
            TenantId = tenantId,
            TemplateCode = $"CANCEL-{tenantId:N}",
            Name = "Trusted cancellation",
            Status = WorkflowTemplateStatus.Published
        });
        var version = await versions.CreateAsync(new WorkflowTemplateVersion
        {
            TenantId = tenantId,
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
        Assert.True(await templates.UpdateAsync(template, template.Version));
        return new CancellationStore(
            context,
            tenantContext,
            instances,
            tasks,
            logs,
            templates,
            versions,
            snapshots,
            template.Id);
    }

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
        Assert.Equal(ApprovalTaskStatus.Cancelled, (await _store.Tasks.GetByIdAsync(started.ApprovalTaskId))!.Status);
        Assert.Equal(WorkflowInstanceStatus.Cancelled, (await _store.Instances.GetByIdAsync(started.WorkflowInstanceId))!.Status);
        Assert.Single((await _store.Logs.ListByInstanceIdAsync(started.WorkflowInstanceId))
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
        Assert.Equal(WorkflowInstanceStatus.Active, (await _store.Instances.GetByIdAsync(started.WorkflowInstanceId))!.Status);
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, (await _store.Tasks.GetByIdAsync(started.ApprovalTaskId))!.Status);
        Assert.DoesNotContain(await _store.Logs.ListByInstanceIdAsync(started.WorkflowInstanceId),
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
        _store.TenantContext.SetTenant(Guid.NewGuid());
        var crossTenant = await coordinator.PreflightAsync(
            ClientId, started.WorkflowInstanceId, started.ApprovalTaskId,
            "GlobalProduct", "GP-1", MakerId, "corr-tenant", default);
        _store.TenantContext.SetTenant(_store.TenantId);

        Assert.All(new[] { wrongObject, wrongClient, crossTenant }, x => Assert.Equal(404, x.StatusCode));
    }

    [Fact]
    public async Task Concurrent_same_key_and_facts_converge_to_one_stable_replay()
    {
        var started = await StartAsync("cancel-start-concurrent");
        var inner = new PlatformTransactionExecutor(_store.Context);
        var coordinator = new TrustedWorkflowCancellationCoordinator(
            _store.Instances,
            _store.Tasks,
            _store.Logs,
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
        Assert.Single((await _store.Logs.ListByInstanceIdAsync(started.WorkflowInstanceId))
            .Where(x => x.Action == WorkflowTransitionAction.Cancel));
    }

    private Task<TrustedWorkflowStartResult> StartAsync(string key) => StartAsync(_store, key);

    private static async Task<TrustedWorkflowStartResult> StartAsync(CancellationStore store, string key)
    {
        var result = await new WorkflowInstanceStartCoordinator(
            store.Templates, store.Versions, store.Instances, store.Tasks, store.Snapshots, store.Logs, store.TenantContext)
            .StartAsync(new TrustedWorkflowStartRequest(
                store.TemplateId, null, "GlobalProduct", "GP-1", null,
                ["approver"], "SUBMIT", key, false, false, null),
                ClientId, MakerId, "corr-start");
        Assert.True(result.IsSuccessful);
        return result.Data!;
    }

    private TrustedWorkflowCancellationCoordinator CancellationCoordinator() => CreateCancellationCoordinator(_store);

    private static TrustedWorkflowCancellationCoordinator CreateCancellationCoordinator(CancellationStore store) => new(
        store.Instances,
        store.Tasks,
        store.Logs,
        new PlatformTransactionExecutor(store.Context));

    private sealed record CancellationStore(
        IPlatformDbContext Context,
        TenantContext TenantContext,
        IWorkflowInstanceRepository Instances,
        IApprovalTaskRepository Tasks,
        IWorkflowTransitionLogRepository Logs,
        IWorkflowTemplateRepository Templates,
        IWorkflowTemplateVersionRepository Versions,
        IRuntimeAssignmentSnapshotRepository Snapshots,
        Guid TemplateId)
    {
        public Guid TenantId => TenantContext.TenantId;
    }

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
