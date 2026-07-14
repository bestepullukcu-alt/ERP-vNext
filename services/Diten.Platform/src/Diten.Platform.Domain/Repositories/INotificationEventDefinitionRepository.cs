using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Repositories;

// MOD-0027-FU03 — Notification Event Catalog persistence. Platform/global records (no TenantId); EventCode unique.
public interface INotificationEventDefinitionRepository
{
    Task<NotificationEventDefinition> CreateAsync(NotificationEventDefinition definition, CancellationToken ct = default);
    Task UpdateAsync(NotificationEventDefinition definition, CancellationToken ct = default);
    Task<NotificationEventDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<NotificationEventDefinition?> GetByEventCodeAsync(string eventCode, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationEventDefinition>> ListAsync(
        string? ownerModuleId = null,
        NotificationChannelCode? channel = null,
        NotificationEventStatus? status = null,
        bool? canTenantOverride = null,
        NotificationEventUsageType? usageType = null,
        int skip = 0,
        int take = 100,
        CancellationToken ct = default);
    Task<IReadOnlyList<NotificationEventDefinition>> ListActiveAsync(CancellationToken ct = default);
}
