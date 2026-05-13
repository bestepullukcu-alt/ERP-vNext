using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PlatformAdministrators.Commands;

public sealed record DeletePlatformAdministratorCommand(Guid Id, PlatformAdministratorVersionRequest Request) : IRequest<Response<NoContent>>;
