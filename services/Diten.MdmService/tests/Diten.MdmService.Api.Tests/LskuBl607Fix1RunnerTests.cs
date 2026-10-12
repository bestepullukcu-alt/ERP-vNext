using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Api.Services.ProductItemSkuMaster;
using Diten.MdmService.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// BL-607 FIX1 (WP-MDM-BL607-FIX1) — the recovery runner's open-approval gauge, with the real runner and a discovery that
/// answers what the test needs (no store, no tenant):
/// <list type="bullet">
/// <item>item 1 — a read that THROWS does not take the cycle down: the cycle is still counted and logged, the gauge is
/// marked failed and its age dropped; a start that is not an instant is ignored by name, never thrown on;</item>
/// <item>item 3 — the alert is said when the threshold is crossed, then at most once an hour while it stays crossed, and
/// at once again after it cleared;</item>
/// <item>R1 — when the last open approval is decided, the age gauge drops.</item>
/// </list>
/// </summary>
public sealed class LskuBl607Fix1RunnerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_gauge_read_that_throws_still_counts_and_logs_the_cycle_and_marks_the_gauge_failed()
    {
        var discovery = new Discovery { Manual = 3, Person = 2, Oldest = () => throw new TimeoutException("store unavailable") };
        var (runner, metrics, log) = Runner(discovery);
        metrics.RecordOldestOpenApproval(LskuIdentityWorkflowRecoveryRunner.Flow, TimeSpan.FromDays(2), TimeSpan.FromDays(1)); // a stale age

        var result = await runner.RunCycleAsync();

        Assert.NotNull(result.Cycle);
        Assert.Equal(3, result.Cycle!.InManualReconciliation);
        Assert.Equal(2, metrics.NeedingAPerson(LskuIdentityWorkflowRecoveryRunner.Flow));         // metrics.Record ran
        Assert.Contains(log.Lines, line => line.Message.Contains("recovery cycle:", StringComparison.Ordinal)
                                           && line.Message.Contains("in manual reconciliation 3", StringComparison.Ordinal)); // cycle.Log ran
        Assert.Equal(1, metrics.OpenApprovalReadFailed(LskuIdentityWorkflowRecoveryRunner.Flow));
        Assert.Null(metrics.OldestOpenApprovalSeconds(LskuIdentityWorkflowRecoveryRunner.Flow));   // dropped, not stale
        Assert.Contains(log.Lines, line => line.Level == LogLevel.Error && line.Message.Contains("TIMEOUTEXCEPTION", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_cancelled_cycle_is_not_swallowed_by_the_gauge()
    {
        using var cancel = new CancellationTokenSource();
        var discovery = new Discovery { Oldest = () => { cancel.Cancel(); throw new OperationCanceledException(cancel.Token); } };
        var (runner, _, _) = Runner(discovery);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.RunCycleAsync(cancel.Token));
    }

    [Theory]
    [InlineData(-5L)]
    [InlineData(long.MaxValue)]
    public async Task A_start_that_is_not_an_instant_is_ignored_by_name_and_the_cycle_goes_on(long ticks)
    {
        var discovery = new Discovery { Manual = 1, Oldest = () => ticks };
        var (runner, metrics, log) = Runner(discovery);

        var result = await runner.RunCycleAsync();

        Assert.Equal(1, result.Cycle!.InManualReconciliation);
        Assert.Null(metrics.OldestOpenApprovalSeconds(LskuIdentityWorkflowRecoveryRunner.Flow));
        Assert.Equal(0, metrics.OpenApprovalReadFailed(LskuIdentityWorkflowRecoveryRunner.Flow));
        Assert.Contains(log.Lines, line => line.Level == LogLevel.Error
                                           && line.Message.Contains(LskuIdentityWorkflowRecoveryRunner.OpenApprovalStartInvalid, StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_alert_is_said_when_crossed_then_at_most_hourly_and_again_at_once_after_it_cleared()
    {
        var discovery = new Discovery { Oldest = () => Now.AddDays(-2).UtcTicks };
        var (runner, _, log) = Runner(discovery);
        int Alerts() => log.Lines.Count(line => line.Level == LogLevel.Warning && line.Message.Contains("past the alert threshold", StringComparison.Ordinal));

        for (var cycle = 0; cycle < 240; cycle++)                // an hour of 15-second cycles, less one
        {
            await runner.RunCycleAsync();
            log.Time.Advance(TimeSpan.FromSeconds(15));
        }
        Assert.Equal(1, Alerts());                                // said once, not 240 times

        log.Time.Advance(TimeSpan.FromSeconds(15));               // one hour since it was said
        await runner.RunCycleAsync();
        Assert.Equal(2, Alerts());

        discovery.Oldest = () => null;                            // decided: nothing past the threshold
        await runner.RunCycleAsync();
        discovery.Oldest = () => log.Time.GetUtcNow().AddDays(-2).UtcTicks;
        await runner.RunCycleAsync();                             // a new crossing is said at once
        Assert.Equal(3, Alerts());
    }

    [Fact]
    public async Task When_the_last_open_approval_is_decided_the_age_gauge_drops()
    {
        var discovery = new Discovery { Oldest = () => Now.AddHours(-3).UtcTicks };
        var (runner, metrics, _) = Runner(discovery);

        await runner.RunCycleAsync();
        Assert.NotNull(metrics.OldestOpenApprovalSeconds(LskuIdentityWorkflowRecoveryRunner.Flow));

        discovery.Oldest = () => null;
        await runner.RunCycleAsync();
        Assert.Null(metrics.OldestOpenApprovalSeconds(LskuIdentityWorkflowRecoveryRunner.Flow));
    }

    private static (LskuIdentityWorkflowRecoveryRunner Runner, ProductIdentityRecoveryMetrics Metrics, CapturingLog Log) Runner(Discovery discovery)
    {
        var services = new ServiceCollection()
            .AddScoped<ILskuIdentityWorkflowTenantPartitionDiscovery>(_ => discovery)
            .BuildServiceProvider();
        var log = new CapturingLog(new MovableTime(Now));
        var metrics = new ProductIdentityRecoveryMetrics();
        var runner = new LskuIdentityWorkflowRecoveryRunner(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new LskuIdentityWorkflowWorkerOptions { Enabled = true, LeaseOwner = "bl607-fix1-test" }),
            log.Time, log, metrics);
        return (runner, metrics, log);
    }

    private sealed class Discovery : ILskuIdentityWorkflowTenantPartitionDiscovery
    {
        public long Manual { get; init; }
        public long Person { get; init; }
        public Func<long?> Oldest { get; set; } = () => null;

        public Task<LskuIdentityWorkflowTenantPartitionPage> DiscoverAsync(Guid? afterTenantId, long nowUtcTicks, int limit,
            CancellationToken cancellationToken = default) => Task.FromResult(new LskuIdentityWorkflowTenantPartitionPage([], null));

        public Task<long> CountManualReconciliationAsync(CancellationToken cancellationToken = default) => Task.FromResult(Manual);
        public Task<long> CountNeedingAPersonAsync(CancellationToken cancellationToken = default) => Task.FromResult(Person);
        public Task<long?> OldestOpenApprovalStartedAtUtcTicksAsync(CancellationToken cancellationToken = default) => Task.FromResult(Oldest());
    }

    private sealed class MovableTime(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    private sealed class CapturingLog(MovableTime time) : ILogger<LskuIdentityWorkflowRecoveryRunner>
    {
        public MovableTime Time { get; } = time;
        public List<(LogLevel Level, string Message)> Lines { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (Lines) Lines.Add((logLevel, formatter(state, exception)));
        }
    }
}
