namespace Diten.TalentEcosystemService.Domain.Enums;

public enum TepRehireRecommendationReadinessState
{
    Draft = 0,
    Deferred = 1,
    Ready = 2,
    Archived = 3
}

public enum TepRehireRecommendationPolicyState
{
    Draft = 0,
    LocalMetadata = 1,
    Deferred = 2,
    Approved = 3,
    Blocked = 4
}

public enum TepRehireRecommendationEvaluationState
{
    NotEvaluated = 0,
    FailClosed = 1,
    Deferred = 2,
    Ready = 3
}

public enum TepRecommendationEligibilityPreconditionState
{
    NotEvaluated = 0,
    LocalMetadata = 1,
    Deferred = 2,
    Blocked = 3
}

public enum TepRecommendationExplainabilityState
{
    NotEvaluated = 0,
    LocalMetadata = 1,
    Deferred = 2,
    Blocked = 3
}

public enum TepHumanReviewState
{
    NotEvaluated = 0,
    LocalMetadata = 1,
    Deferred = 2,
    Blocked = 3
}

public enum TepContestabilityState
{
    NotEvaluated = 0,
    LocalMetadata = 1,
    Deferred = 2,
    Blocked = 3
}

public enum TepMisuseDetectionState
{
    NotEvaluated = 0,
    LocalMetadata = 1,
    Deferred = 2,
    Blocked = 3
}

public enum TepEscalationState
{
    NotEvaluated = 0,
    LocalMetadata = 1,
    Deferred = 2,
    Blocked = 3
}

public enum TepLocalDeferredPolicyState
{
    LocalMetadata = 0,
    Deferred = 1,
    Blocked = 2
}
