using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;

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
            Assert.True(doors.TryAcquireTenantLinkDoor(PasswordDoorRateLimiter.TenantLinkDoor.Check, "proxy", "victim@acme.test", countPerClient: false).Allowed);

        Assert.True(doors.TryAcquireTenantLinkDoor(PasswordDoorRateLimiter.TenantLinkDoor.Check, "proxy", "victim@acme.test", countPerClient: false).Allowed);
        Assert.True(doors.TryAcquireTenantLinkDoor(PasswordDoorRateLimiter.TenantLinkDoor.Redeem, "proxy", "victim@acme.test", countPerClient: false).Allowed);
    }

    /// <summary>FIX5 addendum (small item) — a link door refused by the per-minute address bucket says that minute (60 s),
    /// not the per-client quarter-hour (900 s) it said before; the per-client refusal says its own window.</summary>
    [Fact]
    public void A_refused_link_door_says_the_window_that_refused_it()
    {
        using var doors = new PasswordDoorRateLimiter();

        for (var i = 0; i < PasswordDoorRateLimiter.DefaultUnidentifiedLinkPerMinute; i++)
            Assert.True(doors.TryAcquireTenantLinkDoor(PasswordDoorRateLimiter.TenantLinkDoor.Check, "proxy", "victim@acme.test", countPerClient: false).Allowed);
        var bucket = doors.TryAcquireTenantLinkDoor(PasswordDoorRateLimiter.TenantLinkDoor.Check, "proxy", "victim@acme.test", countPerClient: false);
        Assert.Equal((false, PasswordDoorRateLimiter.UnidentifiedWindow), (bucket.Allowed, bucket.RetryAfter));

        for (var i = 0; i < PasswordDoorRateLimiter.DefaultPerClientLimit; i++)
            Assert.True(doors.TryAcquireTenantLinkDoor(PasswordDoorRateLimiter.TenantLinkDoor.Redeem, "10.0.0.9", "a@acme.test", countPerClient: true).Allowed);
        var client = doors.TryAcquireTenantLinkDoor(PasswordDoorRateLimiter.TenantLinkDoor.Redeem, "10.0.0.9", "a@acme.test", countPerClient: true);
        Assert.Equal((false, doors.Window), (client.Allowed, client.RetryAfter));
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
    /// 30 wrong ones do, for what is LEFT of the client's window (FIX5 addendum: the time left, not the whole window).</summary>
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
        Assert.InRange(refused.RetryAfter, doors.Window - TimeSpan.FromSeconds(30), doors.Window);
        Assert.True(doors.TryAcquireLoginDoor(PasswordDoorRateLimiter.LoginDoor.Tenant, "10.0.0.1", countPerClient: true, null).Allowed);
    }

    /// <summary>FIX5 addendum (small item) — the wrong-attempt refusal says the time LEFT in the window opened by the client's
    /// first wrong attempt: a 2-second window refused after 1.2 s says at most 0.8 s, and once it ends the client is let in.</summary>
    [Fact]
    public async Task The_wrong_attempt_refusal_says_the_time_left_and_lets_the_client_in_when_the_window_ends()
    {
        using var doors = new PasswordDoorRateLimiter(PasswordDoorRateLimiter.DefaultPerAddressLimit,
            PasswordDoorRateLimiter.DefaultPerClientLimit, TimeSpan.FromSeconds(2));
        for (var i = 0; i < PasswordDoorRateLimiter.DefaultLoginFailuresPerClientLimit; i++)
            doors.RecordLoginFailure(PasswordDoorRateLimiter.LoginDoor.Tenant, "10.0.0.3");

        await Task.Delay(TimeSpan.FromMilliseconds(1200));
        var refused = doors.TryAcquireLoginDoor(PasswordDoorRateLimiter.LoginDoor.Tenant, "10.0.0.3", countPerClient: true, null);
        Assert.False(refused.Allowed);
        Assert.InRange(refused.RetryAfter, TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(800));

        await Task.Delay(refused.RetryAfter + TimeSpan.FromMilliseconds(100));
        Assert.True(doors.TryAcquireLoginDoor(PasswordDoorRateLimiter.LoginDoor.Tenant, "10.0.0.3", countPerClient: true, null).Allowed);
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

    /// <summary>FIX5 addendum item 1 — the CPU guard is the password hasher's (<see cref="PasswordWorkGate"/>): at most
    /// <c>Slots</c> pieces of work at once, at most <c>QueueLength</c> waiting, each refused once it has waited
    /// <c>QueueTimeout</c>; past the queue the refusal is immediate; a slot given back lets a waiting one in. FIX6 S2 — the
    /// waiting is <c>EnterAsync</c>'s (the synchronous <c>Enter</c> never waits).</summary>
    [Fact]
    public async Task The_password_work_slots_are_limited_with_a_short_queue_that_times_out()
    {
        using var gate = new PasswordWorkGate(slots: 2, queueLength: 2, queueTimeout: TimeSpan.FromMilliseconds(300));
        var held = new[] { gate.Enter(), gate.Enter() };

        // Waited its turn, then refused — on a task, so a queue without a timeout fails here instead of hanging the run.
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var waited = Task.Run(() => gate.EnterAsync());
        try
        {
            await Assert.ThrowsAsync<Diten.AuthService.Application.Common.Exceptions.PasswordWorkCapacityExceededException>(
                () => waited.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            if (!waited.IsCompleted) { foreach (var slot in held) slot.Dispose(); (await waited).Dispose(); }
        }
        Assert.InRange(timer.Elapsed, TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(5));

        using var slowGate = new PasswordWorkGate(slots: 1, queueLength: 2, queueTimeout: TimeSpan.FromSeconds(10));
        var slow = slowGate.Enter();
        using var started = new CountdownEvent(2);
        var queued = Enumerable.Range(0, 2).Select(_ => Task.Run(() => { started.Signal(); return slowGate.EnterAsync(); })).ToList();
        Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
        await Task.Delay(100);
        timer.Restart();
        await Assert.ThrowsAsync<Diten.AuthService.Application.Common.Exceptions.PasswordWorkCapacityExceededException>(() => slowGate.EnterAsync());
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(2), "past the queue the refusal does not wait");

        slow.Dispose();
        var first = await Task.WhenAny(queued).WaitAsync(TimeSpan.FromSeconds(5));
        (await first).Dispose();
        foreach (var task in queued.Where(task => task != first)) (await task.WaitAsync(TimeSpan.FromSeconds(5))).Dispose();
        foreach (var slot in held) slot.Dispose();
    }

    /// <summary>FIX5 addendum item 1 — THE DOOR'S FILTER TAKES NO PASSWORD-WORK SLOT. Everything after the filter (reading and
    /// binding the body, the Platform call, the store) runs inside <c>next()</c>; with a gate of ONE slot, <c>next()</c> takes
    /// it at once — had the filter taken it first (FIX4: before the body was read, held for the whole request; a trickled body
    /// held it ~34 s), <c>next()</c> would be refused. Measured on the filter itself: the test server did not start the
    /// pipeline before a trickled body was complete, so an HTTP trickle test could not tell the two apart (its sabotage stayed
    /// green).</summary>
    [Fact]
    public async Task The_door_filter_takes_no_password_work_slot_before_the_request_is_bound()
    {
        using var gate = new PasswordWorkGate(slots: 1, queueLength: 0, queueTimeout: TimeSpan.FromMilliseconds(10));
        using var doors = new PasswordDoorRateLimiter();
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .AddSingleton(gate).AddSingleton(doors).AddSingleton(new ClientAddressResolver([]))
            .BuildServiceProvider();
        var http = new Microsoft.AspNetCore.Http.DefaultHttpContext { RequestServices = services };
        var action = new Microsoft.AspNetCore.Mvc.ActionContext(http, new Microsoft.AspNetCore.Routing.RouteData(),
            new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        var executing = new Microsoft.AspNetCore.Mvc.Filters.ResourceExecutingContext(action,
            new List<Microsoft.AspNetCore.Mvc.Filters.IFilterMetadata>(), new List<Microsoft.AspNetCore.Mvc.ModelBinding.IValueProviderFactory>());
        var filter = new Diten.AuthService.Api.Controllers.Common.AnonymousDoorLimitAttribute(PasswordDoorRateLimiter.LoginDoor.Tenant);

        var slotFreeInsideNext = false;
        await filter.OnResourceExecutionAsync(executing, () =>
        {
            using (gate.Enter()) slotFreeInsideNext = true; // throws if the filter holds the only slot
            return Task.FromResult(new Microsoft.AspNetCore.Mvc.Filters.ResourceExecutedContext(action,
                new List<Microsoft.AspNetCore.Mvc.Filters.IFilterMetadata>()));
        });

        Assert.True(slotFreeInsideNext);
    }

    /// <summary>FIX6 N7 — a wrong VERIFICATION CODE uses up the client's guessing count like a wrong password: after the
    /// allowance, the second-factor door refuses the client before the request goes on.</summary>
    [Fact]
    public async Task A_wrong_verification_code_uses_up_the_clients_guessing_count()
    {
        using var doors = new PasswordDoorRateLimiter();
        var wrongCode = Refusal(401, Diten.AuthService.Application.Common.AuthRefusalCodes.InvalidVerificationCode);

        for (var i = 0; i < PasswordDoorRateLimiter.DefaultLoginFailuresPerClientLimit; i++)
            Assert.True(await ThroughTheDoorAsync(doors, PasswordDoorRateLimiter.LoginDoor.Mfa, wrongCode));

        Assert.False(await ThroughTheDoorAsync(doors, PasswordDoorRateLimiter.LoginDoor.Mfa, wrongCode));
    }

    /// <summary>FIX6 N7 — the password-work gate's 429 is not a wrong guess: a client refused for capacity more times than its
    /// wrong-attempt allowance is never refused for guessing.</summary>
    [Fact]
    public async Task The_gates_429_does_not_use_up_the_clients_guessing_count()
    {
        using var doors = new PasswordDoorRateLimiter();
        var busy = Refusal(429, Diten.AuthService.Application.Common.AuthRefusalCodes.TooManyRequests);

        for (var i = 0; i <= 2 * PasswordDoorRateLimiter.DefaultLoginFailuresPerClientLimit; i++)
            Assert.True(await ThroughTheDoorAsync(doors, PasswordDoorRateLimiter.LoginDoor.Tenant, busy));
    }

    private static Microsoft.AspNetCore.Mvc.ObjectResult Refusal(int status, string code) =>
        new(Diten.AuthService.Application.Common.Response<Diten.AuthService.Application.Common.NoContent>.Fail(
            "refused", [new Diten.AuthService.Application.Common.ResponseError(code)], status)) { StatusCode = status };

    /// <summary>One request from one IDENTIFIED client (a listed proxy exists; the peer forwards for nobody) through the door's
    /// filter, answered with <paramref name="answer"/>. True when the filter let it on.</summary>
    private static async Task<bool> ThroughTheDoorAsync(PasswordDoorRateLimiter doors, PasswordDoorRateLimiter.LoginDoor door,
        Microsoft.AspNetCore.Mvc.IActionResult answer)
    {
        var services = new ServiceCollection()
            .AddSingleton(doors).AddSingleton(new ClientAddressResolver([System.Net.IPAddress.Parse("10.9.9.9")]))
            .BuildServiceProvider();
        var http = new Microsoft.AspNetCore.Http.DefaultHttpContext { RequestServices = services };
        http.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.77");
        var action = new Microsoft.AspNetCore.Mvc.ActionContext(http, new Microsoft.AspNetCore.Routing.RouteData(),
            new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        var filters = new List<Microsoft.AspNetCore.Mvc.Filters.IFilterMetadata>();
        var executing = new Microsoft.AspNetCore.Mvc.Filters.ResourceExecutingContext(action, filters,
            new List<Microsoft.AspNetCore.Mvc.ModelBinding.IValueProviderFactory>());

        var reached = false;
        await new Diten.AuthService.Api.Controllers.Common.AnonymousDoorLimitAttribute(door).OnResourceExecutionAsync(executing, () =>
        {
            reached = true;
            return Task.FromResult(new Microsoft.AspNetCore.Mvc.Filters.ResourceExecutedContext(action, filters) { Result = answer });
        });
        return reached;
    }

    /// <summary>FIX5 addendum item 1 — the production default waits at most 2 s, and the production hasher goes through it.</summary>
    [Fact]
    public void The_production_gate_waits_two_seconds_and_bounds_the_hasher()
    {
        using var gate = new PasswordWorkGate();
        Assert.Equal(TimeSpan.FromSeconds(2), gate.QueueTimeout);
        Assert.Equal(Math.Max(2, Environment.ProcessorCount), gate.Slots);

        using var shut = new PasswordWorkGate(slots: 1, queueLength: 0, queueTimeout: TimeSpan.FromMilliseconds(10));
        using var held = shut.Enter();
        var hasher = new BoundedPasswordHasher(new Diten.AuthService.Infrastructure.Services.PasswordHasher(), shut);
        Assert.Throws<Diten.AuthService.Application.Common.Exceptions.PasswordWorkCapacityExceededException>(() => hasher.Verify("x", "y"));
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
