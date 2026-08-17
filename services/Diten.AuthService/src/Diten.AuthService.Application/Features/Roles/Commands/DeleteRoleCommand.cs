using Diten.AuthService.Application.Common;
using MediatR;

namespace Diten.AuthService.Application.Features.Roles.Commands;

public sealed record DeleteRoleCommand(Guid Id) : IRequest<Response<NoContent>>;
