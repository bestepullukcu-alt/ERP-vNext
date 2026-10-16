using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// BL-607 (WP-MDM-BL607-01) — the two rules an open LSKU approval now follows, pinned without a host:
/// the next look is doubling and capped (never a flat poll), and the open-approval alert threshold is refused at start when
/// it is not longer than the backoff ceiling plus one poll interval (never quietly raised).
/// </summary>
public sealed class LskuOpenApprovalBackoffAndThresholdTests
{
    private static readonly DateTimeOffset Opened = new(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

    [Fact]
    public void Successive_looks_at_an_open_approval_double_from_the_retry_delay_and_stop_at_the_ceiling()
    {
        var now = Opened;
        var gaps = new List<TimeSpan>();
        for (var look = 0; look < 40; look++)
        {
            var next = new DateTimeOffset(ProductIdentityOpenApprovalBackoff.NextLookAtUtcTicks(Opened.UtcTicks, now, RetryDelay), TimeSpan.Zero);
            gaps.Add(next - now);
            now = next;
        }

        Assert.Equal(RetryDelay, gaps[0]);                                   // never a look sooner than the configured delay
        Assert.Equal(RetryDelay, gaps[1]);                                   // open 30 s → wait 30 s (now open 60 s)
        Assert.Equal(TimeSpan.FromMinutes(1), gaps[2]);                      // doubling …
        Assert.Equal(TimeSpan.FromMinutes(2), gaps[3]);
        Assert.Equal(TimeSpan.FromMinutes(4), gaps[4]);
        Assert.All(gaps, gap => Assert.True(gap <= ProductIdentityRecoveryStreak.DefaultMaxRetryDelay, $"{gap} past the ceiling"));
        Assert.Equal(ProductIdentityRecoveryStreak.DefaultMaxRetryDelay, gaps[^1]);
        // 40 looks span ~8.5 hours — the old rule had made this approval "stuck" at the 30th.
        Assert.True(now - Opened > TimeSpan.FromHours(8));
        // Not a flat poll: 40 looks, not the ~1000 a 30-second poll would make over the same time.
        Assert.True((now - Opened) / RetryDelay > 900);
    }

    [Fact]
    public void A_configured_delay_longer_than_the_ceiling_is_never_shortened_and_an_unknown_opening_waits_the_delay()
    {
        var longDelay = TimeSpan.FromMinutes(30);
        Assert.Equal(longDelay, ProductIdentityOpenApprovalBackoff.Ceiling(longDelay));
        Assert.Equal(longDelay, ProductIdentityOpenApprovalBackoff.Delay(Opened.UtcTicks, Opened.AddHours(5), longDelay));
        Assert.Equal(RetryDelay, ProductIdentityOpenApprovalBackoff.Delay(null, Opened.AddHours(5), RetryDelay));
        // A clock behind (opened "in the future"): the configured delay. BL-607 FIX1 — this pins the BEHAVIOUR only: without the
        // `since <= now` guard the open time would be negative and the "never sooner than retryDelay" clamp would give the same
        // answer (an equivalent mutant), so no test can tell the guard is there; it stays for a readable TimeSpan.
        Assert.Equal(RetryDelay, ProductIdentityOpenApprovalBackoff.Delay(Opened.AddHours(1).UtcTicks, Opened, RetryDelay));
    }

    /// <summary>BL-607 FIX1 — an approval whose opening was never recorded backs off from the request's creation, not a flat
    /// retryDelay for good; the recorded opening wins when there is one.</summary>
    [Fact]
    public void An_unknown_opening_backs_off_from_the_requests_creation()
    {
        var now = Opened.AddHours(3);
        var created = Opened.UtcTicks;

        Assert.Equal(now.Add(ProductIdentityRecoveryStreak.DefaultMaxRetryDelay).UtcTicks,
            ProductIdentityOpenApprovalBackoff.NextLookAtUtcTicks(null, created, now, RetryDelay));          // 3 h old → the ceiling
        Assert.Equal(now.Add(TimeSpan.FromMinutes(2)).UtcTicks,
            ProductIdentityOpenApprovalBackoff.NextLookAtUtcTicks(now.AddMinutes(-2).UtcTicks, created, now, RetryDelay)); // the opening wins
        Assert.Equal(now.Add(RetryDelay).UtcTicks,
            ProductIdentityOpenApprovalBackoff.NextLookAtUtcTicks(null, 0, now, RetryDelay));                // nothing known → the delay
    }

    private static LskuIdentityWorkflowWorkerOptions Options(int alertAfterMinutes, int pollSeconds = 15, int retrySeconds = 30) => new()
    {
        Enabled = true,
        LeaseOwner = "mdm-test",
        PollIntervalSeconds = pollSeconds,
        RetryDelaySeconds = retrySeconds,
        OpenApprovalAlertAfterMinutes = alertAfterMinutes
    };

    [Fact]
    public void The_default_threshold_is_valid_and_a_threshold_inside_the_ceiling_plus_one_poll_is_refused_by_name()
    {
        var defaults = new LskuIdentityWorkflowWorkerOptions { Enabled = true, LeaseOwner = "mdm-test" };
        Assert.Equal(1440, defaults.OpenApprovalAlertAfterMinutes);
        Assert.True(defaults.OpenApprovalAlertThresholdIsValid());
        defaults.EnsureValidWhenEnabled();

        // 15 min = the ceiling: a decided approval may still look open for ceiling + one poll → an alert on a healthy one.
        Assert.False(Options(15).OpenApprovalAlertThresholdIsValid());
        Assert.False(Options(16, pollSeconds: 120).OpenApprovalAlertThresholdIsValid());   // 16 min ≤ 15 min + 2 min
        Assert.True(Options(16, pollSeconds: 15).OpenApprovalAlertThresholdIsValid());     // 16 min > 15 min + 15 s
        Assert.False(Options(40, retrySeconds: 3600).OpenApprovalAlertThresholdIsValid()); // a 60-min delay is the ceiling
        Assert.False(Options(0).OpenApprovalAlertThresholdIsValid());
        Assert.False(Options(525601).OpenApprovalAlertThresholdIsValid());
        // BL-607 FIX1 — the edges: EQUAL to ceiling + poll is refused (15 min + 120 s = 17 min); the upper bound itself is valid.
        Assert.False(Options(17, pollSeconds: 120).OpenApprovalAlertThresholdIsValid());
        Assert.True(Options(18, pollSeconds: 120).OpenApprovalAlertThresholdIsValid());
        Assert.True(Options(525600).OpenApprovalAlertThresholdIsValid());

        var refused = Assert.Throws<InvalidOperationException>(() => Options(15).EnsureValidWhenEnabled());
        Assert.Equal("LSKU_IDENTITY_WORKFLOW_OPEN_APPROVAL_ALERT_THRESHOLD_INVALID", refused.Message);
    }

    [Fact]
    public void A_disabled_worker_is_not_refused_for_its_threshold()
    {
        var disabled = new LskuIdentityWorkflowWorkerOptions { Enabled = false, OpenApprovalAlertAfterMinutes = 1 };
        disabled.EnsureValidWhenEnabled();
    }
}
