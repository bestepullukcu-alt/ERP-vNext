using Diten.DataKnowledgeService.Domain.Entities;
using Diten.DataKnowledgeService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.DataKnowledgeService.Persistence.Repositories;

public sealed class MongoDataWarehouseLakehouseReadinessMetadataRepository : IDataWarehouseLakehouseReadinessMetadataRepository
{
    public const string CollectionName = "dki_data_warehouse_lakehouse_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_dki_data_warehouse_lakehouse_tenant_code_active";
    public const string TenantStateIndexName = "ix_dki_data_warehouse_lakehouse_tenant_state";

    private readonly IMongoCollection<DataWarehouseLakehouseReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoDataWarehouseLakehouseReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<DataWarehouseLakehouseReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<DataWarehouseLakehouseReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveTenantFilter(tenantId))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<DataWarehouseLakehouseReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<DataWarehouseLakehouseReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<DataWarehouseLakehouseReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<DataWarehouseLakehouseReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<DataWarehouseLakehouseReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<DataWarehouseLakehouseReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(DataWarehouseLakehouseReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(DataWarehouseLakehouseReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<DataWarehouseLakehouseReadinessMetadata>.Filter.And(
            Builders<DataWarehouseLakehouseReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<DataWarehouseLakehouseReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<DataWarehouseLakehouseReadinessMetadata>(
                Builders<DataWarehouseLakehouseReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<DataWarehouseLakehouseReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<DataWarehouseLakehouseReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<DataWarehouseLakehouseReadinessMetadata>(
                Builders<DataWarehouseLakehouseReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.DataWarehouseLakehouseReadinessState)
                    .Ascending(x => x.StorageLayerCatalogBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    private static FilterDefinition<DataWarehouseLakehouseReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<DataWarehouseLakehouseReadinessMetadata>.Filter.And(
            Builders<DataWarehouseLakehouseReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<DataWarehouseLakehouseReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
