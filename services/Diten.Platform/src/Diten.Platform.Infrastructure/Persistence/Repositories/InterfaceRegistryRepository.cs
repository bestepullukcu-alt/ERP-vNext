using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Entities.InterfaceRegistry;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class InterfaceRegistryRepository : IInterfaceRegistryRepository
{
    private readonly IMongoCollection<InterfaceDefinition> _definitions;
    private readonly IMongoCollection<InterfaceDiscoveryBatch> _batches;
    private readonly IMongoCollection<InterfaceDiscoveryDiffItem> _diffItems;
    private readonly IMongoCollection<InterfaceActiveSnapshot> _activeSnapshots;
    private readonly FilterDefinition<InterfaceDefinition> _definitionFilter;
    private readonly FilterDefinition<InterfaceDiscoveryBatch> _batchFilter;
    private readonly FilterDefinition<InterfaceDiscoveryDiffItem> _diffFilter;
    private readonly FilterDefinition<InterfaceActiveSnapshot> _activeFilter;

    public InterfaceRegistryRepository(IPlatformDbContext dbContext)
    {
        _definitions = dbContext.Database.GetCollection<InterfaceDefinition>(PlatformCollections.InterfaceDefinitions);
        _batches = dbContext.Database.GetCollection<InterfaceDiscoveryBatch>(PlatformCollections.InterfaceDiscoveryBatches);
        _diffItems = dbContext.Database.GetCollection<InterfaceDiscoveryDiffItem>(PlatformCollections.InterfaceDiscoveryDiffItems);
        _activeSnapshots = dbContext.Database.GetCollection<InterfaceActiveSnapshot>(PlatformCollections.InterfaceActiveSnapshots);
        _definitionFilter = Builders<InterfaceDefinition>.Filter.Eq(x => x.IsDeleted, false);
        _batchFilter = Builders<InterfaceDiscoveryBatch>.Filter.Eq(x => x.IsDeleted, false);
        _diffFilter = Builders<InterfaceDiscoveryDiffItem>.Filter.Eq(x => x.IsDeleted, false);
        _activeFilter = Builders<InterfaceActiveSnapshot>.Filter.Eq(x => x.IsDeleted, false);
    }

    public async Task<InterfaceDiscoveryBatch> CreateBatchAsync(InterfaceDiscoveryBatch batch, CancellationToken ct = default)
    {
        await _batches.InsertOneAsync(batch, cancellationToken: ct);
        return batch;
    }

    public async Task CreateDiffItemsAsync(IReadOnlyList<InterfaceDiscoveryDiffItem> diffItems, CancellationToken ct = default)
    {
        if (diffItems.Count > 0)
        {
            await _diffItems.InsertManyAsync(diffItems, cancellationToken: ct);
        }
    }

    public async Task<InterfaceDiscoveryBatch?> GetBatchByIdAsync(Guid batchId, CancellationToken ct = default)
    {
        var filter = Builders<InterfaceDiscoveryBatch>.Filter.And(
            _batchFilter,
            Builders<InterfaceDiscoveryBatch>.Filter.Eq(x => x.BatchId, batchId));
        return await _batches.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<InterfaceDiscoveryBatch?> GetBatchByManifestHashAsync(
        string sourceService,
        string sourceModuleCode,
        string manifestHash,
        CancellationToken ct = default)
    {
        var filter = Builders<InterfaceDiscoveryBatch>.Filter.And(
            _batchFilter,
            Builders<InterfaceDiscoveryBatch>.Filter.Eq(x => x.SourceService, sourceService),
            Builders<InterfaceDiscoveryBatch>.Filter.Eq(x => x.SourceModuleCode, sourceModuleCode),
            Builders<InterfaceDiscoveryBatch>.Filter.Eq(x => x.ManifestHash, manifestHash));
        return await _batches.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<InterfaceDiscoveryBatch>> GetBatchesAsync(CancellationToken ct = default) =>
        await _batches.Find(_batchFilter)
            .SortByDescending(x => x.ImportedAtUtc)
            .ToListAsync(ct);

    public async Task UpdateBatchAsync(InterfaceDiscoveryBatch batch, CancellationToken ct = default)
    {
        var filter = Builders<InterfaceDiscoveryBatch>.Filter.And(
            _batchFilter,
            Builders<InterfaceDiscoveryBatch>.Filter.Eq(x => x.BatchId, batch.BatchId));
        await _batches.ReplaceOneAsync(filter, batch, cancellationToken: ct);
    }

    public async Task<InterfaceDiscoveryDiffItem?> GetDiffItemByIdAsync(Guid diffItemId, CancellationToken ct = default)
    {
        var filter = Builders<InterfaceDiscoveryDiffItem>.Filter.And(
            _diffFilter,
            Builders<InterfaceDiscoveryDiffItem>.Filter.Eq(x => x.DiffItemId, diffItemId));
        return await _diffItems.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<InterfaceDiscoveryDiffItem>> GetDiffItemsAsync(Guid batchId, CancellationToken ct = default)
    {
        var filter = Builders<InterfaceDiscoveryDiffItem>.Filter.And(
            _diffFilter,
            Builders<InterfaceDiscoveryDiffItem>.Filter.Eq(x => x.BatchId, batchId));
        return await _diffItems.Find(filter)
            .SortBy(x => x.InterfaceCode)
            .ThenBy(x => x.EndpointKey)
            .ToListAsync(ct);
    }

    public async Task UpdateDiffItemAsync(InterfaceDiscoveryDiffItem diffItem, CancellationToken ct = default)
    {
        var filter = Builders<InterfaceDiscoveryDiffItem>.Filter.And(
            _diffFilter,
            Builders<InterfaceDiscoveryDiffItem>.Filter.Eq(x => x.DiffItemId, diffItem.DiffItemId));
        await _diffItems.ReplaceOneAsync(filter, diffItem, cancellationToken: ct);
    }

    public async Task<bool> ExistsDefinitionVersionAsync(string interfaceCode, string interfaceVersion, CancellationToken ct = default)
    {
        var filter = Builders<InterfaceDefinition>.Filter.And(
            _definitionFilter,
            Builders<InterfaceDefinition>.Filter.Eq(x => x.InterfaceCode, interfaceCode),
            Builders<InterfaceDefinition>.Filter.Eq(x => x.InterfaceVersion, interfaceVersion));
        return await _definitions.Find(filter).AnyAsync(ct);
    }

    public async Task<InterfaceActiveSnapshot?> GetActiveSnapshotAsync(string interfaceCode, string interfaceVersion, CancellationToken ct = default)
    {
        var filter = Builders<InterfaceActiveSnapshot>.Filter.And(
            _activeFilter,
            Builders<InterfaceActiveSnapshot>.Filter.Eq(x => x.InterfaceCode, interfaceCode),
            Builders<InterfaceActiveSnapshot>.Filter.Eq(x => x.InterfaceVersion, interfaceVersion));
        return await _activeSnapshots.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<InterfaceActiveSnapshot>> GetActiveSnapshotsAsync(CancellationToken ct = default) =>
        await _activeSnapshots.Find(_activeFilter).ToListAsync(ct);

    public async Task UpsertActiveSnapshotAsync(InterfaceActiveSnapshot snapshot, CancellationToken ct = default)
    {
        var filter = Builders<InterfaceActiveSnapshot>.Filter.And(
            _activeFilter,
            Builders<InterfaceActiveSnapshot>.Filter.Eq(x => x.InterfaceCode, snapshot.InterfaceCode),
            Builders<InterfaceActiveSnapshot>.Filter.Eq(x => x.InterfaceVersion, snapshot.InterfaceVersion));
        await _activeSnapshots.ReplaceOneAsync(filter, snapshot, new ReplaceOptions { IsUpsert = true }, ct);
    }

    public async Task UpsertDefinitionAsync(InterfaceDefinition definition, CancellationToken ct = default)
    {
        var filter = Builders<InterfaceDefinition>.Filter.And(
            _definitionFilter,
            Builders<InterfaceDefinition>.Filter.Eq(x => x.InterfaceCode, definition.InterfaceCode),
            Builders<InterfaceDefinition>.Filter.Eq(x => x.InterfaceVersion, definition.InterfaceVersion));
        await _definitions.ReplaceOneAsync(filter, definition, new ReplaceOptions { IsUpsert = true }, ct);
    }
}
