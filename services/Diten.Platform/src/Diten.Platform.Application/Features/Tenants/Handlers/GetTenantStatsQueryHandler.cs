using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Application.Features.Tenants.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

public sealed class GetTenantStatsQueryHandler : IRequestHandler<GetTenantStatsQuery, TenantRegistryStatsDto>
{
    private readonly ITenantRegistryRepository _repository;
    private readonly Diten.Platform.Application.Contracts.IPlatformActorTenantScope _scope;

    public GetTenantStatsQueryHandler(ITenantRegistryRepository repository,
        Diten.Platform.Application.Contracts.IPlatformActorTenantScope scope)
    {
        _repository = repository;
        _scope = scope;
    }

    public async Task<TenantRegistryStatsDto> Handle(GetTenantStatsQuery request, CancellationToken cancellationToken)
    {
        // BL-521 — the counts of the tenants the actor may see, nothing more.
        var stats = await _repository.GetStatsAsync((await _scope.GetAsync(cancellationToken)).QueryFilter, cancellationToken);
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
