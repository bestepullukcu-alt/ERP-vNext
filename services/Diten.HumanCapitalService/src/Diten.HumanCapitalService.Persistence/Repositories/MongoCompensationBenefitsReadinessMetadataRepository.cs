using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.HumanCapitalService.Persistence.Repositories;

public sealed class MongoCompensationBenefitsReadinessMetadataRepository : ICompensationBenefitsReadinessMetadataRepository
{
    public const string CollectionName = "hcm_compensation_benefits_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_hcm_compensation_benefits_tenant_code_active";
    public const string TenantStateIndexName = "ix_hcm_compensation_benefits_tenant_state";

    private readonly IMongoCollection<CompensationBenefitsReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoCompensationBenefitsReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<CompensationBenefitsReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<CompensationBenefitsReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveScopeFilter(tenantId, legalEntityIds))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<CompensationBenefitsReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<CompensationBenefitsReadinessMetadata>.Filter.And(
            ActiveScopeFilter(tenantId, legalEntityIds),
            Builders<CompensationBenefitsReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<CompensationBenefitsReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<CompensationBenefitsReadinessMetadata>.Filter.Eq(x => x.LegalEntityId, legalEntityId),
            Builders<CompensationBenefitsReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<CompensationBenefitsReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(CompensationBenefitsReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(CompensationBenefitsReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<CompensationBenefitsReadinessMetadata>.Filter.And(
            Builders<CompensationBenefitsReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<CompensationBenefitsReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<CompensationBenefitsReadinessMetadata>(
                Builders<CompensationBenefitsReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<CompensationBenefitsReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<CompensationBenefitsReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<CompensationBenefitsReadinessMetadata>(
                Builders<CompensationBenefitsReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.CompensationBenefitsReadinessState)
                    .Ascending(x => x.CompensationPlanBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    // Tenant scoping stays authoritative; legal-entity scoping narrows to the effective
    // roll-up set. An empty set matches nothing (In []), so a caller with no scope sees none.
    private static FilterDefinition<CompensationBenefitsReadinessMetadata> ActiveScopeFilter(
        Guid tenantId,
        IReadOnlyCollection<Guid> legalEntityIds) =>
        Builders<CompensationBenefitsReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<CompensationBenefitsReadinessMetadata>.Filter.In(x => x.LegalEntityId, legalEntityIds));

    private static FilterDefinition<CompensationBenefitsReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<CompensationBenefitsReadinessMetadata>.Filter.And(
            Builders<CompensationBenefitsReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<CompensationBenefitsReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
