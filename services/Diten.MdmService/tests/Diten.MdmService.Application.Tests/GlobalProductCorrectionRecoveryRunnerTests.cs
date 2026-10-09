using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Api.Services.ProductItemSkuMaster;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class GlobalProductCorrectionRecoveryRunnerTests
{
    [Fact]
    public void Command_line_requires_one_exact_argument()
    {
        Assert.True(GlobalProductCorrectionRecoveryCommandLine.IsRequested(
            [GlobalProductCorrectionRecoveryCommandLine.ExactArgument]));
        Assert.False(GlobalProductCorrectionRecoveryCommandLine.IsRequested([]));
        Assert.Throws<InvalidOperationException>(() => GlobalProductCorrectionRecoveryCommandLine.IsRequested(
            [GlobalProductCorrectionRecoveryCommandLine.ExactArgument.ToUpperInvariant()]));
        Assert.Throws<InvalidOperationException>(() => GlobalProductCorrectionRecoveryCommandLine.IsRequested(
            [GlobalProductCorrectionRecoveryCommandLine.ExactArgument,
                GlobalProductCorrectionRecoveryCommandLine.ExactArgument]));
    }

    [Fact]
    public async Task Default_disabled_runner_performs_no_scope_or_repository_work()
    {
        await using var provider = new ServiceCollection().BuildServiceProvider();
        var runner = new GlobalProductCorrectionRecoveryRunner(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new GlobalProductCorrectionWorkflowWorkerOptions()), TimeProvider.System);

        var result = await runner.RunCycleAsync();

        Assert.Equal(new GlobalProductCorrectionRecoveryRunResult(0, 0, 0, 0, 0), result);
    }

    [Fact]
    public async Task Command_line_propagates_cancellation_before_runner_work()
    {
        var runner = new GlobalProductCorrectionRecoveryRunner(null!,
            Options.Create(new GlobalProductCorrectionWorkflowWorkerOptions()), TimeProvider.System);
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            GlobalProductCorrectionRecoveryCommandLine.RunAsync(runner, source.Token));
    }
}
