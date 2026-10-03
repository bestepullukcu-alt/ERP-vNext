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
            TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ProductIdentityWorkflowRecoveryRunner>.Instance);

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
            TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ProductIdentityWorkflowRecoveryRunner>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunCycleAsync());

        Assert.Equal("PRODUCT_IDENTITY_WORKFLOW_WORKER_CONFIGURATION_INVALID", exception.Message);
    }

    // P-2026-10-02-04-FIX1 item 8 — a failed cycle is logged by its type and its number, never with the exception:
    // a message or a stack can carry whatever the failing layer put into it.
    [Fact]
    public async Task Worker_logs_a_failed_cycle_by_type_and_number_and_never_the_exception_itself()
    {
        // The cycle fails inside the runner: the transport readiness is not registered in this container.
        using var services = new ServiceCollection().BuildServiceProvider();
        var settings = Options.Create(new ProductIdentityWorkflowWorkerOptions
        {
            Enabled = true, LeaseOwner = "worker", PollIntervalSeconds = 3600
        });
        var log = new Lines();
        var worker = new ProductIdentityWorkflowRecoveryWorker(
            new ProductIdentityWorkflowRecoveryRunner(
                services.GetRequiredService<IServiceScopeFactory>(), settings, TimeProvider.System,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<ProductIdentityWorkflowRecoveryRunner>.Instance),
            settings, log);

        await worker.StartAsync(CancellationToken.None);
        await log.First.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await worker.StopAsync(CancellationToken.None);

        var line = Assert.Single(log.Entries);
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Error, line.Level);
        Assert.Contains("cycle 1 failed with System.InvalidOperationException", line.Message, StringComparison.Ordinal);
        Assert.Null(line.Exception);
        Assert.DoesNotContain("IProductIdentityWorkflowTransportReadiness", line.Message, StringComparison.Ordinal);
    }

    private sealed class Lines : Microsoft.Extensions.Logging.ILogger<ProductIdentityWorkflowRecoveryWorker>
    {
        public List<(Microsoft.Extensions.Logging.LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];
        public TaskCompletionSource First { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
            TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception), exception));
            First.TrySetResult();
        }
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
            TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ProductIdentityWorkflowRecoveryRunner>.Instance);
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            ProductIdentityWorkflowRecoveryCommandLine.RunAsync(runner, source.Token));
    }
}
