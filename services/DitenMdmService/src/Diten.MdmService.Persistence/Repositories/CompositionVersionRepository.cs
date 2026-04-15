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

    public async Task<IReadOnlyList<CompositionVersion>> GetByCompositionIdAsync(Guid compositionId, CancellationToken ct = default)
    {
        var filter = Builders<CompositionVersion>.Filter.And(
            TenantFilter,
            Builders<CompositionVersion>.Filter.Eq(x => x.CompositionId, compositionId));

        return await Collection.Find(filter).SortByDescending(x => x.VersionNo).ToListAsync(ct);
    }

    public async Task<CompositionVersion?> GetCurrentVersionAsync(Guid compositionId, CancellationToken ct = default)
    {
        var filter = Builders<CompositionVersion>.Filter.And(
            TenantFilter,
            Builders<CompositionVersion>.Filter.Eq(x => x.CompositionId, compositionId),
            Builders<CompositionVersion>.Filter.Eq(x => x.IsCurrent, true));

        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<int> GetNextVersionNoAsync(Guid compositionId, CancellationToken ct = default)
    {
        var filter = Builders<CompositionVersion>.Filter.And(
            TenantFilter,
            Builders<CompositionVersion>.Filter.Eq(x => x.CompositionId, compositionId));

        var lastVersion = await Collection.Find(filter)
            .SortByDescending(x => x.VersionNo)
            .Project(x => x.VersionNo)
            .FirstOrDefaultAsync(ct);

        return lastVersion + 1;
    }

    public async Task<bool> MarkOtherVersionsAsSupersededAsync(Guid compositionId, Guid activeVersionId, CancellationToken ct = default)
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

        await Collection.UpdateManyAsync(filter, update, cancellationToken: ct);
        return true;
    }
}
