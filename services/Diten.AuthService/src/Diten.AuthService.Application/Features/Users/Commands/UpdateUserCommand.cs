using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.DTOs;
using MediatR;

namespace Diten.AuthService.Application.Features.Users.Commands;

// WP-AUTH-USER-KIND-UPDATE-01 — AccountKind is optional and is the enum NAME (Unknown | Human | Service).
// Omitted ⇒ the kind is not touched. Supplied AND different from the current kind ⇒ the caller must hold
// auth.users.account-kind.manage, otherwise 403 PERM_DENIED and nothing is written — the same rule as create.
// CallerCanManageAccountKind is NOT caller-supplied data: the controller sets it from the authenticated principal's
// permission claims. CorrelationId is the request's, carried into the audit row (set by the controller).
public sealed record UpdateUserCommand(
    Guid Id,
    string FirstName,
    string LastName,
    bool IsActive,
    string? AccountKind = null,
    bool CallerCanManageAccountKind = false,
    string? CorrelationId = null
) : IRequest<Response<UserDto>>;
