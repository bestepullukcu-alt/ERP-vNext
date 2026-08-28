namespace Diten.Platform.Domain.Enums.DocumentManagement;

// MOD-0029-FU16 — repository assessment / DMS boundary enums (GMG-QMS-SOP-0001 §11, §11.1, §11.2). Kept in a dedicated
// file so FU16 ownership never edits earlier FU enum surfaces.

/// <summary>
/// The three (plus one) environments SOP §11 distinguishes. They are NOT interchangeable, and an approved interim
/// repository shall never be represented or used as a validated DMS.
/// </summary>
public enum RepositoryType
{
    ValidatedDms = 0,
    ApprovedInterimRepository = 1,
    SeparateApprovalMechanism = 2,
    UnapprovedRepository = 3
}

public enum RepositoryAssessmentStatus
{
    Draft = 0,
    UnderReview = 1,
    Approved = 2,
    Rejected = 3,
    Expired = 4,
    Superseded = 5
}

public enum RepositoryLocationType
{
    InHouseSoftware = 0,
    GoogleDrive = 1,
    SharePoint = 2,
    NetworkShare = 3,
    Other = 4
}

public enum RepositoryFindingType
{
    MissingOwner = 0,
    MissingExactLocation = 1,
    MissingAccessModel = 2,
    MissingAccessReview = 3,
    MissingBackup = 4,
    MissingRestoreTest = 5,
    MissingApprovalMechanism = 6,
    MissingEffectiveCopyControl = 7,
    MissingAuditTrail = 8,
    MissingChangeControl = 9,
    InterimPeriodExpired = 10,
    MigrationReconciliationMissing = 11,

    /// <summary>An interim repository claiming/using native e-signature or sharing for regulated approval (SOP §11).</summary>
    NativeESignatureMisuseRisk = 12,
    Other = 13
}

public enum RepositoryFindingSeverity
{
    Warning = 0,
    Major = 1,
    Critical = 2
}

public enum RepositoryFindingStatus
{
    Open = 0,
    Resolved = 1,
    AcceptedAsInterimRisk = 2,
    Closed = 3
}
