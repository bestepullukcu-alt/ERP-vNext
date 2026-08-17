using Diten.BuildingBlocks.InterfaceRegistry.Abstractions;
using Diten.Platform.Domain.Entities.InterfaceRegistry;

namespace Diten.Platform.Application.Features.InterfaceRegistry;

public static class InterfaceDiffBuilder
{
    public static IReadOnlyList<InterfaceDiscoveryDiffItem> Build(
        Guid batchId,
        IReadOnlyList<InterfaceDefinitionSnapshot> incoming,
        IReadOnlyList<InterfaceActiveSnapshot> activeSnapshots)
    {
        var activeByKey = activeSnapshots.ToDictionary(
            x => Key(x.InterfaceCode, x.InterfaceVersion),
            StringComparer.Ordinal);
        var incomingKeys = incoming
            .Select(x => Key(x.InterfaceCode, x.InterfaceVersion))
            .ToHashSet(StringComparer.Ordinal);
        var diffItems = new List<InterfaceDiscoveryDiffItem>();

        foreach (var snapshot in incoming)
        {
            var incomingHash = InterfaceManifestHasher.HashSnapshot(snapshot);
            activeByKey.TryGetValue(Key(snapshot.InterfaceCode, snapshot.InterfaceVersion), out var active);
            var changeType = active is null
                ? InterfaceChangeType.New
                : string.Equals(active.SnapshotHash, incomingHash, StringComparison.Ordinal)
                    ? InterfaceChangeType.Unchanged
                    : InterfaceChangeType.Changed;

            diffItems.Add(new InterfaceDiscoveryDiffItem
            {
                BatchId = batchId,
                InterfaceCode = snapshot.InterfaceCode,
                InterfaceVersion = snapshot.InterfaceVersion,
                EndpointKey = null,
                ChangeType = changeType,
                PreviousHash = active?.SnapshotHash,
                IncomingHash = incomingHash,
                IncomingDefinition = snapshot
            });
        }

        foreach (var active in activeSnapshots.Where(x => !incomingKeys.Contains(Key(x.InterfaceCode, x.InterfaceVersion))))
        {
            diffItems.Add(new InterfaceDiscoveryDiffItem
            {
                BatchId = batchId,
                InterfaceCode = active.InterfaceCode,
                InterfaceVersion = active.InterfaceVersion,
                EndpointKey = null,
                ChangeType = active.Definition.LifecycleStatus == InterfaceLifecycleStatus.Deprecated
                    ? InterfaceChangeType.Deprecated
                    : InterfaceChangeType.Missing,
                PreviousHash = active.SnapshotHash,
                IncomingHash = null,
                IncomingDefinition = active.Definition
            });
        }

        return diffItems;
    }

    private static string Key(string interfaceCode, string version) => $"{interfaceCode}:{version}";
}
