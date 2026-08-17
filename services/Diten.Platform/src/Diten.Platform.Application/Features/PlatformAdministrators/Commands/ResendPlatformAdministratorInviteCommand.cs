using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PlatformAdministrators.Commands;

public sealed record ResendPlatformAdministratorInviteCommand(Guid Id, PlatformAdministratorVersionRequest Request) : IRequest<Response<PlatformAdministratorInviteResultDto>>;
