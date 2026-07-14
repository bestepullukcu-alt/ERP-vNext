namespace Diten.Platform.Domain.Enums.DocumentManagement;

// MOD-0028-FU09 — read-back reconciliation & provisioning-evidence enums. Kept in a dedicated file so this FU never
// edits the FU02/FU08 baseline enum surface.

/// <summary>Where a collection folder was physically provisioned.</summary>
public enum ProvisioningPlatformProvider
{
    InHouse = 0,
    GoogleDrive = 1,
    Other = 2
}

/// <summary>Lifecycle of a single node's provisioning against the platform.</summary>
public enum ProvisioningEvidenceStatus
{
    Pending = 0,
    Created = 1,
    ExistingMatched = 2,
    Failed = 3,
    Skipped = 4,
    Deviated = 5
}

/// <summary>Evidence-level deviation flag (a roll-up on the evidence row).</summary>
public enum EvidenceDeviationStatus
{
    None = 0,
    Open = 1,
    Closed = 2,
    Accepted = 3
}

/// <summary>Kind of read-back difference detected between the register/definition set and the live tree.</summary>
public enum CollectionDeviationType
{
    MissingFolder = 0,
    ExtraFolder = 1,
    RenameMismatch = 2,
    MoveMismatch = 3,
    ParentMismatch = 4,
    DuplicateSibling = 5,
    DuplicateFullPath = 6,
    OrphanFolder = 7,
    MetadataMismatch = 8,
    PermissionMismatch = 9,
    EvidenceMissing = 10
}

public enum DeviationSeverity
{
    Info = 0,
    Warning = 1,
    Major = 2,
    Critical = 3
}

/// <summary>Deviation record lifecycle. No hard delete; a closed row is retained for audit.</summary>
public enum DeviationStatus
{
    Open = 0,
    Accepted = 1,
    Resolved = 2,
    Closed = 3
}
