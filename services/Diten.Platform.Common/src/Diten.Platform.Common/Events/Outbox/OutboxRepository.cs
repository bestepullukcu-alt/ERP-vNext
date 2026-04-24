using MongoDB.Driver;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Common.Events.Outbox;

public sealed class OutboxRepository : TenantRepository<OutboxMessage>
{
    public OutboxRepository(IMongoDatabase database, ITenantContext tenantContext) 
        : base(database, tenantContext, "outbox_messages") { }

    public async Task<IReadOnlyList<OutboxMessage>> GetUnprocessedMessagesAsync(int batchSize, CancellationToken ct = default)
    {
        var filter = Builders<OutboxMessage>.Filter.And(
            Builders<OutboxMessage>.Filter.Eq(m => m.PublishedAt, null),
            Builders<OutboxMessage>.Filter.Eq(m => m.IsDeleted, false),
            Builders<OutboxMessage>.Filter.Lt(m => m.RetryCount, 10));

        return await Collection.Find(filter)
            .Limit(batchSize)
            .SortBy(m => m.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task MarkAsPublishedAsync(Guid id, CancellationToken ct = default)
    {
        var filter = Builders<OutboxMessage>.Filter.Eq(m => m.Id, id);
        var update = Builders<OutboxMessage>.Update
            .Set(m => m.PublishedAt, DateTimeOffset.UtcNow)
            .Set(m => m.UpdatedAt, DateTimeOffset.UtcNow);

        await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    public async Task IncrementRetryAsync(Guid id, string error, CancellationToken ct = default)
    {
        var filter = Builders<OutboxMessage>.Filter.Eq(m => m.Id, id);
        var update = Builders<OutboxMessage>.Update
            .Inc(m => m.RetryCount, 1)
            .Set(m => m.Error, error)
            .Set(m => m.UpdatedAt, DateTimeOffset.UtcNow);

        await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }
}
