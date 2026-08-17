using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.InterfaceRegistry;

public sealed class InterfaceDiscoveryBatch : GlobalEntity
{
    public Guid BatchId { get; set; } = Guid.NewGuid();
    public string SourceService { get; set; } = string.Empty;
    public string SourceModuleCode { get; set; } = string.Empty;
    public string ManifestHash { get; set; } = string.Empty;
    public DateTimeOffset ImportedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? ImportedBy { get; set; }
    public string Status { get; set; } = InterfaceRegistryStatuses.PendingReview;
    public int NewCount { get; set; }
    public int ChangedCount { get; set; }
    public int DeprecatedCount { get; set; }
    public int MissingCount { get; set; }
    public int UnchangedCount { get; set; }
    public int RejectedCount { get; set; }
    public string? ErrorMessage { get; set; }
}
