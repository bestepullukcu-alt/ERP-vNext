using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PlatformAdministrators.Commands;

public sealed record UpdatePlatformAdministratorCommand(Guid Id, UpdatePlatformAdministratorRequest Request) : IRequest<Response<NoContent>>;
