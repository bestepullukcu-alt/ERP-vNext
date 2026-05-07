using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Application.Features.Tenants.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

public sealed class GetTenantLoginSettingsQueryHandler : IRequestHandler<GetTenantLoginSettingsQuery, TenantLoginSettingsDto?>
{
    private readonly ITenantRegistryRepository _tenantRepository;
    private readonly ITenantLoginSettingsRepository _settingsRepository;
    private readonly ICurrentUserContext _currentUser;

    public GetTenantLoginSettingsQueryHandler(
        ITenantRegistryRepository tenantRepository,
        ITenantLoginSettingsRepository settingsRepository,
        ICurrentUserContext currentUser)
    {
        _tenantRepository = tenantRepository;
        _settingsRepository = settingsRepository;
        _currentUser = currentUser;
    }

    public async Task<TenantLoginSettingsDto?> Handle(GetTenantLoginSettingsQuery request, CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant == null)
        {
            return null;
        }

        var settings = await _settingsRepository.GetByTenantRefIdAsync(request.TenantId, cancellationToken);
        if (settings == null)
        {
            var now = DateTimeOffset.UtcNow;
            settings = TenantLoginSettingsMapper.CreateDefault(request.TenantId, GetActor(), now);
            await _settingsRepository.CreateAsync(settings, cancellationToken);
        }

        return TenantLoginSettingsMapper.ToDto(settings);
    }

    private string GetActor()
    {
        return _currentUser.ActorName;
    }
}
