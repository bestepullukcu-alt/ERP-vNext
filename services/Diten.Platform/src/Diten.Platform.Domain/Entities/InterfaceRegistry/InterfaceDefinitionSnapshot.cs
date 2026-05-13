using Diten.BuildingBlocks.InterfaceRegistry.Abstractions;

namespace Diten.Platform.Domain.Entities.InterfaceRegistry;

public sealed class InterfaceDefinitionSnapshot
{
    public string InterfaceCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string OwnerModuleCode { get; set; } = string.Empty;
    public string ProviderService { get; set; } = string.Empty;
    public string InterfaceVersion { get; set; } = string.Empty;
    public InterfaceStability Stability { get; set; }
    public InterfaceVisibility Visibility { get; set; }
    public InterfaceLifecycleStatus LifecycleStatus { get; set; }
    public string? CompatibilityNotes { get; set; }
    public List<InterfaceEndpointSnapshot> Endpoints { get; set; } = [];
    public List<InterfaceConsumerSnapshot> Consumers { get; set; } = [];
}
