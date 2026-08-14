using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;

/*
 * WC-1 — THE PERSONAL OVERLAY'S THREE WRITES.
 *
 * What was measured on 2026-08-14: the detail page had a note box, a save button and a "Not kaydedildi" toast,
 * and the whole of it was one assignment to a JavaScript object. No request left the browser, nothing was stored
 * anywhere, and the next reload took the note away without a word. The snooze was the same shape.
 *
 * ONE guard rail runs through all three handlers: they are guarded by the task's READ permission, not Update.
 * Writing a note to myself about somebody else's work is not a change to that work — it is a change to my own
 * view of it. Requiring Update would mean a person who may look at a task but not move it cannot leave themselves
 * a reminder about it, which is exactly the reader who most needs one.
 *
 * And ONE rule they all obey: the caller's own user id comes from ICurrentUserContext and is never taken from the
 * request. A client-supplied author would make "only I see my notes" a suggestion.
 */

/// <summary>
/// Add one private note to a task.
///
/// <para>A CLOSED task still accepts notes, unlike a comment. The reasoning is the reverse of the comment rule
/// and it is deliberate: a comment is addressed to other people about live work, while a note is addressed to
/// oneself and finished work is exactly what one writes conclusions about ("this is why it was cancelled").
/// Nothing another person can read changes, so there is nothing to seal.</para>
/// </summary>
public sealed class AddTaskPersonalNoteHandler
    : IRequestHandler<AddTaskPersonalNoteCommand, Response<Guid>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly ITaskPersonalOverlayRepository _overlays;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;

    public AddTaskPersonalNoteHandler(
        ITaskItemRepository tasks,
        ITaskPersonalOverlayRepository overlays,
        ICurrentUserContext currentUser,
        ITenantContext tenantContext)
    {
        _tasks = tasks;
        _overlays = overlays;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(AddTaskPersonalNoteCommand command, CancellationToken ct)
    {
        var text = command.Request.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text) || text.Length > TaskPersonalNoteLimits.MaxTextLength)
        {
            return Response<Guid>.Fail(
                $"A note must be between 1 and {TaskPersonalNoteLimits.MaxTextLength} characters.",
                400, TaskReasonCodes.PersonalNoteTextInvalid, command.CorrelationId);
        }

        // The task is READ first so a note can never be written against something the caller cannot see. The
        // repository's execution filter is what makes this a real check: another tenant's task comes back null.
        var task = await _tasks.GetByIdAsync(command.TaskItemId, ct);
        if (task is null)
        {
            return Response<Guid>.Fail("Task not found.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

        var overlay = await _overlays.GetAsync(task.Id, _currentUser.UserId, ct)
            ?? NewOverlay(task.Id, _currentUser.UserId, _tenantContext.TenantId, _currentUser.ActorName);

        if (overlay.Notes.Count >= TaskPersonalNoteLimits.MaxNotesPerTask)
        {
            // Same code as an invalid text, and the message names the limit: from the caller's side "too many"
            // and "too long" are the same correction — write less.
            return Response<Guid>.Fail(
                $"A task keeps at most {TaskPersonalNoteLimits.MaxNotesPerTask} personal notes per person.",
                400, TaskReasonCodes.PersonalNoteTextInvalid, command.CorrelationId);
        }

        var note = new TaskPersonalNote
        {
            Text = text,
            AuthorUserId = _currentUser.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        overlay.Notes.Add(note);
        await _overlays.UpsertAsync(overlay, ct);

        return Response<Guid>.Success(note.Id, 201, command.CorrelationId);
    }

    internal static TaskPersonalOverlay NewOverlay(Guid taskItemId, Guid userId, Guid tenantId, string actorName)
        => new()
        {
            TenantId = tenantId,
            TaskItemId = taskItemId,
            UserId = userId,
            CreatedBy = actorName
        };
}

/// <summary>Delete one of the caller's own notes. Someone else's id is a 404 — see the command for why.</summary>
public sealed class DeleteTaskPersonalNoteHandler
    : IRequestHandler<DeleteTaskPersonalNoteCommand, Response<NoContent>>
{
    private readonly ITaskPersonalOverlayRepository _overlays;
    private readonly ICurrentUserContext _currentUser;

    public DeleteTaskPersonalNoteHandler(
        ITaskPersonalOverlayRepository overlays,
        ICurrentUserContext currentUser)
    {
        _overlays = overlays;
        _currentUser = currentUser;
    }

    public async Task<Response<NoContent>> Handle(DeleteTaskPersonalNoteCommand command, CancellationToken ct)
    {
        /*
         * The overlay is fetched for THIS user, so the note being deleted is structurally the caller's own: there
         * is no branch here that could delete somebody else's note, because no other user's overlay is ever in
         * hand. That is the difference between an authorization CHECK and an authorization SHAPE — a check can be
         * forgotten by the next person to edit this method.
         */
        var overlay = await _overlays.GetAsync(command.TaskItemId, _currentUser.UserId, ct);
        var note = overlay?.Notes.FirstOrDefault(n => n.Id == command.NoteId);
        if (overlay is null || note is null)
        {
            return Response<NoContent>.Fail(
                "Note not found.", 404, TaskReasonCodes.PersonalNoteNotFound, command.CorrelationId);
        }

        overlay.Notes.Remove(note);
        await _overlays.UpsertAsync(overlay, ct);
        return Response<NoContent>.Success(204, command.CorrelationId);
    }
}

/// <summary>
/// Set or clear the caller's snooze.
///
/// <para><b>The task is not touched.</b> Not its lifecycle, not its normalized status, not its waiting context —
/// the executable contract states this as <c>SNOOZE_MUST_NOT_CREATE_WAITING</c>, and it is a rule about people
/// rather than about data: a requester must not be able to see that the holder has parked their request.</para>
/// </summary>
public sealed class SetTaskSnoozeHandler : IRequestHandler<SetTaskSnoozeCommand, Response<NoContent>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly ITaskPersonalOverlayRepository _overlays;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;

    public SetTaskSnoozeHandler(
        ITaskItemRepository tasks,
        ITaskPersonalOverlayRepository overlays,
        ICurrentUserContext currentUser,
        ITenantContext tenantContext)
    {
        _tasks = tasks;
        _overlays = overlays;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
    }

    public async Task<Response<NoContent>> Handle(SetTaskSnoozeCommand command, CancellationToken ct)
    {
        var until = command.Request.SnoozedUntil;

        /*
         * A date already in the past is REFUSED rather than quietly stored. Storing it would be worse than an
         * error: the projection reads "snoozed until yesterday" as not snoozed at all, so the write would report
         * success, change nothing visible, and leave the reader believing the work was parked. That is the same
         * shape as the toast this whole round exists to remove.
         *
         * Clearing (null) is always allowed — waking work up needs no date to be valid.
         */
        if (until is { } date && date <= DateTimeOffset.UtcNow)
        {
            return Response<NoContent>.Fail(
                "A snooze date must be in the future.",
                400, TaskReasonCodes.SnoozeDateInvalid, command.CorrelationId);
        }

        var task = await _tasks.GetByIdAsync(command.TaskItemId, ct);
        if (task is null)
        {
            return Response<NoContent>.Fail(
                "Task not found.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

        var overlay = await _overlays.GetAsync(task.Id, _currentUser.UserId, ct)
            ?? AddTaskPersonalNoteHandler.NewOverlay(
                task.Id, _currentUser.UserId, _tenantContext.TenantId, _currentUser.ActorName);

        overlay.SnoozedUntil = until;
        await _overlays.UpsertAsync(overlay, ct);
        return Response<NoContent>.Success(204, command.CorrelationId);
    }
}
