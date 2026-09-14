using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.HumanCapitalService.Persistence.Repositories;

public sealed class MongoCandidatePipelineReadinessMetadataRepository : ICandidatePipelineReadinessMetadataRepository
{
    public const string CollectionName = "hcm_candidate_pipeline_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_hcm_candidate_pipeline_tenant_code_active";
    public const string TenantStateIndexName = "ix_hcm_candidate_pipeline_tenant_state";

    private readonly IMongoCollection<CandidatePipelineReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoCandidatePipelineReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<CandidatePipelineReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<CandidatePipelineReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveScopeFilter(tenantId, legalEntityIds))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<CandidatePipelineReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<CandidatePipelineReadinessMetadata>.Filter.And(
            ActiveScopeFilter(tenantId, legalEntityIds),
            Builders<CandidatePipelineReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<CandidatePipelineReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<CandidatePipelineReadinessMetadata>.Filter.Eq(x => x.LegalEntityId, legalEntityId),
            Builders<CandidatePipelineReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<CandidatePipelineReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(CandidatePipelineReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(CandidatePipelineReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<CandidatePipelineReadinessMetadata>.Filter.And(
            Builders<CandidatePipelineReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<CandidatePipelineReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<CandidatePipelineReadinessMetadata>(
                Builders<CandidatePipelineReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<CandidatePipelineReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<CandidatePipelineReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<CandidatePipelineReadinessMetadata>(
                Builders<CandidatePipelineReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.PipelineReadinessState)
                    .Ascending(x => x.PipelineStageGovernanceState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    // Tenant scoping stays authoritative; legal-entity scoping narrows to the effective
    // roll-up set. An empty set matches nothing (In []), so a caller with no scope sees none.
    private static FilterDefinition<CandidatePipelineReadinessMetadata> ActiveScopeFilter(
        Guid tenantId,
        IReadOnlyCollection<Guid> legalEntityIds) =>
        Builders<CandidatePipelineReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<CandidatePipelineReadinessMetadata>.Filter.In(x => x.LegalEntityId, legalEntityIds));

    private static FilterDefinition<CandidatePipelineReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<CandidatePipelineReadinessMetadata>.Filter.And(
            Builders<CandidatePipelineReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<CandidatePipelineReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
