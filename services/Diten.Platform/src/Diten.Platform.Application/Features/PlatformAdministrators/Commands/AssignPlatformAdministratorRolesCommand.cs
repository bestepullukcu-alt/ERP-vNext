using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PlatformAdministrators.Commands;

public sealed record AssignPlatformAdministratorRolesCommand(Guid Id, AssignPlatformAdministratorRolesRequest Request) : IRequest<Response<NoContent>>;
