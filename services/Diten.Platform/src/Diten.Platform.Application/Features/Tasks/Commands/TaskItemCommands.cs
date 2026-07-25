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
    string CorrelationId) : IRequest<Response<NoContent>>;
