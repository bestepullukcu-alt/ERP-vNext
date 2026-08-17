using Diten.AuthService.Application.Common;
using MediatR;

namespace Diten.AuthService.Application.Features.Roles.Commands;

public sealed record RevokePermissionCommand(
    Guid RoleId,
    Guid PermissionId
) : IRequest<Response<NoContent>>;
