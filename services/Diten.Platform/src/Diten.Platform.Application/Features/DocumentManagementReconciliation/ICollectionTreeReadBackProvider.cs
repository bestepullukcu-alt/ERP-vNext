using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementReconciliation;

/// <summary>
/// MOD-0028-FU09 — reads the live/provisioned folder tree for a baseline from a specific platform. Read-only: it never
/// creates, moves, renames or deletes anything. The in-house provider reads MOD-0028 CollectionInstance rows; the
/// Google Drive provider is a contract stub in this FU (no real API, no credentials).
/// </summary>
public interface ICollectionTreeReadBackProvider
{
    ProvisioningPlatformProvider Provider { get; }

    Task<IReadOnlyList<ReadBackNode>> ReadAsync(Guid baselineReleaseId, CancellationToken ct = default);
}

/// <summary>Thrown when a read-back provider has no live source available (e.g. the Google Drive stub).</summary>
public sealed class ReadBackProviderUnavailableException(string message) : Exception(message);

/// <summary>
/// In-house read-back: projects the provisioned <c>CollectionInstance</c> tree, joined to its source
/// <c>CollectionDefinition</c> for the stable register folder id and governance metadata. Pure read.
/// </summary>
public sealed class InHouseCollectionTreeReadBackProvider : ICollectionTreeReadBackProvider
{
    private readonly ICollectionInstanceRepository _instances;
    private readonly ICollectionDefinitionRepository _definitions;

    public InHouseCollectionTreeReadBackProvider(
        ICollectionInstanceRepository instances,
        ICollectionDefinitionRepository definitions)
    {
        _instances = instances;
        _definitions = definitions;
    }

    public ProvisioningPlatformProvider Provider => ProvisioningPlatformProvider.InHouse;

    public async Task<IReadOnlyList<ReadBackNode>> ReadAsync(Guid baselineReleaseId, CancellationToken ct = default)
    {
        var definitionsByCanonical = (await _definitions.GetByBaselineAsync(baselineReleaseId, ct))
            .GroupBy(d => d.CanonicalId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var instances = (await _instances.GetAllForTenantAsync(ct))
            .Where(i => i.BaselineReleaseId == baselineReleaseId
                && i.InstanceStatus == CollectionInstanceStatus.Active)
            .ToList();

        // Parent platform id via the ParentCanonicalId chain within the same baseline.
        var instanceByCanonical = instances
            .GroupBy(i => i.CanonicalId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var nodes = new List<ReadBackNode>(instances.Count);
        foreach (var i in instances)
        {
            definitionsByCanonical.TryGetValue(i.CanonicalId, out var def);
            string? parentPlatformId = null;
            if (!string.IsNullOrWhiteSpace(i.ParentCanonicalId)
                && instanceByCanonical.TryGetValue(i.ParentCanonicalId!, out var parent))
            {
                parentPlatformId = parent.Id.ToString("D");
            }

            var metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["AccessProfile"] = def?.AccessProfile,
                ["FolderType"] = def?.FolderType,
                ["RetentionClass"] = def?.RetentionClass
            };

            nodes.Add(new ReadBackNode(
                PlatformFolderId: i.Id.ToString("D"),
                PlatformParentId: parentPlatformId,
                Name: i.Name,
                FullPath: i.FullPath,
                ParentFullPath: ParentPath(i.FullPath),
                RegisterFolderId: def?.RegisterFolderId,
                CreatedAt: i.LastChangeAt,
                CreatedBy: null,
                Metadata: metadata,
                CollectionInstanceId: i.Id));
        }

        return nodes;
    }

    private static string? ParentPath(string fullPath)
    {
        var idx = (fullPath ?? string.Empty).LastIndexOf('/');
        return idx <= 0 ? null : fullPath![..idx];
    }
}

/// <summary>
/// Google Drive read-back — CONTRACT STUB only in MOD-0028-FU09. No real Drive API, no credentials. It advertises the
/// provider and fails fast so callers surface a controlled "provider unavailable" finding. Implementing the real
/// Drive traversal (Shared Drive, parent-id walk, path→id map) is a later task.
/// </summary>
public sealed class GoogleDriveCollectionTreeReadBackProvider : ICollectionTreeReadBackProvider
{
    public ProvisioningPlatformProvider Provider => ProvisioningPlatformProvider.GoogleDrive;

    public Task<IReadOnlyList<ReadBackNode>> ReadAsync(Guid baselineReleaseId, CancellationToken ct = default) =>
        throw new ReadBackProviderUnavailableException(
            "Google Drive read-back is not implemented in this release (contract stub). Use the in-house provider.");
}
