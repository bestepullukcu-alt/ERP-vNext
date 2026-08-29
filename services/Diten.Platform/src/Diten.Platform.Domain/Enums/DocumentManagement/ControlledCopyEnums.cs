namespace Diten.Platform.Domain.Enums.DocumentManagement;

// MOD-0029-FU17 — controlled copy / obsolete copy reconciliation enums (GMG-QMS-SOP-0001 §9.12–9.13, §18 LOG-0002,
// §19 gate 6). Kept in a dedicated file so FU17 ownership never edits earlier FU enum surfaces.

public enum ControlledCopyType
{
    DigitalEffectiveCopy = 0,
    DigitalControlledCopy = 1,
    PrintedControlledCopy = 2,
    TrainingCopy = 3,
    ReferenceCopy = 4,
    ExternalSharedCopy = 5,
    TemporaryControlledIssue = 6
}

public enum ControlledCopyStatus
{
    Active = 0,
    PendingWithdrawal = 1,
    Withdrawn = 2,
    Reconciled = 3,
    Missing = 4,
    Obsolete = 5,
    Destroyed = 6
}

public enum ControlledCopyLocationType
{
    Repository = 0,
    PointOfUse = 1,
    Department = 2,
    Site = 3,
    ThirdParty = 4,
    Archive = 5,
    Other = 6
}

public enum CopyWithdrawalTriggerType
{
    NewEffectiveVersion = 0,
    Superseded = 1,
    Suspended = 2,
    Retired = 3,
    ObsoleteDetected = 4,
    Manual = 5
}

public enum CopyWithdrawalPlanStatus
{
    Draft = 0,
    Active = 1,
    Completed = 2,
    Blocked = 3,
    Cancelled = 4
}

public enum ObsoleteCopyFindingType
{
    SupersededCopyAtPointOfUse = 0,
    RetiredCopyAvailable = 1,
    SuspendedDocumentInUse = 2,
    UncontrolledCopyDetected = 3,
    MissingWithdrawalEvidence = 4,
    MissingCopyDuringReconciliation = 5
}

public enum ObsoleteCopyFindingSeverity
{
    Warning = 0,
    Major = 1,
    Critical = 2
}

public enum ObsoleteCopyFindingStatus
{
    Open = 0,
    Acknowledged = 1,
    Resolved = 2,
    Closed = 3
}
