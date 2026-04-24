using MediatR;

namespace Diten.AuthService.Application.Features.Auth.Commands;

public sealed record LogoutCommand(
    string RefreshToken
) : IRequest<Unit>;
