using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.TalentEcosystemService.Persistence.Repositories;

public sealed class MongoWorkforceAnalyticsReadinessMetadataRepository : IWorkforceAnalyticsReadinessMetadataRepository
{
    public const string CollectionName = "tep_workforce_analytics_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_tep_workforce_analytics_tenant_code_active";
    public const string TenantStateIndexName = "ix_tep_workforce_analytics_tenant_state";

    private readonly IMongoCollection<WorkforceAnalyticsReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoWorkforceAnalyticsReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<WorkforceAnalyticsReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<WorkforceAnalyticsReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveTenantFilter(tenantId))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<WorkforceAnalyticsReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<WorkforceAnalyticsReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<WorkforceAnalyticsReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<WorkforceAnalyticsReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<WorkforceAnalyticsReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<WorkforceAnalyticsReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(WorkforceAnalyticsReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(WorkforceAnalyticsReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<WorkforceAnalyticsReadinessMetadata>.Filter.And(
            Builders<WorkforceAnalyticsReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<WorkforceAnalyticsReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
        await _collection.ReplaceOneAsync(filter, metadata, cancellationToken: ct);
    }

    private async Task EnsureIndexesAsync(CancellationToken ct)
    {
        if (_indexesEnsured)
        {
            return;
        }

        await _collection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<WorkforceAnalyticsReadinessMetadata>(
                Builders<WorkforceAnalyticsReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<WorkforceAnalyticsReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<WorkforceAnalyticsReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<WorkforceAnalyticsReadinessMetadata>(
                Builders<WorkforceAnalyticsReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.WorkforceAnalyticsReadinessState)
                    .Ascending(x => x.AnalyticsCatalogBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    private static FilterDefinition<WorkforceAnalyticsReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<WorkforceAnalyticsReadinessMetadata>.Filter.And(
            Builders<WorkforceAnalyticsReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<WorkforceAnalyticsReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
