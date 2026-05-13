using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PlatformAdministrators.Commands;

public sealed record InvitePlatformAdministratorCommand(InvitePlatformAdministratorRequest Request) : IRequest<Response<PlatformAdministratorInviteResultDto>>;
