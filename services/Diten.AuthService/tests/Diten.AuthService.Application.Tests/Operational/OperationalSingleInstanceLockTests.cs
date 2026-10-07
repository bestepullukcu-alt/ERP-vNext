using Diten.AuthService.Api.Operational;

namespace Diten.AuthService.Application.Tests.Operational;

/// <summary>
/// WP-AUTH-BL570-01 — the operational commands' single-instance lock on every OS (the named Semaphore it replaces threw
/// PlatformNotSupportedException outside Windows). Each test uses its own name, so tests never meet each other.
/// </summary>
public sealed class OperationalSingleInstanceLockTests
{
    private static string UniqueName() => "Diten.Auth.Test.SingleInstance." + Guid.NewGuid().ToString("N");

    [Fact]
    public async Task A_second_concurrent_run_is_refused_at_once_and_the_lock_is_free_again_after_release()
    {
        var name = UniqueName();
        using (var first = OperationalSingleInstanceLock.TryAcquire(name))
        {
            Assert.True(first.Acquired);
            Assert.False(first.RecoveredFromAbandonedRun);
            // The concurrent caller runs elsewhere (another thread, as another process would) and must not wait.
            var started = DateTime.UtcNow;
            var second = await Task.Run(() => OperationalSingleInstanceLock.TryAcquire(name));
            Assert.False(second.Acquired);
            Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(5));
            second.Dispose();
        }

        using var third = OperationalSingleInstanceLock.TryAcquire(name);
        Assert.True(third.Acquired);
    }

    // The holder awaits while it holds the lock: its continuation may resume on another thread. Releasing must still
    // work (a mutex taken on one thread cannot be released on another — the lock owns a dedicated thread for that).
    [Fact]
    public async Task The_lock_is_released_after_awaits_that_hop_threads()
    {
        var name = UniqueName();
        var holder = OperationalSingleInstanceLock.TryAcquire(name);
        Assert.True(holder.Acquired);
        await Task.Yield();
        await Task.Delay(10).ConfigureAwait(false);
        holder.Dispose();
        holder.Dispose(); // idempotent

        using var next = OperationalSingleInstanceLock.TryAcquire(name);
        Assert.True(next.Acquired);
    }

    // An owner that ends without releasing (it crashed) does not keep the lock: the next run takes it over and says so.
    // Measured 2026-10-07 on macOS: when the owning PROCESS dies the OS frees the lock outright (the next WaitOne
    // succeeds without the abandoned signal — no robust mutexes there); the abandoned path below is the in-process one.
    [Fact]
    public void An_abandoned_lock_is_taken_over_by_the_next_run_by_name()
    {
        var name = UniqueName();
        var owner = new Thread(() =>
        {
            var mutex = new Mutex(false, name);
            Assert.True(mutex.WaitOne(0));
            // ends without ReleaseMutex: abandoned
        });
        owner.Start();
        owner.Join();

        using var next = OperationalSingleInstanceLock.TryAcquire(name);
        Assert.True(next.Acquired);
        Assert.True(next.RecoveredFromAbandonedRun);
    }
}
