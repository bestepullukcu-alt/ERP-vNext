namespace Diten.Platform.Domain.Enums.DocumentManagement;

// MOD-0029-FU13 — suspension / urgent withdrawal / retirement / temporary-instruction enums (GMG-QMS-SOP-0001 §12.1,
// §6.1 class 7, §9.16). Kept in a dedicated file so FU13 ownership never edits earlier FU enum surfaces. Approver
// roles reuse the FU09 ApprovalRequiredRole (GQD / IndependentQASenior) for consistent role naming.

public enum SuspensionCaseStatus
{
    Opened = 0,
    Escalated = 1,
    Approved = 2,
    Rejected = 3,
    Executed = 4,
    Closed = 5,
    Cancelled = 6
}

public enum SuspensionTriggerType
{
    UserReportedRisk = 0,
    PeriodicReviewOverdue = 1,
    ExtensionExpired = 2,
    QualityRisk = 3,
    RegulatoryRisk = 4,
    DataIntegrityRisk = 5,
    TrainingGap = 6,
    RepositoryIssue = 7,
    Other = 8
}

/// <summary>The GQD (or independent QA delegate) determination on a suspension case (SOP §12.1).</summary>
public enum SuspensionDecision
{
    Suspend = 0,
    DoNotSuspend = 1,
    TemporaryInstruction = 2,
    Replace = 3,
    Retire = 4
}

public enum RetirementCaseStatus
{
    Requested = 0,
    Approved = 1,
    Rejected = 2,
    Executed = 3,
    Closed = 4,
    Cancelled = 5
}

/// <summary>SOP §6.1 class 7: a temporary instruction is valid for a MAXIMUM of 30 calendar days and shall never
/// remain operational by default after expiry.</summary>
public enum TemporaryInstructionStatus
{
    Active = 0,
    DueToExpire = 1,
    Expired = 2,
    Incorporated = 3,
    Withdrawn = 4,
    ReplacedByNewTemporary = 5,
    Suspended = 6
}

/// <summary>At expiry a temporary instruction shall transition to EXACTLY ONE of these (SOP §6.1 class 7).</summary>
public enum TemporaryInstructionExpiryAction
{
    IncorporateIntoPermanent = 0,
    FormallyWithdraw = 1,
    ReplaceWithNewTemporary = 2,
    SuspendNoReplacement = 3
}
