using MediatR;

namespace Diten.AuthService.Application.Features.Auth.Commands;

public sealed record ChangePasswordCommand(
    string UserId,
    string CurrentPassword,
    string NewPassword
) : IRequest<Unit>;
