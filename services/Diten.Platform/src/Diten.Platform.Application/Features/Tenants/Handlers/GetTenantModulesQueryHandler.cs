using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Application.Features.Tenants.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

public sealed class GetTenantModulesQueryHandler : IRequestHandler<GetTenantModulesQuery, TenantModulesSummaryDto?>
{
    private readonly ITenantRegistryRepository _repository;

    public GetTenantModulesQueryHandler(ITenantRegistryRepository repository)
    {
        _repository = repository;
    }

    public async Task<TenantModulesSummaryDto?> Handle(GetTenantModulesQuery request, CancellationToken cancellationToken)
    {
        var tenant = await _repository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant == null)
        {
            return null;
        }

        var entitlements = new List<TenantModuleEntitlementDto>
        {
            new("mdm", "Master Data Management", true, tenant.Tier ?? "Standard"),
            new("platform", "Platform Shared Services", true, tenant.Tier ?? "Standard"),
            new("esbp", "Enterprise Strategy", !string.Equals(tenant.Tier, "Standard", StringComparison.OrdinalIgnoreCase), tenant.Tier ?? "Standard")
        };

        return new TenantModulesSummaryDto(tenant.Id, tenant.Tier ?? "Standard", entitlements);
    }
}
