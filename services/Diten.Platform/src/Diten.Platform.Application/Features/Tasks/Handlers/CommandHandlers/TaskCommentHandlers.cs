using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using MediatR;

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

    public AddTaskCommentHandler(
        ITaskItemRepository tasks,
        ITaskCommentRepository comments,
        ICurrentUserContext currentUser,
        IUserDisplayNameResolver displayNames,
        ITenantContext tenantContext)
    {
        _tasks = tasks;
        _comments = comments;
        _currentUser = currentUser;
        _displayNames = displayNames;
        _tenantContext = tenantContext;
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

        return Response<Guid>.Success(comment.Id, 201, command.CorrelationId);
    }
}
