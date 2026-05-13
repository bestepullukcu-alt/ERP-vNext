using Diten.BuildingBlocks.InterfaceRegistry.Abstractions;
using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.InterfaceRegistry;

public sealed class InterfaceDefinition : GlobalEntity
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
    public string? DeprecationReason { get; set; }
    public DateTimeOffset? DeprecatedAtUtc { get; set; }
    public string? DeprecatedBy { get; set; }
    public DateTimeOffset? ConfirmedAtUtc { get; set; }
    public string? ConfirmedBy { get; set; }
}
