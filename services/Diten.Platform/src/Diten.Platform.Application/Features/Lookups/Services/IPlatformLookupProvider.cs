using Diten.Platform.Application.Features.Lookups;

namespace Diten.Platform.Application.Features.Lookups.Services;

public interface IPlatformLookupProvider
{
    Task<IReadOnlyList<LookupOptionDto>> GetCurrenciesAsync(CancellationToken ct);
    Task<IReadOnlyList<LookupOptionDto>> GetLocalesAsync(CancellationToken ct);
    Task<IReadOnlyList<LookupOptionDto>> GetTimezonesAsync(CancellationToken ct);
    Task<IReadOnlyList<LookupOptionDto>> GetTenantTiersAsync(CancellationToken ct);
    Task<IReadOnlyList<LookupOptionDto>?> GetLookupOptionsAsync(string lookupKey, CancellationToken ct);
}
