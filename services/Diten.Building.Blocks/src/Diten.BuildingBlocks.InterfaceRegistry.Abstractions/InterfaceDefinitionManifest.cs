namespace Diten.BuildingBlocks.InterfaceRegistry.Abstractions;

public sealed record InterfaceDefinitionManifest(
    string InterfaceCode,
    string DisplayName,
    string? Description,
    string OwnerModuleCode,
    string ProviderService,
    string Version,
    InterfaceStability Stability,
    InterfaceVisibility Visibility,
    InterfaceLifecycleStatus LifecycleStatus,
    IReadOnlyList<InterfaceEndpointManifest> Endpoints,
    IReadOnlyList<InterfaceConsumerManifest> Consumers,
    string? CompatibilityNotes = null);
