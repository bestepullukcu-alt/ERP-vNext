namespace Diten.Platform.Domain.Enums.DocumentManagement;

// MOD-0029-FU09 — approval route matrix + segregation enums (GMG-QMS-SOP-0001 §5, §5.1, §6.1, §7.2). Kept in a
// dedicated file so FU09 ownership never edits earlier FU enum surfaces.

/// <summary>The kind of sign-off a requirement represents (SOP §7.2).</summary>
public enum ApprovalRequirementType
{
    Approval = 0,
    Review = 1,
    Endorsement = 2,
    TechnicalReview = 3,
    QualityConcurrence = 4,
    LegalReview = 5,
    TrainingReadinessVerification = 6
}

/// <summary>The SOP §5 role that must satisfy a requirement. This is the QMS decision role, not an RBAC claim.</summary>
public enum ApprovalRequiredRole
{
    GQD = 0,
    GQDDeputy = 1,
    GRA = 2,
    QPPV = 3,
    QP = 4,
    Legal = 5,
    ITCSVOwner = 6,
    QADocumentation = 7,
    DocumentOwner = 8,
    LocalQA = 9,
    CEO = 10,
    IndependentQASenior = 11,
    TrainingCoordinator = 12
}

public enum ApprovalRequirementStatus
{
    Pending = 0,
    Completed = 1,
    Rejected = 2,
    Waived = 3,
    Blocked = 4
}

/// <summary>Which rule generated a requirement (audit/merge provenance).</summary>
public enum ApprovalSourceRule
{
    Criticality = 0,
    DocumentClass = 1,
    RegulatoryOverlay = 2,
    PVOverlay = 3,
    BatchReleaseOverlay = 4,
    AgreementOverlay = 5,
    DmsCsvOverlay = 6,
    GroupGovernanceOverlay = 7,
    SegregationOverlay = 8,
    ManualOverride = 9
}

public enum ApprovalEvidenceAction
{
    Reviewed = 0,
    Approved = 1,
    Endorsed = 2,
    Rejected = 3,
    Returned = 4
}

public enum SegregationResult
{
    Passed = 0,
    Failed = 1,
    NotApplicable = 2
}

/// <summary>
/// Aggregate approval state written back to <c>DocumentMasterRegisterEntry.ApprovalEvidenceStatus</c>. FU10 consumes
/// this; FU08 MarkEffective already treats Blocked as non-effective.
/// </summary>
public enum ApprovalEvidenceState
{
    NotRequired = 0,
    Pending = 1,
    Complete = 2,
    Rejected = 3,
    Blocked = 4,
    SegregationFailed = 5
}
