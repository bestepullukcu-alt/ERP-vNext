using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Queries;

public sealed record GetModuleCatalogItemByIdQuery(Guid Id) : IRequest<Response<ModuleCatalogItemDto>>;
