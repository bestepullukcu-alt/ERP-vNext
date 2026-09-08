using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.TalentEcosystemService.Persistence.Repositories;

public sealed class MongoEarlyWarningSignalsReadinessMetadataRepository : IEarlyWarningSignalsReadinessMetadataRepository
{
    public const string CollectionName = "tep_early_warning_signals_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_tep_early_warning_signals_tenant_code_active";
    public const string TenantStateIndexName = "ix_tep_early_warning_signals_tenant_state";

    private readonly IMongoCollection<EarlyWarningSignalsReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoEarlyWarningSignalsReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<EarlyWarningSignalsReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<EarlyWarningSignalsReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveTenantFilter(tenantId))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<EarlyWarningSignalsReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<EarlyWarningSignalsReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<EarlyWarningSignalsReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<EarlyWarningSignalsReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<EarlyWarningSignalsReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<EarlyWarningSignalsReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(EarlyWarningSignalsReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(EarlyWarningSignalsReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<EarlyWarningSignalsReadinessMetadata>.Filter.And(
            Builders<EarlyWarningSignalsReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<EarlyWarningSignalsReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<EarlyWarningSignalsReadinessMetadata>(
                Builders<EarlyWarningSignalsReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<EarlyWarningSignalsReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<EarlyWarningSignalsReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<EarlyWarningSignalsReadinessMetadata>(
                Builders<EarlyWarningSignalsReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.EarlyWarningSignalsReadinessState)
                    .Ascending(x => x.SignalCatalogBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    private static FilterDefinition<EarlyWarningSignalsReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<EarlyWarningSignalsReadinessMetadata>.Filter.And(
            Builders<EarlyWarningSignalsReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<EarlyWarningSignalsReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
