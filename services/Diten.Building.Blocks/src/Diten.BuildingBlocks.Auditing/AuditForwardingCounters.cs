using System.Diagnostics.Metrics;

namespace Diten.BuildingBlocks.Auditing;

/// <summary>
/// AUD-001 K8 — a record that is not written is COUNTED, not only logged: the process-wide counts (read by tests and a
/// health page) and the <c>Diten.Audit.Forwarding</c> meter (<c>diten.audit.forward.sent / skipped / failed</c>) for the
/// metrics pipeline. Registered as a singleton per service.
/// </summary>
public sealed class AuditForwardingCounters
{
    public const string MeterName = "Diten.Audit.Forwarding";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> SentCounter = Meter.CreateCounter<long>("diten.audit.forward.sent");
    private static readonly Counter<long> SkippedCounter = Meter.CreateCounter<long>("diten.audit.forward.skipped");
    private static readonly Counter<long> FailedCounter = Meter.CreateCounter<long>("diten.audit.forward.failed");

    private long _sent;
    private long _skipped;
    private long _failed;

    public long Sent => Interlocked.Read(ref _sent);
    public long Skipped => Interlocked.Read(ref _skipped);
    public long Failed => Interlocked.Read(ref _failed);

    public void RecordSent()
    {
        Interlocked.Increment(ref _sent);
        SentCounter.Add(1);
    }

    public void RecordSkipped(string reason)
    {
        Interlocked.Increment(ref _skipped);
        SkippedCounter.Add(1, new KeyValuePair<string, object?>("reason", reason));
    }

    public void RecordFailed()
    {
        Interlocked.Increment(ref _failed);
        FailedCounter.Add(1);
    }
}
