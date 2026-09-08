using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Api.Services.ProductItemSkuMaster;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class GlobalProductRetirementRequestRecoveryRunnerTests
{
    [Fact]
    public void Command_line_requires_one_exact_argument()
    {
        Assert.True(GlobalProductRetirementRequestRecoveryCommandLine.IsRequested(
            [GlobalProductRetirementRequestRecoveryCommandLine.ExactArgument]));
        Assert.False(GlobalProductRetirementRequestRecoveryCommandLine.IsRequested([]));
        Assert.Throws<InvalidOperationException>(() => GlobalProductRetirementRequestRecoveryCommandLine.IsRequested(
            [GlobalProductRetirementRequestRecoveryCommandLine.ExactArgument.ToUpperInvariant()]));
        Assert.Throws<InvalidOperationException>(() => GlobalProductRetirementRequestRecoveryCommandLine.IsRequested(
            [GlobalProductRetirementRequestRecoveryCommandLine.ExactArgument,
                GlobalProductRetirementRequestRecoveryCommandLine.ExactArgument]));
    }

    [Fact]
    public async Task Disabled_runner_is_noop_without_creating_scope_or_mutating_state()
    {
        var runner = new GlobalProductRetirementRequestRecoveryRunner(null!,
            Options.Create(new GlobalProductRetirementRequestWorkflowWorkerOptions { Enabled = false }),
            TimeProvider.System);

        var result = await runner.RunCycleAsync();

        Assert.Equal(new GlobalProductRetirementRequestRecoveryRunResult(0, 0, 0, 0, 0), result);
    }

    [Fact]
    public async Task Invalid_worker_options_fail_before_scope_creation()
    {
        var runner = new GlobalProductRetirementRequestRecoveryRunner(null!,
            Options.Create(new GlobalProductRetirementRequestWorkflowWorkerOptions
            { Enabled = true, LeaseSeconds = 1 }), TimeProvider.System);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunCycleAsync());

        Assert.Equal("GLOBAL_PRODUCT_RETIREMENT_WORKER_CONFIGURATION_INVALID", error.Message);
    }

    [Fact]
    public async Task Command_line_propagates_cancellation_before_runner_work()
    {
        var runner = new GlobalProductRetirementRequestRecoveryRunner(null!,
            Options.Create(new GlobalProductRetirementRequestWorkflowWorkerOptions()), TimeProvider.System);
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            GlobalProductRetirementRequestRecoveryCommandLine.RunAsync(runner, source.Token));
    }
}
