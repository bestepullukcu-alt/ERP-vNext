using Diten.AuthService.Application.Common;
using MediatR;

namespace Diten.AuthService.Application.Features.Users.Commands;

// ANONYMOUS set-password (invitation redemption). No tenant context — the user is located by the
// hashed token, which uniquely identifies the user + tenant.
public sealed record SetTenantPasswordCommand(
    string Email,
    string Token,
    string NewPassword
) : IRequest<Response<NoContent>>;
