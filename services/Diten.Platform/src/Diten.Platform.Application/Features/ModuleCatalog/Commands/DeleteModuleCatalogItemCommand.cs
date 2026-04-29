using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Commands;

public sealed record DeleteModuleCatalogItemCommand(Guid Id) : IRequest<Response<NoContent>>;
