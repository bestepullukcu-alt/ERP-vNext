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

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Unreadable = Meter.CreateCounter<long>(RolloutUnreadable, "{request}",
        "Requests answered 503 because the tenant's scope rollout record cannot be read.");
    private static readonly Counter<long> Retained = Meter.CreateCounter<long>(LeaseRetained, "{lease}",
        "Writer leases kept after an ambiguous, unverifiable or exceptional write (exit: same command, or expiry).");
    private static readonly Counter<long> TakenOver = Meter.CreateCounter<long>(LeaseTakenOver, "{lease}",
        "Expired writer leases taken over by another writer or by a fence.");
    private static readonly Counter<long> Aborted = Meter.CreateCounter<long>(FenceAborted, "{fence}",
        "Activation / suspension fences abandoned by their own command.");

    public static void CountUnreadable(string surface) => Unreadable.Add(1, new KeyValuePair<string, object?>("surface", surface));
    public static void CountRetained(string reason) => Retained.Add(1, new KeyValuePair<string, object?>("reason", reason));
    public static void CountTakenOver(int leases, string by)
    {
        if (leases > 0) TakenOver.Add(leases, new KeyValuePair<string, object?>("by", by));
    }
    public static void CountAborted(string cause) => Aborted.Add(1, new KeyValuePair<string, object?>("cause", cause));
}
