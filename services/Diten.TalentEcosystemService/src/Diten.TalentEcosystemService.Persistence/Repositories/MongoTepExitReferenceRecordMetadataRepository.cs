using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.TalentEcosystemService.Persistence.Repositories;

public sealed class MongoTepExitReferenceRecordMetadataRepository : ITepExitReferenceRecordMetadataRepository
{
    public const string CollectionName = "tep_exit_reference_records";
    public const string ActiveCodeUniqueIndexName = "ux_tep_exit_reference_records_tenant_code_active";
    public const string TenantStateIndexName = "ix_tep_exit_reference_records_tenant_state";

    private readonly IMongoCollection<TepExitReferenceRecordMetadata> _collection;
    private bool _indexesEnsured;

    public MongoTepExitReferenceRecordMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<TepExitReferenceRecordMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<TepExitReferenceRecordMetadata>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveTenantFilter(tenantId))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<TepExitReferenceRecordMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TepExitReferenceRecordMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<TepExitReferenceRecordMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TepExitReferenceRecordMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<TepExitReferenceRecordMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<TepExitReferenceRecordMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(TepExitReferenceRecordMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(TepExitReferenceRecordMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TepExitReferenceRecordMetadata>.Filter.And(
            Builders<TepExitReferenceRecordMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<TepExitReferenceRecordMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<TepExitReferenceRecordMetadata>(
                Builders<TepExitReferenceRecordMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<TepExitReferenceRecordMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<TepExitReferenceRecordMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<TepExitReferenceRecordMetadata>(
                Builders<TepExitReferenceRecordMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.ReferenceRecordState)
                    .Ascending(x => x.ReferenceSharingState)
                    .Ascending(x => x.ConsentPreconditionState)
                    .Ascending(x => x.VisibilityApprovalState)
                    .Ascending(x => x.DataScopeState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    private static FilterDefinition<TepExitReferenceRecordMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<TepExitReferenceRecordMetadata>.Filter.And(
            Builders<TepExitReferenceRecordMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<TepExitReferenceRecordMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
