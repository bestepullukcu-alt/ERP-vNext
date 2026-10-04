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
    private readonly IPositionRepository? _positionRepository;
    private readonly WorkflowTerminalTransitionWriter _terminal;

    public WorkflowTaskTransitionSupport(
        IApprovalTaskRepository taskRepository,
        IWorkflowInstanceRepository instanceRepository,
        IRuntimeAssignmentSnapshotRepository snapshotRepository,
        IWorkflowTransitionLogRepository logRepository,
        IWorkflowTemplateVersionRepository? versionRepository = null,
        IPositionAssignmentRepository? positionAssignmentRepository = null,
        WorkflowTransitionSeams? seams = null)
    {
        _taskRepository = taskRepository;
        _instanceRepository = instanceRepository;
        _snapshotRepository = snapshotRepository;
        _logRepository = logRepository;
        _versionRepository = versionRepository;
        _positionAssignmentRepository = positionAssignmentRepository;
        _positionRepository = seams?.Positions;
        _terminal = new WorkflowTerminalTransitionWriter(
            taskRepository, instanceRepository, logRepository,
            seams?.Transactions, seams?.Events, seams?.Templates, snapshotRepository);
    }

    public async Task<Response<WorkflowTaskTransitionResponse>> TransitionAsync(
        Guid taskId,
        WorkflowTransitionAction action,
        string actorId,
        string reasonCode,
        string idempotencyKey,
        int? expectedVersion,
        string? comment,
        string? evidenceRef,
        string correlationId,
        CancellationToken ct)
    {
        actorId = actorId.Trim();
        reasonCode = reasonCode.Trim();

        var existingLog = await _logRepository.GetByTaskActionIdempotencyKeyAsync(taskId, action, idempotencyKey, ct);
        if (existingLog is not null)
        {
            return await BuildActorBoundIdempotentResponseAsync(existingLog, actorId, correlationId, ct);
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

        if (expectedVersion.HasValue && task.Version != expectedVersion.Value)
        {
            return StaleVersion(correlationId);
        }

        if (!IsDecidable(task))
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

        if (!IsCandidate(snapshot, actorId))
        {
            return Response<WorkflowTaskTransitionResponse>.Fail(
                "Actor is not assigned to this workflow task.",
                403,
                WorkflowReasonCodes.WorkflowActorDenied,
                correlationId);
        }

        if (StartIsIncomplete(instance))
        {
            return StartNotCompleted(correlationId);
        }

        if (action == WorkflowTransitionAction.Approve && IsStarter(instance, actorId))
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
                _positionRepository,
                ct);
            if (nextCandidates.Count == 0)
            {
                return Response<WorkflowTaskTransitionResponse>.Fail(
                    "At least one assignment candidate is required for the next workflow step.",
                    409,
                    WorkflowReasonCodes.WorkflowAssignmentCandidatesRequired,
                    correlationId);
            }

            // D2 — the starter is not a candidate of the next step either. Nobody left: the approval is refused and
            // the step does not advance (fail-closed; never a silent fall back to the escalation candidates).
            nextCandidates = WithoutStarter(nextCandidates, instance.StartedByUserId, instance.StartedBy);
            if (nextCandidates.Count == 0)
            {
                return Response<WorkflowTaskTransitionResponse>.Fail(
                    "Every candidate of the next workflow step is the person who started the workflow; the approval "
                    + "was not taken.",
                    409,
                    WorkflowReasonCodes.WorkflowNoEligibleApprover,
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

        if (nextStep is null)
        {
            // WP-CL-BE-3 — final approval or rejection makes the instance TERMINAL: task, instance, log and the
            // completion event commit in one Platform transaction.
            var terminalLog = NewLog(task, instance, action, previousTaskStatus, previousInstanceStatus, actorId,
                reasonCode, idempotencyKey, comment, evidenceRef, correlationId,
                await _logRepository.GetLatestSequenceNoAsync(instance.Id, ct) + 1);
            var terminal = await _terminal.CommitAsync(task, task.Version, instance, instance.Version, terminalLog,
                escalationWrite: false, actorId, reasonCode, correlationId, ct);
            return TerminalResponse(terminal, task, instance, previousTaskStatus, previousInstanceStatus, action,
                correlationId);
        }

        /*
         * D1 (WP-WF-ENGINE-CANDIDATES-01) — AN INTERMEDIATE APPROVAL IS ONE WRITE. Any candidate of the step may now
         * decide, so two people CAN act on the same task in the same instant. This used to be five separate writes
         * (task, instance, next task, next snapshot, two log rows): the loser of the version check on the task was
         * turned away, but a crash — or a second writer on the instance — between them left an approved step with
         * no next task. The writer commits all of it in one Platform transaction, or none of it.
         */
        var sequenceNo = await _logRepository.GetLatestSequenceNoAsync(instance.Id, ct) + 1;
        var log = NewLog(task, instance, action, previousTaskStatus, previousInstanceStatus, actorId, reasonCode,
            idempotencyKey, comment, evidenceRef, correlationId, sequenceNo);
        var assignLog = new WorkflowTransitionLog
        {
            TenantId = task.TenantId,
            WorkflowInstanceId = instance.Id,
            ApprovalTaskId = nextTask!.Id,
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
        var step = await _terminal.CommitStepAsync(task, task.Version, instance, instance.Version, log, nextTask,
            nextSnapshot!, assignLog, ct);
        if (step.Outcome != WorkflowTerminalWriteOutcome.Committed)
        {
            return TerminalResponse(step, task, instance, previousTaskStatus, previousInstanceStatus, action,
                correlationId);
        }

        var createdLog = step.Log!;

        return Response<WorkflowTaskTransitionResponse>.Success(
            new WorkflowTaskTransitionResponse(
                instance.Id,
                task.Id,
                previousTaskStatus.ToString(),
                task.Status.ToString(),
                previousInstanceStatus.ToString(),
                instance.Status.ToString(),
                action.ToString(),
                // D2-FIX1 — a twin call's commit found after "unavailable" is a replay, and says so.
                step.Replayed,
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
        int? expectedVersion,
        string? comment,
        string correlationId,
        CancellationToken ct)
    {
        actorId = actorId.Trim();
        reasonCode = reasonCode.Trim();

        // D2 (C4) — THE REPLAY IS READ FIRST. A delegation stored before the target rule (free text, a position) is
        // answered with its stored response on retry; the target is validated only for a NEW delegation.
        var existingLog = await _logRepository.GetByTaskActionIdempotencyKeyAsync(
            taskId,
            WorkflowTransitionAction.Delegate,
            idempotencyKey,
            ct);
        if (existingLog is not null)
        {
            return await BuildActorBoundIdempotentResponseAsync(existingLog, actorId, correlationId, ct);
        }

        // BL-491 — every later gate compares principals letter for letter, so a user id written in another spelling
        // (upper case, braces, no dashes) would pass the "not yourself" check below and then leave the task with a
        // principal nobody's token ever matches. A user id is kept in ONE spelling; any other principal is untouched.
        // D1-FIX1 — and a delegation names a USER. Any candidate may delegate now, so a free-text or `position:`
        // target would lock every other candidate out until the task times out.
        var targetIsUser = TryCanonicalUser(delegatePrincipalId, out var canonicalTarget);

        // "Not to yourself" is asked first and in any spelling of the id (`user:` prefix, upper case, braces).
        if (PrincipalEquals(actorId, targetIsUser ? canonicalTarget : delegatePrincipalId))
        {
            return Response<WorkflowTaskTransitionResponse>.Fail(
                "Delegate principal must be different from the actor.",
                409,
                WorkflowReasonCodes.WorkflowDelegateSameActorInvalid,
                correlationId);
        }

        if (!targetIsUser)
        {
            return Response<WorkflowTaskTransitionResponse>.Fail(
                "A workflow task is delegated to a user: the target must be a user id.",
                400,
                WorkflowReasonCodes.WorkflowDelegateTargetInvalid,
                correlationId);
        }

        delegatePrincipalId = canonicalTarget;

        var context = await LoadTransitionContextAsync(taskId, correlationId, requireAssignment: true, ct);
        if (!context.Response.IsSuccessful)
        {
            return context.Response;
        }

        var task = context.Task!;
        var instance = context.Instance!;
        var snapshot = context.Snapshot!;
        if (expectedVersion.HasValue && task.Version != expectedVersion.Value)
        {
            return StaleVersion(correlationId);
        }
        if (!IsDecidable(task))
        {
            return InvalidState(correlationId);
        }

        if (!IsCandidate(snapshot, actorId))
        {
            return ActorDenied(correlationId);
        }

        // D1-FIX1 — THE STARTER DOES NOT CHOOSE THEIR OWN APPROVER. With "any candidate decides", a starter who sits
        // in the approving seat next to A and B could delegate the task to X: the snapshot becomes [X], A and B are
        // out, X approves. Refused here — asked AFTER the candidate check, so somebody who is not a candidate learns
        // nothing about who started the workflow. (The starter may still REJECT: that is withdrawing their own
        // request, and it hands nobody an approval.)
        if (IsStarter(instance, actorId))
        {
            return Response<WorkflowTaskTransitionResponse>.Fail(
                "The person who started the workflow cannot delegate its approval.",
                409,
                WorkflowReasonCodes.SodViolation,
                correlationId);
        }

        if (StartIsIncomplete(instance))
        {
            return StartNotCompleted(correlationId);
        }

        // BL-491 — the approve gate refuses the starter (SOD_VIOLATION), so a task delegated TO the starter would sit
        // with the one person who can never approve it. The same rule, at the gate that would create that lock —
        // asked after the assignment check, so somebody the task is not with learns nothing about who started it.
        if (IsStarter(instance, delegatePrincipalId))
        {
            return Response<WorkflowTaskTransitionResponse>.Fail(
                "A workflow task cannot be delegated to the person who started the workflow.",
                409,
                WorkflowReasonCodes.SodViolation,
                correlationId);
        }

        var previousTaskStatus = task.Status;
        var previousInstanceStatus = instance.Status;
        var newSnapshot = new RuntimeAssignmentSnapshot
        {
            Id = DeriveDelegateSnapshotId(task.TenantId, task.Id, idempotencyKey),
            TenantId = task.TenantId,
            WorkflowInstanceId = instance.Id,
            ApprovalTaskId = task.Id,
            ResolverSource = "delegate_request",
            ResolvedPrincipalId = delegatePrincipalId,
            CandidatePrincipalIds = [delegatePrincipalId],
            ResolvedAt = DateTime.UtcNow,
            TieBreakExplanation = "single_candidate"
        };
        // D1-FIX1 — the new snapshot is written INSIDE the transition's transaction (PersistAndLogAsync). Only the
        // sequential fallback (in-memory doubles, no executor) keeps the old ensure-then-compare.
        if (!_terminal.IsTransactional)
        {
            var ensured = await _snapshotRepository.EnsureTrustedStartSnapshotAsync(newSnapshot, ct);
            if (!MatchesDelegateSnapshot(ensured, newSnapshot))
            {
                return StaleVersion(correlationId);
            }
        }

        task.Status = ApprovalTaskStatus.WaitingApproval;
        task.AssignmentSnapshotId = newSnapshot.Id;
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
            ct,
            _terminal.IsTransactional ? newSnapshot : null);
    }

    public async Task<Response<WorkflowTaskTransitionResponse>> RequestInfoAsync(
        Guid taskId,
        string actorId,
        string? targetPrincipalId,
        string reasonCode,
        string idempotencyKey,
        int? expectedVersion,
        string? comment,
        string? evidenceRef,
        string correlationId,
        CancellationToken ct)
    {
        actorId = actorId.Trim();
        reasonCode = reasonCode.Trim();

        var existingLog = await _logRepository.GetByTaskActionIdempotencyKeyAsync(
            taskId,
            WorkflowTransitionAction.RequestInfo,
            idempotencyKey,
            ct);
        if (existingLog is not null)
        {
            return await BuildActorBoundIdempotentResponseAsync(existingLog, actorId, correlationId, ct);
        }

        var context = await LoadTransitionContextAsync(taskId, correlationId, requireAssignment: true, ct);
        if (!context.Response.IsSuccessful)
        {
            return context.Response;
        }

        var task = context.Task!;
        var instance = context.Instance!;
        var snapshot = context.Snapshot!;
        if (expectedVersion.HasValue && task.Version != expectedVersion.Value)
        {
            return StaleVersion(correlationId);
        }
        if (!IsDecidable(task))
        {
            return InvalidState(correlationId);
        }

        if (!IsCandidate(snapshot, actorId))
        {
            return ActorDenied(correlationId);
        }

        if (StartIsIncomplete(instance))
        {
            return StartNotCompleted(correlationId);
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
        CancellationToken ct,
        bool allowEscalated = false)
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
        // D2 (C5) — a trusted start that has not finished: a cancel now would take sequence number 1, the start log's,
        // and the start's own recovery would then fail on it (500). The decision gate's rule, the same helper.
        if (StartIsIncomplete(instance))
        {
            return StartNotCompleted(correlationId);
        }

        // B3 — an ESCALATED task is still open (nobody decided; it only moved to somebody else), but only the OWNING
        // module's in-process withdraw may call it off (allowEscalated). The public cancel endpoint never sets it, so
        // an escalated approval cannot be cancelled by whoever holds the cancel permission. D2 — "escalated" is
        // WasEscalated (the level), so a delegation after the escalation no longer opens the public cancel again.
        if (!IsOpen(task) && task.Status != ApprovalTaskStatus.Escalated)
        {
            return InvalidState(correlationId);
        }

        if (WasEscalated(task) && !allowEscalated)
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
        CancellationToken ct,
        RuntimeAssignmentSnapshot? newSnapshot = null)
    {
        if (WorkflowOutcomes.For(instance.Status) is not null)
        {
            // WP-CL-BE-3 — cancel makes the instance TERMINAL: one transaction with the completion event.
            var terminalLog = NewLog(task, instance, action, previousTaskStatus, previousInstanceStatus, actorId,
                reasonCode, idempotencyKey, comment, evidenceRef, correlationId,
                await _logRepository.GetLatestSequenceNoAsync(instance.Id, ct) + 1);
            var terminal = await _terminal.CommitAsync(task, task.Version, instance, instance.Version, terminalLog,
                escalationWrite: false, actorId, reasonCode, correlationId, ct);
            return TerminalResponse(terminal, task, instance, previousTaskStatus, previousInstanceStatus, action,
                correlationId);
        }

        // D1-FIX1 — one write: task + instance (+ the new snapshot) + log, or nothing (see CommitTransitionAsync).
        var log = NewLog(task, instance, action, previousTaskStatus, previousInstanceStatus, actorId, reasonCode,
            idempotencyKey, comment, evidenceRef, correlationId,
            await _logRepository.GetLatestSequenceNoAsync(instance.Id, ct) + 1);
        var written = await _terminal.CommitTransitionAsync(task, task.Version, instance, instance.Version, log,
            newSnapshot, escalationWrite: false, ct);
        return TerminalResponse(written, task, instance, previousTaskStatus, previousInstanceStatus, action,
            correlationId);
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

    private static WorkflowTransitionLog NewLog(
        ApprovalTask task,
        WorkflowInstance instance,
        WorkflowTransitionAction action,
        ApprovalTaskStatus previousTaskStatus,
        WorkflowInstanceStatus previousInstanceStatus,
        string actorId,
        string reasonCode,
        string idempotencyKey,
        string? comment,
        string? evidenceRef,
        string correlationId,
        long sequenceNo) => new()
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

    private static Response<WorkflowTaskTransitionResponse> TerminalResponse(
        WorkflowTerminalWriteResult result,
        ApprovalTask task,
        WorkflowInstance instance,
        ApprovalTaskStatus previousTaskStatus,
        WorkflowInstanceStatus previousInstanceStatus,
        WorkflowTransitionAction action,
        string correlationId) => result.Outcome switch
    {
        WorkflowTerminalWriteOutcome.Committed => Response<WorkflowTaskTransitionResponse>.Success(
            new WorkflowTaskTransitionResponse(
                instance.Id,
                task.Id,
                previousTaskStatus.ToString(),
                task.Status.ToString(),
                previousInstanceStatus.ToString(),
                instance.Status.ToString(),
                action.ToString(),
                // D2-FIX1 — the same for every terminal transition: Replayed means this call found the twin's commit.
                result.Replayed,
                result.Log!.Id,
                correlationId),
            correlationId: correlationId),
        WorkflowTerminalWriteOutcome.TransactionUnavailable => Response<WorkflowTaskTransitionResponse>.Fail(
            "The outcome of the workflow transition is unknown: it may or may not have been committed. Retrying with the same idempotency key is safe.",
            503,
            WorkflowReasonCodes.WorkflowTransactionUnavailable,
            correlationId),
        _ => Response<WorkflowTaskTransitionResponse>.Fail(
            "Workflow transition conflict.",
            409,
            WorkflowReasonCodes.WorkflowTransitionConflict,
            correlationId)
    };

    private static bool IsOpen(ApprovalTask task) =>
        task.Status is ApprovalTaskStatus.WaitingApproval or ApprovalTaskStatus.WaitingEvidence;

    /// <summary>
    /// D1 — a task somebody can still DECIDE (approve, reject, delegate, ask for information). An ESCALATED task is
    /// one of them: nobody decided, it only moved to the escalation candidates. Before this, escalation left a task
    /// that no gate accepted — only a timeout or its owner's withdraw could end it (BL-478). Cancel keeps its own,
    /// narrower rule (<see cref="IsOpen"/>): an escalated task is still not cancellable from the public endpoint.
    /// BL-547 — also the trusted terminal-decision evidence's "still open" (GetTrustedWorkflowTerminalDecisionEvidenceHandler):
    /// the endpoint asks this rule instead of keeping a copy, so an approval it calls open is one the gates accept.
    /// </summary>
    internal static bool IsDecidable(ApprovalTask task) =>
        IsOpen(task) || task.Status == ApprovalTaskStatus.Escalated;

    /// <summary>
    /// D1 — WHO MAY DECIDE: any principal the step resolved to, not only the first of them. The snapshot's candidate
    /// list is the authority; <c>ResolvedPrincipalId</c> / <c>AssigneeRef</c> stay filled with the first candidate and
    /// mean "who to show", and <c>ActionedBy</c> records who actually decided. A snapshot without a candidate list
    /// (none is written that way, but the field is a plain list) falls back to its resolved principal.
    /// </summary>
    internal static bool IsCandidate(RuntimeAssignmentSnapshot snapshot, string actorId) =>
        DecidingPrincipals(snapshot).Any(candidate => PrincipalEquals(candidate, actorId));

    /// <summary>The principals a snapshot lets decide — the ONE list the gate, both "my approvals" reads and the Task
    /// Center projection use, so somebody who sees a task can decide it and somebody who can decide it sees it.</summary>
    internal static IReadOnlyList<string> DecidingPrincipals(RuntimeAssignmentSnapshot snapshot) =>
        snapshot.CandidatePrincipalIds.Count > 0 ? snapshot.CandidatePrincipalIds : [snapshot.ResolvedPrincipalId];

    /// <summary>A task with no assignment snapshot is decidable by nobody (the gate answers 404) — so it is shown to nobody.</summary>
    internal static bool MayDecide(RuntimeAssignmentSnapshot? snapshot, string principalId) =>
        snapshot is not null && IsCandidate(snapshot, principalId);

    /// <summary>
    /// D1 — WHO A TASK IS LISTED FOR, in both "my approvals" reads. With a readable snapshot it is exactly
    /// <see cref="MayDecide"/>: whoever sees the task can decide it, whoever can decide it sees it.
    /// <para>⚠ The one case where the two differ, kept on purpose: a task whose snapshot cannot be read (none, or a
    /// dangling id). Production always writes the two together, so this is a damaged record — it stays listed for its
    /// <c>AssigneeRef</c>, as it always was, so that somebody sees it; the gate still refuses it (404).</para>
    /// </summary>
    internal static bool IsListedFor(ApprovalTask task, RuntimeAssignmentSnapshot? snapshot, string principalId) =>
        snapshot is null ? PrincipalEquals(task.AssigneeRef, principalId) : MayDecide(snapshot, principalId);

    /// <summary>
    /// D1 — THE ONE COMPARISON OF TWO PRINCIPALS (BL-477). Two user ids are the same person whatever their spelling
    /// (upper case, braces, no dashes): compared as GUIDs. Anything that is not a GUID is an opaque principal and is
    /// compared letter for letter. Snapshots written before ids were kept in one spelling match without a migration.
    /// </summary>
    internal static bool PrincipalEquals(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        var leftIsId = Guid.TryParse(left.Trim(), out var leftId);
        var rightIsId = Guid.TryParse(right.Trim(), out var rightId);

        // The empty id is nobody: it equals nothing, not even itself. (The controller refuses an empty actor; this is
        // the helper's own guard, so a zeroed id in a snapshot can never match a zeroed id from anywhere else.)
        if ((leftIsId && leftId == Guid.Empty) || (rightIsId && rightId == Guid.Empty))
        {
            return false;
        }

        return leftIsId && rightIsId
            ? leftId == rightId
            : string.Equals(left.Trim(), right.Trim(), StringComparison.Ordinal);
    }

    private const string UserPrefix = "user:";

    /// <summary>
    /// D1-FIX1 — a USER principal in its one spelling: a user id, optionally written <c>user:{id}</c> (the prefix the
    /// candidate resolver strips). False for anything else — free text, <c>position:…</c>, the empty id.
    /// </summary>
    internal static bool TryCanonicalUser(string? principal, out string canonical)
    {
        var value = (principal ?? string.Empty).Trim();
        if (value.StartsWith(UserPrefix, StringComparison.OrdinalIgnoreCase))
        {
            value = value[UserPrefix.Length..].Trim();
        }

        if (Guid.TryParse(value, out var userId) && userId != Guid.Empty)
        {
            canonical = userId.ToString();
            return true;
        }

        canonical = string.Empty;
        return false;
    }

    /// <summary>
    /// D1-FIX1 — a TRUSTED start that has not finished (task and snapshot written, start log not yet). A decision
    /// taken now would take sequence number 1 — the start log's — and the start could never be completed afterwards.
    /// </summary>
    private static bool StartIsIncomplete(WorkflowInstance instance) =>
        instance.TrustedConsumerClientId is not null && instance.StartCheckpoint != WorkflowStartCheckpoint.Completed;

    private static Response<WorkflowTaskTransitionResponse> StartNotCompleted(string correlationId) =>
        Response<WorkflowTaskTransitionResponse>.Fail(
            "The workflow start has not completed yet. Decide once it has.",
            409,
            WorkflowReasonCodes.WorkflowStartNotCompleted,
            correlationId);

    /// <summary>A principal that is a user id, in the one spelling tokens and snapshots carry it; anything else as given.</summary>
    internal static string CanonicalPrincipal(string principalId) =>
        Guid.TryParse(principalId, out var userId) ? userId.ToString() : principalId;


    /// <summary>
    /// B2 — did <paramref name="actorId"/> start this instance? Decided by the starter's USER ID when the instance has
    /// one (every instance started from now on); an older instance has only the actor name and keeps the old comparison.
    /// </summary>
    internal static bool IsStarter(WorkflowInstance instance, string actorId) =>
        IsStarter(instance.StartedByUserId, instance.StartedBy, actorId);

    /// <summary>D2 — the same question before the instance exists (a start), on D1's one identity comparison: a user
    /// id matches in any spelling; the legacy actor name letter for letter.</summary>
    internal static bool IsStarter(Guid? startedByUserId, string? startedBy, string? principalId)
    {
        if (startedByUserId is { } starter && starter != Guid.Empty)
        {
            return PrincipalEquals(principalId, starter.ToString("D"));
        }

        return !string.IsNullOrWhiteSpace(startedBy) && PrincipalEquals(startedBy, principalId);
    }

    /// <summary>
    /// D2 — THE STARTER IS NOT A CANDIDATE. Dropped from a resolved list right after resolution, at every place a list
    /// is resolved (start, trusted start, next step, escalation): the gate refused the starter's approval anyway, so a
    /// list that names them only offered a seat that could never decide.
    /// </summary>
    internal static IReadOnlyList<string> WithoutStarter(
        IReadOnlyList<string> candidates, Guid? startedByUserId, string? startedBy) =>
        candidates.Where(candidate => !IsStarter(startedByUserId, startedBy, candidate)).ToList();

    /// <summary>
    /// D2 — WAS THIS TASK EVER ESCALATED. Read from the escalation level, not the status: a delegation or a request
    /// for information on an escalated task moves its status back to Waiting*, but the escalation level stays — and
    /// an escalated approval is not cancelled by whoever holds the cancel permission (B3). One rule for the public
    /// cancel and the trusted cancel coordinator.
    /// </summary>
    internal static bool WasEscalated(ApprovalTask task) =>
        task.Status == ApprovalTaskStatus.Escalated || task.EscalationLevel > 0;

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

    private static Response<WorkflowTaskTransitionResponse> StaleVersion(string correlationId) =>
        Response<WorkflowTaskTransitionResponse>.Fail(
            "Workflow task version is stale.",
            409,
            WorkflowReasonCodes.WorkflowTransitionConflict,
            correlationId);

    private async Task<Response<WorkflowTaskTransitionResponse>> BuildActorBoundIdempotentResponseAsync(
        WorkflowTransitionLog existingLog,
        string actorId,
        string correlationId,
        CancellationToken ct)
    {
        if (!string.Equals(existingLog.ActorId, actorId, StringComparison.Ordinal) ||
            !string.Equals(existingLog.ActorRef, actorId, StringComparison.Ordinal))
        {
            return ActorDenied(correlationId);
        }

        return await BuildIdempotentResponseAsync(existingLog, correlationId, ct);
    }

    private static Guid DeriveDelegateSnapshotId(Guid tenantId, Guid taskId, string idempotencyKey)
    {
        var canonicalIdentity = $"{tenantId:D}|{taskId:D}|{WorkflowTransitionAction.Delegate:D}|{idempotencyKey}";
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(canonicalIdentity));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static bool MatchesDelegateSnapshot(
        RuntimeAssignmentSnapshot persisted,
        RuntimeAssignmentSnapshot requested) =>
        persisted.Id == requested.Id &&
        persisted.TenantId == requested.TenantId &&
        persisted.WorkflowInstanceId == requested.WorkflowInstanceId &&
        persisted.ApprovalTaskId == requested.ApprovalTaskId &&
        string.Equals(persisted.ResolverSource, requested.ResolverSource, StringComparison.Ordinal) &&
        string.Equals(persisted.ResolvedPrincipalId, requested.ResolvedPrincipalId, StringComparison.Ordinal) &&
        persisted.CandidatePrincipalIds.SequenceEqual(requested.CandidatePrincipalIds, StringComparer.Ordinal) &&
        string.Equals(persisted.TieBreakExplanation, requested.TieBreakExplanation, StringComparison.Ordinal) &&
        !persisted.IsDeleted;

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
