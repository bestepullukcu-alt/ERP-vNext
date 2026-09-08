using Diten.DataKnowledgeService.Domain.Entities;
using Diten.DataKnowledgeService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.DataKnowledgeService.Persistence.Repositories;

public sealed class MongoBaselineExperimentMeasurementReadinessMetadataRepository : IBaselineExperimentMeasurementReadinessMetadataRepository
{
    public const string CollectionName = "dki_baseline_experiment_measurement_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_dki_baseline_experiment_measurement_tenant_code_active";
    public const string TenantStateIndexName = "ix_dki_baseline_experiment_measurement_tenant_state";

    private readonly IMongoCollection<BaselineExperimentMeasurementReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoBaselineExperimentMeasurementReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<BaselineExperimentMeasurementReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<BaselineExperimentMeasurementReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveTenantFilter(tenantId))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<BaselineExperimentMeasurementReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<BaselineExperimentMeasurementReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<BaselineExperimentMeasurementReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<BaselineExperimentMeasurementReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<BaselineExperimentMeasurementReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<BaselineExperimentMeasurementReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(BaselineExperimentMeasurementReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(BaselineExperimentMeasurementReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<BaselineExperimentMeasurementReadinessMetadata>.Filter.And(
            Builders<BaselineExperimentMeasurementReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<BaselineExperimentMeasurementReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<BaselineExperimentMeasurementReadinessMetadata>(
                Builders<BaselineExperimentMeasurementReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<BaselineExperimentMeasurementReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<BaselineExperimentMeasurementReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<BaselineExperimentMeasurementReadinessMetadata>(
                Builders<BaselineExperimentMeasurementReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.BaselineExperimentMeasurementReadinessState)
                    .Ascending(x => x.BaselineCatalogBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    private static FilterDefinition<BaselineExperimentMeasurementReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<BaselineExperimentMeasurementReadinessMetadata>.Filter.And(
            Builders<BaselineExperimentMeasurementReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<BaselineExperimentMeasurementReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
