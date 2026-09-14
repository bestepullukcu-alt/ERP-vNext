namespace Diten.TalentEcosystemService.Domain.Enums;

public enum TepExitReferenceRecordState
{
    Draft = 0,
    Deferred = 1,
    Active = 2,
    Archived = 3
}

public enum TepReferenceSharingState
{
    LocalMetadata = 0,
    Deferred = 1,
    Blocked = 2
}

public enum TepEvidenceRetentionDecisionState
{
    LocalMetadata = 0,
    Deferred = 1,
    Blocked = 2
}

public enum TepReviewDisputeBoundaryState
{
    PreconditionSatisfied = 0,
    Deferred = 1,
    Blocked = 2
}
