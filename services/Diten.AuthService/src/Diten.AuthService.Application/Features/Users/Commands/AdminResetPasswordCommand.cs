using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.DTOs;
using MediatR;

namespace Diten.AuthService.Application.Features.Users.Commands;

// Admin-initiated password reset for an ALREADY-ACTIVE user: issues a fresh set-password token,
// forces a password change, and emails the set-password link. (Pending invites use Resend instead.)
// Returns the dev-only set-password link (null in prod).
public sealed record AdminResetPasswordCommand(
    Guid UserId
) : IRequest<Response<InviteLinkResult>>;
