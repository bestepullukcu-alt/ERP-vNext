using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;

public sealed class RunWorkflowEscalationsHandler
    : IRequestHandler<RunWorkflowEscalationsCommand, Response<RunWorkflowEscalationsResponse>>
{
    private const string SystemActor = "system:workflow-escalation-runner";

    private readonly IApprovalTaskRepository _tasks;
    private readonly IWorkflowInstanceRepository _instances;
    private readonly ISlaEscalationRuleRepository _rules;
    private readonly IWorkflowTransitionLogRepository _logs;
    private readonly IPositionAssignmentRepository? _positionAssignmentRepository;
    private readonly IRuntimeAssignmentSnapshotRepository? _assignmentSnapshotRepository;

    public RunWorkflowEscalationsHandler(
        IApprovalTaskRepository tasks,
        IWorkflowInstanceRepository instances,
        ISlaEscalationRuleRepository rules,
        IWorkflowTransitionLogRepository logs,
        IPositionAssignmentRepository? positionAssignmentRepository = null,
        IRuntimeAssignmentSnapshotRepository? assignmentSnapshotRepository = null)
    {
        _tasks = tasks;
        _instances = instances;
        _rules = rules;
        _logs = logs;
        _positionAssignmentRepository = positionAssignmentRepository;
        _assignmentSnapshotRepository = assignmentSnapshotRepository;
    }

    public async Task<Response<RunWorkflowEscalationsResponse>> Handle(
        RunWorkflowEscalationsCommand request,
        CancellationToken ct)
    {
        var now = (request.Request.NowUtc ?? DateTimeOffset.UtcNow).ToUniversalTime();
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
        var instance = await _instances.GetByIdAsync(task.WorkflowInstanceId, ct);
        if (instance is null)
        {
            return Skipped(task, WorkflowTransitionAction.Escalate, previousTaskStatus, instance, WorkflowReasonCodes.WorkflowTaskInvalidState);
        }

        var rule = await _rules.FindForStepAsync(
            instance.TemplateId == Guid.Empty ? instance.WorkflowTemplateId : instance.TemplateId,
            task.StageCode,
            task.StepCode,
            ct);
        if (rule is null || task.DueAt is null)
        {
            return Skipped(task, WorkflowTransitionAction.Escalate, previousTaskStatus, instance, WorkflowReasonCodes.WorkflowSlaRuleNotFound);
        }

        var elapsedAfterDue = now - task.DueAt.Value.ToUniversalTime();
        var timeoutDue = rule.TimeoutAfterMinutes.HasValue &&
            elapsedAfterDue.TotalMinutes >= rule.TimeoutAfterMinutes.Value - rule.DueInMinutes;
        var action = timeoutDue ? WorkflowTransitionAction.Timeout : WorkflowTransitionAction.Escalate;
        var idempotencyKey = BuildIdempotencyKey(runIdempotencyKey, task.Id, action);
        var existingLog = await _logs.FindByIdempotencyAsync(task.Id, action, idempotencyKey, ct);
        if (existingLog is not null)
        {
            return new WorkflowEscalationResultDto(
                instance.Id,
                task.Id,
                action.ToString(),
                existingLog.FromState ?? previousTaskStatus.ToString(),
                existingLog.ToState ?? task.Status.ToString(),
                existingLog.FromStatus ?? instance.Status.ToString(),
                existingLog.ToStatus ?? instance.Status.ToString(),
                WorkflowReasonCodes.WorkflowEscalationIdempotent,
                true,
                existingLog.Id);
        }

        if (task.Status is not (ApprovalTaskStatus.WaitingApproval or ApprovalTaskStatus.WaitingEvidence or ApprovalTaskStatus.Escalated) ||
            instance.Status is not (WorkflowInstanceStatus.Active or WorkflowInstanceStatus.Escalated))
        {
            return Skipped(task, action, previousTaskStatus, instance, WorkflowReasonCodes.WorkflowTaskInvalidState);
        }

        if (!timeoutDue && elapsedAfterDue.TotalMinutes < rule.EscalateAfterMinutes - rule.DueInMinutes)
        {
            return Skipped(task, action, previousTaskStatus, instance, WorkflowReasonCodes.WorkflowNoOverdueTasks);
        }

        var previousInstanceStatus = instance.Status;
        Guid? newSnapshotId = null;
        string? newAssignee = null;

        if (timeoutDue)
        {
            task.Status = ApprovalTaskStatus.TimedOut;
            task.TimedOutAt = now;
            task.CompletedAt = now;
            task.LastEscalationReasonCode = WorkflowReasonCodes.WorkflowTimeoutProcessed;
            instance.Status = WorkflowInstanceStatus.TimedOut;
            instance.CompletedAt = now;
        }
        else
        {
            if (rule.EscalationPrincipalIds is null || rule.EscalationPrincipalIds.Count == 0)
            {
                return Skipped(task, action, previousTaskStatus, instance, WorkflowReasonCodes.WorkflowAssignmentCandidatesRequired);
            }

            var resolvedEscalationCandidates = await WorkflowCandidateResolver.ResolveAsync(
                rule.EscalationPrincipalIds,
                _positionAssignmentRepository,
                ct);

            if (resolvedEscalationCandidates.Count == 0)
            {
                return Skipped(task, action, previousTaskStatus, instance, WorkflowReasonCodes.WorkflowAssignmentCandidatesRequired);
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
                var createdSnapshot = await _assignmentSnapshotRepository.CreateAsync(snapshot, ct);
                newSnapshotId = createdSnapshot.Id;
            }

            task.Status = ApprovalTaskStatus.Escalated;
            task.EscalatedAt = now;
            task.EscalationLevel++;
            task.LastEscalationReasonCode = WorkflowReasonCodes.WorkflowEscalationProcessed;
            if (newAssignee is not null)
            {
                task.AssigneeRef = newAssignee;
            }
            if (newSnapshotId.HasValue)
            {
                task.AssignmentSnapshotId = newSnapshotId.Value;
            }
            instance.Status = WorkflowInstanceStatus.Escalated;
        }

        instance.LastTransitionAt = now;
        var taskVersion = task.Version;
        var instanceVersion = instance.Version;
        if (!await _tasks.UpdateEscalationAsync(task, taskVersion, ct) ||
            !await _instances.UpdateEscalationOrTimeoutAsync(instance, instanceVersion, ct))
        {
            return Skipped(task, action, previousTaskStatus, instance, WorkflowReasonCodes.WorkflowTransitionConflict);
        }

        var sequenceNo = await _logs.GetLatestSequenceNoAsync(instance.Id, ct) + 1;
        var reasonCode = timeoutDue
            ? WorkflowReasonCodes.WorkflowTimeoutProcessed
            : WorkflowReasonCodes.WorkflowEscalationProcessed;
        var log = await _logs.AppendAsync(new WorkflowTransitionLog
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
            SequenceNo = sequenceNo
        }, ct);

        return new WorkflowEscalationResultDto(
            instance.Id,
            task.Id,
            action.ToString(),
            previousTaskStatus.ToString(),
            task.Status.ToString(),
            previousInstanceStatus.ToString(),
            instance.Status.ToString(),
            reasonCode,
            false,
            log.Id);
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
