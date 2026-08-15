using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Application.Features.Tasks.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;

/// <summary>
/// BL-034 item 7 — the writable half of the activity feed.
///
/// <para><b>Who may comment: anyone who can SEE the task.</b> The endpoint is guarded by the READ permission, not
/// by assignment. A comment is a question as often as it is an answer ("why are we still waiting on this?"), and
/// the person asking is usually not the assignee — locking it to the holder would kill the point of having a
/// feed at all.</para>
///
/// <para><b>A closed task cannot be commented on, and the REFUSAL is here.</b> The composer is already hidden for
/// a terminal task, but this module has shipped three separate rules that existed only as a hidden control —
/// cancel authority, dependencies, subtasks — and each one had to be fixed after a caller posted straight to the
/// endpoint. Reading stays open: history is not sealed, it is finished.</para>
/// </summary>
public sealed class AddTaskCommentHandler : IRequestHandler<AddTaskCommentCommand, Response<Guid>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly ITaskCommentRepository _comments;
    private readonly ICurrentUserContext _currentUser;
    private readonly IUserDisplayNameResolver _displayNames;
    private readonly ITenantContext _tenantContext;
    private readonly ITaskWatcherRepository _watchers;
    private readonly ITaskNotificationService _notifications;
    private readonly ILogger<AddTaskCommentHandler> _logger;

    public AddTaskCommentHandler(
        ITaskItemRepository tasks,
        ITaskCommentRepository comments,
        ICurrentUserContext currentUser,
        IUserDisplayNameResolver displayNames,
        ITenantContext tenantContext,
        ITaskWatcherRepository watchers,
        ITaskNotificationService notifications,
        ILogger<AddTaskCommentHandler> logger)
    {
        _tasks = tasks;
        _comments = comments;
        _currentUser = currentUser;
        _displayNames = displayNames;
        _tenantContext = tenantContext;
        _watchers = watchers;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<Response<Guid>> Handle(AddTaskCommentCommand command, CancellationToken ct)
    {
        var text = command.Request.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text) || text.Length > TaskCommentLimits.MaxTextLength)
        {
            // One code for both: from the caller's side "say something" and "say less" are the same correction,
            // and the message the client shows names the limit.
            return Response<Guid>.Fail(
                $"A comment must be between 1 and {TaskCommentLimits.MaxTextLength} characters.",
                400, TaskReasonCodes.CommentTextInvalid, command.CorrelationId);
        }

        var task = await _tasks.GetByIdAsync(command.TaskItemId, ct);
        if (task is null)
        {
            return Response<Guid>.Fail("Task not found.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

        // 409, not 403: this is about the task's STATE, not about who is asking. Everyone who can see a closed
        // task is equally unable to comment on it.
        if (task.Lifecycle is TaskLifecycle.Done or TaskLifecycle.Cancelled)
        {
            return Response<Guid>.Fail(
                "This task is closed, so it can no longer be commented on.",
                409, TaskReasonCodes.CommentTaskClosed, command.CorrelationId);
        }

        // The author's name is COPIED, not referenced: the feed records who said it at the time, and a later
        // rename must not silently reattribute what was said. Best effort — an unresolved name stays null rather
        // than falling back to a GUID, and the client shows its own "name unavailable" label.
        var names = await _displayNames.ResolveAsync([_currentUser.UserId], ct);
        var authorName = names.TryGetValue(_currentUser.UserId, out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : null;

        var comment = await _comments.CreateAsync(
            new TaskComment
            {
                TenantId = _tenantContext.TenantId,
                TaskItemId = task.Id,
                Text = text,
                AuthorUserId = _currentUser.UserId,
                AuthorDisplayName = authorName,
                CreatedBy = _currentUser.ActorName
            },
            ct);

        await NotifyAsync(task, ct);

        return Response<Guid>.Success(comment.Id, 201, command.CorrelationId);
    }

    /// <summary>
    /// WHO HEARS ABOUT A NEW COMMENT: the holder, the requester, the watchers, and everyone who has already said
    /// something here. The writer is excluded — but not HERE: <c>ITaskNotificationService.NotifyAsync</c> owns the
    /// actor rule for every event in the module, and restating it at a call site is how one caller ends up with a
    /// different definition of "the actor".
    ///
    /// <para><b>Why previous commenters.</b> A conversation is the one audience the task's own fields cannot
    /// name: somebody who asked "why is this still waiting?" has declared an interest no assignment field
    /// records, and answering into silence is how a feed stops being used.</para>
    ///
    /// <para><b>The watchers are READ, not recomputed.</b> They reached the projection last round as
    /// <c>watchers: [{person, role}]</c>; the same repository answers here. A second derivation of "who is
    /// watching" would be a second answer waiting to disagree.</para>
    ///
    /// <para>The task's own preferences are honoured by the service (master switch, then the per-event list —
    /// where ABSENT means "never chosen, send everything" and EMPTY means "chose none"). Nothing about that is
    /// re-implemented here.</para>
    /// </summary>
    private async Task NotifyAsync(TaskItem task, CancellationToken ct)
    {
        var watchers = await _watchers.ListByTaskIdAsync(task.Id, ct);
        var conversation = await _comments.ListByTaskIdAsync(task.Id, ct);

        var audience = new[] { task.AssigneeUserId, task.CreatedByUserId }
            .Concat(watchers.Select(w => (Guid?)w.UserId))
            .Concat(conversation.Select(c => c.AuthorUserId))
            .Where(id => id is { } value && value != Guid.Empty)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        await TaskNotificationSafely.NotifyAsync(
            _notifications, _logger, task, TaskNotificationEvents.Commented,
            audience, _currentUser.UserId, ct);
    }
}

/*
 * ══ THE DECISION THAT CHANGED, AND WHAT REPLACED IT (2026-08-14) ═══════════════════════════════════════════════
 *
 * Comments were IMMUTABLE, and both the controller and the entity said so in as many words: "There is deliberately
 * no PUT and no DELETE… If retraction is ever needed it arrives as a 'withdrawn' MARK, never as a deletion."
 *
 * That reasoning was sound and is not being dismissed: editing a sentence somebody has already replied to can turn
 * their reply into nonsense, and in an ERP that is rewriting history.
 *
 * What changed is that the compromise the old text gestured at was actually built — THE TRAIL. Immutability was
 * protecting one property: nothing disappears or changes silently. An edit that says it was edited, and a
 * withdrawal that leaves a marker where the comment stood, both keep that property intact.
 *
 * Three rules hold the line:
 *   1. ONLY THE AUTHOR. No manager exception, no administrator override. Nobody asked for one, and an authority
 *      over other people's words is far easier to grant than to take back.
 *   2. WITHDRAWAL IS A TOMBSTONE. The row survives with its text cleared; the feed keeps saying somebody spoke
 *      here and took it back. There is still no hard delete anywhere in this module.
 *   3. NEITHER SENDS EMAIL. A typo correction does not earn anybody's inbox, and a retraction that shouted would
 *      be louder than the sentence it retracts.
 */

/// <summary>Rewrite one's own comment, leaving an <c>editedAt</c> stamp the feed shows.</summary>
public sealed class UpdateTaskCommentHandler : IRequestHandler<UpdateTaskCommentCommand, Response<NoContent>>
{
    private readonly ITaskCommentRepository _comments;
    private readonly ICurrentUserContext _currentUser;

    public UpdateTaskCommentHandler(ITaskCommentRepository comments, ICurrentUserContext currentUser)
    {
        _comments = comments;
        _currentUser = currentUser;
    }

    public async Task<Response<NoContent>> Handle(UpdateTaskCommentCommand command, CancellationToken ct)
    {
        var text = command.Request.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text) || text.Length > TaskCommentLimits.MaxTextLength)
        {
            return Response<NoContent>.Fail(
                $"A comment must be between 1 and {TaskCommentLimits.MaxTextLength} characters.",
                400, TaskReasonCodes.CommentTextInvalid, command.CorrelationId);
        }

        var comment = await TaskCommentAuthority.LoadOwnAsync(
            _comments, _currentUser, command.TaskItemId, command.CommentId, ct);
        if (comment.Refusal is { } refusal)
        {
            return Response<NoContent>.Fail(
                refusal.Message, refusal.Status, refusal.ReasonCode, command.CorrelationId);
        }

        comment.Value!.Text = text;
        // The INSTANT, not a flag: "edited" alone cannot answer "before or after I read it?", which is the only
        // question the mark exists to settle.
        comment.Value.EditedAt = DateTimeOffset.UtcNow;
        await _comments.UpdateAsync(comment.Value, ct);

        // No notification, deliberately — see the block above.
        return Response<NoContent>.Success(204, command.CorrelationId);
    }
}

/// <summary>Withdraw one's own comment: the text goes, the row and its marker stay.</summary>
public sealed class WithdrawTaskCommentHandler : IRequestHandler<WithdrawTaskCommentCommand, Response<NoContent>>
{
    private readonly ITaskCommentRepository _comments;
    private readonly ICurrentUserContext _currentUser;

    public WithdrawTaskCommentHandler(ITaskCommentRepository comments, ICurrentUserContext currentUser)
    {
        _comments = comments;
        _currentUser = currentUser;
    }

    public async Task<Response<NoContent>> Handle(WithdrawTaskCommentCommand command, CancellationToken ct)
    {
        var comment = await TaskCommentAuthority.LoadOwnAsync(
            _comments, _currentUser, command.TaskItemId, command.CommentId, ct);
        if (comment.Refusal is { } refusal)
        {
            return Response<NoContent>.Fail(
                refusal.Message, refusal.Status, refusal.ReasonCode, command.CorrelationId);
        }

        /*
         * THE TEXT IS CLEARED, not merely hidden from the projection. A withdrawn sentence that still sits in the
         * database is one query away from being read back, and "I deleted that" has to be true at rest as well as
         * on screen. What remains is the row, its author and its instant — which is the marker.
         */
        comment.Value!.Text = null;
        comment.Value.WithdrawnAt = DateTimeOffset.UtcNow;
        await _comments.UpdateAsync(comment.Value, ct);

        return Response<NoContent>.Success(204, command.CorrelationId);
    }
}

/// <summary>
/// The author check, written ONCE for both writes.
///
/// <para>Both handlers need the same four refusals in the same order, and a second copy is how two endpoints end
/// up disagreeing about who may withdraw a comment. It also keeps the "not yours" and "no such comment" answers
/// deliberately close together — see below for why they are not the same answer here.</para>
/// </summary>
internal static class TaskCommentAuthority
{
    internal sealed record Refusal(string Message, int Status, string ReasonCode);

    internal sealed record Loaded(TaskComment? Value, Refusal? Refusal);

    internal static async Task<Loaded> LoadOwnAsync(
        ITaskCommentRepository comments,
        ICurrentUserContext currentUser,
        Guid taskItemId,
        Guid commentId,
        CancellationToken ct)
    {
        var comment = await comments.GetByIdAsync(commentId, ct);

        // Not found, or found on a DIFFERENT task than the URL claims. The second check is not pedantry: without
        // it the task id in the route would be decorative, and a caller could act on any comment in the tenant by
        // pairing its id with a task they can see.
        if (comment is null || comment.TaskItemId != taskItemId)
        {
            return new Loaded(null, new Refusal("Comment not found.", 404, TaskReasonCodes.NotFound));
        }

        /*
         * SOMEBODY ELSE'S COMMENT ANSWERS 403, NOT 404 — the opposite of the personal-note rule two rounds ago,
         * and the difference is real. A private note's existence is itself private, so "not yours" would leak it.
         * A comment is already visible to everyone who can read the task: the reader can SEE it on screen, so
         * pretending it does not exist would be a confusing lie rather than a protective one. What is refused
         * here is the authority, and the honest code says so.
         */
        if (comment.AuthorUserId != currentUser.UserId)
        {
            return new Loaded(null, new Refusal(
                "Only the author may edit or withdraw a comment.", 403, TaskReasonCodes.CommentNotAuthor));
        }

        if (comment.WithdrawnAt is not null)
        {
            return new Loaded(null, new Refusal(
                "This comment has been withdrawn.", 409, TaskReasonCodes.CommentWithdrawn));
        }

        return new Loaded(comment, null);
    }
}
