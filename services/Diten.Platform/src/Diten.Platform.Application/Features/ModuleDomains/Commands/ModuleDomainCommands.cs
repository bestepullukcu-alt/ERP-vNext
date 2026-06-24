using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleDomains.Commands;

public sealed record CreateModuleDomainCommand(CreateModuleDomainRequest Request) : IRequest<Response<Guid>>;

public sealed record UpdateModuleDomainCommand(Guid Id, UpdateModuleDomainRequest Request) : IRequest<Response<NoContent>>;

public sealed record DeleteModuleDomainCommand(Guid Id) : IRequest<Response<NoContent>>;
