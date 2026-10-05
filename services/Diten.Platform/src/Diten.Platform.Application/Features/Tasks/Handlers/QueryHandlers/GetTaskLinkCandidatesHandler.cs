using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;

/// <summary>MOD-0357 S4 — see <see cref="GetTaskLinkCandidatesQuery"/>'s own doc comment. Reads the same
/// tenant-scan repository method <c>GetAllForTenantAsync</c> already uses elsewhere in this feature, with the
/// SAME accepted scale caveat that method's own doc comment states — this is a typeahead, not a report.
///
/// <para><b>Only tasks the caller may read</b> (WP-WCN-TASK-REQUEST-01 FIX3, item 4). MOD-0024's read rule, batched
/// (<see cref="ITaskReadAccessPolicy.ReadableTaskIdsAsync"/>), is asked of the matches BEFORE the limit.</para>
///
/// <para><b>In chunks, newest first, with a ceiling</b> (FIX5, item 2). The matches are put to the rule
/// <see cref="ChunkSize"/> at a time until <c>limit</c> readable ones are found or <see cref="MaxChunks"/> chunks were
/// judged — so a crowd of newer unreadable tasks does not hide older readable ones (FIX4's single chunk did), and the
/// cost no longer grows with the whole tenant. When the ceiling stops the search with matches left unjudged, the answer
/// says so (<see cref="TaskLinkCandidatesDto.Truncated"/>) and the picker asks the reader to narrow the search.</para></summary>
public sealed class GetTaskLinkCandidatesHandler
    : IRequestHandler<GetTaskLinkCandidatesQuery, Response<TaskLinkCandidatesDto>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly ITaskReadAccessPolicy _readAccess;
    private readonly ICurrentUserContext _currentUser;

    public GetTaskLinkCandidatesHandler(
        ITaskItemRepository tasks, ITaskReadAccessPolicy readAccess, ICurrentUserContext currentUser)
    {
        _tasks = tasks;
        _readAccess = readAccess;
        _currentUser = currentUser;
    }

    /// <summary>How many matches are put to the read rule at once.</summary>
    internal const int ChunkSize = 200;

    /// <summary>How many chunks at most: past this, the answer is <see cref="TaskLinkCandidatesDto.Truncated"/>.</summary>
    internal const int MaxChunks = 5;

    public async Task<Response<TaskLinkCandidatesDto>> Handle(
        GetTaskLinkCandidatesQuery query, CancellationToken ct)
    {
        var limit = query.Limit is > 0 and <= 20 ? query.Limit : 20;
        var term = query.Term?.Trim();

        var all = await _tasks.GetAllForTenantAsync(ct);
        var matches = all
            .Where(task => task.Lifecycle != TaskLifecycle.Cancelled)
            .Where(task => string.IsNullOrWhiteSpace(term)
                           || task.Title.Contains(term, StringComparison.OrdinalIgnoreCase))
            // Newest first; equal instants by id, so which tasks fall into which chunk never depends on store order.
            .OrderByDescending(task => task.CreatedAt)
            .ThenBy(task => task.Id)
            .ToList();

        var found = new List<Domain.Entities.Tasks.TaskItem>();
        var judged = 0;
        for (var chunk = 0; chunk < MaxChunks && judged < matches.Count && found.Count < limit; chunk++)
        {
            var batch = matches.Skip(judged).Take(ChunkSize).ToList();
            judged += batch.Count;
            var readable = await _readAccess.ReadableTaskIdsAsync(batch, _currentUser.UserId, ct);
            found.AddRange(batch.Where(task => readable.Contains(task.Id)));
        }

        var truncated = found.Count < limit && judged < matches.Count;
        var candidates = found
            .OrderBy(task => task.Title, StringComparer.Ordinal)
            .Take(limit)
            .Select(task => new TaskLinkCandidateDto(task.Id, task.Title))
            .ToList();

        return Response<TaskLinkCandidatesDto>.Success(
            new TaskLinkCandidatesDto(candidates, truncated), 200, query.CorrelationId);
    }
}
