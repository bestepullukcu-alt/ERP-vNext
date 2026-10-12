using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Application.Features.Tenants.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

public sealed class GetTenantUsersSummaryQueryHandler : IRequestHandler<GetTenantUsersSummaryQuery, TenantUsersSummaryDto?>
{
    private readonly ITenantRegistryRepository _repository;
    private readonly ITenantUserCountReader _userCounts;

    public GetTenantUsersSummaryQueryHandler(ITenantRegistryRepository repository, ITenantUserCountReader userCounts)
    {
        _repository = repository;
        _userCounts = userCounts;
    }

    public async Task<TenantUsersSummaryDto?> Handle(GetTenantUsersSummaryQuery request, CancellationToken cancellationToken)
    {
        var tenant = await _repository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant == null)
        {
            return null;
        }

        var changed = TenantAdminUserSupport.EnsureInitialAdminUser(tenant);
        if (changed)
        {
            tenant.ActiveUserCount = TenantAdminUserSupport.CountUsersQuotaUsage(tenant);
            await _repository.UpdateAsync(tenant, cancellationToken);
        }

        // BL-459 — the tenant's users live in AuthService: count them there (every user the tenant has, by the status the
        // Users screen shows). AdminUsers is only the invited-administrator list; it stays the fallback when AuthService
        // cannot answer, so the page still renders.
        var counts = await _userCounts.GetCountsAsync(tenant.Id, cancellationToken);
        if (counts is not null)
        {
            return new TenantUsersSummaryDto(
                tenant.Id,
                (int)counts.Total,
                (int)counts.Active,
                (int)counts.Invited,
                "AdminApprovalRequired");
        }

        return new TenantUsersSummaryDto(
            tenant.Id,
            tenant.AdminUsers.Count,
            TenantAdminUserSupport.CountUsersQuotaUsage(tenant),
            tenant.AdminUsers.Count(user => user.Status == Domain.Entities.TenantAdminUserStatus.PendingInvitation),
            "AdminApprovalRequired");
    }
}
