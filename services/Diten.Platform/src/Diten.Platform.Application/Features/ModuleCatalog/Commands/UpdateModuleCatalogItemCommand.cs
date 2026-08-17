using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Commands;

public sealed record UpdateModuleCatalogItemCommand(Guid Id, UpdateModuleCatalogItemRequest Request) : IRequest<Response<NoContent>>;
