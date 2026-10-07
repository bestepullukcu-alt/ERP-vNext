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
/// <item><c>platform.workflow.start_recovery.count_capped</c> — 1 when the tenant's count stopped at its scan limit: the two
/// counts above are then "at least" (FIX2 item 4).</item>
/// </list>
/// FIX2 K5 — a tenant's values expire: a cycle drops the tenants it no longer counts (<see cref="Retain"/>), and a value
/// older than <see cref="MaxAge"/> is not published (the counting stopped; an old number would read as current).
/// </summary>
public sealed class WorkflowStartRecoveryMetrics : IDisposable
{
    public const string MeterName = "Diten.Platform.WorkflowStartRecovery";

    /// <summary>Longer than the longest count interval (24 h), so a live tenant's value never blinks out between cycles.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(25);

    private readonly Meter _meter = new(MeterName);
    private readonly TimeProvider _clock;
    private readonly ConcurrentDictionary<Guid, Values> _byTenant = new();

    public WorkflowStartRecoveryMetrics() : this(TimeProvider.System)
    {
    }

    public WorkflowStartRecoveryMetrics(TimeProvider clock)
    {
        _clock = clock;
        _meter.CreateObservableGauge("platform.workflow.start_recovery.abandoned", () => Read(x => x.Abandoned));
        _meter.CreateObservableGauge("platform.workflow.start_recovery.stuck", () => Read(x => x.Stuck));
        _meter.CreateObservableGauge("platform.workflow.start_recovery.incomplete_trusted", () => Read(x => x.IncompleteTrusted));
        _meter.CreateObservableGauge("platform.workflow.start_recovery.count_capped", () => Read(x => x.Capped ? 1 : 0));
        // FIX3 K11 — when each tenant was last counted (Unix seconds): a stale gauge is visible as stale.
        _meter.CreateObservableGauge("platform.workflow.start_recovery.last_counted_unix_seconds", () => Read(x => x.RecordedAt.ToUnixTimeSeconds()));
    }

    public void Record(Guid tenantId, long abandoned, long stuck, long incompleteTrusted, bool capped = false) =>
        _byTenant[tenantId] = new Values(abandoned, stuck, incompleteTrusted, capped, _clock.GetUtcNow());

    /// <summary>FIX2 K5 — keep only the tenants a cycle has just counted; a tenant no longer active stops being published.</summary>
    public void Retain(IReadOnlyCollection<Guid> tenantIds)
    {
        foreach (var tenantId in _byTenant.Keys.Where(id => !tenantIds.Contains(id)).ToList())
        {
            _byTenant.TryRemove(tenantId, out _);
        }
    }

    /// <summary>The published tenants right now (expired values excluded).</summary>
    public IReadOnlyCollection<Guid> PublishedTenants() => Current().Select(entry => entry.Key).ToList();

    /// <summary>The last recorded values of one tenant (zeros when the recovery has not looked at it).</summary>
    public (long Abandoned, long Stuck, long IncompleteTrusted) For(Guid tenantId) =>
        _byTenant.TryGetValue(tenantId, out var values) ? (values.Abandoned, values.Stuck, values.IncompleteTrusted) : (0, 0, 0);

    /// <summary>FIX2 item 4 — whether the tenant's last count stopped at its scan limit.</summary>
    public bool CappedFor(Guid tenantId) => _byTenant.TryGetValue(tenantId, out var values) && values.Capped;

    private readonly ConcurrentDictionary<Guid, int> _deferred = new();

    private readonly ConcurrentDictionary<Guid, byte> _cappedWarned = new();

    /// <summary>FIX3 item 3 — true the FIRST time a tenant's close cycle is capped (the job warns once); cleared when a cycle
    /// is no longer capped, so a later cap is warned again.</summary>
    public bool FirstCappedWarning(Guid tenantId, bool capped)
    {
        if (!capped)
        {
            _cappedWarned.TryRemove(tenantId, out _);
            return false;
        }

        return _cappedWarned.TryAdd(tenantId, 0);
    }

    /// <summary>FIX3 K11 — "in a row" is in a row: after a close cycle, every instance that was NOT deferred in it starts
    /// again from zero.</summary>
    public void KeepDeferredOnly(IReadOnlyCollection<Guid> deferredThisCycle)
    {
        foreach (var instanceId in _deferred.Keys.Where(id => !deferredThisCycle.Contains(id)).ToList())
        {
            _deferred.TryRemove(instanceId, out _);
        }
    }

    /// <summary>The current streak of <paramref name="instanceId"/> (0 when none).</summary>
    public int DeferredStreak(Guid instanceId) => _deferred.TryGetValue(instanceId, out var cycles) ? cycles : 0;

    /// <summary>FIX2 K5 — one more cycle in which <paramref name="instanceId"/> could not be written; returns how many in a
    /// row. Bounded like the stuck list.</summary>
    public int NoteDeferred(Guid instanceId)
    {
        if (_deferred.Count >= 10_000)
        {
            _deferred.Clear();
        }

        return _deferred.AddOrUpdate(instanceId, 1, (_, cycles) => cycles + 1);
    }

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

    public void Unstuck(Guid instanceId)
    {
        _reportedStuck.TryRemove(instanceId, out _);
        _deferred.TryRemove(instanceId, out _);
    }

    public void Dispose() => _meter.Dispose();

    private IEnumerable<KeyValuePair<Guid, Values>> Current()
    {
        var oldest = _clock.GetUtcNow() - MaxAge;
        return _byTenant.Where(entry => entry.Value.RecordedAt >= oldest);
    }

    private IEnumerable<Measurement<long>> Read(Func<Values, long> pick) =>
        Current().Select(entry => new Measurement<long>(pick(entry.Value), new KeyValuePair<string, object?>("tenant", entry.Key.ToString("D"))));

    private sealed record Values(long Abandoned, long Stuck, long IncompleteTrusted, bool Capped, DateTimeOffset RecordedAt);
}
