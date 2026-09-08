using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.TalentEcosystemService.Persistence.Repositories;

public sealed class MongoPayBenchmarkingReadinessMetadataRepository : IPayBenchmarkingReadinessMetadataRepository
{
    public const string CollectionName = "tep_salary_benchmarking_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_tep_salary_benchmarking_tenant_code_active";
    public const string TenantStateIndexName = "ix_tep_salary_benchmarking_tenant_state";

    private readonly IMongoCollection<PayBenchmarkingReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoPayBenchmarkingReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<PayBenchmarkingReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<PayBenchmarkingReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveTenantFilter(tenantId))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<PayBenchmarkingReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<PayBenchmarkingReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<PayBenchmarkingReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<PayBenchmarkingReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<PayBenchmarkingReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<PayBenchmarkingReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(PayBenchmarkingReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(PayBenchmarkingReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<PayBenchmarkingReadinessMetadata>.Filter.And(
            Builders<PayBenchmarkingReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<PayBenchmarkingReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<PayBenchmarkingReadinessMetadata>(
                Builders<PayBenchmarkingReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<PayBenchmarkingReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<PayBenchmarkingReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<PayBenchmarkingReadinessMetadata>(
                Builders<PayBenchmarkingReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.PayBenchmarkingReadinessState)
                    .Ascending(x => x.ReferenceRangeCatalogBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    private static FilterDefinition<PayBenchmarkingReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<PayBenchmarkingReadinessMetadata>.Filter.And(
            Builders<PayBenchmarkingReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<PayBenchmarkingReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
