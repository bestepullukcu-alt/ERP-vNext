using Diten.Platform.Application.Common;

namespace Diten.Platform.Application.Features.Notifications.Services;

public interface ITenantMessagingSettingsResolver
{
    Task<Response<ResolvedMessagingSettingsDto>> ResolveAsync(Guid tenantId, CancellationToken ct = default);
}
