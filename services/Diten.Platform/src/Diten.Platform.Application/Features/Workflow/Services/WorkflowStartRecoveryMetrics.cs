using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace Diten.Platform.Application.Features.Workflow.Services;

/// <summary>
/// BL-549 + BL-539 — WHAT THE RECOVERY SEES, as gauges (the ProductIdentityRecoveryMetrics pattern of MDM: a
/// <see cref="Meter"/> with observable gauges, one value per tenant, refreshed by each cycle). prometheus-net publishes .NET
/// meters, so the gauges reach the same scrape as the rest of Platform's metrics.
/// <list type="bullet">
/// <item><c>platform.workflow.start_recovery.abandoned</c> — generic starts past the age with no task, as the cycle found
/// them (before it closed any).</item>
/// <item><c>platform.workflow.start_recovery.stuck</c> — of those, the ones the cycle could NOT close.</item>
/// <item><c>platform.workflow.start_recovery.incomplete_trusted</c> — trusted starts past the age that never completed.</item>
/// </list>
/// </summary>
public sealed class WorkflowStartRecoveryMetrics : IDisposable
{
    public const string MeterName = "Diten.Platform.WorkflowStartRecovery";

    private readonly Meter _meter = new(MeterName);
    private readonly ConcurrentDictionary<Guid, (long Abandoned, long Stuck, long IncompleteTrusted)> _byTenant = new();

    public WorkflowStartRecoveryMetrics()
    {
        _meter.CreateObservableGauge("platform.workflow.start_recovery.abandoned", () => Read(x => x.Abandoned));
        _meter.CreateObservableGauge("platform.workflow.start_recovery.stuck", () => Read(x => x.Stuck));
        _meter.CreateObservableGauge("platform.workflow.start_recovery.incomplete_trusted", () => Read(x => x.IncompleteTrusted));
    }

    public void Record(Guid tenantId, long abandoned, long stuck, long incompleteTrusted) =>
        _byTenant[tenantId] = (abandoned, stuck, incompleteTrusted);

    /// <summary>The last recorded values of one tenant (zeros when the recovery has not looked at it).</summary>
    public (long Abandoned, long Stuck, long IncompleteTrusted) For(Guid tenantId) =>
        _byTenant.TryGetValue(tenantId, out var values) ? values : (0, 0, 0);

    private readonly ConcurrentDictionary<Guid, byte> _reportedStuck = new();

    /// <summary>True the FIRST time <paramref name="instanceId"/> is reported stuck (the error line is written once; later
    /// cycles say "still stuck" quietly). Bounded: past 10 000 remembered instances every one is reported again.</summary>
    public bool FirstStuckReport(Guid instanceId)
    {
        if (_reportedStuck.Count >= 10_000)
        {
            _reportedStuck.Clear();
        }

        return _reportedStuck.TryAdd(instanceId, 0);
    }

    public void Unstuck(Guid instanceId) => _reportedStuck.TryRemove(instanceId, out _);

    public void Dispose() => _meter.Dispose();

    private IEnumerable<Measurement<long>> Read(Func<(long Abandoned, long Stuck, long IncompleteTrusted), long> pick) =>
        _byTenant.Select(entry => new Measurement<long>(pick(entry.Value), new KeyValuePair<string, object?>("tenant", entry.Key.ToString("D"))));
}
