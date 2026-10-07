using System.Reflection;
using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Api.Services.ProductItemSkuMaster;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class GlobalProductIdentityWorkflowRecoveryRunnerTests
{
    // BL-552 flow 3 FIX1 item 7 (was Disabled_runner_is_noop_before_service_resolution): a runner switched off touches no
    // operation — and still records the stuck gauges, exactly; a worker switched off is when what is stuck stays stuck.
    [Fact]
    public async Task Disabled_runner_touches_no_operation_and_records_the_stuck_gauges_exactly()
    {
        using var metrics = new ProductIdentityRecoveryMetrics();
        using var services = StuckServices(readinessFault: null);
        var runner = new ProductIdentityWorkflowRecoveryRunner(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new ProductIdentityWorkflowWorkerOptions()),
            TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ProductIdentityWorkflowRecoveryRunner>.Instance,
            metrics);

        var result = await runner.RunCycleAsync();

        Assert.Equal(new ProductIdentityWorkflowRecoveryRunResult(0, 0, 0, 0, 0), result);
        Assert.Equal(1, metrics.Current(ProductIdentityWorkflowRecoveryRunner.Flow));
        Assert.Equal(0, metrics.NeedingAPerson(ProductIdentityWorkflowRecoveryRunner.Flow));
    }

    // Flow 3 FIX1 item 7: a cycle that stops on an unready transport records the gauges too.
    [Fact]
    public async Task A_cycle_stopped_by_an_unready_transport_records_the_stuck_gauges_exactly()
    {
        using var metrics = new ProductIdentityRecoveryMetrics();
        using var services = StuckServices(readinessFault: "PRODUCT_WORKFLOW_CLIENT_CONFIGURATION_INVALID");
        var runner = new ProductIdentityWorkflowRecoveryRunner(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new ProductIdentityWorkflowWorkerOptions { Enabled = true, LeaseOwner = "runner" }),
            TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ProductIdentityWorkflowRecoveryRunner>.Instance,
            metrics);

        var result = await runner.RunCycleAsync();

        Assert.Equal(new ProductIdentityWorkflowRecoveryRunResult(0, 0, 0, 0, 0), result);
        Assert.Equal(1, metrics.Current(ProductIdentityWorkflowRecoveryRunner.Flow));
        Assert.Equal(0, metrics.NeedingAPerson(ProductIdentityWorkflowRecoveryRunner.Flow));
    }

    // Flow 3 FIX2 item 4: the HOSTED worker switched off records the gauges too (before: it returned at once and the
    // runner's off branch ran only in the one-shot command line and in tests).
    [Fact]
    public async Task The_hosted_worker_switched_off_records_the_stuck_gauges()
    {
        using var metrics = new ProductIdentityRecoveryMetrics();
        using var services = StuckServices(readinessFault: null);
        var options = Options.Create(new ProductIdentityWorkflowWorkerOptions { PollIntervalSeconds = 3_600 });
        var runner = new ProductIdentityWorkflowRecoveryRunner(
            services.GetRequiredService<IServiceScopeFactory>(), options, TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ProductIdentityWorkflowRecoveryRunner>.Instance, metrics);
        using var worker = new ProductIdentityWorkflowRecoveryWorker(runner, options,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ProductIdentityWorkflowRecoveryWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        for (var wait = 0; wait < 100 && metrics.Current(ProductIdentityWorkflowRecoveryRunner.Flow) is null; wait++)
            await Task.Delay(20);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(1, metrics.Current(ProductIdentityWorkflowRecoveryRunner.Flow));
        Assert.Equal(0, metrics.NeedingAPerson(ProductIdentityWorkflowRecoveryRunner.Flow));
    }

    private static readonly Guid StuckTenant = Guid.Parse("90000000-0000-0000-0000-0000000000a7");

    // One tenant holds one stuck submit its submitter's repeat can settle: (1 in manual reconciliation, 0 for a person).
    // The work discovery must never be asked — no operation is touched.
    private static ServiceProvider StuckServices(string? readinessFault)
    {
        var counts = DispatchProxy.Create<IGlobalProductIdentityWorkflowOperationRepository, StuckCounts>();
        return new ServiceCollection()
            .AddSingleton<IGlobalProductIdentityWorkflowTenantPartitionDiscovery>(new StuckOnly())
            .AddScoped<ITenantContext, Tenant>()
            .AddSingleton(counts)
            .AddSingleton<IProductIdentityWorkflowTransportReadiness>(new Readiness(readinessFault))
            .BuildServiceProvider();
    }

    private sealed class StuckOnly : IGlobalProductIdentityWorkflowTenantPartitionDiscovery
    {
        public Task<GlobalProductIdentityWorkflowTenantPartitionPage> DiscoverAsync(Guid? afterTenantId,
            long nowUtcTicks, int limit, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("No operation may be looked at.");

        public Task<GlobalProductIdentityWorkflowTenantPartitionPage> DiscoverStuckAsync(Guid? afterTenantId,
            int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult(new GlobalProductIdentityWorkflowTenantPartitionPage(
                afterTenantId is null ? [StuckTenant] : [], null));
    }

    public class StuckCounts : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
        {
            nameof(IGlobalProductIdentityWorkflowOperationRepository.CountManualReconciliationAsync) => Task.FromResult(1L),
            nameof(IGlobalProductIdentityWorkflowOperationRepository.CountNeedingAPersonAsync) => Task.FromResult(0L),
            _ => throw new InvalidOperationException($"No operation may be touched ({targetMethod?.Name}).")
        };
    }

    private sealed class Tenant : ITenantContext
    {
        public Guid TenantId { get; private set; }
        public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }

    private sealed class Readiness(string? fault) : IProductIdentityWorkflowTransportReadiness
    {
        public string? FindConfigurationFault() => fault;
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
