using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Users.Models;
using Diten.AuthService.Application.Features.Users.Queries;
using Diten.AuthService.Application.Features.Users.Services;
using MediatR;

namespace Diten.AuthService.Application.Features.Users.Handlers.QueryHandlers;

public sealed class GetAllUsersQueryHandler : IRequestHandler<GetAllUsersQuery, Response<UserListResult>>
{
    private readonly IUserListReader _reader;
    private readonly ITenantContext _tenantContext;

    public GetAllUsersQueryHandler(IUserListReader reader, ITenantContext tenantContext)
    {
        _reader = reader;
        _tenantContext = tenantContext;
    }

    public async Task<Response<UserListResult>> Handle(GetAllUsersQuery request, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId;

        UserListCriteria criteria;
        if (request.List is null)
        {
            criteria = UserListRules.Legacy(request.Page, request.PageSize);
        }
        else
        {
            if (!UserListRules.TryBuild(request.List, out criteria, out var failure))
            {
                return failure!;
            }

            // The role filter is resolved to a user-id set first; the users query then restricts to it (`_id in`).
            if (request.List.RoleId is { Count: > 0 } roleIds)
            {
                var holders = await _reader.GetUserIdsHoldingAnyRoleAsync(tenantId, roleIds, ct);
                criteria = criteria with { RestrictToUserIds = holders };
            }
        }

        var page = await _reader.SearchAsync(tenantId, criteria, ct);

        // ONE query for the roles of the whole page — the loop that used to ask per user is gone.
        var roles = await _reader.GetRoleNamesForUsersAsync(tenantId, page.Items.Select(u => u.Id).ToList(), ct);
        var items = page.Items
            .Select(user => new UserDto(user.Id, user.Email, user.FirstName, user.LastName, user.IsActive,
                roles.TryGetValue(user.Id, out var names) ? names : [], user.TenantId,
                user.LastLoginAt, user.FailedLoginAttempts, user.MustChangePassword, "TenantPolicy",
                AccountKind: user.AccountKind.ToString(), Status: UserLifecycle.StatusOf(user)))
            .ToList();

        if (request.List is null)
        {
            // Legacy: no filter, so what matched is the total. The summary was never part of this call.
            return Response<UserListResult>.Success(new UserListResult(items, page.FilteredTotal, page.FilteredTotal, null));
        }

        var summary = await _reader.GetSummaryAsync(tenantId, ct);
        return Response<UserListResult>.Success(new UserListResult(items, summary.Total, page.FilteredTotal, summary));
    }
}
