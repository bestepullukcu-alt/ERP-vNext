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
    private readonly ITaskReadAccessPolicy _readAccess;
    private readonly ILogger<AddTaskCommentHandler> _logger;

    public AddTaskCommentHandler(
        ITaskItemRepository tasks,
        ITaskCommentRepository comments,
        ICurrentUserContext currentUser,
        IUserDisplayNameResolver displayNames,
        ITenantContext tenantContext,
        ITaskWatcherRepository watchers,
        ITaskNotificationService notifications,
        ITaskReadAccessPolicy readAccess,
        ILogger<AddTaskCommentHandler> logger)
    {
        _tasks = tasks;
        _comments = comments;
        _currentUser = currentUser;
        _displayNames = displayNames;
        _tenantContext = tenantContext;
        _watchers = watchers;
        _notifications = notifications;
        _readAccess = readAccess;
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

        var mentionValidation = await TaskMentionValidation.ValidateAsync(
            _readAccess, task, command.Request.MentionedUserIds, ct);
        if (mentionValidation.Refusal is { } mentionRefusal)
        {
            return Response<Guid>.Fail(
                mentionRefusal.Message, mentionRefusal.Status, mentionRefusal.ReasonCode, command.CorrelationId);
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
                MentionedUserIds = mentionValidation.Value,
                CreatedBy = _currentUser.ActorName
            },
            ct);

        // Mentioned people are excluded from the general "somebody commented" audience below and told through
        // their OWN event instead — a direct address is a stronger, more specific thing than "you're involved in
        // this conversation", and sending both would put two emails in one inbox for one comment.
        await NotifyAsync(task, mentionValidation.Value, ct);
        // The actor exclusion and the task's own opt-out live inside NotifyAsync/ITaskNotificationService — a
        // mention to the person who just wrote it produces zero notifications there, same as any other event.
        await TaskNotificationSafely.NotifyAsync(
            _notifications, _logger, task, TaskNotificationEvents.Mentioned,
            mentionValidation.Value, _currentUser.UserId, ct);

        return Response<Guid>.Success(comment.Id, 201, command.CorrelationId);
    }

    /// <summary>
    /// WHO HEARS ABOUT A NEW COMMENT: the holder, the requester, the watchers, and everyone who has already said
    /// something here. The writer is excluded — but not HERE: <c>ITaskNotificationService.NotifyAsync</c> owns the
    /// actor rule for every event in the module, and restating it at a call site is how one caller ends up with a
    /// different definition of "the actor". <paramref name="mentioned"/> is excluded too, for the same reason —
    /// see the call site.
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
    private async Task NotifyAsync(TaskItem task, IReadOnlyList<Guid> mentioned, CancellationToken ct)
    {
        var watchers = await _watchers.ListByTaskIdAsync(task.Id, ct);
        var conversation = await _comments.ListByTaskIdAsync(task.Id, ct);
        var mentionedSet = mentioned.ToHashSet();

        var audience = new[] { task.AssigneeUserId, task.CreatedByUserId }
            .Concat(watchers.Select(w => (Guid?)w.UserId))
            .Concat(conversation.Select(c => c.AuthorUserId))
            .Where(id => id is { } value && value != Guid.Empty && !mentionedSet.Contains(value))
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
    private readonly ITaskItemRepository _tasks;
    private readonly ITaskCommentRepository _comments;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITaskReadAccessPolicy _readAccess;
    private readonly ITaskNotificationService _notifications;
    private readonly ILogger<UpdateTaskCommentHandler> _logger;

    public UpdateTaskCommentHandler(
        ITaskItemRepository tasks,
        ITaskCommentRepository comments,
        ICurrentUserContext currentUser,
        ITaskReadAccessPolicy readAccess,
        ITaskNotificationService notifications,
        ILogger<UpdateTaskCommentHandler> logger)
    {
        _tasks = tasks;
        _comments = comments;
        _currentUser = currentUser;
        _readAccess = readAccess;
        _notifications = notifications;
        _logger = logger;
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

        var task = await _tasks.GetByIdAsync(command.TaskItemId, ct);
        if (task is null)
        {
            return Response<NoContent>.Fail("Task not found.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

        var mentionValidation = await TaskMentionValidation.ValidateAsync(
            _readAccess, task, command.Request.MentionedUserIds, ct);
        if (mentionValidation.Refusal is { } mentionRefusal)
        {
            return Response<NoContent>.Fail(
                mentionRefusal.Message, mentionRefusal.Status, mentionRefusal.ReasonCode, command.CorrelationId);
        }

        // WHO IS NEW, not who is named: an edit's request carries the FULL replacement set (record contract
        // above), and re-notifying everybody already named would turn a correction into a second round of the
        // same email — the one thing an edit must not do (see the immutability-replaced-by-a-trail note below).
        var previouslyMentioned = comment.Value!.MentionedUserIds.ToHashSet();
        var newlyMentioned = mentionValidation.Value.Where(id => !previouslyMentioned.Contains(id)).ToList();

        comment.Value.Text = text;
        comment.Value.MentionedUserIds = mentionValidation.Value;
        // The INSTANT, not a flag: "edited" alone cannot answer "before or after I read it?", which is the only
        // question the mark exists to settle.
        comment.Value.EditedAt = DateTimeOffset.UtcNow;
        await _comments.UpdateAsync(comment.Value, ct);

        // No notification for the EDIT itself, deliberately — see the block above. A newly-added mention is a
        // different event (K3): the person is being addressed for the first time, which earns exactly the
        // notification a brand-new mention would.
        if (newlyMentioned.Count > 0)
        {
            await TaskNotificationSafely.NotifyAsync(
                _notifications, _logger, task, TaskNotificationEvents.Mentioned,
                newlyMentioned, _currentUser.UserId, ct);
        }

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

/// <summary>
/// WP-PSS-MOD0024-TASK-MENTIONS-01 K2/K4 — the @mention checks both write handlers need, written ONCE. A comment
/// author picks names from the same candidate list the mention-candidates endpoint serves, so under normal use
/// this never refuses; it exists for the caller who posts straight to the endpoint with an id the picker never
/// offered — the same class of gap this module has closed three times already for cancel, dependencies and
/// subtasks.
/// </summary>
internal static class TaskMentionValidation
{
    internal sealed record Refusal(string Message, int Status, string ReasonCode);

    internal sealed record Validated(IReadOnlyList<Guid> Value, Refusal? Refusal);

    internal static async Task<Validated> ValidateAsync(
        ITaskReadAccessPolicy readAccess,
        TaskItem task,
        IReadOnlyList<Guid>? requested,
        CancellationToken ct)
    {
        var mentionedUserIds = (requested ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (mentionedUserIds.Count > TaskCommentLimits.MaxMentionsPerComment)
        {
            return new Validated([], new Refusal(
                $"A comment may @mention at most {TaskCommentLimits.MaxMentionsPerComment} people.",
                400, TaskReasonCodes.MentionLimitExceeded));
        }

        /*
         * WP-PSS-MOD0024-FOLLOWUPS-02 (BL-399) — the DATA legs (pool holders, watchers, the parent task) are
         * resolved ONCE for the whole comment, not once per mentioned person. `CanReadAsync` itself asks
         * `ResolveDataLegCandidatesAsync` first (that IS the refactor BL-399 asked for; see
         * TaskReadAccessPolicy), so a candidate this ONE call already admits never has to ask again — up to 10
         * mentions on one comment used to mean 10 repeats of the same pool/watcher/parent reads.
         *
         * The scope and ReadAll legs stay PER-CANDIDATE, deliberately (K2's own narrowing, unchanged by this
         * WP): they answer true only for the CURRENT caller, never for an arbitrary candidate, so they cannot be
         * folded into a task-wide cache — falling through to the full `CanReadAsync` for the rare candidate the
         * data legs do not already cover is one extra (redundant) resolution at most, not ten.
         */
        var dataLegCandidates = await readAccess.ResolveDataLegCandidatesAsync(task, ct);

        foreach (var candidateId in mentionedUserIds)
        {
            if (dataLegCandidates.Contains(candidateId))
            {
                continue;
            }

            if (!await readAccess.CanReadAsync(task, candidateId, ct))
            {
                // Refuses the WHOLE write rather than silently dropping the unreadable name — a mention that
                // silently failed to notify anyone would be indistinguishable from a mention that worked.
                return new Validated([], new Refusal(
                    "One of the people you mentioned cannot see this task. Add them as a watcher first.",
                    400, TaskReasonCodes.MentionNotVisible));
            }
        }

        return new Validated(mentionedUserIds, null);
    }
}
