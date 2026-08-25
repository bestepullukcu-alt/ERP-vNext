using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Features.EntitlementAttestations;

public sealed class PlatformEntitlementAttestationOptions
{
    public bool Enabled { get; set; }
    public TimeSpan AuthorityTimeout { get; set; } = TimeSpan.FromSeconds(5);
}

public interface IAuthoritativeEntitlementDecisionSource
{
    Task<EntitlementDecisionSnapshotV1> ReadAsync(Guid tenantId, string normalizedModuleCode, string requestHash, CancellationToken cancellationToken);
    Task<EntitlementStateVersionV1> ReadCurrentVersionAsync(Guid tenantId, string normalizedModuleCode, CancellationToken cancellationToken);
}

public sealed class VersionAwareEntitlementDecisionCache
{
    private readonly ConcurrentDictionary<CacheKey, EntitlementDecisionSnapshotV1> _entries = new();
    private readonly ConcurrentDictionary<FenceKey, EntitlementStateVersionV1> _fences = new();

    public bool TryGet(Guid tenantId, string moduleCode, string requestHash, EntitlementStateVersionV1 current, out EntitlementDecisionSnapshotV1 value)
    {
        value = null!;
        if (!current.IsComplete) return false;
        return _entries.TryGetValue(new(tenantId, moduleCode, requestHash, current), out value!);
    }

    public bool TryWrite(EntitlementDecisionSnapshotV1 value, EntitlementStateVersionV1 current)
    {
        if (!value.Version.IsComplete || value.Version != current) return false;
        var fenceKey = new FenceKey(value.TenantId, value.ModuleCode);
        if (_fences.TryGetValue(fenceKey, out var fence) && Compare(value.Version, fence) is not VersionOrder.Equal and not VersionOrder.Newer) return false;
        _fences.AddOrUpdate(fenceKey, value.Version, (_, existing) => Compare(value.Version, existing) is VersionOrder.Newer ? value.Version : existing);
        _entries[new(value.TenantId, value.ModuleCode, value.RequestHash, value.Version)] = value;
        return true;
    }

    public bool Invalidate(Guid tenantId, string moduleCode, EntitlementStateVersionV1 next)
    {
        if (!next.IsComplete) return false;
        var key = new FenceKey(tenantId, moduleCode);
        if (_fences.TryGetValue(key, out var current) && Compare(next, current) is VersionOrder.Older or VersionOrder.Incomparable) return false;
        _fences.AddOrUpdate(key, next, (_, existing) => Compare(next, existing) is VersionOrder.Newer ? next : existing);
        foreach (var entry in _entries.Keys.Where(x => x.TenantId == tenantId && x.ModuleCode == moduleCode && x.Version != next)) _entries.TryRemove(entry, out _);
        return true;
    }

    private static VersionOrder Compare(EntitlementStateVersionV1 left, EntitlementStateVersionV1 right)
    {
        var ge = left.PhysicalEntitlementVersion >= right.PhysicalEntitlementVersion && left.SubscriptionVersion >= right.SubscriptionVersion && left.ModuleApplicabilityVersion >= right.ModuleApplicabilityVersion;
        var le = left.PhysicalEntitlementVersion <= right.PhysicalEntitlementVersion && left.SubscriptionVersion <= right.SubscriptionVersion && left.ModuleApplicabilityVersion <= right.ModuleApplicabilityVersion;
        if (left == right) return VersionOrder.Equal;
        if (ge) return VersionOrder.Newer;
        if (le) return VersionOrder.Older;
        return VersionOrder.Incomparable;
    }

    private enum VersionOrder { Equal, Newer, Older, Incomparable }
    private readonly record struct FenceKey(Guid TenantId, string ModuleCode);
    private readonly record struct CacheKey(Guid TenantId, string ModuleCode, string RequestHash, EntitlementStateVersionV1 Version);
}

public sealed class PlatformEntitlementDecisionProvider : IPlatformEntitlementDecisionProvider
{
    private readonly IAuthoritativeEntitlementDecisionSource _source;
    private readonly VersionAwareEntitlementDecisionCache _cache;
    private readonly PlatformEntitlementAttestationOptions _options;

    public PlatformEntitlementDecisionProvider(IAuthoritativeEntitlementDecisionSource source, VersionAwareEntitlementDecisionCache cache, IOptions<PlatformEntitlementAttestationOptions> options)
    { _source = source; _cache = cache; _options = options.Value; }

    public async Task<EntitlementDecisionResultV1> DecideAsync(EntitlementDecisionRequestV1 request, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled) return new EntitlementDecisionResultV1.ServiceUnavailable(EntitlementDecisionFailureV1.ProviderDisabled);
        if (request.TenantId == Guid.Empty) return new EntitlementDecisionResultV1.ServiceUnavailable(EntitlementDecisionFailureV1.MalformedAuthority);
        string moduleCode, requestHash;
        try { moduleCode = EntitlementAttestationContractV1.NormalizeModuleCode(request.ModuleCode); requestHash = EntitlementAttestationContractV1.ValidateRequestHash(request.RequestHash); }
        catch (ArgumentException) { return new EntitlementDecisionResultV1.ServiceUnavailable(EntitlementDecisionFailureV1.MalformedAuthority); }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.AuthorityTimeout);
        try
        {
            var before = await _source.ReadCurrentVersionAsync(request.TenantId, moduleCode, timeout.Token);
            if (!before.IsComplete) return new EntitlementDecisionResultV1.ServiceUnavailable(EntitlementDecisionFailureV1.Indeterminate);
            if (_cache.TryGet(request.TenantId, moduleCode, requestHash, before, out var cached)) return new EntitlementDecisionResultV1.Authoritative(cached);
            var decision = await _source.ReadAsync(request.TenantId, moduleCode, requestHash, timeout.Token);
            var after = await _source.ReadCurrentVersionAsync(request.TenantId, moduleCode, timeout.Token);
            if (before != after || decision.Version != after || !after.IsComplete) return new EntitlementDecisionResultV1.ServiceUnavailable(EntitlementDecisionFailureV1.Indeterminate);
            _cache.TryWrite(decision, after);
            return new EntitlementDecisionResultV1.Authoritative(decision);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return new EntitlementDecisionResultV1.ServiceUnavailable(EntitlementDecisionFailureV1.Timeout); }
        catch (FormatException) { return new EntitlementDecisionResultV1.ServiceUnavailable(EntitlementDecisionFailureV1.MalformedAuthority); }
        catch { return new EntitlementDecisionResultV1.ServiceUnavailable(EntitlementDecisionFailureV1.ProviderUnavailable); }
    }
}
