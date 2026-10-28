using System.Reflection;
using System.Text.Json;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Application.Features.Lookups;
using Diten.Platform.Application.Features.Lookups.Services;
using Diten.Platform.Domain.Entities;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

/// <summary>
/// MOD-0048-FU01 §21 (TRM-17) — the Treasury consumable sets <c>payment-method</c> and <c>currency-minor-units</c> in
/// <c>treasury-reference.json</c>, read through the SAME production code the startup catalog loader runs (duplicate
/// property guard, <c>Parse</c>, <c>NormalizeSet</c>) and the attribute contract a draft must pass before publish.
/// AC-R1 (allow list + catalog), AC-R2 (minor units; waits for the ISO 4217 list), AC-R4 (existing consumers unchanged:
/// their own suites). AC-R3 runtime parts: <see cref="TreasuryReferenceCatalogLoadMongoTests"/>.
/// </summary>
public sealed class TreasuryReferenceCatalogTests
{
    internal const string CatalogFile = "treasury-reference.json";
    internal const string PaymentMethod = "payment-method";
    internal const string CurrencyMinorUnits = "currency-minor-units";

    // TRM-17: the ISO 4217 list (SIX list-one.xml) has not been supplied yet; the set is not in the catalog. Remove the
    // Skip when the values are added — the assertions below are the AC-R2 gate.
    private const string CurrencyValuesPending = "TRM-17: currency-minor-units values wait for the ISO 4217 list (SIX list-one.xml).";

    private static readonly string[] PaymentMethodCodes =
        ["BANK_TRANSFER", "SEPA_CREDIT_TRANSFER", "SWIFT_TRANSFER", "DOMESTIC_TRANSFER", "CHEQUE", "CASH"];

    private static readonly string[] MinorUnitValues = ["0", "1", "2", "3", "4"];

    private static string ApiDirectory() => Path.Combine(RepoPaths.Root(), "services", "Diten.Platform", "src", "Diten.Platform.API");

    internal static string CatalogPath() => Path.Combine(ApiDirectory(), "Seed", "business-reference-data", CatalogFile);

    private static T InvokeLoader<T>(string method, params object[] args)
    {
        var info = typeof(BusinessReferenceDataCatalogLoaderService)
            .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(nameof(BusinessReferenceDataCatalogLoaderService), method);
        return (T)info.Invoke(null, args)!;
    }

    private static BusinessReferenceDataCatalogDocument Document()
    {
        var payload = File.ReadAllText(CatalogPath());
        typeof(BusinessReferenceDataCatalogLoaderService)
            .GetMethod("ValidateNoDuplicateJsonProperties", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [payload]);
        return InvokeLoader<BusinessReferenceDataCatalogDocument>("Parse", payload);
    }

    private static BusinessReferenceDataCatalogSetDocument Set(string code)
    {
        var set = Assert.Single(Document().Sets, s => s.SetCode == code);
        return InvokeLoader<BusinessReferenceDataCatalogSetDocument>("NormalizeSet", set);
    }

    private static JsonElement ReadSettings(string file) => JsonDocument.Parse(File.ReadAllText(Path.Combine(ApiDirectory(), file)),
        new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }).RootElement;

    private static void AssertAttributeContract(BusinessReferenceDataCatalogSetDocument set)
    {
        var validate = typeof(BusinessReferenceDataValidationService)
            .GetMethod("ValidateAttributeContract", BindingFlags.NonPublic | BindingFlags.Static)!;
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
                Attributes = v.Attributes.ToDictionary(x => x.Key, x => x.Value)
            }).ToList()
        };

        var issues = (List<string>)validate.Invoke(null, [version])!;
        Assert.True(issues.Count == 0, string.Join("; ", issues));
    }

    // ---- AC-R1 -------------------------------------------------------------------------------------------------

    [Fact]
    public void Catalog_is_a_business_reference_data_module_the_loader_accepts_with_a_version()
    {
        var document = Document();
        Assert.Equal("BusinessReferenceData", document.Module); // the loader refuses any other module (invalid_catalog_module)
        Assert.False(string.IsNullOrWhiteSpace(document.CatalogVersion));
    }

    [Fact]
    public void Payment_method_parses_through_the_production_loader_global_active_with_the_six_codes_in_order()
    {
        var set = Set(PaymentMethod);
        Assert.Equal("global", set.ScopeType);
        Assert.Equal("Active", set.Status);
        Assert.Equal(PaymentMethodCodes, set.Values.Select(v => v.ValueCode));
        Assert.All(set.Values, v =>
        {
            Assert.True(v.IsActive);
            Assert.False(string.IsNullOrWhiteSpace(v.DisplayName)); // English; UI labels come from the consumer's resx
        });
        Assert.Equal(Enumerable.Range(1, PaymentMethodCodes.Length).Select(i => i * 10), set.Values.Select(v => v.SortOrder));
        AssertAttributeContract(set);
    }

    [Theory]
    [InlineData(PaymentMethod)]
    [InlineData(CurrencyMinorUnits)]
    public void Both_sets_are_consumable_and_required_by_the_development_load(string code)
    {
        Assert.Contains(code, ReadSettings("appsettings.json").GetProperty("BusinessReferenceData").GetProperty("ConsumableSets")
            .EnumerateArray().Select(x => x.GetString()));
        Assert.Contains(code, BusinessReferenceDataConsumableSetsOptions.DefaultConsumableSets);
        Assert.Contains(code, ReadSettings("appsettings.Development.json").GetProperty("BusinessReferenceData")
            .GetProperty("CatalogLoad").GetProperty("RequiredSetCodes").EnumerateArray().Select(x => x.GetString()));

        var options = new BusinessReferenceDataConsumableSetsOptions();
        Assert.True(options.TryResolve(code.ToUpperInvariant(), out var resolved));
        Assert.Equal(code, resolved);
    }

    [Fact]
    public void A_set_code_outside_the_allow_list_still_answers_as_before()
    {
        var options = new BusinessReferenceDataConsumableSetsOptions();
        Assert.False(options.TryResolve("treasury-not-listed-set", out _));
    }

    [Fact]
    public void Payment_method_is_declared_in_no_other_seed_catalog()
    {
        foreach (var file in Directory.EnumerateFiles(Path.GetDirectoryName(CatalogPath())!, "*.json")
                     .Where(f => !f.EndsWith(CatalogFile, StringComparison.OrdinalIgnoreCase)))
        {
            using var json = JsonDocument.Parse(File.ReadAllText(file));
            if (json.RootElement.TryGetProperty("sets", out var sets))
            {
                var codes = sets.EnumerateArray().Select(s => s.GetProperty("set_code").GetString()).ToList();
                Assert.DoesNotContain(codes, c => string.Equals(c, PaymentMethod, StringComparison.OrdinalIgnoreCase));
                Assert.DoesNotContain(codes, c => string.Equals(c, CurrencyMinorUnits, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    // ---- AC-R2 -------------------------------------------------------------------------------------------------

    [Fact(Skip = CurrencyValuesPending)]
    public void Currency_minor_units_every_value_has_minor_units_0_to_4_and_no_duplicate_codes()
    {
        var set = Set(CurrencyMinorUnits);
        Assert.Equal("global", set.ScopeType);
        Assert.Equal("Active", set.Status);
        Assert.NotEmpty(set.Values);
        Assert.All(set.Values, v =>
        {
            Assert.True(v.Attributes.TryGetValue("minorUnits", out var units), $"{v.ValueCode}: minorUnits missing");
            Assert.Contains(units, MinorUnitValues);
        });
        Assert.Equal(set.Values.Count, set.Values.Select(v => v.ValueCode).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        AssertAttributeContract(set);
    }

    [Fact(Skip = CurrencyValuesPending)]
    public void Currency_minor_units_covers_every_code_the_currencies_lookup_returns()
    {
        // The production lookup (GET /api/lookups/reference/currencies) — not a copy of its RegionInfo walk.
        var build = typeof(PlatformLookupProvider).GetMethod("BuildCurrencyOptions", BindingFlags.NonPublic | BindingFlags.Static)!;
        var lookupCodes = ((IReadOnlyList<LookupOptionDto>)build.Invoke(null, null)!).Select(o => o.Value).ToList();
        var seeded = Set(CurrencyMinorUnits).Values.Select(v => v.ValueCode!).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = lookupCodes.Where(code => !seeded.Contains(code)).OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.True(missing.Count == 0, "currency-minor-units lacks: " + string.Join(", ", missing));
    }
}
