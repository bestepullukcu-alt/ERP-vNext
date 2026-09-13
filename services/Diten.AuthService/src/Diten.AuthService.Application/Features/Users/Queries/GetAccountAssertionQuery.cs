using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.DTOs;
using MediatR;

namespace Diten.AuthService.Application.Features.Users.Queries;

/// <summary>
/// WP-INFRA-AUTH-ACCOUNT-KIND-01 — "what does AuthService assert about this account": tenant membership (a user of
/// another tenant is a 404, byte-identical to a missing one), active flag and kind. Guarded by <c>auth.users.lookup</c>.
/// </summary>
public sealed record GetAccountAssertionQuery(Guid UserId) : IRequest<Response<AccountAssertionDto>>;
