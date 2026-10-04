using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Domain.Entities.Tasks;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>What kind of write the caller is making on a task — it decides which relationship rule applies.</summary>
public enum TaskWriteKind
{
    /// <summary>Changing the work: fields, checklist, dependencies, attachments, subtasks, time, transitions.
    /// The sender of an accepted request may not (decision 3); the person asked may not before answering.</summary>
    Change,

    /// <summary>Saying something about it. The sender of an accepted request may (they "watch and comment"); the
    /// person asked may not before answering — their name would reach a feed the sender's colleagues read.</summary>
    Comment,

    /// <summary>A manager's verb carried by its own key (delete). Only the read base applies.</summary>
    Administer,

    /// <summary>The caller's own view of the task (pin, snooze). Only the read base applies.</summary>
    Personal
}

/// <summary>
/// WP-WCN-TASK-REQUEST-01 FIX2 — THE WRITE GATE OF A TASK (CT decision 2026-10-03): nobody reaches a write on a task
/// they cannot READ. The read rule (<see cref="ITaskReadAccessPolicy.CanReadAsync"/>) is the base of every write;
/// each endpoint's own relationship rules narrow it, never widen it.
///
/// <para><b>One order everywhere.</b> Read (404 — the task's existence does not leak) → person and relationship
/// (403, or 409 for the person a request waits on) → the task's state (409) → the request's own validation (400).
/// Asked in another order, a caller who cannot read the task learns from a state or validation code that it exists
/// — and, for a request, that it is one (<c>TASK_REQUEST_UNIT_FIXED</c>, a 403 on accept).</para>
///
/// <para>Before this gate most writes asked only for the endpoint's permission: anybody holding
/// <c>platform.tasks.update</c> could change any task whose id they knew, and task ids are listed by the link
/// picker (BL-525).</para>
/// </summary>
public interface ITaskWriteGate
{
    /// <summary>Null when the caller may make this kind of write; the refusal otherwise. A null task is a 404.</summary>
    Task<TaskAssignmentRefusal?> CheckAsync(TaskItem? task, TaskWriteKind kind, CancellationToken ct);
}

/// <inheritdoc cref="ITaskWriteGate"/>
public sealed class TaskWriteGate : ITaskWriteGate
{
    private readonly ITaskReadAccessPolicy _readAccess;
    private readonly ICurrentUserContext _currentUser;

    public TaskWriteGate(ITaskReadAccessPolicy readAccess, ICurrentUserContext currentUser)
    {
        _readAccess = readAccess;
        _currentUser = currentUser;
    }

    /// <summary>The one answer for "no such task" and "not yours to see" alike.</summary>
    public static TaskAssignmentRefusal NotFound { get; } = new(404, TaskReasonCodes.NotFound, "Task not found.");

    /// <summary>The person a request waits on reads it to answer it, and does nothing else until they do.</summary>
    public static TaskAssignmentRefusal AnswerFirst { get; } = new(
        409, TaskReasonCodes.RequestNotAccepted, "Answer the request first: accept it, or decline it with a reason.");

    public async Task<TaskAssignmentRefusal?> CheckAsync(TaskItem? task, TaskWriteKind kind, CancellationToken ct)
    {
        var me = _currentUser.UserId;
        if (task is null || !await _readAccess.CanReadAsync(task, me, ct))
        {
            return NotFound;
        }

        if (kind is TaskWriteKind.Administer or TaskWriteKind.Personal)
        {
            return null;
        }

        if (kind == TaskWriteKind.Change && TaskRequestPolicy.RefuseSenderOfAcceptedRequest(task, me) is { } sender)
        {
            return sender;
        }

        return TaskRequestPolicy.IsOfferedTo(task, me) ? AnswerFirst : null;
    }
}

/// <summary>A gate refusal as the handler's response.</summary>
public static class TaskWriteGateResponses
{
    public static Response<T> ToResponse<T>(this TaskAssignmentRefusal refusal, string correlationId)
        => Response<T>.Fail(refusal.Message, refusal.StatusCode, refusal.ReasonCode, correlationId);
}
