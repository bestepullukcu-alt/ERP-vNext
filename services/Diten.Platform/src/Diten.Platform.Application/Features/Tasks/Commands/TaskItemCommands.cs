using Diten.Platform.Application.Common;
using Diten.Platform.Domain.Enums.Tasks;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Commands;

// MOD-0024 — commands are sealed records. TenantId never travels in a command payload: it is resolved from the
// server-side tenant context. Every state-changing command carries an expected version so a concurrent write
// produces a controlled 409 instead of a silent overwrite (pack §13).

public sealed record CreateTaskItemCommand(CreateTaskItemRequest Request, string CorrelationId)
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

/// <summary>Create a task from a reusable template, instantiating its checklist too (pack §12 E5).</summary>
public sealed record CreateTaskItemFromTemplateCommand(
    CreateTaskFromTemplateRequest Request,
    string CorrelationId) : IRequest<Response<Guid>>;
