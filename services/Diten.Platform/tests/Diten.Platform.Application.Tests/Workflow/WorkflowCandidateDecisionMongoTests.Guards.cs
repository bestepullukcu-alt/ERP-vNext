using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

/// <summary>
/// WP-WF-ENGINE-CANDIDATES-01 · D1 test round (P-2026-10-02-09-D1-TESTS) — rules FIX1 already implements but no test
/// held: each one-line break of them (CT's E-list) left every test green. One test per rule, against production code.
/// </summary>
public sealed partial class WorkflowCandidateDecisionMongoTests
{
    // ── E1 · a replay whose candidates REALLY changed is a conflict, not a silent success on the stored list ──────

    [Fact]
    public async Task A_trusted_start_replayed_with_DIFFERENT_candidates_is_a_conflict()
    {
        var templateId = await _store.PublishedTemplateAsync();
        var started = await _store.Coordinator().StartAsync(
            TrustedRequest(templateId, "changed-candidates", [D(First), D(Second)]), TrustedClient, TrustedMaker, "corr-cc");
        Assert.True(started.IsSuccessful, started.ReasonCode);

        var replay = await _store.Coordinator().StartAsync(
            TrustedRequest(templateId, "changed-candidates", [D(First), D(NextApprover)]), TrustedClient, TrustedMaker, "corr-cc-2");

        Assert.Equal(409, replay.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowStartIdempotencyConflict, replay.ReasonCode);
        var snapshot = (await _store.Snapshots.GetByIdAsync(started.Data!.AssignmentSnapshotId))!;
        Assert.Equal([D(First), D(Second)], snapshot.CandidatePrincipalIds.OrderBy(x => x, StringComparer.Ordinal));
    }

    // ── E2 · another candidate's log row under the SAME idempotency key is not "my write" ────────────────────────

    [Fact]
    public async Task Another_candidates_log_under_the_same_key_is_not_taken_for_my_own_commit()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        // After this caller's own idempotency check, the other candidate decides with the SAME key, then this caller's
        // transaction is reported unavailable.
        var executor = new ReportsUnavailableAfter(
            () => _store.ApproveAsync(seeded.Task.Id, D(First), "shared-key"));

        var response = await _store.ApproveAsync(seeded.Task.Id, D(Second), "shared-key", executor);

        Assert.Equal(409, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowTransitionConflict, response.ReasonCode);
        Assert.Equal(D(First), (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!.ActionedBy);
    }

    // ── E4 · every checkpoint short of Completed refuses a decision, the start log's included ───────────────────

    [Fact]
    public async Task A_trusted_start_whose_start_log_is_written_but_not_completed_still_refuses_a_decision()
    {
        var seeded = await _store.SeedAsync([D(First)], trustedCheckpoint: WorkflowStartCheckpoint.StartLogPersisted);

        var response = await _store.ApproveAsync(seeded.Task.Id, D(First), "start-log-persisted");

        Assert.Equal(409, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowStartNotCompleted, response.ReasonCode);
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!.Status);
    }

    // ── E7 · "the newest task of an instance" really is the newest ─────────────────────────────────────────────

    [Fact]
    public async Task After_an_intermediate_step_the_week_rule_reads_the_NEW_step_task_not_the_decided_one()
    {
        // CreatedAt is stored as a [ticks, offsetMinutes] array (no DateTimeOffsetSerializer, BL-030): a DESCENDING sort
        // reads the ticks, an ascending one reads the offset (0 for both tasks) and leaves the order to the Id tie-break.
        // The decided task gets the largest Id, so an oldest-first sort puts it first every time instead of half the time.
        var seeded = await _store.SeedAsync([D(First), D(Second)], twoSteps: true,
            firstTaskId: Guid.Parse("ffffffff-ffff-ffff-" + Guid.NewGuid().ToString("D")[19..]));
        Assert.True((await _store.ApproveAsync(seeded.Task.Id, D(First), "step-a-first")).IsSuccessful);
        var tasks = await _store.Tasks.ListByInstanceIdsAsync([seeded.Instance.Id]);
        Assert.Equal(2, tasks.Count);
        Assert.Equal("step-b", tasks[0].StepCode); // newest first

        // The timesheet's read rule (the same service the approvals page uses) asks about this instance.
        var approvals = new TimesheetApprovalService(
            null!, null!, null!, _store.Tasks, null!, _store.TenantContext, null!, _store.Snapshots);
        // The step-b candidate may decide it now…
        Assert.Contains(seeded.Instance.Id,
            await approvals.DecidableInstanceIdsAsync(NextApprover, [seeded.Instance.Id], includeDecidedByUser: true));
        // …and step-a's decider has nothing to take on board: the instance's newest task is not theirs.
        Assert.DoesNotContain(seeded.Instance.Id,
            await approvals.DecidableInstanceIdsAsync(First, [seeded.Instance.Id], includeDecidedByUser: true));
    }

    // ── E8 · "it landed but was reported unavailable" is recognised for every transition, not only approve ──────

    [Theory]
    [InlineData("reject")]
    [InlineData("delegate")]
    [InlineData("requestInfo")]
    public async Task A_landed_commit_reported_unavailable_is_a_success_for_every_transition(string action)
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        var executor = new CommitsThenReportsUnavailable(new PlatformTransactionExecutor(_store.Context));

        var response = action switch
        {
            "reject" => await _store.RejectAsync(seeded.Task.Id, D(Second), "landed-" + action, executor),
            "delegate" => await _store.DelegateAsync(seeded.Task.Id, D(Second), D(NextApprover), "landed-" + action, executor),
            _ => await _store.RequestInfoAsync(seeded.Task.Id, D(Second), "landed-" + action, executor)
        };

        Assert.True(response.IsSuccessful, response.ReasonCode);
        Assert.Equal(1, executor.Calls);
        Assert.Equal(D(Second), (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!.ActionedBy);
    }

    [Fact]
    public async Task A_landed_escalation_reported_unavailable_is_processed_not_skipped()
    {
        var seeded = await _store.SeedAsync([D(First)], dueAt: DateTimeOffset.UtcNow.AddMinutes(-5));
        await _store.EscalationRuleAsync(seeded.Instance.TemplateId, [D(CandidateA)]);
        var executor = new CommitsThenReportsUnavailable(new PlatformTransactionExecutor(_store.Context));

        var run = await _store.RunEscalationsAsync(executor);

        var result = run.Results.Single(r => r.ApprovalTaskId == seeded.Task.Id);
        Assert.Equal(WorkflowReasonCodes.WorkflowEscalationProcessed, result.ReasonCode);
        Assert.Equal(ApprovalTaskStatus.Escalated, (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!.Status);
    }

    // ── E9 · my own commit seen only on the SECOND look (version first, log a moment later) is still mine ───────

    [Fact]
    public async Task My_commit_that_becomes_visible_between_the_two_reads_is_a_success()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        var executor = new HoldsTheCommitBack(new PlatformTransactionExecutor(_store.Context));
        // The commit lands while the writer re-reads the task: its first "is it mine" found nothing, the version has
        // moved by the time the task is read, and only the second look finds my log row.
        var tasks = new CommitsOnFirstReadAfterArmed(_store.Tasks, executor);

        var response = await _store.ApproveAsync(seeded.Task.Id, D(Second), "seen-second", executor, tasks);

        Assert.True(tasks.CommittedDuringRead, "non-vacuity: the commit landed inside the re-read");
        Assert.True(response.IsSuccessful, response.ReasonCode);
        Assert.Equal(D(Second), (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!.ActionedBy);
    }

    // ── E10 · the `user:` prefix is matched in any letter case ─────────────────────────────────────────────────

    [Fact]
    public async Task A_delegation_target_written_USER_in_capitals_is_accepted()
    {
        var seeded = await _store.SeedAsync([D(First)]);

        var response = await _store.DelegateAsync(seeded.Task.Id, D(First), "USER:" + D(NextApprover), "capital-prefix");

        Assert.True(response.IsSuccessful, response.ReasonCode);
        Assert.Equal(D(NextApprover), (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!.AssigneeRef);
    }

    // ── harness (test round) ───────────────────────────────────────────────────────────────────────────────────

    /// <summary>Runs nothing when called: keeps the body and reports "unavailable". The body is committed later, by
    /// <see cref="CommitsOnFirstReadAfterArmed"/>, at the moment the writer re-reads the task.</summary>
    private sealed class HoldsTheCommitBack(IPlatformTransactionExecutor inner) : IPlatformTransactionExecutor
    {
        public Func<Task>? Pending { get; private set; }

        public Task<T> ExecuteAsync<T>(
            Func<IPlatformTransactionSession, CancellationToken, Task<T>> body, CancellationToken cancellationToken = default)
        {
            Pending = () => inner.ExecuteAsync(body, cancellationToken);
            throw new PlatformTransactionUnavailableException("commit result unknown", new InvalidOperationException());
        }
    }

    private sealed class CommitsOnFirstReadAfterArmed(IApprovalTaskRepository inner, HoldsTheCommitBack executor)
        : IApprovalTaskRepository
    {
        public bool CommittedDuringRead { get; private set; }

        public async Task<ApprovalTask?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            if (executor.Pending is { } pending && !CommittedDuringRead)
            {
                CommittedDuringRead = true;
                await pending();
            }

            return await inner.GetByIdAsync(id, ct);
        }

        public Task<ApprovalTask> CreateAsync(ApprovalTask task, CancellationToken ct = default) => inner.CreateAsync(task, ct);
        public Task<ApprovalTask?> GetFirstByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            inner.GetFirstByInstanceIdAsync(workflowInstanceId, ct);
        public Task<ApprovalTask?> GetActiveByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            inner.GetActiveByInstanceIdAsync(workflowInstanceId, ct);
        public Task<IReadOnlyList<ApprovalTask>> ListByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            inner.ListByInstanceIdAsync(workflowInstanceId, ct);
        public Task<IReadOnlyList<ApprovalTask>> GetAllForTenantAsync(CancellationToken ct = default) => inner.GetAllForTenantAsync(ct);
        public Task<bool> UpdateAsync(ApprovalTask task, int expectedVersion, CancellationToken ct = default) =>
            inner.UpdateAsync(task, expectedVersion, ct);
        public Task<ApprovalTask?> GetByIdAsync(IPlatformTransactionSession session, Guid id, CancellationToken ct = default) =>
            inner.GetByIdAsync(session, id, ct);
        public Task<bool> UpdateAsync(IPlatformTransactionSession session, ApprovalTask task, int expectedVersion,
            CancellationToken ct = default) => inner.UpdateAsync(session, task, expectedVersion, ct);
        public Task<ApprovalTask> CreateAsync(IPlatformTransactionSession session, ApprovalTask task,
            CancellationToken ct = default) => inner.CreateAsync(session, task, ct);
    }
}
