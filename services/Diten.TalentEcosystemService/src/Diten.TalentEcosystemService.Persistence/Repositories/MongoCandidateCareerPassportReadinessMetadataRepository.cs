using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.TalentEcosystemService.Persistence.Repositories;

public sealed class MongoCandidateCareerPassportReadinessMetadataRepository : ICandidateCareerPassportReadinessMetadataRepository
{
    public const string CollectionName = "tep_candidate_career_passport_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_tep_candidate_career_passport_tenant_code_active";
    public const string TenantStateIndexName = "ix_tep_candidate_career_passport_tenant_state";

    private readonly IMongoCollection<CandidateCareerPassportReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoCandidateCareerPassportReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<CandidateCareerPassportReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<CandidateCareerPassportReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveTenantFilter(tenantId))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<CandidateCareerPassportReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<CandidateCareerPassportReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<CandidateCareerPassportReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<CandidateCareerPassportReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<CandidateCareerPassportReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<CandidateCareerPassportReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(CandidateCareerPassportReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(CandidateCareerPassportReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<CandidateCareerPassportReadinessMetadata>.Filter.And(
            Builders<CandidateCareerPassportReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<CandidateCareerPassportReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<CandidateCareerPassportReadinessMetadata>(
                Builders<CandidateCareerPassportReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<CandidateCareerPassportReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<CandidateCareerPassportReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<CandidateCareerPassportReadinessMetadata>(
                Builders<CandidateCareerPassportReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.CandidateCareerPassportReadinessState)
                    .Ascending(x => x.CareerMilestoneCatalogBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    private static FilterDefinition<CandidateCareerPassportReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<CandidateCareerPassportReadinessMetadata>.Filter.And(
            Builders<CandidateCareerPassportReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<CandidateCareerPassportReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
