using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.HumanCapitalService.Persistence.Repositories;

public sealed class MongoHrKpiAnalyticsReadinessMetadataRepository : IHrKpiAnalyticsReadinessMetadataRepository
{
    public const string CollectionName = "hcm_hr_kpi_analytics_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_hcm_hr_kpi_analytics_tenant_code_active";
    public const string TenantStateIndexName = "ix_hcm_hr_kpi_analytics_tenant_state";

    private readonly IMongoCollection<HrKpiAnalyticsReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoHrKpiAnalyticsReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<HrKpiAnalyticsReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<HrKpiAnalyticsReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveTenantFilter(tenantId))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<HrKpiAnalyticsReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<HrKpiAnalyticsReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<HrKpiAnalyticsReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<HrKpiAnalyticsReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<HrKpiAnalyticsReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<HrKpiAnalyticsReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(HrKpiAnalyticsReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(HrKpiAnalyticsReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<HrKpiAnalyticsReadinessMetadata>.Filter.And(
            Builders<HrKpiAnalyticsReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<HrKpiAnalyticsReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<HrKpiAnalyticsReadinessMetadata>(
                Builders<HrKpiAnalyticsReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<HrKpiAnalyticsReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<HrKpiAnalyticsReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<HrKpiAnalyticsReadinessMetadata>(
                Builders<HrKpiAnalyticsReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.HrKpiAnalyticsReadinessState)
                    .Ascending(x => x.KpiCatalogBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    private static FilterDefinition<HrKpiAnalyticsReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<HrKpiAnalyticsReadinessMetadata>.Filter.And(
            Builders<HrKpiAnalyticsReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<HrKpiAnalyticsReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
