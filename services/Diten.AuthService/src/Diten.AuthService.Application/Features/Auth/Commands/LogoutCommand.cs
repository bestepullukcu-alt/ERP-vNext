using MediatR;

namespace Diten.AuthService.Application.Features.Auth.Commands;

public sealed record LogoutCommand(
    string AccessToken,
    string RefreshToken,
    string RequestIp
) : IRequest<Unit>;
