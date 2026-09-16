namespace Diten.TalentEcosystemService.Domain.Enums;

public enum TepReferenceExchangeReadinessState
{
    Draft = 0,
    Deferred = 1,
    Ready = 2,
    Archived = 3
}

public enum TepReferenceExchangeAvailabilityState
{
    Closed = 0,
    LocalMetadata = 1,
    Deferred = 2,
    Available = 3
}

public enum TepParticipantEligibilityState
{
    NotEvaluated = 0,
    Eligible = 1,
    Deferred = 2,
    Ineligible = 3
}

public enum TepLegalPrivacyBasisState
{
    Draft = 0,
    LocalMetadata = 1,
    Deferred = 2,
    Approved = 3,
    Rejected = 4
}

public enum TepAuditReadinessState
{
    LocalMetadata = 0,
    Deferred = 1,
    Blocked = 2
}

public enum TepAbuseControlState
{
    NotEvaluated = 0,
    LocalMetadata = 1,
    Deferred = 2,
    Blocked = 3
}

public enum TepThrottlingPolicyState
{
    NotRequired = 0,
    LocalMetadata = 1,
    Deferred = 2,
    Blocked = 3
}

public enum TepExternalDependencyBoundaryState
{
    Deferred = 0,
    OutOfScope = 1,
    Blocked = 2
}
