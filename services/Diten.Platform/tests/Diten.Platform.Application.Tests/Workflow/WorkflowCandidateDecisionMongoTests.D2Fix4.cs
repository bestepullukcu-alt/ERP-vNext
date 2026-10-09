using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

/// <summary>
/// WP-WF-ENGINE-CANDIDATES-01 · D2 fix round 4 (CT-WF-D2-FIX4). Real replica set, production handlers and repositories.
/// </summary>
public sealed partial class WorkflowCandidateDecisionMongoTests
{
    // ── 3 · the look-time reset the rule handlers use: targeted, open tasks only, this tenant only ───────────────

    [Fact]
    public async Task The_look_reset_clears_only_the_open_tasks_of_the_given_instances_in_this_tenant()
    {
        var instanceA = Guid.NewGuid();
        var instanceB = Guid.NewGuid();
        var lookAt = DateTime.UtcNow.AddHours(12);
        var waiting = await _store.InsertRestingAsync(instanceA, ApprovalTaskStatus.WaitingApproval, lookAt);
        var escalated = await _store.InsertRestingAsync(instanceA, ApprovalTaskStatus.Escalated, lookAt);
        var closed = await _store.InsertRestingAsync(instanceA, ApprovalTaskStatus.Approved, lookAt);
        var otherInstance = await _store.InsertRestingAsync(instanceB, ApprovalTaskStatus.WaitingApproval, lookAt);
        var neverLooked = await _store.InsertRestingAsync(instanceA, ApprovalTaskStatus.WaitingApproval, lookAt: null);
        var foreign = await _store.InsertRestingAsync(instanceA, ApprovalTaskStatus.WaitingApproval, lookAt, tenant: Guid.NewGuid());

        var reset = await _store.Tasks.ResetEscalationLooksAsync([instanceA]);

        Assert.Equal(2, reset);                                                            // only what had a look time and was open
        Assert.Null((await _store.Tasks.GetByIdAsync(waiting))!.EscalationNextLookAtUtc);
        Assert.Null((await _store.Tasks.GetByIdAsync(escalated))!.EscalationNextLookAtUtc);
        Assert.NotNull((await _store.Tasks.GetByIdAsync(closed))!.EscalationNextLookAtUtc);
        Assert.NotNull((await _store.Tasks.GetByIdAsync(otherInstance))!.EscalationNextLookAtUtc);
        Assert.Null((await _store.Tasks.GetByIdAsync(neverLooked))!.EscalationNextLookAtUtc);
        // Another tenant's task is not even visible through this tenant's repository; read it raw: untouched.
        Assert.NotNull((await _store.RawTaskAsync(foreign)).EscalationNextLookAtUtc);
        Assert.Equal(0, await _store.Tasks.ResetEscalationLooksAsync([]));
    }

    [Fact]
    public async Task The_open_instance_read_lists_this_tenants_open_instances_of_the_template_and_nothing_else()
    {
        var template = Guid.NewGuid();
        var active = await _store.InsertInstanceAsync(template, WorkflowInstanceStatus.Active);
        var escalated = await _store.InsertInstanceAsync(template, WorkflowInstanceStatus.Escalated);
        var pending = await _store.InsertInstanceAsync(template, WorkflowInstanceStatus.Pending);
        await _store.InsertInstanceAsync(template, WorkflowInstanceStatus.Completed);
        await _store.InsertInstanceAsync(template, WorkflowInstanceStatus.Cancelled);
        await _store.InsertInstanceAsync(template, WorkflowInstanceStatus.TimedOut);
        await _store.InsertInstanceAsync(Guid.NewGuid(), WorkflowInstanceStatus.Active);                 // another template
        await _store.InsertInstanceAsync(template, WorkflowInstanceStatus.Active, tenant: Guid.NewGuid()); // another tenant

        var ids = await _store.Instances.ListOpenIdsByTemplateAsync(template);

        Assert.Equal(new[] { active, escalated, pending }.Order(), ids.Order());
    }

    // ── 7 · the stray-task withdraw's log line: an idempotent append ─────────────────────────────────────────────

    [Fact]
    public async Task A_stray_withdraws_log_line_whose_sequence_number_was_taken_is_appended_at_the_next_one()
    {
        var templateId = await _store.PublishedTemplateAsync();
        var started = await _store.Coordinator().StartAsync(
            TrustedRequest(templateId, "stray-seq", [D(First)]), TrustedClient, TrustedMaker, "corr-ss");
        Assert.True(started.IsSuccessful, started.ReasonCode);
        await _store.CloseAsIfRacedAsync(started.Data!);
        var racing = new LogsThatLoseTheirSequenceNumber(
            _store.Logs, _store.TenantContext.TenantId, started.Data!.WorkflowInstanceId, started.Data.ApprovalTaskId);

        var answer = await _store.CoordinatorWith(logs: racing).StartAsync(
            TrustedRequest(templateId, "stray-seq", [D(First)]), TrustedClient, TrustedMaker, "corr-ss-2");

        // The closed reservation's own answer — not an exception thrown after the task was already cancelled.
        Assert.Equal(400, answer.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowNoEligibleApprover, answer.ReasonCode);
        Assert.Equal(ApprovalTaskStatus.Cancelled, (await _store.Tasks.GetByIdAsync(started.Data.ApprovalTaskId))!.Status);
        var line = Assert.Single(await _store.Logs.ListByInstanceIdAsync(started.Data.WorkflowInstanceId),
            l => l.Action == WorkflowTransitionAction.Cancel);
        Assert.True(racing.CompetingSequence is > 0);
        Assert.True(line.SequenceNo > racing.CompetingSequence);          // appended after the line that took its number
    }

    // ── harness (D2-FIX4) ──────────────────────────────────────────────────────────────────────────────────────

    private sealed partial class Store
    {
        public async Task<Guid> InsertRestingAsync(
            Guid instanceId, ApprovalTaskStatus status, DateTime? lookAt, Guid? tenant = null)
        {
            var task = new ApprovalTask
            {
                TenantId = tenant ?? TenantContext.TenantId,
                WorkflowInstanceId = instanceId,
                Status = status,
                DueAt = DateTimeOffset.UtcNow.AddDays(-1),
                EscalationNextLookAtUtc = lookAt
            };
            await Context.Database.GetCollection<ApprovalTask>(PlatformCollections.ApprovalTasks).InsertOneAsync(task);
            return task.Id;
        }

        public Task<ApprovalTask> RawTaskAsync(Guid id) =>
            Context.Database.GetCollection<ApprovalTask>(PlatformCollections.ApprovalTasks).Find(t => t.Id == id).SingleAsync();

        public async Task<Guid> InsertInstanceAsync(Guid templateId, WorkflowInstanceStatus status, Guid? tenant = null)
        {
            var instance = new WorkflowInstance
            {
                TenantId = tenant ?? TenantContext.TenantId,
                TemplateId = templateId,
                WorkflowTemplateId = templateId,
                ObjectType = "d2fix4",
                ObjectId = Guid.NewGuid().ToString("N"),
                ObjectRef = "d2fix4|" + Guid.NewGuid().ToString("N"),
                Status = status,
                StartedAt = DateTimeOffset.UtcNow
            };
            await Context.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances).InsertOneAsync(instance);
            return instance.Id;
        }
    }

    /// <summary>
    /// The transition log, except that the FIRST read of an instance's latest sequence number is stale: another transition of the
    /// instance took the next number between that read and the append (it is written here, then the old number is returned) —
    /// so the append meets the real unique (tenant, instance, sequence) index.
    /// </summary>
    private sealed class LogsThatLoseTheirSequenceNumber(
        IWorkflowTransitionLogRepository inner, Guid tenantId, Guid instanceId, Guid taskId) : IWorkflowTransitionLogRepository
    {
        private bool _raced;

        public long? CompetingSequence { get; private set; }

        public Task<WorkflowTransitionLog> CreateAsync(WorkflowTransitionLog log, CancellationToken ct = default) =>
            inner.CreateAsync(log, ct);

        public Task<WorkflowTransitionLog?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);

        public Task<WorkflowTransitionLog?> GetByTaskActionIdempotencyKeyAsync(
            Guid approvalTaskId, WorkflowTransitionAction action, string idempotencyKey, CancellationToken ct = default) =>
            inner.GetByTaskActionIdempotencyKeyAsync(approvalTaskId, action, idempotencyKey, ct);

        public Task<IReadOnlyList<WorkflowTransitionLog>> ListByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            inner.ListByInstanceIdAsync(workflowInstanceId, ct);

        public async Task<long> GetLatestSequenceNoAsync(Guid workflowInstanceId, CancellationToken ct = default)
        {
            var latest = await inner.GetLatestSequenceNoAsync(workflowInstanceId, ct);
            if (_raced || workflowInstanceId != instanceId)
            {
                return latest;
            }

            _raced = true;
            await inner.CreateAsync(new WorkflowTransitionLog
            {
                TenantId = tenantId,
                WorkflowInstanceId = instanceId,
                ApprovalTaskId = taskId,
                Action = WorkflowTransitionAction.Escalate,
                FromState = ApprovalTaskStatus.WaitingApproval.ToString(),
                ToState = ApprovalTaskStatus.Escalated.ToString(),
                IdempotencyKey = "competing-transition",
                SequenceNo = latest + 1
            }, ct);
            CompetingSequence = latest + 1;
            return latest;
        }
    }
}
