using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;

/// <summary>
/// MOD-0024 — edit a task's descriptive/planning fields. Assignment target, lifecycle and effort actuals are
/// deliberately NOT editable here: they move through their own commands so each keeps its own guard rails.
/// </summary>
public sealed class UpdateTaskItemHandler : IRequestHandler<UpdateTaskItemCommand, Response<TaskItemEditResultDto>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly IOrganizationUnitRepository _organizationUnits;
    private readonly ITaskFieldDefinitionService _fieldDefinitions;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITaskApprovalService _approvals;
    private readonly ITaskReviewService _reviews;
    private readonly TaskDocumentReferenceFreezer _documentFreezer;
    private readonly ILogger<UpdateTaskItemHandler> _logger;

    public UpdateTaskItemHandler(
        ITaskItemRepository tasks,
        IOrganizationUnitRepository organizationUnits,
        ITaskFieldDefinitionService fieldDefinitions,
        ICurrentUserContext currentUser,
        ITaskApprovalService approvals,
        ITaskReviewService reviews,
        ILogger<UpdateTaskItemHandler> logger,
        // DCP-005 §6.1 — REQUIRED, and that word is the whole fix. This was optional (`= null`), which meant
        // a missing DI registration compiled, passed every unit test, and dropped every citation at runtime
        // with no error anywhere: measured live 2026-08-26, the form showed two documents and the saved task
        // carried none. A seam that defaults to "do nothing" cannot be caught by review, so it was pinned by
        // a test that grepped DependencyInjection.cs for a string — a discipline problem patched with a
        // discipline check. Required turns it into an architectural one: forget the registration and the
        // container refuses to build the handler, which ValidateOnBuild surfaces at STARTUP.
        TaskDocumentReferenceFreezer documentFreezer)
    {
        _tasks = tasks;
        _organizationUnits = organizationUnits;
        _fieldDefinitions = fieldDefinitions;
        _currentUser = currentUser;
        _approvals = approvals;
        _reviews = reviews;
        _documentFreezer = documentFreezer;
        _logger = logger;
    }

    public async Task<Response<TaskItemEditResultDto>> Handle(UpdateTaskItemCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var task = await _tasks.GetByIdAsync(command.Id, ct);
        if (task is null)
        {
            return Response<TaskItemEditResultDto>.Fail("Task not found.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

        // A closed task is read-only — a terminal item exposes no state-changing action.
        if (task.CompletedAt is not null || task.CancelledAt is not null)
        {
            return Response<TaskItemEditResultDto>.Fail(
                "A closed task cannot be edited.", 409, TaskReasonCodes.InvalidState, command.CorrelationId);
        }

        if (request.OrganizationUnitId is { } unitId && unitId != task.OrganizationUnitId)
        {
            var unit = await _organizationUnits.GetByIdAsync(unitId, ct);
            if (unit is null || unit.IsArchived)
            {
                return Response<TaskItemEditResultDto>.Fail(
                    "The organization unit could not be resolved.",
                    400, TaskReasonCodes.OrganizationUnitUnresolved, command.CorrelationId);
            }

            task.OrganizationUnitId = unitId;
        }

        /*
         * BL-024 Phase 2 — the STORED values are handed in, and this argument is the whole fix.
         *
         * ⚠ FOUND LIVE, NOT BY A TEST. The service grew the `existing` parameter and its unit tests passed by
         * calling it directly; this caller was never updated, so the parameter defaulted to null and preservation
         * never ran. On the live system an ordinary title edit by a user who could not see a restricted field
         * DELETED that field — 204, no error, no trace. The unit test proved the service; only the round trip
         * proved the system.
         *
         * `task.FieldValues` is the pre-edit state (the entity was read above and nothing has touched the values
         * yet), which is exactly what a value the caller may not write has to be carried through from.
         */
        var fields = await _fieldDefinitions.ValidateAndMaterializeAsync(
            request.FieldValues, ct, enforceRequired: true, existing: task.FieldValues);
        if (!fields.IsValid)
        {
            return Response<TaskItemEditResultDto>.Fail(
                fields.Message ?? "Invalid task field value.",
                400, fields.ReasonCode ?? TaskReasonCodes.FieldValueInvalid, command.CorrelationId);
        }

        task.Title = request.Title.Trim();
        task.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        task.Priority = request.Priority;
        task.DueAt = request.DueAt;
        task.StartAt = request.StartAt;
        task.PlannedDate = request.PlannedDate;
        task.EstimateHours = request.EstimateHours;
        task.Tags = (request.Tags ?? []).Select(t => t.Trim()).Where(t => t.Length > 0).Distinct().ToList();
        /*
         * Review switched OFF while one is running: the instance has to be called off too, or the task keeps a
         * reviewer waiting on work nobody needs reviewed. Same decide-here / act-after-the-write shape as approval
         * below, and for the same reason — no workflow is cancelled for an edit that then loses its race.
         */
        /*
         * The SAME rule as creation, from the same source. This request is a full replace of the review
         * requirement — ReviewRequired is a plain bool, not the nullable "not editing this" ApprovalRequired uses
         * — so an edit that omits the reviewer while leaving the requirement on would otherwise strip a task's
         * reviewer and leave a review that can never start.
         */
        if (TaskReviewRules.ReviewerMissing(request.ReviewRequired, request.ReviewerCandidateUserId))
        {
            return Response<TaskItemEditResultDto>.Fail(
                TaskReviewRules.ReviewerRequiredMessage,
                400, TaskReasonCodes.ReviewerRequired, command.CorrelationId);
        }

        // D2-FIX1 A1 — a link still held while the requirement is already off is a cancel that failed before: an edit
        // that keeps the requirement off retries it.
        // D2-FIX3 item 8 — switching the requirement OFF withdraws even with no link: a submission whose link write lost
        // its race left a live review the task does not point at (the service finds it by its object reference).
        var cancelReview = !request.ReviewRequired && (task.ReviewWorkflowInstanceId is not null || task.ReviewRequired);
        var heldReviewLink = task.ReviewWorkflowInstanceId is not null;
        task.ReviewRequired = request.ReviewRequired;
        task.ReviewerCandidateUserId = request.ReviewerCandidateUserId;
        task.EmailNotificationsEnabled = request.EmailNotificationsEnabled;
        /*
         * BL-065 — the notification preferences are edited as ONE BLOCK, and NotifyOnEvents is what says the
         * caller is editing it.
         *
         * The first version applied each field only when non-null, which made "I am not editing this" and "I am
         * clearing this" the same null: choosing "no reminder" in the form saved nothing and the sweep kept
         * emailing. Inside an edit of the block the lead time is therefore applied VERBATIM — null included, so
         * the reminder can actually be switched off.
         *
         * The cost, stated rather than discovered: a caller that sends ONLY a lead time is not editing the block,
         * so it is ignored rather than half-applied. Such a caller must send the event list it wants kept —
         * which is exactly what the form does on every save. The alternative (a -1 sentinel or a second
         * "clear" field) buys per-field editing at the price of a magic number in the contract, and this block is
         * one card in one form.
         *
         * Same shape as ReviewRequired + the reviewer above, deliberately: fields that only make sense together
         * travel together.
         */
        if (request.NotifyOnEvents is not null)
        {
            task.NotifyOnEvents = request.NotifyOnEvents;
            task.ReminderLeadDays = request.ReminderLeadDays;
        }
        task.DelegationAllowed = request.DelegationAllowed;
        task.FieldValues = fields.Values.ToList();
        task.UpdatedBy = _currentUser.ActorName;
        // SpentHours is untouched: it only moves through execution/time entry, never an edit form (pack §12 Y1).

        // ── Approval toggled by an edit (pack §12 K2, charter Binding A) ──────
        // false→true starts a MOD-0023 instance, true→false cancels the running one. Both are decided here but
        // ACTED ON after the write succeeds, so a workflow is never started or cancelled for an edit that then
        // lost a concurrency race.
        var startApproval = false;
        var cancelApproval = false;
        if (request.ApprovalRequired == false && !task.ApprovalRequired && task.WorkflowInstanceId is not null)
        {
            // D2-FIX1 A1 — the requirement is already off but the link is still held: a cancel that failed before.
            // This edit retries it.
            cancelApproval = true;
        }
        else if (request.ApprovalRequired is { } wantsApproval && wantsApproval != task.ApprovalRequired)
        {
            if (wantsApproval)
            {
                // The manager may already be on the task from an earlier round; only a task with neither is invalid.
                var manager = request.ApprovalManagerUserId ?? task.ApprovalManagerUserId;
                if (manager is null || manager == Guid.Empty)
                {
                    return Response<TaskItemEditResultDto>.Fail(
                        "An approval manager is required when approval is requested.",
                        400, TaskReasonCodes.ValidationFailed, command.CorrelationId);
                }

                // B2 follow-up — the editor switching approval on starts it, and a starter cannot decide it.
                if (manager == _currentUser.UserId)
                {
                    return Response<TaskItemEditResultDto>.Fail(
                        "You cannot be the approver of an approval you request.",
                        400, TaskReasonCodes.ApprovalManagerIsSelf, command.CorrelationId);
                }

                task.ApprovalRequired = true;
                task.ApprovalManagerUserId = manager;
                startApproval = true;
            }
            else
            {
                // ApprovalRequired goes false now; WorkflowInstanceId is kept until the cancel below has used it.
                // D2-FIX3 item 8 — withdrawn with NO link too: a start whose link write fell over (below) left a live
                // instance the task does not point at; the service finds it by the task's object reference.
                task.ApprovalRequired = false;
                cancelApproval = true;
            }
        }
        else if (request.ApprovalManagerUserId is { } reassigned
            && task.ApprovalRequired
            && reassigned != Guid.Empty
            && reassigned != task.ApprovalManagerUserId)
        {
            // B2 follow-up — the same rule for re-pointing: the editor cannot name themselves as the approver.
            if (reassigned == _currentUser.UserId)
            {
                return Response<TaskItemEditResultDto>.Fail(
                    "You cannot be the approver of an approval you request.",
                    400, TaskReasonCodes.ApprovalManagerIsSelf, command.CorrelationId);
            }

            // Re-pointing the approver without touching the requirement: MOD-0023 owns the running instance's
            // assignee, so MOD-0024 records the intent only. Reassignment inside a live approval is Phase 3b.
            task.ApprovalManagerUserId = reassigned;
            _logger.LogInformation(
                "Task {TaskId} approval manager recorded as {ManagerId}; the running MOD-0023 instance keeps its own assignee.",
                task.Id, reassigned);
        }

        /*
         * WHO EDITED IT. ⚠ FOUND LIVE, not by a test: the field-history rows rendered "İsim bulunamadı" as the
         * actor while the `created` row beside them named a person. The repository writes the history at the
         * COMMIT and has no user context of its own — the actor has always had to be DECLARED by the writer, and
         * this handler never declared anything because before field logging it produced no entry at all.
         *
         * "Who changed the due date" is the entire question this log exists to answer, so an entry that cannot
         * name a person is the defect rather than a cosmetic gap.
         *
         * The kind is stated too, rather than left to the repository's `moved ? Unknown : Edited` fallback: this
         * writer knows it is an edit, and a declared intent is how every other handler says so.
         */
        /*
         * ── DCP-005 slice 3 — citations, and what this code deliberately does NOT do ───────────────────────
         *
         * It does not re-read a single document the task already cites. The freezer is handed the EXISTING
         * objects and hands the unchanged ones straight back, so an edit cannot refresh a title, a version or a
         * status — which is the whole of §6.2. There is no batch job and no "refresh" control anywhere in the
         * product for the same reason: the first one written would cancel the freezing silently.
         *
         * Null means "not editing the citations" (every payload written before this field says exactly that);
         * an empty list means "no citations" and clears them.
         */
        {
            // Unconditional now that the freezer is required. ResolveNewAsync itself distinguishes the two
            // meanings of the payload: null DocumentUids is "not editing the citations" and returns them
            // unchanged; an empty list is "no citations" and clears them.
            var frozen = await _documentFreezer.ResolveNewAsync(
                task.DocumentReferences, request.DocumentUids, DateTimeOffset.UtcNow, ct);
            if (!frozen.Success)
            {
                return Response<TaskItemEditResultDto>.Fail(
                    $"The document '{frozen.OffendingUid}' cannot be cited.",
                    400, frozen.ReasonCode!, command.CorrelationId);
            }

            task.DocumentReferences = frozen.References;
        }

        /*
         * D2-FIX4 items 1 and 4 — THE LIVE INSTANCE A WITHDRAW WILL HAVE TO FIND IS ADOPTED BEFORE THIS WRITE, so the link is
         * STORED BY THE SAME CONDITIONAL WRITE THAT SWITCHES THE REQUIREMENT OFF.
         *
         * Before, an approval or review the task did not point at (its link write had fallen over, or the link was stale and
         * a later round was the live one) was found only AFTER this write, adopted in memory, and stored by a SECOND write
         * whose result nobody read. The flag was already off by then, so a second write that lost its race left a live
         * instance with no link and no requirement — nothing would ever look for it again, and the approver kept a decision
         * nobody reads. Read first (read-only, nothing in MOD-0023 is touched), the link rides in the one write: if that
         * write loses, the edit is the 409 it always was and MOD-0023 has not been touched; if it lands, a withdraw that
         * fails after it has a link to follow on the retry.
         *
         * An engine that cannot be READ here refuses the edit the same way (409, nothing written): writing the flag off on a
         * guess is the very loss this guards against.
         */
        if (cancelReview || cancelApproval)
        {
            try
            {
                if (cancelReview && await _reviews.FindUnlinkedOpenInstanceAsync(task, ct) is { } liveReview)
                {
                    task.ReviewWorkflowInstanceId = liveReview;
                }

                if (cancelApproval && await _approvals.FindUnlinkedOpenInstanceAsync(task, ct) is { } liveApproval)
                {
                    task.WorkflowInstanceId = liveApproval;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "Task {TaskId} was not edited: its running approval or review could not be looked up, and the requirement "
                    + "is not switched off on a guess.", task.Id);
                return Response<TaskItemEditResultDto>.Fail(
                    "The task changed meanwhile; reload and retry.",
                    409, TaskReasonCodes.ConcurrencyConflict, command.CorrelationId);
            }

            heldReviewLink = task.ReviewWorkflowInstanceId is not null;
        }

        task.Declare(TaskTransitionKind.Edited, _currentUser.UserId);

        if (!await _tasks.UpdateAsync(task, request.ExpectedVersion, ct))
        {
            return Response<TaskItemEditResultDto>.Fail(
                "The task changed meanwhile; reload and retry.",
                409, TaskReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        /*
         * D2-FIX2 item 2 — THE VERSION THE STORE HOLDS NOW. The edit has landed (version + 1); a refusal from here on is
         * about a follow-up, not the edit, so it carries this number and the form's "save again" is a retry instead of
         * a collision with the very edit it retries. Moved only by a write that LANDED: UpdateAsync stamps the entity
         * before its conditional write, so task.Version alone would also count a write that lost.
         *
         * Why the write is not made to wait for the withdraw (the other way to fix it): the sentence the reader is
         * shown for these two codes, in seven languages, is "your changes are saved … save again to retry" — true only
         * if the edit lands first — and acting on MOD-0023 only AFTER the edit is known to have landed is the rule this
         * handler keeps for every handoff (no workflow touched for an edit that lost its race).
         */
        var landedVersion = task.Version;

        if (startApproval)
        {
            // Same contract as creation: a task whose approval could not be started is KEPT with ApprovalRequired
            // true and no instance id, so the fail-closed gate holds `start` shut until it is retried.
            var instanceId = await _approvals.TryStartApprovalAsync(task, ct);
            if (instanceId is not null)
            {
                task.WorkflowInstanceId = instanceId;
                if (await _tasks.UpdateAsync(task, task.Version, ct))
                {
                    landedVersion = task.Version;
                }
                else
                {
                    // The approval IS running; only its link failed to persist. Never silent: without the id the
                    // projection cannot read the state, and fail-closed then keeps the task blocked.
                    _logger.LogError(
                        "Approval instance {InstanceId} started for task {TaskId} but the link could not be stored; "
                        + "the task stays blocked until it is retried.", instanceId, task.Id);

                    /*
                     * D2-FIX5 item 1 — IF THE WRITER WHO WON DOES NOT WANT IT, IT IS WITHDRAWN. The link write lost because
                     * somebody saved the task between the start and here. When that was an edit that switched the approval
                     * requirement off again (or a cancel, or a delete), its own withdraw ran BEFORE this start and found
                     * nothing: the approval just started would stay decidable for good, with the requirement off and no
                     * link for anything to follow. The task is read again — still wanting approval and open, the approval
                     * stays (the gate reads it by its object reference, and the log above says what is left); otherwise it is
                     * withdrawn, or, if MOD-0023 refuses, the link is stored for the next edit's retry. Never throws.
                     */
                    await TaskWorkflowLinkGate.WithdrawUnwantedStartAsync(
                        task, review: false, _tasks, _approvals, reviews: null, _logger, ct);
                }
            }
            else
            {
                _logger.LogWarning(
                    "Approval was switched on for task {TaskId} but the MOD-0023 instance could not be started; "
                    + "the task stays blocked until it is retried.", task.Id);
            }
        }
        if (cancelReview)
        {
            if (!await _reviews.CancelReviewAsync(task, ct))
            {
                // D2-FIX1 A1 — the review is still open: the link stays, or the reviewer could decide a review that
                // nothing reads any more. The requirement change is saved; the next edit retries the cancel.
                // D2-FIX2 item 2 — and the refusal says at which version the edit landed (see landedVersion).
                // D2-FIX3 item 8 — a review the task did not point at (found by its object reference) was adopted as
                // the link: it is STORED here, or the retry this refusal asks for would have no link to follow.
                // D2-FIX4 item 4 — normally it was adopted BEFORE the write and rides in it (above); only a review that
                // started between that read and now is adopted here, and a write that cannot store it is NEVER dropped
                // silently: the flag is already off, so a link that is not stored is an instance nothing looks for again.
                if (!heldReviewLink && task.ReviewWorkflowInstanceId is not null)
                {
                    if (!await _tasks.UpdateAsync(task, task.Version, ct))
                    {
                        _logger.LogError(
                            "Review {InstanceId} of task {TaskId} could not be withdrawn and its link could not be stored.",
                            task.ReviewWorkflowInstanceId, task.Id);
                        return Response<TaskItemEditResultDto>.Fail(
                            "The task changed meanwhile; reload and retry.",
                            409, TaskReasonCodes.ConcurrencyConflict, command.CorrelationId);
                    }

                    landedVersion = task.Version;
                }

                return Response<TaskItemEditResultDto>.FailWithData(
                    "The edit is saved, but the running review could not be withdrawn; try again.",
                    409, TaskReasonCodes.ReviewCancelFailed, new TaskItemEditResultDto(landedVersion),
                    command.CorrelationId);
            }

            task.ReviewWorkflowInstanceId = null;
            // A task left in PendingReview with no review would be waiting on nobody, with no action to clear it.
            var rewound = false;
            if (task.Lifecycle == TaskLifecycle.PendingReview)
            {
                task.Lifecycle = TaskLifecycle.InProgress;
                // WC-1. Its own kind rather than Resumed: the holder did not pick this back up, the requirement
                // was withdrawn from under them, and reading "resumed" would name the wrong person's decision.
                task.Declare(TaskTransitionKind.ReviewCancelled, _currentUser.UserId);
                rewound = true;
            }

            // Written only when something stored changes: a review found by its object reference and withdrawn was never
            // the stored link, so clearing it changes nothing on disk.
            if (heldReviewLink || rewound)
            {
                if (await _tasks.UpdateAsync(task, task.Version, ct))
                {
                    landedVersion = task.Version;
                }
                else
                {
                    _logger.LogWarning(
                        "Review was switched off for task {TaskId} and cancelled, but clearing the instance link failed.",
                        task.Id);
                }
            }
        }

        if (cancelApproval)
        {
            var heldApprovalLink = task.WorkflowInstanceId is not null;
            if (!await _approvals.CancelApprovalAsync(task, ct))
            {
                // D2-FIX1 A1 — the approval is still open: the link stays (see the review's cancel above).
                // D2-FIX2 item 2 — the version the edit landed at travels with the refusal.
                // D2-FIX3 item 8 — an approval found by its object reference was adopted as the link: stored, so the
                // retry follows it (the requirement is already off, so only a held link makes the next edit retry).
                // D2-FIX4 item 4 — normally stored by the edit's own write (above); a write that cannot store a late
                // adoption is refused loudly, never dropped.
                if (!heldApprovalLink && task.WorkflowInstanceId is not null)
                {
                    if (!await _tasks.UpdateAsync(task, task.Version, ct))
                    {
                        _logger.LogError(
                            "Approval {InstanceId} of task {TaskId} could not be withdrawn and its link could not be stored.",
                            task.WorkflowInstanceId, task.Id);
                        return Response<TaskItemEditResultDto>.Fail(
                            "The task changed meanwhile; reload and retry.",
                            409, TaskReasonCodes.ConcurrencyConflict, command.CorrelationId);
                    }

                    landedVersion = task.Version;
                }

                return Response<TaskItemEditResultDto>.FailWithData(
                    "The edit is saved, but the running approval could not be withdrawn; try again.",
                    409, TaskReasonCodes.ApprovalCancelFailed, new TaskItemEditResultDto(landedVersion),
                    command.CorrelationId);
            }

            task.WorkflowInstanceId = null;
            // A link found by the object reference and withdrawn was never stored: nothing on disk to clear.
            if (heldApprovalLink && !await _tasks.UpdateAsync(task, task.Version, ct))
            {
                _logger.LogWarning(
                    "Approval was switched off for task {TaskId} and cancelled, but clearing the instance link failed.",
                    task.Id);
            }
        }

        return Response<TaskItemEditResultDto>.Success(204, command.CorrelationId);
    }
}

/// <summary>
/// Soft-delete one task.
///
/// <para><b>D2-FIX3 item 1 — a deleted task takes what it started in MOD-0023 with it.</b> Deleting used to leave the
/// approval or review running: the approver kept a decision that escalated, timed out and was read by nobody. Now the
/// delete withdraws them first, exactly as a cancel does (<see cref="TaskWorkflowLinkGate.WithdrawAsync"/>), and a withdraw
/// that does not happen REFUSES the delete — 409 with the existing withdraw code — so nothing is deleted around a
/// decision that is still open.</para>
///
/// <para>The withdraw services are OPTIONAL for the reason they are on <see cref="TransitionTaskItemHandler"/>: tests that
/// predate this construct the handler directly; production wires both. Without them a task holding a link is NOT deleted
/// (fail-closed); a task holding none never asks.</para>
/// </summary>
public sealed class DeleteTaskItemHandler : IRequestHandler<DeleteTaskItemCommand, Response<NoContent>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly ITaskApprovalService? _approvals;
    private readonly ITaskReviewService? _reviews;

    public DeleteTaskItemHandler(
        ITaskItemRepository tasks,
        ITaskApprovalService? approvals = null,
        ITaskReviewService? reviews = null)
    {
        _tasks = tasks;
        _approvals = approvals;
        _reviews = reviews;
    }

    public async Task<Response<NoContent>> Handle(DeleteTaskItemCommand command, CancellationToken ct)
    {
        var task = await _tasks.GetByIdAsync(command.Id, ct);
        if (task is null)
        {
            return Response<NoContent>.Fail("Task not found.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

        if (await TaskWorkflowLinkGate.WithdrawAsync(task, _approvals, _reviews, ct) is { } refusedCode)
        {
            return Response<NoContent>.Fail(
                refusedCode == TaskReasonCodes.ReviewCancelFailed
                    ? "The task was not deleted: its running review could not be withdrawn; try again."
                    : "The task was not deleted: its running approval could not be withdrawn; try again.",
                409, refusedCode, command.CorrelationId);
        }

        /*
         * D2-FIX4 item 7 — THE DELETE IS CONDITIONAL ON THE VERSION READ ABOVE. Between the withdraw and an unconditional
         * soft delete, a start (an edit switching approval or review on) could land and open a NEW instance: the task was
         * deleted around a live decision again. Now a task that moved since it was read is not deleted — 409, and the retry
         * reads it afresh and withdraws what started in between.
         */
        // Soft delete (repository base sets IsDeleted); no hard delete anywhere.
        if (!await _tasks.DeleteAsync(command.Id, task.Version, ct))
        {
            return Response<NoContent>.Fail(
                "The task changed meanwhile; reload and retry.",
                409, TaskReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        return Response<NoContent>.Success(204, command.CorrelationId);
    }
}

/// <summary>
/// Soft-delete several tasks. D2-FIX3 item 1 — each one withdraws what it started first, like the single delete; a task
/// whose withdraw does not happen is left as it is.
///
/// <para><b>D2-FIX5 item 4 — AND THE ANSWER SAYS WHAT WAS LEFT.</b> A task that moved after it was read (an edit that
/// started something between the withdraw and the delete) was skipped with a log line and the same 204 as a delete, so a
/// caller was told "deleted" for a task that was still there. Now, like the subtask cascade, a task whose conditional delete
/// loses is read again and tried again (at most <see cref="MaxDeleteAttempts"/> times — the withdraw runs each time, so what
/// started in between is withdrawn too); and whatever is STILL not deleted — a withdraw MOD-0023 refused, or a task that kept
/// moving — is named: <b>200</b> with <see cref="BulkDeleteTaskItemResultDto.SkippedIds"/> instead of the body-less 204 (which
/// stays the answer when nothing was left out). Only ids of tasks that exist in THIS tenant are ever named: an id that is
/// unknown, deleted or another tenant's is answered like a delete, so the answer leaks nothing about which ids exist — the
/// ids named are ones the caller already holds and can see.</para>
///
/// <para>The single delete is stricter by design — it refuses a task that moved (409) and lets the caller re-read it; a bulk
/// delete has nobody to ask halfway through a list, so it retries on its own, a few times, and reports the rest. Nothing in
/// the web tier calls this endpoint (no proxy, no script), so the new answer needs no screen text.</para>
/// </summary>
public sealed class BulkDeleteTaskItemHandler : IRequestHandler<BulkDeleteTaskItemCommand, Response<BulkDeleteTaskItemResultDto>>
{
    /// <summary>D2-FIX5 item 4 — how many times one task's delete is read and tried while its conditional write keeps losing.</summary>
    internal const int MaxDeleteAttempts = 3;

    private readonly ITaskItemRepository _tasks;
    private readonly ITaskApprovalService? _approvals;
    private readonly ITaskReviewService? _reviews;
    private readonly ILogger<BulkDeleteTaskItemHandler>? _logger;

    public BulkDeleteTaskItemHandler(
        ITaskItemRepository tasks,
        ITaskApprovalService? approvals = null,
        ITaskReviewService? reviews = null,
        ILogger<BulkDeleteTaskItemHandler>? logger = null)
    {
        _tasks = tasks;
        _approvals = approvals;
        _reviews = reviews;
        _logger = logger;
    }

    public async Task<Response<BulkDeleteTaskItemResultDto>> Handle(BulkDeleteTaskItemCommand command, CancellationToken ct)
    {
        var skipped = new List<Guid>();
        foreach (var id in command.Request.Ids.Distinct())
        {
            if (!await DeleteOneAsync(id, ct))
            {
                skipped.Add(id);
            }
        }

        return skipped.Count == 0
            ? Response<BulkDeleteTaskItemResultDto>.Success(204, command.CorrelationId)
            : Response<BulkDeleteTaskItemResultDto>.Success(new BulkDeleteTaskItemResultDto(skipped), 200, command.CorrelationId);
    }

    /// <summary>False only for a task of this tenant that is still there and could not be deleted.</summary>
    private async Task<bool> DeleteOneAsync(Guid id, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxDeleteAttempts; attempt++)
        {
            // Each delete re-reads through the tenant filter, so an id from another tenant — or one somebody deleted
            // meanwhile — is simply not there: answered like a delete.
            var task = await _tasks.GetByIdAsync(id, ct);
            if (task is null)
            {
                return true;
            }

            if (await TaskWorkflowLinkGate.WithdrawAsync(task, _approvals, _reviews, ct) is { } refusedCode)
            {
                _logger?.LogWarning(
                    "Task {TaskId} was left out of a bulk delete: its running approval or review could not be withdrawn ({ReasonCode}).",
                    task.Id, refusedCode);
                return false;
            }

            // Conditional on the version read above, like the single delete: a task that moved since (a start landed after
            // its withdraw) is not deleted around the new instance — it is read again, withdrawn again, and tried again.
            if (await _tasks.DeleteAsync(id, task.Version, ct))
            {
                return true;
            }

            _logger?.LogWarning(
                "Task {TaskId} changed after it was read for a bulk delete (try {Attempt} of {Attempts}).",
                task.Id, attempt, MaxDeleteAttempts);
        }

        _logger?.LogWarning(
            "Task {TaskId} was left out of a bulk delete: it kept changing after it was read ({Attempts} tries).",
            id, MaxDeleteAttempts);
        return false;
    }
}
