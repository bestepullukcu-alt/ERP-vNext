using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.TalentEcosystemService.Persistence.Repositories;

public sealed class MongoMentorshipRecommendationNetworkReadinessMetadataRepository : IMentorshipRecommendationNetworkReadinessMetadataRepository
{
    public const string CollectionName = "tep_mentorship_recommendation_network_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_tep_mentorship_recommendation_network_tenant_code_active";
    public const string TenantStateIndexName = "ix_tep_mentorship_recommendation_network_tenant_state";

    private readonly IMongoCollection<MentorshipRecommendationNetworkReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoMentorshipRecommendationNetworkReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<MentorshipRecommendationNetworkReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<MentorshipRecommendationNetworkReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveTenantFilter(tenantId))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<MentorshipRecommendationNetworkReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<MentorshipRecommendationNetworkReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<MentorshipRecommendationNetworkReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<MentorshipRecommendationNetworkReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<MentorshipRecommendationNetworkReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<MentorshipRecommendationNetworkReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(MentorshipRecommendationNetworkReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(MentorshipRecommendationNetworkReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<MentorshipRecommendationNetworkReadinessMetadata>.Filter.And(
            Builders<MentorshipRecommendationNetworkReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<MentorshipRecommendationNetworkReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<MentorshipRecommendationNetworkReadinessMetadata>(
                Builders<MentorshipRecommendationNetworkReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<MentorshipRecommendationNetworkReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<MentorshipRecommendationNetworkReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<MentorshipRecommendationNetworkReadinessMetadata>(
                Builders<MentorshipRecommendationNetworkReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.MentorshipRecommendationNetworkReadinessState)
                    .Ascending(x => x.NetworkCatalogBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    private static FilterDefinition<MentorshipRecommendationNetworkReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<MentorshipRecommendationNetworkReadinessMetadata>.Filter.And(
            Builders<MentorshipRecommendationNetworkReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<MentorshipRecommendationNetworkReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
