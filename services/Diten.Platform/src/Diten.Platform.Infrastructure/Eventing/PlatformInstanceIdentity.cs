namespace Diten.Platform.Infrastructure.Eventing;

/// <summary>
/// AG-STEP-010 / MOD-0018-FU13 Group A — process-lifetime identity for per-instance cache-invalidation fan-out.
///
/// A single GUID is generated once per process (static initializer) and used as the MassTransit endpoint
/// <c>InstanceId</c> for the <see cref="EntitlementCacheInvalidationConsumer"/> only. Combined with a temporary
/// (non-durable, auto-delete) endpoint, this gives every Platform instance its OWN receive endpoint, so a published
/// entitlement-invalidation event reaches all running instances and each evicts its local
/// <c>IMemoryCache</c> — closing the multi-instance stale-cache gap left by the default competing-consumer topology.
/// </summary>
internal static class PlatformInstanceIdentity
{
    /// <summary>
    /// Queue-safe (32 lowercase hex characters), stable for the lifetime of the process. Different processes get
    /// different values, so each instance's MassTransit endpoint name is unique.
    /// </summary>
    public static string InstanceId { get; } = Guid.NewGuid().ToString("N");
}
