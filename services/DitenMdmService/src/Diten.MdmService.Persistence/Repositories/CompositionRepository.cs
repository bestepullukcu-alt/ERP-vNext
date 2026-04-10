using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class CompositionRepository : RepositoryBase<Composition>, ICompositionRepository
{
    public CompositionRepository(IMongoDatabase database, ITenantContext tenantContext)
        : base(database, tenantContext, "compositions")
    {
        var indexKeys = Builders<Composition>.IndexKeys
            .Ascending(x => x.TenantId)
            .Ascending(x => x.FormulationCode)
            .Ascending(x => x.IsDeleted);
        Collection.Indexes.CreateOne(new CreateIndexModel<Composition>(indexKeys, new CreateIndexOptions { Unique = true }));
    }

    public async Task<Composition> CreateAsync(Composition entity, CancellationToken cancellationToken = default)
    {
        return await InsertAsync(entity, cancellationToken);
    }

    public async Task<bool> UpdateAsync(Composition entity, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Composition>.Filter.And(
            TenantFilter,
            Builders<Composition>.Filter.Eq(x => x.Id, entity.Id));

        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.TenantId = TenantContext.TenantId;
        var result = await Collection.ReplaceOneAsync(filter, entity, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<Composition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await FindByIdAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<Composition>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await Collection.Find(TenantFilter).SortBy(x => x.FormulationCode).ToListAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Composition>.Filter.And(
            TenantFilter,
            Builders<Composition>.Filter.Eq(x => x.Id, id));

        var update = Builders<Composition>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow);

        await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    public async Task<bool> ExistsByCodeAsync(string formulationCode, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Composition>.Filter.And(
            TenantFilter,
            Builders<Composition>.Filter.Eq(x => x.FormulationCode, formulationCode));

        if (excludeId.HasValue)
        {
            filter &= Builders<Composition>.Filter.Ne(x => x.Id, excludeId.Value);
        }

        return await Collection.Find(filter).AnyAsync(cancellationToken);
    }
}
