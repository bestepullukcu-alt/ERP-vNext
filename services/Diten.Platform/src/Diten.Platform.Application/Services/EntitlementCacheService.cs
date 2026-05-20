using Diten.Platform.Common.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Services;

public sealed class EntitlementCacheService
{
    private readonly IMemoryCache cache;
    private readonly EntitlementCacheOptions options;

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
            cache.Set(cacheKey, result, options.GetCacheTtl());
        }

        return result;
    }

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();
}
