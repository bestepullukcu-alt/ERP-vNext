using System.Diagnostics;
using Diten.AuthService.Api.Operational;
using Diten.AuthService.Application.Tests.ServiceIdentityTokens;

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

    // FIX1 R4 (CT decision A): the lock is MACHINE-WIDE across sessions. A child in ANOTHER Unix session (setsid) holds
    // the name; this process is refused. Without the Global\ prefix the child's lock would be session-scoped and this
    // process would acquire it (measured).
    [UnixFact]
    public async Task A_run_in_another_session_holds_the_lock_and_this_run_is_refused()
    {
        var name = OperationalSingleInstanceLock.MachineWide(UniqueName());
        var host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (string.IsNullOrEmpty(host) && Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet") host = Environment.ProcessPath;
        Assert.True(host is not null && File.Exists(host), "The dotnet host running these tests is required.");
        var start = new ProcessStartInfo("/usr/bin/python3") { UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in new[]
                 {
                     "-c", "import os, sys; os.setsid(); os.execv(sys.argv[1], sys.argv[1:])",
                     host!, "exec", typeof(OperationalLockHolder).Assembly.Location, OperationalLockHolder.Flag, name, "4000"
                 }) start.ArgumentList.Add(argument);
        using var child = Process.Start(start)!;
        var line = await child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(60));
        Assert.NotNull(line);
        Assert.StartsWith("HELD ", line);
        Assert.NotEqual(OperationalLockHolder.GetSid(0), int.Parse(line![5..], System.Globalization.CultureInfo.InvariantCulture));

        using (var mine = OperationalSingleInstanceLock.TryAcquire(name)) Assert.False(mine.Acquired);

        await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60));
        Assert.Equal(0, child.ExitCode);
        using var after = OperationalSingleInstanceLock.TryAcquire(name);
        Assert.True(after.Acquired);
    }

    [Fact]
    public void The_operational_command_uses_the_machine_wide_name()
    {
        Assert.Equal(@"Global\Diten.Auth.ProductIdentityEntitlementReconciliation", EntitlementReconciliationOperationalRunner.LockName);
    }
}
