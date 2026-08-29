using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU17 — a plan to withdraw superseded copies from point of use (GMG-QMS-SOP-0001 §9.13). Raised when a new
/// version becomes effective, or a document is superseded/suspended/retired, or an obsolete copy is detected. It tallies
/// the required vs withdrawn/missing/obsolete copies and completes only when every required copy is withdrawn or
/// reconciled (or documented missing with a deviation). Never hard-deleted.
/// </summary>
public sealed class DocumentCopyWithdrawalPlan : TenantScopedEntity
{
    public required Guid RegisterEntryId { get; set; }

    public CopyWithdrawalTriggerType TriggerType { get; set; }
    public Guid? TriggerRegisterEntryId { get; set; }
    public Guid? TriggerLifecycleTransitionId { get; set; }

    public CopyWithdrawalPlanStatus PlanStatus { get; set; } = CopyWithdrawalPlanStatus.Draft;

    public int RequiredCopyCount { get; set; }
    public int WithdrawnCopyCount { get; set; }
    public int MissingCopyCount { get; set; }
    public int ObsoleteCopyCount { get; set; }

    public DateTimeOffset? DueDate { get; set; }
    public string? PlanEvidenceReference { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
    public string? CompletedBy { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
