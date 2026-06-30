using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Application.Features.Tenants.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

public sealed class GetTenantBrandingQueryHandler : IRequestHandler<GetTenantBrandingQuery, TenantBrandingDto?>
{
    private readonly ITenantRegistryRepository _tenantRepository;

    public GetTenantBrandingQueryHandler(ITenantRegistryRepository tenantRepository)
    {
        _tenantRepository = tenantRepository;
    }

    public async Task<TenantBrandingDto?> Handle(GetTenantBrandingQuery request, CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant == null)
        {
            return null;
        }

        // Project ONLY presentation fields — no sensitive tenant data crosses the pre-auth boundary.
        return new TenantBrandingDto(
            tenant.Id,
            tenant.DisplayName,
            tenant.LogoDataUrl,
            tenant.FaviconDataUrl);
    }
}
