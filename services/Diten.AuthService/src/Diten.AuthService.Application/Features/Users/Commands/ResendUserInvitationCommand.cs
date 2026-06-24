using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.DTOs;
using MediatR;

namespace Diten.AuthService.Application.Features.Users.Commands;

// Authed, tenant-scoped: re-issue a fresh 7-day set-password token for a pending invited user and
// re-send the invitation email. Returns the dev-only set-password link (null in prod).
public sealed record ResendUserInvitationCommand(
    Guid UserId
) : IRequest<Response<InviteLinkResult>>;
