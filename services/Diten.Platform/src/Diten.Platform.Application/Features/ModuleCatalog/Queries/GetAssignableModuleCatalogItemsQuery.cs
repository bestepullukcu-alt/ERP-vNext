using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Queries;

public sealed record GetAssignableModuleCatalogItemsQuery() : IRequest<Response<IReadOnlyList<ModuleCatalogListItemDto>>>;
