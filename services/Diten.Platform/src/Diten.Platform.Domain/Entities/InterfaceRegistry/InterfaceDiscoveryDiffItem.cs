using Diten.BuildingBlocks.InterfaceRegistry.Abstractions;
using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.InterfaceRegistry;

public sealed class InterfaceDiscoveryDiffItem : GlobalEntity
{
    public Guid DiffItemId { get; set; } = Guid.NewGuid();
    public Guid BatchId { get; set; }
    public string InterfaceCode { get; set; } = string.Empty;
    public string InterfaceVersion { get; set; } = string.Empty;
    public string? EndpointKey { get; set; }
    public InterfaceChangeType ChangeType { get; set; }
    public string? PreviousHash { get; set; }
    public string? IncomingHash { get; set; }
    public string ReviewStatus { get; set; } = InterfaceRegistryStatuses.PendingReview;
    public InterfaceReviewDecision? Decision { get; set; }
    public string? ReviewReason { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public string? ReviewedBy { get; set; }
    public InterfaceDefinitionSnapshot IncomingDefinition { get; set; } = new();
}
