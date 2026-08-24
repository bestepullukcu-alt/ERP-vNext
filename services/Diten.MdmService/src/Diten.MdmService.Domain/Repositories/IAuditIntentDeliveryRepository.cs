namespace Diten.MdmService.Domain.Repositories;

/// <summary>
/// Internal-only, tenant-enforcing persistence contract for embedded MOD-0290 audit intents.
/// It intentionally includes soft-deleted aggregates for delivery recovery and never exposes aggregate payloads.
/// </summary>
public interface IAuditIntentDeliveryRepository
{
    Task<IReadOnlyList<AuditIntentWorkItem>> DiscoverEligibleAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task<AuditIntentClaim?> TryClaimAsync(
        AuditIntentLocator locator,
        long expectedClaimGeneration,
        string leaseOwner,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    Task<bool> MarkRetryableFailureAsync(
        AuditIntentClaim claim,
        TimeSpan retryDelay,
        string reason,
        CancellationToken cancellationToken = default);

    Task<bool> MarkDeadLetterAsync(
        AuditIntentClaim claim,
        string reason,
        CancellationToken cancellationToken = default);

    Task<bool> MarkDeliveredAsync(
        AuditIntentClaim claim,
        AuditIntentAcknowledgement acknowledgement,
        CancellationToken cancellationToken = default);

    Task<bool> CompactDeliveredAsync(
        AuditIntentClaim claim,
        string compactReceiptReference,
        CancellationToken cancellationToken = default);
}
