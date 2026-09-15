using Diten.HumanCapitalService.Domain.Common;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Domain.Entities;

public sealed class OffboardingCase : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public Guid EmployeeProjectionId { get; set; }
    public Guid? AssignmentOverlayId { get; set; }
    public string ExitReasonCode { get; set; } = string.Empty;
    public string ExitTypeCode { get; set; } = string.Empty;
    public DateTimeOffset? NoticeDate { get; set; }
    public DateTimeOffset PlannedExitDate { get; set; }
    public DateTimeOffset? ActualExitDate { get; set; }
    public OffboardingState OffboardingState { get; set; }
    public OffboardingChecklistState ChecklistState { get; set; }
    public OffboardingSensitiveAccessDecisionState SensitiveAccessDecisionState { get; set; }
    public OffboardingDependencyDecisionState DependencyDecisionState { get; set; }
    public OffboardingTepHandoffState TepHandoffState { get; set; }
    public string? TepHandoffReferenceKey { get; set; }
    public string SourceContractVersion { get; set; } = string.Empty;
    public long OffboardingVersion { get; set; } = 1;
    public DateTimeOffset? LastDependencyEvaluatedAt { get; set; }
    public DateTimeOffset? LastHandoffPlannedAt { get; set; }
    public string? DeferredReason { get; set; }
}
