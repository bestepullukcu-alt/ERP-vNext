using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModulePages.Queries;
using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ModulePages.Handlers.QueryHandlers;

public sealed class SearchModulePageDescriptorsQueryHandler
    : IRequestHandler<SearchModulePageDescriptorsQuery, Response<PagedResult<ModulePageDescriptorListItemDto>>>
{
    private readonly IModulePageDescriptorRepository _repository;

    public SearchModulePageDescriptorsQueryHandler(IModulePageDescriptorRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<PagedResult<ModulePageDescriptorListItemDto>>> Handle(SearchModulePageDescriptorsQuery request, CancellationToken ct)
    {
        var pageTypes = SplitValues(request.Filter.PageType)
            .Select(value => Enum.Parse<ModulePageType>(value, ignoreCase: false))
            .ToArray();

        var statuses = SplitValues(request.Filter.Status)
            .Select(value => Enum.Parse<ModulePageStatus>(value, ignoreCase: false))
            .ToArray();

        var query = new ModulePageDescriptorQuery(
            request.Filter.Search,
            string.IsNullOrWhiteSpace(request.Filter.ModuleCode) ? null : ModulePageDescriptorNormalizer.NormalizeModuleCode(request.Filter.ModuleCode),
            pageTypes.Length > 0 ? pageTypes : null,
            statuses.Length > 0 ? statuses : null,
            request.Filter.Page,
            request.Filter.PageSize,
            request.Filter.Sort);

        var (items, totalCount) = await _repository.SearchAsync(query, ct);
        return Response<PagedResult<ModulePageDescriptorListItemDto>>.Success(
            ModulePageDescriptorMapper.ToPagedResult(items, request.Filter.Page, request.Filter.PageSize, totalCount));
    }

    private static IReadOnlyList<string> SplitValues(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
