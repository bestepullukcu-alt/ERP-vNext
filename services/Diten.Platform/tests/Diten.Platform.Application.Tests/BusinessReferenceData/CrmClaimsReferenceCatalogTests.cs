using System.Reflection;
using System.Text.Json;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

/// <summary>
/// WP-CL-REF-1 — the Claims v2 reference catalog (<c>crm-claims-reference.json</c>) is read through the SAME production
/// code the startup catalog loader runs: the duplicate-property guard, <c>Parse</c> and <c>NormalizeSet</c> of
/// <see cref="BusinessReferenceDataCatalogLoaderService"/>, and the version attribute contract of
/// <see cref="BusinessReferenceDataValidationService"/> (the gate a draft must pass before it is published). A copy of
/// those rules in the test would go green against a catalog production refuses.
/// <para>The catalog is read from the SOURCE tree (the startup worker loads every *.json in that folder from the
/// content root), found by walking up to the AGENTS.md marker so the test also runs from a git worktree.</para>
/// </summary>
public sealed class CrmClaimsReferenceCatalogTests
{
    private const string CatalogFile = "crm-claims-reference.json";

    private static readonly string[] ExpectedSetCodes =
        ["country-content-languages", "claim-country-closure-reason", "claim-adaptation-type", "evidence-type"];

    // The current global COUNTRY_CODES axis (user decision: start with these 6). Value codes must match it 1:1.
    private static readonly string[] CountryCodes = ["TR", "BY", "UZ", "TM", "GE", "AZ"];

    private static string SeedDirectory() => Path.Combine(RepoPaths.Root(), "services", "Diten.Platform", "src",
        "Diten.Platform.API", "Seed", "business-reference-data");

    private static string Payload() => File.ReadAllText(Path.Combine(SeedDirectory(), CatalogFile));

    private static T InvokeLoader<T>(string method, params object[] args)
    {
        var info = typeof(BusinessReferenceDataCatalogLoaderService)
            .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(nameof(BusinessReferenceDataCatalogLoaderService), method);
        return (T)info.Invoke(null, args)!;
    }

    private static IReadOnlyList<BusinessReferenceDataCatalogSetDocument> NormalizedSets()
    {
        var payload = Payload();
        typeof(BusinessReferenceDataCatalogLoaderService)
            .GetMethod("ValidateNoDuplicateJsonProperties", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [payload]);
        var document = InvokeLoader<BusinessReferenceDataCatalogDocument>("Parse", payload);
        Assert.Equal("BusinessReferenceData", document.Module);
        Assert.False(string.IsNullOrWhiteSpace(document.CatalogVersion));
        return document.Sets.Select(s => InvokeLoader<BusinessReferenceDataCatalogSetDocument>("NormalizeSet", s)).ToList();
    }

    private static BusinessReferenceDataCatalogSetDocument Set(string code) =>
        NormalizedSets().Single(s => s.SetCode == code);

    [Fact]
    public void Catalog_parses_through_the_production_loader_with_the_four_sets_scopes_and_counts()
    {
        var sets = NormalizedSets();
        Assert.Equal(ExpectedSetCodes.OrderBy(x => x), sets.Select(s => s.SetCode!).OrderBy(x => x));

        Assert.Equal("global", Set("country-content-languages").ScopeType);
        Assert.Equal("tenant", Set("claim-country-closure-reason").ScopeType);
        Assert.Equal("tenant", Set("claim-adaptation-type").ScopeType);
        Assert.Equal("global", Set("evidence-type").ScopeType);

        Assert.Equal(6, Set("country-content-languages").Values.Count);
        Assert.Equal(3, Set("claim-country-closure-reason").Values.Count);
        Assert.Equal(3, Set("claim-adaptation-type").Values.Count);
        Assert.Equal(5, Set("evidence-type").Values.Count);

        foreach (var set in sets)
        {
            Assert.Equal("Active", set.Status);
            Assert.False(string.IsNullOrWhiteSpace(set.Description));
            Assert.All(set.Values, v =>
            {
                Assert.True(v.IsActive);
                Assert.False(string.IsNullOrWhiteSpace(v.Description));
            });
            Assert.Equal(Enumerable.Range(1, set.Values.Count).Select(i => i * 10), set.Values.Select(v => v.SortOrder));
            Assert.Equal(set.Values.Count, set.Values.Select(v => v.ValueCode).Distinct(StringComparer.Ordinal).Count());
        }
    }

    [Fact]
    public void Value_codes_are_the_ones_the_claims_rules_rely_on()
    {
        Assert.Equal(["no-license", "regulation-disallows", "business-decision"],
            Set("claim-country-closure-reason").Values.Select(v => v.ValueCode));
        // "verbatim" is the one adaptation that needs no reason (CL-BE-1 rule keys on this exact code).
        Assert.Equal(["verbatim", "narrowed", "softened"], Set("claim-adaptation-type").Values.Select(v => v.ValueCode));
        Assert.Equal(["smpc-pil", "clinical-study", "literature", "internal-data", "regulatory-letter"],
            Set("evidence-type").Values.Select(v => v.ValueCode));
    }

    [Fact]
    public void Country_content_languages_mirror_country_codes_and_carry_the_required_languages_attribute()
    {
        var set = Set("country-content-languages");
        Assert.Equal(CountryCodes, set.Values.Select(v => v.ValueCode));

        var definition = Assert.Single(set.AttributeDefinitions);
        Assert.Equal("Languages", definition.AttributeCode);
        Assert.Equal("string", definition.DataType);
        Assert.True(definition.IsRequired);

        var expected = new Dictionary<string, string>
        {
            ["TR"] = "tr", ["BY"] = "ru,be", ["UZ"] = "uz,ru", ["TM"] = "tk,ru", ["GE"] = "ka", ["AZ"] = "az"
        };
        foreach (var value in set.Values)
        {
            Assert.Equal(expected[value.ValueCode!], value.Attributes["Languages"]);
            Assert.All(value.Attributes["Languages"].Split(','), l => Assert.Matches("^[a-z]{2}$", l));
        }
    }

    [Fact]
    public void Every_set_passes_the_production_attribute_contract_a_draft_must_meet_before_publish()
    {
        var validate = typeof(BusinessReferenceDataValidationService)
            .GetMethod("ValidateAttributeContract", BindingFlags.NonPublic | BindingFlags.Static)!;

        foreach (var set in NormalizedSets())
        {
            var version = new BusinessReferenceDataVersion
            {
                TenantId = Guid.Empty,
                AttributeDefinitions = set.AttributeDefinitions.Select(d => new BusinessReferenceDataAttributeDefinition
                {
                    AttributeCode = d.AttributeCode!, DisplayName = d.DisplayName!, DataType = d.DataType!, IsRequired = d.IsRequired
                }).ToList(),
                Values = set.Values.Select(v => new BusinessReferenceDataValue
                {
                    ValueCode = v.ValueCode!, DisplayName = v.DisplayName!, Description = v.Description,
                    IsActive = v.IsActive, SortOrder = v.SortOrder,
                    Attributes = v.Attributes.Count == 0 ? null : v.Attributes.ToDictionary(x => x.Key, x => x.Value)
                }).ToList()
            };

            var issues = (List<string>)validate.Invoke(null, [version])!;
            Assert.True(issues.Count == 0, $"{set.SetCode}: {string.Join("; ", issues)}");
        }

        // Sabotage check of the gate itself: a country without Languages must be refused.
        var broken = new BusinessReferenceDataVersion
        {
            TenantId = Guid.Empty,
            AttributeDefinitions = [new BusinessReferenceDataAttributeDefinition
                { AttributeCode = "Languages", DisplayName = "L", DataType = "string", IsRequired = true }],
            Values = [new BusinessReferenceDataValue { ValueCode = "TR", DisplayName = "Turkey" }]
        };
        Assert.Contains((List<string>)validate.Invoke(null, [broken])!, i => i.Contains("missing required attribute"));
    }

    [Fact]
    public void No_other_catalog_declares_these_set_codes_and_country_codes_is_left_alone()
    {
        foreach (var file in Directory.EnumerateFiles(SeedDirectory(), "*.json")
                     .Where(f => !f.EndsWith(CatalogFile, StringComparison.OrdinalIgnoreCase)))
        {
            using var json = JsonDocument.Parse(File.ReadAllText(file));
            if (!json.RootElement.TryGetProperty("sets", out var sets))
            {
                continue;
            }

            var codes = sets.EnumerateArray().Select(s => s.GetProperty("set_code").GetString()).ToList();
            Assert.DoesNotContain(codes, c => ExpectedSetCodes.Contains(c));
        }

        Assert.DoesNotContain("COUNTRY_CODES", NormalizedSets().Select(s => s.SetCode));
    }

    [Fact]
    public void Development_catalog_load_requires_the_four_new_sets()
    {
        var path = Path.Combine(RepoPaths.Root(), "services", "Diten.Platform", "src", "Diten.Platform.API",
            "appsettings.Development.json");
        using var json = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true
        });
        var required = json.RootElement.GetProperty("BusinessReferenceData").GetProperty("CatalogLoad")
            .GetProperty("RequiredSetCodes").EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Superset(ExpectedSetCodes.ToHashSet(), required.OfType<string>().ToHashSet());
        Assert.Contains("qms-owner-function", required); // existing entries untouched
    }
}
