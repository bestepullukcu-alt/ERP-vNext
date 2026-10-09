using System.Reflection;
using System.Text.Json;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

/// <summary>
/// WP-VW-W2 (A1) — the visit workspace reason catalog (<c>crm-visit-reference.json</c>) read through the SAME production
/// code the startup catalog loader runs (duplicate-property guard, <c>Parse</c>, <c>NormalizeSet</c>) and the version
/// attribute contract a draft must pass before publish. Also: the set is required by the development catalog load and
/// listed as consumable (a rep role reads it through consumable-sets).
/// </summary>
public sealed class CrmVisitReferenceCatalogTests
{
    private const string CatalogFile = "crm-visit-reference.json";
    private const string SetCode = "visit-outcome-reason";
    private static readonly string[] Languages = ["tr", "fr", "es", "zh", "ar", "ru"];

    private static readonly string[] ActiveCodes =
    [
        "doctor_unavailable", "doctor_declined", "clinic_closed", "rep_unavailable", "meeting_conflict",
        "travel_weather", "target_inactive", "other"
    ];

    private static string ApiDirectory() => Path.Combine(RepoPaths.Root(), "services", "Diten.Platform", "src", "Diten.Platform.API");

    private static string SeedDirectory() => Path.Combine(ApiDirectory(), "Seed", "business-reference-data");

    private static T InvokeLoader<T>(string method, params object[] args)
    {
        var info = typeof(BusinessReferenceDataCatalogLoaderService)
            .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(nameof(BusinessReferenceDataCatalogLoaderService), method);
        return (T)info.Invoke(null, args)!;
    }

    private static BusinessReferenceDataCatalogSetDocument Set()
    {
        var payload = File.ReadAllText(Path.Combine(SeedDirectory(), CatalogFile));
        typeof(BusinessReferenceDataCatalogLoaderService)
            .GetMethod("ValidateNoDuplicateJsonProperties", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [payload]);
        var document = InvokeLoader<BusinessReferenceDataCatalogDocument>("Parse", payload);
        Assert.Equal("BusinessReferenceData", document.Module);
        Assert.False(string.IsNullOrWhiteSpace(document.CatalogVersion));
        var set = Assert.Single(document.Sets);
        return InvokeLoader<BusinessReferenceDataCatalogSetDocument>("NormalizeSet", set);
    }

    [Fact]
    public void Catalog_parses_through_the_production_loader_as_one_tenant_set_with_8_active_and_2_legacy_values()
    {
        var set = Set();
        Assert.Equal(SetCode, set.SetCode);
        Assert.Equal("tenant", set.ScopeType);
        Assert.Equal("Active", set.Status);
        Assert.Equal(ActiveCodes, set.Values.Where(v => v.IsActive).Select(v => v.ValueCode));
        Assert.Equal(["rescheduled_by_doctor", "rescheduled_by_rep"], set.Values.Where(v => !v.IsActive).Select(v => v.ValueCode));
        Assert.Equal(Enumerable.Range(1, 10).Select(i => i * 10), set.Values.Select(v => v.SortOrder));
    }

    [Fact]
    public void Every_value_carries_all_seven_languages_and_its_rules()
    {
        foreach (var value in Set().Values)
        {
            Assert.False(string.IsNullOrWhiteSpace(value.DisplayName)); // en
            foreach (var language in Languages)
            {
                Assert.True(value.Attributes.TryGetValue("label_" + language, out var label) && !string.IsNullOrWhiteSpace(label),
                    $"{value.ValueCode}: label_{language}");
                Assert.NotEqual(value.ValueCode, label);
            }

            Assert.Contains(value.Attributes["requires_note"], new[] { "true", "false" });
        }

        var byCode = Set().Values.ToDictionary(v => v.ValueCode!);
        Assert.Equal("Diğer", byCode["other"].Attributes["label_tr"]);
        Assert.Equal("true", byCode["other"].Attributes["requires_note"]);
        Assert.All(ActiveCodes.Where(c => c != "other"), c => Assert.Equal("false", byCode[c].Attributes["requires_note"]));
        Assert.Equal("cancel,missed", byCode["target_inactive"].Attributes["applies_to"]);
        Assert.All(ActiveCodes.Where(c => c != "target_inactive"),
            c => Assert.Equal("cancel,missed,reschedule", byCode[c].Attributes["applies_to"]));
        Assert.False(byCode["rescheduled_by_doctor"].Attributes.ContainsKey("applies_to"));
    }

    [Fact]
    public void The_set_passes_the_production_attribute_contract_a_draft_must_meet_before_publish()
    {
        var validate = typeof(BusinessReferenceDataValidationService)
            .GetMethod("ValidateAttributeContract", BindingFlags.NonPublic | BindingFlags.Static)!;
        var set = Set();
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
        Assert.True(issues.Count == 0, string.Join("; ", issues));

        // the gate itself: a value missing a required label is refused
        version.Values[0].Attributes!.Remove("label_ar");
        Assert.Contains((List<string>)validate.Invoke(null, [version])!, i => i.Contains("missing required attribute"));
    }

    [Fact]
    public void No_other_catalog_declares_the_set()
    {
        foreach (var file in Directory.EnumerateFiles(SeedDirectory(), "*.json")
                     .Where(f => !f.EndsWith(CatalogFile, StringComparison.OrdinalIgnoreCase)))
        {
            using var json = JsonDocument.Parse(File.ReadAllText(file));
            if (json.RootElement.TryGetProperty("sets", out var sets))
            {
                Assert.DoesNotContain(sets.EnumerateArray().Select(s => s.GetProperty("set_code").GetString()), c => c == SetCode);
            }
        }
    }

    [Fact]
    public void The_set_is_required_by_the_development_load_and_consumable()
    {
        static JsonElement Read(string file) => JsonDocument.Parse(File.ReadAllText(Path.Combine(ApiDirectory(), file)),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }).RootElement;

        var required = Read("appsettings.Development.json").GetProperty("BusinessReferenceData").GetProperty("CatalogLoad")
            .GetProperty("RequiredSetCodes").EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Contains(SetCode, required);
        Assert.Contains("evidence-type", required); // existing entries untouched

        var consumable = Read("appsettings.json").GetProperty("BusinessReferenceData").GetProperty("ConsumableSets")
            .EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Contains(SetCode, consumable);
        Assert.Contains("content-moderator-role", consumable);
    }
}
