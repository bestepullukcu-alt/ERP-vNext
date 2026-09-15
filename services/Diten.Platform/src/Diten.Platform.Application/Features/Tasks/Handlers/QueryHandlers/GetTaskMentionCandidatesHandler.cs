using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;

/// <summary>
/// WP-PSS-MOD0024-TASK-MENTIONS-01 K2 — who the comment box's @ picker may offer for THIS task.
///
/// <para>The caller must be able to read the task at all before being told who else can — the same 404 parity
/// <c>GetTaskItemByIdHandler</c> uses (BL-349): a task the caller has no relationship to must not be
/// distinguishable from one that does not exist.</para>
/// </summary>
public sealed class GetTaskMentionCandidatesHandler
    : IRequestHandler<GetTaskMentionCandidatesQuery, Response<IReadOnlyList<TaskMentionCandidateDto>>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly ITaskReadAccessPolicy _readAccess;
    private readonly IUserDisplayNameResolver _displayNames;
    private readonly ICurrentUserContext _currentUser;

    public GetTaskMentionCandidatesHandler(
        ITaskItemRepository tasks,
        ITaskReadAccessPolicy readAccess,
        IUserDisplayNameResolver displayNames,
        ICurrentUserContext currentUser)
    {
        _tasks = tasks;
        _readAccess = readAccess;
        _displayNames = displayNames;
        _currentUser = currentUser;
    }

    public async Task<Response<IReadOnlyList<TaskMentionCandidateDto>>> Handle(
        GetTaskMentionCandidatesQuery request, CancellationToken ct)
    {
        var task = await _tasks.GetByIdAsync(request.TaskItemId, ct);
        if (task is null)
        {
            return Response<IReadOnlyList<TaskMentionCandidateDto>>.Fail(
                "Task not found.", 404, TaskReasonCodes.NotFound, request.CorrelationId);
        }

        if (!await _readAccess.CanReadAsync(task, _currentUser.UserId, ct))
        {
            // Same byte-identical refusal GetTaskItemByIdHandler gives a caller with no relationship to the
            // task — a real task must not be distinguishable from one that does not exist (BL-349).
            return Response<IReadOnlyList<TaskMentionCandidateDto>>.Fail(
                "Task not found.", 404, TaskReasonCodes.NotFound, request.CorrelationId);
        }

        var candidateIds = await _readAccess.ResolveDataLegCandidatesAsync(task, ct);
        var names = candidateIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _displayNames.ResolveAsync(candidateIds, ct);
        var search = request.SearchText?.Trim();

        IReadOnlyList<TaskMentionCandidateDto> candidates = candidateIds
            // An id whose name cannot be resolved is omitted, never shown as a raw GUID (IUserDisplayNameResolver
            // contract) — a picker row with no label is not a usable row.
            .Where(id => names.TryGetValue(id, out var name) && !string.IsNullOrWhiteSpace(name))
            .Select(id => new TaskMentionCandidateDto(id, names[id]))
            .Where(candidate => string.IsNullOrEmpty(search)
                || candidate.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(candidate => candidate.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Response<IReadOnlyList<TaskMentionCandidateDto>>.Success(
            candidates, correlationId: request.CorrelationId);
    }
}
