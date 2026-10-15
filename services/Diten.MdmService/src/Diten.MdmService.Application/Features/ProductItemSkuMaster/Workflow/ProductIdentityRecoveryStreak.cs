namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

/// <summary>
/// WP-MDM-GSKU-ACCEPT-01 FIX3 item 1 (B13): the streak of consecutive attempts that ended in a retry, for the flows
/// whose operation keeps no service-fault streak of its own (GSKU correction, GSKU retirement request). Each retry
/// waits twice as long as the one before (never longer than the ceiling); after the configured number of attempts in
/// a row the operation is handed to manual reconciliation instead of being retried for ever without anybody knowing.
/// The identity flows keep their own streak (ServiceFaultCount, ProductIdentityWorkflowFaultRules): a service fault
/// there waits and becomes an error, it is never quarantined — that rule is unchanged.
/// </summary>
public static class ProductIdentityRecoveryStreak
{
    public const int DefaultQuarantineAfterFailedAttempts = 30;
    public static readonly TimeSpan DefaultMaxRetryDelay = TimeSpan.FromMinutes(15);

    /// <param name="failedSoFar">The streak on record before this attempt.</param>
    /// <param name="limit">Attempts in a row after which the operation is quarantined; 0 or less = never.</param>
    public static (bool Exhausted, int Attempts, long NextAttemptAtUtcTicks) Next(
        int failedSoFar, DateTimeOffset now, TimeSpan retryDelay, int limit, TimeSpan? maxRetryDelay)
    {
        var attempts = checked(Math.Max(failedSoFar, 0) + 1);
        var delay = ProductIdentityWorkflowFaultPolicy.Backoff(attempts, retryDelay,
            maxRetryDelay is { } ceiling && ceiling > TimeSpan.Zero ? ceiling : DefaultMaxRetryDelay);
        return (limit > 0 && attempts >= limit, attempts, now.Add(delay).UtcTicks);
    }
}
