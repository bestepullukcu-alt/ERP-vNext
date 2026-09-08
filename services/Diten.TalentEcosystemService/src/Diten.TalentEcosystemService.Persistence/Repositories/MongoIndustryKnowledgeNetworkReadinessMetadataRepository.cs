using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.TalentEcosystemService.Persistence.Repositories;

public sealed class MongoIndustryKnowledgeNetworkReadinessMetadataRepository : IIndustryKnowledgeNetworkReadinessMetadataRepository
{
    public const string CollectionName = "tep_industry_knowledge_network_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_tep_industry_knowledge_network_tenant_code_active";
    public const string TenantStateIndexName = "ix_tep_industry_knowledge_network_tenant_state";

    private readonly IMongoCollection<IndustryKnowledgeNetworkReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoIndustryKnowledgeNetworkReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<IndustryKnowledgeNetworkReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<IndustryKnowledgeNetworkReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveTenantFilter(tenantId))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<IndustryKnowledgeNetworkReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<IndustryKnowledgeNetworkReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<IndustryKnowledgeNetworkReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<IndustryKnowledgeNetworkReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<IndustryKnowledgeNetworkReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<IndustryKnowledgeNetworkReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(IndustryKnowledgeNetworkReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(IndustryKnowledgeNetworkReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<IndustryKnowledgeNetworkReadinessMetadata>.Filter.And(
            Builders<IndustryKnowledgeNetworkReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<IndustryKnowledgeNetworkReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<IndustryKnowledgeNetworkReadinessMetadata>(
                Builders<IndustryKnowledgeNetworkReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<IndustryKnowledgeNetworkReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<IndustryKnowledgeNetworkReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<IndustryKnowledgeNetworkReadinessMetadata>(
                Builders<IndustryKnowledgeNetworkReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.IndustryKnowledgeNetworkReadinessState)
                    .Ascending(x => x.KnowledgeCatalogBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    private static FilterDefinition<IndustryKnowledgeNetworkReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<IndustryKnowledgeNetworkReadinessMetadata>.Filter.And(
            Builders<IndustryKnowledgeNetworkReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<IndustryKnowledgeNetworkReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
