using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleCatalog.Queries;
using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.QueryHandlers;

public sealed class GetModuleCatalogItemsQueryHandler
    : IRequestHandler<GetModuleCatalogItemsQuery, Response<PagedResult<ModuleCatalogListItemDto>>>
{
    private readonly IModuleCatalogRepository _repository;

    public GetModuleCatalogItemsQueryHandler(IModuleCatalogRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<PagedResult<ModuleCatalogListItemDto>>> Handle(GetModuleCatalogItemsQuery request, CancellationToken ct)
    {
        var statuses = SplitValues(request.Filter.Status)
            .Select(value => Enum.Parse<ModuleCatalogStatus>(value, ignoreCase: false))
            .ToArray();

        var query = new ModuleCatalogQuery(
            request.Filter.Search,
            request.Filter.Domain,
            request.Filter.Service,
            request.Filter.Category,
            statuses.Length > 0 ? statuses : null,
            request.Filter.IsCoreModule,
            request.Filter.IsTenantAssignable,
            request.Filter.Page,
            request.Filter.PageSize,
            request.Filter.Sort);

        var (items, totalCount) = await _repository.QueryAsync(query, ct);
        return Response<PagedResult<ModuleCatalogListItemDto>>.Success(
            ModuleCatalogMapper.ToPagedResult(items, request.Filter.Page, request.Filter.PageSize, totalCount));
    }

    private static IReadOnlyList<string> SplitValues(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
