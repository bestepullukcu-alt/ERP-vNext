using Diten.Platform.Application.Common;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Notifications.Services;

public sealed class TenantMessagingSettingsResolver : ITenantMessagingSettingsResolver
{
    private readonly ITenantMessagingSettingsRepository _repository;

    public TenantMessagingSettingsResolver(ITenantMessagingSettingsRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<ResolvedMessagingSettingsDto>> ResolveAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenantSettings = await _repository.GetByTenantIdAsync(tenantId, ct);
        if (tenantSettings is not null)
        {
            if (!tenantSettings.IsEnabled)
            {
                return tenantSettings.FallbackPolicy == NotificationFallbackPolicy.UsePlatformDefault
                    ? await ResolvePlatformDefaultAsync(tenantId, ct)
                    : Response<ResolvedMessagingSettingsDto>.Fail("Tenant messaging settings are disabled and fallback is not allowed.", 400);
            }

            return Response<ResolvedMessagingSettingsDto>.Success(tenantSettings.ToResolvedDto(tenantId));
        }

        return await ResolvePlatformDefaultAsync(tenantId, ct);
    }

    private async Task<Response<ResolvedMessagingSettingsDto>> ResolvePlatformDefaultAsync(Guid requestedTenantId, CancellationToken ct)
    {
        var platformDefault = await _repository.GetPlatformDefaultAsync(ct);
        if (platformDefault is null || !platformDefault.IsEnabled)
        {
            return Response<ResolvedMessagingSettingsDto>.Fail("Platform default messaging settings were not found or are disabled.", 400);
        }

        return Response<ResolvedMessagingSettingsDto>.Success(platformDefault.ToResolvedDto(requestedTenantId));
    }
}
