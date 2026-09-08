using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.HumanCapitalService.Persistence.Repositories;

public sealed class MongoHrDocumentationReadinessMetadataRepository : IHrDocumentationReadinessMetadataRepository
{
    public const string CollectionName = "hcm_hr_documentation_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_hcm_hr_documentation_tenant_code_active";
    public const string TenantStateIndexName = "ix_hcm_hr_documentation_tenant_state";

    private readonly IMongoCollection<HrDocumentationReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoHrDocumentationReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<HrDocumentationReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<HrDocumentationReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveTenantFilter(tenantId))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<HrDocumentationReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<HrDocumentationReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<HrDocumentationReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<HrDocumentationReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<HrDocumentationReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<HrDocumentationReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(HrDocumentationReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(HrDocumentationReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<HrDocumentationReadinessMetadata>.Filter.And(
            Builders<HrDocumentationReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<HrDocumentationReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<HrDocumentationReadinessMetadata>(
                Builders<HrDocumentationReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<HrDocumentationReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<HrDocumentationReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<HrDocumentationReadinessMetadata>(
                Builders<HrDocumentationReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.HrDocumentationReadinessState)
                    .Ascending(x => x.DocumentWorkspaceBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    private static FilterDefinition<HrDocumentationReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<HrDocumentationReadinessMetadata>.Filter.And(
            Builders<HrDocumentationReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<HrDocumentationReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
