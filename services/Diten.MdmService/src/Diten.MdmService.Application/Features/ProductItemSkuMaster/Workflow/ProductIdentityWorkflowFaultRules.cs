using Diten.MdmService.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

/// <summary>
/// The rules every product-identity processor applies to a service fault it waits through: how a streak of
/// consecutive faults is counted, when it is over, how loud it is said, and how long a withdrawal in flight is left
/// alone. One place, so that the Global Product and GSKU processors cannot drift apart.
/// </summary>
public static class ProductIdentityWorkflowFaultRules
{
    /*
     * How long the worker leaves a withdrawal in flight alone. The requester's cancellation is processed by the
     * engine inside its own two-second budget; a worker that reads the approval before that budget has surely run
     * out can find it "still open", forget the withdrawal — and then the cancellation lands. The floor is fixed
     * (fifteen budgets), not a setting: a RetryDelaySeconds of 1 must not shrink it. A longer retry delay extends it.
     */
    public static readonly TimeSpan WithdrawalSettleFloor = TimeSpan.FromSeconds(30);

    /*
     * The streak of CONSECUTIVE service faults this operation is in: how many of them were the worker's, and when
     * the first one happened. A streak ends with the first answer the chain gives for the operation (every step
     * that got one writes the two fields back to nothing), and it also ends by itself when its last fault is long
     * past: faults further apart than two ceilings are not one outage — the worker never leaves a waiting operation
     * alone for longer than one — so a glitch at eleven does not read "failing since nine".
     */
    public static (int Count, long Since) Streak(
        IProductIdentityWorkflowFaultFields operation,
        DateTimeOffset now,
        bool countsAsWorkerAttempt,
        ProductIdentityWorkflowServiceFaultSettings settings)
    {
        var ongoing = operation.ServiceFaultSinceUtcTicksV1 is { } since && !StreakIsOver(operation, now, settings)
            ? (Count: operation.ServiceFaultCount, Since: since)
            : (Count: 0, Since: now.UtcTicks);
        return countsAsWorkerAttempt ? (ongoing.Count + 1, ongoing.Since) : ongoing;
    }

    // A streak is on record and its last write is further back than two ceilings. A write that only passes by
    // (the first step of a withdrawal, a lease given back) clears such a streak, so that its own timestamp does not
    // make an old streak look as if it were going on.
    public static bool StreakIsOver(
        IProductIdentityWorkflowFaultFields operation,
        DateTimeOffset now,
        ProductIdentityWorkflowServiceFaultSettings settings) =>
        operation.ServiceFaultSinceUtcTicksV1.HasValue
        && now.UtcTicks - operation.UpdatedAtUtcTicksV1 > 2 * settings.MaxRetryDelay.Ticks;

    // An error when the worker has tried often enough, when the streak of consecutive faults has lasted longer than
    // the longest back-off (so a fault met only by makers still becomes an error — by how long it has gone on, not
    // by their clicks), or on every attempt for a fault that waiting will not mend.
    public static LogLevel Level(
        bool loud,
        int workerAttempts,
        long sinceUtcTicks,
        DateTimeOffset now,
        ProductIdentityWorkflowServiceFaultSettings settings) =>
        loud || workerAttempts >= settings.ErrorAfterAttempts
             || now.UtcTicks - sinceUtcTicks >= settings.MaxRetryDelay.Ticks
            ? LogLevel.Error
            : LogLevel.Warning;

    // Never earlier than the worker's own schedule, never inside the settle window.
    public static long SettledNextAttempt(
        IProductIdentityWorkflowFaultFields operation,
        DateTimeOffset now,
        TimeSpan retryDelay)
    {
        var settled = now.Add(retryDelay > WithdrawalSettleFloor ? retryDelay : WithdrawalSettleFloor).UtcTicks;
        return operation.NextAttemptAtUtcTicksV1 is { } scheduled && scheduled > settled ? scheduled : settled;
    }

    /*
     * How long a withdrawal whose cancellation landed may wait for its requester. Only the requester's repeat (same
     * command, their own token) can fetch the cancellation's evidence; a requester who left, lost the permission or
     * lost the command never comes back, and the product would stay pending for ever without anybody noticing.
     * After this long the operation is handed to manual reconciliation, with an error — that is the alarm.
     */
    public static readonly TimeSpan WithdrawalRequesterWaitLimit = TimeSpan.FromHours(72);
}

/// <summary>
/// A claim that neither the caller can cut off half-way (it may be written with nobody left to release it) nor a
/// half-open connection can hang for ever: it runs under its own token, cancelled after half the lease. If that
/// runs out, a lease this owner may have written is given back (best effort, bounded the same way) and the caller
/// is told the store did not answer.
/// </summary>
public static class ProductIdentityWorkflowClaims
{
    public const string StoreUnavailable = "PRODUCT_IDENTITY_WORKFLOW_STORE_UNAVAILABLE";

    public static TimeSpan Bound(TimeSpan leaseDuration) => leaseDuration / 2;

    public static async Task<(TClaim? Claim, bool TimedOut)> ClaimAsync<TClaim>(
        Func<CancellationToken, Task<TClaim?>> claim,
        Func<CancellationToken, Task> releaseIfOwned,
        TimeSpan leaseDuration,
        Action<Exception> releaseFailed) where TClaim : class
    {
        using var bound = new CancellationTokenSource(Bound(leaseDuration));
        try
        {
            return (await claim(bound.Token), false);
        }
        catch (OperationCanceledException) when (bound.IsCancellationRequested)
        {
            using var releaseBound = new CancellationTokenSource(Bound(leaseDuration));
            try
            {
                await releaseIfOwned(releaseBound.Token);
            }
            catch (Exception exception)
            {
                releaseFailed(exception);
            }
            return (null, true);
        }
    }
}
