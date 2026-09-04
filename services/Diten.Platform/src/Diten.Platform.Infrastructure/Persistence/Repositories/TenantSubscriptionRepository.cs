using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class TenantSubscriptionRepository : GlobalRepository<TenantSubscription>, ITenantSubscriptionRepository
{
    private readonly IPlatformDbContext _dbContext;
    public TenantSubscriptionRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.TenantSubscriptions)
    {
        _dbContext = dbContext;
    }

    public async Task<TenantSubscription> CreateAsync(IPlatformTransactionSession session, TenantSubscription subscription, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        subscription.RowVersion = Guid.NewGuid().ToByteArray();
        try
        {
            await Collection.InsertOneAsync(PlatformMongoTransactionSession.Require(session, _dbContext), subscription, cancellationToken: ct);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new TenantSubscriptionConcurrencyException();
        }
        catch (MongoCommandException exception) when (exception.HasErrorLabel("TransientTransactionError"))
        {
            throw new TenantSubscriptionConcurrencyException();
        }
        return subscription;
    }

    public async Task<TenantSubscription?> GetByTenantIdAsync(Guid tenantId, Guid subscriptionId, CancellationToken ct = default)
    {
        var filter = Builders<TenantSubscription>.Filter.And(
            ExecutionFilter,
            Builders<TenantSubscription>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<TenantSubscription>.Filter.Eq(x => x.Id, subscriptionId));

        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<TenantSubscription?> GetCurrentByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
    {
        var filter = Builders<TenantSubscription>.Filter.And(
            ExecutionFilter,
            Builders<TenantSubscription>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<TenantSubscription>.Filter.In(x => x.Status, TenantSubscriptionStatuses.Current));

        return await Collection.Find(filter)
            .Sort(Builders<TenantSubscription>.Sort.Descending(x => x.CreatedAt))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<TenantSubscription>> GetHistoryByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
    {
        var filter = Builders<TenantSubscription>.Filter.And(
            ExecutionFilter,
            Builders<TenantSubscription>.Filter.Eq(x => x.TenantId, tenantId));

        return await Collection.Find(filter)
            .Sort(Builders<TenantSubscription>.Sort.Descending(x => x.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<bool> HasCurrentAsync(Guid tenantId, Guid? excludeSubscriptionId = null, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<TenantSubscription>>
        {
            ExecutionFilter,
            Builders<TenantSubscription>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<TenantSubscription>.Filter.In(x => x.Status, TenantSubscriptionStatuses.Current)
        };

        if (excludeSubscriptionId.HasValue)
        {
            filters.Add(Builders<TenantSubscription>.Filter.Ne(x => x.Id, excludeSubscriptionId.Value));
        }

        return await Collection.Find(Builders<TenantSubscription>.Filter.And(filters)).AnyAsync(ct);
    }

    public async Task UpdateAsync(TenantSubscription subscription, byte[]? expectedRowVersion, CancellationToken ct = default)
        => await UpdateCoreAsync(null, subscription, expectedRowVersion, ct);

    public async Task UpdateAsync(IPlatformTransactionSession session, TenantSubscription subscription, byte[]? expectedRowVersion, CancellationToken ct = default)
        => await UpdateCoreAsync(PlatformMongoTransactionSession.Require(session, _dbContext), subscription, expectedRowVersion, ct);

    private async Task UpdateCoreAsync(IClientSessionHandle? session, TenantSubscription subscription, byte[]? expectedRowVersion, CancellationToken ct)
    {
        var filters = new List<FilterDefinition<TenantSubscription>>
        {
            ExecutionFilter,
            Builders<TenantSubscription>.Filter.Eq(x => x.TenantId, subscription.TenantId),
            Builders<TenantSubscription>.Filter.Eq(x => x.Id, subscription.Id)
        };

        if (expectedRowVersion is { Length: > 0 })
        {
            filters.Add(Builders<TenantSubscription>.Filter.Eq(x => x.RowVersion, expectedRowVersion));
        }

        subscription.UpdatedAt = DateTimeOffset.UtcNow;
        subscription.RowVersion = Guid.NewGuid().ToByteArray();

        var filter = Builders<TenantSubscription>.Filter.And(filters);
        ReplaceOneResult result;
        try
        {
            result = session is null
                ? await Collection.ReplaceOneAsync(filter, subscription, cancellationToken: ct)
                : await Collection.ReplaceOneAsync(session, filter, subscription, cancellationToken: ct);
        }
        catch (MongoCommandException exception) when (session is not null && exception.HasErrorLabel("TransientTransactionError"))
        {
            throw new TenantSubscriptionConcurrencyException();
        }

        if (result.MatchedCount == 0)
        {
            throw new TenantSubscriptionConcurrencyException();
        }
    }
}
