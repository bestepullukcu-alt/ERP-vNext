using System.Collections.Concurrent;
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
/// MOD-0023 §17 DL-2 — the start side of the same-key answer, over the real repositories on the disposable replica set:
/// AC-W5 (forced overlap), AC-W9 (a repeat after each terminal state), AC-W11 (same key, different facts or starter;
/// the <c>idempotentReplay</c> flag), AC-W12 (a closed never-whole start). Every rule through the production handler.
/// </summary>
public sealed partial class WorkflowStartRecoveryMongoTests
{
    // ── AC-W5 · every caller missed the key, one reservation wins ───────────────────────────────────────────────

    /// <summary>One counter shared by the N callers of a round. (a) = callers whose key look-up found NOTHING; (b) = callers
    /// that reached the reservation. The release comes when (b) = N, or after 10 s; which of the two, and (a) at that
    /// instant, are written down when it happens — later reads move the counts, so the final count is no proof.</summary>
    private sealed class ReserveHoldState(int callers)
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _missed;
        private int _reserving;
        private int _created;
        private int _notCreated;
        private int _released;
        private int _order;
        public int MissedKey => Volatile.Read(ref _missed);
        public int Reserving => Volatile.Read(ref _reserving);
        public int Created => Volatile.Read(ref _created);
        public int NotCreated => Volatile.Read(ref _notCreated);
        public bool ReleasedByArrival { get; private set; }
        public bool ReleasedByTimeout { get; private set; }
        public int MissedAtRelease { get; private set; } = -1;
        public ConcurrentDictionary<int, int> EarlyReturners { get; } = new();

        public int NextOrder() => Interlocked.Increment(ref _order);
        public void KeyMissed() => Interlocked.Increment(ref _missed);

        public async Task ReservingAsync()
        {
            if (Interlocked.Increment(ref _reserving) == callers)
            {
                ReleaseWith(byArrival: true);
            }

            if (await Task.WhenAny(_release.Task, Task.Delay(TimeSpan.FromSeconds(10))) != _release.Task)
            {
                ReleaseWith(byArrival: false);
            }
        }

        public void Counted(bool created)
        {
            if (created)
            {
                Interlocked.Increment(ref _created);
            }
            else
            {
                Interlocked.Increment(ref _notCreated);
            }
        }

        private void ReleaseWith(bool byArrival)
        {
            if (Interlocked.Exchange(ref _released, 1) != 0)
            {
                return;
            }

            MissedAtRelease = MissedKey; // taken at the instant of the release, before anything else moves
            ReleasedByArrival = byArrival;
            ReleasedByTimeout = !byArrival;
            _release.TrySetResult();
        }
    }

    /// <summary>The real instance store, holding each caller between its key look-up and its reservation. Counts through ONE
    /// shared state; forwards every member (the claim included), so the write under test is the real one.</summary>
    private sealed class ReserveHold(IWorkflowInstanceRepository inner, ReserveHoldState state, int caller) : IWorkflowInstanceRepository
    {
        public Task<WorkflowInstance> CreateAsync(WorkflowInstance value, CancellationToken ct = default) => inner.CreateAsync(value, ct);
        public Task<WorkflowInstance?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);

        public async Task<WorkflowInstance?> GetByIdempotencyKeyAsync(string key, CancellationToken ct = default)
        {
            var found = await inner.GetByIdempotencyKeyAsync(key, ct);
            if (found is null)
            {
                state.KeyMissed();
            }
            else
            {
                state.EarlyReturners[caller] = state.NextOrder(); // saw the key before reserving: the race was not forced
            }

            return found;
        }

        public Task<WorkflowInstance?> GetLatestByObjectRefAsync(string objectRef, string objectType, string objectId, CancellationToken ct = default) =>
            inner.GetLatestByObjectRefAsync(objectRef, objectType, objectId, ct);
        public Task<IReadOnlyList<WorkflowInstance>> GetAllForTenantAsync(CancellationToken ct = default) => inner.GetAllForTenantAsync(ct);
        public Task<bool> UpdateAsync(WorkflowInstance value, int version, CancellationToken ct = default) => inner.UpdateAsync(value, version, ct);

        public async Task<(WorkflowInstance Instance, bool Created)> ReserveTrustedStartAsync(WorkflowInstance value, CancellationToken ct = default)
        {
            await state.ReservingAsync();
            var reserved = await inner.ReserveTrustedStartAsync(value, ct);
            state.Counted(reserved.Created);
            return reserved;
        }

        public Task<bool> ClaimStartCompletionAsync(Guid instanceId, int expectedVersion, Guid approvalTaskId, Guid assignmentSnapshotId,
            Guid startTransitionLogId, DateTimeOffset now, CancellationToken ct = default) =>
            inner.ClaimStartCompletionAsync(instanceId, expectedVersion, approvalTaskId, assignmentSnapshotId, startTransitionLogId, now, ct);
        public Task<bool> ClaimStartCompletionAsync(IPlatformTransactionSession session, Guid instanceId, int expectedVersion,
            Guid approvalTaskId, Guid assignmentSnapshotId, Guid startTransitionLogId, DateTimeOffset now, CancellationToken ct = default) =>
            inner.ClaimStartCompletionAsync(session, instanceId, expectedVersion, approvalTaskId, assignmentSnapshotId, startTransitionLogId, now, ct);
    }

    [Fact]
    public async Task AC_W5_forced_overlap_every_caller_missed_the_key_and_one_reservation_wins()
    {
        const int callers = 6;
        for (var round = 1; round <= 3; round++)
        {
            var request = Request("w5-forced-" + round);
            var state = new ReserveHoldState(callers);

            var firstAnswers = await Task.WhenAll(Enumerable.Range(0, callers).Select(caller => Task.Run(() =>
                Handler(instances: new ReserveHold(_instances, state, caller)).Handle(Command(request), default))));

            // A 503 is the retryable answer of a lost claim: the same key is sent again until the start answers.
            var finals = new List<Response<StartWorkflowInstanceResponse>>();
            foreach (var answer in firstAnswers)
            {
                var current = answer;
                for (var retry = 0; retry < 5 && !current.IsSuccessful && current.StatusCode == 503; retry++)
                {
                    current = await Handler().Handle(Command(request), default);
                }

                finals.Add(current);
            }

            var early = string.Join("; ", state.EarlyReturners.OrderBy(e => e.Value).Select(e =>
                $"caller {e.Key} (order {e.Value}) answered {firstAnswers[e.Key].StatusCode} {firstAnswers[e.Key].ReasonCode}"));
            Assert.True(state.ReleasedByArrival && !state.ReleasedByTimeout,
                $"round {round}: released by the 10 s timeout, not by arrival — (a)={state.MissedKey} (b)={state.Reserving}; "
                + $"callers that found the key before reserving: {early}");
            Assert.Equal(callers, state.MissedAtRelease);   // at the instant of the release every caller had missed the key
            Assert.Equal(callers, state.MissedKey);          // and no later read changed it
            Assert.Equal(callers, state.Reserving);
            Assert.Equal(1, state.Created);                  // exactly one reservation won
            Assert.Equal(callers - 1, state.NotCreated);

            var instance = Assert.Single(await InstancesOfAsync(request));
            var task = Assert.Single(await _tasks.ListByInstanceIdAsync(instance.Id));
            Assert.Single(await _snapshots.ListByInstanceIdAsync(instance.Id));
            Assert.Single(await _logs.ListByInstanceIdAsync(instance.Id));
            Assert.All(finals, f =>
            {
                Assert.True(f.IsSuccessful, $"round {round}: {f.StatusCode} {f.ReasonCode} — a business refusal");
                Assert.Equal(instance.Id, f.Data!.WorkflowInstanceId);
                Assert.Equal(task.Id, f.Data.ApprovalTaskId);
            });
        }
    }

    // ── AC-W9 · a repeat after each terminal state ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(WorkflowInstanceStatus.Completed)]
    [InlineData(WorkflowInstanceStatus.Rejected)]
    [InlineData(WorkflowInstanceStatus.Cancelled)]
    [InlineData(WorkflowInstanceStatus.TimedOut)]
    public async Task AC_W9_replay_after_each_terminal_state_answers_200_same_instance(WorkflowInstanceStatus terminal)
    {
        var request = terminal == WorkflowInstanceStatus.TimedOut
            ? Request("w9-" + terminal) with { DueAt = DateTimeOffset.UtcNow.AddMinutes(-5) }
            : Request("w9-" + terminal);
        var first = await Handler().Handle(Command(request), default);
        Assert.True(first.IsSuccessful, first.ReasonCode);
        Assert.False(first.Data!.IdempotentReplay);
        var instanceId = first.Data.WorkflowInstanceId;
        var taskId = first.Data.ApprovalTaskId;

        await DriveToAsync(terminal, taskId, request.IdempotencyKey!);

        var stored = (await _instances.GetByIdAsync(instanceId))!;
        Assert.Equal(terminal, stored.Status); // non-vacuity: the instance really is in that state before the repeat
        var before = await CountsAsync(instanceId);

        var replay = await Handler().Handle(Command(request), default);

        Assert.True(replay.IsSuccessful, $"{replay.StatusCode} {replay.ReasonCode}");
        Assert.Equal(instanceId, replay.Data!.WorkflowInstanceId);
        Assert.Equal(taskId, replay.Data.ApprovalTaskId);
        Assert.Equal(terminal.ToString(), replay.Data.Status);
        Assert.True(replay.Data.IdempotentReplay);
        Assert.Equal(before, await CountsAsync(instanceId));                       // no log, no task, no second instance
        Assert.Single(await InstancesOfAsync(request));
    }

    private async Task<(int Instances, int Tasks, int Logs, int Version)> CountsAsync(Guid instanceId) =>
        ((int)await _context.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances)
                .CountDocumentsAsync(i => i.TenantId == _tenant.TenantId),
            (await _tasks.ListByInstanceIdAsync(instanceId)).Count,
            (await _logs.ListByInstanceIdAsync(instanceId)).Count,
            (await _instances.GetByIdAsync(instanceId))!.Version);

    private async Task DriveToAsync(WorkflowInstanceStatus terminal, Guid taskId, string key)
    {
        var executor = new PlatformTransactionExecutor(_context);
        var events = new IntentWriter(_context);
        switch (terminal)
        {
            case WorkflowInstanceStatus.Completed:
                var approved = await new ApproveWorkflowTaskHandler(_tasks, _instances, _snapshots, _logs, _versions,
                        transactions: executor, events: events, templates: _templates)
                    .Handle(new ApproveWorkflowTaskCommand(taskId,
                        new ApproveWorkflowTaskRequest(D(Approver), "APPROVED", key + ":decide", null, null), "corr-w9"), default);
                Assert.True(approved.IsSuccessful, approved.ReasonCode);
                break;
            case WorkflowInstanceStatus.Rejected:
                var rejected = await new RejectWorkflowTaskHandler(_tasks, _instances, _snapshots, _logs,
                        transactions: executor, events: events, templates: _templates)
                    .Handle(new RejectWorkflowTaskCommand(taskId,
                        new RejectWorkflowTaskRequest(D(Approver), "REJECTED", key + ":decide", "no", null), "corr-w9"), default);
                Assert.True(rejected.IsSuccessful, rejected.ReasonCode);
                break;
            case WorkflowInstanceStatus.Cancelled:
                var cancelled = await new CancelWorkflowTaskHandler(_tasks, _instances, _snapshots, _logs, executor, events, _templates)
                    .Handle(new CancelWorkflowTaskCommand(taskId,
                        new CancelWorkflowTaskRequest(D(Starter), "WITHDRAWN", key + ":decide", null), "corr-w9"), default);
                Assert.True(cancelled.IsSuccessful, cancelled.ReasonCode);
                break;
            case WorkflowInstanceStatus.TimedOut:
                await new SlaEscalationRuleRepository(_context, _tenant).CreateAsync(new SlaEscalationRule
                {
                    TenantId = _tenant.TenantId,
                    TemplateId = _templateId,
                    StageCode = "stage-1",
                    StepCode = "step-1",
                    DueInMinutes = 10,
                    EscalateAfterMinutes = 10,
                    TimeoutAfterMinutes = 10,
                    EscalationPrincipalIds = [D(OtherApprover)]
                });
                var run = await new RunWorkflowEscalationsHandler(_tasks, _instances, new SlaEscalationRuleRepository(_context, _tenant),
                        _logs, TimeProvider.System, assignmentSnapshotRepository: _snapshots, transactions: executor, events: events,
                        templates: _templates)
                    .Handle(new RunWorkflowEscalationsCommand(new RunWorkflowEscalationsRequest(null, 500, null), "corr-w9-sweep"), default);
                Assert.True(run.IsSuccessful, run.ReasonCode);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(terminal));
        }
    }

    [Fact]
    public async Task AC_W9_recovery_refuses_a_whole_start_and_writes_nothing()
    {
        var request = Request("w9-recovery");
        var started = await Handler().Handle(Command(request), default);
        Assert.True(started.IsSuccessful, started.ReasonCode);
        var instanceId = started.Data!.WorkflowInstanceId;
        await AgeAsync(instanceId);
        var before = await CountsAsync(instanceId);

        var direct = await CloseHandler().Handle(new CloseAbandonedWorkflowStartCommand(instanceId, Age, "corr-w9-direct"), default);

        Assert.Equal((409, WorkflowReasonCodes.WorkflowStartNotAbandoned), (direct.StatusCode, direct.ReasonCode));
        Assert.Equal(WorkflowInstanceStatus.Active, (await _instances.GetByIdAsync(instanceId))!.Status);
        Assert.Equal(before, await CountsAsync(instanceId));
        Assert.Equal(1, before.Logs); // the start's own log, nothing added
    }

    // ── AC-W11 · same key, different facts or a different starter ───────────────────────────────────────────────

    [Theory]
    [InlineData("template")]
    [InlineData("object-id")]
    [InlineData("object-ref")]
    [InlineData("candidates")]
    [InlineData("due-at")]
    [InlineData("reason-code")]
    public async Task AC_W11_same_key_different_facts_is_409_and_nothing_changes(string different)
    {
        var request = Request("w11-" + different);
        var first = await Handler().Handle(Command(request), default);
        Assert.True(first.IsSuccessful, first.ReasonCode);
        var instanceId = first.Data!.WorkflowInstanceId;
        var before = await CountsAsync(instanceId);

        var other = different switch
        {
            "template" => request with { TemplateId = await SecondTemplateAsync() },
            "object-id" => request with { ObjectId = Guid.NewGuid().ToString("D"), ObjectRef = null },
            "object-ref" => request with { ObjectRef = "another-ref|" + Guid.NewGuid().ToString("N") },
            "candidates" => request with { CandidatePrincipalIds = [D(OtherApprover)] },
            "due-at" => request with { DueAt = DateTimeOffset.UtcNow.AddHours(3) },
            "reason-code" => request with { ReasonCode = "SOMETHING_ELSE" },
            _ => throw new ArgumentOutOfRangeException(nameof(different))
        };

        var refused = await Handler().Handle(Command(other), default);

        Assert.Equal((409, WorkflowReasonCodes.WorkflowInstanceStartConflict), (refused.StatusCode, refused.ReasonCode));
        Assert.Equal(before, await CountsAsync(instanceId));
        Assert.Single(await InstancesOfAsync(request));
        Assert.Single(await _snapshots.ListByInstanceIdAsync(instanceId));
        Assert.Equal(first.Data.ApprovalTaskId, Assert.Single(await _tasks.ListByInstanceIdAsync(instanceId)).Id);
        // the genuine repeat still answers 200: the refusal changed nothing
        var again = await Handler().Handle(Command(request), default);
        Assert.True(again.IsSuccessful, again.ReasonCode);
    }

    private async Task<Guid> SecondTemplateAsync()
    {
        var template = await _templates.CreateAsync(new WorkflowTemplate
        {
            TenantId = _tenant.TenantId,
            TemplateCode = "RECOVERY-2-" + Guid.NewGuid().ToString("N")[..10],
            Name = "Second definition",
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
        return template.Id;
    }

    [Fact]
    public async Task AC_W11_same_key_another_starter_is_409()
    {
        var request = Request("w11-starter");
        var first = await Handler().Handle(Command(request), default);
        Assert.True(first.IsSuccessful, first.ReasonCode);
        var before = await CountsAsync(first.Data!.WorkflowInstanceId);

        var other = await Handler(user: new UserOf(OtherApprover)).Handle(Command(request), default);

        Assert.Equal((409, WorkflowReasonCodes.WorkflowInstanceStartConflict), (other.StatusCode, other.ReasonCode));
        Assert.Equal(before, await CountsAsync(first.Data.WorkflowInstanceId));
    }

    [Fact]
    public async Task AC_W11_legacy_start_without_fingerprint_is_held_to_its_identity()
    {
        // A whole start, then the fingerprint taken off the stored row: exactly what a start before BL-549 looks like.
        var request = Request("w11-legacy");
        var first = await Handler().Handle(Command(request), default);
        Assert.True(first.IsSuccessful, first.ReasonCode);
        await _context.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances).UpdateOneAsync(
            i => i.Id == first.Data!.WorkflowInstanceId, Builders<WorkflowInstance>.Update.Unset(i => i.StartRequestFingerprint));
        Assert.Null((await _instances.GetByIdAsync(first.Data!.WorkflowInstanceId))!.StartRequestFingerprint); // non-vacuity

        var sameIdentityOtherCandidate = await Handler().Handle(Command(request with { CandidatePrincipalIds = [D(OtherApprover)] }), default);
        var otherObject = await Handler().Handle(
            Command(request with { ObjectId = Guid.NewGuid().ToString("D"), ObjectRef = null }), default);

        Assert.True(sameIdentityOtherCandidate.IsSuccessful, $"{sameIdentityOtherCandidate.StatusCode} {sameIdentityOtherCandidate.ReasonCode}");
        Assert.Equal(first.Data.ApprovalTaskId, sameIdentityOtherCandidate.Data!.ApprovalTaskId);
        Assert.True(sameIdentityOtherCandidate.Data.IdempotentReplay);
        Assert.Equal((409, WorkflowReasonCodes.WorkflowInstanceStartConflict), (otherObject.StatusCode, otherObject.ReasonCode));
        Assert.Single(await InstancesOfAsync(request));
    }

    [Theory]
    [InlineData("whole")]
    [InlineData("completing")]
    public async Task AC_W11_idempotent_replay_flag_false_first_true_after(string mode)
    {
        var request = Request("w11-flag-" + mode);
        Response<StartWorkflowInstanceResponse> first;
        if (mode == "whole")
        {
            first = await Handler().Handle(Command(request), default);
            Assert.True(first.IsSuccessful, first.ReasonCode);
            Assert.False(first.Data!.IdempotentReplay); // only the first write of a key answers false
        }
        else
        {
            // the first call stops after its instance (CrashBeforeTaskRepository); the repeat that COMPLETES the start says true
            await Assert.ThrowsAsync<InjectedCrashException>(() =>
                Handler(tasks: new CrashBeforeTaskRepository(_tasks)).Handle(Command(request), default));
            first = await Handler().Handle(Command(request), default);
            Assert.True(first.IsSuccessful, first.ReasonCode);
            Assert.True(first.Data!.IdempotentReplay);
        }

        var instance = Assert.Single(await InstancesOfAsync(request));
        var version = instance.Version;

        var again = await Handler().Handle(Command(request), default);
        var andAgain = await Handler().Handle(Command(request), default);

        Assert.All(new[] { again, andAgain }, replay =>
        {
            Assert.True(replay.IsSuccessful, replay.ReasonCode);
            Assert.True(replay.Data!.IdempotentReplay);
            Assert.Equal(first.Data.WorkflowInstanceId, replay.Data.WorkflowInstanceId);
            Assert.Equal(first.Data.ApprovalTaskId, replay.Data.ApprovalTaskId);
        });
        var after = Assert.Single(await InstancesOfAsync(request)); // the first instance, and no second one
        Assert.Equal(instance.Id, after.Id);
        Assert.Equal(version, after.Version);                        // a repeat writes nothing: the same version
    }

    [Fact]
    public async Task AC_W11_trusted_whole_start_replayed_through_the_generic_handler()
    {
        // A whole TRUSTED start (as MDM makes one), then the same key and facts sent to the generic start endpoint's handler.
        // The trusted start stores a fingerprint of another shape, so the generic comparison cannot match it. §17.9 does not
        // write this case; this test pins what is measured.
        var key = "w11-trusted:" + Guid.NewGuid().ToString("N");
        var objectId = "GP-" + Guid.NewGuid().ToString("N");
        var trusted = new TrustedWorkflowStartRequest(_templateId, null, "GlobalProduct", objectId, null, [D(Approver)], "SUBMIT", key, false, false, null);
        var started = await TrustedCoordinator().StartAsync(trusted, TrustedClient, Maker, "corr-w11-trusted");
        Assert.True(started.IsSuccessful, started.ReasonCode);
        var instance = Assert.Single(await InstancesOfKeyAsync(key));
        Assert.NotNull(instance.TrustedConsumerClientId); // non-vacuity: this really is a trusted start
        var before = await CountsAsync(instance.Id);

        var generic = new StartWorkflowInstanceRequest(_templateId, null, "GlobalProduct", objectId, null, [D(Approver)], "SUBMIT", key, false, false, null);
        var replay = await Handler(user: new UserOf(Maker)).Handle(Command(generic), default);

        Assert.Equal((409, WorkflowReasonCodes.WorkflowInstanceStartConflict), (replay.StatusCode, replay.ReasonCode));
        Assert.Equal(before, await CountsAsync(instance.Id));
    }

    [Fact]
    public async Task AC_W11_idempotent_replay_flag_is_true_when_a_start_told_unavailable_is_found_whole()
    {
        // WholeStartAsync via AfterUnavailableAsync: the winner completes the start inside this caller's transaction, the caller is
        // told "unavailable", re-reads and answers as the whole start it finds — a replay by definition (the key was bound before it).
        var request = Request("w11-whole-after-unavailable");
        Response<StartWorkflowInstanceResponse>? winner = null;

        var answer = await HandlerWith(new UnavailableAfter(async () => winner = await Handler().Handle(Command(request), default)))
            .Handle(Command(request), default);

        Assert.True(winner!.IsSuccessful, winner.ReasonCode); // non-vacuity: the winner completed it
        Assert.True(answer.IsSuccessful, $"{answer.StatusCode} {answer.ReasonCode}");
        Assert.Equal(winner.Data!.ApprovalTaskId, answer.Data!.ApprovalTaskId);
        Assert.True(answer.Data.IdempotentReplay);
    }
}
