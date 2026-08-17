namespace Diten.Platform.Application.Services;

public sealed class EntitlementCacheOptions
{
    public const string SectionName = "Authorization";

    public int CacheTtlSeconds { get; set; } = 300;

    public TimeSpan GetCacheTtl()
    {
        return TimeSpan.FromSeconds(CacheTtlSeconds > 0 ? CacheTtlSeconds : 300);
    }
}
