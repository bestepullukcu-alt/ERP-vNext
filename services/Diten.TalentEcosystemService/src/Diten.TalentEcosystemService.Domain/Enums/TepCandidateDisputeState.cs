namespace Diten.TalentEcosystemService.Domain.Enums;

public enum TepCandidateDisputeReadinessState
{
    Draft = 0,
    Deferred = 1,
    Ready = 2,
    Archived = 3
}

public enum TepCandidateResponseBoundaryState
{
    Deferred = 0,
    LocalMetadata = 1,
    Ready = 2,
    Blocked = 3
}

public enum TepDisputeIntakeState
{
    Deferred = 0,
    LocalMetadata = 1,
    Ready = 2,
    Blocked = 3
}

public enum TepDisputeReviewState
{
    Deferred = 0,
    LocalMetadata = 1,
    Ready = 2,
    Blocked = 3
}

public enum TepResolutionLifecycleState
{
    Deferred = 0,
    LocalMetadata = 1,
    Ready = 2,
    Blocked = 3
}

public enum TepSelfServiceBoundaryState
{
    Deferred = 0,
    OutOfScope = 1,
    Blocked = 2
}

public enum TepExternalDependencyState
{
    Deferred = 0,
    OutOfScope = 1,
    Waived = 2,
    Blocked = 3
}

public enum TepCandidateDisputeBoundaryState
{
    Deferred = 0,
    OutOfScope = 1,
    LocalMetadata = 2,
    Blocked = 3
}
