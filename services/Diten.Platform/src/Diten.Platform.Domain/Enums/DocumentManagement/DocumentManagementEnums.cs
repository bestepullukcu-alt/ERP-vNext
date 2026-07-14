namespace Diten.Platform.Domain.Enums.DocumentManagement;

public enum CollectionDefinitionStatus
{
    Active = 0,
    Deprecated = 1,
    Retired = 2
}

public enum BaselineReleaseStatus
{
    Draft = 0,

    /// <summary>
    /// LEGACY (pre-FU08) terminal state produced by the old publish flow. Retained for backward compatibility:
    /// existing stored baselines keep value 1 and remain instantiable exactly like <see cref="Effective"/>. New
    /// lifecycle uses Draft → Approved → Effective → Superseded instead of Published.
    /// </summary>
    Published = 1,

    /// <summary>MOD-0028-FU08 — reviewed; immutable snapshot/manifest frozen; NOT yet live, NOT instantiable.</summary>
    Approved = 2,

    /// <summary>MOD-0028-FU08 — the single live canonical baseline for a tenant + source key; instantiable.</summary>
    Effective = 3,

    /// <summary>MOD-0028-FU08 — replaced by a newer Effective baseline; retained for history, never deleted.</summary>
    Superseded = 4
}

/// <summary>MOD-0028-FU08 lifecycle helpers kept next to the enum so every feature shares one definition.</summary>
public static class BaselineReleaseStatusExtensions
{
    /// <summary>
    /// A baseline may be provisioned/instantiated only when it is the live canonical (<see cref="BaselineReleaseStatus.Effective"/>)
    /// or a legacy <see cref="BaselineReleaseStatus.Published"/> baseline (backward compatibility). Draft, Approved,
    /// and Superseded are never instantiable.
    /// </summary>
    public static bool IsInstantiable(this BaselineReleaseStatus status) =>
        status is BaselineReleaseStatus.Effective or BaselineReleaseStatus.Published;
}

public enum CollectionScopeType
{
    Company = 0,
    Plant = 1,
    BusinessUnit = 2
}

public enum CollectionInstanceStatus
{
    Active = 0,
    Blocked = 1,
    Superseded = 2,
    Archived = 3
}

public enum OrgBindingScopeType
{
    Company = 0,
    Plant = 1,
    BusinessUnit = 2
}

public enum ScopeBindingStatus
{
    Active = 0,
    Unvalidated = 1,
    Invalid = 2
}

public enum InstantiationOperationType
{
    DryRun = 0,
    Execute = 1,
    Retry = 2
}

public enum InstantiationOperationStatus
{
    Completed = 0,
    Blocked = 1,
    Partial = 2,
    Failed = 3
}

public enum InstantiationOutcomeStatus
{
    Created = 0,
    Skipped = 1,
    Failed = 2
}

public enum InstantiationSelectionMode
{
    FullTree = 0,
    SelectedBranches = 1
}
