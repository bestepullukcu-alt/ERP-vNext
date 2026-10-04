using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class NotificationDispatchRepository : INotificationDispatchRepository
{
    private const int MaxSweepBatchSize = 500;

    private readonly IMongoCollection<NotificationDispatch> _collection;

    public NotificationDispatchRepository(IPlatformDbContext dbContext)
    {
        _collection = dbContext.GetCollection<NotificationDispatch>(PlatformCollections.NotificationDispatches);
    }

    public async Task<NotificationDispatch> CreateAsync(NotificationDispatch dispatch, CancellationToken ct = default)
    {
        await _collection.InsertOneAsync(dispatch, cancellationToken: ct);
        return dispatch;
    }

    public async Task<NotificationDispatch?> GetByIdForTenantAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var filter = Builders<NotificationDispatch>.Filter.And(
            ActiveFilter,
            Builders<NotificationDispatch>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<NotificationDispatch>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<NotificationDispatch>> ListByTenantAsync(
        Guid tenantId,
        int skip = 0,
        int take = 50,
        NotificationDispatchStatus? status = null,
        DateTimeOffset? queuedFrom = null,
        DateTimeOffset? queuedTo = null,
        string? templateKey = null,
        CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<NotificationDispatch>>
        {
            ActiveFilter,
            Builders<NotificationDispatch>.Filter.Eq(x => x.TenantId, tenantId)
        };

        if (status is not null)
        {
            filters.Add(Builders<NotificationDispatch>.Filter.Eq(x => x.Status, status.Value));
        }

        if (queuedFrom is not null)
        {
            filters.Add(Builders<NotificationDispatch>.Filter.Gte(x => x.QueuedAt, queuedFrom.Value));
        }

        if (queuedTo is not null)
        {
            filters.Add(Builders<NotificationDispatch>.Filter.Lte(x => x.QueuedAt, queuedTo.Value));
        }

        if (!string.IsNullOrWhiteSpace(templateKey))
        {
            filters.Add(Builders<NotificationDispatch>.Filter.Eq(x => x.TemplateKey, templateKey));
        }

        return await _collection.Find(Builders<NotificationDispatch>.Filter.And(filters))
            .SortByDescending(x => x.QueuedAt)
            .Skip(skip)
            .Limit(take)
            .ToListAsync(ct);
    }

    public async Task UpdateAsync(NotificationDispatch dispatch, CancellationToken ct = default)
    {
        var filter = Builders<NotificationDispatch>.Filter.And(
            ActiveFilter,
            Builders<NotificationDispatch>.Filter.Eq(x => x.TenantId, dispatch.TenantId),
            Builders<NotificationDispatch>.Filter.Eq(x => x.Id, dispatch.Id));
        await _collection.ReplaceOneAsync(filter, dispatch, cancellationToken: ct);
    }

    public async Task<bool> TryUpdateAsync(
        NotificationDispatch dispatch,
        int expectedVersion,
        NotificationDispatchStatus expectedStatus,
        CancellationToken ct = default)
    {
        var filter = Builders<NotificationDispatch>.Filter.And(
            ActiveFilter,
            Builders<NotificationDispatch>.Filter.Eq(x => x.TenantId, dispatch.TenantId),
            Builders<NotificationDispatch>.Filter.Eq(x => x.Id, dispatch.Id),
            Builders<NotificationDispatch>.Filter.Eq(x => x.Version, expectedVersion),
            Builders<NotificationDispatch>.Filter.Eq(x => x.Status, expectedStatus));
        var result = await _collection.ReplaceOneAsync(filter, dispatch, cancellationToken: ct);
        return result.MatchedCount == 1;
    }

    public async Task<IReadOnlyList<NotificationDispatchRetryHandle>> FindDueRetriesAsync(
        DateTimeOffset asOfUtc,
        int maxRetryCount,
        int take,
        CancellationToken ct = default)
    {
        if (take <= 0)
        {
            return [];
        }

        var boundedTake = Math.Min(take, MaxSweepBatchSize);
        var filter = Builders<NotificationDispatch>.Filter.And(
            ActiveFilter,
            Builders<NotificationDispatch>.Filter.Eq(x => x.Status, NotificationDispatchStatus.Failed),
            Builders<NotificationDispatch>.Filter.Lt(x => x.RetryCount, maxRetryCount),
            Builders<NotificationDispatch>.Filter.Ne<DateTimeOffset?>(x => x.NextRetryAt, null),
            Builders<NotificationDispatch>.Filter.Lte(x => x.NextRetryAt, asOfUtc),
            // BL-454 — a permanent failure is never retried, whatever its count and NextRetryAt still say (the retry
            // window closes a row without touching either).
            Builders<NotificationDispatch>.Filter.Eq<DateTimeOffset?>(x => x.PermanentlyFailedNotifiedAt, null));

        var projection = Builders<NotificationDispatch>.Projection
            .Include(x => x.Id)
            .Include(x => x.TenantId);

        var rows = await _collection
            .Find(filter)
            .Project<NotificationDispatch>(projection)
            .SortBy(x => x.NextRetryAt)
            .Limit(boundedTake)
            .ToListAsync(ct);

        return rows.Select(x => new NotificationDispatchRetryHandle(x.TenantId, x.Id)).ToArray();
    }

    public async Task<IReadOnlyList<NotificationDispatchExpiryHandle>> FindRetryWindowExpiredAsync(
        DateTimeOffset queuedBefore,
        int take,
        CancellationToken ct = default)
    {
        if (take <= 0)
        {
            return [];
        }

        var projection = Builders<NotificationDispatch>.Projection
            .Include(x => x.Id)
            .Include(x => x.TenantId)
            .Include(x => x.Status)
            .Include(x => x.Version)
            .Include(x => x.ErrorCode)
            .Include(x => x.QueuedAt);

        var rows = await _collection
            .Find(RetryWindowExpiredFilter(queuedBefore))
            .Project<NotificationDispatch>(projection)
            .SortBy(x => x.QueuedAt)
            .Limit(Math.Min(take, MaxSweepBatchSize))
            .ToListAsync(ct);

        return rows
            .Select(x => new NotificationDispatchExpiryHandle(x.TenantId, x.Id, x.Status, x.Version, x.ErrorCode, x.QueuedAt))
            .ToArray();
    }

    /// <summary>
    /// BL-454 — the window query, served by <c>ix_notification_dispatches_retry_window_waiting</c> (Status, QueuedAt;
    /// partial on IsDeleted=false AND PermanentlyFailedNotifiedAt=null): both equalities match the partial filter, Status
    /// and QueuedAt are the bounds, and a row already closed as permanent is not in the index at all.
    /// </summary>
    internal static FilterDefinition<NotificationDispatch> RetryWindowExpiredFilter(DateTimeOffset queuedBefore) =>
        Builders<NotificationDispatch>.Filter.And(
            ActiveFilter,
            Builders<NotificationDispatch>.Filter.In(
                x => x.Status, [NotificationDispatchStatus.Queued, NotificationDispatchStatus.Failed]),
            Builders<NotificationDispatch>.Filter.Lt(x => x.QueuedAt, queuedBefore),
            Builders<NotificationDispatch>.Filter.Eq<DateTimeOffset?>(x => x.PermanentlyFailedNotifiedAt, null));

    private static FilterDefinition<NotificationDispatch> ActiveFilter =>
        Builders<NotificationDispatch>.Filter.Eq(x => x.IsDeleted, false);
}
