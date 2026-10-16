using Diten.Platform.Application.Common;
using Diten.Platform.Domain.Entities.Notifications;
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
        // BL-499 (2) — the same selection the SMTP provider applies (MessagingSettingsSelection); the refusal carries
        // its own code, so a disabled tenant is told apart from a missing platform default.
        var tenantSettings = await _repository.GetByTenantIdAsync(tenantId, ct);
        TenantMessagingSettings? platformDefault = null;
        if (tenantSettings is not { IsDeleted: false, IsEnabled: true })
        {
            platformDefault = await _repository.GetPlatformDefaultAsync(ct);
        }

        var (settings, refusal) = MessagingSettingsSelection.Select(tenantSettings, () => platformDefault);
        return settings is null
            ? Response<ResolvedMessagingSettingsDto>.Fail(MessagingSettingsSelection.Describe(refusal!), 400, refusal)
            : Response<ResolvedMessagingSettingsDto>.Success(settings.ToResolvedDto(tenantId));
    }
}
