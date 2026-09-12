using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.DTOs;
using MediatR;

namespace Diten.AuthService.Application.Features.Users.Queries;

/// <summary>
/// WP-INFRA-AUTH-ACCOUNT-KIND-01 — name search over the caller's tenant's ACTIVE, non-deleted users, for a reference
/// picker. Guarded by <c>auth.users.lookup</c> (an ordinary tenant key). Returns id + display label only.
/// </summary>
/// <param name="Search">Optional name fragment (≤ 100 chars); empty lists the first <paramref name="Limit"/> users.</param>
/// <param name="Limit">1..50; the controller defaults to 20.</param>
public sealed record LookupUsersQuery(string? Search, int Limit = 20)
    : IRequest<Response<IReadOnlyList<UserLookupItemDto>>>;
