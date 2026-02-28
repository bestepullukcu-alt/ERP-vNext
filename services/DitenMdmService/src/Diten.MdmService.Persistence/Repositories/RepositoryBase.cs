using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

/// <summary>
/// Tenant-enforcing generic repository base.
/// Her sorgu otomatik olarak TenantId filtresi içerir.
/// Her insert otomatik olarak TenantId set eder.
/// </summary>
public abstract class RepositoryBase<TEntity> where TEntity : EntityBase
{
    protected readonly IMongoCollection<TEntity> Collection;
    protected readonly ITenantContext TenantContext;

    protected RepositoryBase(IMongoDatabase database, ITenantContext tenantContext, string collectionName)
    {
        Collection = database.GetCollection<TEntity>(collectionName);
        TenantContext = tenantContext;
    }

    /// <summary>
    /// Her okuma bu filter builder üzerinden yapılmalıdır.
    /// </summary>
    protected FilterDefinition<TEntity> TenantFilter =>
        Builders<TEntity>.Filter.Eq(e => e.TenantId, TenantContext.TenantId);

    protected async Task<TEntity?> FindByIdAsync(Guid id, CancellationToken ct)
    {
        var filter = Builders<TEntity>.Filter.And(
            TenantFilter,
            Builders<TEntity>.Filter.Eq(e => e.Id, id));

        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    protected async Task<IReadOnlyList<TEntity>> FindAllAsync(CancellationToken ct)
    {
        return await Collection.Find(TenantFilter).ToListAsync(ct);
    }

    protected async Task<TEntity> InsertAsync(TEntity entity, CancellationToken ct)
    {
        // TenantId server-side set edilir — DTO'dan gelen değer varsa ezilir
        entity.TenantId = TenantContext.TenantId;

        await Collection.InsertOneAsync(entity, cancellationToken: ct);
        return entity;
    }
}
