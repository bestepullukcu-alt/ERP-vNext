using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Services;
using Diten.Platform.Application.Features.Workflow.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Workflow.Queries;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

public sealed class TrustedWorkflowStartRecoveryMongoTests : IAsyncLifetime
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
            TemplateCode = $"TRUSTED-{_harness.TenantId:N}",
            Name = "Trusted workflow recovery",
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
    public async Task Exact_replay_returns_same_completed_graph_without_duplicates()
    {
        var coordinator = Coordinator();
        var request = Request("trusted-replay");

        var first = await coordinator.StartAsync(request, ClientId, MakerId, "corr-1");
        var replay = await coordinator.StartAsync(request, ClientId, MakerId, "corr-2");

        Assert.True(first.IsSuccessful);
        Assert.Equal(201, first.StatusCode);
        Assert.True(replay.IsSuccessful);
        Assert.Equal(200, replay.StatusCode);
        Assert.True(replay.Data!.IsReplay);
        Assert.Equal(first.Data!.WorkflowInstanceId, replay.Data.WorkflowInstanceId);
        Assert.Equal(first.Data.ApprovalTaskId, replay.Data.ApprovalTaskId);
        Assert.Equal(first.Data.AssignmentSnapshotId, replay.Data.AssignmentSnapshotId);
        Assert.Equal(first.Data.StartTransitionLogId, replay.Data.StartTransitionLogId);

        Assert.Single(await _instances.GetAllForTenantAsync());
        Assert.Single(await _tasks.ListByInstanceIdAsync(first.Data.WorkflowInstanceId));
        Assert.Single(await _snapshots.ListByInstanceIdAsync(first.Data.WorkflowInstanceId));
        Assert.Single(await _logs.ListByInstanceIdAsync(first.Data.WorkflowInstanceId));
        var stored = await _instances.GetByIdAsync(first.Data.WorkflowInstanceId);
        Assert.Equal(WorkflowStartCheckpoint.Completed, stored!.StartCheckpoint);
        Assert.Equal(WorkflowInstanceStatus.Active, stored.Status);
    }

    [Fact]
    public async Task Same_key_with_payload_drift_conflicts_without_mutating_graph()
    {
        var coordinator = Coordinator();
        var first = await coordinator.StartAsync(Request("trusted-drift"), ClientId, MakerId, "corr-1");
        var drifted = Request("trusted-drift") with { ObjectId = "PRODUCT-DRIFT" };

        var conflict = await coordinator.StartAsync(drifted, ClientId, MakerId, "corr-2");

        Assert.True(first.IsSuccessful);
        Assert.False(conflict.IsSuccessful);
        Assert.Equal(409, conflict.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowStartIdempotencyConflict, conflict.ReasonCode);
        Assert.Single(await _instances.GetAllForTenantAsync());
        Assert.Single(await _tasks.ListByInstanceIdAsync(first.Data!.WorkflowInstanceId));
        Assert.Single(await _snapshots.ListByInstanceIdAsync(first.Data.WorkflowInstanceId));
        Assert.Single(await _logs.ListByInstanceIdAsync(first.Data.WorkflowInstanceId));
    }

    [Fact]
    public async Task Concurrent_matching_starts_converge_on_one_completed_graph()
    {
        var request = Request("trusted-concurrent-match") with { DueAt = null };

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(index =>
            Coordinator().StartAsync(request, ClientId, MakerId, $"corr-{index}")));

        Assert.All(responses, response => Assert.True(response.IsSuccessful));
        Assert.Single(responses.Select(response => response.Data!.WorkflowInstanceId).Distinct());
        var instanceId = responses[0].Data!.WorkflowInstanceId;
        Assert.Single(await _instances.GetAllForTenantAsync());
        Assert.Single(await _tasks.ListByInstanceIdAsync(instanceId));
        Assert.Single(await _snapshots.ListByInstanceIdAsync(instanceId));
        Assert.Single(await _logs.ListByInstanceIdAsync(instanceId));
    }

    [Fact]
    public async Task Concurrent_same_key_different_fingerprints_produce_one_success_and_one_conflict()
    {
        var firstRequest = Request("trusted-concurrent-drift");
        var secondRequest = firstRequest with { ObjectId = "GP-CONCURRENT-DRIFT" };

        var responses = await Task.WhenAll(
            Coordinator().StartAsync(firstRequest, ClientId, MakerId, "corr-a"),
            Coordinator().StartAsync(secondRequest, ClientId, MakerId, "corr-b"));

        Assert.Single(responses.Where(response => response.IsSuccessful));
        var conflict = Assert.Single(responses.Where(response => !response.IsSuccessful));
        Assert.Equal(409, conflict.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowStartIdempotencyConflict, conflict.ReasonCode);
        Assert.Single(await _instances.GetAllForTenantAsync());
    }

    [Theory]
    [InlineData(CrashBoundary.Reservation)]
    [InlineData(CrashBoundary.Task)]
    [InlineData(CrashBoundary.Snapshot)]
    [InlineData(CrashBoundary.StartLog)]
    [InlineData(CrashBoundary.CompletedCheckpoint)]
    public async Task Replay_recovers_same_graph_after_each_persisted_boundary(CrashBoundary boundary)
    {
        var request = Request($"trusted-recovery-{boundary}");
        var crashing = Coordinator(boundary);

        await Assert.ThrowsAsync<InjectedCrashException>(() =>
            crashing.StartAsync(request, ClientId, MakerId, "corr-crash"));

        var recovered = await Coordinator().StartAsync(request, ClientId, MakerId, "corr-replay");

        Assert.True(recovered.IsSuccessful);
        Assert.Equal(200, recovered.StatusCode);
        Assert.True(recovered.Data!.IsReplay);
        Assert.Single(await _instances.GetAllForTenantAsync());
        Assert.Single(await _tasks.ListByInstanceIdAsync(recovered.Data.WorkflowInstanceId));
        Assert.Single(await _snapshots.ListByInstanceIdAsync(recovered.Data.WorkflowInstanceId));
        Assert.Single(await _logs.ListByInstanceIdAsync(recovered.Data.WorkflowInstanceId));
        var stored = await _instances.GetByIdAsync(recovered.Data.WorkflowInstanceId);
        Assert.Equal(WorkflowStartCheckpoint.Completed, stored!.StartCheckpoint);
        Assert.Equal(WorkflowInstanceStatus.Active, stored.Status);
    }

    [Fact]
    public async Task Service_only_start_result_returns_sanitized_completed_graph_and_mismatches_do_not_leak()
    {
        var started = await Coordinator().StartAsync(Request("trusted-result"), ClientId, MakerId, "corr-start");
        var handler = new GetTrustedWorkflowStartResultHandler(_instances, _tasks, _snapshots, _logs);

        var found = await handler.Handle(new(
            "trusted-result", ClientId, MakerId, "GlobalProduct", "GP-0001", "corr-read"), default);
        var wrongClient = await handler.Handle(new(
            "trusted-result", Guid.NewGuid(), MakerId, "GlobalProduct", "GP-0001", "corr-client"), default);
        var wrongMaker = await handler.Handle(new(
            "trusted-result", ClientId, Guid.NewGuid(), "GlobalProduct", "GP-0001", "corr-maker"), default);
        var wrongObject = await handler.Handle(new(
            "trusted-result", ClientId, MakerId, "GlobalProduct", "GP-OTHER", "corr-object"), default);
        _harness.TenantContext.SetTenant(Guid.NewGuid());
        var crossTenant = await handler.Handle(new(
            "trusted-result", ClientId, MakerId, "GlobalProduct", "GP-0001", "corr-tenant"), default);
        _harness.TenantContext.SetTenant(_harness.TenantId);

        Assert.True(found.IsSuccessful);
        Assert.Equal(200, found.StatusCode);
        Assert.True(found.Data!.IsReplay);
        Assert.Equal(started.Data!.WorkflowInstanceId, found.Data.WorkflowInstanceId);
        Assert.All(new[] { wrongClient, wrongMaker, wrongObject, crossTenant }, response =>
        {
            Assert.Equal(404, response.StatusCode);
            Assert.Equal(WorkflowReasonCodes.NotFoundNonLeakage, response.ReasonCode);
        });
    }

    [Fact]
    public async Task Service_only_start_result_reports_incomplete_checkpoint_as_retryable_conflict()
    {
        await Assert.ThrowsAsync<InjectedCrashException>(() =>
            Coordinator(CrashBoundary.Reservation).StartAsync(
                Request("trusted-result-incomplete"), ClientId, MakerId, "corr-crash"));
        var handler = new GetTrustedWorkflowStartResultHandler(_instances, _tasks, _snapshots, _logs);

        var response = await handler.Handle(new(
            "trusted-result-incomplete", ClientId, MakerId,
            "GlobalProduct", "GP-0001", "corr-read"), default);

        Assert.Equal(409, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowStartNotCompleted, response.ReasonCode);
    }

    [Fact]
    public async Task Service_only_start_result_rejects_inconsistent_persisted_graph()
    {
        var started = await Coordinator().StartAsync(
            Request("trusted-result-inconsistent"), ClientId, MakerId, "corr-start");
        var task = await _tasks.GetByIdAsync(started.Data!.ApprovalTaskId);
        Assert.NotNull(task);
        task!.AssignmentSnapshotId = Guid.NewGuid();
        Assert.True(await _tasks.UpdateAsync(task, task.Version));
        var handler = new GetTrustedWorkflowStartResultHandler(_instances, _tasks, _snapshots, _logs);

        var response = await handler.Handle(new(
            "trusted-result-inconsistent", ClientId, MakerId,
            "GlobalProduct", "GP-0001", "corr-read"), default);

        Assert.Equal(409, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowStartRecoveryConflict, response.ReasonCode);
    }

    [Theory]
    [InlineData(StartProofCorruption.InstanceStartedBy)]
    [InlineData(StartProofCorruption.LogActorRef)]
    [InlineData(StartProofCorruption.LogToState)]
    [InlineData(StartProofCorruption.LogToStatus)]
    [InlineData(StartProofCorruption.Combined)]
    public async Task Service_only_start_result_rejects_corrupted_native_start_facts(
        StartProofCorruption corruption)
    {
        var key = $"trusted-result-native-{corruption}";
        var started = await Coordinator().StartAsync(Request(key), ClientId, MakerId, "corr-start");
        Assert.True(started.IsSuccessful);

        if (corruption is StartProofCorruption.InstanceStartedBy or StartProofCorruption.Combined)
        {
            var instance = await _instances.GetByIdAsync(started.Data!.WorkflowInstanceId);
            Assert.NotNull(instance);
            instance!.StartedBy = Guid.NewGuid().ToString("D");
            Assert.True(await _instances.UpdateAsync(instance, instance.Version));
        }

        var logUpdates = new List<UpdateDefinition<WorkflowTransitionLog>>();
        if (corruption is StartProofCorruption.LogActorRef or StartProofCorruption.Combined)
        {
            logUpdates.Add(Builders<WorkflowTransitionLog>.Update.Set(x => x.ActorRef, "forged-actor"));
        }

        if (corruption is StartProofCorruption.LogToState or StartProofCorruption.Combined)
        {
            logUpdates.Add(Builders<WorkflowTransitionLog>.Update.Set(x => x.ToState, "Pending"));
        }

        if (corruption is StartProofCorruption.LogToStatus or StartProofCorruption.Combined)
        {
            logUpdates.Add(Builders<WorkflowTransitionLog>.Update.Set(x => x.ToStatus, "Completed"));
        }

        if (logUpdates.Count != 0)
        {
            var logs = _harness.Database.GetCollection<WorkflowTransitionLog>(
                PlatformCollections.WorkflowTransitionLogs);
            var update = await logs.UpdateOneAsync(
                x => x.TenantId == _harness.TenantId && x.Id == started.Data!.StartTransitionLogId,
                Builders<WorkflowTransitionLog>.Update.Combine(logUpdates));
            Assert.Equal(1, update.ModifiedCount);
        }

        var response = await new GetTrustedWorkflowStartResultHandler(_instances, _tasks, _snapshots, _logs)
            .Handle(new(
                key, ClientId, MakerId, "GlobalProduct", "GP-0001", "corr-read"), default);

        Assert.Equal(409, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowStartRecoveryConflict, response.ReasonCode);
    }

    private WorkflowInstanceStartCoordinator Coordinator(CrashBoundary? boundary = null) =>
        new(
            _templates,
            _versions,
            boundary switch
            {
                CrashBoundary.Reservation => new CrashAfterReservationInstanceRepository(_instances),
                CrashBoundary.CompletedCheckpoint => new CrashAfterCompletionInstanceRepository(_instances),
                _ => _instances
            },
            boundary == CrashBoundary.Task ? new CrashAfterTaskRepository(_tasks) : _tasks,
            boundary == CrashBoundary.Snapshot ? new CrashAfterSnapshotRepository(_snapshots) : _snapshots,
            boundary == CrashBoundary.StartLog ? new CrashAfterLogRepository(_logs) : _logs,
            _harness.TenantContext);

    private TrustedWorkflowStartRequest Request(string key) =>
        new(
            _templateId,
            null,
            "GlobalProduct",
            "GP-0001",
            null,
            ["maker-approval"],
            "SUBMIT",
            key,
            true,
            false,
            DateTimeOffset.UtcNow.AddHours(1));

    private static readonly Guid ClientId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MakerId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public enum CrashBoundary
    {
        Reservation,
        Task,
        Snapshot,
        StartLog,
        CompletedCheckpoint
    }

    public enum StartProofCorruption
    {
        InstanceStartedBy,
        LogActorRef,
        LogToState,
        LogToStatus,
        Combined
    }

    private sealed class InjectedCrashException : Exception;

    private sealed class CrashAfterReservationInstanceRepository(IWorkflowInstanceRepository inner)
        : DelegatingInstanceRepository(inner)
    {
        public override async Task<(WorkflowInstance Instance, bool Created)> ReserveTrustedStartAsync(
            WorkflowInstance value,
            CancellationToken ct = default)
        {
            await Inner.ReserveTrustedStartAsync(value, ct);
            throw new InjectedCrashException();
        }
    }

    private sealed class CrashAfterTaskRepository(IApprovalTaskRepository inner) : IApprovalTaskRepository
    {
        public Task<ApprovalTask> CreateAsync(ApprovalTask task, CancellationToken ct = default) => inner.CreateAsync(task, ct);
        public Task<ApprovalTask?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
        public Task<ApprovalTask?> GetFirstByInstanceIdAsync(Guid id, CancellationToken ct = default) => inner.GetFirstByInstanceIdAsync(id, ct);
        public Task<ApprovalTask?> GetActiveByInstanceIdAsync(Guid id, CancellationToken ct = default) => inner.GetActiveByInstanceIdAsync(id, ct);
        public Task<IReadOnlyList<ApprovalTask>> ListByInstanceIdAsync(Guid id, CancellationToken ct = default) => inner.ListByInstanceIdAsync(id, ct);
        public Task<IReadOnlyList<ApprovalTask>> GetAllForTenantAsync(CancellationToken ct = default) => inner.GetAllForTenantAsync(ct);
        public Task<bool> UpdateAsync(ApprovalTask task, int version, CancellationToken ct = default) => inner.UpdateAsync(task, version, ct);
        public async Task<ApprovalTask> EnsureTrustedStartTaskAsync(ApprovalTask task, CancellationToken ct = default)
        {
            await inner.EnsureTrustedStartTaskAsync(task, ct);
            throw new InjectedCrashException();
        }
    }

    private sealed class CrashAfterSnapshotRepository(IRuntimeAssignmentSnapshotRepository inner)
        : IRuntimeAssignmentSnapshotRepository
    {
        public Task<RuntimeAssignmentSnapshot> CreateAsync(RuntimeAssignmentSnapshot value, CancellationToken ct = default) => inner.CreateAsync(value, ct);
        public Task<RuntimeAssignmentSnapshot?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
        public Task<IReadOnlyList<RuntimeAssignmentSnapshot>> ListByInstanceIdAsync(Guid id, CancellationToken ct = default) => inner.ListByInstanceIdAsync(id, ct);
        public async Task<RuntimeAssignmentSnapshot> EnsureTrustedStartSnapshotAsync(RuntimeAssignmentSnapshot value, CancellationToken ct = default)
        {
            await inner.EnsureTrustedStartSnapshotAsync(value, ct);
            throw new InjectedCrashException();
        }
    }

    private sealed class CrashAfterLogRepository(IWorkflowTransitionLogRepository inner) : IWorkflowTransitionLogRepository
    {
        public Task<WorkflowTransitionLog> CreateAsync(WorkflowTransitionLog log, CancellationToken ct = default) => inner.CreateAsync(log, ct);
        public Task<WorkflowTransitionLog> AppendAsync(WorkflowTransitionLog log, CancellationToken ct = default) => inner.AppendAsync(log, ct);
        public Task<WorkflowTransitionLog?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
        public Task<WorkflowTransitionLog?> GetByTaskActionIdempotencyKeyAsync(Guid taskId, WorkflowTransitionAction action, string key, CancellationToken ct = default) => inner.GetByTaskActionIdempotencyKeyAsync(taskId, action, key, ct);
        public Task<long> GetLatestSequenceNoAsync(Guid id, CancellationToken ct = default) => inner.GetLatestSequenceNoAsync(id, ct);
        public Task<IReadOnlyList<WorkflowTransitionLog>> ListByInstanceIdAsync(Guid id, CancellationToken ct = default) => inner.ListByInstanceIdAsync(id, ct);
        public async Task<WorkflowTransitionLog> EnsureTrustedStartLogAsync(WorkflowTransitionLog log, CancellationToken ct = default)
        {
            await inner.EnsureTrustedStartLogAsync(log, ct);
            throw new InjectedCrashException();
        }
    }

    private sealed class CrashAfterCompletionInstanceRepository(IWorkflowInstanceRepository inner)
        : DelegatingInstanceRepository(inner)
    {
        public override async Task<bool> AdvanceStartCheckpointAsync(Guid id, int version, WorkflowStartCheckpoint expected, WorkflowStartCheckpoint next, CancellationToken ct = default)
        {
            var updated = await Inner.AdvanceStartCheckpointAsync(id, version, expected, next, ct);
            if (next == WorkflowStartCheckpoint.Completed && updated)
            {
                throw new InjectedCrashException();
            }

            return updated;
        }
    }

    private abstract class DelegatingInstanceRepository(IWorkflowInstanceRepository inner)
        : IWorkflowInstanceRepository
    {
        protected IWorkflowInstanceRepository Inner { get; } = inner;
        public Task<WorkflowInstance> CreateAsync(WorkflowInstance value, CancellationToken ct = default) => Inner.CreateAsync(value, ct);
        public Task<WorkflowInstance?> GetByIdAsync(Guid id, CancellationToken ct = default) => Inner.GetByIdAsync(id, ct);
        public Task<WorkflowInstance?> GetByIdempotencyKeyAsync(string key, CancellationToken ct = default) => Inner.GetByIdempotencyKeyAsync(key, ct);
        public Task<WorkflowInstance?> GetLatestByObjectRefAsync(string objectRef, string objectType, string objectId, CancellationToken ct = default) => Inner.GetLatestByObjectRefAsync(objectRef, objectType, objectId, ct);
        public Task<IReadOnlyList<WorkflowInstance>> GetAllForTenantAsync(CancellationToken ct = default) => Inner.GetAllForTenantAsync(ct);
        public Task<bool> UpdateAsync(WorkflowInstance value, int version, CancellationToken ct = default) => Inner.UpdateAsync(value, version, ct);
        public virtual Task<(WorkflowInstance Instance, bool Created)> ReserveTrustedStartAsync(WorkflowInstance value, CancellationToken ct = default) => Inner.ReserveTrustedStartAsync(value, ct);
        public virtual Task<bool> AdvanceStartCheckpointAsync(Guid id, int version, WorkflowStartCheckpoint expected, WorkflowStartCheckpoint next, CancellationToken ct = default) => Inner.AdvanceStartCheckpointAsync(id, version, expected, next, ct);
    }
}
