using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU11 — a role-to-document training requirement resolved for a Document Master Register entry (GMG-QMS-
/// SOP-0001 §7.3, §17). Requirements are resolved from the entry's class/criticality/impact flags and are idempotent
/// per <see cref="RequirementKey"/>. Concrete assignments (<see cref="DocumentTrainingAssignment"/>) satisfy them.
/// Never hard-deleted; there is no waiver (SOP §19 gate 5 is non-waivable).
/// </summary>
public sealed class DocumentTrainingMatrixRequirement : TenantScopedEntity
{
    public required Guid RegisterEntryId { get; set; }

    /// <summary>Deterministic dedupe key, e.g. <c>Role:GQD:FullSopCompetencyAssessment</c>.</summary>
    public required string RequirementKey { get; set; }

    public TrainingAudienceType AudienceType { get; set; } = TrainingAudienceType.Role;
    public ApprovalRequiredRole? RequiredRole { get; set; }
    public Guid? RequiredUserId { get; set; }
    public string? RequiredDepartment { get; set; }

    public DocumentTrainingType TrainingType { get; set; }

    public bool IsCriticalProcessUserRequirement { get; set; }
    public bool EffectivenessCheckRequired { get; set; }
    public bool AcknowledgementRequired { get; set; }
    public bool MandatoryBeforeEffective { get; set; } = true;

    public TrainingSourceRule SourceRule { get; set; }
    public TrainingRequirementStatus Status { get; set; } = TrainingRequirementStatus.Pending;

    public bool DueBeforeEffectiveDate { get; set; } = true;

    public DateTimeOffset? DeletedAt { get; set; }
}
