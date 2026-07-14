using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0028-FU09 — a single read-back deviation between the register/definition set and the live provisioned tree,
/// kept as a tenant-scoped SIDECAR aggregate. Never auto-hard-deleted: resolving/accepting changes status only, so
/// the deviation trail is preserved for qualification evidence. Detection is idempotent (see the reconciliation
/// service) so re-running a read-back updates an existing OPEN deviation rather than duplicating it.
/// </summary>
public sealed class DocumentCollectionDeviation : TenantScopedEntity
{
    public required Guid BaselineReleaseId { get; set; }
    public Guid? CollectionInstanceId { get; set; }
    public string? RegisterFolderId { get; set; }

    public required string ExpectedFullPath { get; set; }
    public string? ActualFullPath { get; set; }

    public CollectionDeviationType DeviationType { get; set; }
    public DeviationSeverity Severity { get; set; } = DeviationSeverity.Warning;
    public DeviationStatus Status { get; set; } = DeviationStatus.Open;

    public string? Description { get; set; }
    public string? ResolutionComment { get; set; }

    public DateTimeOffset DetectedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? DetectedBy { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
