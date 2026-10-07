using System.Diagnostics.Metrics;
using Diten.BuildingBlocks.BackgroundJobs;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.Behaviors;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Features.Tasks.Recovery;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.BackgroundJobs;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Workflow.Services;
using Diten.Platform.Application.Tests.Audit;
using Diten.Platform.Application.Tests.TimeEntry;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

/// <summary>
/// WP-WF-ENGINE-RECOVERY-01 — THE ENGINE ENDS WHAT IT LEFT HALF-DONE, over the real repositories on a disposable replica set
/// (transactions; never the shared 27017), every rule through its production handler:
/// <list type="bullet">
/// <item>BL-549 rule 1 — a generic start that crashed after its instance (or after its task) is COMPLETED by its key's replay,
/// with the first call's answer; different facts get 409; two replays at once write one task.</item>
/// <item>BL-549 rule 2 — an abandoned start (Active, no task, past the age) is closed by the engine's own terminal write
/// (reason, log, completion event, audit row); one with a task, a young one and another tenant's are left alone; the task
/// approval that was stuck on it starts a new round.</item>
/// <item>Rule 3 — the gauges count what the cycle found, incomplete trusted starts included.</item>
/// <item>BL-540 (1) — the orphan recovery lists by default and closes only orphans when told to.</item>
/// </list>
/// </summary>
public sealed class WorkflowStartRecoveryMongoTests(AuditOutboxTemporalReplicaSetFixture replicaSet) :
    IAsyncLifetime,
    IClassFixture<AuditOutboxTemporalReplicaSetFixture>
{
    private static readonly Guid Approver = Guid.Parse("0a000000-0000-4000-8000-0000000000a1");
    private static readonly Guid OtherApprover = Guid.Parse("0a000000-0000-4000-8000-0000000000a2");
    private static readonly Guid Starter = Guid.Parse("0a000000-0000-4000-8000-0000000000a3");
    private static readonly TimeSpan Age = TimeSpan.FromMinutes(30);
    private const int ScanLimit = WorkflowStartRecoveryOptions.DefaultScanLimitPerTenant;

    private IPlatformDbContext _context = null!;
    private TenantContext _tenant = null!;
    private WorkflowTemplateRepository _templates = null!;
    private WorkflowTemplateVersionRepository _versions = null!;
    private WorkflowInstanceRepository _instances = null!;
    private ApprovalTaskRepository _tasks = null!;
    private RuntimeAssignmentSnapshotRepository _snapshots = null!;
    private WorkflowTransitionLogRepository _logs = null!;
    private WorkflowStartRecoveryStore _store = null!;
    private TaskItemRepository _taskItems = null!;
    private Guid _templateId;
    private readonly CapturingAuditService _audit = new();

    public async Task InitializeAsync()
    {
        var settings = MongoClientSettings.FromConnectionString(replicaSet.ConnectionString);
#pragma warning disable CS0618
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        var client = new MongoClient(settings);
        var database = client.GetDatabase("diten_platform_workflow_start_recovery");
        await PlatformSchemaManifest.ApplyAsync(database, [SchemaProfile.WorkflowWorkCenter, SchemaProfile.Eventing]);
        _tenant = new TenantContext();
        _tenant.SetTenant(Guid.NewGuid());
        _context = new PlatformDbContext(client, database);
        _templates = new WorkflowTemplateRepository(_context, _tenant);
        _versions = new WorkflowTemplateVersionRepository(_context, _tenant);
        _instances = new WorkflowInstanceRepository(_context, _tenant);
        _tasks = new ApprovalTaskRepository(_context, _tenant);
        _snapshots = new RuntimeAssignmentSnapshotRepository(_context, _tenant);
        _logs = new WorkflowTransitionLogRepository(_context, _tenant);
        _store = new WorkflowStartRecoveryStore(_context, _tenant);
        _taskItems = new TaskItemRepository(_context, _tenant, new TaskTransitionRepository(_context, _tenant));

        var template = await _templates.CreateAsync(new WorkflowTemplate
        {
            TenantId = _tenant.TenantId,
            TemplateCode = "RECOVERY-" + Guid.NewGuid().ToString("N")[..10],
            Name = "Start recovery",
            Status = WorkflowTemplateStatus.Published
        });
        var version = await _versions.CreateAsync(new WorkflowTemplateVersion
        {
            TenantId = _tenant.TenantId,
            TemplateId = template.Id,
            VersionNumber = 1,
            DefinitionJson = "{}",
            SchemaVersion = "1.0",
            ExpressionVersion = "1.0",
            Status = WorkflowTemplateVersionStatus.Published,
            IsImmutable = true
        });
        template.ActivePublishedVersionId = version.Id;
        Assert.True(await _templates.UpdateAsync(template, template.Version));
        _templateId = template.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── BL-549 rule 1 · the replay completes ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_replay_completes_a_start_that_crashed_after_its_instance_and_answers_as_the_first_call_would()
    {
        var request = Request("crash-after-instance");
        await Assert.ThrowsAsync<InjectedCrashException>(() =>
            Handler(tasks: new CrashBeforeTaskRepository(_tasks)).Handle(Command(request), default));
        var instance = Assert.Single(await InstancesOfAsync(request));
        Assert.Equal(WorkflowInstanceStatus.Active, instance.Status);
        Assert.Empty(await _tasks.ListByInstanceIdAsync(instance.Id)); // non-vacuity: the crash left it task-less

        var replay = await Handler().Handle(Command(request), default);

        Assert.True(replay.IsSuccessful, replay.ReasonCode);
        Assert.Equal(instance.Id, replay.Data!.WorkflowInstanceId);
        Assert.Equal(instance.InitialApprovalTaskId, replay.Data.ApprovalTaskId); // the ids the first call fixed
        Assert.Equal(instance.InitialAssignmentSnapshotId, replay.Data.AssignmentSnapshotId);
        var task = Assert.Single(await _tasks.ListByInstanceIdAsync(instance.Id));
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, task.Status);
        Assert.Equal(D(Approver), task.AssigneeRef);
        Assert.Single(await _snapshots.ListByInstanceIdAsync(instance.Id));
        var log = Assert.Single(await _logs.ListByInstanceIdAsync(instance.Id));
        Assert.Equal((WorkflowTransitionAction.Start, 1L, instance.StartTransitionLogId), (log.Action, log.SequenceNo, (Guid?)log.Id));

        // A later replay answers the same, and writes nothing more.
        var again = await Handler().Handle(Command(request), default);
        Assert.Equal(replay.Data.ApprovalTaskId, again.Data!.ApprovalTaskId);
        Assert.Single(await _tasks.ListByInstanceIdAsync(instance.Id));
    }

    [Fact]
    public async Task A_replay_completes_a_start_that_crashed_after_its_task_with_the_snapshot_the_task_names()
    {
        // FIX3 K8 — a start today writes its task, snapshot and log together, so this state is only left by a start written
        // before that (its task stored, its snapshot not): built here as it was left.
        var request = Request("crash-after-task");
        var instance = await InsertHalfStartAsync(request, withIds: true);
        await _context.Database.GetCollection<ApprovalTask>(PlatformCollections.ApprovalTasks).InsertOneAsync(new ApprovalTask
        {
            Id = instance.InitialApprovalTaskId!.Value, TenantId = _tenant.TenantId, WorkflowInstanceId = instance.Id,
            StageCode = "stage-1", StepCode = "step-1", Status = ApprovalTaskStatus.WaitingApproval, AssigneeRef = D(Approver),
            AssignmentSnapshotId = instance.InitialAssignmentSnapshotId, IdempotencyKey = request.IdempotencyKey
        });
        var task = Assert.Single(await _tasks.ListByInstanceIdAsync(instance.Id));
        Assert.Null(await _snapshots.GetByIdAsync(task.AssignmentSnapshotId!.Value)); // non-vacuity: no snapshot yet

        var replay = await Handler().Handle(Command(request), default);

        Assert.True(replay.IsSuccessful, replay.ReasonCode);
        Assert.Equal(task.Id, replay.Data!.ApprovalTaskId);
        Assert.Equal(task.AssignmentSnapshotId, replay.Data.AssignmentSnapshotId);
        Assert.NotNull(await _snapshots.GetByIdAsync(task.AssignmentSnapshotId!.Value));
        Assert.Single(await _tasks.ListByInstanceIdAsync(instance.Id));
        Assert.Single(await _logs.ListByInstanceIdAsync(instance.Id));
    }

    [Fact]
    public async Task A_start_crashed_before_BL549_without_ids_on_its_instance_is_completed_too()
    {
        var request = Request("legacy-half");
        var instance = await InsertHalfStartAsync(request, withIds: false);

        var replay = await Handler().Handle(Command(request), default);

        Assert.True(replay.IsSuccessful, replay.ReasonCode);
        var task = Assert.Single(await _tasks.ListByInstanceIdAsync(instance.Id));
        Assert.Equal(task.Id, replay.Data!.ApprovalTaskId);
        Assert.Equal(task.Id, (await _instances.GetByIdAsync(instance.Id))!.InitialApprovalTaskId); // the claim fixed it
    }

    [Fact]
    public async Task A_replay_with_different_facts_is_refused_and_writes_nothing()
    {
        var request = Request("different-facts");
        await Assert.ThrowsAsync<InjectedCrashException>(() =>
            Handler(tasks: new CrashBeforeTaskRepository(_tasks)).Handle(Command(request), default));
        var instance = Assert.Single(await InstancesOfAsync(request));

        var otherApprover = await Handler().Handle(Command(request with { CandidatePrincipalIds = [D(OtherApprover)] }), default);
        var otherObject = await Handler().Handle(Command(request with { ObjectId = Guid.NewGuid().ToString("D"), ObjectRef = null }), default);

        Assert.All(new[] { otherApprover, otherObject }, response =>
        {
            Assert.Equal(409, response.StatusCode);
            Assert.Equal(WorkflowReasonCodes.WorkflowInstanceStartConflict, response.ReasonCode);
        });
        Assert.Empty(await _tasks.ListByInstanceIdAsync(instance.Id));
    }

    [Fact]
    public async Task Two_replays_at_once_write_one_task()
    {
        var request = Request("two-replays");
        var instance = await InsertHalfStartAsync(request, withIds: false); // the hardest case: no ids fixed yet
        var gate = new TwoReaderGate();

        var replies = await Task.WhenAll(
            Task.Run(() => Handler(tasks: new GatedTaskRepository(_tasks, gate)).Handle(Command(request), default)),
            Task.Run(() => Handler(tasks: new GatedTaskRepository(_tasks, gate)).Handle(Command(request), default)));

        Assert.True(gate.BothRead); // non-vacuity: both replays read "no task" before either wrote one
        Assert.All(replies, reply => Assert.True(reply.IsSuccessful, reply.ReasonCode));
        Assert.Single(replies.Select(r => r.Data!.ApprovalTaskId).Distinct());
        Assert.Single(await _tasks.ListByInstanceIdAsync(instance.Id));
        Assert.Single(await _snapshots.ListByInstanceIdAsync(instance.Id));
        Assert.Single(await _logs.ListByInstanceIdAsync(instance.Id));
    }

    // ── BL-549 rule 2 · the abandoned start is closed by the engine ──────────────────────────────────────────────

    [Fact]
    public async Task An_abandoned_start_is_closed_with_a_reason_a_log_a_completion_event_and_an_audit_row()
    {
        var request = Request("abandoned");
        var instance = await InsertHalfStartAsync(request, withIds: true);
        await AgeAsync(instance.Id);
        var metrics = new WorkflowStartRecoveryMetrics();

        await SweepAsync(metrics);

        var closed = (await _instances.GetByIdAsync(instance.Id))!;
        Assert.Equal(WorkflowInstanceStatus.Cancelled, closed.Status);
        Assert.Equal(WorkflowReasonCodes.WorkflowStartAbandoned, closed.StartClosedReasonCode);
        var log = Assert.Single(await _logs.ListByInstanceIdAsync(instance.Id));
        Assert.Equal(WorkflowTransitionAction.Cancel, log.Action);
        Assert.Equal(WorkflowReasonCodes.WorkflowStartAbandoned, log.ReasonCode);
        Assert.Equal(CloseAbandonedWorkflowStartResult.SystemActor, log.ActorId);
        Assert.Equal((nameof(WorkflowInstanceStatus.Active), nameof(WorkflowInstanceStatus.Cancelled)), (log.FromStatus, log.ToStatus));
        Assert.Null(log.ApprovalTaskId);
        Assert.Equal(1, await CompletionEventsAsync(instance.Id));
        var audit = Assert.Single(_audit.Requests, r => r.EntityId == instance.Id);
        Assert.Equal(nameof(CloseAbandonedWorkflowStartCommand), audit.RequestType);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal(AuditActorType.System, audit.ActorType);
        Assert.Equal((1L, 0L), (metrics.For(_tenant.TenantId).Abandoned, metrics.For(_tenant.TenantId).Stuck));

        // Idempotent: the next cycle finds nothing, a repeated close answers "already closed" and writes nothing.
        await SweepAsync(metrics);
        var repeat = await CloseHandler().Handle(new CloseAbandonedWorkflowStartCommand(instance.Id, Age, "corr-repeat"), default);
        Assert.True(repeat.IsSuccessful);
        Assert.True(repeat.Data!.AlreadyClosed);
        Assert.Single(await _logs.ListByInstanceIdAsync(instance.Id));
        Assert.Equal(0L, metrics.For(_tenant.TenantId).Abandoned);
    }

    [Fact]
    public async Task The_sweep_never_touches_a_start_with_a_task_nor_a_young_start()
    {
        var whole = await Handler().Handle(Command(Request("whole")), default);
        Assert.True(whole.IsSuccessful, whole.ReasonCode);
        await AgeAsync(whole.Data!.WorkflowInstanceId);
        var young = await InsertHalfStartAsync(Request("young"), withIds: true); // not aged
        var abandoned = await InsertHalfStartAsync(Request("old"), withIds: true);
        await AgeAsync(abandoned.Id);

        await SweepAsync(new WorkflowStartRecoveryMetrics());

        Assert.Equal(WorkflowInstanceStatus.Active, (await _instances.GetByIdAsync(whole.Data.WorkflowInstanceId))!.Status);
        Assert.Equal(WorkflowInstanceStatus.Active, (await _instances.GetByIdAsync(young.Id))!.Status);
        Assert.Equal(WorkflowInstanceStatus.Cancelled, (await _instances.GetByIdAsync(abandoned.Id))!.Status); // non-vacuity
        // Asked directly, the close refuses both and writes nothing.
        foreach (var id in new[] { whole.Data.WorkflowInstanceId, young.Id })
        {
            var refused = await CloseHandler().Handle(new CloseAbandonedWorkflowStartCommand(id, Age, "corr-refused"), default);
            Assert.Equal((409, WorkflowReasonCodes.WorkflowStartNotAbandoned), (refused.StatusCode, refused.ReasonCode));
        }
    }

    [Fact]
    public async Task Another_tenants_abandoned_start_is_neither_listed_nor_closed()
    {
        var foreignTenant = Guid.NewGuid();
        var foreign = await InsertHalfStartAsync(Request("foreign"), withIds: true, tenantId: foreignTenant);
        await AgeAsync(foreign.Id);

        var listed = (await _store.FindAbandonedGenericStartsAsync(DateTimeOffset.UtcNow - Age, 100, ScanLimit)).Page;
        var closed = await CloseHandler().Handle(new CloseAbandonedWorkflowStartCommand(foreign.Id, Age, "corr-foreign"), default);

        Assert.DoesNotContain(listed, i => i.Id == foreign.Id);
        Assert.Equal(404, closed.StatusCode);
        var stored = await _context.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances)
            .Find(i => i.Id == foreign.Id).SingleAsync();
        Assert.Equal(WorkflowInstanceStatus.Active, stored.Status);
    }

    [Fact]
    public async Task A_replay_after_the_close_does_not_revive_it()
    {
        var request = Request("revive");
        var instance = await InsertHalfStartAsync(request, withIds: true);
        await AgeAsync(instance.Id);
        await SweepAsync(new WorkflowStartRecoveryMetrics());

        var replay = await Handler().Handle(Command(request), default);

        Assert.Equal(409, replay.StatusCode);
        Assert.Empty(await _tasks.ListByInstanceIdAsync(instance.Id));
        Assert.Equal(WorkflowInstanceStatus.Cancelled, (await _instances.GetByIdAsync(instance.Id))!.Status);
    }

    [Fact]
    public async Task A_replay_that_crashed_before_its_task_leaves_nothing_behind_and_the_old_start_is_closed()
    {
        // FIX2 item 1 — the replay's claim and its task commit together: a crash before the task rolls the claim back, so the
        // instance is exactly what it was (same version, same ids, same age) — an abandoned start the sweep may close. A replay
        // after the close is refused and writes nothing (the callers then start a new round).
        var request = Request("claimed");
        var instance = await InsertHalfStartAsync(request, withIds: false);
        await AgeAsync(instance.Id);
        await Assert.ThrowsAsync<InjectedCrashException>(() =>
            Handler(tasks: new CrashBeforeTaskRepository(_tasks)).Handle(Command(request), default));
        var after = (await _instances.GetByIdAsync(instance.Id))!;
        Assert.Equal((instance.Version, (Guid?)null), (after.Version, after.InitialApprovalTaskId)); // nothing committed

        await SweepAsync(new WorkflowStartRecoveryMetrics());

        Assert.Equal(WorkflowInstanceStatus.Cancelled, (await _instances.GetByIdAsync(instance.Id))!.Status);
        var replay = await Handler().Handle(Command(request), default);
        Assert.Equal(409, replay.StatusCode);
        Assert.Empty(await _tasks.ListByInstanceIdAsync(instance.Id));
    }

    [Fact]
    public async Task The_task_approval_stuck_on_an_abandoned_start_starts_a_new_round_once_it_is_closed()
    {
        var task = new TaskItem
        {
            TenantId = _tenant.TenantId,
            Title = "Approval-gated work",
            AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
            OrganizationUnitId = Guid.NewGuid(),
            ApprovalRequired = true,
            ApprovalManagerUserId = Approver
        };
        var approvals = ApprovalService(crashBeforeTask: true);
        await approvals.TryStartApprovalAsync(task, default); // the first start crashes after its instance
        var half = (await _instances.GetLatestByObjectRefAsync(
            TaskApprovalService.BuildObjectRef(task.Id), TaskApprovalService.ApprovalObjectType, task.Id.ToString()))!;
        Assert.Empty(await _tasks.ListByInstanceIdAsync(half.Id));

        // Nobody asks again before the recovery closes it (FIX1 item 3 covers the asking-again case).
        await AgeAsync(half.Id);
        await SweepAsync(new WorkflowStartRecoveryMetrics());
        var renewed = await ApprovalService().TryStartApprovalAsync(task, default);

        Assert.NotNull(renewed);
        Assert.NotEqual(half.Id, renewed);
        Assert.Single(await _tasks.ListByInstanceIdAsync(renewed!.Value), t => t.Status == ApprovalTaskStatus.WaitingApproval);
    }

    // ── rule 3 · the gauges ──────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_gauges_count_abandoned_starts_and_incomplete_trusted_starts_and_only_the_old_ones_are_closed()
    {
        var abandoned = await InsertHalfStartAsync(Request("gauge"), withIds: true);
        await AgeAsync(abandoned.Id);
        var trusted = await InsertIncompleteTrustedStartAsync(reservedAgo: TimeSpan.FromHours(2));
        var youngTrusted = await InsertIncompleteTrustedStartAsync(reservedAgo: TimeSpan.FromMinutes(1));
        using var metrics = new WorkflowStartRecoveryMetrics();
        var observed = new Dictionary<string, long>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == WorkflowStartRecoveryMetrics.MeterName)
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "tenant" && (string?)tag.Value == _tenant.TenantId.ToString("D"))
                {
                    observed[instrument.Name] = value;
                }
            }
        });
        listener.Start();

        await SweepAsync(metrics);
        listener.RecordObservableInstruments();

        Assert.Equal(1, observed["platform.workflow.start_recovery.abandoned"]);
        Assert.Equal(0, observed["platform.workflow.start_recovery.stuck"]);
        Assert.Equal(1, observed["platform.workflow.start_recovery.incomplete_trusted"]);
        Assert.Equal(WorkflowInstanceStatus.Cancelled, (await _instances.GetByIdAsync(trusted))!.Status); // CT decision: closed
        Assert.Equal(WorkflowInstanceStatus.Pending, (await _instances.GetByIdAsync(youngTrusted))!.Status);
    }

    // ── BL-540 (1) · the orphan recovery ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_orphan_recovery_lists_by_default_and_closes_only_orphans_when_told_to()
    {
        // An approval whose task was deleted, a review round a later round replaced, and a healthy approval.
        var deletedTask = Guid.NewGuid();
        var deleted = await StartForAsync(TaskApprovalService.ApprovalObjectType, deletedTask, "orphan-deleted");

        var reviewed = await SaveTaskItemAsync();
        var roundOne = await StartForAsync(TaskReviewService.ReviewObjectType, reviewed.Id, "orphan-review-1");
        var roundTwo = await StartForAsync(TaskReviewService.ReviewObjectType, reviewed.Id, "orphan-review-2", startedLater: true);
        reviewed.ReviewWorkflowInstanceId = roundTwo;
        Assert.True(await _taskItems.UpdateAsync(reviewed, reviewed.Version));

        // A review whose LATEST round is not linked yet (a start whose link write is still to come): adoptable, not an orphan.
        var unlinked = await SaveTaskItemAsync();
        var unlinkedLatest = await StartForAsync(TaskReviewService.ReviewObjectType, unlinked.Id, "unlinked-latest");

        var healthy = await SaveTaskItemAsync();
        var healthyApproval = await StartForAsync(TaskApprovalService.ApprovalObjectType, healthy.Id, "healthy");
        healthy.WorkflowInstanceId = healthyApproval;
        Assert.True(await _taskItems.UpdateAsync(healthy, healthy.Version));

        var dryRun = await OrphanHandler().Handle(new SweepWorkflowOrphansCommand(Apply: false, 100, "corr-dry"), default);

        Assert.True(dryRun.IsSuccessful, dryRun.ReasonCode);
        Assert.True(dryRun.Data!.DryRun);
        Assert.Equal(
            new[] { (deleted, SweepWorkflowOrphansCommand.TaskDeleted), (roundOne, SweepWorkflowOrphansCommand.ReviewRoundReplaced) }
                .OrderBy(x => x.Item1),
            dryRun.Data.Items.Select(i => (i.WorkflowInstanceId, i.Reason)).OrderBy(x => x.Item1));
        Assert.All(dryRun.Data.Items, i => Assert.Null(i.Outcome));
        foreach (var id in new[] { deleted, roundOne, roundTwo, unlinkedLatest, healthyApproval })
        {
            Assert.Equal(WorkflowInstanceStatus.Active, (await _instances.GetByIdAsync(id))!.Status); // the dry run wrote nothing
        }

        var applied = await OrphanHandler().Handle(new SweepWorkflowOrphansCommand(Apply: true, 100, "corr-apply"), default);

        Assert.Equal(2, applied.Data!.Closed);
        Assert.All(applied.Data.Items, i => Assert.Equal("CLOSED", i.Outcome));
        Assert.Equal(WorkflowInstanceStatus.Cancelled, (await _instances.GetByIdAsync(deleted))!.Status);
        Assert.Equal(WorkflowInstanceStatus.Cancelled, (await _instances.GetByIdAsync(roundOne))!.Status);
        Assert.Equal(WorkflowInstanceStatus.Active, (await _instances.GetByIdAsync(roundTwo))!.Status);
        Assert.Equal(WorkflowInstanceStatus.Active, (await _instances.GetByIdAsync(healthyApproval))!.Status);
        Assert.Equal(WorkflowInstanceStatus.Active, (await _instances.GetByIdAsync(unlinkedLatest))!.Status);
        var closing = Assert.Single(await _logs.ListByInstanceIdAsync(deleted), l => l.Action == WorkflowTransitionAction.Cancel);
        Assert.Equal((SweepWorkflowOrphansCommand.SystemActor, WorkflowReasonCodes.WorkflowOrphanClosed), (closing.ActorId, closing.ReasonCode));
        Assert.Contains(_audit.Requests, r => r.RequestType == nameof(CancelWorkflowTaskCommand));
    }

    // ── FIX1 item 1 (CT decision D) · the operator's one-shot command ───────────────────────────────────────────

    private async Task<(Guid Orphan, Guid Healthy)> OrphanAndHealthyAsync()
    {
        var orphan = await StartForAsync(TaskApprovalService.ApprovalObjectType, Guid.NewGuid(), "cmd-orphan"); // its task is gone
        var healthyTask = await SaveTaskItemAsync();
        var healthy = await StartForAsync(TaskApprovalService.ApprovalObjectType, healthyTask.Id, "cmd-healthy");
        healthyTask.WorkflowInstanceId = healthy;
        Assert.True(await _taskItems.UpdateAsync(healthyTask, healthyTask.Version));
        return (orphan, healthy);
    }

    private Func<IServiceProvider> OrphanCommandServices()
    {
        var scope = new TenantContext(); // the command sets the tenant on it
        return () =>
        {
            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<ITenantContext>(services, scope);
            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<IMediator>(services, new RecoveryMediator(this, scope: scope));
            return Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(services);
        };
    }

    private async Task<(int Exit, string Output, string Error)> RunOrphanCommandAsync(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = await WorkflowOrphanRecoveryCommandLine.RunAsync(
            [WorkflowOrphanRecoveryCommandLine.Argument, .. args], OrphanCommandServices(), output, error, "test-host");
        return (exit, output.ToString(), error.ToString());
    }

    [Fact]
    public async Task The_orphan_command_lists_by_default_and_records_who_ran_it()
    {
        var (orphan, healthy) = await OrphanAndHealthyAsync();

        var (exit, output, _) = await RunOrphanCommandAsync("--tenant", _tenant.TenantId.ToString());

        Assert.Equal(0, exit);
        Assert.Contains(orphan.ToString("D"), output);
        Assert.DoesNotContain(healthy.ToString("D"), output);
        Assert.Equal(WorkflowInstanceStatus.Active, (await _instances.GetByIdAsync(orphan))!.Status); // nothing written
        var audit = Assert.Single(_audit.Requests, r => r.RequestType == nameof(SweepWorkflowOrphansCommand));
        Assert.Equal(AuditActorType.System, audit.ActorType);
        Assert.Equal(WorkflowOrphanRecoveryCommandLine.ToolName, audit.Metadata["Tool"]);
        Assert.Equal("test-host", audit.Metadata["Host"]);
    }

    [Fact]
    public async Task The_orphan_command_closes_only_orphans_with_apply()
    {
        var (orphan, healthy) = await OrphanAndHealthyAsync();

        var (exit, output, _) = await RunOrphanCommandAsync("--tenant", _tenant.TenantId.ToString(), "--apply", "--expect", "1");

        Assert.Equal(0, exit);
        Assert.Contains("APPLIED — 1 of 1", output);
        Assert.Equal(WorkflowInstanceStatus.Cancelled, (await _instances.GetByIdAsync(orphan))!.Status);
        Assert.Equal(WorkflowInstanceStatus.Active, (await _instances.GetByIdAsync(healthy))!.Status);
    }

    [Fact]
    public async Task The_orphan_command_refuses_without_a_tenant_and_writes_nothing_on_a_wrong_count()
    {
        var (orphan, _) = await OrphanAndHealthyAsync();

        Assert.Equal(2, (await RunOrphanCommandAsync("--apply")).Exit);
        var (exit, _, error) = await RunOrphanCommandAsync("--tenant", _tenant.TenantId.ToString(), "--apply", "--expect", "5");

        Assert.Equal(5, exit);
        Assert.Contains("nothing was closed", error);
        Assert.Equal(WorkflowInstanceStatus.Active, (await _instances.GetByIdAsync(orphan))!.Status);
    }

    [Fact]
    public async Task The_orphan_command_never_touches_another_tenants_orphan()
    {
        var otherTenant = Guid.NewGuid();
        var theirs = new WorkflowInstance
        {
            TenantId = otherTenant, TemplateId = _templateId, WorkflowTemplateId = _templateId,
            ObjectType = TaskApprovalService.ApprovalObjectType, ObjectId = Guid.NewGuid().ToString(),
            ObjectRef = "tasks|task|" + Guid.NewGuid(), Status = WorkflowInstanceStatus.Active, StartedAt = DateTimeOffset.UtcNow
        };
        await _context.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances).InsertOneAsync(theirs);
        await OrphanAndHealthyAsync();

        var (exit, output, _) = await RunOrphanCommandAsync("--tenant", _tenant.TenantId.ToString(), "--apply");

        Assert.Equal(0, exit);
        Assert.DoesNotContain(theirs.Id.ToString("D"), output);
        Assert.Equal(WorkflowInstanceStatus.Active,
            (await _context.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances).Find(i => i.Id == theirs.Id).SingleAsync()).Status);
    }

    [Fact]
    public async Task The_orphan_command_process_never_prints_a_broken_connection_string()
    {
        // As a process: the API host's own one-shot mode, configured from the environment as an operator configures it.
        var marker = string.Concat("do-not", "-print-", "orphans");
        var secret = string.Concat("mongodb", "://", "user", ":", "pw", "@", marker, ":", "99999999", "/");
        Assert.Contains(marker, Record.Exception(() => MongoClientSettings.FromConnectionString(secret))?.Message ?? string.Empty,
            StringComparison.OrdinalIgnoreCase); // measured: the driver's own message quotes it
        var dll = Path.Combine(AppContext.BaseDirectory, "Diten.Platform.API.dll");
        Assert.True(File.Exists(dll), dll);
        var start = new System.Diagnostics.ProcessStartInfo("dotnet",
            $"\"{dll}\" {WorkflowOrphanRecoveryCommandLine.Argument} --tenant {Guid.NewGuid():D}")
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory
        };
        // Measured (FIX1 sabotage X1c): without the host's required secrets the process stops at secret validation and never
        // reaches the connection string. Throwaway values, made here per run, let it get to the driver's parse.
        static string Throwaway() => Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        start.Environment["JwtSettings__Secret"] = Throwaway();
        start.Environment["AuthService__InternalApiKey"] = Throwaway();
        start.Environment["ModuleRegistrationCredentials__Mdm__ActiveSecret"] = Throwaway();
        start.Environment["MongoDbSettings__ConnectionString"] = secret;
        start.Environment["MongoDbSettings__DatabaseName"] = "orphan_command_process";
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        using var process = System.Diagnostics.Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(120_000));

        Assert.Equal(1, process.ExitCode);
        Assert.DoesNotContain(marker, await stdout, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(marker, await stderr, StringComparison.OrdinalIgnoreCase);
        // The run reached the connection string: it failed at the driver's parse, not at secret validation.
        Assert.Contains("setup failure (MongoConfigurationException)", await stderr); // FIX3 K13 — told apart from a run failure
        Assert.Contains("nothing was read or closed", await stderr);
    }

    // ── BL-539 (CT decision) · the incomplete trusted reservation ───────────────────────────────────────────────

    [Fact]
    public async Task An_incomplete_trusted_reservation_is_closed_and_its_key_then_reads_exactly_as_one_never_seen()
    {
        var (request, _) = await CrashedReservationAsync("trusted-reserved", tasks: new CrashBeforeTaskRepository(_tasks));
        var reservation = Assert.Single(await InstancesOfKeyAsync(request.IdempotencyKey));
        Assert.Equal((WorkflowInstanceStatus.Pending, WorkflowStartCheckpoint.Reserved), (reservation.Status, reservation.StartCheckpoint));
        await AgeReservationAsync(reservation.Id);

        await SweepAsync(new WorkflowStartRecoveryMetrics());

        var closed = (await _instances.GetByIdAsync(reservation.Id))!;
        Assert.Equal((WorkflowInstanceStatus.Cancelled, WorkflowReasonCodes.WorkflowStartAbandoned), (closed.Status, closed.StartClosedReasonCode));
        var log = Assert.Single(await _logs.ListByInstanceIdAsync(reservation.Id));
        Assert.Equal((WorkflowTransitionAction.Cancel, CloseAbandonedWorkflowStartResult.SystemActor), (log.Action, log.ActorId));
        Assert.Contains(_audit.Requests, r => r.EntityId == reservation.Id && r.RequestType == nameof(CloseAbandonedWorkflowStartCommand));

        // The closed key and a key the engine never saw: the same answer, byte for byte (status, code, body).
        var closedKey = await StartResultAsync(request.IdempotencyKey, request.ObjectId);
        var unknownKey = await StartResultAsync("never-seen:" + Guid.NewGuid().ToString("N"), request.ObjectId);
        Assert.Equal(404, closedKey.StatusCode);
        Assert.Equal(Wire(unknownKey), Wire(closedKey));
        // A replay of the start answers the same, and is not brought back.
        var replay = await TrustedCoordinator().StartAsync(request, TrustedClient, Maker, "corr-wire");
        Assert.Equal(Wire(unknownKey), Wire(replay));
        Assert.Equal(WorkflowInstanceStatus.Cancelled, (await _instances.GetByIdAsync(reservation.Id))!.Status);
        Assert.Empty(await _tasks.ListByInstanceIdAsync(reservation.Id));
    }

    [Fact]
    public async Task A_reservation_whose_task_was_written_shows_it_but_refuses_a_decision_and_the_close_withdraws_it()
    {
        var (request, _) = await CrashedReservationAsync("trusted-task-written", logs: new CrashBeforeLogRepository(_logs));
        var reservation = Assert.Single(await InstancesOfKeyAsync(request.IdempotencyKey));
        Assert.Equal(WorkflowStartCheckpoint.AssignmentSnapshotPersisted, reservation.StartCheckpoint);
        var task = Assert.Single(await _tasks.ListByInstanceIdAsync(reservation.Id));

        // Measured (CT rule 5): the task is LISTED to its candidate — and every decision on it is refused.
        Assert.Contains(await MyWorkflowTasksAsync(Approver), t => t.Id == task.Id);
        var approve = await new ApproveWorkflowTaskHandler(_tasks, _instances, _snapshots, _logs)
            .Handle(new ApproveWorkflowTaskCommand(task.Id, new ApproveWorkflowTaskRequest(D(Approver), "APPROVED", "try-approve", null, null), "corr-approve"), default);
        Assert.Equal((409, WorkflowReasonCodes.WorkflowStartNotCompleted), (approve.StatusCode, approve.ReasonCode));

        await AgeReservationAsync(reservation.Id);
        await SweepAsync(new WorkflowStartRecoveryMetrics());

        Assert.Equal(WorkflowInstanceStatus.Cancelled, (await _instances.GetByIdAsync(reservation.Id))!.Status);
        var withdrawn = (await _tasks.GetByIdAsync(task.Id))!;
        Assert.Equal((ApprovalTaskStatus.Cancelled, WorkflowReasonCodes.WorkflowStartAbandoned), (withdrawn.Status, withdrawn.ActionReasonCode));
        Assert.DoesNotContain(await MyWorkflowTasksAsync(Approver), t => t.Id == task.Id);
        var logs = await _logs.ListByInstanceIdAsync(reservation.Id);
        Assert.Single(logs, l => l.ApprovalTaskId == task.Id && l.Action == WorkflowTransitionAction.Cancel); // the withdraw
        Assert.Single(logs, l => l.ApprovalTaskId == null && l.ReasonCode == WorkflowReasonCodes.WorkflowStartAbandoned); // the close
    }

    [Fact]
    public async Task A_reservation_whose_start_task_somebody_moved_is_not_closed()
    {
        var (request, _) = await CrashedReservationAsync("trusted-moved", logs: new CrashBeforeLogRepository(_logs));
        var reservation = Assert.Single(await InstancesOfKeyAsync(request.IdempotencyKey));
        var task = Assert.Single(await _tasks.ListByInstanceIdAsync(reservation.Id));
        await _context.Database.GetCollection<ApprovalTask>(PlatformCollections.ApprovalTasks).UpdateOneAsync(
            t => t.Id == task.Id, Builders<ApprovalTask>.Update.Set(t => t.Status, ApprovalTaskStatus.Approved));
        await AgeReservationAsync(reservation.Id);

        await SweepAsync(new WorkflowStartRecoveryMetrics());
        var direct = await CloseHandler().Handle(new CloseAbandonedWorkflowStartCommand(reservation.Id, Age, "corr-moved"), default);

        Assert.Equal((409, WorkflowReasonCodes.WorkflowStartNotAbandoned), (direct.StatusCode, direct.ReasonCode));
        Assert.Equal(WorkflowInstanceStatus.Pending, (await _instances.GetByIdAsync(reservation.Id))!.Status);
        Assert.Equal(ApprovalTaskStatus.Approved, (await _tasks.GetByIdAsync(task.Id))!.Status);
    }

    [Fact]
    public async Task A_replay_that_completes_first_wins_and_the_close_held_behind_it_writes_nothing()
    {
        var (request, _) = await CrashedReservationAsync("race-completion-first", snapshots: new CrashBeforeSnapshotRepository(_snapshots));
        var reservation = Assert.Single(await InstancesOfKeyAsync(request.IdempotencyKey));
        Assert.Equal(WorkflowStartCheckpoint.TaskPersisted, reservation.StartCheckpoint);
        await AgeReservationAsync(reservation.Id);
        var hold = new Hold();

        // The close reads the reservation, then is held (reading its start task); the replay completes meanwhile.
        var close = Task.Run(() => CloseHandler(tasks: new HoldingTaskRepository(_tasks, hold))
            .Handle(new CloseAbandonedWorkflowStartCommand(reservation.Id, Age, "corr-race-a"), default));
        await hold.ReachedAsync();
        var replay = await TrustedCoordinator().StartAsync(request, TrustedClient, Maker, "corr-race-a-replay");
        hold.Release();
        var closed = await close;

        Assert.True(replay.IsSuccessful, replay.ReasonCode);
        Assert.Equal((409, WorkflowReasonCodes.WorkflowTransitionConflict), (closed.StatusCode, closed.ReasonCode));
        var completed = (await _instances.GetByIdAsync(reservation.Id))!;
        Assert.Equal((WorkflowInstanceStatus.Active, WorkflowStartCheckpoint.Completed), (completed.Status, completed.StartCheckpoint));
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, (await _tasks.GetByIdAsync(completed.InitialApprovalTaskId!.Value))!.Status);
    }

    [Fact]
    public async Task A_replay_that_moved_the_reservation_on_meanwhile_beats_the_close_even_without_completing_it()
    {
        // The close read the reservation at TaskPersisted; a replay then advanced it a step (snapshot written) and crashed
        // before its start log. Still Pending — only the checkpoint fence tells the close it lost.
        var (request, _) = await CrashedReservationAsync("race-advanced", snapshots: new CrashBeforeSnapshotRepository(_snapshots));
        var reservation = Assert.Single(await InstancesOfKeyAsync(request.IdempotencyKey));
        await AgeReservationAsync(reservation.Id);
        var hold = new Hold();

        var close = Task.Run(() => CloseHandler(tasks: new HoldingTaskRepository(_tasks, hold))
            .Handle(new CloseAbandonedWorkflowStartCommand(reservation.Id, Age, "corr-race-c"), default));
        await hold.ReachedAsync();
        await Assert.ThrowsAsync<InjectedCrashException>(() =>
            TrustedCoordinator(logs: new CrashBeforeLogRepository(_logs)).StartAsync(request, TrustedClient, Maker, "corr-race-c-replay"));
        hold.Release();
        var closed = await close;

        Assert.Equal((409, WorkflowReasonCodes.WorkflowTransitionConflict), (closed.StatusCode, closed.ReasonCode));
        var after = (await _instances.GetByIdAsync(reservation.Id))!;
        Assert.Equal((WorkflowInstanceStatus.Pending, WorkflowStartCheckpoint.AssignmentSnapshotPersisted), (after.Status, after.StartCheckpoint));
        Assert.True((await TrustedCoordinator().StartAsync(request, TrustedClient, Maker, "corr-race-c-finish")).IsSuccessful);
    }

    [Fact]
    public async Task A_close_that_lands_while_a_replay_is_held_wins_and_the_replay_reads_the_key_as_never_seen()
    {
        var (request, _) = await CrashedReservationAsync("race-close-first", snapshots: new CrashBeforeSnapshotRepository(_snapshots));
        var reservation = Assert.Single(await InstancesOfKeyAsync(request.IdempotencyKey));
        await AgeReservationAsync(reservation.Id);
        var hold = new Hold();

        // The replay reads the reservation and is held (reading its snapshot); the recovery closes it meanwhile.
        var replay = Task.Run(() => TrustedCoordinator(snapshots: new HoldingSnapshotRepository(_snapshots, hold))
            .StartAsync(request, TrustedClient, Maker, "corr-race-b"));
        await hold.ReachedAsync();
        var closed = await CloseHandler().Handle(new CloseAbandonedWorkflowStartCommand(reservation.Id, Age, "corr-race-b-close"), default);
        hold.Release();
        var answer = await replay;

        Assert.True(closed.IsSuccessful, closed.ReasonCode);
        Assert.Equal((404, WorkflowReasonCodes.NotFoundNonLeakage), (answer.StatusCode, answer.ReasonCode));
        var after = (await _instances.GetByIdAsync(reservation.Id))!;
        Assert.Equal(WorkflowInstanceStatus.Cancelled, after.Status);
        Assert.NotEqual(WorkflowStartCheckpoint.Completed, after.StartCheckpoint);
        Assert.All(await _tasks.ListByInstanceIdAsync(reservation.Id), t => Assert.Equal(ApprovalTaskStatus.Cancelled, t.Status));
    }

    [Fact]
    public async Task A_closed_reservation_is_never_advanced_even_at_its_own_version_and_checkpoint()
    {
        // CT rule 4 — completion only while Pending. Every close today also moves the version (which alone already stops a
        // stale replay); this holds the rule on its own, for a close that would not.
        var (request, _) = await CrashedReservationAsync("closed-no-advance", tasks: new CrashBeforeTaskRepository(_tasks));
        var reservation = Assert.Single(await InstancesOfKeyAsync(request.IdempotencyKey));
        await _context.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances).UpdateOneAsync(
            i => i.Id == reservation.Id, Builders<WorkflowInstance>.Update.Set(i => i.Status, WorkflowInstanceStatus.Cancelled));

        var advanced = await _instances.AdvanceStartCheckpointAsync(
            reservation.Id, reservation.Version, WorkflowStartCheckpoint.Reserved, WorkflowStartCheckpoint.TaskPersisted);

        Assert.False(advanced);
        Assert.Equal(WorkflowStartCheckpoint.Reserved, (await _instances.GetByIdAsync(reservation.Id))!.StartCheckpoint);
    }

    // ── BL-539 item 4 (CT decision A) · the consumer's own cancel of an incomplete start, on today's contract ──────

    [Fact]
    public async Task A_consumer_cancels_an_incomplete_start_by_replaying_it_then_preflight_then_cancel()
    {
        var (request, _) = await CrashedReservationAsync("consumer-cancel", tasks: new CrashBeforeTaskRepository(_tasks));
        Assert.Equal(409, (await StartResultAsync(request.IdempotencyKey, request.ObjectId)).StatusCode); // incomplete: no ids

        var replay = await TrustedCoordinator().StartAsync(request, TrustedClient, Maker, "corr-cancel-replay"); // 1. completes
        Assert.True(replay.IsSuccessful, replay.ReasonCode);
        // ── the window: from here until the cancel below, the task is live and listed to its candidate ──
        Assert.Contains(await MyWorkflowTasksAsync(Approver), t => t.Id == replay.Data!.ApprovalTaskId);
        var cancellations = new TrustedWorkflowCancellationCoordinator(_instances, _tasks, _logs, new PlatformTransactionExecutor(_context));
        var preflight = await cancellations.PreflightAsync(TrustedClient, replay.Data!.WorkflowInstanceId, replay.Data.ApprovalTaskId,
            request.ObjectType, request.ObjectId, Maker, "corr-cancel-preflight", default); // 2. preflight
        Assert.True(preflight.IsSuccessful, preflight.ReasonCode);
        var cancel = await cancellations.CancelAsync(TrustedClient, Maker, new TrustedWorkflowCancellationRequest(
            replay.Data.WorkflowInstanceId, replay.Data.ApprovalTaskId, request.ObjectType, request.ObjectId, Maker,
            preflight.Data!.WorkflowInstanceVersion, preflight.Data.ApprovalTaskVersion, "WITHDRAWN", null, "cancel-1"), "corr-cancel", default); // 3. cancel

        Assert.True(cancel.IsSuccessful, cancel.ReasonCode);
        var evidence = await new Diten.Platform.Application.Features.Workflow.Handlers.QueryHandlers.GetTrustedWorkflowTerminalDecisionEvidenceHandler(
                _instances, _tasks, _logs)
            .Handle(new Diten.Platform.Application.Features.Workflow.Queries.GetTrustedWorkflowTerminalDecisionEvidenceQuery(
                replay.Data.WorkflowInstanceId, TrustedClient, request.ObjectType, request.ObjectId, "corr-evidence"), default);
        Assert.Equal((409, WorkflowReasonCodes.WorkflowInstanceClosedWithoutDecision), (evidence.StatusCode, evidence.ReasonCode));
    }

    // ══ FIX1 ═════════════════════════════════════════════════════════════════════════════════════════════════════

    // ── item 2 · the recovery and a first call that stopped between its instance and its task ────────────────────

    [Fact]
    public async Task A_first_call_resumed_after_the_recovery_closed_its_instance_writes_no_task()
    {
        var request = Request("paused-first-call");
        var hold = new Hold();
        var start = Task.Run(() => Handler(instances: new HoldAfterReserveRepository(_instances, hold)).Handle(Command(request), default));
        await hold.ReachedAsync(); // the instance is written; the call is paused before its task
        var instance = Assert.Single(await InstancesOfAsync(request));
        await AgeAsync(instance.Id);
        var closed = await CloseHandler().Handle(new CloseAbandonedWorkflowStartCommand(instance.Id, Age, "corr-paused"), default);
        Assert.True(closed.IsSuccessful, closed.ReasonCode);

        hold.Release();
        var answer = await start;

        Assert.Equal((409, WorkflowReasonCodes.WorkflowInstanceStartConflict), (answer.StatusCode, answer.ReasonCode));
        Assert.Equal(WorkflowInstanceStatus.Cancelled, (await _instances.GetByIdAsync(instance.Id))!.Status);
        Assert.Empty(await _tasks.ListByInstanceIdAsync(instance.Id)); // never Cancelled + an open task
    }

    [Fact]
    public async Task A_recovery_that_read_before_the_first_call_wrote_its_task_writes_nothing()
    {
        var request = Request("paused-recovery");
        var startHold = new Hold();
        var start = Task.Run(() => Handler(instances: new HoldAfterReserveRepository(_instances, startHold)).Handle(Command(request), default));
        await startHold.ReachedAsync();
        var instance = Assert.Single(await InstancesOfAsync(request));
        await AgeAsync(instance.Id);
        var closeHold = new Hold();
        // The recovery reads the instance and its (no) tasks, and is held there; the first call resumes and writes its task.
        var close = Task.Run(() => CloseHandler(taskList: new HoldingTaskListRepository(_tasks, closeHold))
            .Handle(new CloseAbandonedWorkflowStartCommand(instance.Id, Age, "corr-paused-recovery"), default));
        await closeHold.ReachedAsync();
        startHold.Release();
        var started = await start;
        closeHold.Release();
        var closed = await close;

        Assert.True(started.IsSuccessful, started.ReasonCode);
        Assert.Equal((409, WorkflowReasonCodes.WorkflowTransitionConflict), (closed.StatusCode, closed.ReasonCode));
        Assert.Equal(WorkflowInstanceStatus.Active, (await _instances.GetByIdAsync(instance.Id))!.Status);
        Assert.Single(await _tasks.ListByInstanceIdAsync(instance.Id), t => t.Status == ApprovalTaskStatus.WaitingApproval);
    }

    // ── item 3 · the callers complete a half start by replaying the round's key ─────────────────────────────────

    [Fact]
    public async Task A_task_approval_on_a_half_start_completes_it_by_replaying_the_rounds_key()
    {
        var task = new TaskItem
        {
            TenantId = _tenant.TenantId, Title = "Approval-gated work", AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
            OrganizationUnitId = Guid.NewGuid(), ApprovalRequired = true, ApprovalManagerUserId = Approver
        };
        await ApprovalService(crashBeforeTask: true).TryStartApprovalAsync(task, default);
        var half = (await _instances.GetLatestByObjectRefAsync(
            TaskApprovalService.BuildObjectRef(task.Id), TaskApprovalService.ApprovalObjectType, task.Id.ToString()))!;
        Assert.Empty(await _tasks.ListByInstanceIdAsync(half.Id)); // non-vacuity

        var linked = await ApprovalService().TryStartApprovalAsync(task, default);

        Assert.Equal(half.Id, linked);
        Assert.Single(await _tasks.ListByInstanceIdAsync(half.Id), t => t.Status == ApprovalTaskStatus.WaitingApproval);
        Assert.Single(await _context.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances)
            .Find(i => i.ObjectId == task.Id.ToString()).ToListAsync()); // completed, not replaced
    }

    [Fact]
    public async Task A_task_review_on_a_half_start_completes_it_by_replaying_the_rounds_key()
    {
        var task = new TaskItem
        {
            TenantId = _tenant.TenantId, Title = "Review-gated work", AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
            OrganizationUnitId = Guid.NewGuid(), ReviewRequired = true, ReviewerCandidateUserId = Approver
        };
        await ReviewService(crashBeforeTask: true).StartReviewAsync(task, default);
        var half = (await _instances.GetLatestByObjectRefAsync(
            TaskReviewService.BuildObjectRef(task.Id), TaskReviewService.ReviewObjectType, task.Id.ToString()))!;
        Assert.Empty(await _tasks.ListByInstanceIdAsync(half.Id)); // non-vacuity

        var started = await ReviewService().StartReviewAsync(task, default);

        Assert.Equal(half.Id, started.InstanceId);
        Assert.Single(await _tasks.ListByInstanceIdAsync(half.Id), t => t.Status == ApprovalTaskStatus.WaitingApproval);
    }

    // ── item 4 · the gauges while the job is off; only a real failure is "stuck" ────────────────────────────────

    [Fact]
    public async Task With_the_job_off_the_count_only_cycle_feeds_the_gauges_and_closes_nothing()
    {
        var abandoned = await InsertHalfStartAsync(Request("count-only"), withIds: true);
        await AgeAsync(abandoned.Id);
        var metrics = new WorkflowStartRecoveryMetrics();
        var logs = new JobLog();

        await Job(metrics, new RecoveryMediator(this), logs).CountOnlyAsync("corr-count");

        Assert.True(metrics.For(_tenant.TenantId).Abandoned > 0);
        Assert.Equal(WorkflowInstanceStatus.Active, (await _instances.GetByIdAsync(abandoned.Id))!.Status);
        Assert.Contains(logs.Lines, l => l.Contains("Mode=count-only", StringComparison.Ordinal));
        Assert.False(WorkflowStartRecoveryCountingService.SweepJobRuns(new BackgroundJobSchedulerOptions())); // off by default
        Assert.True(WorkflowStartRecoveryCountingService.SweepJobRuns(new BackgroundJobSchedulerOptions
        {
            Enabled = true, RegisterStandardJobs = true,
            EnabledJobs = { [WorkflowStartRecoveryCountingService.SweepJobId] = true }
        }));
    }

    [Fact]
    public async Task Only_a_real_failure_is_stuck_named_once_then_quietly()
    {
        var abandoned = await InsertHalfStartAsync(Request("stuck"), withIds: true);
        await AgeAsync(abandoned.Id);
        var metrics = new WorkflowStartRecoveryMetrics();
        var logs = new JobLog();
        var failing = new AnsweringMediator(Response<CloseAbandonedWorkflowStartResult>.Fail("broken", 500, "SOMETHING_BROKE", "corr"));

        await Job(metrics, failing, logs).HandleAsync(new WorkflowStartRecoverySweepJobArgs(0), JobContext());
        await Job(metrics, failing, logs).HandleAsync(new WorkflowStartRecoverySweepJobArgs(0), JobContext());

        Assert.Equal(1L, metrics.For(_tenant.TenantId).Stuck);
        Assert.Single(logs.Errors, l => l.Contains("workflow.start_recovery.stuck", StringComparison.Ordinal));
        Assert.Contains(logs.Lines, l => l.Contains("still_stuck", StringComparison.Ordinal));
        Assert.Contains(logs.Lines, l => l.Contains("workflow.start_recovery.cycle", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(409, WorkflowReasonCodes.WorkflowTransitionConflict)]
    [InlineData(503, WorkflowReasonCodes.WorkflowTransitionConflict)]
    public async Task A_lost_race_or_an_unavailable_store_is_not_stuck(int status, string code)
    {
        var abandoned = await InsertHalfStartAsync(Request("not-stuck"), withIds: true);
        await AgeAsync(abandoned.Id);
        var metrics = new WorkflowStartRecoveryMetrics();
        var logs = new JobLog();

        await Job(metrics, new AnsweringMediator(Response<CloseAbandonedWorkflowStartResult>.Fail("no", status, code, "corr")), logs)
            .HandleAsync(new WorkflowStartRecoverySweepJobArgs(0), JobContext());

        Assert.Equal(0L, metrics.For(_tenant.TenantId).Stuck);
        Assert.Empty(logs.Errors);
    }

    // ── item 5 · the orphan scan walks every page ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_orphan_scan_finds_an_orphan_beyond_the_first_page_and_says_when_it_stopped_early()
    {
        // FIX2 K3 — fixed, ordered ids (random ids put an orphan on the first page about one run in twenty-one): five healthy
        // approvals first, then the two orphans, so with pages of two the orphans sit on pages three and four.
        var prefix = Guid.NewGuid().ToString("N")[..8];
        Guid Ordered(int n) => Guid.Parse($"{prefix}-0000-4000-8000-{n:D12}");
        for (var i = 1; i <= 5; i++)
        {
            var healthy = await SaveTaskItemAsync();
            healthy.WorkflowInstanceId = await InsertOpenApprovalAsync(Ordered(i), healthy.Id);
            Assert.True(await _taskItems.UpdateAsync(healthy, healthy.Version));
        }

        var first = await InsertOpenApprovalAsync(Ordered(6), Guid.NewGuid()); // its task is gone
        var second = await InsertOpenApprovalAsync(Ordered(7), Guid.NewGuid());
        SweepWorkflowOrphansHandler Paged() =>
            new(_store, _instances, _tasks, _taskItems, new RecoveryMediator(this), scanPageSize: 2);

        var all = await Paged().Handle(new SweepWorkflowOrphansCommand(Apply: false, 100, "corr-pages"), default);
        Assert.Equal([first, second], all.Data!.Items.Select(i => i.WorkflowInstanceId));
        Assert.False(all.Data.More);

        var one = await Paged().Handle(new SweepWorkflowOrphansCommand(Apply: false, 1, "corr-more"), default);
        Assert.Equal([first], one.Data!.Items.Select(i => i.WorkflowInstanceId));
        Assert.True(one.Data.More);
        var two = await Paged().Handle(new SweepWorkflowOrphansCommand(Apply: false, 1, "corr-more-2", one.Data.NextAfter), default);
        Assert.Equal([second], two.Data!.Items.Select(i => i.WorkflowInstanceId));
        Assert.True(two.Data.More); // it stopped at its limit on the last orphan: it cannot know nothing follows
        var none = await Paged().Handle(new SweepWorkflowOrphansCommand(Apply: false, 1, "corr-more-3", two.Data.NextAfter), default);
        Assert.Empty(none.Data!.Items);
        Assert.False(none.Data.More);
    }

    private async Task<Guid> InsertOpenApprovalAsync(Guid id, Guid taskId)
    {
        await _context.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances).InsertOneAsync(new WorkflowInstance
        {
            Id = id, TenantId = _tenant.TenantId, TemplateId = _templateId, WorkflowTemplateId = _templateId,
            ObjectType = TaskApprovalService.ApprovalObjectType, ObjectId = taskId.ToString(),
            ObjectRef = TaskApprovalService.BuildObjectRef(taskId), Status = WorkflowInstanceStatus.Active,
            StartedAt = DateTimeOffset.UtcNow, LastTransitionAt = DateTimeOffset.UtcNow
        });
        return id;
    }

    // ── item 6 · the D2 close keeps its answer; the recovery's close reads as never seen ─────────────────────────

    [Fact]
    public async Task A_reservation_closed_for_nobody_may_approve_keeps_its_start_result_answer()
    {
        // What the coordinator's D2 close leaves (a replay found nobody but the starter may approve): Cancelled at Reserved,
        // with its reason.
        var objectId = "GP-" + Guid.NewGuid().ToString("N");
        var key = $"d2-close:{Guid.NewGuid():N}";
        await _context.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances).InsertOneAsync(new WorkflowInstance
        {
            TenantId = _tenant.TenantId, TemplateId = _templateId, WorkflowTemplateId = _templateId,
            ObjectType = "GlobalProduct", ObjectId = objectId, ObjectRef = "GlobalProduct|" + objectId,
            Status = WorkflowInstanceStatus.Cancelled, StartCheckpoint = WorkflowStartCheckpoint.Reserved,
            StartClosedReasonCode = WorkflowReasonCodes.WorkflowNoEligibleApprover,
            TrustedConsumerClientId = TrustedClient, DelegatedMakerUserId = Maker, IdempotencyKey = key
        });
        var request = new { IdempotencyKey = key, ObjectId = objectId };

        var answer = await StartResultAsync(request.IdempotencyKey, request.ObjectId);

        Assert.Equal((409, WorkflowReasonCodes.WorkflowStartNotCompleted), (answer.StatusCode, answer.ReasonCode));
    }

    [Fact]
    public async Task The_terminal_evidence_of_a_reservation_the_recovery_closed_reads_as_not_found()
    {
        // Measured (FIX1 item 6): the consumer never held this instance's id (the start never completed), and the evidence
        // read answers not found, the same as start-result — the consumer's "not found → finish and let go" (CT decision A).
        var (request, _) = await CrashedReservationAsync("evidence-of-closed", tasks: new CrashBeforeTaskRepository(_tasks));
        var reservation = Assert.Single(await InstancesOfKeyAsync(request.IdempotencyKey));
        await AgeReservationAsync(reservation.Id);
        await SweepAsync(new WorkflowStartRecoveryMetrics());

        var evidence = await new Diten.Platform.Application.Features.Workflow.Handlers.QueryHandlers.GetTrustedWorkflowTerminalDecisionEvidenceHandler(
                _instances, _tasks, _logs)
            .Handle(new Diten.Platform.Application.Features.Workflow.Queries.GetTrustedWorkflowTerminalDecisionEvidenceQuery(
                reservation.Id, TrustedClient, request.ObjectType, request.ObjectId, "corr-evidence"), default);

        Assert.Equal((404, WorkflowReasonCodes.NotFoundNonLeakage), (evidence.StatusCode, evidence.ReasonCode));
    }

    // ── item 7 · the floor holds in the handler; the defaults ───────────────────────────────────────────────────

    [Fact]
    public async Task The_close_holds_the_fifteen_minute_floor_whatever_age_it_is_asked_with()
    {
        var instance = await InsertHalfStartAsync(Request("floor"), withIds: true);
        await _context.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances).UpdateOneAsync(
            i => i.Id == instance.Id, Builders<WorkflowInstance>.Update.Set(i => i.LastTransitionAt, DateTimeOffset.UtcNow.AddMinutes(-10)));

        var refused = await CloseHandler().Handle(new CloseAbandonedWorkflowStartCommand(instance.Id, TimeSpan.Zero, "corr-floor"), default);

        Assert.Equal((409, WorkflowReasonCodes.WorkflowStartNotAbandoned), (refused.StatusCode, refused.ReasonCode));
        Assert.Equal(TimeSpan.FromMinutes(30), WorkflowStartRecoveryOptions.AbandonAfterOf(null));
        Assert.Equal(TimeSpan.FromMinutes(30), WorkflowStartRecoveryOptions.AbandonAfterOf(new WorkflowStartRecoveryOptions()));
        Assert.Equal(TimeSpan.FromMinutes(15), WorkflowStartRecoveryOptions.AbandonAfterOf(new WorkflowStartRecoveryOptions { AbandonAfterMinutes = 5 }));
        Assert.Equal(TimeSpan.FromMinutes(45), WorkflowStartRecoveryOptions.AbandonAfterOf(new WorkflowStartRecoveryOptions { AbandonAfterMinutes = 45 }));
    }

    [Fact]
    public async Task The_job_closes_each_tenants_abandoned_starts_in_that_tenants_scope()
    {
        var mine = await InsertHalfStartAsync(Request("tenant-a"), withIds: true);
        await AgeAsync(mine.Id);
        var otherTenant = Guid.NewGuid();
        var theirs = await InsertHalfStartAsync(Request("tenant-b"), withIds: true, tenantId: otherTenant);
        await AgeAsync(theirs.Id);
        var metrics = new WorkflowStartRecoveryMetrics();

        await SweepAsync(metrics, otherTenant);

        var instances = _context.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances);
        Assert.Equal(WorkflowInstanceStatus.Cancelled, (await instances.Find(i => i.Id == mine.Id).SingleAsync()).Status);
        Assert.Equal(WorkflowInstanceStatus.Cancelled, (await instances.Find(i => i.Id == theirs.Id).SingleAsync()).Status);
        Assert.Equal(1L, metrics.For(_tenant.TenantId).Abandoned);
        Assert.Equal(1L, metrics.For(otherTenant).Abandoned);
    }

    // ── item 8 · the store's own rules (CT W1–W3), on the real store ───────────────────────────────────────────

    [Fact]
    public async Task The_store_ages_a_start_from_its_last_transition_not_from_its_start()
    {
        var instance = await InsertHalfStartAsync(Request("w1"), withIds: true);
        await _context.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances).UpdateOneAsync(
            i => i.Id == instance.Id, Builders<WorkflowInstance>.Update
                .Set(i => i.StartedAt, DateTimeOffset.UtcNow.AddHours(-3))
                .Set(i => i.LastTransitionAt, DateTimeOffset.UtcNow)); // started long ago, moved just now

        var (page, total) = await _store.FindAbandonedGenericStartsAsync(DateTimeOffset.UtcNow - Age, 100, ScanLimit);

        Assert.DoesNotContain(page, i => i.Id == instance.Id);
        Assert.Equal(0, total.Count);
        Assert.Equal(0, (await _store.CountAbandonedGenericStartsAsync(DateTimeOffset.UtcNow - Age, ScanLimit)).Count);
    }

    [Fact]
    public async Task The_store_ages_a_start_with_no_transition_field_from_its_start()
    {
        var old = await InsertHalfStartAsync(Request("w2-old"), withIds: true);
        var young = await InsertHalfStartAsync(Request("w2-young"), withIds: true);
        var raw = _context.Database.GetCollection<BsonDocument>(PlatformCollections.WorkflowInstances);
        foreach (var (id, startedAgo) in new[] { (old.Id, TimeSpan.FromHours(2)), (young.Id, TimeSpan.FromMinutes(1)) })
        {
            await raw.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(id, GuidRepresentation.Standard)),
                Builders<BsonDocument>.Update.Unset(nameof(WorkflowInstance.LastTransitionAt)));
            await _context.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances).UpdateOneAsync(
                i => i.Id == id, Builders<WorkflowInstance>.Update.Set(i => i.StartedAt, DateTimeOffset.UtcNow - startedAgo));
        }

        var (page, total) = await _store.FindAbandonedGenericStartsAsync(DateTimeOffset.UtcNow - Age, 100, ScanLimit);

        Assert.Contains(page, i => i.Id == old.Id);
        Assert.DoesNotContain(page, i => i.Id == young.Id);
        Assert.Equal(1, total.Count);
        Assert.Equal(1, (await _store.CountAbandonedGenericStartsAsync(DateTimeOffset.UtcNow - Age, ScanLimit)).Count);
    }

    [Fact]
    public async Task The_store_never_counts_a_completed_trusted_start_as_incomplete()
    {
        var completed = await InsertIncompleteTrustedStartAsync(reservedAgo: TimeSpan.FromHours(2));
        await _context.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances).UpdateOneAsync(
            i => i.Id == completed, Builders<WorkflowInstance>.Update.Set(i => i.StartCheckpoint, WorkflowStartCheckpoint.Completed));
        var incomplete = await InsertIncompleteTrustedStartAsync(reservedAgo: TimeSpan.FromHours(2));

        var listed = await _store.ListIncompleteTrustedStartsAsync(DateTimeOffset.UtcNow - Age, 100);
        var counted = await _store.CountIncompleteTrustedStartsAsync(DateTimeOffset.UtcNow - Age, ScanLimit);

        Assert.Equal([incomplete], listed.Select(i => i.Id));
        Assert.Equal(new RecoveryCount(1, false), counted);
    }

    // ── K1, K2 ─────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Somebody_else_replaying_the_key_does_not_complete_the_start()
    {
        var request = Request("other-starter");
        var instance = await InsertHalfStartAsync(request, withIds: true);

        var replay = await Handler(user: new UserOf(OtherApprover)).Handle(Command(request), default);

        Assert.Equal((409, WorkflowReasonCodes.WorkflowInstanceStartConflict), (replay.StatusCode, replay.ReasonCode));
        Assert.Empty(await _tasks.ListByInstanceIdAsync(instance.Id));
    }

    [Fact]
    public async Task A_closed_start_written_before_its_log_answers_as_before()
    {
        var request = Request("legacy-no-log");
        var started = await Handler().Handle(Command(request), default);
        Assert.True(started.IsSuccessful);
        var logs = _context.Database.GetCollection<WorkflowTransitionLog>(PlatformCollections.WorkflowTransitionLogs);
        await logs.DeleteManyAsync(l => l.WorkflowInstanceId == started.Data!.WorkflowInstanceId); // an old start with no log
        await _context.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances).UpdateOneAsync(
            i => i.Id == started.Data!.WorkflowInstanceId, Builders<WorkflowInstance>.Update.Set(i => i.Status, WorkflowInstanceStatus.Completed));

        var replay = await Handler().Handle(Command(request), default);

        Assert.True(replay.IsSuccessful, replay.ReasonCode);
        Assert.Equal(started.Data!.ApprovalTaskId, replay.Data!.ApprovalTaskId);
        Assert.Empty(await logs.Find(l => l.WorkflowInstanceId == started.Data.WorkflowInstanceId).ToListAsync()); // nothing written
    }

    // ══ FIX2 ══════════════════════════════════════════════════════════════════════════════════════════════════════

    // ── item 1 · the claim and the task commit together ─────────────────────────────────────────────────────────

    [Fact]
    public async Task A_first_call_paused_after_its_claim_never_leaves_a_closed_instance_with_an_open_task()
    {
        var request = Request("claim-then-pause");
        var reserveHold = new Hold();
        var taskHold = new Hold();
        var start = Task.Run(() => Handler(
                instances: new HoldAfterReserveRepository(_instances, reserveHold),
                tasks: new HoldBeforeTaskInsertRepository(_tasks, taskHold))
            .Handle(Command(request), default));
        await reserveHold.ReachedAsync();
        var instance = Assert.Single(await InstancesOfAsync(request));
        await AgeAsync(instance.Id);
        reserveHold.Release();
        await taskHold.ReachedAsync(); // claimed; the task insert is in flight (the pod is paused here)
        // A claim written on its own is visible now and refreshed the age: the paused pod outlives the age, so age it again.
        // A claim inside the open transaction is not visible: the recovery sees the instance as it was before it.
        if ((await _instances.GetByIdAsync(instance.Id))!.Version != instance.Version)
        {
            await AgeAsync(instance.Id);
        }

        var closed = await CloseHandler().Handle(new CloseAbandonedWorkflowStartCommand(instance.Id, Age, "corr-claim-pause"), default);
        taskHold.Release();
        var started = await start;

        var stored = (await _instances.GetByIdAsync(instance.Id))!;
        var tasks = await _tasks.ListByInstanceIdAsync(instance.Id);
        Assert.False(
            stored.Status == WorkflowInstanceStatus.Cancelled && tasks.Any(t => t.Status == ApprovalTaskStatus.WaitingApproval),
            $"a Cancelled instance with an open task (close: {closed.StatusCode} {closed.ReasonCode}, start: {started.StatusCode})");
        Assert.True(started.IsSuccessful, started.ReasonCode);
        Assert.False(closed.IsSuccessful);
        Assert.Equal(WorkflowInstanceStatus.Active, stored.Status);
        Assert.Single(tasks, t => t.Status == ApprovalTaskStatus.WaitingApproval);
    }

    // ── item 2 · a first call that loses its claim to a replay ──────────────────────────────────────────────────

    [Fact]
    public async Task A_first_call_that_loses_its_claim_to_a_replay_answers_as_the_replay_does()
    {
        var request = Request("double-submit");
        var hold = new Hold();
        var first = Task.Run(() => Handler(instances: new HoldAfterReserveRepository(_instances, hold)).Handle(Command(request), default));
        await hold.ReachedAsync(); // the first call wrote its instance and is paused before its claim
        var second = await Handler().Handle(Command(request), default); // the double submit completes it

        hold.Release();
        var answer = await first;

        Assert.True(second.IsSuccessful, second.ReasonCode);
        Assert.True(answer.IsSuccessful, answer.ReasonCode);
        Assert.Equal(second.Data!.WorkflowInstanceId, answer.Data!.WorkflowInstanceId);
        Assert.Equal(second.Data.ApprovalTaskId, answer.Data.ApprovalTaskId);
        Assert.Single(await _tasks.ListByInstanceIdAsync(answer.Data.WorkflowInstanceId));
    }

    // ── item 3 · the operator's wiring, as it runs ──────────────────────────────────────────────────────────────

    private const string DatabaseName = "diten_platform_workflow_start_recovery";

    // Production reads its secrets from the PROCESS environment only (SecretRequirementValidator) — measured by the process
    // test above, which sets them as an operator does. In-process, a non-production environment takes them from this
    // configuration; every registration and the run are the same.
    private const string OperatorEnvironment = "Staging";

    private Microsoft.Extensions.Configuration.IConfiguration OperatorConfiguration()
    {
        static string Throwaway() => Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        // The operator's own configuration (the host's appsettings, as the run reads them), with only the database and
        // throwaway secrets laid over it.
        var builder = Microsoft.Extensions.Configuration.ChainedBuilderExtensions.AddConfiguration(
            new Microsoft.Extensions.Configuration.ConfigurationBuilder(),
            Diten.Platform.API.Configuration.WorkflowOrphanRecoveryOperationalMode.ReadConfiguration(
                OperatorEnvironment, AppContext.BaseDirectory));
        return Microsoft.Extensions.Configuration.MemoryConfigurationBuilderExtensions.AddInMemoryCollection(
                builder, new Dictionary<string, string?>
            {
                ["MongoDbSettings:ConnectionString"] = replicaSet.ConnectionString,
                ["MongoDbSettings:DatabaseName"] = DatabaseName,
                ["JwtSettings:Secret"] = Throwaway(),
                ["AuthService:InternalApiKey"] = Throwaway(),
                ["ModuleRegistrationCredentials:Mdm:ActiveSecret"] = Throwaway()
            }).Build();
    }

    private async Task<(int Exit, string Output, string Error)> RunRealOrphanCommandAsync(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = await WorkflowOrphanRecoveryCommandLine.RunAsync(
            [WorkflowOrphanRecoveryCommandLine.Argument, .. args],
            () => Diten.Platform.API.Configuration.WorkflowOrphanRecoveryOperationalMode.BuildServices(
                OperatorConfiguration(), OperatorEnvironment, AppContext.BaseDirectory),
            output, error, "operator-host");
        return (exit, output.ToString(), error.ToString());
    }

    [Fact]
    public async Task The_operator_wiring_builds_validated_runs_a_dry_run_and_records_a_system_actor()
    {
        using (var provider = Diten.Platform.API.Configuration.WorkflowOrphanRecoveryOperationalMode.BuildServices(
                   OperatorConfiguration(), OperatorEnvironment, AppContext.BaseDirectory))
        {
            Assert.NotNull(provider); // scopes validated, and the command's own graph resolved at build (not the whole container)
        }

        var (orphan, healthy) = await OrphanAndHealthyAsync();

        var (exit, output, error) = await RunRealOrphanCommandAsync("--tenant", _tenant.TenantId.ToString());

        Assert.True(exit == 0, error);
        Assert.Contains(orphan.ToString("D"), output);
        Assert.DoesNotContain(healthy.ToString("D"), output);
        Assert.Equal(WorkflowInstanceStatus.Active, (await _instances.GetByIdAsync(orphan))!.Status);
        // The audit row the production pipeline queued, read by the production mapper.
        var row = Assert.Single(await Diten.Platform.Application.Tests.Audit.DataExportAuditTestKit.Outbox(_context.Database)
            .Find(m => m.TenantId == _tenant.TenantId && m.RequestType == nameof(SweepWorkflowOrphansCommand)).ToListAsync());
        var written = new Diten.Platform.Infrastructure.Services.Audit.AuditOutboxPayloadMapper()
            .Map(Diten.Platform.Application.Tests.Audit.DataExportAuditTestKit.ToProcessingItem(row), DateTimeOffset.UtcNow);
        Assert.Equal(AuditActorType.System, written.ActorType);
        Assert.Equal(WorkflowOrphanRecoveryCommandLine.ToolName, written.Metadata!["Tool"]?.ToString());
        Assert.Equal("operator-host", written.Metadata["Host"]?.ToString());
    }

    [Fact]
    public async Task The_operator_wiring_exits_4_and_names_an_orphan_it_could_not_close()
    {
        var (orphan, _) = await OrphanAndHealthyAsync();
        var openTask = Assert.Single(await _tasks.ListByInstanceIdAsync(orphan), t => t.Status == ApprovalTaskStatus.WaitingApproval);
        // The store refuses any change to that one task (a collection validator): the engine's cancel of it fails.
        var restore = await RefuseChangesToAsync(PlatformCollections.ApprovalTasks, openTask.Id);
        try
        {
            var (exit, output, error) = await RunRealOrphanCommandAsync("--tenant", _tenant.TenantId.ToString(), "--apply", "--expect", "1");

            Assert.True(exit == 4, $"exit {exit}: {error}");
            Assert.Contains(orphan.ToString("D"), error);
            Assert.Contains("APPLIED — 0 of 1", output);
        }
        finally
        {
            await restore();
        }

        Assert.Equal(WorkflowInstanceStatus.Active, (await _instances.GetByIdAsync(orphan))!.Status);
    }

    private async Task<Func<Task>> RefuseChangesToAsync(string collection, Guid documentId)
    {
        var listed = await (await _context.Database.ListCollectionsAsync(new ListCollectionsOptions
        {
            Filter = new BsonDocument("name", collection)
        })).SingleAsync();
        var options = listed.GetValue("options", new BsonDocument()).AsBsonDocument;
        var before = options.GetValue("validator", new BsonDocument()).AsBsonDocument;
        await _context.Database.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            { "collMod", collection },
            { "validator", new BsonDocument("_id", new BsonDocument("$ne", new BsonBinaryData(documentId, GuidRepresentation.Standard))) },
            { "validationLevel", "strict" },
            { "validationAction", "error" }
        });
        return async () =>
        {
            await _context.Database.RunCommandAsync<BsonDocument>(new BsonDocument
            {
                { "collMod", collection },
                { "validator", before },
                { "validationLevel", options.GetValue("validationLevel", "strict") },
                { "validationAction", options.GetValue("validationAction", "error") }
            });
        };
    }

    // ── item 4 · the counting worker and the bounded count ──────────────────────────────────────────────────────

    private sealed class ServiceScopes(IServiceProvider provider) : Microsoft.Extensions.DependencyInjection.IServiceScopeFactory
    {
        public Microsoft.Extensions.DependencyInjection.IServiceScope CreateScope() => new Scope(provider);

        private sealed class Scope(IServiceProvider provider) : Microsoft.Extensions.DependencyInjection.IServiceScope
        {
            public IServiceProvider ServiceProvider { get; } = provider;
            public void Dispose() { }
        }
    }

    private sealed class SingleService(object service) : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType.IsInstanceOfType(service) ? service : null;
    }

    private sealed class ThrowingService : IServiceProvider
    {
        public object? GetService(Type serviceType) => throw new InvalidOperationException("the count's store is down");
    }

    private sealed class WorkerLog : Microsoft.Extensions.Logging.ILogger<WorkflowStartRecoveryCountingService>
    {
        public System.Collections.Concurrent.ConcurrentQueue<string> Warnings { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == Microsoft.Extensions.Logging.LogLevel.Warning)
            {
                Warnings.Enqueue(formatter(state, exception) + (exception is null ? string.Empty : " " + exception.Message));
            }
        }
    }

    private static BackgroundJobSchedulerOptions SchedulerWithTheJob(bool on) => new()
    {
        Enabled = on, RegisterStandardJobs = on,
        EnabledJobs = { [WorkflowStartRecoveryCountingService.SweepJobId] = on }
    };

    private WorkflowStartRecoveryCountingService Worker(
        IServiceProvider services, bool jobOn, bool countWhileOff, WorkerLog log, Func<TimeSpan, CancellationToken, Task>? delay = null,
        int intervalMinutes = WorkflowStartRecoveryOptions.DefaultCountIntervalMinutes) =>
        new(new ServiceScopes(services), Options.Create(SchedulerWithTheJob(jobOn)),
            Options.Create(new WorkflowStartRecoveryOptions { CountWhileSweepIsOff = countWhileOff, CountIntervalMinutes = intervalMinutes }),
            log, delay);

    [Fact]
    public async Task The_counting_worker_counts_only_while_the_job_is_off_and_it_is_switched_on()
    {
        var abandoned = await InsertHalfStartAsync(Request("worker"), withIds: true);
        await AgeAsync(abandoned.Id);
        var metrics = new WorkflowStartRecoveryMetrics();
        var job = Job(metrics, new RecoveryMediator(this), new JobLog());
        var log = new WorkerLog();

        Assert.Equal(WorkflowStartRecoveryCountingService.Turn.SkippedJobRuns,
            await Worker(new SingleService(job), jobOn: true, countWhileOff: true, log).RunOnceAsync(default));
        Assert.Equal(WorkflowStartRecoveryCountingService.Turn.SkippedSwitchedOff,
            await Worker(new SingleService(job), jobOn: false, countWhileOff: false, log).RunOnceAsync(default));
        Assert.Equal(0, metrics.For(_tenant.TenantId).Abandoned); // neither skipped turn counted

        Assert.Equal(WorkflowStartRecoveryCountingService.Turn.Counted,
            await Worker(new SingleService(job), jobOn: false, countWhileOff: true, log).RunOnceAsync(default));
        Assert.Equal(1, metrics.For(_tenant.TenantId).Abandoned);
        Assert.Equal(WorkflowInstanceStatus.Active, (await _instances.GetByIdAsync(abandoned.Id))!.Status);
    }

    [Fact]
    public async Task A_failing_count_is_named_by_its_type_and_the_worker_goes_on()
    {
        var log = new WorkerLog();

        var turn = await Worker(new ThrowingService(), jobOn: false, countWhileOff: true, log).RunOnceAsync(default);

        Assert.Equal(WorkflowStartRecoveryCountingService.Turn.Failed, turn);
        var line = Assert.Single(log.Warnings);
        Assert.Contains("workflow.start_recovery.count_failed", line);
        Assert.Contains(nameof(InvalidOperationException), line);
        Assert.DoesNotContain("the count's store is down", line); // the type only
    }

    private sealed class RecordingService(object service, List<string> events) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            lock (events)
            {
                events.Add("count");
            }

            return serviceType.IsInstanceOfType(service) ? service : null;
        }
    }

    [Fact]
    public async Task The_worker_waits_one_bounded_interval_before_its_first_count_and_between_counts()
    {
        var events = new List<string>();
        var waits = new List<TimeSpan>();
        var third = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var job = Job(new WorkflowStartRecoveryMetrics(), new RecoveryMediator(this), new JobLog());

        Task Delay(TimeSpan interval, CancellationToken stopping)
        {
            lock (events)
            {
                events.Add("wait");
                waits.Add(interval);
            }

            if (waits.Count < 3)
            {
                return Task.CompletedTask;
            }

            third.TrySetResult();
            return Task.Delay(Timeout.InfiniteTimeSpan, stopping); // until the host stops it
        }

        var worker = Worker(new RecordingService(job, events), jobOn: false, countWhileOff: true, new WorkerLog(), Delay, intervalMinutes: 1);
        await worker.StartAsync(default);
        await third.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await worker.StopAsync(default);

        Assert.Equal(["wait", "count", "wait", "count", "wait"], events); // the first count comes after a wait, never at start
        Assert.All(waits, w => Assert.Equal(TimeSpan.FromMinutes(WorkflowStartRecoveryOptions.MinimumCountIntervalMinutes), w));
        Assert.True(worker.ExecuteTask!.IsCompletedSuccessfully);
        Assert.Equal(TimeSpan.FromMinutes(15),
            WorkflowStartRecoveryOptions.CountIntervalOf(new WorkflowStartRecoveryOptions())); // the default
        Assert.Equal(TimeSpan.FromHours(24),
            WorkflowStartRecoveryOptions.CountIntervalOf(new WorkflowStartRecoveryOptions { CountIntervalMinutes = 100_000 }));
    }

    [Fact]
    public void The_counting_worker_is_a_hosted_service_of_the_application()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        Diten.Platform.Application.DependencyInjection.AddApplication(services);

        Assert.Contains(services, d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
            && (d.ImplementationType == typeof(WorkflowStartRecoveryCountingService)
                || d.ImplementationFactory?.Method.ReturnType == typeof(WorkflowStartRecoveryCountingService)));
    }

    [Fact]
    public async Task The_count_only_cycle_reads_no_page_and_says_at_least_past_its_limit()
    {
        for (var i = 0; i < 3; i++)
        {
            await AgeAsync((await InsertHalfStartAsync(Request($"capped-{i}"), withIds: true)).Id);
        }

        var commands = new List<BsonDocument>();
        var store = new WorkflowStartRecoveryStore(Observed(commands), _tenant);

        var under = await store.CountAbandonedGenericStartsAsync(DateTimeOffset.UtcNow - Age, scanLimit: 10);
        var capped = await store.CountAbandonedGenericStartsAsync(DateTimeOffset.UtcNow - Age, scanLimit: 2);

        Assert.Equal(new RecoveryCount(3, false), under);
        Assert.Equal(new RecoveryCount(2, true), capped);
        var aggregates = commands.Where(c => c.Contains("aggregate")).ToList();
        Assert.Equal(2, aggregates.Count);
        Assert.All(aggregates, c =>
        {
            var stages = c["pipeline"].AsBsonArray.Select(stage => stage.AsBsonDocument.Names.Single()).ToList();
            Assert.Contains("$limit", stages);
            Assert.Contains("$group", stages);
            Assert.DoesNotContain("$facet", stages); // no page
            Assert.True(stages.IndexOf("$limit") < stages.IndexOf("$lookup"), "the bound comes before the join");
        });

        // The job reports it: past the per-tenant limit (held at 100 at least) the gauge says "at least".
        var then = DateTimeOffset.UtcNow.AddHours(-2);
        var template = (await _templates.GetByIdAsync(_templateId))!;
        await _context.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances).InsertManyAsync(
            Enumerable.Range(0, 101).Select(i => new WorkflowInstance
            {
                TenantId = _tenant.TenantId, TemplateId = _templateId, WorkflowTemplateId = _templateId,
                TemplateVersionId = template.ActivePublishedVersionId, ObjectType = "recovery-object",
                ObjectId = Guid.NewGuid().ToString("D"), ObjectRef = "recovery-object|" + Guid.NewGuid().ToString("N"),
                Status = WorkflowInstanceStatus.Active, StartedAt = then, LastTransitionAt = then
            }));
        var metrics = new WorkflowStartRecoveryMetrics();
        var scope = new TenantContext();
        var log = new JobLog();
        var jobCommands = new List<BsonDocument>();
        await new WorkflowStartRecoverySweepJob(new OneTenantRegistry(_tenant.TenantId), scope, new WorkflowStartRecoveryStore(Observed(jobCommands), scope),
                new RecoveryMediator(this), metrics, log,
                Options.Create(new WorkflowStartRecoveryOptions { AbandonAfterMinutes = (int)Age.TotalMinutes, ScanLimitPerTenant = 100 }))
            .CountOnlyAsync("corr-capped");
        Assert.True(metrics.CappedFor(_tenant.TenantId));
        Assert.Equal(100, metrics.For(_tenant.TenantId).Abandoned); // at least 100 (104 are there)
        Assert.DoesNotContain(jobCommands, c => c.Contains("aggregate") && c.ToJson().Contains("$facet")); // the job read no page
        Assert.Contains(log.Lines, l => l.Contains("CappedTenants=1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_abandoned_count_walks_the_last_transition_index()
    {
        var commands = new List<BsonDocument>();
        var store = new WorkflowStartRecoveryStore(Observed(commands), _tenant);
        await store.CountAbandonedGenericStartsAsync(DateTimeOffset.UtcNow - Age, scanLimit: 10);
        var aggregate = commands.Single(c => c.Contains("aggregate"));

        var explained = await _context.Database.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            { "explain", new BsonDocument
                {
                    { "aggregate", aggregate["aggregate"] },
                    { "pipeline", aggregate["pipeline"] },
                    { "cursor", new BsonDocument() }
                }
            },
            { "verbosity", "queryPlanner" }
        });

        var plan = explained.ToJson();
        Assert.Contains("IXSCAN", plan);
        Assert.Contains("ix_workflow_instances_tenant_status_last_transition", plan);
        Assert.DoesNotContain("COLLSCAN", plan);
    }

    /// <summary>A context over the same database whose client records every command it sends (what the store asked).</summary>
    private IPlatformDbContext Observed(List<BsonDocument> commands)
    {
        var settings = MongoClientSettings.FromConnectionString(replicaSet.ConnectionString);
#pragma warning disable CS0618
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        settings.ClusterConfigurator = builder => builder.Subscribe<MongoDB.Driver.Core.Events.CommandStartedEvent>(e =>
        {
            lock (commands)
            {
                commands.Add(e.Command.DeepClone().AsBsonDocument);
            }
        });
        var client = new MongoClient(settings);
        return new PlatformDbContext(client, client.GetDatabase(DatabaseName));
    }

    // ── item 5 · a half start its replay cannot complete is closed and restarted ────────────────────────────────

    public static TheoryData<string, bool> RefusedReplays => new()
    {
        { "approval", true },  // the approval manager was edited after the failed start
        { "approval", false }, // somebody else submits it
        { "review", true },    // the reviewer was edited
        { "review", false }
    };

    [Theory]
    [MemberData(nameof(RefusedReplays))]
    public async Task A_half_start_whose_replay_is_refused_is_superseded_and_a_new_round_opens(string kind, bool editedFacts)
    {
        var task = new TaskItem
        {
            TenantId = _tenant.TenantId, Title = "Gated work", AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
            OrganizationUnitId = Guid.NewGuid(),
            ApprovalRequired = kind == "approval", ApprovalManagerUserId = kind == "approval" ? Approver : null,
            ReviewRequired = kind == "review", ReviewerCandidateUserId = kind == "review" ? Approver : null
        };
        var (objectRef, objectType) = kind == "approval"
            ? (TaskApprovalService.BuildObjectRef(task.Id), TaskApprovalService.ApprovalObjectType)
            : (TaskReviewService.BuildObjectRef(task.Id), TaskReviewService.ReviewObjectType);
        if (kind == "approval")
        {
            await ApprovalService(crashBeforeTask: true).TryStartApprovalAsync(task, default);
        }
        else
        {
            await ReviewService(crashBeforeTask: true).StartReviewAsync(task, default);
        }

        var half = (await _instances.GetLatestByObjectRefAsync(objectRef, objectType, task.Id.ToString()))!;
        Assert.Empty(await _tasks.ListByInstanceIdAsync(half.Id)); // non-vacuity: a half start
        Diten.Platform.Application.Contracts.ICurrentUserContext? user = null;
        if (editedFacts)
        {
            task.ApprovalManagerUserId = kind == "approval" ? OtherApprover : null;
            task.ReviewerCandidateUserId = kind == "review" ? OtherApprover : null;
        }
        else
        {
            user = new UserOf(Guid.NewGuid());
        }

        var opened = kind == "approval"
            ? await ApprovalService(user: user).TryStartApprovalAsync(task, default)
            : (await ReviewService(user: user).StartReviewAsync(task, default)).InstanceId;

        var closed = (await _instances.GetByIdAsync(half.Id))!;
        Assert.Equal((WorkflowInstanceStatus.Cancelled, WorkflowReasonCodes.WorkflowStartSuperseded), (closed.Status, closed.StartClosedReasonCode));
        var log = Assert.Single(await _logs.ListByInstanceIdAsync(half.Id), l => l.Action == WorkflowTransitionAction.Cancel);
        Assert.Equal(WorkflowReasonCodes.WorkflowStartSuperseded, log.ReasonCode);
        Assert.Contains(_audit.Requests, r => r.RequestType == nameof(SupersedeWorkflowHalfStartCommand) && r.EntityId == half.Id);
        Assert.NotNull(opened);
        Assert.NotEqual(half.Id, opened);
        var next = (await _instances.GetByIdAsync(opened!.Value))!;
        Assert.Equal(WorkflowInstanceStatus.Active, next.Status);
        var newTask = Assert.Single(await _tasks.ListByInstanceIdAsync(next.Id));
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, newTask.Status);
        Assert.Equal(D(editedFacts ? OtherApprover : Approver), newTask.AssigneeRef);
    }

    [Fact]
    public async Task The_supersede_never_closes_a_start_that_has_a_task()
    {
        var started = await Handler().Handle(Command(Request("whole")), default);
        Assert.True(started.IsSuccessful, started.ReasonCode);

        var refused = await new RecoveryMediator(this).Send(
            new SupersedeWorkflowHalfStartCommand(started.Data!.WorkflowInstanceId, "corr-whole"));

        Assert.Equal((409, WorkflowReasonCodes.WorkflowStartNotAbandoned), (refused.StatusCode, refused.ReasonCode));
        Assert.Equal(WorkflowInstanceStatus.Active, (await _instances.GetByIdAsync(started.Data.WorkflowInstanceId))!.Status);
    }

    // ── K1 · --expect binds the ids it counted ──────────────────────────────────────────────────────────────────

    private sealed class OrphanAppearsBetweenScans(IMediator inner, Func<Task> appear) : IMediator
    {
        private bool _appeared;

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            if (request is SweepWorkflowOrphansCommand { Apply: true } && !_appeared)
            {
                _appeared = true;
                await appear();
            }

            return await inner.Send(request, ct);
        }

        public Task<object?> Send(object request, CancellationToken ct = default) => inner.Send(request, ct);
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest => inner.Send(request, ct);
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default) => inner.CreateStream(request, ct);
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => inner.CreateStream(request, ct);
        public Task Publish(object notification, CancellationToken ct = default) => inner.Publish(notification, ct);
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : INotification => inner.Publish(notification, ct);
    }

    [Fact]
    public async Task The_orphan_command_closes_only_the_orphans_its_dry_run_counted()
    {
        var (counted, _) = await OrphanAndHealthyAsync();
        var late = Guid.Empty;
        var scope = new TenantContext();
        IServiceProvider Services()
        {
            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<ITenantContext>(services, scope);
            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<IMediator>(services,
                new OrphanAppearsBetweenScans(new RecoveryMediator(this, scope: scope), async () =>
                    late = await StartForAsync(TaskApprovalService.ApprovalObjectType, Guid.NewGuid(), "late-orphan")));
            return Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(services);
        }

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = await WorkflowOrphanRecoveryCommandLine.RunAsync(
            [WorkflowOrphanRecoveryCommandLine.Argument, "--tenant", _tenant.TenantId.ToString(), "--apply", "--expect", "1"],
            Services, output, error, "test-host");

        Assert.True(exit == 0, error.ToString());
        Assert.NotEqual(Guid.Empty, late); // non-vacuity: an orphan did appear between the two scans
        Assert.Equal(WorkflowInstanceStatus.Cancelled, (await _instances.GetByIdAsync(counted))!.Status);
        Assert.Equal(WorkflowInstanceStatus.Active, (await _instances.GetByIdAsync(late))!.Status); // left for the next run
        Assert.Contains("APPLIED — 1 of 1", output.ToString());
    }

    // ── K2 · an orphan with nothing to cancel; the failure text ─────────────────────────────────────────────────

    [Fact]
    public async Task An_orphan_with_no_open_task_is_listed_apart_and_is_not_a_failure()
    {
        // Its task is gone and so is its approval task (cancelled by hand, the instance left live).
        var orphan = await StartForAsync(TaskApprovalService.ApprovalObjectType, Guid.NewGuid(), "no-open-task");
        var approvalTask = Assert.Single(await _tasks.ListByInstanceIdAsync(orphan));
        await _context.Database.GetCollection<ApprovalTask>(PlatformCollections.ApprovalTasks).UpdateOneAsync(
            t => t.Id == approvalTask.Id, Builders<ApprovalTask>.Update.Set(t => t.Status, ApprovalTaskStatus.Cancelled));

        var (exit, output, error) = await RunOrphanCommandAsync("--tenant", _tenant.TenantId.ToString(), "--apply", "--expect", "1");

        Assert.True(exit == 3, $"exit {exit}: {error}"); // FIX3 K12 — its own exit code
        Assert.Contains(orphan.ToString("D"), error);   // …and the ids on stderr too
        Assert.Contains("NO OPEN TASK — 1", output);
        Assert.Contains(orphan.ToString("D"), output);
        Assert.Contains("APPLIED — 0 of 1", output);
        Assert.DoesNotContain("could not be closed", error);
    }

    // ── K5 · a 503 that never clears; the gauges expire ─────────────────────────────────────────────────────────

    [Fact]
    public async Task A_close_that_cannot_be_written_cycle_after_cycle_is_named_once_at_warning()
    {
        var abandoned = await InsertHalfStartAsync(Request("no-transactions"), withIds: true);
        await AgeAsync(abandoned.Id);
        var metrics = new WorkflowStartRecoveryMetrics();
        var logs = new JobLog();
        var unavailable = new AnsweringMediator(Response<CloseAbandonedWorkflowStartResult>.Fail(
            "no transaction", 503, WorkflowReasonCodes.WorkflowTransitionConflict, "corr"));

        for (var cycle = 0; cycle < 7; cycle++)
        {
            await Job(metrics, unavailable, logs).HandleAsync(new WorkflowStartRecoverySweepJobArgs(0), JobContext());
        }

        Assert.Single(logs.Warnings, l => l.Contains("workflow.start_recovery.deferred_persistent", StringComparison.Ordinal)
            && l.Contains(abandoned.Id.ToString(), StringComparison.Ordinal));
        Assert.Empty(logs.Errors);
        Assert.Equal(0L, metrics.For(_tenant.TenantId).Stuck);
    }

    private sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public void A_tenants_gauges_drop_out_when_it_is_no_longer_counted_and_expire_when_counting_stops()
    {
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        using var metrics = new WorkflowStartRecoveryMetrics(clock);
        var kept = Guid.NewGuid();
        var gone = Guid.NewGuid();
        metrics.Record(kept, 1, 0, 0);
        metrics.Record(gone, 2, 0, 0);

        metrics.Retain([kept]);
        Assert.Equal([kept], metrics.PublishedTenants());

        clock.Now += WorkflowStartRecoveryMetrics.MaxAge + TimeSpan.FromMinutes(1);
        Assert.Empty(metrics.PublishedTenants());
    }

    // ══ FIX3 ══════════════════════════════════════════════════════════════════════════════════════════════════════

    // ── item 1 · parallel submits of one key ────────────────────────────────────────────────────────────────────

    /// <summary>Holds every call at its FIRST read (the key look-up, before anything is written) until all N are there (or
    /// 10 s): they then reserve and claim at once — none can find the start already made and skip the race.</summary>
    private sealed class AllReservedBarrier(IWorkflowInstanceRepository inner, int callers, TaskCompletionSource all, int[] arrived)
        : IWorkflowInstanceRepository
    {
        public Task<WorkflowInstance> CreateAsync(WorkflowInstance value, CancellationToken ct = default) => inner.CreateAsync(value, ct);
        public Task<WorkflowInstance?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
        private bool _waited;

        public async Task<WorkflowInstance?> GetByIdempotencyKeyAsync(string key, CancellationToken ct = default)
        {
            if (!_waited)
            {
                _waited = true;
                if (Interlocked.Increment(ref arrived[0]) == callers)
                {
                    all.TrySetResult();
                }

                await Task.WhenAny(all.Task, Task.Delay(TimeSpan.FromSeconds(10)));
            }

            return await inner.GetByIdempotencyKeyAsync(key, ct);
        }

        public Task<WorkflowInstance?> GetLatestByObjectRefAsync(string objectRef, string objectType, string objectId, CancellationToken ct = default) =>
            inner.GetLatestByObjectRefAsync(objectRef, objectType, objectId, ct);
        public Task<IReadOnlyList<WorkflowInstance>> GetAllForTenantAsync(CancellationToken ct = default) => inner.GetAllForTenantAsync(ct);
        public Task<bool> UpdateAsync(WorkflowInstance value, int version, CancellationToken ct = default) => inner.UpdateAsync(value, version, ct);
        public Task<(WorkflowInstance Instance, bool Created)> ReserveTrustedStartAsync(WorkflowInstance value, CancellationToken ct = default) =>
            inner.ReserveTrustedStartAsync(value, ct);

        public Task<bool> ClaimStartCompletionAsync(Guid instanceId, int expectedVersion, Guid approvalTaskId, Guid assignmentSnapshotId,
            Guid startTransitionLogId, DateTimeOffset now, CancellationToken ct = default) =>
            inner.ClaimStartCompletionAsync(instanceId, expectedVersion, approvalTaskId, assignmentSnapshotId, startTransitionLogId, now, ct);
        public Task<bool> ClaimStartCompletionAsync(IPlatformTransactionSession session, Guid instanceId, int expectedVersion,
            Guid approvalTaskId, Guid assignmentSnapshotId, Guid startTransitionLogId, DateTimeOffset now, CancellationToken ct = default) =>
            inner.ClaimStartCompletionAsync(session, instanceId, expectedVersion, approvalTaskId, assignmentSnapshotId, startTransitionLogId, now, ct);
    }

    [Fact]
    public async Task Parallel_submits_of_one_key_write_one_task_and_answer_200_or_a_retryable_503()
    {
        const int callers = 6;
        // What one start alone leaves: its instance claimed exactly once.
        var reference = await Handler().Handle(Command(Request("parallel-reference")), default);
        var claimedOnce = (await _instances.GetByIdAsync(reference.Data!.WorkflowInstanceId))!.Version;
        var request = Request("parallel");
        var all = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrived = new int[1];

        var answers = await Task.WhenAll(Enumerable.Range(0, callers).Select(_ => Task.Run(() =>
            Handler(instances: new AllReservedBarrier(_instances, callers, all, arrived)).Handle(Command(request), default))));

        Assert.True(all.Task.IsCompletedSuccessfully, "non-vacuity: all callers were in before any wrote");
        var instance = Assert.Single(await InstancesOfAsync(request));
        Assert.Single(await _tasks.ListByInstanceIdAsync(instance.Id));
        Assert.All(answers, a => Assert.True(
            a.IsSuccessful || (a.StatusCode == 503 && a.ReasonCode == WorkflowReasonCodes.WorkflowTransitionConflict),
            $"{a.StatusCode} {a.ReasonCode}"));
        Assert.All(answers.Where(a => a.IsSuccessful), a => Assert.Equal(instance.Id, a.Data!.WorkflowInstanceId));
        Assert.True(answers.Count(a => a.IsSuccessful) > callers / 2, string.Join(", ", answers.Select(a => a.StatusCode)));
        // An answer "as the replay" writes nothing: the instance was claimed once, by the winner.
        Assert.Equal(claimedOnce, instance.Version);
    }

    private sealed class HoldBeforeBody(IPlatformTransactionExecutor inner, Hold hold) : IPlatformTransactionExecutor
    {
        public async Task<T> ExecuteAsync<T>(Func<IPlatformTransactionSession, CancellationToken, Task<T>> body, CancellationToken ct = default)
        {
            await hold.PassAsync();
            return await inner.ExecuteAsync(body, ct);
        }
    }

    /// <summary>Lets <paramref name="before"/> run (a winner commits), then says "unavailable" — what three write conflicts
    /// against an open winner end in.</summary>
    private sealed class UnavailableAfter(Func<Task> before) : IPlatformTransactionExecutor
    {
        public async Task<T> ExecuteAsync<T>(Func<IPlatformTransactionSession, CancellationToken, Task<T>> body, CancellationToken ct = default)
        {
            await before();
            throw new PlatformTransactionUnavailableException("injected: the winner held the instance");
        }
    }

    private StartWorkflowInstanceHandler HandlerWith(IPlatformTransactionExecutor transactions) =>
        new(_templates, _versions, _instances, _tasks, _snapshots, _logs, _tenant, new StarterUser(), transactions: transactions);

    [Fact]
    public async Task A_replay_that_loses_its_claim_to_a_whole_start_answers_as_it_and_writes_nothing()
    {
        var request = Request("lost-to-whole");
        var half = await InsertHalfStartAsync(request, withIds: true);
        var hold = new Hold();
        var loser = Task.Run(() => HandlerWith(new HoldBeforeBody(new PlatformTransactionExecutor(_context), hold)).Handle(Command(request), default));
        await hold.ReachedAsync(); // the loser read the instance (no task) and is about to claim it
        var winner = await Handler().Handle(Command(request), default);
        Assert.True(winner.IsSuccessful, winner.ReasonCode);
        var claimed = (await _instances.GetByIdAsync(half.Id))!.Version;

        hold.Release();
        var answer = await loser;

        Assert.True(answer.IsSuccessful, $"{answer.StatusCode} {answer.ReasonCode}");
        Assert.Equal(winner.Data!.ApprovalTaskId, answer.Data!.ApprovalTaskId);
        Assert.Equal(claimed, (await _instances.GetByIdAsync(half.Id))!.Version); // the loser wrote nothing
        Assert.Single(await _tasks.ListByInstanceIdAsync(half.Id));
    }

    [Fact]
    public async Task A_start_told_unavailable_after_a_winner_committed_answers_as_the_winner()
    {
        var request = Request("unavailable-after-winner");
        Response<StartWorkflowInstanceResponse>? winner = null;

        var answer = await HandlerWith(new UnavailableAfter(async () => winner = await Handler().Handle(Command(request), default)))
            .Handle(Command(request), default);

        Assert.True(winner!.IsSuccessful, winner.ReasonCode); // non-vacuity: the winner completed the start
        Assert.True(answer.IsSuccessful, $"{answer.StatusCode} {answer.ReasonCode}");
        Assert.Equal(winner.Data!.ApprovalTaskId, answer.Data!.ApprovalTaskId);
        Assert.Single(await _tasks.ListByInstanceIdAsync(answer.Data.WorkflowInstanceId));
    }

    // ── item 2 · the operator's wiring, all of it ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_operator_wiring_applies_with_a_transaction_a_completion_event_and_audit_rows()
    {
        var (orphan, healthy) = await OrphanAndHealthyAsync();

        var (exit, output, error) = await RunRealOrphanCommandAsync("--tenant", _tenant.TenantId.ToString(), "--apply", "--expect", "1");

        Assert.True(exit == 0, $"exit {exit}: {error}");
        Assert.Contains("APPLIED — 1 of 1", output);
        Assert.Equal(WorkflowInstanceStatus.Cancelled, (await _instances.GetByIdAsync(orphan))!.Status);
        Assert.Equal(WorkflowInstanceStatus.Active, (await _instances.GetByIdAsync(healthy))!.Status);
        // The completion event the production writer queued, in the production outbox.
        var events = await _context.Database.GetCollection<BsonDocument>(PlatformCollections.OutboxEvents).Find(new BsonDocument()).ToListAsync();
        Assert.Contains(events, e => TextOf(e).Contains(orphan.ToString("D"), StringComparison.OrdinalIgnoreCase)
            && TextOf(e).Contains(Diten.Platform.Application.Features.Workflow.Events.WorkflowInstanceCompletedV1.Name, StringComparison.Ordinal));
        // The audit rows the production pipeline queued: the sweep (twice: the dry pass and the apply) and the engine's cancel.
        var rows = await Diten.Platform.Application.Tests.Audit.DataExportAuditTestKit.Outbox(_context.Database)
            .Find(m => m.TenantId == _tenant.TenantId).ToListAsync();
        Assert.Equal(2, rows.Count(r => r.RequestType == nameof(SweepWorkflowOrphansCommand)));
        Assert.Single(rows, r => r.RequestType == nameof(CancelWorkflowTaskCommand));
    }

    /// <summary>A stored outbox row as text: its fields, and its payload bytes read as the UTF-8 they are.</summary>
    private static string TextOf(BsonDocument document)
    {
        var text = new System.Text.StringBuilder(document.ToJson());
        void Walk(BsonValue value)
        {
            switch (value)
            {
                case BsonBinaryData binary when binary.SubType == BsonBinarySubType.Binary:
                    text.Append(' ').Append(System.Text.Encoding.UTF8.GetString(binary.Bytes));
                    break;
                case BsonDocument inner:
                    foreach (var element in inner)
                    {
                        Walk(element.Value);
                    }

                    break;
                case BsonArray array:
                    foreach (var item in array)
                    {
                        Walk(item);
                    }

                    break;
            }
        }

        Walk(document);
        return text.ToString();
    }

    [Fact]
    public async Task The_start_handler_from_the_operator_container_has_its_transaction_executor()
    {
        using var provider = Diten.Platform.API.Configuration.WorkflowOrphanRecoveryOperationalMode.BuildServices(
            OperatorConfiguration(), OperatorEnvironment, AppContext.BaseDirectory);
        using var scope = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.CreateScope(provider);

        var handler = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<
            IRequestHandler<StartWorkflowInstanceCommand, Response<StartWorkflowInstanceResponse>>>(scope.ServiceProvider);

        var executor = typeof(StartWorkflowInstanceHandler)
            .GetField("_transactions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(handler);
        Assert.IsType<PlatformTransactionExecutor>(executor);
        Assert.Null(Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetService<SequentialWorkflowStartWritesForTests>(scope.ServiceProvider)); // never in production
        await Task.CompletedTask;
    }

    [Fact]
    public void The_start_handler_refuses_to_be_built_without_a_transaction_executor()
    {
        var refused = Assert.Throws<ArgumentNullException>(() => new StartWorkflowInstanceHandler(
            _templates, _versions, _instances, _tasks, _snapshots, _logs, _tenant, new StarterUser()));
        Assert.Equal("transactions", refused.ParamName);
    }

    [Fact]
    public async Task The_operator_command_process_runs_a_production_dry_run_end_to_end()
    {
        var (orphan, healthy) = await OrphanAndHealthyAsync();
        static string Throwaway() => Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var dll = Path.Combine(AppContext.BaseDirectory, "Diten.Platform.API.dll");
        var start = new System.Diagnostics.ProcessStartInfo("dotnet",
            $"\"{dll}\" {WorkflowOrphanRecoveryCommandLine.Argument} --tenant {_tenant.TenantId:D}")
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory
        };
        // Production, as an operator runs it: every secret from the process environment — the owned test replica set,
        // never the shared Mongo, and throwaway values made here.
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        start.Environment["MongoDbSettings__ConnectionString"] = replicaSet.ConnectionString;
        start.Environment["MongoDbSettings__DatabaseName"] = DatabaseName;
        start.Environment["JwtSettings__Secret"] = Throwaway();
        start.Environment["AuthService__InternalApiKey"] = Throwaway();
        start.Environment["ModuleRegistrationCredentials__Mdm__ActiveSecret"] = Throwaway();
        using var process = System.Diagnostics.Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(120_000));

        Assert.True(process.ExitCode == 0, $"exit {process.ExitCode}: {await stderr}");
        Assert.Contains(orphan.ToString("D"), await stdout);
        Assert.DoesNotContain(healthy.ToString("D"), await stdout);
        Assert.Contains("DRY RUN", await stdout);
        Assert.Equal(WorkflowInstanceStatus.Active, (await _instances.GetByIdAsync(orphan))!.Status);
    }

    [Fact]
    public async Task A_setup_failure_is_told_apart_from_a_run_failure()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await WorkflowOrphanRecoveryCommandLine.RunAsync(
            [WorkflowOrphanRecoveryCommandLine.Argument, "--tenant", _tenant.TenantId.ToString()],
            () => throw new InvalidOperationException("secret-looking detail"), output, error, "test-host");

        Assert.Equal(1, exit);
        Assert.Contains("setup failure (InvalidOperationException)", error.ToString());
        Assert.DoesNotContain("secret-looking detail", error.ToString());
        Assert.DoesNotContain("dry run again", error.ToString());
    }

    // ── item 3 · past the limit: oldest first, and said once ────────────────────────────────────────────────────

    private async Task InsertHealthyOldAsync(int count, DateTimeOffset at)
    {
        var template = (await _templates.GetByIdAsync(_templateId))!;
        var instances = Enumerable.Range(0, count).Select(_ => new WorkflowInstance
        {
            TenantId = _tenant.TenantId, TemplateId = _templateId, WorkflowTemplateId = _templateId,
            TemplateVersionId = template.ActivePublishedVersionId, ObjectType = "recovery-object",
            ObjectId = Guid.NewGuid().ToString("D"), ObjectRef = "recovery-object|" + Guid.NewGuid().ToString("N"),
            Status = WorkflowInstanceStatus.Active, StartedAt = at, LastTransitionAt = at
        }).ToList();
        await _context.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances).InsertManyAsync(instances);
        await _context.Database.GetCollection<ApprovalTask>(PlatformCollections.ApprovalTasks).InsertManyAsync(instances.Select(i =>
            new ApprovalTask
            {
                TenantId = _tenant.TenantId, WorkflowInstanceId = i.Id, StageCode = "stage-1", StepCode = "step-1",
                Status = ApprovalTaskStatus.WaitingApproval, AssigneeRef = D(Approver)
            }));
    }

    [Fact]
    public async Task Past_its_limit_the_close_warns_once_that_it_sees_a_subset()
    {
        // 101 healthy approvals waiting for hours and one half start; the limit is 100. (Which 100 are looked at is not
        // ordered: see the report — an index-backed oldest-first order is CT's decision.)
        await InsertHealthyOldAsync(101, DateTimeOffset.UtcNow.AddHours(-3));
        var half = await InsertHalfStartAsync(Request("oldest-half"), withIds: true);
        var then = DateTimeOffset.UtcNow.AddHours(-5);
        await _context.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances).UpdateOneAsync(
            i => i.Id == half.Id, Builders<WorkflowInstance>.Update.Set(i => i.LastTransitionAt, then).Set(i => i.StartedAt, then));
        var metrics = new WorkflowStartRecoveryMetrics();
        var log = new JobLog();
        var scope = new TenantContext();
        WorkflowStartRecoverySweepJob CappedJob() =>
            new(new OneTenantRegistry(_tenant.TenantId), scope, new WorkflowStartRecoveryStore(_context, scope),
                new RecoveryMediator(this, scope: scope), metrics, log,
                Options.Create(new WorkflowStartRecoveryOptions { AbandonAfterMinutes = (int)Age.TotalMinutes, ScanLimitPerTenant = 100 }));

        await CappedJob().HandleAsync(new WorkflowStartRecoverySweepJobArgs(0), JobContext());
        await CappedJob().HandleAsync(new WorkflowStartRecoverySweepJobArgs(0), JobContext());

        Assert.True(metrics.CappedFor(_tenant.TenantId)); // "at least"
        Assert.Single(log.Warnings, l => l.Contains("workflow.start_recovery.capped", StringComparison.Ordinal)); // once
    }

    [Fact]
    public async Task The_abandoned_count_walks_its_index_and_joins_tasks_by_theirs()
    {
        await InsertHealthyOldAsync(20, DateTimeOffset.UtcNow.AddHours(-3));
        await AgeAsync((await InsertHalfStartAsync(Request("explained"), withIds: true)).Id);
        var commands = new List<BsonDocument>();
        var store = new WorkflowStartRecoveryStore(Observed(commands), _tenant);
        Assert.Equal(new RecoveryCount(1, false), await store.CountAbandonedGenericStartsAsync(DateTimeOffset.UtcNow - Age, scanLimit: 100));
        var aggregate = commands.Single(c => c.Contains("aggregate"));

        var explained = await _context.Database.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            { "explain", new BsonDocument
                {
                    { "aggregate", aggregate["aggregate"] },
                    { "pipeline", aggregate["pipeline"] },
                    { "cursor", new BsonDocument() }
                }
            },
            { "verbosity", "executionStats" }
        });

        var plan = explained.ToJson();
        Assert.Contains("ix_workflow_instances_tenant_status_last_transition", plan);
        Assert.DoesNotContain("COLLSCAN", plan);
        Assert.DoesNotContain("\"stage\" : \"SORT\"", plan); // no in-memory sort of every unmoved instance
        var lookup = FindStage(explained, "$lookup");
        Assert.NotNull(lookup);
        Assert.Contains("ix_approval_tasks_tenant_instance_status", lookup!.ToJson());
        Assert.True(lookup["totalDocsExamined"].ToInt64() <= 21, lookup.ToJson()); // at most one task per instance looked at
    }

    private static BsonDocument? FindStage(BsonValue node, string stage)
    {
        switch (node)
        {
            case BsonDocument document:
                if (document.Contains(stage))
                {
                    return document;
                }

                foreach (var element in document)
                {
                    if (FindStage(element.Value, stage) is { } found)
                    {
                        return found;
                    }
                }

                return null;
            case BsonArray array:
                return array.Select(item => FindStage(item, stage)).FirstOrDefault(found => found is not null);
            default:
                return null;
        }
    }

    // ── K7 · a transaction that cannot commit ───────────────────────────────────────────────────────────────────

    private sealed class FailingCommits(int failures, string label) : IPlatformTransactionFaultProbe
    {
        private int _left = failures;

        public Task BeforeCommitAsync(IPlatformTransactionSession session, int commitAttempt, CancellationToken ct)
        {
            if (Interlocked.Decrement(ref _left) >= 0)
            {
                var exception = new MongoException("injected commit failure");
                exception.AddErrorLabel(label);
                throw exception;
            }

            return Task.CompletedTask;
        }

        public Task AfterCommitAsync(IPlatformTransactionSession session, int commitAttempt, CancellationToken ct) => Task.CompletedTask;
    }

    [Theory]
    [InlineData("UnknownTransactionCommitResult", 3)]
    [InlineData("TransientTransactionError", 1)]
    public async Task A_start_whose_commit_fails_answers_a_retryable_503_and_its_replay_writes_one_task(string label, int failures)
    {
        var request = Request("commit-" + label);
        var failing = new StartWorkflowInstanceHandler(_templates, _versions, _instances, _tasks, _snapshots, _logs, _tenant,
            new StarterUser(), transactions: new PlatformTransactionExecutor(_context, new FailingCommits(failures, label)));

        var first = await failing.Handle(Command(request), default);

        Assert.Equal((503, WorkflowReasonCodes.WorkflowTransitionConflict), (first.StatusCode, first.ReasonCode));
        var instance = Assert.Single(await InstancesOfAsync(request));
        Assert.Empty(await _tasks.ListByInstanceIdAsync(instance.Id)); // nothing of the start committed
        var replay = await Handler().Handle(Command(request), default);
        Assert.True(replay.IsSuccessful, replay.ReasonCode);
        Assert.Single(await _tasks.ListByInstanceIdAsync(instance.Id));
        Assert.Single(await _snapshots.ListByInstanceIdAsync(instance.Id));
    }

    // ── K8 · the snapshot and the start log commit with the claim ───────────────────────────────────────────────

    [Fact]
    public async Task A_start_that_cannot_write_its_snapshot_or_log_leaves_no_task_either()
    {
        var request = Request("rows-together");
        await Assert.ThrowsAnyAsync<Exception>(() =>
            Handler(snapshots: new CrashBeforeSnapshotRepository(_snapshots)).Handle(Command(request), default));
        var instance = Assert.Single(await InstancesOfAsync(request));
        Assert.Empty(await _tasks.ListByInstanceIdAsync(instance.Id)); // the task rolled back with the snapshot

        var replay = await Handler().Handle(Command(request), default);
        Assert.True(replay.IsSuccessful, replay.ReasonCode);
        Assert.Single(await _tasks.ListByInstanceIdAsync(instance.Id));
        Assert.Single(await _snapshots.ListByInstanceIdAsync(instance.Id));
        Assert.Single(await _logs.ListByInstanceIdAsync(instance.Id), l => l.Action == WorkflowTransitionAction.Start);
    }

    // ── K9 · V2 · V4 · the supersede ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_second_supersede_of_one_half_start_answers_already_closed()
    {
        var half = await InsertHalfStartAsync(Request("twice"), withIds: true);

        var first = await new RecoveryMediator(this).Send(new SupersedeWorkflowHalfStartCommand(half.Id, "corr-1"));
        var second = await new RecoveryMediator(this).Send(new SupersedeWorkflowHalfStartCommand(half.Id, "corr-2"));

        Assert.True(first.IsSuccessful, first.ReasonCode);
        Assert.False(first.Data!.AlreadyClosed);
        Assert.True(second.IsSuccessful, second.ReasonCode);
        Assert.True(second.Data!.AlreadyClosed);
        Assert.Single(await _logs.ListByInstanceIdAsync(half.Id), l => l.Action == WorkflowTransitionAction.Cancel);
    }

    [Fact]
    public async Task A_supersede_after_the_recovery_closed_the_half_start_answers_already_closed()
    {
        var half = await InsertHalfStartAsync(Request("swept-first"), withIds: true);
        await AgeAsync(half.Id);
        await SweepAsync(new WorkflowStartRecoveryMetrics());
        Assert.Equal(WorkflowReasonCodes.WorkflowStartAbandoned, (await _instances.GetByIdAsync(half.Id))!.StartClosedReasonCode);

        var superseded = await new RecoveryMediator(this).Send(new SupersedeWorkflowHalfStartCommand(half.Id, "corr-after-sweep"));

        Assert.True(superseded.IsSuccessful, superseded.ReasonCode);
        Assert.True(superseded.Data!.AlreadyClosed);
    }

    [Fact]
    public async Task A_trusted_half_start_is_never_superseded()
    {
        var reservation = await InsertIncompleteTrustedStartAsync(reservedAgo: TimeSpan.FromMinutes(1));
        // a trusted start that is Active and taskless — what the generic rule would otherwise close
        await _context.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances).UpdateOneAsync(
            i => i.Id == reservation, Builders<WorkflowInstance>.Update.Set(i => i.Status, WorkflowInstanceStatus.Active));

        var refused = await new RecoveryMediator(this).Send(new SupersedeWorkflowHalfStartCommand(reservation, "corr-trusted"));

        Assert.Equal((409, WorkflowReasonCodes.WorkflowStartNotAbandoned), (refused.StatusCode, refused.ReasonCode));
        Assert.Equal(WorkflowInstanceStatus.Active, (await _instances.GetByIdAsync(reservation))!.Status);
    }

    // ── V3 · a lost claim over an instance that is no longer Active ─────────────────────────────────────────────

    [Fact]
    public async Task A_first_call_that_lost_its_claim_to_a_start_since_closed_answers_409()
    {
        var request = Request("lost-then-closed");
        var hold = new Hold();
        var first = Task.Run(() => Handler(instances: new HoldAfterReserveRepository(_instances, hold)).Handle(Command(request), default));
        await hold.ReachedAsync();
        var replay = await Handler().Handle(Command(request), default); // a whole start…
        Assert.True(replay.IsSuccessful, replay.ReasonCode);
        await _context.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances).UpdateOneAsync(
            i => i.Id == replay.Data!.WorkflowInstanceId,
            Builders<WorkflowInstance>.Update.Set(i => i.Status, WorkflowInstanceStatus.Cancelled)); // …closed since

        hold.Release();
        var answer = await first;

        Assert.Equal((409, WorkflowReasonCodes.WorkflowInstanceStartConflict), (answer.StatusCode, answer.ReasonCode));
    }

    // ── V1 · V5 · V6 · the bounded counts ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_trusted_count_is_bounded_and_says_at_least()
    {
        for (var i = 0; i < 3; i++)
        {
            await InsertIncompleteTrustedStartAsync(reservedAgo: TimeSpan.FromHours(2));
        }

        Assert.Equal(new RecoveryCount(3, false), await _store.CountIncompleteTrustedStartsAsync(DateTimeOffset.UtcNow - Age, 10));
        Assert.Equal(new RecoveryCount(2, true), await _store.CountIncompleteTrustedStartsAsync(DateTimeOffset.UtcNow - Age, 2));
    }

    [Fact]
    public async Task The_count_counts_only_starts_without_a_task()
    {
        await InsertHealthyOldAsync(3, DateTimeOffset.UtcNow.AddHours(-3));
        await AgeAsync((await InsertHalfStartAsync(Request("only-taskless"), withIds: true)).Id);

        Assert.Equal(new RecoveryCount(1, false), await _store.CountAbandonedGenericStartsAsync(DateTimeOffset.UtcNow - Age, 100));
        var (page, total) = await _store.FindAbandonedGenericStartsAsync(DateTimeOffset.UtcNow - Age, 100, 100);
        Assert.Single(page);
        Assert.Equal(1, total.Count);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(-5, 100)]
    [InlineData(1_000_000, 100_000)]
    [InlineData(7_000, 7_000)]
    public void The_scan_limit_setting_is_held_between_100_and_100000(int setting, int held) =>
        Assert.Equal(held, WorkflowStartRecoveryOptions.ScanLimitOf(new WorkflowStartRecoveryOptions { ScanLimitPerTenant = setting }));

    // ── K11 · "in a row"; when it was last counted ──────────────────────────────────────────────────────────────

    private sealed class ScriptedMediator(Queue<Response<CloseAbandonedWorkflowStartResult>> answers) : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default) =>
            request is CloseAbandonedWorkflowStartCommand ? Task.FromResult((TResponse)(object)answers.Dequeue()) : throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : INotification => throw new NotSupportedException();
    }

    [Fact]
    public async Task A_deferral_streak_starts_again_after_a_cycle_that_was_not_deferred()
    {
        var abandoned = await InsertHalfStartAsync(Request("streak"), withIds: true);
        await AgeAsync(abandoned.Id);
        var unavailable = Response<CloseAbandonedWorkflowStartResult>.Fail("no", 503, WorkflowReasonCodes.WorkflowTransitionConflict, "corr");
        var moved = Response<CloseAbandonedWorkflowStartResult>.Fail("moved", 409, WorkflowReasonCodes.WorkflowStartNotAbandoned, "corr");
        // 4 deferred, 1 not, 4 deferred: never 5 in a row.
        var script = new Queue<Response<CloseAbandonedWorkflowStartResult>>(
            [unavailable, unavailable, unavailable, unavailable, moved, unavailable, unavailable, unavailable, unavailable]);
        var metrics = new WorkflowStartRecoveryMetrics();
        var logs = new JobLog();

        for (var cycle = 0; cycle < 9; cycle++)
        {
            await Job(metrics, new ScriptedMediator(script), logs).HandleAsync(new WorkflowStartRecoverySweepJobArgs(0), JobContext());
        }

        Assert.Equal(4, metrics.DeferredStreak(abandoned.Id));
        Assert.DoesNotContain(logs.Warnings, l => l.Contains("deferred_persistent", StringComparison.Ordinal));
    }

    [Fact]
    public void Each_tenant_publishes_when_it_was_last_counted()
    {
        var clock = new ManualClock(DateTimeOffset.FromUnixTimeSeconds(1_800_000_000));
        using var metrics = new WorkflowStartRecoveryMetrics(clock);
        var tenant = Guid.NewGuid();
        metrics.Record(tenant, 0, 0, 0);
        var seen = new List<(string Instrument, long Value, string? Tenant)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == WorkflowStartRecoveryMetrics.MeterName)
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            string? tag = null;
            foreach (var t in tags)
            {
                if (t.Key == "tenant")
                {
                    tag = t.Value?.ToString();
                }
            }

            seen.Add((instrument.Name, value, tag));
        });
        listener.Start();

        listener.RecordObservableInstruments();

        Assert.Contains(seen, m => m.Instrument == "platform.workflow.start_recovery.last_counted_unix_seconds"
            && m.Value == 1_800_000_000 && m.Tenant == tenant.ToString("D"));
    }

    private TaskReviewService ReviewService(
        bool crashBeforeTask = false, Diten.Platform.Application.Contracts.ICurrentUserContext? user = null) =>
        new(new RecoveryMediator(this, crashBeforeTask, user: user), _templates, _instances, _tasks, _tenant, user ?? new StarterUser(),
            Options.Create(new TaskReviewOptions()), NullLogger<TaskReviewService>.Instance);

    private WorkflowStartRecoverySweepJob Job(WorkflowStartRecoveryMetrics metrics, IMediator mediator, JobLog log)
    {
        var scope = new TenantContext(); // the job sets each tenant's scope on it; the store reads the same one
        return new(new OneTenantRegistry(_tenant.TenantId), scope, new WorkflowStartRecoveryStore(_context, scope), mediator,
            metrics, log, Options.Create(new WorkflowStartRecoveryOptions { AbandonAfterMinutes = (int)Age.TotalMinutes }));
    }

    private static BackgroundJobContext JobContext() => new(TriggerType: BackgroundJobTriggerTypes.Recurring, TriggeredBy: "test");

    /// <summary>Captures what the job logs (the cycle line, "stuck", "still stuck").</summary>
    private sealed class JobLog : Microsoft.Extensions.Logging.ILogger<WorkflowStartRecoverySweepJob>
    {
        public System.Collections.Concurrent.ConcurrentQueue<string> Lines { get; } = new();
        public System.Collections.Concurrent.ConcurrentQueue<string> Errors { get; } = new();
        public System.Collections.Concurrent.ConcurrentQueue<string> Warnings { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var line = formatter(state, exception);
            Lines.Enqueue(line);
            if (logLevel == Microsoft.Extensions.Logging.LogLevel.Warning)
            {
                Warnings.Enqueue(line);
            }

            if (logLevel >= Microsoft.Extensions.Logging.LogLevel.Error)
            {
                Errors.Enqueue(line);
            }
        }
    }

    /// <summary>Answers every close with a fixed response.</summary>
    private sealed class AnsweringMediator(Response<CloseAbandonedWorkflowStartResult> answer) : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default) =>
            request is CloseAbandonedWorkflowStartCommand ? Task.FromResult((TResponse)(object)answer) : throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : INotification => throw new NotSupportedException();
    }

    /// <summary>The instance store, holding the FIRST call right after its instance was reserved (a paused first call).</summary>
    private sealed class HoldAfterReserveRepository(IWorkflowInstanceRepository inner, Hold hold) : IWorkflowInstanceRepository
    {
        public Task<WorkflowInstance> CreateAsync(WorkflowInstance value, CancellationToken ct = default) => inner.CreateAsync(value, ct);
        public Task<WorkflowInstance?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
        public Task<WorkflowInstance?> GetByIdempotencyKeyAsync(string key, CancellationToken ct = default) => inner.GetByIdempotencyKeyAsync(key, ct);
        public Task<WorkflowInstance?> GetLatestByObjectRefAsync(string objectRef, string objectType, string objectId, CancellationToken ct = default) =>
            inner.GetLatestByObjectRefAsync(objectRef, objectType, objectId, ct);
        public Task<IReadOnlyList<WorkflowInstance>> GetAllForTenantAsync(CancellationToken ct = default) => inner.GetAllForTenantAsync(ct);
        public Task<bool> UpdateAsync(WorkflowInstance value, int version, CancellationToken ct = default) => inner.UpdateAsync(value, version, ct);
        public async Task<(WorkflowInstance Instance, bool Created)> ReserveTrustedStartAsync(WorkflowInstance value, CancellationToken ct = default)
        {
            var reserved = await inner.ReserveTrustedStartAsync(value, ct);
            await hold.PassAsync();
            return reserved;
        }

        public Task<bool> ClaimStartCompletionAsync(Guid instanceId, int expectedVersion, Guid approvalTaskId, Guid assignmentSnapshotId,
            Guid startTransitionLogId, DateTimeOffset now, CancellationToken ct = default) =>
            inner.ClaimStartCompletionAsync(instanceId, expectedVersion, approvalTaskId, assignmentSnapshotId, startTransitionLogId, now, ct);
        public Task<bool> ClaimStartCompletionAsync(IPlatformTransactionSession session, Guid instanceId, int expectedVersion,
            Guid approvalTaskId, Guid assignmentSnapshotId, Guid startTransitionLogId, DateTimeOffset now, CancellationToken ct = default) =>
            inner.ClaimStartCompletionAsync(session, instanceId, expectedVersion, approvalTaskId, assignmentSnapshotId, startTransitionLogId, now, ct);
    }

    /// <summary>FIX2 item 1 — the task store, holding the start task's insert (inside the claim's transaction when there is one)
    /// until released: a pod paused after its claim.</summary>
    private sealed class HoldBeforeTaskInsertRepository(IApprovalTaskRepository inner, Hold hold) : IApprovalTaskRepository
    {
        public Task<ApprovalTask> CreateAsync(ApprovalTask task, CancellationToken ct = default) => inner.CreateAsync(task, ct);
        public async Task<ApprovalTask> CreateAsync(IPlatformTransactionSession session, ApprovalTask task, CancellationToken ct = default)
        {
            await hold.PassAsync();
            return await inner.CreateAsync(session, task, ct);
        }

        public async Task<ApprovalTask> EnsureTrustedStartTaskAsync(ApprovalTask task, CancellationToken ct = default)
        {
            await hold.PassAsync();
            return await inner.EnsureTrustedStartTaskAsync(task, ct);
        }

        public Task<ApprovalTask?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
        public Task<ApprovalTask?> GetFirstByInstanceIdAsync(Guid id, CancellationToken ct = default) => inner.GetFirstByInstanceIdAsync(id, ct);
        public Task<ApprovalTask?> GetActiveByInstanceIdAsync(Guid id, CancellationToken ct = default) => inner.GetActiveByInstanceIdAsync(id, ct);
        public Task<IReadOnlyList<ApprovalTask>> ListByInstanceIdAsync(Guid id, CancellationToken ct = default) => inner.ListByInstanceIdAsync(id, ct);
        public Task<IReadOnlyList<ApprovalTask>> GetAllForTenantAsync(CancellationToken ct = default) => inner.GetAllForTenantAsync(ct);
        public Task<bool> UpdateAsync(ApprovalTask task, int version, CancellationToken ct = default) => inner.UpdateAsync(task, version, ct);
    }

    /// <summary>The task store, holding the FIRST "list this instance's tasks" (the recovery's read) until released.</summary>
    private sealed class HoldingTaskListRepository(IApprovalTaskRepository inner, Hold hold) : IApprovalTaskRepository
    {
        public Task<ApprovalTask> CreateAsync(IPlatformTransactionSession session, ApprovalTask task, CancellationToken ct = default) => inner.CreateAsync(session, task, ct);
        public Task<ApprovalTask> CreateAsync(ApprovalTask task, CancellationToken ct = default) => inner.CreateAsync(task, ct);
        public Task<ApprovalTask> EnsureTrustedStartTaskAsync(ApprovalTask task, CancellationToken ct = default) => inner.EnsureTrustedStartTaskAsync(task, ct);
        public Task<ApprovalTask?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
        public Task<ApprovalTask?> GetFirstByInstanceIdAsync(Guid id, CancellationToken ct = default) => inner.GetFirstByInstanceIdAsync(id, ct);
        public Task<ApprovalTask?> GetActiveByInstanceIdAsync(Guid id, CancellationToken ct = default) => inner.GetActiveByInstanceIdAsync(id, ct);
        public async Task<IReadOnlyList<ApprovalTask>> ListByInstanceIdAsync(Guid id, CancellationToken ct = default)
        {
            var tasks = await inner.ListByInstanceIdAsync(id, ct);
            await hold.PassAsync();
            return tasks;
        }

        public Task<IReadOnlyList<ApprovalTask>> GetAllForTenantAsync(CancellationToken ct = default) => inner.GetAllForTenantAsync(ct);
        public Task<bool> UpdateAsync(ApprovalTask task, int version, CancellationToken ct = default) => inner.UpdateAsync(task, version, ct);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────────────

    private static string D(Guid id) => id.ToString("D");

    private StartWorkflowInstanceRequest Request(string key) => new(
        _templateId, null, "recovery-object", Guid.NewGuid().ToString("D"), null, [D(Approver)], "SUBMIT",
        $"recovery:{key}:{Guid.NewGuid():N}", false, false, null);

    private static StartWorkflowInstanceCommand Command(StartWorkflowInstanceRequest request) =>
        new(request, "corr-" + request.IdempotencyKey);

    private StartWorkflowInstanceHandler Handler(
        IApprovalTaskRepository? tasks = null,
        IRuntimeAssignmentSnapshotRepository? snapshots = null,
        IWorkflowInstanceRepository? instances = null,
        Diten.Platform.Application.Contracts.ICurrentUserContext? user = null) =>
        new(_templates, _versions, instances ?? _instances, tasks ?? _tasks, snapshots ?? _snapshots, _logs, _tenant, user ?? new StarterUser(),
            transactions: new PlatformTransactionExecutor(_context));


    private SweepWorkflowOrphansHandler OrphanHandler() =>
        new(_store, _instances, _tasks, _taskItems, new RecoveryMediator(this));

    private TaskApprovalService ApprovalService(
        bool crashBeforeTask = false, Diten.Platform.Application.Contracts.ICurrentUserContext? user = null) =>
        new(new RecoveryMediator(this, crashBeforeTask, user: user), _templates, _instances, _tasks, _tenant, user ?? new StarterUser(),
            Options.Create(new TaskApprovalOptions()), NullLogger<TaskApprovalService>.Instance);

    private async Task SweepAsync(WorkflowStartRecoveryMetrics metrics, params Guid[] moreTenants)
    {
        var tenantId = _tenant.TenantId;
        var context = new TenantContext(); // the job sets each tenant's scope itself
        var job = new WorkflowStartRecoverySweepJob(
            new OneTenantRegistry([tenantId, .. moreTenants]), context,
            new WorkflowStartRecoveryStore(_context, context),
            new RecoveryMediator(this, scope: context),
            metrics,
            NullLogger<WorkflowStartRecoverySweepJob>.Instance,
            Options.Create(new WorkflowStartRecoveryOptions { AbandonAfterMinutes = (int)Age.TotalMinutes }));
        await job.HandleAsync(new WorkflowStartRecoverySweepJobArgs(0),
            new BackgroundJobContext(TriggerType: BackgroundJobTriggerTypes.Recurring, TriggeredBy: "test"), default);
    }

    private Task<List<WorkflowInstance>> InstancesOfAsync(StartWorkflowInstanceRequest request) =>
        _context.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances)
            .Find(i => i.IdempotencyKey == request.IdempotencyKey).ToListAsync();

    /// <summary>What a crash right after the instance write leaves: an Active instance and nothing else — with the ids a
    /// start since BL-549 writes on it, or without them (a start before).</summary>
    private async Task<WorkflowInstance> InsertHalfStartAsync(
        StartWorkflowInstanceRequest request, bool withIds, Guid? tenantId = null)
    {
        var template = (await _templates.GetByIdAsync(_templateId))!;
        var instance = new WorkflowInstance
        {
            TenantId = tenantId ?? _tenant.TenantId,
            TemplateId = _templateId,
            WorkflowTemplateId = _templateId,
            TemplateVersionId = template.ActivePublishedVersionId,
            ObjectType = request.ObjectType,
            ObjectId = request.ObjectId,
            ObjectRef = request.ObjectRef ?? $"{request.ObjectType}|{request.ObjectId}",
            Status = WorkflowInstanceStatus.Active,
            IdempotencyKey = request.IdempotencyKey,
            StartedBy = "starter@example.test",
            StartedByUserId = Starter,
            StartedAt = DateTimeOffset.UtcNow,
            LastTransitionAt = DateTimeOffset.UtcNow,
            InitialApprovalTaskId = withIds ? Guid.NewGuid() : null,
            InitialAssignmentSnapshotId = withIds ? Guid.NewGuid() : null,
            StartTransitionLogId = withIds ? Guid.NewGuid() : null
        };
        await _context.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances).InsertOneAsync(instance);
        return instance;
    }

    private async Task<Guid> InsertIncompleteTrustedStartAsync(TimeSpan reservedAgo)
    {
        var instance = new WorkflowInstance
        {
            TenantId = _tenant.TenantId,
            TemplateId = _templateId,
            WorkflowTemplateId = _templateId,
            ObjectType = "GlobalProduct",
            ObjectId = Guid.NewGuid().ToString("D"),
            ObjectRef = "GlobalProduct|" + Guid.NewGuid().ToString("N"),
            Status = WorkflowInstanceStatus.Pending,
            StartCheckpoint = WorkflowStartCheckpoint.Reserved,
            TrustedConsumerClientId = Guid.NewGuid(),
            DelegatedMakerUserId = Guid.NewGuid(),
            IdempotencyKey = "trusted:" + Guid.NewGuid().ToString("N"),
            CreatedAt = DateTimeOffset.UtcNow - reservedAgo,
            StartedAt = DateTimeOffset.UtcNow - reservedAgo
        };
        await _context.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances).InsertOneAsync(instance);
        return instance.Id;
    }

    private Task AgeAsync(Guid instanceId)
    {
        var then = DateTimeOffset.UtcNow.AddHours(-2);
        return _context.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances).UpdateOneAsync(
            i => i.Id == instanceId,
            Builders<WorkflowInstance>.Update.Set(i => i.LastTransitionAt, then).Set(i => i.StartedAt, then));
    }

    private Task<long> CompletionEventsAsync(Guid instanceId) =>
        _context.Database.GetCollection<BsonDocument>(IntentWriter.Collection).CountDocumentsAsync(
            Builders<BsonDocument>.Filter.Eq("WorkflowInstanceId", instanceId.ToString("D")));

    private async Task<Guid> StartForAsync(string objectType, Guid taskId, string key, bool startedLater = false)
    {
        var started = await Handler().Handle(Command(new StartWorkflowInstanceRequest(
            _templateId, null, objectType, taskId.ToString(), $"tasks|{objectType}|{taskId}", [D(Approver)], null,
            $"{key}:{Guid.NewGuid():N}", false, false, null)), default);
        Assert.True(started.IsSuccessful, started.ReasonCode);
        if (startedLater)
        {
            await _context.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances).UpdateOneAsync(
                i => i.Id == started.Data!.WorkflowInstanceId,
                Builders<WorkflowInstance>.Update.Set(i => i.StartedAt, DateTimeOffset.UtcNow.AddMinutes(5)));
        }

        return started.Data!.WorkflowInstanceId;
    }

    private Task<TaskItem> SaveTaskItemAsync() => _taskItems.CreateAsync(new TaskItem
    {
        TenantId = _tenant.TenantId,
        Title = "Linked work",
        AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
        OrganizationUnitId = Guid.NewGuid()
    });

    private static readonly Guid TrustedClient = Guid.Parse("0c000000-0000-4000-8000-0000000000c1");
    private static readonly Guid Maker = Guid.Parse("0c000000-0000-4000-8000-0000000000c2");

    private WorkflowInstanceStartCoordinator TrustedCoordinator(
        IApprovalTaskRepository? tasks = null,
        IRuntimeAssignmentSnapshotRepository? snapshots = null,
        IWorkflowTransitionLogRepository? logs = null) =>
        new(_templates, _versions, _instances, tasks ?? _tasks, snapshots ?? _snapshots, logs ?? _logs, _tenant);

    /// <summary>A trusted start (as MDM makes one) that crashed at the repository that throws.</summary>
    private async Task<(TrustedWorkflowStartRequest Request, Guid ObjectKey)> CrashedReservationAsync(
        string key,
        IApprovalTaskRepository? tasks = null,
        IRuntimeAssignmentSnapshotRepository? snapshots = null,
        IWorkflowTransitionLogRepository? logs = null)
    {
        var request = new TrustedWorkflowStartRequest(_templateId, null, "GlobalProduct", "GP-" + Guid.NewGuid().ToString("N"), null,
            [D(Approver)], "SUBMIT", $"{key}:{Guid.NewGuid():N}", false, false, null);
        await Assert.ThrowsAsync<InjectedCrashException>(() =>
            TrustedCoordinator(tasks, snapshots, logs).StartAsync(request, TrustedClient, Maker, "corr-" + key));
        return (request, Guid.Empty);
    }

    private Task<List<WorkflowInstance>> InstancesOfKeyAsync(string key) =>
        _context.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances)
            .Find(i => i.IdempotencyKey == key).ToListAsync();

    private Task AgeReservationAsync(Guid instanceId)
    {
        var then = DateTimeOffset.UtcNow.AddHours(-2);
        return _context.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances).UpdateOneAsync(
            i => i.Id == instanceId,
            Builders<WorkflowInstance>.Update.Set(i => i.UpdatedAt, then).Set(i => i.CreatedAt, then));
    }

    private Task<Response<TrustedWorkflowStartResult>> StartResultAsync(string key, string objectId) =>
        new Diten.Platform.Application.Features.Workflow.Handlers.QueryHandlers.GetTrustedWorkflowStartResultHandler(
                _instances, _tasks, _snapshots, _logs)
            .Handle(new Diten.Platform.Application.Features.Workflow.Queries.GetTrustedWorkflowStartResultQuery(
                key, TrustedClient, Maker, "GlobalProduct", objectId, "corr-wire"), default);

    /// <summary>What goes on the wire: the status and the serialized body (the correlation id is the caller's, the same here).</summary>
    private static string Wire(Response<TrustedWorkflowStartResult> response) =>
        response.StatusCode + " " + System.Text.Json.JsonSerializer.Serialize(response,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

    private async Task<IReadOnlyList<WorkflowTaskDto>> MyWorkflowTasksAsync(Guid userId) =>
        (await new Diten.Platform.Application.Features.Workflow.Handlers.QueryHandlers.GetMyWorkflowTasksHandler(
                _tasks, _snapshots, new UserOf(userId))
            .Handle(new Diten.Platform.Application.Features.Workflow.Queries.GetMyWorkflowTasksQuery("corr"), default)).Data!;

    private CloseAbandonedWorkflowStartHandler CloseHandler(IApprovalTaskRepository? tasks = null, IApprovalTaskRepository? taskList = null) =>
        CloseHandlerOver(taskList ?? tasks);

    private CloseAbandonedWorkflowStartHandler CloseHandlerOver(IApprovalTaskRepository? tasks) =>
        new(_instances, tasks ?? _tasks, _logs, new PlatformTransactionExecutor(_context), new IntentWriter(_context), _templates);

    private sealed class UserOf(Guid userId) : Diten.Platform.Application.Contracts.ICurrentUserContext
    {
        public Guid UserId { get; } = userId;
        public string? Email => "user@example.test";
        public string? DisplayName => "User";
        public string ActorName => Email!;
        public bool IsAuthenticated => true;
    }

    /// <summary>Holds the FIRST call of a repository read until the test releases it.</summary>
    private sealed class Hold
    {
        private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _taken;

        public async Task PassAsync()
        {
            if (Interlocked.Exchange(ref _taken, 1) == 0)
            {
                _reached.TrySetResult();
                await _release.Task.WaitAsync(TimeSpan.FromSeconds(30));
            }
        }

        public Task ReachedAsync() => _reached.Task.WaitAsync(TimeSpan.FromSeconds(30));
        public void Release() => _release.TrySetResult();
    }

    private sealed class HoldingTaskRepository(IApprovalTaskRepository inner, Hold hold) : IApprovalTaskRepository
    {
        public Task<ApprovalTask> CreateAsync(IPlatformTransactionSession session, ApprovalTask task, CancellationToken ct = default) => inner.CreateAsync(session, task, ct);
        public Task<ApprovalTask> CreateAsync(ApprovalTask task, CancellationToken ct = default) => inner.CreateAsync(task, ct);
        public Task<ApprovalTask> EnsureTrustedStartTaskAsync(ApprovalTask task, CancellationToken ct = default) => inner.EnsureTrustedStartTaskAsync(task, ct);
        public async Task<ApprovalTask?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            await hold.PassAsync();
            return await inner.GetByIdAsync(id, ct);
        }

        public Task<ApprovalTask?> GetFirstByInstanceIdAsync(Guid id, CancellationToken ct = default) => inner.GetFirstByInstanceIdAsync(id, ct);
        public Task<ApprovalTask?> GetActiveByInstanceIdAsync(Guid id, CancellationToken ct = default) => inner.GetActiveByInstanceIdAsync(id, ct);
        public Task<IReadOnlyList<ApprovalTask>> ListByInstanceIdAsync(Guid id, CancellationToken ct = default) => inner.ListByInstanceIdAsync(id, ct);
        public Task<IReadOnlyList<ApprovalTask>> GetAllForTenantAsync(CancellationToken ct = default) => inner.GetAllForTenantAsync(ct);
        public Task<bool> UpdateAsync(ApprovalTask task, int version, CancellationToken ct = default) => inner.UpdateAsync(task, version, ct);
    }

    private sealed class HoldingSnapshotRepository(IRuntimeAssignmentSnapshotRepository inner, Hold hold) : IRuntimeAssignmentSnapshotRepository
    {
        public Task<RuntimeAssignmentSnapshot> CreateAsync(RuntimeAssignmentSnapshot value, CancellationToken ct = default) => inner.CreateAsync(value, ct);
        public Task<RuntimeAssignmentSnapshot> EnsureTrustedStartSnapshotAsync(RuntimeAssignmentSnapshot value, CancellationToken ct = default) => inner.EnsureTrustedStartSnapshotAsync(value, ct);
        public async Task<RuntimeAssignmentSnapshot?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            await hold.PassAsync();
            return await inner.GetByIdAsync(id, ct);
        }

        public Task<IReadOnlyList<RuntimeAssignmentSnapshot>> ListByInstanceIdAsync(Guid id, CancellationToken ct = default) => inner.ListByInstanceIdAsync(id, ct);
    }

    /// <summary>A crash right before the start log write: instance, task and snapshot are stored.</summary>
    private sealed class CrashBeforeLogRepository(IWorkflowTransitionLogRepository inner) : IWorkflowTransitionLogRepository
    {
        public Task<WorkflowTransitionLog> CreateAsync(WorkflowTransitionLog log, CancellationToken ct = default) => throw new InjectedCrashException();
        public Task<WorkflowTransitionLog> EnsureTrustedStartLogAsync(WorkflowTransitionLog log, CancellationToken ct = default) => throw new InjectedCrashException();
        public Task<WorkflowTransitionLog> AppendAsync(WorkflowTransitionLog log, CancellationToken ct = default) => inner.AppendAsync(log, ct);
        public Task<WorkflowTransitionLog?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
        public Task<WorkflowTransitionLog?> GetByTaskActionIdempotencyKeyAsync(Guid taskId, WorkflowTransitionAction action, string key, CancellationToken ct = default) => inner.GetByTaskActionIdempotencyKeyAsync(taskId, action, key, ct);
        public Task<long> GetLatestSequenceNoAsync(Guid id, CancellationToken ct = default) => inner.GetLatestSequenceNoAsync(id, ct);
        public Task<IReadOnlyList<WorkflowTransitionLog>> ListByInstanceIdAsync(Guid id, CancellationToken ct = default) => inner.ListByInstanceIdAsync(id, ct);
    }

    private sealed class InjectedCrashException : Exception;

    private sealed class StarterUser : Diten.Platform.Application.Contracts.ICurrentUserContext
    {
        public Guid UserId => Starter;
        public string? Email => "starter@example.test";
        public string? DisplayName => "Starter";
        public string ActorName => Email!;
        public bool IsAuthenticated => true;
    }

    /// <summary>The engine's own handlers behind the commands the recovery sends, the commands passing through the REAL
    /// <see cref="AuditBehavior{TRequest,TResponse}"/> (no principal → System). With <paramref name="scope"/> the handlers
    /// read the tenant the job set; with <paramref name="crashBeforeTask"/> a start crashes right after its instance.</summary>
    private sealed class RecoveryMediator(
        WorkflowStartRecoveryMongoTests t, bool crashBeforeTask = false, TenantContext? scope = null,
        Diten.Platform.Application.Contracts.ICurrentUserContext? user = null) : IMediator
    {
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            var tenant = scope ?? t._tenant;
            var instances = new WorkflowInstanceRepository(t._context, tenant);
            IApprovalTaskRepository tasks = new ApprovalTaskRepository(t._context, tenant);
            var snapshots = new RuntimeAssignmentSnapshotRepository(t._context, tenant);
            var logs = new WorkflowTransitionLogRepository(t._context, tenant);
            var templates = new WorkflowTemplateRepository(t._context, tenant);
            object response = request switch
            {
                StartWorkflowInstanceCommand start => await new StartWorkflowInstanceHandler(
                        templates, new WorkflowTemplateVersionRepository(t._context, tenant), instances,
                        crashBeforeTask ? new CrashBeforeTaskRepository(tasks) : tasks, snapshots, logs, tenant, user ?? new StarterUser(),
                        transactions: new PlatformTransactionExecutor(t._context))
                    .Handle(start, ct),
                SupersedeWorkflowHalfStartCommand supersede => await Audited(supersede, () => new SupersedeWorkflowHalfStartHandler(
                        instances, tasks, logs, user ?? new StarterUser(), new PlatformTransactionExecutor(t._context),
                        new IntentWriter(t._context), templates)
                    .Handle(supersede, ct)),
                CreateWorkflowDefinitionCommand create => await new CreateWorkflowDefinitionHandler(templates, tenant).Handle(create, ct),
                PublishWorkflowDefinitionCommand publish => await new PublishWorkflowDefinitionHandler(
                        templates, new WorkflowTemplateVersionRepository(t._context, tenant), tenant, new StarterUser())
                    .Handle(publish, ct),
                CloseAbandonedWorkflowStartCommand close => await Audited(close, () => new CloseAbandonedWorkflowStartHandler(
                        instances, tasks, logs, new PlatformTransactionExecutor(t._context), new IntentWriter(t._context), templates)
                    .Handle(close, ct)),
                SweepWorkflowOrphansCommand sweep => await Audited(sweep, () => new SweepWorkflowOrphansHandler(
                        new WorkflowStartRecoveryStore(t._context, tenant), instances, tasks,
                        new TaskItemRepository(t._context, tenant, new TaskTransitionRepository(t._context, tenant)), this)
                    .Handle(sweep, ct)),
                CancelWorkflowTaskCommand cancel => await Audited(cancel, () => new CancelWorkflowTaskHandler(
                        tasks, instances, snapshots, logs, new PlatformTransactionExecutor(t._context), new IntentWriter(t._context), templates)
                    .Handle(cancel, ct)),
                _ => throw new NotSupportedException(request.GetType().Name)
            };
            return (TResponse)response;
        }

        private Task<TResponse> Audited<TRequest, TResponse>(TRequest request, Func<Task<TResponse>> handle) where TRequest : notnull =>
            new AuditBehavior<TRequest, TResponse>(t._audit, new AnonymousTenantAuthorizationContext(), new AuditBehaviorOptions(),
                NullLogger<AuditBehavior<TRequest, TResponse>>.Instance).Handle(request, () => handle(), default);

        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : INotification => throw new NotSupportedException();
    }

    private sealed class OneTenantRegistry(params Guid[] tenantIds) : ITenantRegistryRepository
    {
        public Task<IReadOnlyList<Tenant>> GetActiveTenantsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Tenant>>(tenantIds.Select(tenantId => new Tenant
            {
                Id = tenantId, Code = "T-" + tenantId.ToString("N")[..8], Slug = "t-" + tenantId.ToString("N")[..8], Name = "Tenant",
                DisplayName = "Tenant", Domain = tenantId.ToString("N") + ".example", Status = TenantStatus.Active
            }).ToList());

        public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Tenant?> GetByCodeAsync(string code, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Tenant?> GetByDomainAsync(string domain, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Tenant> CreateAsync(Tenant tenant, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(Tenant tenant, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateStatusAsync(Guid id, TenantStatus status, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<Tenant> Items, long TotalCount)> QueryAsync(TenantListQuery query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TenantRegistryStats> GetStatsAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }

    /// <summary>The completion event's outbox row, written in the caller's transaction.</summary>
    private sealed class IntentWriter(IPlatformDbContext context) : ITransactionalIntegrationEventWriter
    {
        public const string Collection = "recovery_completion_intents";

        public async Task<EventEnvelope<TEvent>> EnqueueAsync<TEvent>(IPlatformTransactionSession session, TEvent @event,
            EventPublishOptions options, CancellationToken cancellationToken = default) where TEvent : IIntegrationEvent
        {
            var completed = Assert.IsType<Diten.Platform.Application.Features.Workflow.Events.WorkflowInstanceCompletedV1>(@event);
            var metadata = new EventMetadata(options.EventId!.Value, @event.EventName, @event.EventVersion,
                options.CorrelationId!.Value, null, options.TenantId, options.Producer!, options.OccurredAtUtc!.Value);
            await context.Database.GetCollection<BsonDocument>(Collection).InsertOneAsync(
                PlatformMongoTransactionSession.Require(session, context),
                new BsonDocument
                {
                    ["EventId"] = metadata.EventId.ToString(),
                    ["WorkflowInstanceId"] = completed.WorkflowInstanceId.ToString("D")
                },
                cancellationToken: cancellationToken);
            return new EventEnvelope<TEvent>(metadata, @event);
        }
    }

    /// <summary>A crash right before the task write: the instance is stored, nothing after it.</summary>
    private sealed class CrashBeforeTaskRepository(IApprovalTaskRepository inner) : IApprovalTaskRepository
    {
        public Task<ApprovalTask> CreateAsync(ApprovalTask task, CancellationToken ct = default) => throw new InjectedCrashException();
        public Task<ApprovalTask> EnsureTrustedStartTaskAsync(ApprovalTask task, CancellationToken ct = default) => throw new InjectedCrashException();
        public Task<ApprovalTask?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
        public Task<ApprovalTask?> GetFirstByInstanceIdAsync(Guid id, CancellationToken ct = default) => inner.GetFirstByInstanceIdAsync(id, ct);
        public Task<ApprovalTask?> GetActiveByInstanceIdAsync(Guid id, CancellationToken ct = default) => inner.GetActiveByInstanceIdAsync(id, ct);
        public Task<IReadOnlyList<ApprovalTask>> ListByInstanceIdAsync(Guid id, CancellationToken ct = default) => inner.ListByInstanceIdAsync(id, ct);
        public Task<IReadOnlyList<ApprovalTask>> GetAllForTenantAsync(CancellationToken ct = default) => inner.GetAllForTenantAsync(ct);
        public Task<bool> UpdateAsync(ApprovalTask task, int version, CancellationToken ct = default) => inner.UpdateAsync(task, version, ct);
    }

    /// <summary>A crash right before the snapshot write: the instance and the task are stored.</summary>
    private sealed class CrashBeforeSnapshotRepository(IRuntimeAssignmentSnapshotRepository inner) : IRuntimeAssignmentSnapshotRepository
    {
        public Task<RuntimeAssignmentSnapshot> CreateAsync(RuntimeAssignmentSnapshot value, CancellationToken ct = default) => throw new InjectedCrashException();
        public Task<RuntimeAssignmentSnapshot> EnsureTrustedStartSnapshotAsync(RuntimeAssignmentSnapshot value, CancellationToken ct = default) => throw new InjectedCrashException();
        public Task<RuntimeAssignmentSnapshot?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
        public Task<IReadOnlyList<RuntimeAssignmentSnapshot>> ListByInstanceIdAsync(Guid id, CancellationToken ct = default) => inner.ListByInstanceIdAsync(id, ct);
    }

    /// <summary>Both replays read the instance's task (none) and only then go on — so both have decided to complete before
    /// either writes.</summary>
    private sealed class TwoReaderGate
    {
        private readonly TaskCompletionSource _both = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _readers;

        public bool BothRead => _both.Task.IsCompletedSuccessfully;

        public async Task ArriveAsync()
        {
            if (Interlocked.Increment(ref _readers) == 2)
            {
                _both.TrySetResult();
            }

            await Task.WhenAny(_both.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        }
    }

    private sealed class GatedTaskRepository(IApprovalTaskRepository inner, TwoReaderGate gate) : IApprovalTaskRepository
    {
        private bool _passed;

        public Task<ApprovalTask> CreateAsync(IPlatformTransactionSession session, ApprovalTask task, CancellationToken ct = default) => inner.CreateAsync(session, task, ct);
        public Task<ApprovalTask> CreateAsync(ApprovalTask task, CancellationToken ct = default) => inner.CreateAsync(task, ct);
        public Task<ApprovalTask> EnsureTrustedStartTaskAsync(ApprovalTask task, CancellationToken ct = default) => inner.EnsureTrustedStartTaskAsync(task, ct);
        public Task<ApprovalTask?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
        public async Task<ApprovalTask?> GetFirstByInstanceIdAsync(Guid id, CancellationToken ct = default)
        {
            var task = await inner.GetFirstByInstanceIdAsync(id, ct);
            if (!_passed)
            {
                _passed = true;
                await gate.ArriveAsync();
            }

            return task;
        }

        public Task<ApprovalTask?> GetActiveByInstanceIdAsync(Guid id, CancellationToken ct = default) => inner.GetActiveByInstanceIdAsync(id, ct);
        public Task<IReadOnlyList<ApprovalTask>> ListByInstanceIdAsync(Guid id, CancellationToken ct = default) => inner.ListByInstanceIdAsync(id, ct);
        public Task<IReadOnlyList<ApprovalTask>> GetAllForTenantAsync(CancellationToken ct = default) => inner.GetAllForTenantAsync(ct);
        public Task<bool> UpdateAsync(ApprovalTask task, int version, CancellationToken ct = default) => inner.UpdateAsync(task, version, ct);
    }
}
