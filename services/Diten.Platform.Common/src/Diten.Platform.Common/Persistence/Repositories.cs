using MongoDB.Driver;
using Diten.Platform.Common.Tenancy;

namespace Diten.Platform.Common.Persistence;

/// <summary>
/// Root repository logic for MongoDB.
/// </summary>
public abstract class RepositoryBase<TEntity> where TEntity : BaseEntity
{
    protected readonly IMongoCollection<TEntity> Collection;
    protected readonly ITenantContext TenantContext;

    protected RepositoryBase(IMongoDatabase database, ITenantContext tenantContext, string collectionName)
    {
        Collection = database.GetCollection<TEntity>(collectionName);
        TenantContext = tenantContext;
    }

    protected abstract FilterDefinition<TEntity> ExecutionFilter { get; }

    public virtual async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var filter = Builders<TEntity>.Filter.And(
            ExecutionFilter,
            Builders<TEntity>.Filter.Eq(e => e.Id, id));
        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public virtual async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default)
    {
        return await Collection.Find(ExecutionFilter).ToListAsync(ct);
    }

    public virtual async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var filter = Builders<TEntity>.Filter.And(
            ExecutionFilter,
            Builders<TEntity>.Filter.Eq(e => e.Id, id));

        var update = Builders<TEntity>.Update
            .Set(e => e.IsDeleted, true)
            .Set(e => e.UpdatedAt, DateTimeOffset.UtcNow);

        await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }
}

/// <summary>
/// Repository for Global entities (no tenant isolation).
/// </summary>
public class GlobalRepository<TEntity> : RepositoryBase<TEntity> where TEntity : GlobalEntity
{
    public GlobalRepository(IMongoDatabase database, ITenantContext tenantContext, string collectionName) 
        : base(database, tenantContext, collectionName) { }

    protected override FilterDefinition<TEntity> ExecutionFilter => 
        Builders<TEntity>.Filter.Eq(e => e.IsDeleted, false);

    public virtual async Task<TEntity> CreateAsync(TEntity entity, CancellationToken ct = default)
    {
        await Collection.InsertOneAsync(entity, cancellationToken: ct);
        return entity;
    }
}

/// <summary>
/// Repository for Tenant-scoped entities (strict isolation).
/// </summary>
public class TenantRepository<TEntity> : RepositoryBase<TEntity> where TEntity : TenantScopedEntity
{
    public TenantRepository(IMongoDatabase database, ITenantContext tenantContext, string collectionName) 
        : base(database, tenantContext, collectionName) { }

    protected override FilterDefinition<TEntity> ExecutionFilter =>
        Builders<TEntity>.Filter.And(
            Builders<TEntity>.Filter.Eq(e => e.TenantId, TenantContext.TenantId),
            Builders<TEntity>.Filter.Eq(e => e.IsDeleted, false)
        );

    public virtual async Task<TEntity> CreateAsync(TEntity entity, CancellationToken ct = default)
    {
        // Enforce tenant_id from context
        var prop = typeof(TEntity).GetProperty("TenantId");
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(entity, TenantContext.TenantId);
        }
        await Collection.InsertOneAsync(entity, cancellationToken: ct);
        return entity;
    }
}

/// <summary>
/// Repository for Hybrid entities (Global default + Tenant override).
/// </summary>
public class HybridRepository<TEntity> : RepositoryBase<TEntity> where TEntity : HybridEntity
{
    public HybridRepository(IMongoDatabase database, ITenantContext tenantContext, string collectionName) 
        : base(database, tenantContext, collectionName) { }

    protected override FilterDefinition<TEntity> ExecutionFilter =>
        Builders<TEntity>.Filter.And(
            Builders<TEntity>.Filter.Or(
                Builders<TEntity>.Filter.Eq(e => e.TenantId, null),
                Builders<TEntity>.Filter.Eq(e => e.TenantId, TenantContext.TenantId)
            ),
            Builders<TEntity>.Filter.Eq(e => e.IsDeleted, false)
        );
}
