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
/// <para><b>Only tasks the caller may read</b> (WP-WCN-TASK-REQUEST-01 FIX3, item 4). The picker used to list the id and
/// title of EVERY task in the tenant to anybody holding <c>tasks.read</c> — a pending cross-company request
/// included — which is also where the ids every other write was probed with came from (BL-525). MOD-0024's read rule,
/// batched (<see cref="ITaskReadAccessPolicy.ReadableTaskIdsAsync"/>), is asked of the matches BEFORE the limit, so a
/// page of unreadable matches never crowds out the readable ones.</para></summary>
public sealed class GetTaskLinkCandidatesHandler
    : IRequestHandler<GetTaskLinkCandidatesQuery, Response<IReadOnlyList<TaskLinkCandidateDto>>>
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

    public async Task<Response<IReadOnlyList<TaskLinkCandidateDto>>> Handle(
        GetTaskLinkCandidatesQuery query, CancellationToken ct)
    {
        var limit = query.Limit is > 0 and <= 20 ? query.Limit : 20;
        var term = query.Term?.Trim();

        var all = await _tasks.GetAllForTenantAsync(ct);
        var matches = all
            .Where(task => task.Lifecycle != TaskLifecycle.Cancelled)
            .Where(task => string.IsNullOrWhiteSpace(term)
                           || task.Title.Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var readable = await _readAccess.ReadableTaskIdsAsync(matches, _currentUser.UserId, ct);

        var candidates = matches
            .Where(task => readable.Contains(task.Id))
            .OrderBy(task => task.Title, StringComparer.Ordinal)
            .Take(limit)
            .Select(task => new TaskLinkCandidateDto(task.Id, task.Title))
            .ToList();

        return Response<IReadOnlyList<TaskLinkCandidateDto>>.Success(candidates, 200, query.CorrelationId);
    }
}
