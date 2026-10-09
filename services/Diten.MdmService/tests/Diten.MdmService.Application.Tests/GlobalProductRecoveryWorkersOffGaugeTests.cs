using System.Reflection;
using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Api.Services.ProductItemSkuMaster;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// BL-552 flow 3 FIX3 item 5 — the correction and retirement hosted workers, switched off, still record their stuck
/// gauges (counts only: no request is discovered for recovery, claimed or written), and they refuse a bad setting at
/// start even when off. Measured at the hosted-worker level: the real BackgroundService, the real runner, a store that
/// answers one stuck tenant.
/// </summary>
public sealed class GlobalProductRecoveryWorkersOffGaugeTests
{
    private static readonly Guid StuckTenant = Guid.Parse("7f000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task The_correction_worker_switched_off_records_its_stuck_gauges_and_recovers_nothing()
    {
        var store = StuckStore<IGlobalProductCorrectionOperationRepository>.Create();
        using var metrics = new ProductIdentityRecoveryMetrics();
        await using var provider = Services(store);
        var worker = new GlobalProductCorrectionRecoveryWorker(
            new GlobalProductCorrectionRecoveryRunner(provider.GetRequiredService<IServiceScopeFactory>(),
                Options.Create(new GlobalProductCorrectionWorkflowWorkerOptions()), TimeProvider.System, null, metrics),
            Options.Create(new GlobalProductCorrectionWorkflowWorkerOptions()),
            NullLogger<GlobalProductCorrectionRecoveryWorker>.Instance);

        await RunUntilRecordedAsync(worker, () => metrics.NeedingAPerson(GlobalProductCorrectionRecoveryRunner.Flow));

        Assert.Equal(3, metrics.Current(GlobalProductCorrectionRecoveryRunner.Flow));
        Assert.Equal(2, metrics.NeedingAPerson(GlobalProductCorrectionRecoveryRunner.Flow));
        AssertCountsOnly(StuckStore<IGlobalProductCorrectionOperationRepository>.Of(store));
    }

    [Fact]
    public async Task The_retirement_worker_switched_off_records_its_stuck_gauges_and_recovers_nothing()
    {
        var store = StuckStore<IGlobalProductRetirementRequestOperationRepository>.Create();
        using var metrics = new ProductIdentityRecoveryMetrics();
        await using var provider = Services(store);
        var worker = new GlobalProductRetirementRequestRecoveryWorker(
            new GlobalProductRetirementRequestRecoveryRunner(provider.GetRequiredService<IServiceScopeFactory>(),
                Options.Create(new GlobalProductRetirementRequestWorkflowWorkerOptions()), TimeProvider.System, null,
                metrics),
            Options.Create(new GlobalProductRetirementRequestWorkflowWorkerOptions()),
            NullLogger<GlobalProductRetirementRequestRecoveryWorker>.Instance);

        await RunUntilRecordedAsync(worker,
            () => metrics.NeedingAPerson(GlobalProductRetirementRequestRecoveryRunner.Flow));

        Assert.Equal(3, metrics.Current(GlobalProductRetirementRequestRecoveryRunner.Flow));
        Assert.Equal(2, metrics.NeedingAPerson(GlobalProductRetirementRequestRecoveryRunner.Flow));
        AssertCountsOnly(StuckStore<IGlobalProductRetirementRequestOperationRepository>.Of(store));
    }

    [Theory]
    [InlineData(14)]
    [InlineData(3601)]
    public async Task A_gauge_interval_out_of_range_stops_both_workers_at_start_even_when_off(int seconds)
    {
        await using var provider = new ServiceCollection().BuildServiceProvider();
        var scopes = provider.GetRequiredService<IServiceScopeFactory>();
        var correction = Options.Create(new GlobalProductCorrectionWorkflowWorkerOptions { StuckGaugeIntervalSeconds = seconds });
        var retirement = Options.Create(new GlobalProductRetirementRequestWorkflowWorkerOptions
            { StuckGaugeIntervalSeconds = seconds });

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new GlobalProductCorrectionRecoveryWorker(new(scopes, correction, TimeProvider.System), correction,
                NullLogger<GlobalProductCorrectionRecoveryWorker>.Instance).StartAsync(default));
        Assert.Equal("GLOBAL_PRODUCT_CORRECTION_WORKER_CONFIGURATION_INVALID", refused.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new GlobalProductRetirementRequestRecoveryWorker(new(scopes, retirement, TimeProvider.System), retirement,
                NullLogger<GlobalProductRetirementRequestRecoveryWorker>.Instance).StartAsync(default));
    }

    // Flow 3 FIX3 EK (SETTLE-CAP FIX1: 600 s cap): the settle window may not outlive Platform's sweep of half-started
    // workflows — 601 s stops both workers at start, switched off or not; 600 s is accepted.
    [Fact]
    public async Task A_settle_window_past_the_cap_stops_both_workers_at_start_and_the_cap_is_accepted()
    {
        await using var provider = new ServiceCollection().BuildServiceProvider();
        var scopes = provider.GetRequiredService<IServiceScopeFactory>();
        var correction = Options.Create(new GlobalProductCorrectionWorkflowWorkerOptions { StartSettleSeconds = 601 });
        var retirement = Options.Create(new GlobalProductRetirementRequestWorkflowWorkerOptions { StartSettleSeconds = 601 });

        Assert.Equal("GLOBAL_PRODUCT_CORRECTION_WORKER_CONFIGURATION_INVALID",
            (await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new GlobalProductCorrectionRecoveryWorker(new(scopes, correction, TimeProvider.System), correction,
                    NullLogger<GlobalProductCorrectionRecoveryWorker>.Instance).StartAsync(default))).Message);
        Assert.Equal("GLOBAL_PRODUCT_RETIREMENT_WORKER_CONFIGURATION_INVALID",
            (await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new GlobalProductRetirementRequestRecoveryWorker(new(scopes, retirement, TimeProvider.System), retirement,
                    NullLogger<GlobalProductRetirementRequestRecoveryWorker>.Instance).StartAsync(default))).Message);

        Assert.True(new GlobalProductCorrectionWorkflowWorkerOptions { StartSettleSeconds = 600 }.IsValid());
        Assert.True(new GlobalProductRetirementRequestWorkflowWorkerOptions { StartSettleSeconds = 600 }.IsValid());
    }

    private static async Task RunUntilRecordedAsync(BackgroundService worker, Func<long?> recorded)
    {
        await worker.StartAsync(default);
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (recorded() is null && DateTime.UtcNow < deadline) await Task.Delay(20);
        }
        finally
        {
            await worker.StopAsync(default);
        }
    }

    private static void AssertCountsOnly(StuckStoreState state)
    {
        Assert.Contains("DiscoverStuckTenantPartitionsAsync", state.Calls);
        Assert.Contains("CountManualReconciliationAsync", state.Calls);
        Assert.Contains("CountNeedingAPersonAsync", state.Calls);
        Assert.Empty(state.Calls.Except(
            ["DiscoverStuckTenantPartitionsAsync", "CountManualReconciliationAsync", "CountNeedingAPersonAsync"]));
        Assert.All(state.CountedFor, tenant => Assert.Equal(StuckTenant, tenant));
    }

    private static ServiceProvider Services<TStore>(TStore store) where TStore : class =>
        new ServiceCollection()
            .AddScoped<ITenantContext, Tenant>()
            .AddScoped(provider => StuckStore<TStore>.ForScope(store, provider.GetRequiredService<ITenantContext>()))
            .BuildServiceProvider();

    public sealed class StuckStoreState
    {
        public List<string> Calls { get; } = [];
        public List<Guid> CountedFor { get; } = [];
    }

    // One stuck tenant: 3 in manual reconciliation, 2 of them needing a person. Anything else the runner calls is
    // recorded and fails the "counts only" check.
    public class StuckStore<TStore> : DispatchProxy where TStore : class
    {
        private StuckStoreState _state = null!;
        private ITenantContext? _tenant;

        public static TStore Create()
        {
            var store = DispatchProxy.Create<TStore, StuckStore<TStore>>();
            ((StuckStore<TStore>)(object)store)._state = new StuckStoreState();
            return store;
        }

        public static StuckStoreState Of(TStore store) => ((StuckStore<TStore>)(object)store)._state;

        public static TStore ForScope(TStore shared, ITenantContext tenant)
        {
            var scoped = DispatchProxy.Create<TStore, StuckStore<TStore>>();
            var proxy = (StuckStore<TStore>)(object)scoped;
            proxy._state = Of(shared);
            proxy._tenant = tenant;
            return scoped;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var name = targetMethod!.Name;
            lock (_state) _state.Calls.Add(name);
            switch (name)
            {
                case "DiscoverStuckTenantPartitionsAsync":
                    return Task.FromResult(new GlobalProductIdentityWorkflowTenantPartitionPage(
                        args![0] is null ? [StuckTenant] : [], null));
                case "CountManualReconciliationAsync":
                    lock (_state) _state.CountedFor.Add(_tenant!.TenantId);
                    return Task.FromResult(3L);
                case "CountNeedingAPersonAsync":
                    return Task.FromResult(2L);
                default:
                    throw new InvalidOperationException($"The switched-off worker called {name}.");
            }
        }
    }

    private sealed class Tenant : ITenantContext
    {
        public Guid TenantId { get; private set; }
        public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }
}
