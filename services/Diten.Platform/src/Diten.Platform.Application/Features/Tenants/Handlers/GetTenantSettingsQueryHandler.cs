using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Application.Features.Tenants.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

public sealed class GetTenantSettingsQueryHandler : IRequestHandler<GetTenantSettingsQuery, TenantSettingsDto?>
{
    private readonly ITenantRegistryRepository _repository;

    public GetTenantSettingsQueryHandler(ITenantRegistryRepository repository)
    {
        _repository = repository;
    }

    public async Task<TenantSettingsDto?> Handle(GetTenantSettingsQuery request, CancellationToken cancellationToken)
    {
        var tenant = await _repository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant == null)
        {
            return null;
        }

        return new TenantSettingsDto(
            tenant.Id,
            tenant.Region ?? "US",
            tenant.Settings.Language,
            tenant.Settings.Timezone,
            tenant.Settings.Currency,
            tenant.Settings.Environment);
    }
}
