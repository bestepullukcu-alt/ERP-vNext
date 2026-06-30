namespace Diten.Platform.Application.Features.ModuleCatalog.Services;

/// <summary>
/// FIX-DOMAIN-SERVICE-CANONICAL — resolves a raw module Domain/Service value to the canonical lookup Code against
/// the live <c>platform_module_domains</c> / <c>platform_module_services</c> options (format-tolerant: matches by
/// Code or DisplayName). Used by the manifest reconcile (first-seed) and the create/update handlers so the catalog
/// only ever stores Codes. Unresolved values are preserved (never lost) and logged.
/// </summary>
public interface IModuleTaxonomyResolver
{
    Task<string> ResolveDomainCodeAsync(string? rawDomain, CancellationToken ct = default);

    Task<string> ResolveServiceCodeAsync(string? rawService, CancellationToken ct = default);
}
