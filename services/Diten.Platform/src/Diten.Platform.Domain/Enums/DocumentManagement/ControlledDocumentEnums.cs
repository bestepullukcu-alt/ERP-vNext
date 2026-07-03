namespace Diten.Platform.Domain.Enums.DocumentManagement;

// MOD-0029-FU01 — controlled document / template / versioning / sharing enums. Kept in a dedicated file
// so MOD-0029 ownership never edits the MOD-0028 DocumentManagementEnums.cs surface.

public enum DocumentType
{
    Sop = 0,
    WorkInstruction = 1,
    Policy = 2,
    Form = 3,
    Template = 4,
    Other = 5
}

/// <summary>
/// Technical version lifecycle. ACTIVE is technical activation / current-version resolution only — NOT an
/// approval decision (FU01 implements no review/approve workflow).
/// </summary>
public enum DocumentVersionStatus
{
    Draft = 0,
    Active = 1,
    Superseded = 2,
    Archived = 3
}

/// <summary>Document/template aggregate lifecycle (not the version lifecycle, no hard delete).</summary>
public enum ControlledItemStatus
{
    Active = 0,
    Archived = 1
}

public enum DocumentShareMode
{
    Reference = 0,
    CopyOnAdopt = 1
}

public enum ShareVisibilityScope
{
    Company = 0,
    Plant = 1,
    BusinessUnit = 2
}

public enum DocumentAccessAction
{
    View = 0,
    Download = 1,
    Edit = 2,
    Version = 3,
    Share = 4,
    ManageAccess = 5
}

public enum AccessTargetType
{
    User = 0,
    Role = 1,
    Company = 2,
    Plant = 3,
    BusinessUnit = 4
}

public enum AccessPolicySource
{
    Inherited = 0,
    Explicit = 1
}

public enum SharedItemKind
{
    ControlledDocument = 0,
    Template = 1
}

public enum FolderShareOperationType
{
    DryRun = 0,
    Execute = 1
}

public enum FolderShareStatus
{
    Completed = 0,
    Blocked = 1,
    Partial = 2,
    Failed = 3
}

public enum FolderShareItemType
{
    Folder = 0,
    Template = 1
}

public enum FolderShareOutcomeStatus
{
    Shared = 0,
    Copied = 1,
    Skipped = 2,
    Failed = 3
}
