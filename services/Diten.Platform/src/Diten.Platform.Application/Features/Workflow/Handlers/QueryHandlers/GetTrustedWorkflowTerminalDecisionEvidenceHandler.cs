using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Workflow.Queries;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Handlers.QueryHandlers;

public sealed class GetTrustedWorkflowTerminalDecisionEvidenceHandler
    : IRequestHandler<GetTrustedWorkflowTerminalDecisionEvidenceQuery, Response<TrustedWorkflowTerminalDecisionEvidence>>
{
    private readonly IWorkflowInstanceRepository _instances;
    private readonly IApprovalTaskRepository _tasks;
    private readonly IWorkflowTransitionLogRepository _logs;

    public GetTrustedWorkflowTerminalDecisionEvidenceHandler(
        IWorkflowInstanceRepository instances,
        IApprovalTaskRepository tasks,
        IWorkflowTransitionLogRepository logs)
    {
        _instances = instances;
        _tasks = tasks;
        _logs = logs;
    }

    public Task<Response<TrustedWorkflowTerminalDecisionEvidence>> Handle(
        GetTrustedWorkflowTerminalDecisionEvidenceQuery request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return EvaluateAsync(request, mayReread: true, ct);
    }

    /// <summary>
    /// Reads the instance, then its tasks, then its logs, and answers from what it read. The three reads are NOT one
    /// snapshot (BL-547-FIX1): a transition can commit between them. <paramref name="mayReread"/> is the one second look
    /// that closes that window — see the end of the non-terminal branch.
    /// </summary>
    private async Task<Response<TrustedWorkflowTerminalDecisionEvidence>> EvaluateAsync(
        GetTrustedWorkflowTerminalDecisionEvidenceQuery request,
        bool mayReread,
        CancellationToken ct)
    {
        var instance = await _instances.GetByIdAsync(request.WorkflowInstanceId, ct);
        if (instance is null ||
            instance.TrustedConsumerClientId != request.TrustedConsumerClientId ||
            !string.Equals(instance.ObjectType, request.ExpectedObjectType, StringComparison.Ordinal) ||
            !string.Equals(instance.ObjectId, request.ExpectedObjectId, StringComparison.Ordinal) ||
            !HasCoherentTrustedStartIdentity(instance))
        {
            return Response<TrustedWorkflowTerminalDecisionEvidence>.Fail(
                "Workflow instance not found.",
                404,
                WorkflowReasonCodes.NotFoundNonLeakage,
                request.CorrelationId);
        }

        var tasks = await _tasks.ListByInstanceIdAsync(instance.Id, ct);
        var logs = await _logs.ListByInstanceIdAsync(instance.Id, ct);
        if (!HasCoherentTrustedStartProof(instance, tasks, logs))
        {
            return Response<TrustedWorkflowTerminalDecisionEvidence>.Fail(
                "Workflow instance not found.",
                404,
                WorkflowReasonCodes.NotFoundNonLeakage,
                request.CorrelationId);
        }
        if (!HasStrictMonotonicSequence(logs))
        {
            return Inconsistent("Workflow transition history is not strictly monotonic.", request.CorrelationId);
        }

        var decisionLogs = logs
            .Where(x => x.Action is WorkflowTransitionAction.Approve or WorkflowTransitionAction.Reject)
            .OrderBy(x => x.SequenceNo)
            .ToList();
        var expectedAction = instance.Status switch
        {
            WorkflowInstanceStatus.Completed or WorkflowInstanceStatus.Approved =>
                WorkflowTransitionAction.Approve,
            WorkflowInstanceStatus.Rejected => WorkflowTransitionAction.Reject,
            _ => (WorkflowTransitionAction?)null
        };
        if (expectedAction is null)
        {
            if (IsCoherentNonTerminal(instance.Status, tasks, decisionLogs))
            {
                return NotTerminal(request.CorrelationId);
            }

            /*
             * BL-547 — AN APPROVAL THAT ENDED WITH NOBODY DECIDING HAS ITS OWN CODE. Cancelled (the trusted consumer's own
             * cancel, or the public task cancel) and TimedOut (the SLA ran out) used to be answered "inconsistent", the code that means
             * "the engine's own facts contradict each other" — a consumer could not tell a closed approval from a corrupt
             * one, and quarantined both. The answer is the closed-without-decision code only when the facts HOLD TOGETHER
             * (see IsCoherentClosedWithoutDecision): a consumer that hears it may stop waiting and take its request back,
             * so it must be true that no decision can still come. Facts that do not hold together stay "inconsistent".
             */
            if (instance.Status is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.TimedOut)
            {
                return IsCoherentClosedWithoutDecision(instance, tasks, logs, decisionLogs)
                    ? ClosedWithoutDecision(instance.Status, request.CorrelationId)
                    : Inconsistent("Workflow closed-without-decision facts are contradictory.", request.CorrelationId);
            }

            /*
             * BL-547-FIX1 — A RUNNING INSTANCE WHOSE FACTS DO NOT HOLD TOGETHER MAY ONLY HAVE BEEN READ IN THE MIDDLE OF A
             * TRANSITION. The instance, its tasks and its logs are three reads, not a snapshot: if the instance was read
             * Active or Escalated and the final approve / reject / cancel / timeout committed before the tasks or the logs
             * were read, the facts of a DECIDED approval look contradictory, and "inconsistent" is the answer some consumers
             * release a request on. So: read all three ONCE more and answer from that (decided now → the ordinary terminal
             * path, closed now → the closed answer; still incoherent → inconsistent, and no third look). A coherent answer
             * costs no extra read, and a closed instance is not read again (nothing about it changes any more).
             */
            if (mayReread
                && instance.Status is WorkflowInstanceStatus.Pending
                    or WorkflowInstanceStatus.Active
                    or WorkflowInstanceStatus.Escalated)
            {
                return await EvaluateAsync(request, mayReread: false, ct);
            }

            return Inconsistent("Workflow non-terminal facts are contradictory.", request.CorrelationId);
        }

        if (decisionLogs.Count == 0)
        {
            return Inconsistent("Workflow terminal transition proof is missing or contradictory.", request.CorrelationId);
        }

        var terminalLog = decisionLogs[^1];
        if (terminalLog.Action != expectedAction
            || terminalLog.SequenceNo != logs[^1].SequenceNo
            || decisionLogs.Take(decisionLogs.Count - 1).Any(x => x.Action != WorkflowTransitionAction.Approve)
            || !decisionLogs.Take(decisionLogs.Count - 1).All(x => IsCoherentPriorApproval(x, tasks)))
        {
            return Inconsistent("Workflow terminal transition proof is missing or contradictory.", request.CorrelationId);
        }

        var task = FindSingleTask(tasks, terminalLog.ApprovalTaskId);
        var expectedTaskStatus = expectedAction == WorkflowTransitionAction.Approve
            ? ApprovalTaskStatus.Approved
            : ApprovalTaskStatus.Rejected;
        if (task is null ||
            task.Status != expectedTaskStatus ||
            task.CompletedAt is null ||
            string.IsNullOrWhiteSpace(terminalLog.ActorId) ||
            !string.Equals(terminalLog.ActorRef, terminalLog.ActorId, StringComparison.Ordinal) ||
            instance.TemplateVersionId is null ||
            instance.TemplateVersionId == Guid.Empty ||
            instance.CompletedAt is null ||
            terminalLog.SequenceNo <= 0 ||
            !string.Equals(terminalLog.ToStatus, instance.Status.ToString(), StringComparison.Ordinal) ||
            !string.Equals(terminalLog.ToState, task.Status.ToString(), StringComparison.Ordinal) ||
            !string.Equals(task.ActionedBy, terminalLog.ActorId, StringComparison.Ordinal) ||
            !string.Equals(task.ActionReasonCode, terminalLog.ReasonCode, StringComparison.Ordinal))
        {
            return Inconsistent("Workflow terminal task, instance, and log facts are inconsistent.", request.CorrelationId);
        }

        var evidence = new TrustedWorkflowTerminalDecisionEvidence(
            instance.Id,
            task.Id,
            instance.TemplateId,
            instance.TemplateVersionId.Value,
            instance.ObjectType,
            instance.ObjectId,
            instance.ObjectRef,
            terminalLog.Action.ToString(),
            terminalLog.ActorId,
            terminalLog.ReasonCode,
            terminalLog.CreatedAt,
            terminalLog.SequenceNo,
            task.Status.ToString(),
            instance.Status.ToString(),
            terminalLog.CorrelationId ?? request.CorrelationId);
        return Response<TrustedWorkflowTerminalDecisionEvidence>.Success(evidence, correlationId: request.CorrelationId);
    }

    private static Response<TrustedWorkflowTerminalDecisionEvidence> Inconsistent(
        string message,
        string correlationId) =>
        Response<TrustedWorkflowTerminalDecisionEvidence>.Fail(
            message,
            409,
            WorkflowReasonCodes.WorkflowTerminalEvidenceInconsistent,
            correlationId);

    private static Response<TrustedWorkflowTerminalDecisionEvidence> NotTerminal(string correlationId) =>
        Response<TrustedWorkflowTerminalDecisionEvidence>.Fail(
            "Workflow decision is not terminal yet.",
            409,
            WorkflowReasonCodes.WorkflowDecisionNotTerminal,
            correlationId);

    /// <summary>
    /// BL-547. The reason is told in the message only: the answer travels in the envelope the consumers already read, and
    /// that envelope is closed (MDM's client accepts exactly six keys and files anything else as an unreadable response),
    /// so a machine-readable sub-reason is a contract change for the consumers to take, not something to add here.
    /// </summary>
    private static Response<TrustedWorkflowTerminalDecisionEvidence> ClosedWithoutDecision(
        WorkflowInstanceStatus status,
        string correlationId) =>
        Response<TrustedWorkflowTerminalDecisionEvidence>.Fail(
            status == WorkflowInstanceStatus.TimedOut
                ? "Workflow instance timed out without a terminal decision."
                : "Workflow instance was cancelled without a terminal decision.",
            409,
            WorkflowReasonCodes.WorkflowInstanceClosedWithoutDecision,
            correlationId);

    private static bool HasStrictMonotonicSequence(IReadOnlyList<WorkflowTransitionLog> logs)
    {
        long previous = 0;
        foreach (var log in logs.OrderBy(x => x.SequenceNo))
        {
            if (log.SequenceNo != previous + 1) return false;
            previous = log.SequenceNo;
        }

        return true;
    }

    private static bool HasCoherentTrustedStartIdentity(WorkflowInstance instance) =>
        instance.TrustedConsumerClientId is { } clientId
        && clientId != Guid.Empty
        && instance.DelegatedMakerUserId is { } makerId
        && makerId != Guid.Empty
        && instance.StartCheckpoint == WorkflowStartCheckpoint.Completed
        && instance.InitialApprovalTaskId is { } taskId
        && taskId != Guid.Empty
        && instance.InitialAssignmentSnapshotId is { } snapshotId
        && snapshotId != Guid.Empty
        && instance.StartTransitionLogId is { } logId
        && logId != Guid.Empty
        && !string.IsNullOrWhiteSpace(instance.IdempotencyKey)
        && !string.IsNullOrWhiteSpace(instance.StartRequestFingerprint)
        && string.Equals(instance.StartedBy, makerId.ToString("D"), StringComparison.Ordinal);

    private static bool HasCoherentTrustedStartProof(
        WorkflowInstance instance,
        IReadOnlyList<ApprovalTask> tasks,
        IReadOnlyList<WorkflowTransitionLog> logs)
    {
        var initialTask = FindSingleTask(tasks, instance.InitialApprovalTaskId);
        var startLogs = logs.Where(x => x.Id == instance.StartTransitionLogId).Take(2).ToArray();
        if (initialTask is null || initialTask.WorkflowInstanceId != instance.Id || startLogs.Length != 1)
        {
            return false;
        }

        var startLog = startLogs[0];
        return startLog.WorkflowInstanceId == instance.Id
               && startLog.ApprovalTaskId == initialTask.Id
               && startLog.Action == WorkflowTransitionAction.Start
               && startLog.SequenceNo == 1
               && string.Equals(startLog.ActorId, instance.StartedBy, StringComparison.Ordinal)
               && string.Equals(startLog.ActorRef, instance.StartedBy, StringComparison.Ordinal)
               && startLog.FromStatus is null
               && string.Equals(startLog.ToState, WorkflowInstanceStatus.Active.ToString(), StringComparison.Ordinal)
               && string.Equals(startLog.ToStatus, WorkflowInstanceStatus.Active.ToString(), StringComparison.Ordinal);
    }

    private static bool IsCoherentNonTerminal(
        WorkflowInstanceStatus instanceStatus,
        IReadOnlyList<ApprovalTask> tasks,
        IReadOnlyList<WorkflowTransitionLog> decisionLogs)
    {
        if (decisionLogs.Any(x => x.Action != WorkflowTransitionAction.Approve)
            || !decisionLogs.All(x => IsCoherentPriorApproval(x, tasks)))
        {
            return false;
        }

        return instanceStatus switch
        {
            WorkflowInstanceStatus.Pending => decisionLogs.Count == 0 && tasks.Count == 0,
            // BL-547 — an SLA escalation moves the TASK and the INSTANCE to Escalated, and both stay open: nobody has
            // decided, the approval only moved to somebody else, and the decision gates still take their decision. So an
            // Escalated instance is as open as an Active one, and an Active instance may carry an escalated task as well
            // as a waiting one (what "open" means is IsOpenTask's).
            WorkflowInstanceStatus.Active or WorkflowInstanceStatus.Escalated => tasks.Count(IsOpenTask) == 1,
            _ => false
        };
    }

    /// <summary>
    /// BL-547 — somebody can still decide this task: WaitingApproval, WaitingEvidence or Escalated. It is the decision
    /// gates' own rule (<see cref="WorkflowTaskTransitionSupport.IsDecidable"/>), asked here rather than copied, so what
    /// this endpoint calls "open" is what approve, reject, delegate and request-info accept.
    /// </summary>
    private static bool IsOpenTask(ApprovalTask task) => WorkflowTaskTransitionSupport.IsDecidable(task);

    /// <summary>
    /// BL-547 — the facts of an approval that was cancelled or timed out, and that nobody decided. They hold together
    /// only when NOTHING CAN STILL BE DECIDED, because a consumer that hears "closed without a decision" gives its
    /// request back and stops reading: a decision that could still land afterwards would be an approval nobody applies.
    /// <list type="bullet">
    /// <item>Every decision log is an earlier step's approval (<see cref="IsCoherentPriorApproval"/>, unchanged): a
    /// reject, or an approve that finished the instance, contradicts a cancelled or timed-out one. And every APPROVED task
    /// has its approval log (BL-547-FIX1): an approved task with none may be a decision this endpoint cannot see, and
    /// "closed" would let a consumer drop it.</item>
    /// <item>Every task is closed — an earlier step's approval, or the cancelled / timed-out one. A task still waiting,
    /// escalated or rejected under a closed instance can still be decided (the gates look at the task) or is a contradiction.</item>
    /// <item>The instance's last transition is the one that closed it (Cancel for Cancelled, Timeout for TimedOut), on a
    /// task that carries the same ending, and says the instance's own status.</item>
    /// </list>
    /// </summary>
    private static bool IsCoherentClosedWithoutDecision(
        WorkflowInstance instance,
        IReadOnlyList<ApprovalTask> tasks,
        IReadOnlyList<WorkflowTransitionLog> logs,
        IReadOnlyList<WorkflowTransitionLog> decisionLogs)
    {
        WorkflowTransitionAction closingAction;
        ApprovalTaskStatus closingTaskStatus;
        switch (instance.Status)
        {
            case WorkflowInstanceStatus.Cancelled:
                closingAction = WorkflowTransitionAction.Cancel;
                closingTaskStatus = ApprovalTaskStatus.Cancelled;
                break;
            case WorkflowInstanceStatus.TimedOut:
                closingAction = WorkflowTransitionAction.Timeout;
                closingTaskStatus = ApprovalTaskStatus.TimedOut;
                break;
            default:
                return false;
        }

        if (!decisionLogs.All(x => IsCoherentPriorApproval(x, tasks))
            || tasks.Count(x => x.Status == ApprovalTaskStatus.Approved) != decisionLogs.Count
            || tasks.Any(x => x.Status is not (ApprovalTaskStatus.Approved
                or ApprovalTaskStatus.Cancelled or ApprovalTaskStatus.TimedOut)))
        {
            return false;
        }

        var closing = logs.MaxBy(x => x.SequenceNo);
        var closingTask = closing is null ? null : FindSingleTask(tasks, closing.ApprovalTaskId);
        return closing is not null
               && closing.Action == closingAction
               && string.Equals(closing.ToStatus, instance.Status.ToString(), StringComparison.Ordinal)
               && closingTask is not null
               && closingTask.Status == closingTaskStatus;
    }

    private static bool IsCoherentPriorApproval(
        WorkflowTransitionLog log,
        IReadOnlyList<ApprovalTask> tasks)
    {
        var task = FindSingleTask(tasks, log.ApprovalTaskId);
        return log.Action == WorkflowTransitionAction.Approve
               && task is not null
               && task.Status == ApprovalTaskStatus.Approved
               && task.CompletedAt is not null
               && !string.IsNullOrWhiteSpace(log.ActorId)
               && string.Equals(log.ActorRef, log.ActorId, StringComparison.Ordinal)
               && string.Equals(task.ActionedBy, log.ActorId, StringComparison.Ordinal)
               && string.Equals(task.ActionReasonCode, log.ReasonCode, StringComparison.Ordinal)
               && string.Equals(log.ToState, ApprovalTaskStatus.Approved.ToString(), StringComparison.Ordinal)
               && string.Equals(log.ToStatus, WorkflowInstanceStatus.Active.ToString(), StringComparison.Ordinal);
    }

    private static ApprovalTask? FindSingleTask(
        IReadOnlyList<ApprovalTask> tasks,
        Guid? taskId)
    {
        if (taskId is null || taskId == Guid.Empty) return null;
        var matches = tasks.Where(x => x.Id == taskId.Value).Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }
}
