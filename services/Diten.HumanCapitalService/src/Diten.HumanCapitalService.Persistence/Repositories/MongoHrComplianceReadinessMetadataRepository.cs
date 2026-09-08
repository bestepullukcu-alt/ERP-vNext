using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.HumanCapitalService.Persistence.Repositories;

public sealed class MongoHrComplianceReadinessMetadataRepository : IHrComplianceReadinessMetadataRepository
{
    public const string CollectionName = "hcm_hr_compliance_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_hcm_hr_compliance_tenant_code_active";
    public const string TenantStateIndexName = "ix_hcm_hr_compliance_tenant_state";

    private readonly IMongoCollection<HrComplianceReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoHrComplianceReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<HrComplianceReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<HrComplianceReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveTenantFilter(tenantId))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<HrComplianceReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<HrComplianceReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<HrComplianceReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<HrComplianceReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<HrComplianceReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<HrComplianceReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(HrComplianceReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(HrComplianceReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<HrComplianceReadinessMetadata>.Filter.And(
            Builders<HrComplianceReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<HrComplianceReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<HrComplianceReadinessMetadata>(
                Builders<HrComplianceReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<HrComplianceReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<HrComplianceReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<HrComplianceReadinessMetadata>(
                Builders<HrComplianceReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.HrComplianceReadinessState)
                    .Ascending(x => x.ObligationCatalogBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    private static FilterDefinition<HrComplianceReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<HrComplianceReadinessMetadata>.Filter.And(
            Builders<HrComplianceReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<HrComplianceReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
