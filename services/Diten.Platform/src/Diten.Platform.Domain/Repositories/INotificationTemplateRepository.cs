using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Repositories;

public interface INotificationTemplateRepository
{
    Task<NotificationTemplate> CreateAsync(NotificationTemplate template, CancellationToken ct = default);
    Task<NotificationTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<NotificationTemplate?> GetActiveByKeyAsync(
        Guid? tenantId,
        bool isPlatformDefault,
        string templateKey,
        string locale,
        NotificationChannelCode channel,
        CancellationToken ct = default);
    Task<NotificationTemplate?> GetBestActiveByKeyAsync(
        Guid tenantId,
        string templateKey,
        string locale,
        NotificationChannelCode channel,
        CancellationToken ct = default);
    Task<bool> ActiveTemplateExistsAsync(
        Guid? tenantId,
        bool isPlatformDefault,
        string templateKey,
        string locale,
        NotificationChannelCode channel,
        Guid? excludeId = null,
        CancellationToken ct = default);
    Task UpdateAsync(NotificationTemplate template, CancellationToken ct = default);
    Task ArchiveAsync(Guid id, CancellationToken ct = default);
}
