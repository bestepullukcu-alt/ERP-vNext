namespace Diten.TalentEcosystemService.Domain.Enums;

public enum TepAssociationMembershipState
{
    Draft = 0,
    Deferred = 1,
    Active = 2,
    Archived = 3
}

public enum TepMemberCompanyState
{
    Draft = 0,
    Verified = 1,
    Deferred = 2
}

public enum TepPolicyEvaluationState
{
    NotEvaluated = 0,
    Approved = 1,
    Deferred = 2,
    Rejected = 3
}

public enum TepVisibilityApprovalState
{
    Pending = 0,
    Approved = 1,
    Deferred = 2
}

public enum TepAssociationActivationState
{
    Draft = 0,
    Ready = 1,
    Active = 2,
    Deferred = 3
}
