using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.TalentEcosystemService.Persistence.Repositories;

public sealed class MongoAssociationOperationsReadinessMetadataRepository : IAssociationOperationsReadinessMetadataRepository
{
    public const string CollectionName = "tep_association_operations_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_tep_association_operations_tenant_code_active";
    public const string TenantStateIndexName = "ix_tep_association_operations_tenant_state";

    private readonly IMongoCollection<AssociationOperationsReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoAssociationOperationsReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<AssociationOperationsReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<AssociationOperationsReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveTenantFilter(tenantId))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<AssociationOperationsReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<AssociationOperationsReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<AssociationOperationsReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<AssociationOperationsReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<AssociationOperationsReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<AssociationOperationsReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(AssociationOperationsReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(AssociationOperationsReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<AssociationOperationsReadinessMetadata>.Filter.And(
            Builders<AssociationOperationsReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<AssociationOperationsReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<AssociationOperationsReadinessMetadata>(
                Builders<AssociationOperationsReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<AssociationOperationsReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<AssociationOperationsReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<AssociationOperationsReadinessMetadata>(
                Builders<AssociationOperationsReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.AssociationOperationsReadinessState)
                    .Ascending(x => x.MembershipCatalogBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    private static FilterDefinition<AssociationOperationsReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<AssociationOperationsReadinessMetadata>.Filter.And(
            Builders<AssociationOperationsReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<AssociationOperationsReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
