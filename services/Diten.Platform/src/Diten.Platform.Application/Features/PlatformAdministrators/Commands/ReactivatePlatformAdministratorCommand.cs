using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PlatformAdministrators.Commands;

public sealed record ReactivatePlatformAdministratorCommand(Guid Id, PlatformAdministratorStatusRequest Request) : IRequest<Response<NoContent>>;
