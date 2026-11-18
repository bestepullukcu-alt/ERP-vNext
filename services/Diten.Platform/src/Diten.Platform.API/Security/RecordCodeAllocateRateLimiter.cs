using System.Threading.RateLimiting;
using Prometheus;

namespace Diten.Platform.API.Security;

/// <summary>
/// MOD-0040-FU02 §8.2 mitigation 3 — the allocate endpoint's own rate limiter, asked INSIDE the executor after the
/// credential and the token are verified. Fixed window per partition, queue 0. Two kinds of partition never share a
/// bucket: a tenant's allocations (<c>{consumer}:{tenant}</c>) and platform-actor refusals
/// (<c>platform-actor:{consumer}:{tenant | platform}</c>) — a flood of refused platform tokens can never push a tenant's
/// Create into 429. ASP.NET's <c>AddRateLimiter</c> / <c>OnRejected</c> (PeopleSearchRateLimit) is not used: this
/// endpoint answers its own 429 in its own envelope.
///
/// <para>What the limiter stops is NOT written to the audit trail (the one named departure from AUD-001 §3, K-10 / K-13):
/// a warning log and <see cref="RateLimitedTotal"/> stand in for it.</para>
/// </summary>
public sealed class RecordCodeAllocateRateLimiter : IDisposable
{
    public const string PermitsPerMinuteKey = "RecordCodes:RateLimit:PermitsPerMinute";

    /// <summary>CT proposal for A-16 (600 requests / minute / partition) until the owner decides.</summary>
    public const int DefaultPermitsPerMinute = 600;

    public const string PlatformActorPartitionPrefix = "platform-actor:";
    public const string PlatformTenantLabel = "platform";
    public const string RateLimitedReason = "rate_limited";
    public const string PlatformActorRefusalReason = "platform_actor_refusal";

    /// <summary>One label set for both reasons (prometheus-net requires one label-name set per counter).</summary>
    public static readonly Counter RateLimitedTotal = Metrics.CreateCounter(
        "record_codes_rate_limited_total",
        "Record-code allocate requests stopped by the endpoint's rate limiter (not written to the audit trail).",
        new CounterConfiguration { LabelNames = new[] { "consumer_service", "tenant", "reason" } });

    private readonly PartitionedRateLimiter<string> _limiter;

    public RecordCodeAllocateRateLimiter(IConfiguration configuration)
        : this(configuration?.GetValue<int?>(PermitsPerMinuteKey) ?? DefaultPermitsPerMinute)
    {
    }

    public RecordCodeAllocateRateLimiter(int permitsPerMinute)
    {
        if (permitsPerMinute < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(permitsPerMinute), "The record-code rate limit is at least one request a minute.");
        }

        _limiter = PartitionedRateLimiter.Create<string, string>(partition => RateLimitPartition.GetFixedWindowLimiter(
            partition,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    }

    public bool TryAcquire(string partition)
    {
        using var lease = _limiter.AttemptAcquire(partition);
        return lease.IsAcquired;
    }

    public static string TenantPartition(string consumerService, Guid tenantId) => $"{consumerService}:{tenantId:D}";

    public static string PlatformActorPartition(string consumerService, string? tenantClaim) =>
        $"{PlatformActorPartitionPrefix}{consumerService}:{PlatformActorTenantLabel(tenantClaim)}";

    /// <summary>The token's <c>tenant_id</c> when it is one non-empty Guid; otherwise <c>platform</c>.</summary>
    public static string PlatformActorTenantLabel(string? tenantClaim) =>
        PlatformActorTargetTenant(tenantClaim)?.ToString("D") ?? PlatformTenantLabel;

    /// <summary>The platform-actor refusal's target tenant: the token's one non-empty Guid <c>tenant_id</c>, else null.</summary>
    public static Guid? PlatformActorTargetTenant(string? tenantClaim) =>
        Guid.TryParse(tenantClaim, out var tenant) && tenant != Guid.Empty ? tenant : null;

    public void Dispose() => _limiter.Dispose();
}
