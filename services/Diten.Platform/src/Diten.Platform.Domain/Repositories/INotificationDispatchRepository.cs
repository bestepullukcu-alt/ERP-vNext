using Diten.Platform.Domain.Entities.Notifications;

namespace Diten.Platform.Domain.Repositories;

public interface INotificationDispatchRepository
{
    Task<NotificationDispatch> CreateAsync(NotificationDispatch dispatch, CancellationToken ct = default);
    Task<NotificationDispatch?> GetByIdForTenantAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationDispatch>> ListByTenantAsync(Guid tenantId, int skip = 0, int take = 50, CancellationToken ct = default);
    Task UpdateAsync(NotificationDispatch dispatch, CancellationToken ct = default);

    /// <summary>
    /// Cross-tenant scan for dispatches in <see cref="Diten.Platform.Domain.Enums.NotificationDispatchStatus.Failed"/>
    /// whose <see cref="NotificationDispatch.NextRetryAt"/> is at or before <paramref name="asOfUtc"/> and whose
    /// <see cref="NotificationDispatch.RetryCount"/> is below <paramref name="maxRetryCount"/>. Server-driven only;
    /// used by the recurring sweep job to enqueue per-dispatch retry work through MOD-0026.
    /// Returned identifiers are intentionally minimal (TenantId, DispatchId) — full dispatch reads happen later through
    /// the tenant-isolated <see cref="GetByIdForTenantAsync"/> path.
    /// </summary>
    Task<IReadOnlyList<NotificationDispatchRetryHandle>> FindDueRetriesAsync(
        DateTimeOffset asOfUtc,
        int maxRetryCount,
        int take,
        CancellationToken ct = default);
}

public sealed record NotificationDispatchRetryHandle(Guid TenantId, Guid DispatchId);
