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

    [Fact]
    public void Without_client_identity_each_door_has_one_coarse_ceiling_for_everybody()
    {
        using var doors = new PasswordDoorRateLimiter();

        for (var i = 0; i < PasswordDoorRateLimiter.DefaultUnidentifiedPerMinute; i++)
            Assert.True(doors.TryAcquireLoginDoor(PasswordDoorRateLimiter.LoginDoor.Tenant, "proxy", countPerClient: false));

        Assert.False(doors.TryAcquireLoginDoor(PasswordDoorRateLimiter.LoginDoor.Tenant, "another-proxy", countPerClient: false));
        // its own count: the other doors are not used up
        Assert.True(doors.TryAcquireLoginDoor(PasswordDoorRateLimiter.LoginDoor.Platform, "proxy", countPerClient: false));
        Assert.True(doors.TryAcquireTenantLinkDoor(PasswordDoorRateLimiter.TenantLinkDoor.Check, "proxy", "a@b.test", countPerClient: false));
    }

    [Fact]
    public void With_client_identity_the_sign_in_door_counts_per_client()
    {
        using var doors = new PasswordDoorRateLimiter();

        for (var i = 0; i < PasswordDoorRateLimiter.DefaultLoginPerClientLimit; i++)
            Assert.True(doors.TryAcquireLoginDoor(PasswordDoorRateLimiter.LoginDoor.Platform, "10.0.0.1", countPerClient: true));

        Assert.False(doors.TryAcquireLoginDoor(PasswordDoorRateLimiter.LoginDoor.Platform, "10.0.0.1", countPerClient: true));
        Assert.True(doors.TryAcquireLoginDoor(PasswordDoorRateLimiter.LoginDoor.Platform, "10.0.0.2", countPerClient: true));
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
