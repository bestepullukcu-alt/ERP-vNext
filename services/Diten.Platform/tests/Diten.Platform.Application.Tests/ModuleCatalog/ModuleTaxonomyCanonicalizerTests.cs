using Diten.Platform.Domain.Catalog;
using Xunit;

namespace Diten.Platform.Application.Tests.ModuleCatalog;

// FIX-DOMAIN-SERVICE-CANONICAL — the resolver must collapse the three historical formats (manifest enum-name,
// form DisplayName incl. the 'Servicec' typo, and the lookup Code) onto the single canonical Code.
public sealed class ModuleTaxonomyCanonicalizerTests
{
    private static readonly ModuleTaxonomyCanonicalizer.TaxonomyOption[] Domains =
    [
        new("PLATFORM-SHARED-SERVICES", "Platform Shared Servicec"), // live row still carries the typo
        new("DEVENABLEMENT", "Developer Enablement"),
        new("MASTER-DATA-MANAGEMENT", "Master Data Management")
    ];

    [Theory]
    // manifest enum-name → Code
    [InlineData("PlatformSharedServices", "PLATFORM-SHARED-SERVICES")]
    // form DisplayName (with the typo) → Code
    [InlineData("Platform Shared Servicec", "PLATFORM-SHARED-SERVICES")]
    // already the Code (dashed) → unchanged
    [InlineData("PLATFORM-SHARED-SERVICES", "PLATFORM-SHARED-SERVICES")]
    // enum-name / DisplayName for another domain
    [InlineData("DevEnablement", "DEVENABLEMENT")]
    [InlineData("Developer Enablement", "DEVENABLEMENT")]
    // separator/case-insensitive
    [InlineData("master data management", "MASTER-DATA-MANAGEMENT")]
    public void ResolveCode_maps_every_format_to_canonical_code(string raw, string expected)
    {
        Assert.Equal(expected, ModuleTaxonomyCanonicalizer.ResolveCode(raw, Domains));
    }

    [Fact]
    public void ResolveCode_preserves_unmatched_value()
    {
        Assert.Equal("Totally Unknown", ModuleTaxonomyCanonicalizer.ResolveCode("  Totally Unknown  ", Domains));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveCode_blank_returns_empty(string? raw)
    {
        Assert.Equal(string.Empty, ModuleTaxonomyCanonicalizer.ResolveCode(raw, Domains));
    }

    [Fact]
    public void TryResolveCode_reports_match_and_miss()
    {
        Assert.True(ModuleTaxonomyCanonicalizer.TryResolveCode("PlatformSharedServices", Domains, out var hit));
        Assert.Equal("PLATFORM-SHARED-SERVICES", hit);

        Assert.False(ModuleTaxonomyCanonicalizer.TryResolveCode("nope", Domains, out var miss));
        Assert.Equal("nope", miss); // preserved
    }

    // FIX-DOMAIN-NORMALIZATION — the write paths must not persist an unmatched value as free text (that is how
    // "MASTER-DATA-MANAGEMENT" came to live beside "MASTERDATAMANAGEMENT" and split one sidebar heading in two).
    [Fact]
    public void ResolveCodeOrKey_collapses_an_unmatched_value_onto_its_key()
    {
        var code = ModuleTaxonomyCanonicalizer.ResolveCodeOrKey("  Field-Service Management  ", Domains, out var matched);

        Assert.False(matched);
        Assert.Equal("FIELDSERVICEMANAGEMENT", code); // canonicalized, not preserved as free text, not dropped
    }

    [Fact]
    public void ResolveCodeOrKey_returns_the_lookup_code_on_a_match()
    {
        var code = ModuleTaxonomyCanonicalizer.ResolveCodeOrKey("PlatformSharedServices", Domains, out var matched);

        Assert.True(matched);
        Assert.Equal("PLATFORM-SHARED-SERVICES", code); // the option's Code wins, even when it carries separators
    }

    // Two drifted spellings of ONE unknown domain must converge — the property that makes a split heading impossible.
    [Fact]
    public void ResolveCodeOrKey_maps_two_spellings_of_one_unknown_domain_to_the_same_value()
    {
        var a = ModuleTaxonomyCanonicalizer.ResolveCodeOrKey("FIELD-SERVICE-MANAGEMENT", Domains, out _);
        var b = ModuleTaxonomyCanonicalizer.ResolveCodeOrKey("FieldServiceManagement", Domains, out _);

        Assert.Equal(a, b);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void ResolveCodeOrKey_blank_returns_empty(string? raw)
    {
        Assert.Equal(string.Empty, ModuleTaxonomyCanonicalizer.ResolveCodeOrKey(raw, Domains, out _));
    }
}
