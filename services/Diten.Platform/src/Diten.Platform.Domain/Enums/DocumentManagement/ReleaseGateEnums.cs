namespace Diten.Platform.Domain.Enums.DocumentManagement;

// MOD-0029-FU10 — non-waivable release gate enums (GMG-QMS-SOP-0001 §19, §21). Kept in a dedicated file so FU10
// ownership never edits earlier FU enum surfaces. This is the DOCUMENT release gate; the MOD-0028 baseline
// qualification gate is a separate subject.

/// <summary>Aggregate status of a release-gate evaluation. Written to <c>DocumentMasterRegisterEntry.LastReleaseGateEvaluationStatus</c>.</summary>
public enum ReleaseGateEvaluationStatus
{
    NotEvaluated = 0,
    Pending = 1,
    Complete = 2,
    Blocked = 3,
    Failed = 4
}

/// <summary>The six non-waivable gates (SOP §19). Order/number is stable and canonical.</summary>
public enum ReleaseGateKey
{
    MasterRegisterActive = 1,
    ApprovedRepositoryAvailable = 2,
    MandatoryApprovalEvidence = 3,
    RequiredExecutionMaterialsEffective = 4,
    TrainingReadiness = 5,
    SupersededCopyWithdrawalMethod = 6
}

public enum ReleaseGateResultValue
{
    No = 0,
    Yes = 1,
    NotApplicable = 2
}

/// <summary>How a gate result was established.</summary>
public enum ReleaseGateEvidenceSource
{
    Automatic = 0,
    ManualEvidence = 1,
    ExternalReference = 2,
    Computed = 3
}
