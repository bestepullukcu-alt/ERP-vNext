using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.TalentEcosystemService.Persistence.Repositories;

public sealed class MongoProfessionalReputationLedgerReadinessMetadataRepository : IProfessionalReputationLedgerReadinessMetadataRepository
{
    public const string CollectionName = "tep_professional_reputation_ledger_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_tep_professional_reputation_ledger_tenant_code_active";
    public const string TenantStateIndexName = "ix_tep_professional_reputation_ledger_tenant_state";

    private readonly IMongoCollection<ProfessionalReputationLedgerReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoProfessionalReputationLedgerReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<ProfessionalReputationLedgerReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<ProfessionalReputationLedgerReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveScopeFilter(tenantId, legalEntityIds))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<ProfessionalReputationLedgerReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<ProfessionalReputationLedgerReadinessMetadata>.Filter.And(
            ActiveScopeFilter(tenantId, legalEntityIds),
            Builders<ProfessionalReputationLedgerReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<ProfessionalReputationLedgerReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<ProfessionalReputationLedgerReadinessMetadata>.Filter.Eq(x => x.LegalEntityId, legalEntityId),
            Builders<ProfessionalReputationLedgerReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<ProfessionalReputationLedgerReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(ProfessionalReputationLedgerReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(ProfessionalReputationLedgerReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<ProfessionalReputationLedgerReadinessMetadata>.Filter.And(
            Builders<ProfessionalReputationLedgerReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<ProfessionalReputationLedgerReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<ProfessionalReputationLedgerReadinessMetadata>(
                Builders<ProfessionalReputationLedgerReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<ProfessionalReputationLedgerReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<ProfessionalReputationLedgerReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<ProfessionalReputationLedgerReadinessMetadata>(
                Builders<ProfessionalReputationLedgerReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.ProfessionalReputationLedgerReadinessState)
                    .Ascending(x => x.ReputationSignalCatalogBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    // Tenant scoping stays authoritative; legal-entity scoping narrows to the effective
    // roll-up set. An empty set matches nothing (In []), so a caller with no scope sees none.
    private static FilterDefinition<ProfessionalReputationLedgerReadinessMetadata> ActiveScopeFilter(
        Guid tenantId,
        IReadOnlyCollection<Guid> legalEntityIds) =>
        Builders<ProfessionalReputationLedgerReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<ProfessionalReputationLedgerReadinessMetadata>.Filter.In(x => x.LegalEntityId, legalEntityIds));

    private static FilterDefinition<ProfessionalReputationLedgerReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<ProfessionalReputationLedgerReadinessMetadata>.Filter.And(
            Builders<ProfessionalReputationLedgerReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<ProfessionalReputationLedgerReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
