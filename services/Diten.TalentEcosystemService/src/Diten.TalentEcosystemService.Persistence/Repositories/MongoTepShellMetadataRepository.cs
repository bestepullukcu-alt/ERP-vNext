using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.TalentEcosystemService.Persistence.Repositories;

public sealed class MongoTepShellMetadataRepository : ITepShellMetadataRepository
{
    public const string CollectionName = "tep_shell_metadata";
    public const string ActiveCodeUniqueIndexName = "ux_tep_shell_metadata_tenant_code_active";
    public const string TenantStateIndexName = "ix_tep_shell_metadata_tenant_state";

    private readonly IMongoCollection<TepShellMetadata> _collection;
    private bool _indexesEnsured;

    public MongoTepShellMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<TepShellMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<TepShellMetadata>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveTenantFilter(tenantId))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<TepShellMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TepShellMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<TepShellMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TepShellMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<TepShellMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<TepShellMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(TepShellMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(TepShellMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TepShellMetadata>.Filter.And(
            Builders<TepShellMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<TepShellMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<TepShellMetadata>(
                Builders<TepShellMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<TepShellMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<TepShellMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<TepShellMetadata>(
                Builders<TepShellMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.ShellState)
                    .Ascending(x => x.HcmFoundationState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    private static FilterDefinition<TepShellMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<TepShellMetadata>.Filter.And(
            Builders<TepShellMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<TepShellMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
