using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Application.Features.Tenants.Queries;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

public sealed class GetTenantStatusQueryHandler : IRequestHandler<GetTenantStatusQuery, TenantStatusDto>
{
    private readonly ITenantRegistryRepository _tenantRepository;

    public GetTenantStatusQueryHandler(ITenantRegistryRepository tenantRepository)
    {
        _tenantRepository = tenantRepository;
    }

    public async Task<TenantStatusDto> Handle(GetTenantStatusQuery request, CancellationToken cancellationToken)
    {
        // GetByIdAsync filters soft-deleted rows out, so a deleted (or never-existing) tenant resolves to null →
        // Exists=false. Only TenantStatus.Active is treated as live; Provisioning/Suspended/Deactivated are not.
        var tenant = await _tenantRepository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null)
        {
            return new TenantStatusDto(false, false, "NotFound");
        }

        return new TenantStatusDto(true, tenant.Status == TenantStatus.Active, tenant.Status.ToString());
    }
}
