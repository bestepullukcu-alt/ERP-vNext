namespace Diten.AuthService.Application.Common.Interfaces;

public interface IIntegrationEventInboxRepository
{
    Task<IntegrationEventClaim> TryClaimAsync(
        Guid eventId,
        string eventName,
        Guid tenantId,
        TimeSpan leaseDuration,
        CancellationToken ct = default);
    Task CompleteClaimAsync(Guid eventId, Guid tenantId, Guid claimId, CancellationToken ct = default);
    Task ReleaseClaimAsync(Guid eventId, Guid tenantId, Guid claimId, CancellationToken ct = default);
    Task<bool> TryInsertAsync(Guid eventId, string eventName, Guid tenantId, CancellationToken ct = default);
}

public sealed record IntegrationEventClaim(IntegrationEventClaimResult Result, Guid? ClaimId = null);

public enum IntegrationEventClaimResult
{
    Claimed,
    Completed,
    Busy,
    TenantMismatch,
    IdentityMismatch
}
