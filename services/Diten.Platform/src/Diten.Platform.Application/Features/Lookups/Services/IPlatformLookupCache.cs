using Diten.Platform.Application.Features.Lookups;

namespace Diten.Platform.Application.Features.Lookups.Services;

public interface IPlatformLookupCache
{
    Task<IReadOnlyList<LookupOptionDto>> GetOrCreateAsync(
        string cacheKey,
        TimeSpan absoluteExpirationRelativeToNow,
        Func<CancellationToken, Task<IReadOnlyList<LookupOptionDto>>> factory,
        CancellationToken ct);
}
