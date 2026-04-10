using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class CompositionVersionRepository : RepositoryBase<CompositionVersion>, ICompositionVersionRepository
{
    public CompositionVersionRepository(IMongoDatabase database, ITenantContext tenantContext)
        : base(database, tenantContext, "composition_versions")
    {
        var indexKeys = Builders<CompositionVersion>.IndexKeys
            .Ascending(x => x.TenantId)
            .Ascending(x => x.CompositionId)
            .Ascending(x => x.VersionNo)
            .Ascending(x => x.IsDeleted);
        Collection.Indexes.CreateOne(new CreateIndexModel<CompositionVersion>(indexKeys, new CreateIndexOptions { Unique = true }));
    }

    public async Task<CompositionVersion> CreateAsync(CompositionVersion entity, CancellationToken cancellationToken = default)
    {
        return await InsertAsync(entity, cancellationToken);
    }

    public async Task<bool> UpdateAsync(CompositionVersion entity, CancellationToken cancellationToken = default)
    {
        var filter = Builders<CompositionVersion>.Filter.And(
            TenantFilter,
            Builders<CompositionVersion>.Filter.Eq(x => x.Id, entity.Id));

        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.TenantId = TenantContext.TenantId;
        var result = await Collection.ReplaceOneAsync(filter, entity, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<CompositionVersion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await FindByIdAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<CompositionVersion>> GetByCompositionIdAsync(Guid compositionId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<CompositionVersion>.Filter.And(
            TenantFilter,
            Builders<CompositionVersion>.Filter.Eq(x => x.CompositionId, compositionId));

        return await Collection.Find(filter).SortByDescending(x => x.VersionNo).ToListAsync(cancellationToken);
    }

    public async Task<CompositionVersion?> GetCurrentVersionAsync(Guid compositionId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<CompositionVersion>.Filter.And(
            TenantFilter,
            Builders<CompositionVersion>.Filter.Eq(x => x.CompositionId, compositionId),
            Builders<CompositionVersion>.Filter.Eq(x => x.IsCurrent, true));

        return await Collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int> GetNextVersionNoAsync(Guid compositionId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<CompositionVersion>.Filter.And(
            TenantFilter,
            Builders<CompositionVersion>.Filter.Eq(x => x.CompositionId, compositionId));

        var lastVersion = await Collection.Find(filter)
            .SortByDescending(x => x.VersionNo)
            .Project(x => x.VersionNo)
            .FirstOrDefaultAsync(cancellationToken);

        return lastVersion + 1;
    }

    public async Task<bool> MarkOtherVersionsAsSupersededAsync(Guid compositionId, Guid activeVersionId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<CompositionVersion>.Filter.And(
            TenantFilter,
            Builders<CompositionVersion>.Filter.Eq(x => x.CompositionId, compositionId),
            Builders<CompositionVersion>.Filter.Ne(x => x.Id, activeVersionId),
            Builders<CompositionVersion>.Filter.Eq(x => x.Status, CompositionVersionStatus.Active));

        var update = Builders<CompositionVersion>.Update
            .Set(x => x.Status, CompositionVersionStatus.Superseded)
            .Set(x => x.IsCurrent, false)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow);

        await Collection.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
        return true;
    }
}
