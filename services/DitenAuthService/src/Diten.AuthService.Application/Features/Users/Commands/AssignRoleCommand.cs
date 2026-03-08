using MediatR;

namespace Diten.AuthService.Application.Features.Users.Commands;

public sealed record AssignRoleCommand(
    Guid UserId,
    Guid RoleId
) : IRequest<Unit>;
