using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU09 — a single mandatory (or optional) approval/review requirement resolved for a Document Master
/// Register entry (GMG-QMS-SOP-0001 §5, §7.2). Requirements are RESOLVED from the entry's class/criticality/impact
/// flags and are idempotent per <see cref="RequirementKey"/>. Status advances as evidence is recorded; the evidence
/// history itself is immutable (<see cref="DocumentApprovalEvidence"/>). Never hard-deleted.
/// </summary>
public sealed class DocumentApprovalRequirement : TenantScopedEntity
{
    public required Guid RegisterEntryId { get; set; }

    /// <summary>Deterministic dedupe key, e.g. <c>GQD:Approval</c>. Idempotent re-resolve upserts on this.</summary>
    public required string RequirementKey { get; set; }

    public ApprovalRequirementType RequirementType { get; set; }
    public ApprovalRequiredRole RequiredRole { get; set; }
    public Guid? RequiredRoleId { get; set; }
    public string? RequiredRoleName { get; set; }
    public string? RequiredRoleDisplayName { get; set; }
    public Guid? RequiredUserId { get; set; }
    public string? RequiredFunction { get; set; }

    public bool IsMandatory { get; set; } = true;
    public bool IsNonDelegable { get; set; }

    public ApprovalSourceRule SourceRule { get; set; }
    public ApprovalRequirementStatus Status { get; set; } = ApprovalRequirementStatus.Pending;

    public Guid? CompletedByUserId { get; set; }
    public ApprovalRequiredRole? CompletedByRole { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? EvidenceReference { get; set; }
    public string? Comment { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
