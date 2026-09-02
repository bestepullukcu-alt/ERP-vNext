using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.TalentEcosystemService.Persistence.Repositories;

public sealed class MongoTepCandidateProfileMetadataRepository : ITepCandidateProfileMetadataRepository
{
    public const string CollectionName = "tep_candidate_profiles";
    public const string ActiveCodeUniqueIndexName = "ux_tep_candidate_profiles_tenant_code_active";
    public const string TenantStateIndexName = "ix_tep_candidate_profiles_tenant_state";

    private readonly IMongoCollection<TepCandidateProfileMetadata> _collection;
    private bool _indexesEnsured;

    public MongoTepCandidateProfileMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<TepCandidateProfileMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<TepCandidateProfileMetadata>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveTenantFilter(tenantId))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<TepCandidateProfileMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TepCandidateProfileMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<TepCandidateProfileMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TepCandidateProfileMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<TepCandidateProfileMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<TepCandidateProfileMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(TepCandidateProfileMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(TepCandidateProfileMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TepCandidateProfileMetadata>.Filter.And(
            Builders<TepCandidateProfileMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<TepCandidateProfileMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<TepCandidateProfileMetadata>(
                Builders<TepCandidateProfileMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<TepCandidateProfileMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<TepCandidateProfileMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<TepCandidateProfileMetadata>(
                Builders<TepCandidateProfileMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.CandidateIdentityState)
                    .Ascending(x => x.TalentProfileState)
                    .Ascending(x => x.PolicyEvaluationState)
                    .Ascending(x => x.VisibilityApprovalState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    private static FilterDefinition<TepCandidateProfileMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<TepCandidateProfileMetadata>.Filter.And(
            Builders<TepCandidateProfileMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<TepCandidateProfileMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
