using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;

/// <summary>MOD-0357 S4 — see <see cref="GetTaskLinkCandidatesQuery"/>'s own doc comment. Reads the same
/// tenant-scan repository method <c>GetAllForTenantAsync</c> already uses elsewhere in this feature, with the
/// SAME accepted scale caveat that method's own doc comment states — this is a typeahead, not a report.</summary>
public sealed class GetTaskLinkCandidatesHandler
    : IRequestHandler<GetTaskLinkCandidatesQuery, Response<IReadOnlyList<TaskLinkCandidateDto>>>
{
    private readonly ITaskItemRepository _tasks;

    public GetTaskLinkCandidatesHandler(ITaskItemRepository tasks) => _tasks = tasks;

    public async Task<Response<IReadOnlyList<TaskLinkCandidateDto>>> Handle(
        GetTaskLinkCandidatesQuery query, CancellationToken ct)
    {
        var limit = query.Limit is > 0 and <= 20 ? query.Limit : 20;
        var term = query.Term?.Trim();

        var all = await _tasks.GetAllForTenantAsync(ct);
        var candidates = all
            .Where(task => task.Lifecycle != TaskLifecycle.Cancelled)
            .Where(task => string.IsNullOrWhiteSpace(term)
                           || task.Title.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderBy(task => task.Title, StringComparer.Ordinal)
            .Take(limit)
            .Select(task => new TaskLinkCandidateDto(task.Id, task.Title))
            .ToList();

        return Response<IReadOnlyList<TaskLinkCandidateDto>>.Success(candidates, 200, query.CorrelationId);
    }
}
