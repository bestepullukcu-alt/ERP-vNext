using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Commands;

public sealed record CreateModuleCatalogItemCommand(CreateModuleCatalogItemRequest Request) : IRequest<Response<Guid>>;
