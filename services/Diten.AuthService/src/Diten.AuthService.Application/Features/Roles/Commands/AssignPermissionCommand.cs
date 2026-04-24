using MediatR;

namespace Diten.AuthService.Application.Features.Roles.Commands;

public sealed record AssignPermissionCommand(
    Guid RoleId,
    Guid PermissionId
) : IRequest<Unit>;
