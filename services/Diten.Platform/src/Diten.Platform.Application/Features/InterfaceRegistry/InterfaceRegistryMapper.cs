using Diten.BuildingBlocks.InterfaceRegistry.Abstractions;
using Diten.Platform.Application.Features.ModuleCatalog;
using Diten.Platform.Domain.Entities.InterfaceRegistry;

namespace Diten.Platform.Application.Features.InterfaceRegistry;

public static class InterfaceRegistryMapper
{
    public static InterfaceDiscoveryBatchDto ToDto(InterfaceDiscoveryBatch batch) =>
        new(
            batch.BatchId,
            batch.SourceService,
            batch.SourceModuleCode,
            batch.ManifestHash,
            batch.Status,
            batch.NewCount,
            batch.ChangedCount,
            batch.DeprecatedCount,
            batch.MissingCount,
            batch.UnchangedCount,
            batch.RejectedCount,
            batch.ImportedAtUtc,
            batch.ErrorMessage);

    public static InterfaceDiscoveryDiffItemDto ToDto(InterfaceDiscoveryDiffItem item) =>
        new(
            item.DiffItemId,
            item.BatchId,
            item.InterfaceCode,
            item.InterfaceVersion,
            item.EndpointKey,
            item.ChangeType,
            item.ReviewStatus,
            item.Decision,
            item.ReviewReason,
            item.ReviewedAtUtc,
            item.ReviewedBy,
            item.PreviousHash,
            item.IncomingHash);

    public static InterfaceActiveSnapshotDto ToDto(InterfaceActiveSnapshot snapshot) =>
        new(
            snapshot.InterfaceCode,
            snapshot.InterfaceVersion,
            snapshot.SnapshotHash,
            snapshot.Definition.LifecycleStatus,
            snapshot.ConfirmedAtUtc,
            snapshot.ConfirmedBy,
            snapshot.DeprecationReason,
            snapshot.DeprecatedAtUtc,
            snapshot.DeprecatedBy,
            ToDto(snapshot.Definition));

    public static InterfaceDefinitionDto ToDto(InterfaceDefinitionSnapshot snapshot) =>
        new(
            snapshot.InterfaceCode,
            snapshot.DisplayName,
            snapshot.Description,
            snapshot.OwnerModuleCode,
            snapshot.ProviderService,
            snapshot.InterfaceVersion,
            snapshot.Stability,
            snapshot.Visibility,
            snapshot.LifecycleStatus,
            snapshot.CompatibilityNotes,
            snapshot.Endpoints.Select(ToDto).ToList(),
            snapshot.Consumers.Select(ToDto).ToList());

    private static InterfaceEndpointDto ToDto(InterfaceEndpointSnapshot endpoint) =>
        new(
            endpoint.EndpointKey,
            endpoint.HttpMethod,
            endpoint.RoutePath,
            endpoint.Version,
            endpoint.RouteName,
            endpoint.PermissionKey,
            endpoint.AuthPolicy,
            endpoint.RequestContract,
            endpoint.ResponseContract,
            endpoint.ProducesStatusCodes);

    private static InterfaceConsumerDependencyDto ToDto(InterfaceConsumerSnapshot consumer) =>
        new(
            consumer.ConsumerModuleCode,
            consumer.ConsumerService,
            consumer.ConsumedInterfaceCode,
            consumer.ConsumedVersionRange,
            consumer.Required,
            consumer.UsageContext);

    public static InterfaceDefinition ToDefinition(
        InterfaceDefinitionSnapshot snapshot,
        DateTimeOffset confirmedAtUtc,
        string actor) =>
        new()
        {
            InterfaceCode = snapshot.InterfaceCode,
            DisplayName = snapshot.DisplayName,
            Description = snapshot.Description,
            OwnerModuleCode = snapshot.OwnerModuleCode,
            ProviderService = snapshot.ProviderService,
            InterfaceVersion = snapshot.InterfaceVersion,
            Stability = snapshot.Stability,
            Visibility = snapshot.Visibility,
            LifecycleStatus = snapshot.LifecycleStatus,
            CompatibilityNotes = snapshot.CompatibilityNotes,
            ConfirmedAtUtc = confirmedAtUtc,
            ConfirmedBy = actor
        };

    public static InterfaceDefinitionSnapshot ToSnapshot(InterfaceDefinitionManifest manifest)
    {
        var interfaceCode = InterfaceCodeNormalizer.Normalize(manifest.InterfaceCode);
        var endpoints = manifest.Endpoints
            .Select(endpoint => new InterfaceEndpointSnapshot
            {
                EndpointKey = EndpointKeyNormalizer.Create(endpoint.HttpMethod, endpoint.RoutePath, endpoint.Version),
                HttpMethod = endpoint.HttpMethod.Trim().ToUpperInvariant(),
                RoutePath = EndpointKeyNormalizer.NormalizeRoute(endpoint.RoutePath),
                Version = endpoint.Version.Trim().ToLowerInvariant(),
                RouteName = TrimToNull(endpoint.RouteName),
                PermissionKey = TrimToNull(endpoint.PermissionKey),
                AuthPolicy = TrimToNull(endpoint.AuthPolicy),
                RequestContract = TrimToNull(endpoint.RequestContract),
                ResponseContract = TrimToNull(endpoint.ResponseContract),
                ProducesStatusCodes = endpoint.ProducesStatusCodes?.ToList() ?? []
            })
            .OrderBy(x => x.EndpointKey, StringComparer.Ordinal)
            .ToList();

        var consumers = manifest.Consumers
            .Select(consumer => new InterfaceConsumerSnapshot
            {
                ConsumerModuleCode = ModuleCatalogCodeNormalizer.Normalize(consumer.ConsumerModuleCode),
                ConsumerService = consumer.ConsumerService.Trim(),
                ConsumedInterfaceCode = InterfaceCodeNormalizer.Normalize(consumer.ConsumedInterfaceCode),
                ConsumedVersionRange = TrimToNull(consumer.ConsumedVersionRange),
                Required = consumer.Required,
                UsageContext = TrimToNull(consumer.UsageContext)
            })
            .OrderBy(x => x.ConsumerModuleCode, StringComparer.Ordinal)
            .ThenBy(x => x.ConsumedInterfaceCode, StringComparer.Ordinal)
            .ToList();

        return new InterfaceDefinitionSnapshot
        {
            InterfaceCode = interfaceCode,
            DisplayName = manifest.DisplayName.Trim(),
            Description = TrimToNull(manifest.Description),
            OwnerModuleCode = ModuleCatalogCodeNormalizer.Normalize(manifest.OwnerModuleCode),
            ProviderService = manifest.ProviderService.Trim(),
            InterfaceVersion = manifest.Version.Trim().ToLowerInvariant(),
            Stability = manifest.Stability,
            Visibility = manifest.Visibility,
            LifecycleStatus = manifest.LifecycleStatus,
            CompatibilityNotes = TrimToNull(manifest.CompatibilityNotes),
            Endpoints = endpoints,
            Consumers = consumers
        };
    }

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
