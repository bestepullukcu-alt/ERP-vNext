using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.Exceptions;
using Diten.PpmService.Domain.Repositories;
using Diten.PpmService.Persistence.Mongo;
using MongoDB.Driver;

namespace Diten.PpmService.Persistence.Repositories;


public abstract class MongoRepository<T>(
    PpmMongoContext context,
    IMongoCollection<T> collection) : IRepository<T>
    where T : EntityBase
{
    public async Task<T?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var filter = ActiveTenantFilter(tenantId) &
                     Builders<T>.Filter.Eq(entity => entity.Id, id);

        try
        {
            var session = context.CurrentSession;
            return session is null
                ? await collection.Find(filter).FirstOrDefaultAsync(cancellationToken)
                : await collection.Find(session, filter).FirstOrDefaultAsync(cancellationToken);
        }
        catch (MongoException exception) { throw Unavailable(exception); }
    }

    public async Task<IReadOnlyList<T>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var filter = ActiveTenantFilter(tenantId);
        var sort = Builders<T>.Sort.Ascending("Code");
        try
        {
            var session = context.CurrentSession;
            return session is null
                ? await collection.Find(filter).Sort(sort).ToListAsync(cancellationToken)
                : await collection.Find(session, filter).Sort(sort).ToListAsync(cancellationToken);
        }
        catch (MongoException exception) { throw Unavailable(exception); }
    }

    public async Task<bool> CodeExistsAsync(
        Guid tenantId,
        string normalizedCode,
        Guid? excludingId,
        CancellationToken cancellationToken)
    {
        var filter = ActiveTenantFilter(tenantId) &
                     Builders<T>.Filter.Eq("Code", normalizedCode);

        if (excludingId.HasValue)
            filter &= Builders<T>.Filter.Ne(entity => entity.Id, excludingId.Value);

        try
        {
            var session = context.CurrentSession;
            return session is null
                ? await collection.Find(filter).AnyAsync(cancellationToken)
                : await collection.Find(session, filter).AnyAsync(cancellationToken);
        }
        catch (MongoException exception) { throw Unavailable(exception); }
    }

    public async Task AddAsync(T entity, CancellationToken cancellationToken)
    {
        ValidateEntityScope(entity);
        var session = context.RequireTransaction();
        await collection.InsertOneAsync(session, entity, cancellationToken: cancellationToken);
    }

    public async Task ReplaceAsync(
        T entity,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        ValidateEntityScope(entity);
        if (expectedVersion < 1 || entity.Version != expectedVersion + 1)
            throw new OptimisticConcurrencyException(
                $"{typeof(T).Name} expected version does not match the mutation base version.");

        var filter = ActiveTenantFilter(entity.TenantId) &
                     Builders<T>.Filter.Eq(item => item.Id, entity.Id) &
                     Builders<T>.Filter.Eq(item => item.Version, expectedVersion);

        var session = context.RequireTransaction();
        var result = await collection.ReplaceOneAsync(
            session,
            filter,
            entity,
            new ReplaceOptions { IsUpsert = false },
            cancellationToken);

        if (result.MatchedCount != 1)
            throw new OptimisticConcurrencyException(
                $"{typeof(T).Name} was not found in the tenant or its version changed.");
    }

    private static FilterDefinition<T> ActiveTenantFilter(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));

        return Builders<T>.Filter.Eq(entity => entity.TenantId, tenantId) &
               Builders<T>.Filter.Eq(entity => entity.IsDeleted, false);
    }

    private static void ValidateEntityScope(T entity)
    {
        if (entity.Id == Guid.Empty || entity.TenantId == Guid.Empty)
            throw new InvalidOperationException("PPM entities require non-empty Id and TenantId.");
        if (entity.CreatedAtUtc.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("CreatedAtUtc must be UTC.");
        if (entity.UpdatedAtUtc is { Kind: not DateTimeKind.Utc })
            throw new InvalidOperationException("UpdatedAtUtc must be UTC.");
        if (entity.DeletedAtUtc is { Kind: not DateTimeKind.Utc })
            throw new InvalidOperationException("DeletedAtUtc must be UTC.");
    }

    private static TransactionUnavailableException Unavailable(MongoException exception) =>
        new("Mongo persistence is unavailable.", exception);
}
