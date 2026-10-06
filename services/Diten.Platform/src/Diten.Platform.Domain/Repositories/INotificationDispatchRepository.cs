using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Repositories;

public interface INotificationDispatchRepository
{
    Task<NotificationDispatch> CreateAsync(NotificationDispatch dispatch, CancellationToken ct = default);
    Task<NotificationDispatch?> GetByIdForTenantAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationDispatch>> ListByTenantAsync(
        Guid tenantId,
        int skip = 0,
        int take = 50,
        NotificationDispatchStatus? status = null,
        DateTimeOffset? queuedFrom = null,
        DateTimeOffset? queuedTo = null,
        string? templateKey = null,
        CancellationToken ct = default);
    Task UpdateAsync(NotificationDispatch dispatch, CancellationToken ct = default);

    /// <summary>
    /// BL-454 — replaces the row only while it still has <paramref name="expectedVersion"/> and
    /// <paramref name="expectedStatus"/> (what the caller read before changing it). False = someone wrote in between
    /// and nothing was written.
    /// </summary>
    Task<bool> TryUpdateAsync(
        NotificationDispatch dispatch,
        int expectedVersion,
        NotificationDispatchStatus expectedStatus,
        CancellationToken ct = default);

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

    /// <summary>
    /// BL-454 — cross-tenant scan for PERMANENT rows whose permanent-failure effects have not run
    /// (<see cref="NotificationDispatch.PermanentFailurePending"/>) and that nobody has touched since
    /// <paramref name="idleBefore"/> (<c>UpdatedAt</c>): the transition's own run, or a claim, gets that long before the
    /// retry sweep re-drives the row. Still Failed only; a row that was sent after all is not owed any effect.
    /// </summary>
    Task<IReadOnlyList<NotificationDispatchExpiryHandle>> FindPermanentFailurePendingAsync(
        DateTimeOffset idleBefore,
        int take,
        CancellationToken ct = default);

    /// <summary>
    /// BL-454 — cross-tenant scan for dispatches still WAITING (<see cref="Diten.Platform.Domain.Enums.NotificationDispatchStatus.Queued"/>
    /// or <see cref="Diten.Platform.Domain.Enums.NotificationDispatchStatus.Failed"/>, never yet a permanent failure)
    /// that were queued before <paramref name="queuedBefore"/>: their retry window has passed and the sweep closes them,
    /// releasing their variables. Same minimal handles as <see cref="FindDueRetriesAsync"/>.
    /// </summary>
    Task<IReadOnlyList<NotificationDispatchExpiryHandle>> FindRetryWindowExpiredAsync(
        DateTimeOffset queuedBefore,
        int take,
        CancellationToken ct = default);
}

public sealed record NotificationDispatchRetryHandle(Guid TenantId, Guid DispatchId);

/// <summary>
/// BL-454 — what the sweep needs to close a row whose retry window passed: where it is, the exact state it was read in
/// (the close is conditional on it), its last real error (kept in the closing message) and when it was queued (a row
/// far older than the window is closed silently).
/// </summary>
public sealed record NotificationDispatchExpiryHandle(
    Guid TenantId,
    Guid DispatchId,
    NotificationDispatchStatus Status,
    int Version,
    string? ErrorCode,
    DateTimeOffset QueuedAt);
