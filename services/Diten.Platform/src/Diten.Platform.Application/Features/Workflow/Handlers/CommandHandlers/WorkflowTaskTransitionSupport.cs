using Diten.Platform.Application.Common;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;

internal sealed class WorkflowTaskTransitionSupport
{
    private readonly IApprovalTaskRepository _taskRepository;
    private readonly IWorkflowInstanceRepository _instanceRepository;
    private readonly IRuntimeAssignmentSnapshotRepository _snapshotRepository;
    private readonly IWorkflowTransitionLogRepository _logRepository;
    private readonly IWorkflowTemplateVersionRepository? _versionRepository;
    private readonly IPositionAssignmentRepository? _positionAssignmentRepository;

    public WorkflowTaskTransitionSupport(
        IApprovalTaskRepository taskRepository,
        IWorkflowInstanceRepository instanceRepository,
        IRuntimeAssignmentSnapshotRepository snapshotRepository,
        IWorkflowTransitionLogRepository logRepository,
        IWorkflowTemplateVersionRepository? versionRepository = null,
        IPositionAssignmentRepository? positionAssignmentRepository = null)
    {
        _taskRepository = taskRepository;
        _instanceRepository = instanceRepository;
        _snapshotRepository = snapshotRepository;
        _logRepository = logRepository;
        _versionRepository = versionRepository;
        _positionAssignmentRepository = positionAssignmentRepository;
    }

    public async Task<Response<WorkflowTaskTransitionResponse>> TransitionAsync(
        Guid taskId,
        WorkflowTransitionAction action,
        string actorId,
        string reasonCode,
        string idempotencyKey,
        string? comment,
        string? evidenceRef,
        string correlationId,
        CancellationToken ct)
    {
        actorId = actorId.Trim();
        reasonCode = reasonCode.Trim();
        idempotencyKey = idempotencyKey.Trim();

        var existingLog = await _logRepository.GetByTaskActionIdempotencyKeyAsync(taskId, action, idempotencyKey, ct);
        if (existingLog is not null)
        {
            return await BuildIdempotentResponseAsync(existingLog, correlationId, ct);
        }

        var task = await _taskRepository.GetByIdAsync(taskId, ct);
        if (task is null)
        {
            return Response<WorkflowTaskTransitionResponse>.Fail(
                "Workflow task not found.",
                404,
                WorkflowReasonCodes.NotFoundNonLeakage,
                correlationId);
        }

        if (task.Status is not (ApprovalTaskStatus.WaitingApproval or ApprovalTaskStatus.WaitingEvidence))
        {
            return Response<WorkflowTaskTransitionResponse>.Fail(
                "Workflow task is not in a transitionable state.",
                409,
                WorkflowReasonCodes.WorkflowTaskInvalidState,
                correlationId);
        }

        var instance = await _instanceRepository.GetByIdAsync(task.WorkflowInstanceId, ct);
        if (instance is null)
        {
            return Response<WorkflowTaskTransitionResponse>.Fail(
                "Workflow instance not found.",
                404,
                WorkflowReasonCodes.NotFoundNonLeakage,
                correlationId);
        }

        if (task.AssignmentSnapshotId is null)
        {
            return Response<WorkflowTaskTransitionResponse>.Fail(
                "Workflow assignment snapshot not found.",
                404,
                WorkflowReasonCodes.WorkflowAssignmentSnapshotNotFound,
                correlationId);
        }

        var snapshot = await _snapshotRepository.GetByIdAsync(task.AssignmentSnapshotId.Value, ct);
        if (snapshot is null)
        {
            return Response<WorkflowTaskTransitionResponse>.Fail(
                "Workflow assignment snapshot not found.",
                404,
                WorkflowReasonCodes.WorkflowAssignmentSnapshotNotFound,
                correlationId);
        }

        if (!string.Equals(snapshot.ResolvedPrincipalId, actorId, StringComparison.Ordinal))
        {
            return Response<WorkflowTaskTransitionResponse>.Fail(
                "Actor is not assigned to this workflow task.",
                403,
                WorkflowReasonCodes.WorkflowActorDenied,
                correlationId);
        }

        if (action == WorkflowTransitionAction.Approve &&
            !string.IsNullOrWhiteSpace(instance.StartedBy) &&
            string.Equals(instance.StartedBy, actorId, StringComparison.Ordinal))
        {
            return Response<WorkflowTaskTransitionResponse>.Fail(
                "Submitter cannot approve their own workflow.",
                409,
                WorkflowReasonCodes.SodViolation,
                correlationId);
        }

        var previousTaskStatus = task.Status;
        var previousInstanceStatus = instance.Status;
        var now = DateTimeOffset.UtcNow;
        task.Status = action == WorkflowTransitionAction.Approve ? ApprovalTaskStatus.Approved : ApprovalTaskStatus.Rejected;
        task.CompletedAt = now;
        task.ActionedBy = actorId;
        task.ActionReasonCode = reasonCode;
        task.ReasonCode = reasonCode;
        task.IdempotencyKey = idempotencyKey;

        var nextStep = action == WorkflowTransitionAction.Approve
            ? await ResolveNextStepAsync(instance, task, ct)
            : null;
        IReadOnlyList<string> nextCandidates = [];
        if (nextStep is not null)
        {
            nextCandidates = await WorkflowCandidateResolver.ResolveAsync(
                nextStep.CandidatePrincipalIds,
                _positionAssignmentRepository,
                ct);
            if (nextCandidates.Count == 0)
            {
                return Response<WorkflowTaskTransitionResponse>.Fail(
                    "At least one assignment candidate is required for the next workflow step.",
                    409,
                    WorkflowReasonCodes.WorkflowAssignmentCandidatesRequired,
                    correlationId);
            }
        }

        instance.Status = action == WorkflowTransitionAction.Approve
            ? (nextStep is null ? WorkflowInstanceStatus.Completed : WorkflowInstanceStatus.Active)
            : WorkflowInstanceStatus.Rejected;
        instance.CompletedAt = action == WorkflowTransitionAction.Approve && nextStep is not null ? null : now;
        if (nextStep is not null)
        {
            instance.CurrentStage = nextStep.StageCode;
            instance.CurrentStep = nextStep.StepCode;
            instance.DueAt = ResolveDueAtFromStep(nextStep, now);
        }
        instance.LastTransitionAt = now;

        ApprovalTask? nextTask = null;
        RuntimeAssignmentSnapshot? nextSnapshot = null;
        if (nextStep is not null)
        {
            var resolvedPrincipal = nextCandidates[0];
            nextTask = new ApprovalTask
            {
                TenantId = task.TenantId,
                WorkflowInstanceId = instance.Id,
                StageCode = nextStep.StageCode,
                StepCode = nextStep.StepCode,
                Status = ApprovalTaskStatus.WaitingApproval,
                AssigneeRef = resolvedPrincipal,
                ReasonCode = reasonCode,
                IdempotencyKey = idempotencyKey,
                CommentRequired = nextStep.CommentRequired,
                EvidenceRequired = nextStep.EvidenceRequired,
                DueAt = instance.DueAt
            };
            nextSnapshot = new RuntimeAssignmentSnapshot
            {
                TenantId = task.TenantId,
                WorkflowInstanceId = instance.Id,
                ApprovalTaskId = nextTask.Id,
                ResolverSource = "runtime_next_step_candidates",
                ResolvedPrincipalId = resolvedPrincipal,
                CandidatePrincipalIds = nextCandidates.ToList(),
                ResolvedAt = now.UtcDateTime,
                TieBreakExplanation = nextCandidates.Count == 1
                    ? "single_candidate"
                    : "lexicographic_first_principal_after_runtime_resolution"
            };
            nextTask.AssignmentSnapshotId = nextSnapshot.Id;
        }

        var taskVersion = task.Version;
        var instanceVersion = instance.Version;
        if (!await _taskRepository.UpdateAsync(task, taskVersion, ct) ||
            !await _instanceRepository.UpdateAsync(instance, instanceVersion, ct))
        {
            return Response<WorkflowTaskTransitionResponse>.Fail(
                "Workflow transition conflict.",
                409,
                WorkflowReasonCodes.WorkflowTransitionConflict,
                correlationId);
        }

        if (nextTask is not null && nextSnapshot is not null)
        {
            await _taskRepository.CreateAsync(nextTask, ct);
            await _snapshotRepository.CreateAsync(nextSnapshot, ct);
        }

        var sequenceNo = await _logRepository.GetLatestSequenceNoAsync(instance.Id, ct) + 1;
        var log = new WorkflowTransitionLog
        {
            TenantId = task.TenantId,
            WorkflowInstanceId = instance.Id,
            ApprovalTaskId = task.Id,
            Action = action,
            FromState = previousTaskStatus.ToString(),
            ToState = task.Status.ToString(),
            FromStatus = previousInstanceStatus.ToString(),
            ToStatus = instance.Status.ToString(),
            ActorId = actorId,
            ActorRef = actorId,
            ReasonCode = reasonCode,
            IdempotencyKey = idempotencyKey,
            Comment = comment,
            EvidenceRef = evidenceRef,
            CorrelationId = correlationId,
            SequenceNo = sequenceNo
        };
        var createdLog = await _logRepository.CreateAsync(log, ct);

        if (nextTask is not null)
        {
            var assignLog = new WorkflowTransitionLog
            {
                TenantId = task.TenantId,
                WorkflowInstanceId = instance.Id,
                ApprovalTaskId = nextTask.Id,
                Action = WorkflowTransitionAction.Start,
                FromState = null,
                ToState = nextTask.Status.ToString(),
                FromStatus = WorkflowInstanceStatus.Active.ToString(),
                ToStatus = WorkflowInstanceStatus.Active.ToString(),
                ActorId = actorId,
                ActorRef = actorId,
                ReasonCode = reasonCode,
                IdempotencyKey = $"{idempotencyKey}:next:{nextTask.Id}",
                Comment = $"next_step:{nextTask.StageCode}/{nextTask.StepCode}",
                CorrelationId = correlationId,
                SequenceNo = sequenceNo + 1
            };
            await _logRepository.CreateAsync(assignLog, ct);
        }

        return Response<WorkflowTaskTransitionResponse>.Success(
            new WorkflowTaskTransitionResponse(
                instance.Id,
                task.Id,
                previousTaskStatus.ToString(),
                task.Status.ToString(),
                previousInstanceStatus.ToString(),
                instance.Status.ToString(),
                action.ToString(),
                false,
                createdLog.Id,
                correlationId),
            correlationId: correlationId);
    }

    private async Task<WorkflowRuntimeStep?> ResolveNextStepAsync(
        WorkflowInstance instance,
        ApprovalTask task,
        CancellationToken ct)
    {
        if (_versionRepository is null ||
            instance.TemplateVersionId is null ||
            instance.TemplateVersionId == Guid.Empty)
        {
            return null;
        }

        var templateId = instance.TemplateId == Guid.Empty ? instance.WorkflowTemplateId : instance.TemplateId;
        if (templateId == Guid.Empty)
        {
            return null;
        }

        var version = await _versionRepository.GetByIdForTemplateAsync(templateId, instance.TemplateVersionId.Value, ct);
        var steps = WorkflowDefinitionRuntimePlan.FromVersion(version);
        if (steps.Count == 0)
        {
            return null;
        }

        var currentIndex = WorkflowDefinitionRuntimePlan.IndexOf(steps, task.StageCode, task.StepCode);
        return currentIndex >= 0 && currentIndex + 1 < steps.Count ? steps[currentIndex + 1] : null;
    }

    private static DateTimeOffset? ResolveDueAtFromStep(WorkflowRuntimeStep step, DateTimeOffset now) =>
        step.DueInMinutes.HasValue ? now.AddMinutes(step.DueInMinutes.Value) : null;

    public async Task<Response<WorkflowTaskTransitionResponse>> DelegateAsync(
        Guid taskId,
        string actorId,
        string delegatePrincipalId,
        string reasonCode,
        string idempotencyKey,
        string? comment,
        string correlationId,
        CancellationToken ct)
    {
        actorId = actorId.Trim();
        delegatePrincipalId = delegatePrincipalId.Trim();
        reasonCode = reasonCode.Trim();
        idempotencyKey = idempotencyKey.Trim();

        if (string.Equals(actorId, delegatePrincipalId, StringComparison.Ordinal))
        {
            return Response<WorkflowTaskTransitionResponse>.Fail(
                "Delegate principal must be different from the actor.",
                409,
                WorkflowReasonCodes.WorkflowDelegateSameActorInvalid,
                correlationId);
        }

        var existingLog = await _logRepository.GetByTaskActionIdempotencyKeyAsync(
            taskId,
            WorkflowTransitionAction.Delegate,
            idempotencyKey,
            ct);
        if (existingLog is not null)
        {
            return await BuildIdempotentResponseAsync(existingLog, correlationId, ct);
        }

        var context = await LoadTransitionContextAsync(taskId, correlationId, requireAssignment: true, ct);
        if (!context.Response.IsSuccessful)
        {
            return context.Response;
        }

        var task = context.Task!;
        var instance = context.Instance!;
        var snapshot = context.Snapshot!;
        if (!IsOpen(task))
        {
            return InvalidState(correlationId);
        }

        if (!string.Equals(snapshot.ResolvedPrincipalId, actorId, StringComparison.Ordinal))
        {
            return ActorDenied(correlationId);
        }

        var previousTaskStatus = task.Status;
        var previousInstanceStatus = instance.Status;
        var newSnapshot = new RuntimeAssignmentSnapshot
        {
            TenantId = task.TenantId,
            WorkflowInstanceId = instance.Id,
            ApprovalTaskId = task.Id,
            ResolverSource = "delegate_request",
            ResolvedPrincipalId = delegatePrincipalId,
            CandidatePrincipalIds = [delegatePrincipalId],
            ResolvedAt = DateTime.UtcNow,
            TieBreakExplanation = "single_candidate"
        };
        var createdSnapshot = await _snapshotRepository.CreateAsync(newSnapshot, ct);

        task.Status = ApprovalTaskStatus.WaitingApproval;
        task.AssignmentSnapshotId = createdSnapshot.Id;
        task.AssigneeRef = delegatePrincipalId;
        task.ActionedBy = actorId;
        task.ActionReasonCode = reasonCode;
        task.ReasonCode = reasonCode;
        task.IdempotencyKey = idempotencyKey;
        instance.Status = WorkflowInstanceStatus.Active;
        instance.LastTransitionAt = DateTimeOffset.UtcNow;

        return await PersistAndLogAsync(
            task,
            instance,
            previousTaskStatus,
            previousInstanceStatus,
            WorkflowTransitionAction.Delegate,
            actorId,
            reasonCode,
            idempotencyKey,
            comment,
            null,
            correlationId,
            ct);
    }

    public async Task<Response<WorkflowTaskTransitionResponse>> RequestInfoAsync(
        Guid taskId,
        string actorId,
        string? targetPrincipalId,
        string reasonCode,
        string idempotencyKey,
        string? comment,
        string? evidenceRef,
        string correlationId,
        CancellationToken ct)
    {
        actorId = actorId.Trim();
        reasonCode = reasonCode.Trim();
        idempotencyKey = idempotencyKey.Trim();

        var existingLog = await _logRepository.GetByTaskActionIdempotencyKeyAsync(
            taskId,
            WorkflowTransitionAction.RequestInfo,
            idempotencyKey,
            ct);
        if (existingLog is not null)
        {
            return await BuildIdempotentResponseAsync(existingLog, correlationId, ct);
        }

        var context = await LoadTransitionContextAsync(taskId, correlationId, requireAssignment: true, ct);
        if (!context.Response.IsSuccessful)
        {
            return context.Response;
        }

        var task = context.Task!;
        var instance = context.Instance!;
        var snapshot = context.Snapshot!;
        if (!IsOpen(task))
        {
            return InvalidState(correlationId);
        }

        if (!string.Equals(snapshot.ResolvedPrincipalId, actorId, StringComparison.Ordinal))
        {
            return ActorDenied(correlationId);
        }

        var previousTaskStatus = task.Status;
        var previousInstanceStatus = instance.Status;
        task.Status = ApprovalTaskStatus.WaitingEvidence;
        task.ActionedBy = actorId;
        task.ActionReasonCode = reasonCode;
        task.ReasonCode = reasonCode;
        task.IdempotencyKey = idempotencyKey;
        instance.Status = WorkflowInstanceStatus.Active;
        instance.LastTransitionAt = DateTimeOffset.UtcNow;

        var logComment = string.IsNullOrWhiteSpace(targetPrincipalId)
            ? comment
            : string.IsNullOrWhiteSpace(comment)
                ? $"target:{targetPrincipalId.Trim()}"
                : $"{comment.Trim()} target:{targetPrincipalId.Trim()}";

        return await PersistAndLogAsync(
            task,
            instance,
            previousTaskStatus,
            previousInstanceStatus,
            WorkflowTransitionAction.RequestInfo,
            actorId,
            reasonCode,
            idempotencyKey,
            logComment,
            evidenceRef,
            correlationId,
            ct);
    }

    public async Task<Response<WorkflowTaskTransitionResponse>> CancelAsync(
        Guid taskId,
        string actorId,
        string reasonCode,
        string idempotencyKey,
        string? comment,
        string correlationId,
        CancellationToken ct)
    {
        actorId = actorId.Trim();
        reasonCode = reasonCode.Trim();
        idempotencyKey = idempotencyKey.Trim();

        var existingLog = await _logRepository.GetByTaskActionIdempotencyKeyAsync(
            taskId,
            WorkflowTransitionAction.Cancel,
            idempotencyKey,
            ct);
        if (existingLog is not null)
        {
            return await BuildIdempotentResponseAsync(existingLog, correlationId, ct);
        }

        var context = await LoadTransitionContextAsync(taskId, correlationId, requireAssignment: false, ct);
        if (!context.Response.IsSuccessful)
        {
            return context.Response;
        }

        var task = context.Task!;
        var instance = context.Instance!;
        if (!IsOpen(task))
        {
            return InvalidState(correlationId);
        }

        var previousTaskStatus = task.Status;
        var previousInstanceStatus = instance.Status;
        var now = DateTimeOffset.UtcNow;
        task.Status = ApprovalTaskStatus.Cancelled;
        task.CompletedAt = now;
        task.ActionedBy = actorId;
        task.ActionReasonCode = reasonCode;
        task.ReasonCode = reasonCode;
        task.IdempotencyKey = idempotencyKey;
        instance.Status = WorkflowInstanceStatus.Cancelled;
        instance.CompletedAt = now;
        instance.LastTransitionAt = now;

        return await PersistAndLogAsync(
            task,
            instance,
            previousTaskStatus,
            previousInstanceStatus,
            WorkflowTransitionAction.Cancel,
            actorId,
            reasonCode,
            idempotencyKey,
            comment,
            null,
            correlationId,
            ct);
    }

    private async Task<Response<WorkflowTaskTransitionResponse>> PersistAndLogAsync(
        ApprovalTask task,
        WorkflowInstance instance,
        ApprovalTaskStatus previousTaskStatus,
        WorkflowInstanceStatus previousInstanceStatus,
        WorkflowTransitionAction action,
        string actorId,
        string reasonCode,
        string idempotencyKey,
        string? comment,
        string? evidenceRef,
        string correlationId,
        CancellationToken ct)
    {
        var taskVersion = task.Version;
        var instanceVersion = instance.Version;
        if (!await _taskRepository.UpdateAsync(task, taskVersion, ct) ||
            !await _instanceRepository.UpdateAsync(instance, instanceVersion, ct))
        {
            return Response<WorkflowTaskTransitionResponse>.Fail(
                "Workflow transition conflict.",
                409,
                WorkflowReasonCodes.WorkflowTransitionConflict,
                correlationId);
        }

        var sequenceNo = await _logRepository.GetLatestSequenceNoAsync(instance.Id, ct) + 1;
        var log = new WorkflowTransitionLog
        {
            TenantId = task.TenantId,
            WorkflowInstanceId = instance.Id,
            ApprovalTaskId = task.Id,
            Action = action,
            FromState = previousTaskStatus.ToString(),
            ToState = task.Status.ToString(),
            FromStatus = previousInstanceStatus.ToString(),
            ToStatus = instance.Status.ToString(),
            ActorId = actorId,
            ActorRef = actorId,
            ReasonCode = reasonCode,
            IdempotencyKey = idempotencyKey,
            Comment = comment,
            EvidenceRef = evidenceRef,
            CorrelationId = correlationId,
            SequenceNo = sequenceNo
        };
        var createdLog = await _logRepository.CreateAsync(log, ct);

        return Response<WorkflowTaskTransitionResponse>.Success(
            new WorkflowTaskTransitionResponse(
                instance.Id,
                task.Id,
                previousTaskStatus.ToString(),
                task.Status.ToString(),
                previousInstanceStatus.ToString(),
                instance.Status.ToString(),
                action.ToString(),
                false,
                createdLog.Id,
                correlationId),
            correlationId: correlationId);
    }

    private async Task<TransitionContext> LoadTransitionContextAsync(
        Guid taskId,
        string correlationId,
        bool requireAssignment,
        CancellationToken ct)
    {
        var task = await _taskRepository.GetByIdAsync(taskId, ct);
        if (task is null)
        {
            return TransitionContext.Fail(Response<WorkflowTaskTransitionResponse>.Fail(
                "Workflow task not found.",
                404,
                WorkflowReasonCodes.NotFoundNonLeakage,
                correlationId));
        }

        var instance = await _instanceRepository.GetByIdAsync(task.WorkflowInstanceId, ct);
        if (instance is null)
        {
            return TransitionContext.Fail(Response<WorkflowTaskTransitionResponse>.Fail(
                "Workflow instance not found.",
                404,
                WorkflowReasonCodes.NotFoundNonLeakage,
                correlationId));
        }

        if (!requireAssignment)
        {
            return TransitionContext.Success(task, instance, null);
        }

        if (task.AssignmentSnapshotId is null)
        {
            return TransitionContext.Fail(Response<WorkflowTaskTransitionResponse>.Fail(
                "Workflow assignment snapshot not found.",
                404,
                WorkflowReasonCodes.WorkflowAssignmentSnapshotNotFound,
                correlationId));
        }

        var snapshot = await _snapshotRepository.GetByIdAsync(task.AssignmentSnapshotId.Value, ct);
        if (snapshot is null)
        {
            return TransitionContext.Fail(Response<WorkflowTaskTransitionResponse>.Fail(
                "Workflow assignment snapshot not found.",
                404,
                WorkflowReasonCodes.WorkflowAssignmentSnapshotNotFound,
                correlationId));
        }

        return TransitionContext.Success(task, instance, snapshot);
    }

    private static bool IsOpen(ApprovalTask task) =>
        task.Status is ApprovalTaskStatus.WaitingApproval or ApprovalTaskStatus.WaitingEvidence;

    private static Response<WorkflowTaskTransitionResponse> InvalidState(string correlationId) =>
        Response<WorkflowTaskTransitionResponse>.Fail(
            "Workflow task is not in a transitionable state.",
            409,
            WorkflowReasonCodes.WorkflowTaskInvalidState,
            correlationId);

    private static Response<WorkflowTaskTransitionResponse> ActorDenied(string correlationId) =>
        Response<WorkflowTaskTransitionResponse>.Fail(
            "Actor is not assigned to this workflow task.",
            403,
            WorkflowReasonCodes.WorkflowActorDenied,
            correlationId);

    private async Task<Response<WorkflowTaskTransitionResponse>> BuildIdempotentResponseAsync(
        WorkflowTransitionLog existingLog,
        string correlationId,
        CancellationToken ct)
    {
        var task = existingLog.ApprovalTaskId is null
            ? null
            : await _taskRepository.GetByIdAsync(existingLog.ApprovalTaskId.Value, ct);
        var instance = await _instanceRepository.GetByIdAsync(existingLog.WorkflowInstanceId, ct);
        if (task is null || instance is null)
        {
            return Response<WorkflowTaskTransitionResponse>.Fail(
                "Workflow transition conflict.",
                409,
                WorkflowReasonCodes.WorkflowTransitionConflict,
                correlationId);
        }

        return Response<WorkflowTaskTransitionResponse>.Success(
            new WorkflowTaskTransitionResponse(
                instance.Id,
                task.Id,
                existingLog.FromState ?? string.Empty,
                existingLog.ToState ?? task.Status.ToString(),
                existingLog.FromStatus ?? string.Empty,
                existingLog.ToStatus ?? instance.Status.ToString(),
                existingLog.Action.ToString(),
                true,
                existingLog.Id,
                correlationId),
            correlationId: correlationId);
    }

    private sealed record TransitionContext(
        ApprovalTask? Task,
        WorkflowInstance? Instance,
        RuntimeAssignmentSnapshot? Snapshot,
        Response<WorkflowTaskTransitionResponse> Response)
    {
        public static TransitionContext Success(
            ApprovalTask task,
            WorkflowInstance instance,
            RuntimeAssignmentSnapshot? snapshot) =>
            new(task, instance, snapshot, Response<WorkflowTaskTransitionResponse>.Success(statusCode: 204));

        public static TransitionContext Fail(Response<WorkflowTaskTransitionResponse> response) =>
            new(null, null, null, response);
    }
}
