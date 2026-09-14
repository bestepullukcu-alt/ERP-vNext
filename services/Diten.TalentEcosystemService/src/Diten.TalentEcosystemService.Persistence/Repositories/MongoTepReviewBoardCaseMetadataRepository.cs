using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.TalentEcosystemService.Persistence.Repositories;

public sealed class MongoTepReviewBoardCaseMetadataRepository : ITepReviewBoardCaseMetadataRepository
{
    public const string CollectionName = "tep_review_board_cases";
    public const string ActiveCodeUniqueIndexName = "ux_tep_review_board_cases_tenant_code_active";
    public const string TenantStateIndexName = "ix_tep_review_board_cases_tenant_state";

    private readonly IMongoCollection<TepReviewBoardCaseMetadata> _collection;
    private bool _indexesEnsured;

    public MongoTepReviewBoardCaseMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<TepReviewBoardCaseMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<TepReviewBoardCaseMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveScopeFilter(tenantId, legalEntityIds))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<TepReviewBoardCaseMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TepReviewBoardCaseMetadata>.Filter.And(
            ActiveScopeFilter(tenantId, legalEntityIds),
            Builders<TepReviewBoardCaseMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TepReviewBoardCaseMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<TepReviewBoardCaseMetadata>.Filter.Eq(x => x.LegalEntityId, legalEntityId),
            Builders<TepReviewBoardCaseMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<TepReviewBoardCaseMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(TepReviewBoardCaseMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(TepReviewBoardCaseMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TepReviewBoardCaseMetadata>.Filter.And(
            Builders<TepReviewBoardCaseMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<TepReviewBoardCaseMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<TepReviewBoardCaseMetadata>(
                Builders<TepReviewBoardCaseMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<TepReviewBoardCaseMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<TepReviewBoardCaseMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<TepReviewBoardCaseMetadata>(
                Builders<TepReviewBoardCaseMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.ReviewBoardCaseState)
                    .Ascending(x => x.ReviewDecisionState)
                    .Ascending(x => x.ReviewerEligibilityState)
                    .Ascending(x => x.SegregationOfDutiesState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    // Tenant scoping stays authoritative; legal-entity scoping narrows to the effective
    // roll-up set. An empty set matches nothing (In []), so a caller with no scope sees none.
    private static FilterDefinition<TepReviewBoardCaseMetadata> ActiveScopeFilter(
        Guid tenantId,
        IReadOnlyCollection<Guid> legalEntityIds) =>
        Builders<TepReviewBoardCaseMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<TepReviewBoardCaseMetadata>.Filter.In(x => x.LegalEntityId, legalEntityIds));

    private static FilterDefinition<TepReviewBoardCaseMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<TepReviewBoardCaseMetadata>.Filter.And(
            Builders<TepReviewBoardCaseMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<TepReviewBoardCaseMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
