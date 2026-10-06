using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Features.Users.Models;
using MediatR;

namespace Diten.AuthService.Application.Features.Users.Queries;

/// <summary>
/// The Users list. <see cref="List"/> null = the legacy <c>page/pageSize</c> call (paged, unfiltered, unchanged);
/// set = the server-side list contract of WP-AUTH-USERS-LIST-QUERY-01 (search, order, filters, summary).
/// <para><see cref="ExportRowCap"/> set = the export of that same list (BL-452): the same criteria from the first row, at
/// most that many rows, no summary. The export is this query — not a second one that could disagree with the screen.</para>
/// </summary>
public sealed record GetAllUsersQuery(
    int Page = 1,
    int PageSize = 20,
    UserListRequest? List = null,
    int? ExportRowCap = null
) : IRequest<Response<UserListResult>>;
