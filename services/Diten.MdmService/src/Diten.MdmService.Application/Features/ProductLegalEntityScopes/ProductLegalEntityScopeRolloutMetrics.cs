using System.Diagnostics.Metrics;

namespace Diten.MdmService.Application.Features.ProductLegalEntityScopes;

/// <summary>
/// WP-MDM-SCOPE-ACCEPT-01 FIX1 — principle B: no stuck state without a named exit AND a gauge that counts it. Every way
/// the scope rollout can hold a tenant is counted here (standard .NET meter, readable with dotnet-counters or any
/// OpenTelemetry exporter, like the product-identity recovery gauges):
/// <list type="bullet">
/// <item><see cref="RolloutUnreadable"/> — a rollout record that cannot be read; reads and writes answer 503.</item>
/// <item><see cref="LeaseRetained"/> — a writer lease kept after an ambiguous (202), failed-and-unverifiable or
/// exceptional write. Exit: the same command repeated, or the lease's expiry (then taken over).</item>
/// <item><see cref="LeaseTakenOver"/> — an expired lease taken over by another writer or by a fence.</item>
/// <item><see cref="FenceAborted"/> — a fence abandoned by its own command (readiness / inventory refused, or abort).</item>
/// <item>FIX2 (3) — <see cref="LeaseSwept"/> (expired leases nobody met, removed by an acquisition), <see cref="LeaseCleared"/>
/// (expired leases a fence removed), <see cref="LeaseRenewed"/> (a repeat whose own lease had run out took a fresh one),
/// <see cref="LeaseContended"/> (an acquisition that gave up after its optimistic rounds), <see cref="BaselineUnverifiable"/>
/// (a write admitted whose objects cannot be hashed: its lease is kept unless it succeeds).</item>
/// <item>FIX2 (3) — the gauges, the INSTANT values a counter cannot give: <see cref="FenceAgeMinutes"/> (the oldest fence
/// still up, in minutes; a fence has no expiry, so this is how a forgotten one shows), <see cref="FencesUp"/> and
/// <see cref="LeasesHeld"/> (leases held now, running or expired-not-yet-taken). Fed by
/// <see cref="RecordHolds"/> from a read-only sweep of the rollout records.</item>
/// </list>
/// Counts only: no tenant, product or person in a tag.
/// </summary>
public static class ProductLegalEntityScopeRolloutMetrics
{
    public const string MeterName = "Diten.MdmService.ProductLegalEntityScopeRollout";
    public const string RolloutUnreadable = "mdm.product_scope.rollout_unreadable";
    public const string LeaseRetained = "mdm.product_scope.writer_lease_retained";
    public const string LeaseTakenOver = "mdm.product_scope.writer_lease_taken_over";
    public const string FenceAborted = "mdm.product_scope.fence_aborted";
    public const string LeaseSwept = "mdm.product_scope.writer_lease_swept";
    public const string LeaseCleared = "mdm.product_scope.writer_lease_cleared_by_fence";
    public const string LeaseRenewed = "mdm.product_scope.writer_lease_renewed";
    public const string LeaseContended = "mdm.product_scope.writer_lease_contended";
    public const string BaselineUnverifiable = "mdm.product_scope.writer_baseline_unverifiable";
    public const string FenceAgeMinutes = "mdm.product_scope.fence_age_minutes";
    public const string FencesUp = "mdm.product_scope.fences_up";
    public const string LeasesHeld = "mdm.product_scope.writer_leases_held";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Unreadable = Meter.CreateCounter<long>(RolloutUnreadable, "{request}",
        "Requests answered 503 because the tenant's scope rollout record cannot be read.");
    private static readonly Counter<long> Retained = Meter.CreateCounter<long>(LeaseRetained, "{lease}",
        "Writer leases kept after an ambiguous, unverifiable or exceptional write (exit: same command, or expiry).");
    private static readonly Counter<long> TakenOver = Meter.CreateCounter<long>(LeaseTakenOver, "{lease}",
        "Expired writer leases taken over by another writer or by a fence.");
    private static readonly Counter<long> Aborted = Meter.CreateCounter<long>(FenceAborted, "{fence}",
        "Activation / suspension fences abandoned by their own command.");

    private static readonly Counter<long> Swept = Meter.CreateCounter<long>(LeaseSwept, "{lease}",
        "Expired writer leases that met no writer, removed by another acquisition.");
    private static readonly Counter<long> Cleared = Meter.CreateCounter<long>(LeaseCleared, "{lease}",
        "Expired writer leases removed when an activation / suspension fence was taken.");
    private static readonly Counter<long> Renewed = Meter.CreateCounter<long>(LeaseRenewed, "{lease}",
        "Repeats of a command whose own lease had run out, given a fresh lease.");
    private static readonly Counter<long> Contended = Meter.CreateCounter<long>(LeaseContended, "{request}",
        "Lease acquisitions that gave up after every optimistic round (answered PRODUCT_SCOPE_WRITER_LEASE_CONTENDED).");
    private static readonly Counter<long> Unverifiable = Meter.CreateCounter<long>(BaselineUnverifiable, "{request}",
        "Writes admitted whose objects cannot be hashed; their lease is kept unless the write succeeds.");

    private static HoldSnapshot _holds = HoldSnapshot.Empty;
    private static DateTimeOffset _holdsObservedAt = DateTimeOffset.MinValue;

    private static readonly ObservableGauge<double> FenceAge = Meter.CreateObservableGauge(FenceAgeMinutes,
        () => CurrentFenceAgeMinutes(), "min",
        "Age of the oldest activation / suspension fence still up (0 when none). A fence has no expiry: a growing value is a forgotten fence.");
    private static readonly ObservableGauge<long> Fences = Meter.CreateObservableGauge(FencesUp,
        () => Volatile.Read(ref _holds).FencesUp, "{fence}", "Activation / suspension fences up now.");
    private static readonly ObservableGauge<long> Held = Meter.CreateObservableGauge(LeasesHeld,
        () => new[]
        {
            new Measurement<long>(Volatile.Read(ref _holds).RunningLeases, new KeyValuePair<string, object?>("state", "running")),
            new Measurement<long>(Volatile.Read(ref _holds).ExpiredLeases, new KeyValuePair<string, object?>("state", "expired"))
        }, "{lease}", "Writer leases held now (running, or expired and not yet taken over / swept).");

    /// <summary>The holds of every tenant, as last read (counts only — no tenant, product or person).</summary>
    public sealed record HoldSnapshot(DateTimeOffset? OldestFenceAcquiredAtUtc, long FencesUp, long RunningLeases, long ExpiredLeases)
    {
        public static HoldSnapshot Empty { get; } = new(null, 0, 0, 0);
    }

    public static void RecordHolds(HoldSnapshot snapshot, DateTimeOffset observedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Volatile.Write(ref _holds, snapshot);
        _holdsObservedAt = observedAtUtc;
    }

    /// <summary>Minutes between the last observation and the oldest fence it saw (0 when none was up).</summary>
    public static double CurrentFenceAgeMinutes()
    {
        var holds = Volatile.Read(ref _holds);
        return holds.OldestFenceAcquiredAtUtc is { } oldest && _holdsObservedAt > oldest
            ? Math.Floor((_holdsObservedAt - oldest).TotalMinutes)
            : 0;
    }

    public static void CountSwept(int leases) { if (leases > 0) Swept.Add(leases); }
    public static void CountCleared(int leases) { if (leases > 0) Cleared.Add(leases); }
    public static void CountRenewed(string by) => Renewed.Add(1, new KeyValuePair<string, object?>("by", by));
    public static void CountContended(string by) => Contended.Add(1, new KeyValuePair<string, object?>("by", by));
    public static void CountBaselineUnverifiable() => Unverifiable.Add(1);

    public static void CountUnreadable(string surface) => Unreadable.Add(1, new KeyValuePair<string, object?>("surface", surface));
    public static void CountRetained(string reason) => Retained.Add(1, new KeyValuePair<string, object?>("reason", reason));
    public static void CountTakenOver(int leases, string by)
    {
        if (leases > 0) TakenOver.Add(leases, new KeyValuePair<string, object?>("by", by));
    }
    public static void CountAborted(string cause) => Aborted.Add(1, new KeyValuePair<string, object?>("cause", cause));
}
