using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

/// <summary>
/// MOD-0028-FU02 tenant-scoped MongoDB persistence. All reads/writes are filtered by the tenant context via
/// <see cref="TenantRepository{TEntity}"/>; there is no hard delete.
/// </summary>
public sealed class BaselineReleaseRepository : TenantRepository<BaselineRelease>, IBaselineReleaseRepository
{
    public BaselineReleaseRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_baseline_releases")
    {
    }

    public async Task<bool> UpdateAsync(BaselineRelease baseline, int expectedVersion, CancellationToken ct = default)
    {
        baseline.Version = expectedVersion + 1;
        baseline.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<BaselineRelease>.Filter.And(
            ExecutionFilter,
            Builders<BaselineRelease>.Filter.Eq(x => x.Id, baseline.Id),
            Builders<BaselineRelease>.Filter.Eq(x => x.Version, expectedVersion));
        var result = await Collection.ReplaceOneAsync(filter, baseline, new ReplaceOptions(), ct);
        return result.IsAcknowledged && result.ModifiedCount == 1;
    }
}

// MOD-0028-FU09 provisioning-evidence + deviation repositories live in DocumentManagementProvisioningRepositories.cs.

public sealed class CollectionDefinitionRepository : TenantRepository<CollectionDefinition>, ICollectionDefinitionRepository
{
    public CollectionDefinitionRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_collection_definitions")
    {
    }

    public async Task CreateManyAsync(IReadOnlyList<CollectionDefinition> definitions, CancellationToken ct = default)
    {
        if (definitions.Count == 0)
        {
            return;
        }

        // TenantId is a required init-only member already set by the handler from the resolved tenant context.
        await Collection.InsertManyAsync(definitions, cancellationToken: ct);
    }

    public async Task<bool> UpdateAsync(CollectionDefinition definition, int expectedVersion, CancellationToken ct = default)
    {
        definition.Version = expectedVersion + 1;
        definition.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<CollectionDefinition>.Filter.And(
            ExecutionFilter,
            Builders<CollectionDefinition>.Filter.Eq(x => x.Id, definition.Id),
            Builders<CollectionDefinition>.Filter.Eq(x => x.Version, expectedVersion));
        var result = await Collection.ReplaceOneAsync(filter, definition, new ReplaceOptions(), ct);
        return result.IsAcknowledged && result.ModifiedCount == 1;
    }

    public async Task UpdateManyAsync(IReadOnlyList<CollectionDefinition> definitions, CancellationToken ct = default)
    {
        foreach (var definition in definitions)
        {
            var expectedVersion = definition.Version;
            definition.Version = expectedVersion + 1;
            definition.UpdatedAt = DateTimeOffset.UtcNow;
            var filter = Builders<CollectionDefinition>.Filter.And(
                ExecutionFilter,
                Builders<CollectionDefinition>.Filter.Eq(x => x.Id, definition.Id),
                Builders<CollectionDefinition>.Filter.Eq(x => x.Version, expectedVersion));
            await Collection.ReplaceOneAsync(filter, definition, new ReplaceOptions(), ct);
        }
    }

    public async Task<bool> SoftDeleteAsync(CollectionDefinition definition, int expectedVersion, CancellationToken ct = default)
    {
        var filter = Builders<CollectionDefinition>.Filter.And(
            ExecutionFilter,
            Builders<CollectionDefinition>.Filter.Eq(x => x.Id, definition.Id),
            Builders<CollectionDefinition>.Filter.Eq(x => x.Version, expectedVersion));
        var update = Builders<CollectionDefinition>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)
            .Set(x => x.Version, expectedVersion + 1);
        var result = await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        return result.IsAcknowledged && result.ModifiedCount == 1;
    }

    public async Task<IReadOnlyList<CollectionDefinition>> GetByBaselineAsync(Guid baselineReleaseId, CancellationToken ct = default)
    {
        var filter = Builders<CollectionDefinition>.Filter.And(
            ExecutionFilter,
            Builders<CollectionDefinition>.Filter.Eq(x => x.BaselineReleaseId, baselineReleaseId));
        return await Collection.Find(filter).SortBy(x => x.DisplayOrder).ToListAsync(ct);
    }

    public Task<CollectionDefinition?> GetByCanonicalIdAsync(Guid baselineReleaseId, string canonicalId, CancellationToken ct = default)
    {
        var filter = Builders<CollectionDefinition>.Filter.And(
            ExecutionFilter,
            Builders<CollectionDefinition>.Filter.Eq(x => x.BaselineReleaseId, baselineReleaseId),
            Builders<CollectionDefinition>.Filter.Eq(x => x.CanonicalId, canonicalId));
        return Collection.Find(filter).FirstOrDefaultAsync(ct)!;
    }
}

public sealed class BaselineSnapshotManifestRepository : TenantRepository<BaselineSnapshotManifest>, IBaselineSnapshotManifestRepository
{
    public BaselineSnapshotManifestRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_baseline_snapshot_manifests")
    {
    }

    public Task<BaselineSnapshotManifest?> GetByBaselineAsync(Guid baselineReleaseId, CancellationToken ct = default)
    {
        var filter = Builders<BaselineSnapshotManifest>.Filter.And(
            ExecutionFilter,
            Builders<BaselineSnapshotManifest>.Filter.Eq(x => x.BaselineReleaseId, baselineReleaseId));
        return Collection.Find(filter).FirstOrDefaultAsync(ct)!;
    }
}
