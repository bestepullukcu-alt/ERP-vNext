using Diten.Application.EnterpriseStrategy.Shared;
using Diten.Persistence.Context;
using MongoDB.Driver;

namespace Diten.Persistence.EnterpriseStrategy;

public sealed class MongoEnterpriseStrategyAuditStore : IEnterpriseStrategyAuditStore
{
    private readonly IMongoCollection<AuditEvent> _collection;

    public MongoEnterpriseStrategyAuditStore(MongoDbContext context)
    {
        _collection = context.GetCollection<AuditEvent>(nameof(AuditEvent));
    }

    public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default) =>
        _collection.InsertOneAsync(auditEvent, cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<AuditEvent>> ListAsync(
        string objectType,
        string objectId,
        CancellationToken cancellationToken = default) =>
        await _collection
            .Find(x => x.ObjectType == objectType && x.ObjectId == objectId)
            .SortByDescending(x => x.TimestampUtc)
            .ToListAsync(cancellationToken);
}
