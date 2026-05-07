using Diten.AuthService.Application.Common;
using MediatR;

namespace Diten.AuthService.Application.Features.Permissions.Commands;

public sealed record DeletePermissionCommand(Guid Id) : IRequest<Response<NoContent>>;
