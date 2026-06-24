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
    Published = 1
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
