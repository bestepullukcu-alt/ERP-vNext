using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Application.Features.Tenants.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

public sealed class GetTenantUsersSummaryQueryHandler : IRequestHandler<GetTenantUsersSummaryQuery, TenantUsersSummaryDto?>
{
    private readonly ITenantRegistryRepository _repository;

    public GetTenantUsersSummaryQueryHandler(ITenantRegistryRepository repository)
    {
        _repository = repository;
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
            await _repository.UpdateAsync(tenant, cancellationToken);
        }

        return new TenantUsersSummaryDto(
            tenant.Id,
            tenant.AdminUsers.Count,
            tenant.AdminUsers.Count(user => user.Status == Domain.Entities.TenantAdminUserStatus.Active),
            tenant.AdminUsers.Count(user => user.Status == Domain.Entities.TenantAdminUserStatus.PendingInvitation),
            "AdminApprovalRequired");
    }
}
