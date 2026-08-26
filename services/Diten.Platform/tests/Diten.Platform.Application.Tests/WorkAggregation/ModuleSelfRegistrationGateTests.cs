using System.Diagnostics;
using Diten.Platform.API.Services.ModuleRegistration;
using Xunit;

namespace Diten.Platform.Application.Tests.WorkAggregation;

// The startup ordering gate that stops the A1 permission worker from stamping a module's permission with
// Module="platform" + Scope=PlatformAdmin before the manifest reconcile can claim it (a scope AuthService can
// never downgrade). The guarantee must come from a real completion signal, and it must never be able to hang
// startup — both properties are asserted here.
public sealed class ModuleSelfRegistrationGateTests
{
    [Fact]
    public async Task Waits_until_completion_is_signalled()
    {
        var gate = new ModuleSelfRegistrationGate();
        Assert.False(gate.IsCompleted);

        var wait = gate.WaitForCompletionAsync(TimeSpan.FromSeconds(30), CancellationToken.None);
        Assert.False(wait.IsCompleted); // still blocked — nothing signalled yet

        gate.MarkCompleted();

        Assert.True(await wait);
        Assert.True(gate.IsCompleted);
    }

    [Fact]
    public async Task Returns_immediately_when_already_completed()
    {
        var gate = new ModuleSelfRegistrationGate();
        gate.MarkCompleted();

        var stopwatch = Stopwatch.StartNew();
        var completed = await gate.WaitForCompletionAsync(TimeSpan.FromSeconds(30), CancellationToken.None);
        stopwatch.Stop();

        Assert.True(completed);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), "An already-completed gate must not wait.");
    }

    [Fact]
    public async Task Falls_back_after_the_timeout_so_a_stuck_manifest_cannot_block_permissions_forever()
    {
        var gate = new ModuleSelfRegistrationGate();

        // Never signalled — the caller must be released with `false` so it can proceed and log the fallback.
        var completed = await gate.WaitForCompletionAsync(TimeSpan.FromMilliseconds(50), CancellationToken.None);

        Assert.False(completed);
        Assert.False(gate.IsCompleted);
    }

    [Fact]
    public async Task Returns_false_on_host_shutdown_rather_than_throwing()
    {
        var gate = new ModuleSelfRegistrationGate();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var completed = await gate.WaitForCompletionAsync(TimeSpan.FromSeconds(30), cts.Token);

        Assert.False(completed);
    }

    [Fact]
    public void MarkCompleted_is_idempotent_so_it_is_safe_in_a_finally_block()
    {
        var gate = new ModuleSelfRegistrationGate();
        gate.MarkCompleted();
        gate.MarkCompleted(); // must not throw (finally + explicit call can both fire)
        Assert.True(gate.IsCompleted);
    }

    [Fact]
    public void Default_timeout_is_bounded()
    {
        Assert.True(ModuleSelfRegistrationGate.DefaultWaitTimeout > TimeSpan.Zero);
        Assert.True(ModuleSelfRegistrationGate.DefaultWaitTimeout <= TimeSpan.FromMinutes(5));
    }
}
