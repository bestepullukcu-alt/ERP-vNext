using Diten.DataKnowledgeService.Domain.Entities;
using Diten.DataKnowledgeService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.DataKnowledgeService.Persistence.Repositories;

public sealed class MongoMetricDefinitionsOwnershipReadinessMetadataRepository : IMetricDefinitionsOwnershipReadinessMetadataRepository
{
    public const string CollectionName = "dki_metric_definitions_ownership_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_dki_metric_definitions_ownership_tenant_code_active";
    public const string TenantStateIndexName = "ix_dki_metric_definitions_ownership_tenant_state";

    private readonly IMongoCollection<MetricDefinitionsOwnershipReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoMetricDefinitionsOwnershipReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<MetricDefinitionsOwnershipReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<MetricDefinitionsOwnershipReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveTenantFilter(tenantId))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<MetricDefinitionsOwnershipReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<MetricDefinitionsOwnershipReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<MetricDefinitionsOwnershipReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<MetricDefinitionsOwnershipReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<MetricDefinitionsOwnershipReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<MetricDefinitionsOwnershipReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(MetricDefinitionsOwnershipReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(MetricDefinitionsOwnershipReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<MetricDefinitionsOwnershipReadinessMetadata>.Filter.And(
            Builders<MetricDefinitionsOwnershipReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<MetricDefinitionsOwnershipReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<MetricDefinitionsOwnershipReadinessMetadata>(
                Builders<MetricDefinitionsOwnershipReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<MetricDefinitionsOwnershipReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<MetricDefinitionsOwnershipReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<MetricDefinitionsOwnershipReadinessMetadata>(
                Builders<MetricDefinitionsOwnershipReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.MetricDefinitionsOwnershipReadinessState)
                    .Ascending(x => x.DefinitionCatalogBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    private static FilterDefinition<MetricDefinitionsOwnershipReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<MetricDefinitionsOwnershipReadinessMetadata>.Filter.And(
            Builders<MetricDefinitionsOwnershipReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<MetricDefinitionsOwnershipReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
