using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Features.Users.Models;
using MediatR;

namespace Diten.AuthService.Application.Features.Users.Queries;

/// <summary>
/// The Users list. <see cref="List"/> null = the legacy <c>page/pageSize</c> call (paged, unfiltered, unchanged);
/// set = the server-side list contract of WP-AUTH-USERS-LIST-QUERY-01 (search, order, filters, summary).
/// </summary>
public sealed record GetAllUsersQuery(
    int Page = 1,
    int PageSize = 20,
    UserListRequest? List = null
) : IRequest<Response<UserListResult>>;
