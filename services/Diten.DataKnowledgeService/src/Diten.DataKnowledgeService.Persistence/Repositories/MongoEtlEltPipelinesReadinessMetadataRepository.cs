using Diten.DataKnowledgeService.Domain.Entities;
using Diten.DataKnowledgeService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.DataKnowledgeService.Persistence.Repositories;

public sealed class MongoEtlEltPipelinesReadinessMetadataRepository : IEtlEltPipelinesReadinessMetadataRepository
{
    public const string CollectionName = "dki_etl_elt_pipelines_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_dki_etl_elt_pipelines_tenant_code_active";
    public const string TenantStateIndexName = "ix_dki_etl_elt_pipelines_tenant_state";

    private readonly IMongoCollection<EtlEltPipelinesReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoEtlEltPipelinesReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<EtlEltPipelinesReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<EtlEltPipelinesReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveTenantFilter(tenantId))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<EtlEltPipelinesReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<EtlEltPipelinesReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<EtlEltPipelinesReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<EtlEltPipelinesReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<EtlEltPipelinesReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<EtlEltPipelinesReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(EtlEltPipelinesReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(EtlEltPipelinesReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<EtlEltPipelinesReadinessMetadata>.Filter.And(
            Builders<EtlEltPipelinesReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<EtlEltPipelinesReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<EtlEltPipelinesReadinessMetadata>(
                Builders<EtlEltPipelinesReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<EtlEltPipelinesReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<EtlEltPipelinesReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<EtlEltPipelinesReadinessMetadata>(
                Builders<EtlEltPipelinesReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.EtlEltPipelinesReadinessState)
                    .Ascending(x => x.PipelineDefinitionCatalogBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    private static FilterDefinition<EtlEltPipelinesReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<EtlEltPipelinesReadinessMetadata>.Filter.And(
            Builders<EtlEltPipelinesReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<EtlEltPipelinesReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
