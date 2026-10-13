using Diten.AuthService.Application.Common;
using MediatR;

namespace Diten.AuthService.Application.Features.Users.Queries;

/// <summary>
/// WP-USERS-ROLES-CLOSE-FIX1 item 3 (KR4) — ANONYMOUS, READ-ONLY: what the set-password page should draw before anyone types a
/// password. 204 = the form; 409 <c>AUTH_ACCOUNT_DEACTIVATED</c> = the account was deactivated (the link is still its own);
/// 400 <c>AUTH_LINK_INVALID</c> = everything else. Exactly the answers the redemption gives for the same link
/// (<see cref="Services.TenantPasswordLink"/>); nothing is written.
/// </summary>
public sealed record CheckTenantPasswordLinkQuery(string? Email, string? Token) : IRequest<Response<NoContent>>;
