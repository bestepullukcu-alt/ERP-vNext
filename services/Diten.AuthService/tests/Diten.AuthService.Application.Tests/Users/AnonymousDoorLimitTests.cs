using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Infrastructure.Security;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// WP-USERS-ROLES-CLOSE-FIX1 FIX3 items 2–4 — the anonymous doors' counts, on the production limiter itself:
/// <list type="bullet">
/// <item>when clients CANNOT be told apart there is no per-address count: junk for a victim's address does not lock the
/// victim's own requests out (FIX2 allowed 5 per address — an anonymous lock-out); one coarse ceiling per door applies;</item>
/// <item>the doors' counts are separate (the tenant sign-in, the platform sign-in, the link check);</item>
/// <item>the anonymous audit budget says when it starts dropping, and how many the last window dropped.</item>
/// </list>
/// </summary>
public sealed class AnonymousDoorLimitTests
{
    [Fact]
    public void Junk_for_a_victims_address_does_not_lock_the_victims_link_out_when_clients_cannot_be_told_apart()
    {
        using var doors = new PasswordDoorRateLimiter();

        for (var i = 0; i < 4 * PasswordDoorRateLimiter.DefaultPerAddressLimit; i++)
            Assert.True(doors.TryAcquireTenantLinkDoor(PasswordDoorRateLimiter.TenantLinkDoor.Check, "proxy", "victim@acme.test", countPerClient: false));

        Assert.True(doors.TryAcquireTenantLinkDoor(PasswordDoorRateLimiter.TenantLinkDoor.Check, "proxy", "victim@acme.test", countPerClient: false));
        Assert.True(doors.TryAcquireTenantLinkDoor(PasswordDoorRateLimiter.TenantLinkDoor.Redeem, "proxy", "victim@acme.test", countPerClient: false));
    }

    /// <summary>FIX4 item 1 — without client identity the coarse ceiling is per TENANT on the tenant doors: one tenant's flood
    /// closes its own door for the minute (Retry-After: that minute), not another tenant's, nor the platform door.</summary>
    [Fact]
    public void Without_client_identity_the_coarse_ceiling_is_per_tenant_and_says_its_own_minute()
    {
        using var doors = new PasswordDoorRateLimiter();
        Guid tenantA = Guid.NewGuid(), tenantB = Guid.NewGuid();

        for (var i = 0; i < PasswordDoorRateLimiter.DefaultUnidentifiedPerMinute; i++)
            Assert.True(doors.TryAcquireLoginDoor(PasswordDoorRateLimiter.LoginDoor.Tenant, "proxy", countPerClient: false, tenantA).Allowed);

        var refused = doors.TryAcquireLoginDoor(PasswordDoorRateLimiter.LoginDoor.Tenant, "another-proxy", countPerClient: false, tenantA);
        Assert.False(refused.Allowed);
        Assert.Equal(PasswordDoorRateLimiter.UnidentifiedWindow, refused.RetryAfter);
        Assert.True(doors.TryAcquireLoginDoor(PasswordDoorRateLimiter.LoginDoor.Tenant, "proxy", countPerClient: false, tenantB).Allowed);
        Assert.True(doors.TryAcquireLoginDoor(PasswordDoorRateLimiter.LoginDoor.Platform, "proxy", countPerClient: false, null).Allowed);
    }

    /// <summary>FIX4 item 4 — with client identity every attempt counts against a high ceiling (an office behind one NAT signs
    /// in all morning), and only the WRONG attempts against the low one: 31 successful sign-ins never refuse the client;
    /// 30 wrong ones do, for the client's window.</summary>
    [Fact]
    public void With_client_identity_only_wrong_attempts_use_up_the_guessing_count()
    {
        using var doors = new PasswordDoorRateLimiter();

        for (var i = 0; i <= PasswordDoorRateLimiter.DefaultLoginFailuresPerClientLimit; i++)
            Assert.True(doors.TryAcquireLoginDoor(PasswordDoorRateLimiter.LoginDoor.Tenant, "10.0.0.1", countPerClient: true, null).Allowed); // successes

        for (var i = 0; i < PasswordDoorRateLimiter.DefaultLoginFailuresPerClientLimit; i++)
            doors.RecordLoginFailure(PasswordDoorRateLimiter.LoginDoor.Tenant, "10.0.0.2");

        var refused = doors.TryAcquireLoginDoor(PasswordDoorRateLimiter.LoginDoor.Tenant, "10.0.0.2", countPerClient: true, null);
        Assert.False(refused.Allowed);
        Assert.Equal(doors.Window, refused.RetryAfter);
        Assert.True(doors.TryAcquireLoginDoor(PasswordDoorRateLimiter.LoginDoor.Tenant, "10.0.0.1", countPerClient: true, null).Allowed);
    }

    [Fact]
    public void With_client_identity_every_attempt_counts_against_the_high_ceiling()
    {
        using var doors = new PasswordDoorRateLimiter();

        for (var i = 0; i < PasswordDoorRateLimiter.DefaultLoginPerClientLimit; i++)
            Assert.True(doors.TryAcquireLoginDoor(PasswordDoorRateLimiter.LoginDoor.Platform, "10.0.0.1", countPerClient: true, null).Allowed);

        Assert.False(doors.TryAcquireLoginDoor(PasswordDoorRateLimiter.LoginDoor.Platform, "10.0.0.1", countPerClient: true, null).Allowed);
        Assert.True(doors.TryAcquireLoginDoor(PasswordDoorRateLimiter.LoginDoor.Platform, "10.0.0.2", countPerClient: true, null).Allowed);
    }

    /// <summary>FIX4 item 1 — the CPU guard: at most <c>DefaultConcurrentVerifications</c> verifications at once, a short queue,
    /// and past the queue no slot (the door answers 429); a slot that is given back lets a waiting one in.</summary>
    [Fact]
    public async Task The_verification_slots_are_limited_with_a_short_queue()
    {
        using var doors = new PasswordDoorRateLimiter();
        var held = new List<System.Threading.RateLimiting.RateLimitLease>();
        for (var i = 0; i < PasswordDoorRateLimiter.DefaultConcurrentVerifications; i++)
        {
            var lease = await doors.AcquireVerificationAsync(CancellationToken.None);
            Assert.True(lease.IsAcquired);
            held.Add(lease);
        }
        var queued = Enumerable.Range(0, PasswordDoorRateLimiter.DefaultVerificationQueue)
            .Select(_ => doors.AcquireVerificationAsync(CancellationToken.None).AsTask()).ToList();
        Assert.All(queued, task => Assert.False(task.IsCompleted));

        using var refused = await doors.AcquireVerificationAsync(CancellationToken.None);
        Assert.False(refused.IsAcquired);

        held[0].Dispose();
        var first = await queued[0].WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(first.IsAcquired);
        first.Dispose();
        foreach (var lease in held.Skip(1)) lease.Dispose();
        foreach (var task in queued.Skip(1)) (await task.WaitAsync(TimeSpan.FromSeconds(5))).Dispose();
    }

    /// <summary>FIX4 item 2 — the platform forgot-password door without client identity: fresh addresses each time meet the
    /// door's coarse ceiling (each one would otherwise be an unlimited store read).</summary>
    [Fact]
    public void Without_client_identity_fresh_addresses_meet_the_forgot_password_doors_ceiling()
    {
        using var doors = new PasswordDoorRateLimiter();

        for (var i = 0; i < PasswordDoorRateLimiter.DefaultUnidentifiedPerMinute; i++)
            Assert.True(doors.TryAcquireForgotPassword("proxy", $"x{i}@acme.test", countPerClient: false));

        Assert.False(doors.TryAcquireForgotPassword("proxy", "fresh@acme.test", countPerClient: false));
    }

    [Fact]
    public void A_full_audit_budget_says_so_once_and_reports_how_many_it_dropped()
    {
        var time = new MovableTime(new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero));
        var budget = new AnonymousAuditBudget(time);
        for (var i = 0; i < AnonymousAuditBudget.RowsPerWindow; i++)
            Assert.True(budget.TryTake(out _, out _));

        Assert.False(budget.TryTake(out var first, out _));
        Assert.True(first);
        Assert.False(budget.TryTake(out var second, out _));
        Assert.False(second);

        time.Advance(AnonymousAuditBudget.Window);
        Assert.True(budget.TryTake(out _, out var dropped));
        Assert.Equal(2, dropped);
    }

    private sealed class MovableTime(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }
}
