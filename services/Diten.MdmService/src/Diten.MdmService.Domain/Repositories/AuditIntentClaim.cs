namespace Diten.MdmService.Domain.Repositories;

public sealed record AuditIntentClaim(
    AuditIntentLocator Locator,
    string ClaimToken,
    string LeaseOwner,
    long ClaimGeneration,
    DateTimeOffset ClaimedAt,
    DateTimeOffset LeaseUntil,
    int AttemptCount);
