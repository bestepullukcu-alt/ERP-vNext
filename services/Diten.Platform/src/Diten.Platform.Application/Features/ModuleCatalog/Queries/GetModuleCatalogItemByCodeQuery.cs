using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Queries;

public sealed record GetModuleCatalogItemByCodeQuery(string ModuleCode) : IRequest<Response<ModuleCatalogItemDto>>;
