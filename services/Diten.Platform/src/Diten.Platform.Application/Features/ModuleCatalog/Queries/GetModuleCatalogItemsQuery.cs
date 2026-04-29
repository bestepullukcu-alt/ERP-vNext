using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Queries;

public sealed record GetModuleCatalogItemsQuery(ModuleCatalogFilterRequest Filter)
    : IRequest<Response<PagedResult<ModuleCatalogListItemDto>>>;
