namespace Diten.PlanningService.Application.Features.DemandPlanning;

// The period authority is independent of both the baseline slot and the
// candidate manifest. A client-supplied period is only a lookup selector.
public interface IInternalCurrentPublishedAuthority
{
    Task<CurrentPublishedAuthorityEvidence> VerifyAsync(Guid tenantId,
        Guid legalEntityId, string planningPeriodKey, Guid actorId,
        CancellationToken cancellationToken);
}

public sealed record CurrentPublishedAuthorityEvidence(bool SourceAvailable,
    bool HasConsumePermission, bool PeriodScopeVerified,
    Guid TenantId, Guid LegalEntityId, string PlanningPeriodKey,
    IReadOnlyList<(Guid SkuId, string WarehouseId)> AuthorizedSeries);

public enum CurrentPublishedOutcome
{
    Found, NotFound, PermissionDenied, AuthorityUnavailable, Unverifiable
}

public sealed record CurrentPublishedRevision(Guid TenantId, Guid LegalEntityId,
    Guid RevisionId, Guid PlanningCycleId, string PlanningPeriodKey,
    int StateVersion, string ChecksumScheme, string Checksum,
    int ExpectedPartCount, int ExpectedRowCount);

public sealed record CurrentPublishedResult(CurrentPublishedOutcome Outcome,
    CurrentPublishedRevision? Revision = null);

public interface IInternalCurrentPublishedReader
{
    Task<CurrentPublishedResult> ReadAsync(Guid tenantId, Guid legalEntityId,
        string planningPeriodKey, Guid actorId, CancellationToken cancellationToken);
}
