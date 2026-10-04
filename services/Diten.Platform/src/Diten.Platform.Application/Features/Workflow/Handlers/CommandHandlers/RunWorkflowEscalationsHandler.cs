using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;

public sealed class RunWorkflowEscalationsHandler
    : IRequestHandler<RunWorkflowEscalationsCommand, Response<RunWorkflowEscalationsResponse>>
{
    private const string SystemActor = "system:workflow-escalation-runner";

    /// <summary>
    /// D2-FIX2 item 4 — how long an overdue task the sweep could not act on rests before it is looked at again (never later
    /// than its next SLA threshold — see <see cref="NextLookAt"/>). Three sweeps at the five-minute cadence: a seat that
    /// changes hands under a parked task is noticed within a quarter of an hour.
    ///
    /// <para>D2-FIX3 items 4 and 5 — kept ONLY for what may change any moment: an empty seat, nobody but the starter, a
    /// write that lost its race, an evaluation that threw, a start still in flight. Everything that waits on a threshold
    /// rests until that threshold, and what nothing will change rests for <see cref="WorkflowEscalationOptions.StandingRest"/>
    /// — the quarter-hour return of THAT backlog is what pushed real escalations back.</para>
    /// </summary>
    internal static readonly TimeSpan InconclusiveRecheckInterval = TimeSpan.FromMinutes(15);

    private readonly TimeSpan _standingRest;
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
        ILogger<RunWorkflowEscalationsHandler>? logger = null,
        IOptions<WorkflowEscalationOptions>? options = null)
    {
        _logger = logger ?? NullLogger<RunWorkflowEscalationsHandler>.Instance;
        _standingRest = WorkflowEscalationOptions.StandingRestOf(options?.Value, InconclusiveRecheckInterval);
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
            /*
             * D2-FIX3 item 5 — ONE TASK CANNOT STOP THE SWEEP. An evaluation that threw used to end the whole run: the task
             * was never rested, and (never looked at, so first in the read) it was read first again five minutes later and
             * threw again — the tenant's sweep was dead. Now it is logged, rested for a quarter of an hour, and the run
             * goes on to the next task.
             */
            var statusBefore = task.Status;
            WorkflowEscalationResultDto result;
            try
            {
                result = await EvaluateTaskAsync(task, now, request.Request.IdempotencyKey, request.CorrelationId, ct);
            }
            catch (Exception failure) when (failure is not OperationCanceledException)
            {
                result = await RestAfterFailureAsync(task, statusBefore, now, failure, ct);
            }

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
        /*
         * D2-FIX3 item 4 — HOW LONG it rests depends on what it is waiting for (see NextLookAt):
         *   • _standingRest (a day, operator setting) for what no look will change — no rule, no timeout left, an instance
         *     gone or closed — bounded by the next real threshold when one is still to come;
         *   • the same bound for a task waiting on its threshold: it is looked at AT the threshold, not every quarter hour;
         *   • InconclusiveRecheckInterval for what may change any moment (a seat, a lost race, a start in flight).
         * <paramref name="thresholds"/> false: a stray under a closed instance is never escalated at a threshold either.
         */
        SlaEscalationRule? rule = null;
        async Task<WorkflowEscalationResultDto> RestAsync(
            WorkflowEscalationResultDto skipped, TimeSpan rest, string? parkReasonCode = null, bool thresholds = true)
        {
            if (!await _tasks.ScheduleEscalationLookAsync(
                    task.Id, NextLookAt(now, thresholds ? rule : null, task, rest), parkReasonCode, ct))
            {
                _logger.LogInformation(
                    "Workflow task {TaskId} is no longer waiting on anybody; no next look was scheduled.", task.Id);
            }

            return skipped;
        }

        var instance = await _instances.GetByIdAsync(task.WorkflowInstanceId, ct);
        if (instance is null)
        {
            return await RestAsync(Skipped(task, WorkflowTransitionAction.Escalate, previousTaskStatus, instance, WorkflowReasonCodes.WorkflowTaskInvalidState),
                _standingRest);
        }

        rule = await _rules.FindForStepAsync(
            instance.TemplateId == Guid.Empty ? instance.WorkflowTemplateId : instance.TemplateId,
            task.StageCode,
            task.StepCode,
            ct);
        if (rule is null || task.DueAt is null)
        {
            return await RestAsync(Skipped(task, WorkflowTransitionAction.Escalate, previousTaskStatus, instance, WorkflowReasonCodes.WorkflowSlaRuleNotFound),
                _standingRest);
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
                existingLog.Id),
                // Nothing further happens to it until its next threshold (a timeout still to come), if it has one.
                _standingRest);
        }

        if (task.Status is not (ApprovalTaskStatus.WaitingApproval or ApprovalTaskStatus.WaitingEvidence or ApprovalTaskStatus.Escalated) ||
            instance.Status is not (WorkflowInstanceStatus.Active or WorkflowInstanceStatus.Escalated))
        {
            // A stray: its instance is closed (or it is not waiting itself). A Pending instance is the one transient case
            // — a start still in flight — and is looked at again soon, its thresholds still counting.
            var inFlight = instance.Status == WorkflowInstanceStatus.Pending;
            return await RestAsync(Skipped(task, action, previousTaskStatus, instance, WorkflowReasonCodes.WorkflowTaskInvalidState),
                inFlight ? InconclusiveRecheckInterval : _standingRest, thresholds: inFlight);
        }

        if (!timeoutDue && elapsedAfterDue.TotalMinutes < rule.EscalateAfterMinutes - rule.DueInMinutes)
        {
            // Overdue, not yet at its threshold: looked at again AT the threshold (NextLookAt), not a sweep before.
            // D2-FIX3 item 4 — and not every quarter hour on the way there either: bounded by the standing rest only.
            return await RestAsync(Skipped(task, action, previousTaskStatus, instance, WorkflowReasonCodes.WorkflowNoOverdueTasks),
                _standingRest);
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
                // D2-FIX3 item 4 — this rule can never escalate; only its timeout (if any) is still to come.
                return await RestAsync(Skipped(task, action, previousTaskStatus, instance, WorkflowReasonCodes.WorkflowAssignmentCandidatesRequired),
                    _standingRest);
            }

            var resolvedEscalationCandidates = await WorkflowCandidateResolver.ResolveAsync(
                rule.EscalationPrincipalIds,
                _positionAssignmentRepository,
                _positionRepository,
                ct);

            if (resolvedEscalationCandidates.Count == 0)
            {
                // An empty seat: somebody may take it any moment.
                return await RestAsync(Skipped(task, action, previousTaskStatus, instance, WorkflowReasonCodes.WorkflowAssignmentCandidatesRequired),
                    InconclusiveRecheckInterval);
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
                    InconclusiveRecheckInterval,
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
                        : WorkflowReasonCodes.WorkflowTransitionConflict),
                    InconclusiveRecheckInterval);
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
                    : WorkflowReasonCodes.WorkflowTransitionConflict),
                InconclusiveRecheckInterval);
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
    /// D2-FIX2 item 4 — when an overdue task the sweep could not act on is looked at again: after <paramref name="rest"/>,
    /// or at its next SLA threshold (escalation or timeout, whichever comes first and is still ahead) when that comes
    /// sooner — so resting never makes an escalation or a timeout later than the sweep would have found it.
    /// D2-FIX3 item 4 — the rest is the caller's: the standing rest for a task waiting on a threshold or on nothing, the
    /// short recheck for one that may change any moment. A null <paramref name="rule"/> counts no threshold.
    /// </summary>
    internal static DateTime NextLookAt(DateTimeOffset now, SlaEscalationRule? rule, ApprovalTask task, TimeSpan rest)
    {
        var lookAt = now + rest;
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

    /// <summary>
    /// D2-FIX3 item 5 — a task whose evaluation threw: an error line naming the task and the exception's TYPE only — never
    /// its message or stack, which can carry what a log must not (a connection string, a token) — and a quarter-hour rest,
    /// so it neither stops the run nor comes first in the next read. A rest that itself fails is logged the same way; the
    /// next sweep then reads the task again, and the run still goes on.
    /// </summary>
    private async Task<WorkflowEscalationResultDto> RestAfterFailureAsync(
        ApprovalTask task, ApprovalTaskStatus statusBefore, DateTimeOffset now, Exception failure, CancellationToken ct)
    {
        var lookAt = NextLookAt(now, null, task, InconclusiveRecheckInterval);
        _logger.LogError(
            "The SLA sweep could not evaluate workflow task {TaskId} (instance {InstanceId}): {ExceptionType}. "
            + "It rests until {NextLookAtUtc:o} and the sweep goes on.",
            task.Id, task.WorkflowInstanceId, failure.GetType().Name, lookAt);
        try
        {
            await _tasks.ScheduleEscalationLookAsync(task.Id, lookAt, null, ct);
        }
        catch (Exception restFailure) when (restFailure is not OperationCanceledException)
        {
            _logger.LogError(
                "Workflow task {TaskId} could not be rested either ({ExceptionType}); the next sweep reads it again.",
                task.Id, restFailure.GetType().Name);
        }

        return new WorkflowEscalationResultDto(
            task.WorkflowInstanceId,
            task.Id,
            WorkflowTransitionAction.Escalate.ToString(),
            statusBefore.ToString(),
            statusBefore.ToString(),
            string.Empty,
            string.Empty,
            WorkflowReasonCodes.WorkflowTransactionUnavailable,
            false,
            null);
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
