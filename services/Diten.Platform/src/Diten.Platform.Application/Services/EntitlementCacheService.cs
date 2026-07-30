using System.Collections.Concurrent;
using Diten.Platform.Common.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Services;

public sealed class EntitlementCacheService
{
    private readonly IMemoryCache cache;
    private readonly EntitlementCacheOptions options;
    private readonly ConcurrentDictionary<string, RegisteredCacheKey> registeredKeys = new(StringComparer.Ordinal);

    public EntitlementCacheService(
        IMemoryCache cache,
        IOptions<EntitlementCacheOptions>? options = null)
    {
        this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
        this.options = options?.Value ?? new EntitlementCacheOptions();
    }

    public Task<EntitlementCheckResult> GetOrCreateModuleAsync(
        Guid tenantId,
        string moduleCode,
        Func<Task<EntitlementCheckResult>> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleCode);
        ArgumentNullException.ThrowIfNull(factory);

        return GetOrCreateAsync(BuildModuleKey(tenantId, moduleCode), factory);
    }

    public Task<EntitlementCheckResult> GetOrCreateFeatureAsync(
        Guid tenantId,
        string featureCode,
        Func<Task<EntitlementCheckResult>> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(featureCode);
        ArgumentNullException.ThrowIfNull(factory);

        return GetOrCreateAsync(BuildFeatureKey(tenantId, featureCode), factory);
    }

    public void EvictModule(Guid tenantId, string moduleCode)
    {
        Evict(BuildModuleKey(tenantId, moduleCode));
    }

    public void EvictFeature(Guid tenantId, string featureCode)
    {
        Evict(BuildFeatureKey(tenantId, featureCode));
    }

    public void EvictTenant(Guid tenantId)
    {
        foreach (var pair in registeredKeys)
        {
            if (pair.Value.TenantId == tenantId)
            {
                Evict(pair.Key);
            }
        }
    }

    public static string BuildModuleKey(Guid tenantId, string moduleCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleCode);

        return $"entitlement:module:{tenantId:D}:{NormalizeCode(moduleCode)}";
    }

    public static string BuildFeatureKey(Guid tenantId, string featureCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(featureCode);

        return $"entitlement:feature:{tenantId:D}:{NormalizeCode(featureCode)}";
    }

    private async Task<EntitlementCheckResult> GetOrCreateAsync(
        string cacheKey,
        Func<Task<EntitlementCheckResult>> factory)
    {
        if (cache.TryGetValue(cacheKey, out EntitlementCheckResult? cached) && cached is not null)
        {
            return cached;
        }

        var result = await factory();
        if (result.IsCacheable)
        {
            var registration = RegisteredCacheKey.From(cacheKey);
            cache.Set(cacheKey, result, CreateEntryOptions(cacheKey, registration));
            registeredKeys[cacheKey] = registration;
        }

        return result;
    }

    private void Evict(string cacheKey)
    {
        registeredKeys.TryRemove(cacheKey, out _);
        cache.Remove(cacheKey);
    }

    private MemoryCacheEntryOptions CreateEntryOptions(string cacheKey, RegisteredCacheKey registration)
    {
        return new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = options.GetCacheTtl()
        }.RegisterPostEvictionCallback(static (key, _, _, state) =>
        {
            if (key is string cacheKey && state is CacheEntryRegistration callback)
            {
                if (callback.CacheService.registeredKeys.TryGetValue(cacheKey, out var current)
                    && ReferenceEquals(current, callback.Registration))
                {
                    callback.CacheService.registeredKeys.TryRemove(cacheKey, out _);
                }
            }
        }, new CacheEntryRegistration(this, registration));
    }

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private sealed record RegisteredCacheKey(Guid TenantId)
    {
        public static RegisteredCacheKey From(string cacheKey)
        {
            var parts = cacheKey.Split(':');
            return new RegisteredCacheKey(Guid.Parse(parts[2]));
        }
    }

    private sealed record CacheEntryRegistration(
        EntitlementCacheService CacheService,
        RegisteredCacheKey Registration);
}
