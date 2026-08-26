using Diten.Platform.Application.Common;
using Diten.Platform.Domain.Enums.Tasks;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Commands;

// MOD-0024 — commands are sealed records. TenantId never travels in a command payload: it is resolved from the
// server-side tenant context. Every state-changing command carries an expected version so a concurrent write
// produces a controlled 409 instead of a silent overwrite (pack §13).

/// <param name="EnforceRequiredFields">
/// Whether a REQUIRED configurable field the request never mentions refuses the create. True for a person
/// filling the form. The MACHINE-made paths — the recurrence sweep and creation from a template — pass false
/// and say why at their call sites: a sweep has nobody to ask, so refusing would not collect the value, it
/// would stop the recurrence while the period is consumed anyway.
/// Trailing and defaulted so every existing caller keeps the strict behaviour without being edited.
/// </param>
public sealed record CreateTaskItemCommand(
    CreateTaskItemRequest Request,
    string CorrelationId,
    bool EnforceRequiredFields = true)
    : IRequest<Response<Guid>>;

public sealed record UpdateTaskItemCommand(Guid Id, UpdateTaskItemRequest Request, string CorrelationId)
    : IRequest<Response<NoContent>>;

public sealed record DeleteTaskItemCommand(Guid Id, string CorrelationId)
    : IRequest<Response<NoContent>>;

public sealed record BulkDeleteTaskItemCommand(BulkDeleteTaskItemRequest Request, string CorrelationId)
    : IRequest<Response<NoContent>>;

/// <summary>Accept a task that was assigned to me (the Inbox acceptance gate).</summary>
public sealed record AcceptTaskItemCommand(Guid Id, TaskTransitionRequest Request, string CorrelationId)
    : IRequest<Response<NoContent>>;

/// <summary>
/// Take an unclaimed pool task. Guarded by expected-version concurrency: with two simultaneous claims exactly
/// one wins and the other receives 409 TASK_ALREADY_CLAIMED (pack §13).
/// </summary>
public sealed record ClaimTaskItemCommand(Guid Id, ClaimTaskItemRequest Request, string CorrelationId)
    : IRequest<Response<NoContent>>;

/// <summary>Return a claimed pool task to its pool (ownership → unowned, admission → pendingClaim).</summary>
public sealed record ReleaseTaskItemCommand(Guid Id, TaskTransitionRequest Request, string CorrelationId)
    : IRequest<Response<NoContent>>;

/// <summary>
/// Set or move a personal plan date.
///
/// <para>Separate from <see cref="TransitionTaskItemCommand"/> for the same reason <see cref="InquireTaskItemCommand"/>
/// is: the date is REQUIRED here and the shared transition request has no field for it. It also targets
/// <c>Planned</c> from BOTH <c>Open</c> (first plan) and <c>Planned</c> itself (re-plan) — a self-loop the generic
/// transition matrix would otherwise have to special-case for every other caller of <c>Target</c>.</para>
/// </summary>
public sealed record PlanTaskItemCommand(Guid Id, PlanTaskItemRequest Request, string CorrelationId)
    : IRequest<Response<NoContent>>;

/// <summary>
/// Hand finished work to a reviewer: InProgress → PendingReview.
///
/// <para>Deliberately NOT routed through <see cref="TransitionTaskItemCommand"/>, for the same reason
/// <see cref="InquireTaskItemCommand"/> is not: this transition has a SIDE EFFECT the generic one has no business
/// carrying — it opens a MOD-0023 instance. Folding it in would put "start a workflow" inside the handler that
/// serves start/complete/cancel, where the next reader would reasonably assume it fires for all of them.</para>
/// </summary>
public sealed record SubmitTaskForReviewCommand(Guid Id, TaskTransitionRequest Request, string CorrelationId)
    : IRequest<Response<NoContent>>;

public sealed record TransitionTaskItemCommand(
    Guid Id,
    TaskLifecycle Target,
    TaskTransitionRequest Request,
    string CorrelationId,
    /// <summary>
    /// Whether the caller holds administrative authority over any task (the DELETE permission), evaluated by the
    /// controller from the caller's claims and passed in as DATA — the same seam WorkItemsController uses.
    /// PermissionClaimEvaluator lives in the API layer precisely so enforcement and evaluation cannot drift, so
    /// the handler must not re-derive this from claims itself.
    ///
    /// <para>Defaults to FALSE so every existing caller, and any new one that forgets, is treated as an ordinary
    /// user: the cancel guard fails closed.</para>
    /// </summary>
    bool ActorMayCancelAnyTask = false) : IRequest<Response<NoContent>>;

/// <summary>
/// Park a task in <see cref="TaskLifecycle.Waiting"/> because the holder is blocked on someone else.
///
/// <para>Separate from <see cref="TransitionTaskItemCommand"/> because the REASON is mandatory here: "waiting"
/// without saying what for is not something a colleague can act on, and the shared transition request carries
/// only an optional reason code.</para>
/// </summary>
public sealed record InquireTaskItemCommand(Guid Id, InquireTaskItemRequest Request, string CorrelationId)
    : IRequest<Response<NoContent>>;

/// <summary>
/// Give assigned work back to whoever asked for it.
///
/// <para><b>`return` is also MOD-0023's verb.</b> An approver returns an approval or a review to its submitter,
/// and that is a DIFFERENT path with a different owner: MOD-0023 decides approvals, MOD-0024 never does (charter
/// Binding A). The two share a verb because "send it back" is the same idea, not because they share an
/// implementation — they act on different work-intent types (`task` here, `approval`/`review` there) and the
/// projection is per item, so nothing routes between them. Do NOT merge these paths.</para>
/// </summary>
public sealed record ReturnTaskItemCommand(Guid Id, ReturnTaskItemRequest Request, string CorrelationId)
    : IRequest<Response<NoContent>>;

/// <summary>Hand work to a different person; they receive it unaccepted, in their Inbox.</summary>
public sealed record ReassignTaskItemCommand(Guid Id, ReassignTaskItemRequest Request, string CorrelationId)
    : IRequest<Response<NoContent>>;

// ── Phase 2: checklist (pack §12 E1) ─────────────────────────────────────────

/// <summary>Tick or untick one checklist item on a task.</summary>
public sealed record SetChecklistItemStateCommand(
    Guid TaskItemId,
    SetChecklistItemStateRequest Request,
    string CorrelationId) : IRequest<Response<NoContent>>;

/// <summary>
/// Add an AD-HOC item to a task's checklist — text the user typed, so it carries display text and never a
/// resource key (a key would render as itself).
/// </summary>
public sealed record AddChecklistItemCommand(
    Guid TaskItemId,
    AddChecklistItemRequest Request,
    string CorrelationId) : IRequest<Response<NoContent>>;

/// <summary>Edit one checklist item's editable face — its text, its level and its evidence flag.</summary>
public sealed record UpdateChecklistItemCommand(
    Guid TaskItemId,
    string ItemCode,
    UpdateChecklistItemRequest Request,
    string CorrelationId) : IRequest<Response<NoContent>>;

/// <summary>Remove one checklist item from a task's live list.</summary>
public sealed record RemoveChecklistItemCommand(
    Guid TaskItemId,
    string ItemCode,
    RemoveChecklistItemRequest Request,
    string CorrelationId) : IRequest<Response<NoContent>>;

/// <summary>Write the whole checklist order in a single conditional write.</summary>
public sealed record ReorderChecklistCommand(
    Guid TaskItemId,
    ReorderChecklistRequest Request,
    string CorrelationId) : IRequest<Response<NoContent>>;

/// <summary>Create a task from a reusable template, instantiating its checklist too (pack §12 E5).</summary>
public sealed record CreateTaskItemFromTemplateCommand(
    CreateTaskFromTemplateRequest Request,
    string CorrelationId) : IRequest<Response<Guid>>;

// ── Dependencies (BL-028, pack §12 Y3) ───────────────────────────────────────

/// <summary>
/// Add a typed edge between two of MOD-0024's own tasks. The handler refuses self-edges, duplicates and anything
/// that would close a CYCLE — a cycle is not a modelling curiosity here, it is work that can never start.
/// </summary>
public sealed record AddTaskDependencyCommand(
    Guid TaskItemId,
    AddTaskDependencyRequest Request,
    string CorrelationId) : IRequest<Response<Guid>>;

/// <summary>Remove one edge. Removing an edge that is not on this task is a 404, not a silent success.</summary>
public sealed record RemoveTaskDependencyCommand(
    Guid TaskItemId,
    Guid DependencyId,
    string CorrelationId) : IRequest<Response<NoContent>>;

// ── Comments (BL-034 item 7) ─────────────────────────────────────────────────

/// <summary>
/// Add a comment. There is no Update or Delete counterpart, and that is a decision rather than an omission: a
/// comment is a record of what was said, and removing it after someone has acted on it rewrites the past.
/// </summary>
public sealed record AddTaskCommentCommand(
    Guid TaskItemId,
    AddTaskCommentRequest Request,
    string CorrelationId) : IRequest<Response<Guid>>;

/// <summary>
/// Rewrite one's OWN comment. The text changes and an <c>editedAt</c> stamp goes with it, so the feed says the
/// sentence has moved since it was first read.
/// </summary>
public sealed record UpdateTaskCommentCommand(
    Guid TaskItemId,
    Guid CommentId,
    UpdateTaskCommentRequest Request,
    string CorrelationId) : IRequest<Response<NoContent>>;

/// <summary>
/// Withdraw one's OWN comment. A TOMBSTONE, not a removal: the text is cleared and the row stays, so the feed
/// keeps a marker where somebody spoke and then took it back.
/// </summary>
public sealed record WithdrawTaskCommentCommand(
    Guid TaskItemId,
    Guid CommentId,
    string CorrelationId) : IRequest<Response<NoContent>>;

// ── The personal overlay (WC-1) ──────────────────────────────────────────────

/// <summary>
/// Add one private note. There is no update command, by decision: delete-then-write is the same act with one
/// fewer endpoint and one fewer concurrency question (see <c>TaskPersonalNote</c>).
/// </summary>
public sealed record AddTaskPersonalNoteCommand(
    Guid TaskItemId,
    AddTaskPersonalNoteRequest Request,
    string CorrelationId) : IRequest<Response<Guid>>;

/// <summary>
/// Delete one of the CALLER'S OWN notes. A note id belonging to anybody else answers 404 — the same answer a
/// nonexistent id gets, because a distinct "not yours" would confirm that somebody else's note exists.
/// </summary>
public sealed record DeleteTaskPersonalNoteCommand(
    Guid TaskItemId,
    Guid NoteId,
    string CorrelationId) : IRequest<Response<NoContent>>;

/// <summary>Set or clear the caller's own snooze. Never touches the task's lifecycle — that is the contract.</summary>
public sealed record SetTaskSnoozeCommand(
    Guid TaskItemId,
    SetTaskSnoozeRequest Request,
    string CorrelationId) : IRequest<Response<NoContent>>;

/// <summary>Pin or unpin for the caller. Same overlay row as the snooze above, same untouched task.</summary>
public sealed record SetTaskPinnedCommand(
    Guid TaskItemId,
    SetTaskPinnedRequest Request,
    string CorrelationId) : IRequest<Response<NoContent>>;

// ── Phase 5: configurable field definitions ──────────────────────────────────

public sealed record CreateTaskFieldDefinitionCommand(
    CreateTaskFieldDefinitionRequest Request, string CorrelationId) : IRequest<Response<Guid>>;

/// <summary>
/// Edit a definition. <c>Code</c> is deliberately NOT on the request — see UpdateTaskFieldDefinitionRequest.
/// </summary>
public sealed record UpdateTaskFieldDefinitionCommand(
    Guid Id, UpdateTaskFieldDefinitionRequest Request, string CorrelationId) : IRequest<Response<NoContent>>;

/// <summary>
/// Retire a definition. SOFT — see the handler for why a definition in use is never hard-deleted.
/// </summary>
public sealed record DeleteTaskFieldDefinitionCommand(Guid Id, string CorrelationId)
    : IRequest<Response<NoContent>>;

/// <summary>
/// Retire several. Deliberately a LOOP over the single command rather than a second write path — the retire
/// semantics (soft, never destructive, IsActive and DeletedAt together) must not exist twice.
/// </summary>
public sealed record BulkDeleteTaskFieldDefinitionCommand(
    BulkDeleteTaskFieldDefinitionRequest Request, string CorrelationId)
    : IRequest<Response<BulkDeactivateFieldDefinitionsResponse>>;


// ── DCP-005 slice 1: task types ─────────────────────────────────────────────

public sealed record CreateTaskTypeCommand(
    CreateTaskTypeRequest Request, string CorrelationId) : IRequest<Response<Guid>>;

public sealed record UpdateTaskTypeCommand(
    Guid Id, UpdateTaskTypeRequest Request, string CorrelationId) : IRequest<Response<NoContent>>;

/// <summary>
/// Retire or restore. NOT a delete: a type that has been used must stay readable, exactly as folders and
/// documents do — see the handler.
/// </summary>
public sealed record SetTaskTypeActiveCommand(
    Guid Id, SetTaskTypeActiveRequest Request, string CorrelationId) : IRequest<Response<NoContent>>;


// ── DCP-005 slice 2: the document reference list ────────────────────────────

public sealed record DryRunDocumentReferenceListCommand(
    ImportDocumentReferenceListRequest Request, string CorrelationId)
    : IRequest<Response<DocumentReferenceListDryRunResult>>;

public sealed record ImportDocumentReferenceListCommand(
    ImportDocumentReferenceListRequest Request, string CorrelationId)
    : IRequest<Response<DocumentReferenceListVersionDto>>;


public sealed record WithdrawDocumentListVersionCommand(
    Guid Id, WithdrawDocumentListVersionRequest Request, string CorrelationId)
    : IRequest<Response<NoContent>>;
