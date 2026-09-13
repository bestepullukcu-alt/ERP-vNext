using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.DTOs;
using MediatR;

namespace Diten.AuthService.Application.Features.Users.Commands;

// Password is optional. When supplied the user is created active via the self-service path.
// When omitted (null/empty) the user is created as an INVITATION: inactive, must-change-password,
// with a set-password token, and a "set your password" email link is sent.
//
// WP-INFRA-AUTH-ACCOUNT-KIND-01 — AccountKind is optional and is the enum NAME. Empty ⇒ the account is created
// Unknown. Supplied ⇒ the caller must hold auth.users.account-kind.manage (explicit-grant-only; separate from
// auth.users.create), otherwise 403 PERM_DENIED. CallerCanManageAccountKind is NOT caller-supplied data: the
// controller sets it from the authenticated principal's permission claims.
public sealed record CreateUserCommand(
    string Email,
    string? Password,
    string FirstName,
    string LastName,
    string? AccountKind = null,
    bool CallerCanManageAccountKind = false
) : IRequest<Response<UserDto>>;
