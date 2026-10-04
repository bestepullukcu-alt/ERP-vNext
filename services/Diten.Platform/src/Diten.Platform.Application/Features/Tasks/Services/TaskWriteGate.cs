using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementContract;
using Diten.Platform.Domain.Entities.Tasks;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>What kind of write the caller is making on a task — it decides which relationship rule applies.</summary>
/// <remarks>
/// The four kinds answer the three relationships differently, and no two answer them all alike (FIX3):
/// <code>
///                the person a request waits on   a reader by watching / being asked   the sender of an accepted request
///   Change       409 answer first                 403                                   403
///   Comment      409 answer first                 allowed                               allowed
///   Administer   409 answer first                 403                                   allowed
///   Personal     allowed                          allowed                               allowed
/// </code>
/// </remarks>
public enum TaskWriteKind
{
    /// <summary>Changing the work: fields, checklist, dependencies, attachments, subtasks, time, the review meeting.
    /// The sender of an accepted request may not (decision 3); the person asked may not before answering; a reader by
    /// watching or by being asked a question may not (FIX3, item 8).</summary>
    Change,

    /// <summary>Saying something about it: a comment, an edit of one's own comment, and the ANSWER to the question a
    /// waiting task is asking the caller (BL-439 — the answer is information handed back, not an act on the work). The
    /// sender of an accepted request may (they "watch and comment"), and so may a watcher and the person asked a
    /// question; the person a request waits on may not before answering — their name would reach a feed the sender's
    /// colleagues read.</summary>
    Comment,

    /// <summary>An act on the task carried by its own verb and key: delete, the lifecycle transitions, claim, release,
    /// plan, return, reassign, waiting. Each handler still asks its own who-question (holder, requester, the delete key).
    /// FIX3 (CT decision, blocker 2): the person a request waits on reaches none of them before answering (409, the same
    /// code a change gets), and a reader by watching or by being asked reaches none of them at all (403). The sender of
    /// an accepted request is not refused HERE — the verbs that would direct the holder's work refuse them by their own
    /// rule (reassign, cancel without the delete key).</summary>
    Administer,

    /// <summary>The caller's OWN layer of the task — pin, snooze, a private note, withdrawing their own words, and
    /// naming the task as a predecessor of their own work. It writes nothing on the task itself, so only the read base
    /// applies: whoever reads the task, for whatever reason, keeps their own view of it.</summary>
    Personal
}

/// <summary>
/// WP-WCN-TASK-REQUEST-01 FIX2 — THE WRITE GATE OF A TASK (CT decision 2026-10-03): nobody reaches a write on a task
/// they cannot READ. The read rule (<see cref="ITaskReadAccessPolicy"/>) is the base of every write; each endpoint's
/// own relationship rules narrow it, never widen it.
///
/// <para><b>One order everywhere.</b> Read (404 — the task's existence does not leak) → person and relationship
/// (403, or 409 for the person a request waits on) → the task's state (409) → the request's own validation (400).
/// Asked in another order, a caller who cannot read the task learns from a state or validation code that it exists
/// — and, for a request, that it is one (<c>TASK_REQUEST_UNIT_FIXED</c>, a 403 on accept).</para>
///
/// <para><b>One decision, two shapes</b> (FIX3, item 6). <see cref="CheckAsync"/> answers for one task,
/// <see cref="AllowedTaskIdsAsync"/> for many (a timesheet save); both ask the read rule WHY the caller reads
/// (<see cref="TaskReadGround"/>) and then the same pure <see cref="Judge"/>. There is no second copy of the
/// relationship rules anywhere — a copy is how the time path came to miss the watcher rule.</para>
///
/// <para>Before this gate most writes asked only for the endpoint's permission: anybody holding
/// <c>platform.tasks.update</c> could change any task whose id they knew, and task ids are listed by the link
/// picker (BL-525).</para>
/// </summary>
public interface ITaskWriteGate
{
    /// <summary>Null when the caller may make this kind of write; the refusal otherwise. A null task is a 404.</summary>
    Task<TaskAssignmentRefusal?> CheckAsync(TaskItem? task, TaskWriteKind kind, CancellationToken ct);

    /// <summary>
    /// FIX3 (item 6) — the ids among <paramref name="tasks"/> on which <paramref name="actorUserId"/> may make this kind
    /// of write: the read rule asked ONCE for all of them (its batched form), then <see cref="TaskWriteGate.Judge"/> task
    /// by task. A task that is not answered is not allowed.
    /// </summary>
    Task<IReadOnlySet<Guid>> AllowedTaskIdsAsync(
        IReadOnlyCollection<TaskItem> tasks, Guid actorUserId, TaskWriteKind kind, CancellationToken ct);
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

    /// <summary>The one answer for "no such task" and "not yours to see" alike — every task 404 that may stand in for
    /// an unreadable task carries exactly this body (FIX3, item 3).</summary>
    public static TaskAssignmentRefusal NotFound { get; } = new(404, TaskReasonCodes.NotFound, "Task not found.");

    /// <summary>The person a request waits on reads it to answer it, and does nothing else until they do.</summary>
    public static TaskAssignmentRefusal AnswerFirst { get; } = new(
        409, TaskReasonCodes.RequestNotAccepted, "Answer the request first: accept it, or decline it with a reason.");

    /// <summary>
    /// FIX3 (item 8) — a reader by watching, or by being the person a waiting task asks, reads and comments; acting on
    /// the task is somebody else's. The permission code every other "not yours to do" answers with, so no client needs
    /// a new text for it.
    /// </summary>
    public static TaskAssignmentRefusal ReadOnlyReader { get; } = new(
        403, DocumentManagementReasonCodes.PermissionDenied,
        "You follow this task or were asked about it: you can read it and comment, but not change it.");

    public async Task<TaskAssignmentRefusal?> CheckAsync(TaskItem? task, TaskWriteKind kind, CancellationToken ct)
    {
        if (task is null)
        {
            return NotFound;
        }

        var me = _currentUser.UserId;
        return Judge(task, await _readAccess.ReadGroundAsync(task, me, ct), kind, me);
    }

    public async Task<IReadOnlySet<Guid>> AllowedTaskIdsAsync(
        IReadOnlyCollection<TaskItem> tasks, Guid actorUserId, TaskWriteKind kind, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        if (tasks.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var grounds = await _readAccess.ReadGroundsAsync(tasks, actorUserId, ct);
        return tasks
            .Where(task => Judge(task, grounds.GetValueOrDefault(task.Id, TaskReadGround.None), kind, actorUserId) is null)
            .Select(task => task.Id)
            .ToHashSet();
    }

    /// <summary>
    /// THE decision, once the read rule has said why <paramref name="actorUserId"/> reads the task. Pure: the same
    /// answer for one task and for a batch. Read → the person a request waits on → the reader who only watches or was
    /// asked → the sender of an accepted request.
    /// </summary>
    public static TaskAssignmentRefusal? Judge(TaskItem task, TaskReadGround ground, TaskWriteKind kind, Guid actorUserId)
    {
        ArgumentNullException.ThrowIfNull(task);

        if (ground == TaskReadGround.None)
        {
            return NotFound;
        }

        if (kind == TaskWriteKind.Personal)
        {
            return null;
        }

        // CT decision (FIX3, blocker 2): before the answer, the person asked reads the request and answers it —
        // accept, or decline with a reason — and NOTHING else, Administer included.
        if (TaskRequestPolicy.IsOfferedTo(task, actorUserId))
        {
            return AnswerFirst;
        }

        // FIX3 (item 8) — read access that comes ONLY from watching or from being asked a question is not a footing to
        // change or administer the task from, whatever write keys the token carries. Saying something stays open.
        if (ground == TaskReadGround.Observer && kind is TaskWriteKind.Change or TaskWriteKind.Administer)
        {
            return ReadOnlyReader;
        }

        // BL-509 decision 3 — the sender of an accepted request watches and comments: refused a CHANGE only.
        if (kind == TaskWriteKind.Change && TaskRequestPolicy.RefuseSenderOfAcceptedRequest(task, actorUserId) is { } sender)
        {
            return sender;
        }

        return null;
    }
}

/// <summary>A gate refusal as the handler's response.</summary>
public static class TaskWriteGateResponses
{
    public static Response<T> ToResponse<T>(this TaskAssignmentRefusal refusal, string correlationId)
        => Response<T>.Fail(refusal.Message, refusal.StatusCode, refusal.ReasonCode, correlationId);
}
