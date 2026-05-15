using Diten.Platform.Application.Features.Lookups;
using Diten.Platform.Application.Features.Lookups.Services;
using Microsoft.Extensions.Caching.Memory;

namespace Diten.Platform.Infrastructure.Services;

public sealed class PlatformLookupMemoryCache : IPlatformLookupCache
{
    private readonly IMemoryCache _cache;

    public PlatformLookupMemoryCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    public async Task<IReadOnlyList<LookupOptionDto>> GetOrCreateAsync(
        string cacheKey,
        TimeSpan absoluteExpirationRelativeToNow,
        Func<CancellationToken, Task<IReadOnlyList<LookupOptionDto>>> factory,
        CancellationToken ct)
    {
        if (_cache.TryGetValue<IReadOnlyList<LookupOptionDto>>(cacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var value = await factory(ct);
        _cache.Set(
            cacheKey,
            value,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = absoluteExpirationRelativeToNow
            });

        return value;
    }
}
