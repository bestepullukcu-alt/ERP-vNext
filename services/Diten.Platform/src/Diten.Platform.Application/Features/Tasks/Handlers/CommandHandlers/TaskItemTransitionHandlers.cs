using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using MediatR;

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

    public TransitionTaskItemHandler(
        ITaskItemRepository tasks,
        ITaskLifecycleService lifecycle,
        ICurrentUserContext currentUser,
        IChecklistRunRepository checklists,
        ITaskChecklistService checklistService,
        IWorkflowTransitionGate workflowGate,
        ITaskDependencyRepository dependencies)
    {
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
        // Only Blocking items gate completion. An unfinished `Required` item is an expectation, not a barrier —
        // and open SUBTASKS never gate it at all, because two competing blocking mechanisms make "why can't I
        // finish this?" unanswerable.
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
         * Which edges apply is decided by TaskDependencyRules, the same source the projection uses — one rule, one
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
            ? TaskDependencyRules.StartActionCode
            : TaskDependencyRules.CompleteActionCode;

        var relevant = edges
            .Where(edge => TaskDependencyRules.AffectedActionCode(edge.DependencyType) == gatedAct)
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
                && !TaskDependencyRules.IsSatisfied(edge.DependencyType, predecessor))
            {
                return predecessor;
            }
        }

        return null;
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
