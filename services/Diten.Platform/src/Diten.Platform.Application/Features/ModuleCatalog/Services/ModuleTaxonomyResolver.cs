using Diten.Platform.Domain.Catalog;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.ModuleCatalog.Services;

public sealed class ModuleTaxonomyResolver : IModuleTaxonomyResolver
{
    private readonly IModuleDomainRepository _domainRepository;
    private readonly IModuleServiceRepository _serviceRepository;
    private readonly ILogger<ModuleTaxonomyResolver> _logger;

    public ModuleTaxonomyResolver(
        IModuleDomainRepository domainRepository,
        IModuleServiceRepository serviceRepository,
        ILogger<ModuleTaxonomyResolver> logger)
    {
        _domainRepository = domainRepository;
        _serviceRepository = serviceRepository;
        _logger = logger;
    }

    public async Task<string> ResolveDomainCodeAsync(string? rawDomain, CancellationToken ct = default)
    {
        var domains = await _domainRepository.GetActiveAsync(ct);
        var options = domains
            .Select(d => new ModuleTaxonomyCanonicalizer.TaxonomyOption(d.Code, d.DisplayName))
            .ToList();

        // FIX-DOMAIN-NORMALIZATION — an unmatched domain is canonicalized to its normalized key rather than stored
        // as free text. Storing the raw value was how "MASTER-DATA-MANAGEMENT" ended up living beside
        // "MASTERDATAMANAGEMENT" in the catalog and split one sidebar heading in two. The value is still kept (the
        // key is derived from it, and self-registration mints unknown lookup rows under this same key form) — the
        // miss is only logged, never silently dropped.
        var code = ModuleTaxonomyCanonicalizer.ResolveCodeOrKey(rawDomain, options, out var matched);
        if (!matched && code.Length > 0)
        {
            _logger.LogWarning(
                "Module domain '{RawDomain}' did not match any active lookup; canonicalized to '{Code}'.", rawDomain, code);
        }

        return code;
    }

    public async Task<string> ResolveServiceCodeAsync(string? rawService, CancellationToken ct = default)
    {
        var services = await _serviceRepository.GetActiveAsync(ct);
        var options = services
            .Select(s => new ModuleTaxonomyCanonicalizer.TaxonomyOption(s.Code, s.DisplayName))
            .ToList();

        if (!ModuleTaxonomyCanonicalizer.TryResolveCode(rawService, options, out var code) && code.Length > 0)
        {
            _logger.LogWarning("Module service '{RawService}' did not match any active lookup; preserving as-is.", rawService);
        }

        return code;
    }
}
