using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleServices.Commands;

public sealed record CreateModuleServiceCommand(CreateModuleServiceRequest Request) : IRequest<Response<Guid>>;

public sealed record UpdateModuleServiceCommand(Guid Id, UpdateModuleServiceRequest Request) : IRequest<Response<NoContent>>;

public sealed record DeleteModuleServiceCommand(Guid Id) : IRequest<Response<NoContent>>;
