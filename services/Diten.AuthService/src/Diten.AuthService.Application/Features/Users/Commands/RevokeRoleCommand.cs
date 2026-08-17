using Diten.AuthService.Application.Common;
using MediatR;

namespace Diten.AuthService.Application.Features.Users.Commands;

public sealed record RevokeRoleCommand(
    Guid UserId,
    Guid RoleId
) : IRequest<Response<NoContent>>;
