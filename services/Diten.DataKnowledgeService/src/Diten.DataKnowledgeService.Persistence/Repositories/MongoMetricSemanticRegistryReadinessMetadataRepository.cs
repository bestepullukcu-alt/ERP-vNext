using Diten.DataKnowledgeService.Domain.Entities;
using Diten.DataKnowledgeService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.DataKnowledgeService.Persistence.Repositories;

public sealed class MongoMetricSemanticRegistryReadinessMetadataRepository : IMetricSemanticRegistryReadinessMetadataRepository
{
    public const string CollectionName = "dki_metric_semantic_registry_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_dki_metric_semantic_registry_tenant_code_active";
    public const string TenantStateIndexName = "ix_dki_metric_semantic_registry_tenant_state";

    private readonly IMongoCollection<MetricSemanticRegistryReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoMetricSemanticRegistryReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<MetricSemanticRegistryReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<MetricSemanticRegistryReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveTenantFilter(tenantId))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<MetricSemanticRegistryReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<MetricSemanticRegistryReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<MetricSemanticRegistryReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<MetricSemanticRegistryReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<MetricSemanticRegistryReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<MetricSemanticRegistryReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(MetricSemanticRegistryReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(MetricSemanticRegistryReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<MetricSemanticRegistryReadinessMetadata>.Filter.And(
            Builders<MetricSemanticRegistryReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<MetricSemanticRegistryReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<MetricSemanticRegistryReadinessMetadata>(
                Builders<MetricSemanticRegistryReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<MetricSemanticRegistryReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<MetricSemanticRegistryReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<MetricSemanticRegistryReadinessMetadata>(
                Builders<MetricSemanticRegistryReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.MetricSemanticRegistryReadinessState)
                    .Ascending(x => x.MetricIdentityCatalogBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    private static FilterDefinition<MetricSemanticRegistryReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<MetricSemanticRegistryReadinessMetadata>.Filter.And(
            Builders<MetricSemanticRegistryReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<MetricSemanticRegistryReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
