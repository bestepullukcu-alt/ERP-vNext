using System.Reflection;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

/// <summary>
/// WP-WF-ENGINE-CANDIDATES-01 · D2 fix round 3 (CT-WF-D2-FIX3), items 4 and 5 — WHEN the SLA sweep looks again at a task it
/// could not act on, for every way it rests one, measured to the second on a fixed clock.
///
/// <para>The rules here put their thresholds AFTER the due date (escalate two hours late, time out a day late) — the shape
/// real rules have. The earlier rounds' helper set threshold = due date, so every task was already past its threshold and
/// the resting path was never taken.</para>
///
/// <para>Three rests: a STANDING one (an operator setting, 24 hours by default) for what no amount of looking changes — no
/// rule, no timeout left, an instance that is gone or closed; the next REAL threshold for a task waiting on one (never
/// later than the standing rest); a quarter of an hour for what may change any moment — an empty seat, a write that lost
/// its race, an evaluation that threw.</para>
/// </summary>
public sealed class WorkflowEscalationRestTests
{
    private static readonly Guid Tenant = Guid.Parse("7a000000-0000-4000-8000-000000000001");
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Standing = WorkflowEscalationOptions.DefaultStandingRest;
    private static readonly TimeSpan Short = RunWorkflowEscalationsHandler.InconclusiveRecheckInterval;
    private const string Escalator = "77777777-7777-4777-8777-777777777777";
    private const string StarterId = "55555555-5555-4555-8555-555555555555";

    // ── a task waiting on its threshold: looked at AT the threshold ────────────────────────────────────────────

    [Fact]
    public async Task A_task_before_its_escalation_threshold_is_looked_at_at_the_threshold_not_every_quarter_hour()
    {
        var w = new World();
        var dueAt = Now.AddMinutes(-1);
        var seeded = w.Seed(dueAt);
        w.Rule(seeded.Instance, escalateAfterMinutes: 120, timeoutAfterMinutes: 1440, Escalator);

        var run = await w.SweepAsync();

        Assert.Equal(WorkflowReasonCodes.WorkflowNoOverdueTasks, w.Result(run, seeded.Task).ReasonCode);
        Assert.Equal(dueAt.AddMinutes(120).UtcDateTime, w.LookAt(seeded.Task)); // the threshold — not Now + 15 minutes
    }

    [Fact]
    public async Task A_threshold_further_away_than_the_standing_rest_is_looked_at_after_the_standing_rest()
    {
        var w = new World();
        var seeded = w.Seed(Now.AddMinutes(-1));
        w.Rule(seeded.Instance, escalateAfterMinutes: 3000, timeoutAfterMinutes: null, Escalator);

        await w.SweepAsync();

        Assert.Equal((Now + Standing).UtcDateTime, w.LookAt(seeded.Task));
    }

    [Fact]
    public async Task An_escalated_task_whose_rule_times_out_later_is_looked_at_at_the_timeout_threshold()
    {
        // Esc:174 with a timeout to come — the escalation threshold is behind it, the timeout threshold is the next real one.
        var w = new World();
        var dueAt = Now.AddHours(-3);
        var seeded = w.Seed(dueAt, taskStatus: ApprovalTaskStatus.Escalated, instanceStatus: WorkflowInstanceStatus.Escalated);
        w.Rule(seeded.Instance, escalateAfterMinutes: 120, timeoutAfterMinutes: 300, Escalator);
        w.EscalatedBefore(seeded);

        var run = await w.SweepAsync();

        Assert.Equal(WorkflowReasonCodes.WorkflowEscalationIdempotent, w.Result(run, seeded.Task).ReasonCode);
        Assert.Equal(dueAt.AddMinutes(300).UtcDateTime, w.LookAt(seeded.Task));
    }

    [Fact]
    public async Task A_rule_with_nobody_to_escalate_to_rests_until_its_timeout_threshold()
    {
        // Esc:217 — the escalation can never happen under this rule; the timeout can.
        var w = new World();
        var dueAt = Now.AddHours(-3);
        var seeded = w.Seed(dueAt);
        w.Rule(seeded.Instance, escalateAfterMinutes: 120, timeoutAfterMinutes: 600);

        var run = await w.SweepAsync();

        Assert.Equal(WorkflowReasonCodes.WorkflowAssignmentCandidatesRequired, w.Result(run, seeded.Task).ReasonCode);
        Assert.Equal(dueAt.AddMinutes(600).UtcDateTime, w.LookAt(seeded.Task));
    }

    // ── nothing to wait for: the standing rest ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_task_without_an_SLA_rule_rests_for_the_standing_rest()
    {
        // Esc:162
        var w = new World();
        var seeded = w.Seed(Now.AddDays(-30));

        var run = await w.SweepAsync();

        Assert.Equal(WorkflowReasonCodes.WorkflowSlaRuleNotFound, w.Result(run, seeded.Task).ReasonCode);
        Assert.Equal((Now + Standing).UtcDateTime, w.LookAt(seeded.Task));
    }

    [Fact]
    public async Task A_task_whose_instance_is_gone_rests_for_the_standing_rest()
    {
        // Esc:152
        var w = new World();
        var seeded = w.Seed(Now.AddHours(-1));
        w.Instances.Items.Remove(seeded.Instance);

        var run = await w.SweepAsync();

        Assert.Equal(WorkflowReasonCodes.WorkflowTaskInvalidState, w.Result(run, seeded.Task).ReasonCode);
        Assert.Equal((Now + Standing).UtcDateTime, w.LookAt(seeded.Task));
    }

    [Fact]
    public async Task An_escalated_task_whose_rule_has_no_timeout_rests_for_the_standing_rest()
    {
        // Esc:174 — found idempotent on every sweep before: escalated once, and nothing further can happen to it.
        var w = new World();
        var seeded = w.Seed(Now.AddHours(-3), taskStatus: ApprovalTaskStatus.Escalated, instanceStatus: WorkflowInstanceStatus.Escalated);
        w.Rule(seeded.Instance, escalateAfterMinutes: 120, timeoutAfterMinutes: null, Escalator);
        w.EscalatedBefore(seeded);

        var run = await w.SweepAsync();

        Assert.Equal(WorkflowReasonCodes.WorkflowEscalationIdempotent, w.Result(run, seeded.Task).ReasonCode);
        Assert.Equal((Now + Standing).UtcDateTime, w.LookAt(seeded.Task));
    }

    [Fact]
    public async Task A_stray_task_under_a_closed_instance_rests_for_the_standing_rest_whatever_its_rule_says()
    {
        // Esc:190 — its rule's threshold is an hour away, and still: a task under a cancelled instance is not escalated
        // at that threshold either, so it is not looked at then.
        var w = new World();
        var dueAt = Now.AddMinutes(-1);
        var seeded = w.Seed(dueAt, instanceStatus: WorkflowInstanceStatus.Cancelled);
        w.Rule(seeded.Instance, escalateAfterMinutes: 60, timeoutAfterMinutes: 120, Escalator);

        var run = await w.SweepAsync();

        Assert.Equal(WorkflowReasonCodes.WorkflowTaskInvalidState, w.Result(run, seeded.Task).ReasonCode);
        Assert.Equal((Now + Standing).UtcDateTime, w.LookAt(seeded.Task));
    }

    [Fact]
    public async Task A_task_whose_start_is_still_in_flight_is_looked_at_again_soon()
    {
        // Esc:190, the one transient case: a Pending instance is a start that has not finished yet, not a closed one.
        var w = new World();
        var seeded = w.Seed(Now.AddHours(-1), instanceStatus: WorkflowInstanceStatus.Pending);

        w.Rule(seeded.Instance, escalateAfterMinutes: 10, timeoutAfterMinutes: null, Escalator);
        await w.SweepAsync();

        Assert.Equal((Now + Short).UtcDateTime, w.LookAt(seeded.Task));
    }

    [Fact]
    public async Task The_standing_rest_is_an_operator_setting()
    {
        var w = new World(standingRest: TimeSpan.FromHours(6));
        var seeded = w.Seed(Now.AddDays(-2));

        await w.SweepAsync();

        Assert.Equal(Now.AddHours(6).UtcDateTime, w.LookAt(seeded.Task));
    }

    // ── what may change any moment: a quarter of an hour ────────────────────────────────────────────────────────

    [Fact]
    public async Task An_escalation_whose_seat_is_empty_is_looked_at_again_in_a_quarter_of_an_hour()
    {
        // Esc:228 — the position resolves to nobody (no holder can be read here); somebody may take the seat.
        var w = new World();
        var seeded = w.Seed(Now.AddHours(-3));
        w.Rule(seeded.Instance, escalateAfterMinutes: 120, timeoutAfterMinutes: 1440, "position:" + Guid.NewGuid().ToString("D"));

        var run = await w.SweepAsync();

        Assert.Equal(WorkflowReasonCodes.WorkflowAssignmentCandidatesRequired, w.Result(run, seeded.Task).ReasonCode);
        Assert.Equal((Now + Short).UtcDateTime, w.LookAt(seeded.Task));
    }

    [Fact]
    public async Task An_escalation_only_the_starter_could_take_is_looked_at_again_in_a_quarter_of_an_hour()
    {
        var w = new World();
        var seeded = w.Seed(Now.AddHours(-3), startedBy: StarterId);
        w.Rule(seeded.Instance, escalateAfterMinutes: 120, timeoutAfterMinutes: null, StarterId);

        var run = await w.SweepAsync();

        Assert.Equal(WorkflowReasonCodes.WorkflowNoEligibleApprover, w.Result(run, seeded.Task).ReasonCode);
        Assert.Equal((Now + Short).UtcDateTime, w.LookAt(seeded.Task));
    }

    [Fact]
    public async Task A_timeout_whose_write_loses_its_race_is_looked_at_again_in_a_quarter_of_an_hour()
    {
        // Esc:320
        var w = new World();
        var seeded = w.Seed(Now.AddHours(-10));
        w.Rule(seeded.Instance, escalateAfterMinutes: 120, timeoutAfterMinutes: 300, Escalator);
        w.Tasks.RefuseTransitionWrites = true;

        var run = await w.SweepAsync();

        Assert.Equal(WorkflowReasonCodes.WorkflowTransitionConflict, w.Result(run, seeded.Task).ReasonCode);
        Assert.Equal(WorkflowTransitionAction.Timeout.ToString(), w.Result(run, seeded.Task).Action);
        Assert.Equal((Now + Short).UtcDateTime, w.LookAt(seeded.Task));
    }

    [Fact]
    public async Task An_escalation_whose_write_loses_its_race_is_looked_at_again_in_a_quarter_of_an_hour()
    {
        // Esc:361
        var w = new World();
        var seeded = w.Seed(Now.AddHours(-3));
        w.Rule(seeded.Instance, escalateAfterMinutes: 120, timeoutAfterMinutes: null, Escalator);
        w.Tasks.RefuseTransitionWrites = true;

        var run = await w.SweepAsync();

        Assert.Equal(WorkflowReasonCodes.WorkflowTransitionConflict, w.Result(run, seeded.Task).ReasonCode);
        Assert.Equal(WorkflowTransitionAction.Escalate.ToString(), w.Result(run, seeded.Task).Action);
        Assert.Equal((Now + Short).UtcDateTime, w.LookAt(seeded.Task));
    }

    // ── item 5 · a task that throws does not stop the sweep ─────────────────────────────────────────────────────

    [Fact]
    public async Task A_task_whose_evaluation_throws_rests_briefly_is_logged_without_its_message_and_the_sweep_goes_on()
    {
        var w = new World();
        var broken = w.Seed(Now.AddHours(-5));                // read first: the older due date
        var healthy = w.Seed(Now.AddHours(-3));
        w.Rule(healthy.Instance, escalateAfterMinutes: 120, timeoutAfterMinutes: null, Escalator);
        w.Rules.ThrowFor = broken.Instance.TemplateId;

        var run = await w.SweepAsync();

        Assert.Equal(2, run.EvaluatedCount);
        Assert.Equal(1, run.EscalatedCount);                  // the one after the failure was still evaluated
        Assert.Equal(ApprovalTaskStatus.Escalated, w.Stored(healthy.Task).Status);
        Assert.Equal(WorkflowReasonCodes.WorkflowTransactionUnavailable, w.Result(run, broken.Task).ReasonCode);
        Assert.Equal((Now + Short).UtcDateTime, w.LookAt(broken.Task));
        var error = Assert.Single(w.Logger.Errors);
        Assert.Contains(broken.Task.Id.ToString(), error, StringComparison.Ordinal);
        Assert.Contains(nameof(InvalidOperationException), error, StringComparison.Ordinal);
        Assert.DoesNotContain(RuleStore.Secret, error, StringComparison.Ordinal); // no exception text in the log
    }

    // ── world ───────────────────────────────────────────────────────────────────────────────────────────────────

    private sealed class World(TimeSpan? standingRest = null)
    {
        public TaskStore Tasks { get; } = new();
        public InstanceStore Instances { get; } = new();
        public RuleStore Rules { get; } = new();
        public LogStore Logs { get; } = new();
        public RecordingLogger Logger { get; } = new();

        public (WorkflowInstance Instance, ApprovalTask Task) Seed(
            DateTimeOffset dueAt,
            ApprovalTaskStatus taskStatus = ApprovalTaskStatus.WaitingApproval,
            WorkflowInstanceStatus instanceStatus = WorkflowInstanceStatus.Active,
            string? startedBy = null)
        {
            var templateId = Guid.NewGuid();
            var instance = new WorkflowInstance
            {
                TenantId = Tenant,
                TemplateId = templateId,
                WorkflowTemplateId = templateId,
                ObjectType = "rest-test",
                ObjectId = Guid.NewGuid().ToString("N"),
                ObjectRef = "rest|" + Guid.NewGuid().ToString("N"),
                CurrentStage = "stage-1",
                CurrentStep = "step-1",
                Status = instanceStatus,
                StartedBy = startedBy,
                StartedByUserId = startedBy is null ? null : Guid.Parse(startedBy),
                StartedAt = dueAt.AddHours(-1),
                DueAt = dueAt
            };
            var task = new ApprovalTask
            {
                TenantId = Tenant,
                WorkflowInstanceId = instance.Id,
                StageCode = "stage-1",
                StepCode = "step-1",
                Status = taskStatus,
                AssigneeRef = Guid.NewGuid().ToString("D"),
                DueAt = dueAt
            };
            Instances.Items.Add(instance);
            Tasks.Items.Add(task);
            return (instance, task);
        }

        /// <summary>DueInMinutes is 10, so a threshold of N minutes after the due date is stored as N + 10.</summary>
        public void Rule(WorkflowInstance instance, int escalateAfterMinutes, int? timeoutAfterMinutes, params string[] principals) =>
            Rules.Items.Add(new SlaEscalationRule
            {
                TenantId = Tenant,
                TemplateId = instance.TemplateId,
                StageCode = "stage-1",
                StepCode = "step-1",
                DueInMinutes = 10,
                EscalateAfterMinutes = escalateAfterMinutes + 10,
                TimeoutAfterMinutes = timeoutAfterMinutes + 10,
                EscalationPrincipalIds = principals.ToList()
            });

        /// <summary>The escalation log an earlier sweep wrote under the recurring sweep's own key.</summary>
        public void EscalatedBefore((WorkflowInstance Instance, ApprovalTask Task) seeded) =>
            Logs.Items.Add(new WorkflowTransitionLog
            {
                TenantId = Tenant,
                WorkflowInstanceId = seeded.Instance.Id,
                ApprovalTaskId = seeded.Task.Id,
                Action = WorkflowTransitionAction.Escalate,
                FromState = ApprovalTaskStatus.WaitingApproval.ToString(),
                ToState = ApprovalTaskStatus.Escalated.ToString(),
                IdempotencyKey = $"workflow-sla:{seeded.Task.Id:N}:{WorkflowTransitionAction.Escalate}",
                SequenceNo = 2
            });

        public async Task<RunWorkflowEscalationsResponse> SweepAsync()
        {
            var response = await new RunWorkflowEscalationsHandler(
                    Tasks, Instances, Rules, Logs, new FixedClock(Now),
                    logger: Logger,
                    options: Options.Create(new WorkflowEscalationOptions { StandingRest = standingRest ?? Standing }))
                .Handle(new RunWorkflowEscalationsCommand(new RunWorkflowEscalationsRequest(null, 100, null), "corr-rest"), default);
            Assert.True(response.IsSuccessful, response.ReasonCode);
            return response.Data!;
        }

        public WorkflowEscalationResultDto Result(RunWorkflowEscalationsResponse run, ApprovalTask task) =>
            Assert.Single(run.Results, r => r.ApprovalTaskId == task.Id);

        public ApprovalTask Stored(ApprovalTask task) => Tasks.Items.Single(t => t.Id == task.Id);

        public DateTime? LookAt(ApprovalTask task) => Stored(task).EscalationNextLookAtUtc;
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static T Copy<T>(T item) where T : class =>
        (T)typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(item, null)!;

    /// <summary>
    /// The store's answers as COPIES, like a database: a change the handler makes but does not commit never lands here.
    /// </summary>
    private sealed class TaskStore : IApprovalTaskRepository
    {
        public List<ApprovalTask> Items { get; } = [];

        /// <summary>Every versioned transition write loses its race (an escalation or a timeout).</summary>
        public bool RefuseTransitionWrites { get; set; }

        public Task<ApprovalTask> CreateAsync(ApprovalTask task, CancellationToken ct = default)
        {
            Items.Add(task);
            return Task.FromResult(task);
        }

        public Task<ApprovalTask?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(t => t.Id == id) is { } found ? Copy(found) : null);

        public Task<ApprovalTask?> GetFirstByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(t => t.WorkflowInstanceId == workflowInstanceId) is { } found ? Copy(found) : null);

        public Task<ApprovalTask?> GetActiveByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(t => t.WorkflowInstanceId == workflowInstanceId
                && t.Status is ApprovalTaskStatus.WaitingApproval or ApprovalTaskStatus.WaitingEvidence) is { } found
                ? Copy(found) : null);

        public Task<IReadOnlyList<ApprovalTask>> ListByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ApprovalTask>>(Items.Where(t => t.WorkflowInstanceId == workflowInstanceId).Select(Copy).ToList());

        public Task<IReadOnlyList<ApprovalTask>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ApprovalTask>>(Items.Select(Copy).ToList());

        public Task<IReadOnlyList<ApprovalTask>> ListOverdueTasksAsync(DateTimeOffset nowUtc, int maxItems, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ApprovalTask>>(Items
                .Where(t => t.Status is ApprovalTaskStatus.WaitingApproval or ApprovalTaskStatus.WaitingEvidence or ApprovalTaskStatus.Escalated)
                .Where(t => t.DueAt is { } due && due <= nowUtc)
                .Where(t => t.EscalationNextLookAtUtc is null || t.EscalationNextLookAtUtc <= nowUtc.UtcDateTime)
                .OrderBy(t => t.EscalationNextLookAtUtc ?? DateTime.MinValue)
                .ThenBy(t => t.DueAt!.Value.UtcTicks)
                .Take(maxItems)
                .Select(Copy)
                .ToList());

        public Task<bool> UpdateAsync(ApprovalTask task, int expectedVersion, CancellationToken ct = default)
        {
            var index = Items.FindIndex(t => t.Id == task.Id && t.Version == expectedVersion);
            if (index < 0)
            {
                return Task.FromResult(false);
            }

            task.Version = expectedVersion + 1;
            Items[index] = Copy(task);
            return Task.FromResult(true);
        }

        public Task<bool> UpdateEscalationAsync(ApprovalTask task, int expectedVersion, CancellationToken ct = default) =>
            RefuseTransitionWrites ? Task.FromResult(false) : UpdateAsync(task, expectedVersion, ct);

        // The targeted write the store does: the look time only, on a task still waiting on somebody, no version.
        public Task<bool> ScheduleEscalationLookAsync(Guid taskId, DateTime nextLookAtUtc, string? parkReasonCode, CancellationToken ct = default)
        {
            var stored = Items.FirstOrDefault(t => t.Id == taskId
                && t.Status is ApprovalTaskStatus.WaitingApproval or ApprovalTaskStatus.WaitingEvidence or ApprovalTaskStatus.Escalated);
            if (stored is null)
            {
                return Task.FromResult(false);
            }

            stored.EscalationNextLookAtUtc = nextLookAtUtc;
            if (parkReasonCode is not null)
            {
                stored.LastEscalationReasonCode = parkReasonCode;
            }

            return Task.FromResult(true);
        }
    }

    private sealed class InstanceStore : IWorkflowInstanceRepository
    {
        public List<WorkflowInstance> Items { get; } = [];

        public Task<WorkflowInstance> CreateAsync(WorkflowInstance instance, CancellationToken ct = default)
        {
            Items.Add(instance);
            return Task.FromResult(instance);
        }

        public Task<WorkflowInstance?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(i => i.Id == id) is { } found ? Copy(found) : null);

        public Task<WorkflowInstance?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default) =>
            Task.FromResult<WorkflowInstance?>(null);

        public Task<WorkflowInstance?> GetLatestByObjectRefAsync(string objectRef, string objectType, string objectId, CancellationToken ct = default) =>
            Task.FromResult<WorkflowInstance?>(null);

        public Task<IReadOnlyList<WorkflowInstance>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<WorkflowInstance>>(Items.Select(Copy).ToList());

        public Task<bool> UpdateAsync(WorkflowInstance instance, int expectedVersion, CancellationToken ct = default)
        {
            var index = Items.FindIndex(i => i.Id == instance.Id && i.Version == expectedVersion);
            if (index < 0)
            {
                return Task.FromResult(false);
            }

            instance.Version = expectedVersion + 1;
            Items[index] = Copy(instance);
            return Task.FromResult(true);
        }
    }

    private sealed class RuleStore : ISlaEscalationRuleRepository
    {
        /// <summary>Planted in the exception text: the error log must not carry it.</summary>
        public const string Secret = "mongodb://sweep:hunter2@db.internal/diten";

        public List<SlaEscalationRule> Items { get; } = [];
        public Guid? ThrowFor { get; set; }

        public Task<SlaEscalationRule> CreateAsync(SlaEscalationRule rule, CancellationToken ct = default)
        {
            Items.Add(rule);
            return Task.FromResult(rule);
        }

        public Task<SlaEscalationRule?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(r => r.Id == id));

        public Task<IReadOnlyList<SlaEscalationRule>> ListActiveByTemplateIdAsync(Guid templateId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SlaEscalationRule>>(Items.Where(r => r.TemplateId == templateId).ToList());

        public Task<IReadOnlyList<SlaEscalationRule>> ListActiveAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SlaEscalationRule>>(Items.ToList());

        public Task<SlaEscalationRule?> FindForStepAsync(Guid templateId, string stageCode, string stepCode, CancellationToken ct = default)
        {
            if (ThrowFor == templateId)
            {
                throw new InvalidOperationException("The rule store could not be read: " + Secret);
            }

            return Task.FromResult(Items.FirstOrDefault(r =>
                r.TemplateId == templateId && r.StageCode == stageCode && r.StepCode == stepCode));
        }

        public Task DeactivateRulesForTemplateAsync(Guid templateId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class LogStore : IWorkflowTransitionLogRepository
    {
        public List<WorkflowTransitionLog> Items { get; } = [];

        public Task<WorkflowTransitionLog> CreateAsync(WorkflowTransitionLog log, CancellationToken ct = default)
        {
            Items.Add(log);
            return Task.FromResult(log);
        }

        public Task<WorkflowTransitionLog?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(l => l.Id == id));

        public Task<WorkflowTransitionLog?> GetByTaskActionIdempotencyKeyAsync(
            Guid approvalTaskId, WorkflowTransitionAction action, string idempotencyKey, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(l =>
                l.ApprovalTaskId == approvalTaskId && l.Action == action && l.IdempotencyKey == idempotencyKey));

        public Task<long> GetLatestSequenceNoAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            Task.FromResult(Items.Where(l => l.WorkflowInstanceId == workflowInstanceId).Select(l => l.SequenceNo).DefaultIfEmpty(0L).Max());

        public Task<IReadOnlyList<WorkflowTransitionLog>> ListByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<WorkflowTransitionLog>>(Items.Where(l => l.WorkflowInstanceId == workflowInstanceId).ToList());
    }

    private sealed class RecordingLogger : ILogger<RunWorkflowEscalationsHandler>
    {
        public List<string> Errors { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error)
            {
                // What a sink writes: the message and, when one was handed over, the exception's text.
                Errors.Add(formatter(state, exception) + (exception is null ? string.Empty : " | " + exception));
            }
        }
    }
}
