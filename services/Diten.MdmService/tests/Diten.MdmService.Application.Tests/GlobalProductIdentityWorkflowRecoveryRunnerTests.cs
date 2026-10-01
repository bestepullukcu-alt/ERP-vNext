using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Api.Services.ProductItemSkuMaster;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class GlobalProductIdentityWorkflowRecoveryRunnerTests
{
    [Fact]
    public async Task Disabled_runner_is_noop_before_service_resolution()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var runner = new ProductIdentityWorkflowRecoveryRunner(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new ProductIdentityWorkflowWorkerOptions()),
            TimeProvider.System);

        var result = await runner.RunCycleAsync();

        Assert.Equal(new ProductIdentityWorkflowRecoveryRunResult(0, 0, 0, 0, 0), result);
    }

    [Fact]
    public async Task Enabled_invalid_configuration_fails_before_service_resolution()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var runner = new ProductIdentityWorkflowRecoveryRunner(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new ProductIdentityWorkflowWorkerOptions { Enabled = true }),
            TimeProvider.System);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunCycleAsync());

        Assert.Equal("PRODUCT_IDENTITY_WORKFLOW_WORKER_CONFIGURATION_INVALID", exception.Message);
    }

    [Fact]
    public void Command_line_argument_is_exact_ordinal_and_duplicate_safe()
    {
        Assert.True(ProductIdentityWorkflowRecoveryCommandLine.IsRequested(
            [ProductIdentityWorkflowRecoveryCommandLine.ExactArgument]));
        Assert.False(ProductIdentityWorkflowRecoveryCommandLine.IsRequested([]));
        Assert.Throws<InvalidOperationException>(() =>
            ProductIdentityWorkflowRecoveryCommandLine.IsRequested(
                [ProductIdentityWorkflowRecoveryCommandLine.ExactArgument.ToUpperInvariant()]));
        Assert.Throws<InvalidOperationException>(() =>
            ProductIdentityWorkflowRecoveryCommandLine.IsRequested(
                [ProductIdentityWorkflowRecoveryCommandLine.ExactArgument,
                    ProductIdentityWorkflowRecoveryCommandLine.ExactArgument]));
    }

    [Fact]
    public async Task Command_line_runner_propagates_caller_cancellation()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var runner = new ProductIdentityWorkflowRecoveryRunner(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new ProductIdentityWorkflowWorkerOptions()),
            TimeProvider.System);
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            ProductIdentityWorkflowRecoveryCommandLine.RunAsync(runner, source.Token));
    }
}
