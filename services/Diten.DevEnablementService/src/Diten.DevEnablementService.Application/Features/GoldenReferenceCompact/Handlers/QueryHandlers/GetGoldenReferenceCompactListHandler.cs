using Diten.Shared.Core;
using Diten.DevEnablementService.Application.Features.GoldenReferenceCompact.Queries;
using Diten.DevEnablementService.Domain.Repositories;
using MediatR;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceCompact.Handlers.QueryHandlers;

public sealed class GetGoldenReferenceCompactListHandler : IRequestHandler<GetGoldenReferenceCompactListQuery, Response<GoldenReferenceCompactListPageDto>>
{
    private readonly IGoldenReferenceCompactRepository _repository;

    public GetGoldenReferenceCompactListHandler(IGoldenReferenceCompactRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<GoldenReferenceCompactListPageDto>> Handle(GetGoldenReferenceCompactListQuery request, CancellationToken cancellationToken)
    {
        // The validator already refused an unknown column; this is the same table, not a second opinion.
        if (!GoldenReferenceCompactListSort.TryResolve(request.OrderBy, out var sortField))
            return Response<GoldenReferenceCompactListPageDto>.Fail($"'orderBy' '{request.OrderBy}' is not a sortable column.", 400);

        var criteria = new GoldenReferenceCompactListCriteria(
            Start: request.Start ?? 0,
            Length: request.Length,
            Search: request.Search,
            SortField: sortField,
            Descending: string.Equals(request.OrderDir, "desc", StringComparison.OrdinalIgnoreCase),
            IsActive: request.Status?
                .Select(value => GoldenReferenceCompactListSort.TryStatus(value, out var active) ? (bool?)active : null)
                .Where(value => value.HasValue).Select(value => value!.Value).ToList(),
            ReferenceTypes: Clean(request.ReferenceType),
            Categories: Clean(request.Category),
            Owners: Clean(request.Owner),
            Priority: request.Priority);

        var page = await _repository.QueryAsync(criteria, cancellationToken);
        var items = page.Items.Select(x => new GoldenReferenceCompactListItemDto(
            x.Id,
            x.Code,
            x.Name,
            x.Description,
            x.ReferenceType,
            x.Category,
            x.GroupKey,
            x.SourceSystem,
            x.Owner,
            x.Version,
            x.EffectiveDate,
            x.ExpirationDate,
            x.Priority,
            x.IsActive)).ToList();
        return Response<GoldenReferenceCompactListPageDto>.Success(new GoldenReferenceCompactListPageDto(items, page.Total, page.FilteredTotal));
    }

    private static IReadOnlyList<string>? Clean(IReadOnlyList<string>? values) =>
        values?.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).Distinct().ToList();
}
