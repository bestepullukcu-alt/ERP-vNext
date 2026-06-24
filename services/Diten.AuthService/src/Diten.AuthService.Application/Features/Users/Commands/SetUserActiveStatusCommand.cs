using Diten.AuthService.Application.Common;
using MediatR;

namespace Diten.AuthService.Application.Features.Users.Commands;

// Admin enable/disable. Disabling also revokes the user's refresh tokens so active sessions die.
public sealed record SetUserActiveStatusCommand(
    Guid Id,
    bool IsActive
) : IRequest<Response<NoContent>>;
