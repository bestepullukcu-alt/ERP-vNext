using Diten.Platform.Domain.Entities.InterfaceRegistry;

namespace Diten.Platform.Domain.Repositories;

public interface IInterfaceRegistryRepository
{
    Task<InterfaceDiscoveryBatch> CreateBatchAsync(InterfaceDiscoveryBatch batch, CancellationToken ct = default);
    Task CreateDiffItemsAsync(IReadOnlyList<InterfaceDiscoveryDiffItem> diffItems, CancellationToken ct = default);
    Task<InterfaceDiscoveryBatch?> GetBatchByIdAsync(Guid batchId, CancellationToken ct = default);
    Task<InterfaceDiscoveryBatch?> GetBatchByManifestHashAsync(string sourceService, string sourceModuleCode, string manifestHash, CancellationToken ct = default);
    Task<IReadOnlyList<InterfaceDiscoveryBatch>> GetBatchesAsync(CancellationToken ct = default);
    Task UpdateBatchAsync(InterfaceDiscoveryBatch batch, CancellationToken ct = default);
    Task<InterfaceDiscoveryDiffItem?> GetDiffItemByIdAsync(Guid diffItemId, CancellationToken ct = default);
    Task<IReadOnlyList<InterfaceDiscoveryDiffItem>> GetDiffItemsAsync(Guid batchId, CancellationToken ct = default);
    Task UpdateDiffItemAsync(InterfaceDiscoveryDiffItem diffItem, CancellationToken ct = default);
    Task<bool> ExistsDefinitionVersionAsync(string interfaceCode, string interfaceVersion, CancellationToken ct = default);
    Task<InterfaceActiveSnapshot?> GetActiveSnapshotAsync(string interfaceCode, string interfaceVersion, CancellationToken ct = default);
    Task<IReadOnlyList<InterfaceActiveSnapshot>> GetActiveSnapshotsAsync(CancellationToken ct = default);
    Task UpsertActiveSnapshotAsync(InterfaceActiveSnapshot snapshot, CancellationToken ct = default);
    Task UpsertDefinitionAsync(InterfaceDefinition definition, CancellationToken ct = default);
}
