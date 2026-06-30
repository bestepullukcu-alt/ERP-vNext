using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleCatalog.Queries;
using Diten.Platform.Common.Catalog;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.QueryHandlers;

public sealed class GetAssignableModuleCatalogItemsQueryHandler
    : IRequestHandler<GetAssignableModuleCatalogItemsQuery, Response<IReadOnlyList<ModuleCatalogListItemDto>>>
{
    private readonly IPlatformCatalogContract _catalogContract;

    public GetAssignableModuleCatalogItemsQueryHandler(IPlatformCatalogContract catalogContract)
    {
        _catalogContract = catalogContract;
    }

    public async Task<Response<IReadOnlyList<ModuleCatalogListItemDto>>> Handle(GetAssignableModuleCatalogItemsQuery request, CancellationToken ct)
    {
        var items = await _catalogContract.GetAssignableModulesAsync(ct);
        var rows = items
            .Select(x => new ModuleCatalogListItemDto(
                x.Id,
                x.ModuleCode,
                x.ModuleName,
                x.DisplayName,
                x.Domain,
                x.Service,
                x.Status,
                x.ModuleVersion,
                x.IsCoreModule,
                x.IsTenantAssignable,
                x.SortOrder,
                Origin: string.Empty, // not applicable to the assignable-modules feed (origin is a catalog-admin concern)
                x.CreatedAt,
                x.UpdatedAt))
            .ToList();
        return Response<IReadOnlyList<ModuleCatalogListItemDto>>.Success(rows);
    }
}
