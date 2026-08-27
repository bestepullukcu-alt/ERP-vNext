using Diten.Platform.Domain.Catalog;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Infrastructure.Persistence.Configurations;
using Xunit;

namespace Diten.Platform.Application.Tests.ModuleCatalog;

// FIX-DOMAIN-NORMALIZATION — the pure planner behind ModuleCatalogDomainCanonicalizationMigration. The live bug:
// REFERENCE-DATA and LEGAL-ENTITY carried Domain "MASTER-DATA-MANAGEMENT" while PRODUCT-ITEM-SKU-MASTER carried
// "MASTERDATAMANAGEMENT", so the tenant sidebar rendered "Master Data Management" twice with the modules split.
public sealed class ModuleCatalogDomainCanonicalizationMigrationTests
{
    private static readonly ModuleTaxonomyCanonicalizer.TaxonomyOption[] Domains =
    [
        new("MASTERDATAMANAGEMENT", "Master Data Management"),
        new("PLATFORMSHAREDSERVICES", "Platform Shared Services"),
        new("DEVENABLEMENT", "Developer Enablement")
    ];

    private static ModuleCatalogItem Item(string code, string domain) =>
        new() { ModuleCode = code, ModuleName = code, DisplayName = code, Domain = domain, Service = "SVC" };

    [Fact]
    public void Plan_collapses_the_two_live_spellings_onto_one_canonical_domain()
    {
        var items = new[]
        {
            Item("REFERENCE-DATA", "MASTER-DATA-MANAGEMENT"),
            Item("LEGAL-ENTITY", "MASTER-DATA-MANAGEMENT"),
            Item("PRODUCT-ITEM-SKU-MASTER", "MASTERDATAMANAGEMENT")
        };

        var rewrites = ModuleCatalogDomainCanonicalizationMigration.Plan(items, Domains);

        // Only the two drifted rows are touched; the already-canonical one is left alone.
        Assert.Equal(2, rewrites.Count);
        Assert.All(rewrites, r => Assert.Equal("MASTERDATAMANAGEMENT", r.NewDomain));
        Assert.All(rewrites, r => Assert.True(r.Matched));
        Assert.DoesNotContain(rewrites, r => r.ModuleCode == "PRODUCT-ITEM-SKU-MASTER");

        // After applying, all three share ONE domain key — the split heading is arithmetically impossible.
        foreach (var r in rewrites)
        {
            items.Single(i => i.ModuleCode == r.ModuleCode).Domain = r.NewDomain;
        }
        Assert.Single(items.Select(i => i.Domain).Distinct(StringComparer.Ordinal));
    }

    // MUTATION GUARD (idempotency): a SECOND run over the migrated catalog must plan NOTHING. If Plan ever emits a
    // rewrite for an already-canonical row, the migration would rewrite UpdatedAt/UpdatedBy on every startup.
    [Fact]
    public void Plan_second_run_over_migrated_catalog_is_a_no_op()
    {
        var items = new[]
        {
            Item("REFERENCE-DATA", "MASTER-DATA-MANAGEMENT"),
            Item("PRODUCT-ITEM-SKU-MASTER", "MASTERDATAMANAGEMENT")
        };

        foreach (var r in ModuleCatalogDomainCanonicalizationMigration.Plan(items, Domains))
        {
            items.Single(i => i.ModuleCode == r.ModuleCode).Domain = r.NewDomain;
        }

        Assert.Empty(ModuleCatalogDomainCanonicalizationMigration.Plan(items, Domains));
    }

    [Theory]
    // manifest enum-name, DisplayName and hyphenated Code all land on the lookup Code
    [InlineData("MasterDataManagement", "MASTERDATAMANAGEMENT")]
    [InlineData("Master Data Management", "MASTERDATAMANAGEMENT")]
    [InlineData("MASTER-DATA-MANAGEMENT", "MASTERDATAMANAGEMENT")]
    [InlineData("PLATFORM-SHARED-SERVICES", "PLATFORMSHAREDSERVICES")]
    [InlineData("DevEnablement", "DEVENABLEMENT")]
    public void Plan_maps_every_drifted_spelling_to_the_lookup_code(string stored, string expected)
    {
        var rewrite = Assert.Single(
            ModuleCatalogDomainCanonicalizationMigration.Plan([Item("MOD", stored)], Domains));
        Assert.Equal(expected, rewrite.NewDomain);
    }

    // DECISION — a domain with NO lookup row is neither dropped nor kept as free text: it collapses onto its
    // normalized key (the same form self-registration mints a lookup row under) and is reported as unmatched so
    // the migration can log it loudly.
    [Fact]
    public void Plan_canonicalizes_an_unknown_domain_to_its_key_and_flags_it_unmatched()
    {
        var rewrite = Assert.Single(
            ModuleCatalogDomainCanonicalizationMigration.Plan([Item("MOD", "Field-Service Management")], Domains));

        Assert.Equal("FIELDSERVICEMANAGEMENT", rewrite.NewDomain);
        Assert.False(rewrite.Matched);
        Assert.Equal("Field-Service Management", rewrite.OldDomain); // never silently lost
    }

    [Fact]
    public void Plan_leaves_a_blank_domain_alone()
    {
        Assert.Empty(ModuleCatalogDomainCanonicalizationMigration.Plan([Item("MOD", "  ")], Domains));
    }
}
