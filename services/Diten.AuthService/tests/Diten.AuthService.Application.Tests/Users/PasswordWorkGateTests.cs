using System.Diagnostics;
using Diten.AuthService.Application.Common.Exceptions;
using Diten.AuthService.Infrastructure.Security;
using Xunit.Abstractions;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// WP-USERS-ROLES-CLOSE-FIX1 FIX6 S2 and N7 — the password-work gate on its own: a wait holds no thread, the synchronous door
/// never waits, a slot is given back whatever the work does, and the queue is short enough to drain within its timeout.
/// </summary>
public sealed class PasswordWorkGateTests(ITestOutputHelper output)
{
    /// <summary>FIX6 S2 — A FULL GATE'S WAITERS HOLD NO THREAD. <c>EnterAsync</c> on a full gate hands back an unfinished task at
    /// once (FIX5's <c>Wait</c> held the calling thread for the whole 2 s); the timeout then ends it with the refusal (429).
    /// Many waiters are started from this one thread and every call returns at once.</summary>
    [Fact]
    public async Task Waiting_for_a_full_gate_holds_no_thread_and_ends_in_the_refusal()
    {
        using var gate = new PasswordWorkGate(slots: 1, queueLength: 8, queueTimeout: TimeSpan.FromMilliseconds(400));
        using var held = gate.Enter();

        var timer = Stopwatch.StartNew();
        var waiting = Enumerable.Range(0, 8).Select(_ => gate.EnterAsync()).ToList();
        var handedBack = timer.Elapsed;

        Assert.All(waiting, task => Assert.False(task.IsCompleted));
        Assert.True(handedBack < TimeSpan.FromMilliseconds(200), $"every waiter was handed back at once ({handedBack})");
        foreach (var task in waiting)
            await Assert.ThrowsAsync<PasswordWorkCapacityExceededException>(() => task.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    /// <summary>FIX6 S2 — the synchronous door takes a free slot or refuses AT ONCE; it never waits on a thread.</summary>
    [Fact]
    public void The_synchronous_door_never_waits()
    {
        using var gate = new PasswordWorkGate(slots: 1, queueLength: 8, queueTimeout: TimeSpan.FromSeconds(5));
        using var held = gate.Enter();

        var timer = Stopwatch.StartNew();
        Assert.Throws<PasswordWorkCapacityExceededException>(() => gate.Enter());
        Assert.True(timer.Elapsed < TimeSpan.FromMilliseconds(500), $"refused at once ({timer.Elapsed})");
    }

    /// <summary>FIX6 N7 — work that throws gives its slot back: the next piece of work gets it at once.</summary>
    [Fact]
    public async Task A_slot_is_given_back_when_the_work_throws()
    {
        using var gate = new PasswordWorkGate(slots: 1, queueLength: 0, queueTimeout: TimeSpan.FromMilliseconds(10));

        await Assert.ThrowsAsync<InvalidOperationException>(() => gate.RunAsync<int>(() => throw new InvalidOperationException("bcrypt failed")));
        Assert.Throws<InvalidOperationException>(() => gate.Run<int>(() => throw new InvalidOperationException("bcrypt failed")));

        using var next = gate.Enter(); // the only slot is free again
    }

    /// <summary>FIX6 S2 — THE QUEUE LENGTH, MEASURED. With work of a BCrypt verification's length (~250 ms), the production
    /// shape — 2× slots waiting behind busy slots — is fully served within the 2-second timeout: nobody in a full queue is
    /// refused for waiting. A longer queue would only turn waits into refusals at the 2-second mark.</summary>
    [Fact]
    public async Task A_full_queue_of_quarter_second_work_drains_within_the_timeout()
    {
        const int slots = 2;
        using var gate = new PasswordWorkGate(slots, 2 * slots, PasswordWorkGate.DefaultQueueTimeout);

        var timer = Stopwatch.StartNew();
        var work = Enumerable.Range(0, slots + 2 * slots)
            .Select(_ => Task.Run(() => gate.RunAsync(() => { Thread.Sleep(250); return true; })))
            .ToList();
        var results = await Task.WhenAll(work).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.All(results, served => Assert.True(served));
        output.WriteLine($"{work.Count} pieces of 250 ms work through {slots} slots: {timer.ElapsedMilliseconds} ms");
        Assert.True(timer.Elapsed < PasswordWorkGate.DefaultQueueTimeout + TimeSpan.FromMilliseconds(250));
    }

    /// <summary>FIX6 S2 — the production BCrypt cost on this machine, so the queue's arithmetic is not assumed: one
    /// verification, the median of three, written to the test output.</summary>
    [Fact]
    public void The_production_bcrypt_cost_is_measured()
    {
        var hasher = new Diten.AuthService.Infrastructure.Services.PasswordHasher();
        var hash = hasher.Hash("measure-" + Guid.NewGuid().ToString("N"));
        var costs = Enumerable.Range(0, 3).Select(attempt =>
        {
            var timer = Stopwatch.StartNew();
            Assert.False(hasher.Verify("wrong-" + attempt, hash));
            return timer.Elapsed;
        }).OrderBy(cost => cost).ToList();

        output.WriteLine($"BCrypt verification (median of 3): {costs[1].TotalMilliseconds:F0} ms; slots {PasswordWorkGate.DefaultSlots}, queue {2 * PasswordWorkGate.DefaultSlots}, timeout {PasswordWorkGate.DefaultQueueTimeout.TotalSeconds:F0} s");
        Assert.True(costs[1] < PasswordWorkGate.DefaultQueueTimeout);
    }
}
