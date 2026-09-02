using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.TalentEcosystemService.Persistence.Repositories;

public sealed class MongoTepCandidateDisputeReadinessMetadataRepository
    : ITepCandidateDisputeReadinessMetadataRepository
{
    public const string CollectionName = "tep_candidate_dispute_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_tep_candidate_dispute_readiness_tenant_code_active";
    public const string TenantStateIndexName = "ix_tep_candidate_dispute_readiness_tenant_state";

    private readonly IMongoCollection<TepCandidateDisputeReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoTepCandidateDisputeReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<TepCandidateDisputeReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<TepCandidateDisputeReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveTenantFilter(tenantId))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<TepCandidateDisputeReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TepCandidateDisputeReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<TepCandidateDisputeReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TepCandidateDisputeReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<TepCandidateDisputeReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<TepCandidateDisputeReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(TepCandidateDisputeReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(TepCandidateDisputeReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TepCandidateDisputeReadinessMetadata>.Filter.And(
            Builders<TepCandidateDisputeReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<TepCandidateDisputeReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<TepCandidateDisputeReadinessMetadata>(
                Builders<TepCandidateDisputeReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<TepCandidateDisputeReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<TepCandidateDisputeReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<TepCandidateDisputeReadinessMetadata>(
                Builders<TepCandidateDisputeReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.DisputeReadinessState)
                    .Ascending(x => x.ResponseBoundaryState)
                    .Ascending(x => x.DisputeIntakeState)
                    .Ascending(x => x.DisputeReviewState)
                    .Ascending(x => x.ConsentPreconditionState)
                    .Ascending(x => x.VisibilityApprovalState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    private static FilterDefinition<TepCandidateDisputeReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<TepCandidateDisputeReadinessMetadata>.Filter.And(
            Builders<TepCandidateDisputeReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<TepCandidateDisputeReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
