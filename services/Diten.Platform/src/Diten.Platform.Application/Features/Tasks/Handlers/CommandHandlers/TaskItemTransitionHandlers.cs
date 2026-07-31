using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;

// MOD-0024 — ownership/admission and lifecycle transitions. Every one is an expected-version conditional write,
// so a concurrent change yields a controlled 409 instead of clobbering (pack §13).

/// <summary>Accept a task assigned to me — the Inbox acceptance gate closes and I become the holder.</summary>
public sealed class AcceptTaskItemHandler : IRequestHandler<AcceptTaskItemCommand, Response<NoContent>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly ITaskAssignmentRepository _assignments;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;

    public AcceptTaskItemHandler(
        ITaskItemRepository tasks,
        ITaskAssignmentRepository assignments,
        ICurrentUserContext currentUser,
        ITenantContext tenantContext)
    {
        _tasks = tasks;
        _assignments = assignments;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
    }

    public async Task<Response<NoContent>> Handle(AcceptTaskItemCommand command, CancellationToken ct)
    {
        var task = await _tasks.GetByIdAsync(command.Id, ct);
        if (task is null)
        {
            return Response<NoContent>.Fail("Task not found.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

        if (task.AssignmentTarget != TaskAssignmentTarget.Person || task.AssigneeUserId != _currentUser.UserId)
        {
            return Response<NoContent>.Fail(
                "Only the assignee can accept this task.", 403, TaskReasonCodes.InvalidState, command.CorrelationId);
        }

        // Acceptance is signalled by leaving the pre-acceptance lifecycle; Planned is preserved if already planned.
        if (task.Lifecycle == TaskLifecycle.Open)
        {
            task.Lifecycle = TaskLifecycle.InProgress;
        }

        task.UpdatedBy = _currentUser.ActorName;

        if (!await _tasks.UpdateAsync(task, command.Request.ExpectedVersion, ct))
        {
            return Response<NoContent>.Fail(
                "The task changed meanwhile; reload and retry.",
                409, TaskReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        await _assignments.CreateAsync(new TaskAssignment
        {
            TenantId = _tenantContext.TenantId,
            TaskItemId = task.Id,
            EventType = TaskAssignmentEventType.Accepted,
            UserId = _currentUser.UserId,
            ActorUserId = _currentUser.UserId,
            ReasonCode = command.Request.ReasonCode,
            Note = command.Request.Note,
            CreatedBy = _currentUser.ActorName
        }, ct);

        return Response<NoContent>.Success(204, command.CorrelationId);
    }
}

/// <summary>Return a claimed pool task to its pool: ownership → unowned, admission → pendingClaim.</summary>
public sealed class ReleaseTaskItemHandler : IRequestHandler<ReleaseTaskItemCommand, Response<NoContent>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly ITaskAssignmentRepository _assignments;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;

    public ReleaseTaskItemHandler(
        ITaskItemRepository tasks,
        ITaskAssignmentRepository assignments,
        ICurrentUserContext currentUser,
        ITenantContext tenantContext)
    {
        _tasks = tasks;
        _assignments = assignments;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
    }

    public async Task<Response<NoContent>> Handle(ReleaseTaskItemCommand command, CancellationToken ct)
    {
        var task = await _tasks.GetByIdAsync(command.Id, ct);
        if (task is null)
        {
            return Response<NoContent>.Fail("Task not found.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

        if (task.AssignmentTarget != TaskAssignmentTarget.PositionPool || task.AssigneeUserId is null)
        {
            return Response<NoContent>.Fail(
                "Only a claimed pooled task can be released.",
                409, TaskReasonCodes.InvalidState, command.CorrelationId);
        }

        if (task.AssigneeUserId != _currentUser.UserId)
        {
            return Response<NoContent>.Fail(
                "Only the current holder can release this task.",
                403, TaskReasonCodes.InvalidState, command.CorrelationId);
        }

        var previousHolder = task.AssigneeUserId;

        // Back to the pool: no holder, and the lifecycle rewinds to Open so it reads as fresh pool work.
        task.AssigneeUserId = null;
        task.Lifecycle = TaskLifecycle.Open;
        task.UpdatedBy = _currentUser.ActorName;

        if (!await _tasks.UpdateAsync(task, command.Request.ExpectedVersion, ct))
        {
            return Response<NoContent>.Fail(
                "The task changed meanwhile; reload and retry.",
                409, TaskReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        // "Released" is an activity EVENT, never an ownership state (pack boundary).
        await _assignments.CreateAsync(new TaskAssignment
        {
            TenantId = _tenantContext.TenantId,
            TaskItemId = task.Id,
            EventType = TaskAssignmentEventType.Released,
            UserId = previousHolder,
            PositionId = task.PoolPositionId,
            ActorUserId = _currentUser.UserId,
            ReasonCode = command.Request.ReasonCode,
            Note = command.Request.Note,
            CreatedBy = _currentUser.ActorName
        }, ct);

        return Response<NoContent>.Success(204, command.CorrelationId);
    }
}

/// <summary>
/// Plan / start / complete / cancel. The permitted set comes from <see cref="ITaskLifecycleService"/> — this
/// handler never encodes its own rules, so an approval-gated task cannot be started here (pack §12 K2).
/// </summary>
public sealed class TransitionTaskItemHandler : IRequestHandler<TransitionTaskItemCommand, Response<NoContent>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly ITaskLifecycleService _lifecycle;
    private readonly ICurrentUserContext _currentUser;
    private readonly IChecklistRunRepository _checklists;
    private readonly ITaskChecklistService _checklistService;
    private readonly IWorkflowTransitionGate _workflowGate;
    private readonly ITaskDependencyRepository _dependencies;

    /// <summary>WC-4 — the shared notification path; see ITaskNotificationService for the four rules it holds.</summary>
    private readonly ITaskNotificationService _notifications;
    private readonly ILogger<TransitionTaskItemHandler> _logger;

    public TransitionTaskItemHandler(
        ITaskItemRepository tasks,
        ITaskLifecycleService lifecycle,
        ICurrentUserContext currentUser,
        IChecklistRunRepository checklists,
        ITaskChecklistService checklistService,
        IWorkflowTransitionGate workflowGate,
        ITaskDependencyRepository dependencies,
        ITaskNotificationService notifications,
        ILogger<TransitionTaskItemHandler> logger)
    {
        _logger = logger;
        _notifications = notifications;
        _dependencies = dependencies;
        _tasks = tasks;
        _lifecycle = lifecycle;
        _currentUser = currentUser;
        _checklists = checklists;
        _checklistService = checklistService;
        _workflowGate = workflowGate;
    }

    public async Task<Response<NoContent>> Handle(TransitionTaskItemCommand command, CancellationToken ct)
    {
        var task = await _tasks.GetByIdAsync(command.Id, ct);
        if (task is null)
        {
            return Response<NoContent>.Fail("Task not found.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

        if (!_lifecycle.CanTransition(task, command.Target, out var reasonCode))
        {
            return Response<NoContent>.Fail(
                "This transition is not allowed in the task's current state.",
                409, reasonCode ?? TaskReasonCodes.InvalidState, command.CorrelationId);
        }

        // Checklist gate, enforced HERE and not only in the projection: the projection disables the button, but a
        // caller can post straight to this endpoint. Hiding a control is presentation; refusing the write is the
        // rule (pack §12 E1).
        //
        // Only Blocking items gate completion — an unfinished `Required` item is an expectation, not a barrier.
        // Open SUBTASKS gate it too, further down (BL-035). That reverses what this comment used to say: the old
        // objection was that two blocking mechanisms would make "why can't I finish this?" unanswerable, and the
        // answer now exists — blockedState.blockers[] names every blocker individually, which it did not when the
        // objection was written.
        if (command.Target == TaskLifecycle.Done)
        {
            var checklist = await _checklists.GetByTaskIdAsync(task.Id, ct);
            if (_checklistService.BlocksCompletion(checklist))
            {
                return Response<NoContent>.Fail(
                    "A blocking checklist item is still open.",
                    409, TaskReasonCodes.ChecklistIncomplete, command.CorrelationId);
            }
        }

        /*
         * Cancelling is the REQUESTER's right, and this is where it is ENFORCED — the projection stops offering
         * the button, but a caller can post straight here, and a hidden control is presentation while the refusal
         * is the rule. Same shape as the workflow gate below: the projection explains, the handler refuses.
         *
         * 403, not 409: this is a refusal of AUTHORITY (you may not cancel someone else's work), not a state
         * conflict (the task is in the wrong lifecycle). Conflating them would tell the caller to reload and
         * retry, which would never help.
         */
        if (command.Target == TaskLifecycle.Cancelled
            && task.CreatedByUserId != _currentUser.UserId
            && !command.ActorMayCancelAnyTask)
        {
            return Response<NoContent>.Fail(
                "Only the requester can cancel this task.",
                403, TaskReasonCodes.CancelNotRequester, command.CorrelationId);
        }

        // ── Workflow gate (pack §12 K2, charter Binding A) ────────────────────
        // Asked ONLY for approval-gated tasks, and only for the two transitions that mean "this work proceeds".
        // A task with no approval requirement never pays for the gate and never depends on it.
        //
        // FAIL-CLOSED by contract: a failed evaluation counts as blocked, so a workflow outage cannot let
        // unapproved work start. The commit below is skipped entirely when the gate says no.
        if (task.ApprovalRequired &&
            command.Target is TaskLifecycle.InProgress or TaskLifecycle.Done)
        {
            var gate = await _workflowGate.EvaluateAsync(new WorkflowGateRequest(
                ObjectType: TaskApprovalService.ApprovalObjectType,
                ObjectId: task.Id.ToString(),
                ObjectRef: TaskApprovalService.BuildObjectRef(task.Id),
                RequestedTransition: command.Target == TaskLifecycle.InProgress ? "start" : "complete",
                RequestedTargetState: command.Target.ToString(),
                ActorId: _currentUser.UserId.ToString(),
                ReasonCode: command.Request.ReasonCode,
                CorrelationId: command.CorrelationId), ct);

            if (gate.IsBlocked)
            {
                return Response<NoContent>.Fail(
                    gate.BlockingMessage ?? "This transition is blocked pending approval.",
                    409,
                    gate.BlockingReasonCode ?? TaskReasonCodes.ApprovalPending,
                    command.CorrelationId);
            }
        }

        /*
         * Review gate (Faz 3b) — the SAME engine, a SECOND decision.
         *
         * Asked only on `complete`, because that is the act a review gates: approval releases the work to START,
         * review releases it to CLOSE. Both go to MOD-0023 through the identical IWorkflowTransitionGate call and
         * differ only in WHICH object reference they name — MOD-0024 owns no review engine and reads no review
         * status of its own.
         *
         * The separate ObjectType is load-bearing rather than tidy: the gate resolves an instance with
         * GetLatestByObjectRefAsync, which returns the latest instance for one reference, so a shared reference
         * would make each gate read whichever decision started last. See TaskReviewService.
         *
         * FAIL-CLOSED like approval: an unevaluable gate counts as blocked, so a workflow outage cannot let
         * unreviewed work close.
         */
        if (task.ReviewRequired && command.Target == TaskLifecycle.Done)
        {
            /*
             * No instance means the work was never SUBMITTED for review, and the gate cannot speak for a decision
             * nobody was asked for: with no instance it answers NotApplicable, which the gate contract treats as
             * allowed. So the requirement itself is enforced here first.
             *
             * That is not MOD-0024 deciding a review — `ReviewRequired` is MOD-0024's own flag, and all this says
             * is "this work has to go through review", never whether the review passed. The verdict is still read
             * from the instance, immediately below.
             */
            if (task.ReviewWorkflowInstanceId is null)
            {
                return Response<NoContent>.Fail(
                    "This task must be submitted for review before it can be completed.",
                    409, TaskReasonCodes.ReviewPending, command.CorrelationId);
            }

            var reviewGate = await _workflowGate.EvaluateAsync(new WorkflowGateRequest(
                ObjectType: TaskReviewService.ReviewObjectType,
                ObjectId: task.Id.ToString(),
                ObjectRef: TaskReviewService.BuildObjectRef(task.Id),
                RequestedTransition: "complete",
                RequestedTargetState: command.Target.ToString(),
                ActorId: _currentUser.UserId.ToString(),
                ReasonCode: command.Request.ReasonCode,
                CorrelationId: command.CorrelationId), ct);

            if (reviewGate.IsBlocked)
            {
                /*
                 * The engine's own reason code is NOT passed through here, unlike the approval branch above.
                 * MOD-0023 answers in its own vocabulary — a blocked instance reports WORKFLOW_PENDING_APPROVAL
                 * whatever question it was asked — so forwarding it would tell a holder waiting on a REVIEWER that
                 * they are waiting for approval, and send them to the wrong person.
                 *
                 * The engine's MESSAGE is still forwarded: that is its description of its own state, and it is the
                 * useful half. Only the code, which the client routes on, is restated in MOD-0024's terms.
                 */
                return Response<NoContent>.Fail(
                    reviewGate.BlockingMessage ?? "This task cannot be completed while its review is open.",
                    409,
                    TaskReasonCodes.ReviewPending,
                    command.CorrelationId);
            }
        }

        /*
         * Dependency gate — the RULE, not the hint.
         *
         * The projection already computes this and ships `start` disabled with DEPENDENCY_BLOCKED beside it. That
         * is presentation: a caller can post straight to this endpoint, and until this check existed one did, and
         * a task with an open predecessor started anyway. Exactly the gap the cancel guard had, and the same cure.
         *
         * Placed AFTER the approval gate so the reason the caller is told matches the reason the projection shows:
         * an approval-pending task reports APPROVAL_PENDING from both sides, and the dependency reason is what
         * remains once the gates above are clear.
         *
         * Which edges apply is decided by TaskBlockingRules, the same source the projection uses — one rule, one
         * place, so the button and the refusal cannot disagree.
         */
        if (command.Target is TaskLifecycle.InProgress or TaskLifecycle.Done)
        {
            var blocker = await FindUnsatisfiedDependencyAsync(task, command.Target, ct);
            if (blocker is not null)
            {
                return Response<NoContent>.Fail(
                    "A dependency has not been met yet.",
                    409, TaskReasonCodes.DependencyBlocked, command.CorrelationId);
            }
        }

        /*
         * Subtask gate (BL-035). AFTER the dependency gate, in the same order the projection resolves its
         * blockers, so the reason beside the greyed button is the reason this endpoint answers with.
         *
         * COMPLETION only. `start` is untouched — work can begin with its parts still open — and `cancel` must
         * stay possible, because cancelling a parent is what CancelOpenSubtasksAsync uses to close its children:
         * blocking it would make an unwanted task with open children impossible to call off.
         *
         * A subtask cannot itself have subtasks (one level only), so this query is always empty for a child and
         * the rule never applies to one. That needs no special case — the emptiness IS the special case.
         */
        if (command.Target == TaskLifecycle.Done)
        {
            var children = await _tasks.ListByParentAsync(task.Id, ct);
            if (TaskBlockingRules.OpenSubtasksBlockingCompletion(children).Count > 0)
            {
                return Response<NoContent>.Fail(
                    "A subtask is still open.",
                    409, TaskReasonCodes.SubtaskBlocked, command.CorrelationId);
            }
        }

        task.Lifecycle = command.Target;
        task.UpdatedBy = _currentUser.ActorName;

        switch (command.Target)
        {
            case TaskLifecycle.Done:
                task.CompletedAt = DateTimeOffset.UtcNow;
                task.ClosureReasonCode = command.Request.ReasonCode;
                break;
            case TaskLifecycle.Cancelled:
                task.CancelledAt = DateTimeOffset.UtcNow;
                task.ClosureReasonCode = command.Request.ReasonCode;
                break;
            case TaskLifecycle.InProgress when task.StartAt is null:
                task.StartAt = DateTimeOffset.UtcNow;
                break;
        }

        if (!await _tasks.UpdateAsync(task, command.Request.ExpectedVersion, ct))
        {
            return Response<NoContent>.Fail(
                "The task changed meanwhile; reload and retry.",
                409, TaskReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        if (command.Target == TaskLifecycle.Cancelled)
        {
            await CancelOpenSubtasksAsync(task, command, ct);
        }

        /*
         * The REQUESTER is told their work is finished (WC-4). They asked for it; the holder already knows.
         *
         * The service's actor rule is what makes "I completed my own task" send nothing — a self-assigned task
         * has the same person on both ends, and telling them about their own act is the noise that teaches
         * people to ignore notifications.
         */
        if (command.Target == TaskLifecycle.Done)
        {
            var requester = task.CreatedByUserId is { } creator ? new[] { creator } : [];
            await TaskNotificationSafely.NotifyAsync(
                _notifications, _logger, task, TaskNotificationEvents.Completed,
                requester, _currentUser.UserId, ct);
        }

        return Response<NoContent>.Success(204, command.CorrelationId);
    }

    /// <summary>
    /// Cancelling a parent cancels its still-open subtasks.
    ///
    /// <para>A subtask exists to serve its parent, so leaving it open would strand work in someone's İşlerim with
    /// no remaining reason to do it — and the person holding it has no way to discover the parent was called off.
    /// Subtasks already FINISHED (Done) or already Cancelled are left untouched: history is not rewritten.</para>
    ///
    /// <para>Best effort per child: one child losing an expected-version race must not fail the parent's
    /// cancellation, which already committed above.</para>
    /// </summary>
    private async Task CancelOpenSubtasksAsync(TaskItem parent, TransitionTaskItemCommand command, CancellationToken ct)
    {
        var children = await _tasks.ListByParentAsync(parent.Id, ct);
        foreach (var child in children)
        {
            if (child.Lifecycle is TaskLifecycle.Done or TaskLifecycle.Cancelled)
            {
                continue;
            }

            child.Lifecycle = TaskLifecycle.Cancelled;
            child.CancelledAt = DateTimeOffset.UtcNow;
            child.ClosureReasonCode = command.Request.ReasonCode;
            child.UpdatedBy = _currentUser.ActorName;

            await _tasks.UpdateAsync(child, child.Version, ct);
        }
    }

    /// <summary>
    /// The first predecessor that has not reached the state its edge waits for, or null when nothing is in the
    /// way. Reads only the edges this task WAITS ON (<c>ListByTaskIdAsync</c> returns exactly those), and only the
    /// ones whose type gates the act being attempted.
    ///
    /// <para>An edge whose far end cannot be read blocks NOTHING. That mirrors the projection, which drops such an
    /// edge rather than showing an unnamed blocker: refusing on a predecessor the caller cannot see or reach would
    /// park the task with no way to clear it.</para>
    /// </summary>
    private async Task<TaskItem?> FindUnsatisfiedDependencyAsync(
        TaskItem task,
        TaskLifecycle target,
        CancellationToken ct)
    {
        var edges = await _dependencies.ListByTaskIdAsync(task.Id, ct);
        if (edges.Count == 0)
        {
            return null;
        }

        var gatedAct = target == TaskLifecycle.InProgress
            ? TaskBlockingRules.StartActionCode
            : TaskBlockingRules.CompleteActionCode;

        var relevant = edges
            .Where(edge => TaskBlockingRules.AffectedActionCode(edge.DependencyType) == gatedAct)
            .ToList();
        if (relevant.Count == 0)
        {
            return null;
        }

        // One batched read for every predecessor involved, never one per edge.
        var predecessors = (await _tasks.ListByIdsAsync(
                relevant.Select(edge => edge.DependsOnTaskItemId).Distinct().ToList(), ct))
            .ToDictionary(item => item.Id);

        foreach (var edge in relevant)
        {
            if (predecessors.TryGetValue(edge.DependsOnTaskItemId, out var predecessor)
                && !TaskBlockingRules.IsSatisfied(edge.DependencyType, predecessor))
            {
                return predecessor;
            }
        }

        return null;
    }
}

/// <summary>
/// Hand finished work to a reviewer — InProgress → PendingReview, and the MOD-0023 instance that carries the
/// decision.
///
/// <para><b>The whole point of the slice.</b> The transition is MOD-0024's (its own lifecycle moves), the DECISION
/// is MOD-0023's (an instance is opened and MOD-0023 answers it). Nothing here approves, rejects, or records a
/// verdict; there is no review status to write, because the status is the instance's.</para>
/// </summary>
public sealed class SubmitTaskForReviewHandler : IRequestHandler<SubmitTaskForReviewCommand, Response<NoContent>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly ITaskLifecycleService _lifecycle;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITaskReviewService _reviews;
    // Read-only access to what MOD-0023 says about an instance. The approval service owns this read because it is
    // keyed by INSTANCE id and never asks what the instance decides, so it serves both decisions unchanged.
    private readonly ITaskApprovalService _reviewStates;
    private readonly ILogger<SubmitTaskForReviewHandler> _logger;

    public SubmitTaskForReviewHandler(
        ITaskItemRepository tasks,
        ITaskLifecycleService lifecycle,
        ICurrentUserContext currentUser,
        ITaskReviewService reviews,
        ITaskApprovalService reviewStates,
        ILogger<SubmitTaskForReviewHandler> logger)
    {
        _reviewStates = reviewStates;
        _tasks = tasks;
        _lifecycle = lifecycle;
        _currentUser = currentUser;
        _reviews = reviews;
        _logger = logger;
    }

    public async Task<Response<NoContent>> Handle(SubmitTaskForReviewCommand command, CancellationToken ct)
    {
        var task = await _tasks.GetByIdAsync(command.Id, ct);
        if (task is null)
        {
            return Response<NoContent>.Fail("Task not found.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

        /*
         * A task that never asked for a review cannot be submitted for one. The projection simply does not offer
         * the action, but a caller can post straight to this endpoint — and a hidden control is presentation while
         * the refusal is the rule. Without this, any task could be parked in PendingReview with a workflow nobody
         * asked for, and `complete` would then be gated by it.
         */
        if (!task.ReviewRequired)
        {
            return Response<NoContent>.Fail(
                "This task does not require a review.",
                409, TaskReasonCodes.ReviewNotRequired, command.CorrelationId);
        }

        /*
         * RESUBMISSION after a refusal. A task refused by its reviewer stays in PendingReview on the record while
         * the projection reports it as InProgress — the work is back with its holder — so the lifecycle matrix,
         * which has no PendingReview → PendingReview edge, would refuse the second round and leave the work with
         * nothing to press.
         *
         * Allowed ONLY when MOD-0023 says the current review was refused. That verdict is READ from the instance,
         * never assumed from the lifecycle: a task still waiting on its reviewer takes the ordinary path below and
         * is refused, because resubmitting work someone is holding right now would silently replace their review.
         */
        var resubmitting = false;
        if (task.Lifecycle == TaskLifecycle.PendingReview && task.ReviewWorkflowInstanceId is { } openInstance)
        {
            var states = await _reviewStates.GetStatesAsync([openInstance], ct);
            resubmitting = TaskReviewView.Resolve(task, states).Rejected;
        }

        if (!resubmitting && !_lifecycle.CanTransition(task, TaskLifecycle.PendingReview, out var reasonCode))
        {
            return Response<NoContent>.Fail(
                "This transition is not allowed in the task's current state.",
                409, reasonCode ?? TaskReasonCodes.InvalidState, command.CorrelationId);
        }

        /*
         * The instance is opened BEFORE the lifecycle moves, which is the opposite order to the approval toggle on
         * an edit — and deliberately so. There, the edit is the user's work and must survive a workflow outage, so
         * the write commits first and the handoff follows. Here the handoff IS the act: a task sitting in
         * PendingReview with no instance is a task waiting on a reviewer who was never asked, and `complete` would
         * then refuse forever with no way to clear it.
         */
        var instanceId = await _reviews.TryStartReviewAsync(task, ct);
        if (instanceId is null)
        {
            _logger.LogWarning(
                "Review could not be started for task {TaskId}; it stays in progress rather than waiting on nobody.",
                task.Id);
            /*
             * REVIEW_START_FAILED, not REVIEW_PENDING. Nothing is waiting: the handoff was refused, so there is
             * no reviewer to wait on and the caller can simply retry. Reporting it as "pending" pointed the user
             * at a reviewer who was never asked — the same distinction approval already draws between its
             * start-failed and approver-pending texts.
             */
            return Response<NoContent>.Fail(
                "The review could not be started. The task is unchanged; try again.",
                409, TaskReasonCodes.ReviewStartFailed, command.CorrelationId);
        }

        task.ReviewWorkflowInstanceId = instanceId;
        task.Lifecycle = TaskLifecycle.PendingReview;
        task.UpdatedBy = _currentUser.ActorName;

        if (!await _tasks.UpdateAsync(task, command.Request.ExpectedVersion, ct))
        {
            // The review IS open; only the task's move lost the race. Cancelling it back out would be a second
            // failure path on an already-failed write, so it is left running and named in the log — the retry
            // finds it by the same idempotency key rather than opening a second one.
            _logger.LogWarning(
                "Review instance {InstanceId} started for task {TaskId} but the task lost a concurrency race.",
                instanceId, task.Id);
            return Response<NoContent>.Fail(
                "The task changed meanwhile; reload and retry.",
                409, TaskReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        return Response<NoContent>.Success(204, command.CorrelationId);
    }
}

/// <summary>
/// Set (or move) a personal plan date. The FIRST plan (Open → Planned) and a RE-plan (Planned → Planned) are the
/// same write — both just record a date — so one handler covers both rather than the create/update split every
/// other transition has.
///
/// <para>Deliberately NOT routed through <see cref="TransitionTaskItemCommand"/>: the date is required here and
/// meaningless for the other nine transitions, and Planned→Planned is a self-loop no other caller of that
/// command needs (see <see cref="TaskLifecycleService.CanTransition"/>).</para>
/// </summary>
public sealed class PlanTaskItemHandler : IRequestHandler<PlanTaskItemCommand, Response<NoContent>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly ITaskLifecycleService _lifecycle;
    private readonly ICurrentUserContext _currentUser;

    public PlanTaskItemHandler(
        ITaskItemRepository tasks,
        ITaskLifecycleService lifecycle,
        ICurrentUserContext currentUser)
    {
        _tasks = tasks;
        _lifecycle = lifecycle;
        _currentUser = currentUser;
    }

    public async Task<Response<NoContent>> Handle(PlanTaskItemCommand command, CancellationToken ct)
    {
        // A JSON body that omits `plannedDate` deserializes this to DateTimeOffset's zero value rather than
        // throwing — nobody types year 1, so treating the zero value as "not supplied" is safe and gives the
        // caller our own reason code instead of a framework-shaped validation error.
        if (command.Request.PlannedDate == default)
        {
            return Response<NoContent>.Fail(
                "A planned date is required.", 400, TaskReasonCodes.PlanDateRequired, command.CorrelationId);
        }

        var task = await _tasks.GetByIdAsync(command.Id, ct);
        if (task is null)
        {
            return Response<NoContent>.Fail("Task not found.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

        if (!_lifecycle.CanTransition(task, TaskLifecycle.Planned, out var reasonCode))
        {
            return Response<NoContent>.Fail(
                "This transition is not allowed in the task's current state.",
                409, reasonCode ?? TaskReasonCodes.InvalidState, command.CorrelationId);
        }

        /*
         * Validation here is deliberately LOOSE: nothing compares PlannedDate against DueAt or against today.
         *
         * A plan after the source due date is a real situation — "I won't make it, planning for the 5th instead"
         * — and refusing it would force the user to pick an earlier date just to get the write accepted, which is
         * a lie the system would be asking for. The screen already surfaces the mismatch as a visible warning
         * (renderPlanDates / wcn-date-conflict); that is the right place for the judgement, not a blocked write.
         *
         * A date in the past is accepted for the same reason: PlannedDate is a personal note, not a commitment
         * the system enforces. The ONLY thing refused is no date at all (checked above).
         *
         * If a future reader is tempted to add a range check here, that is this comment telling them not to.
         */
        task.Lifecycle = TaskLifecycle.Planned;
        task.PlannedDate = command.Request.PlannedDate;
        task.UpdatedBy = _currentUser.ActorName;

        if (!await _tasks.UpdateAsync(task, command.Request.ExpectedVersion, ct))
        {
            return Response<NoContent>.Fail(
                "The task changed meanwhile; reload and retry.",
                409, TaskReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        return Response<NoContent>.Success(204, command.CorrelationId);
    }
}

/// <summary>
/// Park a task in <see cref="TaskLifecycle.Waiting"/> because the holder is blocked on someone else.
///
/// <para>This is the ENTRY to Waiting. The lifecycle and its transition matrix have always allowed the state, and
/// the projection gained a way OUT of it (resume), but no endpoint ever targeted it — so Waiting was reachable
/// only on paper and the Task Center's "Bekleyen" segment could fill from approval alone. A user with no way to
/// say "I am blocked" either leaves the task looking active or cancels it, and neither is true.</para>
///
/// <para>Deliberately NOT routed through <see cref="TransitionTaskItemCommand"/>: the reason is mandatory here,
/// and waiting is not "progress", so it never consults the workflow approval gate. Leaving Waiting DOES —
/// resuming targets InProgress, which the gate covers (TaskApprovalHttpContractTests).</para>
/// </summary>
public sealed class InquireTaskItemHandler : IRequestHandler<InquireTaskItemCommand, Response<NoContent>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly ITaskLifecycleService _lifecycle;
    private readonly ICurrentUserContext _currentUser;

    public InquireTaskItemHandler(
        ITaskItemRepository tasks,
        ITaskLifecycleService lifecycle,
        ICurrentUserContext currentUser)
    {
        _tasks = tasks;
        _lifecycle = lifecycle;
        _currentUser = currentUser;
    }

    public async Task<Response<NoContent>> Handle(InquireTaskItemCommand command, CancellationToken ct)
    {
        var reason = command.Request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            // "Waiting" with no stated cause is not information anyone can act on — least of all the person who
            // reads the task next week.
            return Response<NoContent>.Fail(
                "Say what the task is waiting for.",
                400, TaskReasonCodes.WaitingReasonRequired, command.CorrelationId);
        }

        var task = await _tasks.GetByIdAsync(command.Id, ct);
        if (task is null)
        {
            return Response<NoContent>.Fail("Task not found.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

        // Only the holder can declare a wait: it is a statement about their own work.
        if (task.AssigneeUserId != _currentUser.UserId)
        {
            return Response<NoContent>.Fail(
                "Only the current holder can park this task in waiting.",
                403, TaskReasonCodes.InvalidState, command.CorrelationId);
        }

        if (!_lifecycle.CanTransition(task, TaskLifecycle.Waiting, out var reasonCode))
        {
            return Response<NoContent>.Fail(
                "This transition is not allowed in the task's current state.",
                409, reasonCode ?? TaskReasonCodes.InvalidState, command.CorrelationId);
        }

        task.Lifecycle = TaskLifecycle.Waiting;
        task.WaitingReason = reason;
        task.UpdatedBy = _currentUser.ActorName;

        if (!await _tasks.UpdateAsync(task, command.Request.ExpectedVersion, ct))
        {
            return Response<NoContent>.Fail(
                "The task changed meanwhile; reload and retry.",
                409, TaskReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        return Response<NoContent>.Success(204, command.CorrelationId);
    }
}

/// <summary>
/// Give assigned work BACK to whoever asked for it — the refusal that was missing.
///
/// <para>Until now the only way out of unwanted work was <c>cancel</c>, which means something opposite: the work
/// is called off entirely. So an assignee either did somebody else's job or destroyed their request.</para>
///
/// <para>No new state is invented. The task returns to the creator exactly as a fresh assignment does — they
/// become the assignee, the acceptance gate reopens, and it appears in their Inbox as pendingAcceptance. The
/// lifecycle is untouched: returning says nothing about how far the work got.</para>
///
/// <para><b>The verb is shared with MOD-0023, the path is not.</b> An approver returning an approval/review to its
/// submitter is MOD-0023's decision and lives entirely in that module (charter Binding A). Both are called
/// `return` because "send it back" is one idea; they operate on different work-intent types and must not be
/// merged into one route because they answer to different owners.</para>
/// </summary>
public sealed class ReturnTaskItemHandler : IRequestHandler<ReturnTaskItemCommand, Response<NoContent>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly ITaskAssignmentRepository _assignments;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;

    public ReturnTaskItemHandler(
        ITaskItemRepository tasks,
        ITaskAssignmentRepository assignments,
        ICurrentUserContext currentUser,
        ITenantContext tenantContext)
    {
        _tasks = tasks;
        _assignments = assignments;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
    }

    public async Task<Response<NoContent>> Handle(ReturnTaskItemCommand command, CancellationToken ct)
    {
        var reason = command.Request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Response<NoContent>.Fail(
                "Say why the task is being returned.",
                400, TaskReasonCodes.HandoverReasonRequired, command.CorrelationId);
        }

        var task = await _tasks.GetByIdAsync(command.Id, ct);
        if (task is null)
        {
            return Response<NoContent>.Fail("Task not found.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

        if (task.AssigneeUserId != _currentUser.UserId)
        {
            return Response<NoContent>.Fail(
                "Only the current assignee can return this task.",
                403, TaskReasonCodes.ReturnNotAssignee, command.CorrelationId);
        }

        // Nowhere to send it back TO. Returning a task to yourself is a no-op dressed as an action, and the
        // projection does not offer it — this refuses the direct call for the same reason.
        if (task.CreatedByUserId is null || task.CreatedByUserId == task.AssigneeUserId)
        {
            return Response<NoContent>.Fail(
                "This task has no separate requester to return it to.",
                409, TaskReasonCodes.InvalidState, command.CorrelationId);
        }

        var returnedBy = task.AssigneeUserId;

        // Back to the requester as an ordinary assignment: they hold it, and the acceptance gate reopens so it
        // lands in their Inbox rather than silently in their active work.
        task.AssigneeUserId = task.CreatedByUserId;
        task.AssignmentTarget = TaskAssignmentTarget.Person;
        task.Lifecycle = TaskLifecycle.Open;
        task.UpdatedBy = _currentUser.ActorName;

        if (!await _tasks.UpdateAsync(task, command.Request.ExpectedVersion, ct))
        {
            return Response<NoContent>.Fail(
                "The task changed meanwhile; reload and retry.",
                409, TaskReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        // There is no `Returned` event type, and adding one changes the shape of already-persisted history for a
        // distinction the note already records. A return IS a reassignment — back to the requester.
        await _assignments.CreateAsync(new TaskAssignment
        {
            TenantId = _tenantContext.TenantId,
            TaskItemId = task.Id,
            EventType = TaskAssignmentEventType.Reassigned,
            UserId = task.AssigneeUserId,
            ActorUserId = returnedBy,
            // ReasonCode is for machine-readable classification and there is no code for "returned"; the note is
            // the reason, and it is the user's own words.
            ReasonCode = null,
            Note = reason,
            CreatedBy = _currentUser.ActorName
        }, ct);

        return Response<NoContent>.Success(204, command.CorrelationId);
    }
}

/// <summary>
/// Hand work to somebody else.
///
/// <para>Two people may do it, for two different reasons: the current HOLDER delegating work they cannot do, and
/// the REQUESTER correcting an assignment they got wrong. Nobody else — being able to see a task is not authority
/// to move it onto a colleague.</para>
///
/// <para>The new holder receives it UNACCEPTED: the acceptance gate reopens so the task appears in their Inbox to
/// be taken on, rather than silently joining their active work. Pool tasks are excluded entirely; a pool already
/// has claim/release for exactly this, and reassigning one would name a holder the pool does not have.</para>
/// </summary>
public sealed class ReassignTaskItemHandler : IRequestHandler<ReassignTaskItemCommand, Response<NoContent>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly ITaskAssignmentRepository _assignments;
    private readonly IPositionAssignmentRepository _positionAssignments;
    private readonly IPositionRepository _positions;
    private readonly IOrganizationUnitRepository _organizationUnits;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;

    public ReassignTaskItemHandler(
        ITaskItemRepository tasks,
        ITaskAssignmentRepository assignments,
        IPositionAssignmentRepository positionAssignments,
        IPositionRepository positions,
        IOrganizationUnitRepository organizationUnits,
        ICurrentUserContext currentUser,
        ITenantContext tenantContext)
    {
        _tasks = tasks;
        _assignments = assignments;
        _positionAssignments = positionAssignments;
        _positions = positions;
        _organizationUnits = organizationUnits;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
    }

    public async Task<Response<NoContent>> Handle(ReassignTaskItemCommand command, CancellationToken ct)
    {
        var reason = command.Request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Response<NoContent>.Fail(
                "Say why the task is being reassigned.",
                400, TaskReasonCodes.HandoverReasonRequired, command.CorrelationId);
        }

        var task = await _tasks.GetByIdAsync(command.Id, ct);
        if (task is null)
        {
            return Response<NoContent>.Fail("Task not found.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

        if (task.AssignmentTarget == TaskAssignmentTarget.PositionPool)
        {
            return Response<NoContent>.Fail(
                "Pooled work is claimed and released, not reassigned.",
                409, TaskReasonCodes.InvalidState, command.CorrelationId);
        }

        var isHolder = task.AssigneeUserId == _currentUser.UserId;
        var isRequester = task.CreatedByUserId is not null && task.CreatedByUserId == _currentUser.UserId;
        if (!isHolder && !isRequester)
        {
            return Response<NoContent>.Fail(
                "Only the current assignee or the requester can reassign this task.",
                403, TaskReasonCodes.ReassignNotPermitted, command.CorrelationId);
        }

        if (command.Request.AssigneeUserId == Guid.Empty)
        {
            return Response<NoContent>.Fail(
                "A new assignee is required.",
                400, TaskReasonCodes.AssigneeInvalid, command.CorrelationId);
        }

        if (command.Request.AssigneeUserId == task.AssigneeUserId)
        {
            return Response<NoContent>.Fail(
                "The task is already assigned to that person.",
                409, TaskReasonCodes.InvalidState, command.CorrelationId);
        }

        // The same rule the people picker uses — see TaskAssigneeEligibility for why it is shared rather than
        // written twice. Refusing here is what stops work landing on somebody the product will not offer.
        var assignable = TaskAssigneeEligibility.ResolveAssignableUserIds(
            await _positionAssignments.GetAllAsync(ct),
            await _positions.GetAllAsync(ct),
            await _organizationUnits.GetAllAsync(ct),
            DateTimeOffset.UtcNow);

        if (!assignable.Contains(command.Request.AssigneeUserId))
        {
            return Response<NoContent>.Fail(
                "That person cannot be assigned work.",
                400, TaskReasonCodes.AssigneeNotAssignable, command.CorrelationId);
        }

        // Unaccepted on arrival: the acceptance gate reopens so it lands in the new holder's Inbox.
        task.AssigneeUserId = command.Request.AssigneeUserId;
        task.AssignmentTarget = TaskAssignmentTarget.Person;
        task.Lifecycle = TaskLifecycle.Open;
        task.UpdatedBy = _currentUser.ActorName;

        if (!await _tasks.UpdateAsync(task, command.Request.ExpectedVersion, ct))
        {
            return Response<NoContent>.Fail(
                "The task changed meanwhile; reload and retry.",
                409, TaskReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        await _assignments.CreateAsync(new TaskAssignment
        {
            TenantId = _tenantContext.TenantId,
            TaskItemId = task.Id,
            EventType = TaskAssignmentEventType.Reassigned,
            UserId = command.Request.AssigneeUserId,
            ActorUserId = _currentUser.UserId,
            ReasonCode = null,
            Note = reason,
            CreatedBy = _currentUser.ActorName
        }, ct);

        return Response<NoContent>.Success(204, command.CorrelationId);
    }
}
