using System.Diagnostics.Metrics;
using Diten.MdmService.Api.Services.ItemMaster;
using Diten.MdmService.Api.Services.ProductItemSkuMaster;
using Diten.MdmService.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-S2-LANE-MERGE-01 E3 — the oldest open item approval's age is its OWN instrument (name, unit "s", tag "flow"),
/// observable through the standard .NET meter; and a failing read of it marks only its own count_failed flow — the other
/// item gauges keep counting. No host, no Mongo: the gauge is fed by a fake discovery.
/// </summary>
public sealed class S2MergeItemGaugeTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task S2Merge_E3_The_age_is_its_own_instrument_in_seconds_tagged_with_the_flow()
    {
        var started = Now.AddDays(-2).AddSeconds(-17);
        var metrics = new ProductIdentityRecoveryMetrics();
        var (gauge, _) = Gauge(metrics, new FakeDiscovery { OldestStarted = started.UtcTicks });
        var seen = new List<(string Name, string? Unit, long Value, string? Flow)>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == ProductIdentityRecoveryMetrics.MeterName) l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            string? flow = null;
            foreach (var tag in tags) if (tag.Key == "flow") flow = tag.Value as string;
            lock (seen) seen.Add((instrument.Name, instrument.Unit, value, flow));
        });
        listener.Start();

        await gauge.RecordAsync(CancellationToken.None);
        listener.RecordObservableInstruments();

        var expected = (long)(Now - started).TotalSeconds;
        Assert.Contains(seen, m => m.Name == ProductIdentityRecoveryMetrics.OldestOpenApprovalGauge
                                   && m.Name == "mdm.product_identity.recovery.oldest_open_approval_age"
                                   && m.Unit == "s" && m.Flow == ItemActivationWorkflowRecoveryRunner.Flow && m.Value == expected);
        // Never on the stuck-operations series.
        Assert.DoesNotContain(seen, m => m.Name == ProductIdentityRecoveryMetrics.ManualReconciliationGauge && m.Value == expected);
        Assert.Equal(0, metrics.CountFailed(ItemActivationGauge.OldestOpenApprovalFlow));
    }

    [Fact]
    public async Task S2Merge_E3_A_failing_age_read_marks_only_its_own_flow_and_the_other_gauges_keep_counting()
    {
        var metrics = new ProductIdentityRecoveryMetrics();
        var (gauge, discovery) = Gauge(metrics, new FakeDiscovery { OldestStarted = Now.AddHours(-1).UtcTicks });
        await gauge.RecordAsync(CancellationToken.None);
        Assert.Equal(3600, metrics.OldestOpenApproval(ItemActivationWorkflowRecoveryRunner.Flow));

        discovery.OldestFails = true;
        await gauge.RecordAsync(CancellationToken.None);

        Assert.Equal(1, metrics.CountFailed(ItemActivationGauge.OldestOpenApprovalFlow));
        Assert.Equal(0, metrics.CountFailed(ItemActivationWorkflowRecoveryRunner.Flow));
        Assert.Equal(0, metrics.CountFailed(ItemActivationGauge.UnprocessedFlow));
        Assert.Equal(0, metrics.CountFailed(ItemActivationGauge.OrphanCodeFlow));
        Assert.Equal(4, metrics.Current(ItemActivationWorkflowRecoveryRunner.Flow));
        Assert.Equal(7, metrics.Current(ItemActivationGauge.UnprocessedFlow));
        Assert.Equal(2, metrics.Current(ItemActivationGauge.OrphanCodeFlow));

        discovery.OldestFails = false;
        await gauge.RecordAsync(CancellationToken.None);
        Assert.Equal(0, metrics.CountFailed(ItemActivationGauge.OldestOpenApprovalFlow));
    }

    private static (ItemActivationGauge Gauge, FakeDiscovery Discovery) Gauge(ProductIdentityRecoveryMetrics metrics,
        FakeDiscovery discovery)
    {
        var services = new ServiceCollection()
            .AddSingleton<IItemWorkflowTenantPartitionDiscovery>(discovery)
            .AddSingleton<IItemOrphanCodeCensus>(new FakeCensus())
            .BuildServiceProvider();
        return (new ItemActivationGauge(services.GetRequiredService<IServiceScopeFactory>(), new FixedClock(),
            NullLogger<ItemActivationGauge>.Instance, metrics), discovery);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeDiscovery : IItemWorkflowTenantPartitionDiscovery
    {
        public long? OldestStarted { get; set; }
        public bool OldestFails { get; set; }

        public Task<ItemWorkflowTenantPartitionPage> DiscoverAsync(Guid? afterTenantId, long nowUtcTicks, int limit,
            CancellationToken cancellationToken = default) => Task.FromResult(new ItemWorkflowTenantPartitionPage([], null));
        public Task<long> CountManualReconciliationAsync(long nowUtcTicks, CancellationToken cancellationToken = default) =>
            Task.FromResult(4L);
        public Task<long> CountNeedingAPersonAsync(long nowUtcTicks, CancellationToken cancellationToken = default) =>
            Task.FromResult(1L);
        public Task<long> CountUnprocessedAsync(long untouchedSinceUtcTicks, CancellationToken cancellationToken = default) =>
            Task.FromResult(7L);
        public Task<long?> OldestOpenApprovalStartedAtAsync(CancellationToken cancellationToken = default) =>
            OldestFails ? Task.FromException<long?>(new TimeoutException("synthetic")) : Task.FromResult(OldestStarted);
    }

    private sealed class FakeCensus : IItemOrphanCodeCensus
    {
        public Task<long> CountOrphanItemCodesAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default) =>
            Task.FromResult(2L);
        public Task<IReadOnlyList<Diten.MdmService.Domain.Entities.CodeReservation>> FindOrphanItemCodesAsync(Guid tenantId,
            DateTimeOffset olderThan, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Diten.MdmService.Domain.Entities.CodeReservation>>([]);
    }
}
