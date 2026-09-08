using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.HumanCapitalService.Persistence.Repositories;

public sealed class MongoHrCaseManagementReadinessMetadataRepository : IHrCaseManagementReadinessMetadataRepository
{
    public const string CollectionName = "hcm_hr_case_management_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_hcm_hr_case_management_tenant_code_active";
    public const string TenantStateIndexName = "ix_hcm_hr_case_management_tenant_state";

    private readonly IMongoCollection<HrCaseManagementReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoHrCaseManagementReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<HrCaseManagementReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<HrCaseManagementReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveTenantFilter(tenantId))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<HrCaseManagementReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<HrCaseManagementReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<HrCaseManagementReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<HrCaseManagementReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<HrCaseManagementReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<HrCaseManagementReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(HrCaseManagementReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(HrCaseManagementReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<HrCaseManagementReadinessMetadata>.Filter.And(
            Builders<HrCaseManagementReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<HrCaseManagementReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<HrCaseManagementReadinessMetadata>(
                Builders<HrCaseManagementReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<HrCaseManagementReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<HrCaseManagementReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<HrCaseManagementReadinessMetadata>(
                Builders<HrCaseManagementReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.HrCaseManagementReadinessState)
                    .Ascending(x => x.CaseIntakeBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    private static FilterDefinition<HrCaseManagementReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<HrCaseManagementReadinessMetadata>.Filter.And(
            Builders<HrCaseManagementReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<HrCaseManagementReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
