using Diten.Platform.Application.Features.Notifications;

namespace Diten.Platform.Application.Features.Notifications.Services;

// MOD-0027-FU03 — reads NotificationEvents declared on in-process module manifests and reconciles the catalog.
public interface INotificationEventManifestSyncService
{
    Task<NotificationEventSyncResultDto> SyncAsync(CancellationToken ct = default);
}
