namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

/// <summary>
/// BL-607 (WP-MDM-BL607-01) — WHEN TO ASK THE ENGINE AGAIN ABOUT AN APPROVAL THAT IS STILL OPEN. An open approval waits for a
/// person; it is not a failure, so it never counts toward the retry limit. But it must not be polled flat either (the S2 FIX1
/// review finding: a flat <c>now + retryDelay</c> puts thousands of open approvals in front of the decided ones every 30 s).
/// <para>The next look is as far away as the approval has been open — never closer than <c>retryDelay</c>, never further than
/// the ceiling. Measured from the moment the engine OPENED the approval (<c>WorkflowStartedAtUtcTicksV1</c>, written once
/// when the start is acknowledged), not from the last look: the gaps double by themselves (30 s, 1 min, 2 min, … 15 min) with
/// no counter to keep, and a decision is noticed at most one ceiling after it is made.</para>
/// <para>The ceiling is <see cref="ProductIdentityRecoveryStreak.DefaultMaxRetryDelay"/> (15 min, the faults' ceiling too) —
/// or <c>retryDelay</c> itself when an operator configured a longer one: the configured delay is never shortened.</para>
/// </summary>
public static class ProductIdentityOpenApprovalBackoff
{
    public static TimeSpan Ceiling(TimeSpan retryDelay) =>
        retryDelay > ProductIdentityRecoveryStreak.DefaultMaxRetryDelay ? retryDelay : ProductIdentityRecoveryStreak.DefaultMaxRetryDelay;

    public static TimeSpan Delay(long? openSinceUtcTicks, DateTimeOffset now, TimeSpan retryDelay)
    {
        var open = openSinceUtcTicks is { } since && since <= now.UtcTicks ? TimeSpan.FromTicks(now.UtcTicks - since) : TimeSpan.Zero;
        var ceiling = Ceiling(retryDelay);
        return open < retryDelay ? retryDelay : open > ceiling ? ceiling : open;
    }

    public static long NextLookAtUtcTicks(long? openSinceUtcTicks, DateTimeOffset now, TimeSpan retryDelay) =>
        now.Add(Delay(openSinceUtcTicks, now, retryDelay)).UtcTicks;
}
