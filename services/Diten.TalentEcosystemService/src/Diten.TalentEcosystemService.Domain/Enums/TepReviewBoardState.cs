namespace Diten.TalentEcosystemService.Domain.Enums;

public enum TepReviewBoardCaseState
{
    Draft = 0,
    Deferred = 1,
    Open = 2,
    UnderReview = 3,
    DecisionRecorded = 4,
    Archived = 5
}

public enum TepReviewDecisionState
{
    NotReviewed = 0,
    Deferred = 1,
    Approved = 2,
    Rejected = 3,
    Escalated = 4,
    Archived = 5
}

public enum TepReviewerEligibilityState
{
    NotEvaluated = 0,
    Eligible = 1,
    Deferred = 2,
    Ineligible = 3
}

public enum TepSegregationOfDutiesState
{
    NotEvaluated = 0,
    Passed = 1,
    Deferred = 2,
    Failed = 3
}

public enum TepLegalSecurityDecisionState
{
    Draft = 0,
    LocalMetadata = 1,
    Deferred = 2,
    Approved = 3,
    Rejected = 4
}

public enum TepExternalReviewBoardState
{
    NotRequired = 0,
    Deferred = 1,
    Blocked = 2
}

public enum TepAuditEvidenceState
{
    LocalMetadata = 0,
    Deferred = 1
}

public enum TepRetentionState
{
    LocalMetadata = 0,
    Deferred = 1
}
