using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class SavedViewRepository : RepositoryBase<SavedView>, ISavedViewRepository
{
    public SavedViewRepository(IPlatformDbContext platformDbContext, ITenantContext tenantContext)
        : base(platformDbContext, tenantContext, PlatformCollections.SavedViews)
    {
    }

    public async Task<IReadOnlyList<SavedView>> GetListAsync(Guid userId, string moduleKey, string pageKey, CancellationToken ct = default)
    {
        var filter = Builders<SavedView>.Filter.And(
            Builders<SavedView>.Filter.Eq(entity => entity.TenantId, TenantContext.TenantId),
            Builders<SavedView>.Filter.Eq(entity => entity.Status, true),
            Builders<SavedView>.Filter.Eq(entity => entity.UserId, userId),
            Builders<SavedView>.Filter.Eq(entity => entity.ModuleKey, moduleKey),
            Builders<SavedView>.Filter.Eq(entity => entity.PageKey, pageKey));

        return await Collection
            .Find(filter)
            .SortByDescending(entity => entity.IsDefault)
            .ThenByDescending(entity => entity.ModifiedDate)
            .ThenByDescending(entity => entity.CreatedDate)
            .ToListAsync(ct);
    }

    public async Task<SavedView?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var filter = Builders<SavedView>.Filter.And(
            Builders<SavedView>.Filter.Eq(entity => entity.TenantId, TenantContext.TenantId),
            Builders<SavedView>.Filter.Eq(entity => entity.Status, true),
            Builders<SavedView>.Filter.Eq(entity => entity.Id, id));

        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<SavedView> InsertAsync(SavedView entity, CancellationToken ct = default)
    {
        entity.TenantId = TenantContext.TenantId;
        entity.Status = true;
        entity.CreatedDate = DateTime.UtcNow;
        entity.ModifiedDate = DateTime.UtcNow;
        await Collection.InsertOneAsync(entity, cancellationToken: ct);
        return entity;
    }

    public async Task<SavedView> UpdateAsync(SavedView entity, CancellationToken ct = default)
    {
        var filter = Builders<SavedView>.Filter.And(
            Builders<SavedView>.Filter.Eq(entity => entity.TenantId, TenantContext.TenantId),
            Builders<SavedView>.Filter.Eq(entity => entity.Status, true),
            Builders<SavedView>.Filter.Eq(savedView => savedView.Id, entity.Id));

        entity.TenantId = TenantContext.TenantId;
        entity.ModifiedDate = DateTime.UtcNow;

        await Collection.ReplaceOneAsync(filter, entity, cancellationToken: ct);
        return entity;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        var filter = Builders<SavedView>.Filter.And(
            Builders<SavedView>.Filter.Eq(entity => entity.TenantId, TenantContext.TenantId),
            Builders<SavedView>.Filter.Eq(entity => entity.Status, true),
            Builders<SavedView>.Filter.Eq(entity => entity.Id, id));

        var update = Builders<SavedView>.Update
            .Set(entity => entity.Status, false)
            .Set(entity => entity.ModifiedDate, DateTime.UtcNow);

        var result = await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }

    public async Task ClearDefaultsAsync(Guid userId, string moduleKey, string pageKey, string? excludeId = null, CancellationToken ct = default)
    {
        var filterBuilder = Builders<SavedView>.Filter;
        var filter = filterBuilder.And(
            filterBuilder.Eq(entity => entity.TenantId, TenantContext.TenantId),
            filterBuilder.Eq(entity => entity.Status, true),
            filterBuilder.Eq(entity => entity.UserId, userId),
            filterBuilder.Eq(entity => entity.ModuleKey, moduleKey),
            filterBuilder.Eq(entity => entity.PageKey, pageKey),
            filterBuilder.Eq(entity => entity.IsDefault, true));

        if (!string.IsNullOrWhiteSpace(excludeId))
        {
            filter = filterBuilder.And(filter, filterBuilder.Ne(entity => entity.Id, excludeId));
        }

        var update = Builders<SavedView>.Update
            .Set(entity => entity.IsDefault, false)
            .Set(entity => entity.ModifiedDate, DateTime.UtcNow);

        await Collection.UpdateManyAsync(filter, update, cancellationToken: ct);
    }
}
