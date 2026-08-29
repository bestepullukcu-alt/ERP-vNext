using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU09 — an IMMUTABLE approval evidence record (GMG-QMS-SOP-0001 §9.9). One row per sign-off action against
/// a <see cref="DocumentApprovalRequirement"/>. The requirement's status may advance, but this evidence history is
/// append-only and never hard-deleted; a full GDocP correction trail is FU21.
/// </summary>
public sealed class DocumentApprovalEvidence : TenantScopedEntity
{
    public required Guid RegisterEntryId { get; set; }
    public required Guid RequirementId { get; set; }

    public ApprovalEvidenceAction Action { get; set; }

    public Guid PerformedByUserId { get; set; }
    public ApprovalRequiredRole PerformedByRole { get; set; }
    public DateTimeOffset PerformedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? EvidenceReference { get; set; }
    public string? Comment { get; set; }

    public bool IsSegregationChecked { get; set; }
    public SegregationResult SegregationResult { get; set; } = SegregationResult.NotApplicable;
    public string? FailureReason { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
