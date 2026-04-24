using MediatR;

namespace Diten.AuthService.Application.Features.Roles.Commands;

public sealed record DeleteRoleCommand(Guid Id) : IRequest<Unit>;
