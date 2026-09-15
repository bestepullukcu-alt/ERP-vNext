using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.TalentEcosystemService.Persistence.Repositories;

public sealed class MongoIndustrySkillPassportReadinessMetadataRepository : IIndustrySkillPassportReadinessMetadataRepository
{
    public const string CollectionName = "tep_industry_skill_passport_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_tep_industry_skill_passport_tenant_code_active";
    public const string TenantStateIndexName = "ix_tep_industry_skill_passport_tenant_state";

    private readonly IMongoCollection<IndustrySkillPassportReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoIndustrySkillPassportReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<IndustrySkillPassportReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<IndustrySkillPassportReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveScopeFilter(tenantId, legalEntityIds))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<IndustrySkillPassportReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<IndustrySkillPassportReadinessMetadata>.Filter.And(
            ActiveScopeFilter(tenantId, legalEntityIds),
            Builders<IndustrySkillPassportReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<IndustrySkillPassportReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<IndustrySkillPassportReadinessMetadata>.Filter.Eq(x => x.LegalEntityId, legalEntityId),
            Builders<IndustrySkillPassportReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<IndustrySkillPassportReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(IndustrySkillPassportReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(IndustrySkillPassportReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<IndustrySkillPassportReadinessMetadata>.Filter.And(
            Builders<IndustrySkillPassportReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<IndustrySkillPassportReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<IndustrySkillPassportReadinessMetadata>(
                Builders<IndustrySkillPassportReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<IndustrySkillPassportReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<IndustrySkillPassportReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<IndustrySkillPassportReadinessMetadata>(
                Builders<IndustrySkillPassportReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.IndustrySkillPassportReadinessState)
                    .Ascending(x => x.SkillClaimCatalogBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    // Tenant scoping stays authoritative; legal-entity scoping narrows to the effective
    // roll-up set. An empty set matches nothing (In []), so a caller with no scope sees none.
    private static FilterDefinition<IndustrySkillPassportReadinessMetadata> ActiveScopeFilter(
        Guid tenantId,
        IReadOnlyCollection<Guid> legalEntityIds) =>
        Builders<IndustrySkillPassportReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<IndustrySkillPassportReadinessMetadata>.Filter.In(x => x.LegalEntityId, legalEntityIds));

    private static FilterDefinition<IndustrySkillPassportReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<IndustrySkillPassportReadinessMetadata>.Filter.And(
            Builders<IndustrySkillPassportReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<IndustrySkillPassportReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
