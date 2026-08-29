using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU11 — a concrete training assignment against a <see cref="DocumentTrainingMatrixRequirement"/> (GMG-QMS-
/// SOP-0001 §7.3, §9.11). Tracks completion, the effectiveness check, and any FORMAL RESTRICTION (a critical-process
/// user who has not completed training is formally restricted from independent execution). Completion/effectiveness
/// evidence and restrictions are append-only audit records; never hard-deleted. Loose-coupling extension point for a
/// future HCM/LMS: <see cref="CompletionEvidenceReference"/> may carry an external LMS record id.
/// </summary>
public sealed class DocumentTrainingAssignment : TenantScopedEntity
{
    public required Guid RegisterEntryId { get; set; }
    public required Guid RequirementId { get; set; }

    public Guid? AssignedToUserId { get; set; }
    public ApprovalRequiredRole? AssignedToRole { get; set; }
    public string? AssignedToDepartment { get; set; }

    public DocumentTrainingType TrainingType { get; set; }

    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? AssignedBy { get; set; }
    public DateTimeOffset? DueDate { get; set; }

    public TrainingAssignmentStatus Status { get; set; } = TrainingAssignmentStatus.Assigned;

    public string? CompletionEvidenceReference { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? CompletedBy { get; set; }

    public TrainingEffectivenessCheckStatus EffectivenessCheckStatus { get; set; } = TrainingEffectivenessCheckStatus.NotRequired;
    public string? EffectivenessEvidenceReference { get; set; }

    public string? RestrictionReason { get; set; }
    public DateTimeOffset? RestrictedAt { get; set; }
    public string? RestrictedBy { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
