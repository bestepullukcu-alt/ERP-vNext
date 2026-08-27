using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Domain.Catalog;
using Diten.Platform.Domain.Entities;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

/// <summary>
/// FIX-DOMAIN-NORMALIZATION — pins every LIVE catalog row's <c>Domain</c> to the canonical domain-lookup
/// <b>Code</b>, so two spellings of one domain can never split a sidebar heading.
///
/// <para>Why a second migration exists next to <see cref="ModuleCatalogTaxonomyCanonicalizationMigration"/>: that
/// one is marker-gated ("run once"), and rows written AFTER it ran re-drifted — the live catalog still carried
/// <c>MASTER-DATA-MANAGEMENT</c> (Reference Data, Legal Entity) beside <c>MASTERDATAMANAGEMENT</c> (Product/Item/SKU),
/// which rendered "Master Data Management" TWICE in the tenant menu. Following the
/// <see cref="ModuleDomainDeduplicationMigration"/> precedent, this one is NOT marker-gated: it is re-runnable and
/// self-idempotent, so it also heals any future drift on the next startup.</para>
///
/// <para>Domain is a SOFT (operator-owned) catalog field that manifest re-pushes never overwrite, so normalizing the
/// write paths alone would have left the live rows — and the double heading — exactly as they were. This carries
/// them over.</para>
///
/// <para>Unmatched domains: a value that matches no live lookup row is NOT left as free text and NOT dropped — it is
/// rewritten to its normalized key (the same form self-registration mints a lookup row under) and logged loudly. That
/// keeps the module's domain while making a two-spelling split impossible. Today every live catalog domain matches a
/// lookup row, so this path only covers future arrivals.</para>
/// </summary>
public static class ModuleCatalogDomainCanonicalizationMigration
{
    private const string Actor = "system:module-catalog-domain-canonical";

    /// <summary>One rewrite decision. Pure output of <see cref="Plan"/> (no IO).</summary>
    public sealed record DomainRewrite(Guid ItemId, string ModuleCode, string OldDomain, string NewDomain, bool Matched);

    /// <summary>
    /// Pure, unit-testable core. A rewrite is emitted ONLY when the stored Domain actually differs from its canonical
    /// form, so a re-run over an already-canonical catalog yields an empty list — that is the idempotency guarantee.
    /// Blank domains are left alone (a module with no domain is a catalog gap, not a drift to fix here).
    /// </summary>
    public static IReadOnlyList<DomainRewrite> Plan(
        IEnumerable<ModuleCatalogItem> liveItems,
        IReadOnlyCollection<ModuleTaxonomyCanonicalizer.TaxonomyOption> domainOptions)
    {
        var rewrites = new List<DomainRewrite>();

        foreach (var item in liveItems)
        {
            var current = item.Domain ?? string.Empty;
            if (string.IsNullOrWhiteSpace(current))
            {
                continue;
            }

            var canonical = ModuleTaxonomyCanonicalizer.ResolveCodeOrKey(current, domainOptions, out var matched);
            if (canonical.Length == 0 || string.Equals(canonical, current, StringComparison.Ordinal))
            {
                continue; // already canonical → no write (idempotent re-run)
            }

            rewrites.Add(new DomainRewrite(item.Id, item.ModuleCode ?? string.Empty, current, canonical, matched));
        }

        return rewrites;
    }

    public static async Task MigrateAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        var domains = database.GetCollection<ModuleDomain>(PlatformCollections.ModuleDomains);
        var domainRows = await domains
            .Find(Builders<ModuleDomain>.Filter.Eq(x => x.IsDeleted, false))
            .ToListAsync(ct);

        // Match against every LIVE lookup row (active AND inactive), mirroring the self-registration path: a domain
        // the operator merely DEACTIVATED must still be recognised, never treated as unknown.
        var options = domainRows
            .Select(d => new ModuleTaxonomyCanonicalizer.TaxonomyOption(d.Code, d.DisplayName))
            .ToList();

        var catalog = database.GetCollection<ModuleCatalogItem>(PlatformCollections.ModuleCatalog);
        var items = await catalog
            .Find(Builders<ModuleCatalogItem>.Filter.Eq(x => x.IsDeleted, false))
            .ToListAsync(ct);

        foreach (var rewrite in Plan(items, options))
        {
            if (!rewrite.Matched)
            {
                Console.Error.WriteLine(
                    $"[ModuleCatalogDomainCanonicalizationMigration] Domain '{rewrite.OldDomain}' for module " +
                    $"'{rewrite.ModuleCode}' matched no live domain lookup row; canonicalized to '{rewrite.NewDomain}'. " +
                    "Register it in Domain Management or correct the module's domain.");
            }

            await catalog.UpdateOneAsync(
                Builders<ModuleCatalogItem>.Filter.Eq(x => x.Id, rewrite.ItemId),
                Builders<ModuleCatalogItem>.Update
                    .Set(x => x.Domain, rewrite.NewDomain)
                    .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)
                    .Set(x => x.UpdatedBy, Actor),
                cancellationToken: ct);

            Console.Error.WriteLine(
                $"[ModuleCatalogDomainCanonicalizationMigration] Module '{rewrite.ModuleCode}': " +
                $"Domain '{rewrite.OldDomain}' → '{rewrite.NewDomain}'.");
        }
    }
}
