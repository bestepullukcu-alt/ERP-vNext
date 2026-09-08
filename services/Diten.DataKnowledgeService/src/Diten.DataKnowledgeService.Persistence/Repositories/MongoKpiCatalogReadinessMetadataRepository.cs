using Diten.DataKnowledgeService.Domain.Entities;
using Diten.DataKnowledgeService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.DataKnowledgeService.Persistence.Repositories;

public sealed class MongoKpiCatalogReadinessMetadataRepository : IKpiCatalogReadinessMetadataRepository
{
    public const string CollectionName = "dki_kpi_catalog_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_dki_kpi_catalog_tenant_code_active";
    public const string TenantStateIndexName = "ix_dki_kpi_catalog_tenant_state";

    private readonly IMongoCollection<KpiCatalogReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoKpiCatalogReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<KpiCatalogReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<KpiCatalogReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveTenantFilter(tenantId))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<KpiCatalogReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<KpiCatalogReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<KpiCatalogReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<KpiCatalogReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<KpiCatalogReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<KpiCatalogReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(KpiCatalogReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(KpiCatalogReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<KpiCatalogReadinessMetadata>.Filter.And(
            Builders<KpiCatalogReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<KpiCatalogReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<KpiCatalogReadinessMetadata>(
                Builders<KpiCatalogReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<KpiCatalogReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<KpiCatalogReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<KpiCatalogReadinessMetadata>(
                Builders<KpiCatalogReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.KpiCatalogReadinessState)
                    .Ascending(x => x.KpiIdentityCatalogBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    private static FilterDefinition<KpiCatalogReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<KpiCatalogReadinessMetadata>.Filter.And(
            Builders<KpiCatalogReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<KpiCatalogReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
