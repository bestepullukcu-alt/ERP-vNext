using MediatR;

namespace Diten.AuthService.Application.Features.Permissions.Commands;

public sealed record DeletePermissionCommand(Guid Id) : IRequest<Unit>;
