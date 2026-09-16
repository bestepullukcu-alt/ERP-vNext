namespace Diten.TalentEcosystemService.Domain.Enums;

public enum TepTrustLevelPolicyState
{
    Draft = 0,
    Deferred = 1,
    Active = 2,
    Archived = 3
}

public enum TepTrustValidationState
{
    NotEvaluated = 0,
    Approved = 1,
    Deferred = 2,
    Rejected = 3
}

public enum TepMultiSignatureRequirementState
{
    NotRequired = 0,
    Required = 1,
    Approved = 2,
    Deferred = 3,
    Rejected = 4
}

public enum TepMultiSignaturePolicyUnavailableBehavior
{
    FailClosed = 0,
    DeferredEvaluation = 1
}

public enum TepSignatureSubstrateState
{
    NotRequired = 0,
    Available = 1,
    Deferred = 2,
    Unavailable = 3
}

public enum TepTrustLegalSecurityState
{
    Draft = 0,
    LocalMetadata = 1,
    Deferred = 2,
    Approved = 3,
    Rejected = 4
}

public enum TepTrustActivationState
{
    Draft = 0,
    Ready = 1,
    Active = 2,
    Deferred = 3,
    Archived = 4
}
