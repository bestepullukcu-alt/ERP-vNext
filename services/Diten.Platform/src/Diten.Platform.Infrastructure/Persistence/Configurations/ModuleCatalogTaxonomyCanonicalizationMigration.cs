using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Catalog;
using Diten.Platform.Domain.Entities;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

/// <summary>
/// FIX-DOMAIN-SERVICE-CANONICAL — one-shot, idempotent backfill that pins every catalog item's drifted
/// <c>Domain</c>/<c>Service</c> value to the canonical lookup <b>Code</b>. Historically these stored three formats
/// (manifest enum-name, form DisplayName — including the 'Platform Shared Servicec' typo — or the lookup Code), so
/// the same domain produced TWO navigation group headings. We resolve each value against the live
/// <c>platform_module_domains</c> / <c>platform_module_services</c> options (format-tolerant: by Code OR DisplayName)
/// and rewrite it to the Code. Unmatched values are left untouched and logged.
///
/// <para>Order matters: the catalog rows are converted FIRST (while the typo'd DisplayName is still present so a
/// typo'd module value resolves), THEN the 'Servicec'→'Services' DisplayName typo is corrected on the lookup row.
/// Marker-gated (runs once) and otherwise idempotent (Code→Code is a no-op).</para>
/// </summary>
public static class ModuleCatalogTaxonomyCanonicalizationMigration
{
    private const string SeedKey = "ModuleCatalogTaxonomyCanonical-v1";
    private const string Actor = "system:module-taxonomy-canonical";
    private const string TypoDisplay = "Servicec";
    private const string TypoFixedDisplay = "Services";

    public static async Task MigrateAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        if (await SeedMarkerStore.ExistsAsync(database, SeedKey, ct))
        {
            return;
        }

        var domainOptions = await LoadOptionsAsync<ModuleDomain>(database, "platform_module_domains", d => d.Code, d => d.DisplayName, ct);
        var serviceOptions = await LoadOptionsAsync<ModuleService>(database, "platform_module_services", s => s.Code, s => s.DisplayName, ct);

        // 1) Convert catalog Domain/Service to canonical Code (typo'd DisplayName still present here so it resolves).
        var catalog = database.GetCollection<ModuleCatalogItem>(PlatformCollections.ModuleCatalog);
        var items = await catalog
            .Find(Builders<ModuleCatalogItem>.Filter.Eq(x => x.IsDeleted, false))
            .ToListAsync(ct);

        foreach (var item in items)
        {
            var resolvedDomain = ResolveOrWarn(item.Domain, domainOptions, item.ModuleCode, "Domain");
            var resolvedService = ResolveOrWarn(item.Service, serviceOptions, item.ModuleCode, "Service");

            if (string.Equals(resolvedDomain, item.Domain, StringComparison.Ordinal)
                && string.Equals(resolvedService, item.Service, StringComparison.Ordinal))
            {
                continue; // already canonical
            }

            var update = Builders<ModuleCatalogItem>.Update
                .Set(x => x.Domain, resolvedDomain)
                .Set(x => x.Service, resolvedService)
                .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)
                .Set(x => x.UpdatedBy, Actor);

            await catalog.UpdateOneAsync(
                Builders<ModuleCatalogItem>.Filter.Eq(x => x.Id, item.Id),
                update,
                cancellationToken: ct);
        }

        // 2) Correct the 'Platform Shared Servicec' → 'Platform Shared Services' DisplayName typo on the domain
        //    lookup row(s). Catalog rows already store Codes, so this is presentation-only and never re-touches them.
        var domains = database.GetCollection<ModuleDomain>(PlatformCollections.ModuleDomains);
        var typoRows = await domains
            .Find(Builders<ModuleDomain>.Filter.And(
                Builders<ModuleDomain>.Filter.Eq(x => x.IsDeleted, false),
                Builders<ModuleDomain>.Filter.Regex(x => x.DisplayName, new MongoDB.Bson.BsonRegularExpression(TypoDisplay))))
            .ToListAsync(ct);

        foreach (var row in typoRows)
        {
            var fixedDisplay = row.DisplayName.Replace(TypoDisplay, TypoFixedDisplay, StringComparison.Ordinal);
            if (string.Equals(fixedDisplay, row.DisplayName, StringComparison.Ordinal))
            {
                continue;
            }

            await domains.UpdateOneAsync(
                Builders<ModuleDomain>.Filter.Eq(x => x.Id, row.Id),
                Builders<ModuleDomain>.Update
                    .Set(x => x.DisplayName, fixedDisplay)
                    .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)
                    .Set(x => x.UpdatedBy, Actor),
                cancellationToken: ct);
        }

        await SeedMarkerStore.SetAsync(database, SeedKey, ct);
    }

    private static async Task<List<ModuleTaxonomyCanonicalizer.TaxonomyOption>> LoadOptionsAsync<T>(
        IMongoDatabase database,
        string collectionName,
        Func<T, string> codeSelector,
        Func<T, string> displaySelector,
        CancellationToken ct)
        where T : GlobalEntity
    {
        var collection = database.GetCollection<T>(collectionName);
        var rows = await collection
            .Find(Builders<T>.Filter.Eq(x => x.IsDeleted, false))
            .ToListAsync(ct);

        return rows
            .Select(r => new ModuleTaxonomyCanonicalizer.TaxonomyOption(codeSelector(r), displaySelector(r)))
            .ToList();
    }

    private static string ResolveOrWarn(
        string? rawValue,
        IReadOnlyCollection<ModuleTaxonomyCanonicalizer.TaxonomyOption> options,
        string moduleCode,
        string field)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return rawValue ?? string.Empty;
        }

        if (!ModuleTaxonomyCanonicalizer.TryResolveCode(rawValue, options, out var code))
        {
            Console.Error.WriteLine(
                $"[ModuleCatalogTaxonomyCanonicalizationMigration] {field} '{rawValue}' for module '{moduleCode}' matched no active lookup; left unchanged.");
        }

        return code;
    }
}
