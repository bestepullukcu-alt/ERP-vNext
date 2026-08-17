using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.InterfaceRegistry;

public sealed class InterfaceActiveSnapshot : GlobalEntity
{
    public string InterfaceCode { get; set; } = string.Empty;
    public string InterfaceVersion { get; set; } = string.Empty;
    public string SnapshotHash { get; set; } = string.Empty;
    public InterfaceDefinitionSnapshot Definition { get; set; } = new();
    public DateTimeOffset ConfirmedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? ConfirmedBy { get; set; }
    public string? DeprecationReason { get; set; }
    public DateTimeOffset? DeprecatedAtUtc { get; set; }
    public string? DeprecatedBy { get; set; }
}
