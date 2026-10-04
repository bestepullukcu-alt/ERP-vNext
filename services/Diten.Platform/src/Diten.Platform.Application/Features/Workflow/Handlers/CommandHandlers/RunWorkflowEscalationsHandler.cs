using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;

public sealed class RunWorkflowEscalationsHandler
    : IRequestHandler<RunWorkflowEscalationsCommand, Response<RunWorkflowEscalationsResponse>>
{
    private const string SystemActor = "system:workflow-escalation-runner";

    /// <summary>
    /// D2-FIX2 item 4 — how long an overdue task the sweep could not act on rests before it is looked at again (never later
    /// than its next SLA threshold — see <see cref="NextLookAt"/>). Three sweeps at the five-minute cadence: a seat that
    /// changes hands under a parked task is noticed within a quarter of an hour, and a backlog nothing can be done about
    /// is read four times an hour instead of every five minutes, ahead of everything else.
    /// </summary>
    internal static readonly TimeSpan InconclusiveRecheckInterval = TimeSpan.FromMinutes(15);

    private readonly IApprovalTaskRepository _tasks;
    private readonly IWorkflowInstanceRepository _instances;
    private readonly ISlaEscalationRuleRepository _rules;
    private readonly IWorkflowTransitionLogRepository _logs;
    private readonly IPositionAssignmentRepository? _positionAssignmentRepository;
    private readonly IRuntimeAssignmentSnapshotRepository? _assignmentSnapshotRepository;
    private readonly TimeProvider _clock;
    private readonly IPositionRepository? _positionRepository;
    private readonly WorkflowTerminalTransitionWriter _terminal;
    private readonly ILogger<RunWorkflowEscalationsHandler> _logger;

    public RunWorkflowEscalationsHandler(
        IApprovalTaskRepository tasks,
        IWorkflowInstanceRepository instances,
        ISlaEscalationRuleRepository rules,
        IWorkflowTransitionLogRepository logs,
        TimeProvider clock,
        IPositionAssignmentRepository? positionAssignmentRepository = null,
        IRuntimeAssignmentSnapshotRepository? assignmentSnapshotRepository = null,
        IPositionRepository? positionRepository = null,
        IPlatformTransactionExecutor? transactions = null,
        ITransactionalIntegrationEventWriter? events = null,
        IWorkflowTemplateRepository? templates = null,
        ILogger<RunWorkflowEscalationsHandler>? logger = null)
    {
        _logger = logger ?? NullLogger<RunWorkflowEscalationsHandler>.Instance;
        _tasks = tasks;
        _instances = instances;
        _rules = rules;
        _logs = logs;
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _positionAssignmentRepository = positionAssignmentRepository;
        _assignmentSnapshotRepository = assignmentSnapshotRepository;
        _positionRepository = positionRepository;
        // WP-CL-BE-3 — a timeout is TERMINAL: it commits with the completion event in one transaction.
        _terminal = new WorkflowTerminalTransitionWriter(tasks, instances, logs, transactions, events, templates,
            assignmentSnapshotRepository);
    }

    public async Task<Response<RunWorkflowEscalationsResponse>> Handle(
        RunWorkflowEscalationsCommand request,
        CancellationToken ct)
    {
        // BL-422 — WHAT "OVERDUE" MEANS IS THE SERVER'S CALL. NowUtc used to be honoured, so anyone holding
        // platform.workflow.escalations.run could escalate or time out tasks in their tenant that were not due yet by
        // sending a date in the future — and the transition log would record it as the system acting on time. A value
        // is REFUSED rather than ignored so a client still sending it finds out. The recurring sweep sends none, and
        // tests move time through the injected TimeProvider, not through the request.
        if (request.Request.NowUtc.HasValue)
        {
            return Response<RunWorkflowEscalationsResponse>.Fail(
                "NowUtc is not accepted: escalations are evaluated against the server clock.",
                400,
                WorkflowReasonCodes.WorkflowEscalationClockNotAccepted,
                request.CorrelationId);
        }

        var now = _clock.GetUtcNow();
        var maxItems = request.Request.MaxItems.GetValueOrDefault(100);
        var overdueTasks = await _tasks.ListOverdueTasksAsync(now, maxItems, ct);
        var results = new List<WorkflowEscalationResultDto>();
        var escalated = 0;
        var timedOut = 0;
        var skipped = 0;

        foreach (var task in overdueTasks)
        {
            var result = await EvaluateTaskAsync(task, now, request.Request.IdempotencyKey, request.CorrelationId, ct);
            results.Add(result);
            if (result.Action == WorkflowTransitionAction.Escalate.ToString() && !result.IsIdempotent &&
                result.ReasonCode == WorkflowReasonCodes.WorkflowEscalationProcessed)
            {
                escalated++;
            }
            else if (result.Action == WorkflowTransitionAction.Timeout.ToString() && !result.IsIdempotent &&
                     result.ReasonCode == WorkflowReasonCodes.WorkflowTimeoutProcessed)
            {
                timedOut++;
            }
            else
            {
                skipped++;
            }
        }

        return Response<RunWorkflowEscalationsResponse>.Success(
            new RunWorkflowEscalationsResponse(
                overdueTasks.Count,
                escalated,
                timedOut,
                skipped,
                results,
                request.CorrelationId),
            correlationId: request.CorrelationId);
    }

    private async Task<WorkflowEscalationResultDto> EvaluateTaskAsync(
        ApprovalTask task,
        DateTimeOffset now,
        string? runIdempotencyKey,
        string correlationId,
        CancellationToken ct)
    {
        var previousTaskStatus = task.Status;

        /*
         * D2-FIX2 item 4 — EVERY WAY OUT OF HERE THAT ACTS ON NOTHING GIVES THE TASK ITS NEXT LOOK TIME. A task skipped
         * without one was read again by the very next sweep, and enough of them (no SLA rule, nobody eligible, already
         * escalated with no timeout …) filled every page while newly overdue tasks starved behind them. Rested, they
         * are read again when their time comes — a parked task included, so a seat that changed hands is noticed.
         */
        SlaEscalationRule? rule = null;
        async Task<WorkflowEscalationResultDto> RestAsync(WorkflowEscalationResultDto skipped, string? parkReasonCode = null)
        {
            if (!await _tasks.ScheduleEscalationLookAsync(task.Id, NextLookAt(now, rule, task), parkReasonCode, ct))
            {
                _logger.LogInformation(
                    "Workflow task {TaskId} is no longer waiting on anybody; no next look was scheduled.", task.Id);
            }

            return skipped;
        }

        var instance = await _instances.GetByIdAsync(task.WorkflowInstanceId, ct);
        if (instance is null)
        {
            return await RestAsync(Skipped(task, WorkflowTransitionAction.Escalate, previousTaskStatus, instance, WorkflowReasonCodes.WorkflowTaskInvalidState));
        }

        rule = await _rules.FindForStepAsync(
            instance.TemplateId == Guid.Empty ? instance.WorkflowTemplateId : instance.TemplateId,
            task.StageCode,
            task.StepCode,
            ct);
        if (rule is null || task.DueAt is null)
        {
            return await RestAsync(Skipped(task, WorkflowTransitionAction.Escalate, previousTaskStatus, instance, WorkflowReasonCodes.WorkflowSlaRuleNotFound));
        }

        var elapsedAfterDue = now - task.DueAt.Value.ToUniversalTime();
        var timeoutDue = rule.TimeoutAfterMinutes.HasValue &&
            elapsedAfterDue.TotalMinutes >= rule.TimeoutAfterMinutes.Value - rule.DueInMinutes;
        var action = timeoutDue ? WorkflowTransitionAction.Timeout : WorkflowTransitionAction.Escalate;
        var idempotencyKey = BuildIdempotencyKey(runIdempotencyKey, task.Id, action);
        var existingLog = await _logs.FindByIdempotencyAsync(task.Id, action, idempotencyKey, ct);
        if (existingLog is not null)
        {
            // Already done under this key (an escalation whose rule has no timeout is found here on every sweep).
            return await RestAsync(new WorkflowEscalationResultDto(
                instance.Id,
                task.Id,
                action.ToString(),
                existingLog.FromState ?? previousTaskStatus.ToString(),
                existingLog.ToState ?? task.Status.ToString(),
                existingLog.FromStatus ?? instance.Status.ToString(),
                existingLog.ToStatus ?? instance.Status.ToString(),
                WorkflowReasonCodes.WorkflowEscalationIdempotent,
                true,
                existingLog.Id));
        }

        if (task.Status is not (ApprovalTaskStatus.WaitingApproval or ApprovalTaskStatus.WaitingEvidence or ApprovalTaskStatus.Escalated) ||
            instance.Status is not (WorkflowInstanceStatus.Active or WorkflowInstanceStatus.Escalated))
        {
            return await RestAsync(Skipped(task, action, previousTaskStatus, instance, WorkflowReasonCodes.WorkflowTaskInvalidState));
        }

        if (!timeoutDue && elapsedAfterDue.TotalMinutes < rule.EscalateAfterMinutes - rule.DueInMinutes)
        {
            // Overdue, not yet at its threshold: looked at again AT the threshold (NextLookAt), not a sweep before.
            return await RestAsync(Skipped(task, action, previousTaskStatus, instance, WorkflowReasonCodes.WorkflowNoOverdueTasks));
        }

        var previousInstanceStatus = instance.Status;
        RuntimeAssignmentSnapshot? escalationSnapshot = null;
        string? newAssignee = null;

        if (timeoutDue)
        {
            task.Status = ApprovalTaskStatus.TimedOut;
            task.TimedOutAt = now;
            task.CompletedAt = now;
            task.LastEscalationReasonCode = WorkflowReasonCodes.WorkflowTimeoutProcessed;
            task.EscalationNextLookAtUtc = null;
            instance.Status = WorkflowInstanceStatus.TimedOut;
            instance.CompletedAt = now;
        }
        else
        {
            if (rule.EscalationPrincipalIds is null || rule.EscalationPrincipalIds.Count == 0)
            {
                return await RestAsync(Skipped(task, action, previousTaskStatus, instance, WorkflowReasonCodes.WorkflowAssignmentCandidatesRequired));
            }

            var resolvedEscalationCandidates = await WorkflowCandidateResolver.ResolveAsync(
                rule.EscalationPrincipalIds,
                _positionAssignmentRepository,
                _positionRepository,
                ct);

            if (resolvedEscalationCandidates.Count == 0)
            {
                return await RestAsync(Skipped(task, action, previousTaskStatus, instance, WorkflowReasonCodes.WorkflowAssignmentCandidatesRequired));
            }

            // D2 — the starter is not an escalation candidate. Nobody left: the escalation is NOT written, the task
            // stays where it is, and the run says so (a rule that escalates only to the starter needs a person).
            resolvedEscalationCandidates = WorkflowTaskTransitionSupport.WithoutStarter(
                resolvedEscalationCandidates, instance.StartedByUserId, instance.StartedBy);
            if (resolvedEscalationCandidates.Count == 0)
            {
                // D2-FIX1 A4 — PARKED, once: the first sweep that finds nobody warns and marks the reason; a later look sees
                // the mark and stays quiet (the reason has not changed). D2-FIX2 item 4 — and the task RESTS until its look
                // time like every other inconclusive one; when it is looked at again its candidates are resolved again, so
                // a seat that changed hands meanwhile escalates it.
                var firstPark = !string.Equals(
                    task.LastEscalationReasonCode, WorkflowReasonCodes.WorkflowNoEligibleApprover, StringComparison.Ordinal);
                if (firstPark)
                {
                    _logger.LogWarning(
                        "Escalation of workflow task {TaskId} (instance {InstanceId}) skipped: every escalation candidate "
                        + "is the person who started the workflow. The task stays with its current approvers.",
                        task.Id, instance.Id);
                }

                return await RestAsync(
                    Skipped(task, action, previousTaskStatus, instance, WorkflowReasonCodes.WorkflowNoEligibleApprover),
                    firstPark ? WorkflowReasonCodes.WorkflowNoEligibleApprover : null);
            }

            newAssignee = resolvedEscalationCandidates[0];
            if (_assignmentSnapshotRepository is not null)
            {
                var snapshot = new RuntimeAssignmentSnapshot
                {
                    TenantId = task.TenantId,
                    WorkflowInstanceId = instance.Id,
                    ApprovalTaskId = task.Id,
                    ResolverSource = "escalation_rules",
                    ResolvedPrincipalId = newAssignee,
                    CandidatePrincipalIds = resolvedEscalationCandidates.ToList(),
                    ResolvedAt = now.UtcDateTime,
                    TieBreakExplanation = resolvedEscalationCandidates.Count == 1
                        ? "single_escalation_candidate"
                        : "lexicographic_first_escalation_principal"
                };
                // D1-FIX1 — written with the task, the instance and the log in ONE transaction (below): an
                // escalation that lost the version check no longer leaves an orphan snapshot behind.
                escalationSnapshot = snapshot;
            }

            task.Status = ApprovalTaskStatus.Escalated;
            task.EscalatedAt = now;
            task.EscalationLevel++;
            task.LastEscalationReasonCode = WorkflowReasonCodes.WorkflowEscalationProcessed;
            task.EscalationNextLookAtUtc = null;
            if (newAssignee is not null)
            {
                task.AssigneeRef = newAssignee;
            }
            if (escalationSnapshot is not null)
            {
                task.AssignmentSnapshotId = escalationSnapshot.Id;
            }
            instance.Status = WorkflowInstanceStatus.Escalated;
        }

        instance.LastTransitionAt = now;
        if (timeoutDue)
        {
            // WP-CL-BE-3 — TimedOut is terminal: task, instance, log and the completion event commit together.
            var timeoutLog = new WorkflowTransitionLog
            {
                TenantId = task.TenantId,
                WorkflowInstanceId = instance.Id,
                ApprovalTaskId = task.Id,
                Action = action,
                FromState = previousTaskStatus.ToString(),
                ToState = task.Status.ToString(),
                FromStatus = previousInstanceStatus.ToString(),
                ToStatus = instance.Status.ToString(),
                ActorId = SystemActor,
                ActorRef = SystemActor,
                ReasonCode = WorkflowReasonCodes.WorkflowTimeoutProcessed,
                IdempotencyKey = idempotencyKey,
                CorrelationId = correlationId,
                SequenceNo = await _logs.GetLatestSequenceNoAsync(instance.Id, ct) + 1
            };
            var terminal = await _terminal.CommitAsync(task, task.Version, instance, instance.Version, timeoutLog,
                escalationWrite: true, completedBy: null, WorkflowReasonCodes.WorkflowTimeoutProcessed,
                correlationId, ct);
            if (terminal.Outcome != WorkflowTerminalWriteOutcome.Committed)
            {
                // The look time is written by a targeted update keyed on the id — never by this changed copy.
                return await RestAsync(Skipped(task, action, previousTaskStatus, instance,
                    terminal.Outcome == WorkflowTerminalWriteOutcome.TransactionUnavailable
                        ? WorkflowReasonCodes.WorkflowTransactionUnavailable
                        : WorkflowReasonCodes.WorkflowTransitionConflict));
            }

            return new WorkflowEscalationResultDto(
                instance.Id,
                task.Id,
                action.ToString(),
                previousTaskStatus.ToString(),
                task.Status.ToString(),
                previousInstanceStatus.ToString(),
                instance.Status.ToString(),
                terminal.Replayed ? WorkflowReasonCodes.WorkflowEscalationIdempotent : WorkflowReasonCodes.WorkflowTimeoutProcessed,
                terminal.Replayed,
                terminal.Log!.Id);
        }

        var reasonCode = WorkflowReasonCodes.WorkflowEscalationProcessed;
        var escalationLog = new WorkflowTransitionLog
        {
            TenantId = task.TenantId,
            WorkflowInstanceId = instance.Id,
            ApprovalTaskId = task.Id,
            Action = action,
            FromState = previousTaskStatus.ToString(),
            ToState = task.Status.ToString(),
            FromStatus = previousInstanceStatus.ToString(),
            ToStatus = instance.Status.ToString(),
            ActorId = SystemActor,
            ActorRef = SystemActor,
            ReasonCode = reasonCode,
            IdempotencyKey = idempotencyKey,
            CorrelationId = correlationId,
            SequenceNo = await _logs.GetLatestSequenceNoAsync(instance.Id, ct) + 1
        };
        var escalated = await _terminal.CommitTransitionAsync(task, task.Version, instance, instance.Version,
            escalationLog, escalationSnapshot, escalationWrite: true, ct);
        if (escalated.Outcome != WorkflowTerminalWriteOutcome.Committed)
        {
            return await RestAsync(Skipped(task, action, previousTaskStatus, instance,
                escalated.Outcome == WorkflowTerminalWriteOutcome.TransactionUnavailable
                    ? WorkflowReasonCodes.WorkflowTransactionUnavailable
                    : WorkflowReasonCodes.WorkflowTransitionConflict));
        }

        var log = escalated.Log!;

        return new WorkflowEscalationResultDto(
            instance.Id,
            task.Id,
            action.ToString(),
            previousTaskStatus.ToString(),
            task.Status.ToString(),
            previousInstanceStatus.ToString(),
            instance.Status.ToString(),
            // D2 (C1) — two runs share the system actor and the deterministic key: the run that only FOUND the other's
            // row after "unavailable" reports a replay, so the escalation is counted once.
            escalated.Replayed ? WorkflowReasonCodes.WorkflowEscalationIdempotent : reasonCode,
            escalated.Replayed,
            log.Id);
    }

    /// <summary>
    /// D2-FIX2 item 4 — when an overdue task the sweep could not act on is looked at again: after
    /// <see cref="InconclusiveRecheckInterval"/>, or at its next SLA threshold (escalation, then timeout) when that comes
    /// sooner — so resting never makes an escalation or a timeout later than the sweep would have found it.
    /// </summary>
    internal static DateTime NextLookAt(DateTimeOffset now, SlaEscalationRule? rule, ApprovalTask task)
    {
        var lookAt = now + InconclusiveRecheckInterval;
        if (rule is not null && task.DueAt is { } dueAt)
        {
            foreach (var afterMinutes in new int?[] { rule.EscalateAfterMinutes, rule.TimeoutAfterMinutes })
            {
                if (afterMinutes is not { } minutes)
                {
                    continue;
                }

                var threshold = dueAt.ToUniversalTime().AddMinutes(minutes - rule.DueInMinutes);
                if (threshold > now && threshold < lookAt)
                {
                    lookAt = threshold;
                }
            }
        }

        return lookAt.UtcDateTime;
    }

    private static WorkflowEscalationResultDto Skipped(
        ApprovalTask task,
        WorkflowTransitionAction action,
        ApprovalTaskStatus previousTaskStatus,
        WorkflowInstance? instance,
        string reasonCode) =>
        new(
            instance?.Id ?? task.WorkflowInstanceId,
            task.Id,
            action.ToString(),
            previousTaskStatus.ToString(),
            task.Status.ToString(),
            instance?.Status.ToString() ?? string.Empty,
            instance?.Status.ToString() ?? string.Empty,
            reasonCode,
            false,
            null);

    private static string BuildIdempotencyKey(
        string? runIdempotencyKey,
        Guid taskId,
        WorkflowTransitionAction action) =>
        string.IsNullOrWhiteSpace(runIdempotencyKey)
            ? $"workflow-sla:{taskId:N}:{action}"
            : $"{runIdempotencyKey.Trim()}:{taskId:N}:{action}";
}
