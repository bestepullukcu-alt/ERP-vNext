using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Application.Features.Tenants.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

public sealed class GetTenantStatsQueryHandler : IRequestHandler<GetTenantStatsQuery, TenantRegistryStatsDto>
{
    private readonly ITenantRegistryRepository _repository;

    public GetTenantStatsQueryHandler(ITenantRegistryRepository repository)
    {
        _repository = repository;
    }

    public async Task<TenantRegistryStatsDto> Handle(GetTenantStatsQuery request, CancellationToken cancellationToken)
    {
        var stats = await _repository.GetStatsAsync(cancellationToken);
        return new TenantRegistryStatsDto(
            stats.Total,
            stats.Active,
            stats.Provisioning,
            stats.Suspended,
            stats.Deactivated,
            stats.Trial,
            stats.OverQuota);
    }
}
