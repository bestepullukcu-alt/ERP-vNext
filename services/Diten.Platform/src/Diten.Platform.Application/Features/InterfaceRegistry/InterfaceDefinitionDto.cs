using Diten.BuildingBlocks.InterfaceRegistry.Abstractions;

namespace Diten.Platform.Application.Features.InterfaceRegistry;

public sealed record InterfaceDefinitionDto(
    string InterfaceCode,
    string DisplayName,
    string? Description,
    string OwnerModuleCode,
    string ProviderService,
    string InterfaceVersion,
    InterfaceStability Stability,
    InterfaceVisibility Visibility,
    InterfaceLifecycleStatus LifecycleStatus,
    string? CompatibilityNotes,
    IReadOnlyList<InterfaceEndpointDto> Endpoints,
    IReadOnlyList<InterfaceConsumerDependencyDto> Consumers);
