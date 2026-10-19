using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// WP-MDM-SCOPE-ACCEPT-01 FIX2 (3) — listens to the scope rollout meter the way an exporter would, so a test can say
/// "this path was counted" or "this gauge shows that". The meter is process-wide: a test reads the change of a tagged
/// counter (scope tests that produce the same tag run in one collection, never side by side).
/// </summary>
internal sealed class ProductLegalEntityScopeMetricRecorder : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly ConcurrentQueue<(string Name, double Value, KeyValuePair<string, object?>[] Tags)> _measurements = new();

    public ProductLegalEntityScopeMetricRecorder()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == ProductLegalEntityScopeRolloutMetrics.MeterName)
                listener.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            _measurements.Enqueue((instrument.Name, value, tags.ToArray())));
        _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            _measurements.Enqueue((instrument.Name, value, tags.ToArray())));
        _listener.Start();
    }

    public double Sum(string name, string? tag = null, string? value = null) => _measurements
        .Where(item => item.Name == name && (tag is null || item.Tags.Any(pair =>
            pair.Key == tag && string.Equals(pair.Value?.ToString(), value, StringComparison.Ordinal))))
        .Sum(item => item.Value);

    /// <summary>Reads every gauge now and returns the last value of <paramref name="name"/> (with the tag, if given).</summary>
    public double Observe(string name, string? tag = null, string? value = null)
    {
        while (_measurements.TryDequeue(out _)) { }
        _listener.RecordObservableInstruments();
        return Sum(name, tag, value);
    }

    public void Dispose() => _listener.Dispose();
}
