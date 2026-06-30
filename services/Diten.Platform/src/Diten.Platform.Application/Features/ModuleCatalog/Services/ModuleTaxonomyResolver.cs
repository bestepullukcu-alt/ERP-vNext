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

        if (!ModuleTaxonomyCanonicalizer.TryResolveCode(rawDomain, options, out var code) && code.Length > 0)
        {
            _logger.LogWarning("Module domain '{RawDomain}' did not match any active lookup; preserving as-is.", rawDomain);
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
