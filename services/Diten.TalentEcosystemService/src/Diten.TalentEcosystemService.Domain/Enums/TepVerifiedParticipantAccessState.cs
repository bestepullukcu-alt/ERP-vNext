namespace Diten.TalentEcosystemService.Domain.Enums;

public enum TepVerificationState
{
    Draft = 0,
    Deferred = 1,
    Verified = 2,
    Rejected = 3,
    Archived = 4
}

public enum TepAccessState
{
    Draft = 0,
    Deferred = 1,
    Active = 2,
    Suspended = 3,
    Archived = 4
}

public enum TepVerifiedCompanyAccessState
{
    LocalMetadata = 0,
    Deferred = 1,
    Ready = 2,
    Blocked = 3
}
