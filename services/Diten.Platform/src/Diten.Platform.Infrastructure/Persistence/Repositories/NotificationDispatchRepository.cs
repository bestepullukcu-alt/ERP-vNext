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
        _collection = dbContext.GetCollection<NotificationDispatch>("notification_dispatches");
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

    public async Task<IReadOnlyList<NotificationDispatch>> ListByTenantAsync(Guid tenantId, int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var filter = Builders<NotificationDispatch>.Filter.And(
            ActiveFilter,
            Builders<NotificationDispatch>.Filter.Eq(x => x.TenantId, tenantId));
        return await _collection.Find(filter)
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
            Builders<NotificationDispatch>.Filter.Lte(x => x.NextRetryAt, asOfUtc));

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

    private static FilterDefinition<NotificationDispatch> ActiveFilter =>
        Builders<NotificationDispatch>.Filter.Eq(x => x.IsDeleted, false);
}
