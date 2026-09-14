namespace Diten.TalentEcosystemService.Domain.Enums;

public enum TepPolicyState
{
    Draft = 0,
    Deferred = 1,
    Active = 2,
    Archived = 3
}

public enum TepConsentRequirementState
{
    NotRequired = 0,
    Required = 1,
    Approved = 2,
    Deferred = 3
}

public enum TepVisibilityScope
{
    InternalOnly = 0,
    AssociationVisible = 1,
    Restricted = 2
}

public enum TepDataScopeState
{
    Available = 0,
    Deferred = 1,
    Unavailable = 2
}

public enum TepAccessPolicyState
{
    Draft = 0,
    Approved = 1,
    Deferred = 2
}

public enum TepAssociationConsumptionState
{
    Draft = 0,
    ActivationApproved = 1,
    Deferred = 2
}

public enum TepPolicyUnavailableBehavior
{
    FailClosed = 0,
    DeferredEvaluation = 1
}

public enum TepLocalAuditEvidenceRetentionState
{
    LocalMetadata = 0,
    Deferred = 1
}
