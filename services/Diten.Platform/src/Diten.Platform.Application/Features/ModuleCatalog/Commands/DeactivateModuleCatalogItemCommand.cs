using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Commands;

public sealed record DeactivateModuleCatalogItemCommand(Guid Id) : IRequest<Response<NoContent>>;
