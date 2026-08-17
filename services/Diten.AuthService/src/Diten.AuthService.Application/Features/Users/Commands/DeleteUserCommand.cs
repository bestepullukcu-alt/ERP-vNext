using Diten.AuthService.Application.Common;
using MediatR;

namespace Diten.AuthService.Application.Features.Users.Commands;

public sealed record DeleteUserCommand(Guid Id) : IRequest<Response<NoContent>>;
