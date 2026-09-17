namespace Diten.TalentEcosystemService.Domain.Enums;

public enum TepCandidateIdentityState
{
    Draft = 0,
    Deferred = 1,
    Active = 2,
    Archived = 3
}

public enum TepTalentProfileState
{
    Draft = 0,
    Deferred = 1,
    Published = 2,
    Archived = 3
}

public enum TepProfileCompletenessState
{
    NotEvaluated = 0,
    Incomplete = 1,
    Complete = 2,
    Deferred = 3
}

public enum TepCandidateVisibilityClassification
{
    Private = 0,
    AssociationVisible = 1,
    Restricted = 2
}

public enum TepCandidateConsentBasisState
{
    NotEvaluated = 0,
    Approved = 1,
    Deferred = 2,
    Rejected = 3
}

public enum TepDataMinimizationState
{
    NotEvaluated = 0,
    Approved = 1,
    Deferred = 2,
    Rejected = 3
}
