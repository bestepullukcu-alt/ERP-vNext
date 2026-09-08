using Diten.DataKnowledgeService.Domain.Entities;
using Diten.DataKnowledgeService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.DataKnowledgeService.Persistence.Repositories;

public sealed class MongoScorecardsDashboardsReadinessMetadataRepository : IScorecardsDashboardsReadinessMetadataRepository
{
    public const string CollectionName = "dki_scorecards_dashboards_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_dki_scorecards_dashboards_tenant_code_active";
    public const string TenantStateIndexName = "ix_dki_scorecards_dashboards_tenant_state";

    private readonly IMongoCollection<ScorecardsDashboardsReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoScorecardsDashboardsReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<ScorecardsDashboardsReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<ScorecardsDashboardsReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveTenantFilter(tenantId))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<ScorecardsDashboardsReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<ScorecardsDashboardsReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<ScorecardsDashboardsReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<ScorecardsDashboardsReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<ScorecardsDashboardsReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<ScorecardsDashboardsReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(ScorecardsDashboardsReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(ScorecardsDashboardsReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<ScorecardsDashboardsReadinessMetadata>.Filter.And(
            Builders<ScorecardsDashboardsReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<ScorecardsDashboardsReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<ScorecardsDashboardsReadinessMetadata>(
                Builders<ScorecardsDashboardsReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<ScorecardsDashboardsReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<ScorecardsDashboardsReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<ScorecardsDashboardsReadinessMetadata>(
                Builders<ScorecardsDashboardsReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.ScorecardsDashboardsReadinessState)
                    .Ascending(x => x.ScorecardCatalogBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    private static FilterDefinition<ScorecardsDashboardsReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<ScorecardsDashboardsReadinessMetadata>.Filter.And(
            Builders<ScorecardsDashboardsReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<ScorecardsDashboardsReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
