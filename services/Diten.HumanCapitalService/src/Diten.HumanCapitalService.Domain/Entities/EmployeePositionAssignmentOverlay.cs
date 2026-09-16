using Diten.HumanCapitalService.Domain.Common;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Domain.Entities;

public sealed class EmployeePositionAssignmentOverlay : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public Guid EmployeeProjectionId { get; set; }
    public Guid PersonReferenceId { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public Guid? PositionId { get; set; }
    public Guid? ManagerEmployeeProjectionId { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public AssignmentOverlayState AssignmentState { get; set; }
    public string SourceContractVersion { get; set; } = string.Empty;
    public AssignmentReferenceValidationState ReferenceValidationState { get; set; }
    public AssignmentSensitiveAccessDecisionState SensitiveAccessDecisionState { get; set; }
    public long AssignmentVersion { get; set; } = 1;
    public DateTimeOffset? LastReferenceValidatedAt { get; set; }
    public string? DeferredReason { get; set; }
}
