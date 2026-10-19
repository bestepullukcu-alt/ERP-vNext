using System.Reflection;
using System.Text.Json;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Application.Features.Crm.SelfRegistration;
using Diten.Platform.Domain.Entities;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

/// <summary>
/// WP-VW-W4 (W4-BE-c, §2.1) — the five doctor profile / potential sets (in <c>crm-visit-report-v2-reference.json</c>) read
/// through the SAME production code the startup catalog loader runs, the attribute contract a draft must pass before
/// publish, their development-load / consumable registration, and the potential thresholds page + the doctor insights keys
/// on the CRM manifest.
/// </summary>
public sealed class CrmDoctorInsightsCatalogTests
{
    private const string CatalogFile = "crm-visit-report-v2-reference.json";
    private static readonly string[] Languages = ["tr", "fr", "es", "zh", "ar", "ru"];

    public static readonly TheoryData<string, string[]> Sets = new()
    {
        { "crm-adoption-stage", ["unaware", "aware", "evaluating", "trying", "regular", "advocate"] },
        { "crm-share-bucket", ["none", "occasional", "half", "mostly", "always"] },
        { "crm-adoption-typology", ["innovator", "early_adopter", "early_majority", "late_majority", "laggard"] },
        { "crm-communication-style", ["analytical", "driver", "amiable", "expressive"] },
        { "crm-kol-level", ["none", "local", "regional", "national", "international"] }
    };

    private static string ApiDirectory() => Path.Combine(RepoPaths.Root(), "services", "Diten.Platform", "src", "Diten.Platform.API");

    private static T InvokeLoader<T>(string method, params object[] args)
    {
        var info = typeof(BusinessReferenceDataCatalogLoaderService)
            .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(nameof(BusinessReferenceDataCatalogLoaderService), method);
        return (T)info.Invoke(null, args)!;
    }

    private static BusinessReferenceDataCatalogSetDocument Set(string code)
    {
        var payload = File.ReadAllText(Path.Combine(ApiDirectory(), "Seed", "business-reference-data", CatalogFile));
        typeof(BusinessReferenceDataCatalogLoaderService)
            .GetMethod("ValidateNoDuplicateJsonProperties", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [payload]);
        var document = InvokeLoader<BusinessReferenceDataCatalogDocument>("Parse", payload);
        Assert.Equal("BusinessReferenceData", document.Module);
        var set = Assert.Single(document.Sets, s => s.SetCode == code);
        return InvokeLoader<BusinessReferenceDataCatalogSetDocument>("NormalizeSet", set);
    }

    [Theory]
    [MemberData(nameof(Sets))]
    public void Each_set_parses_through_the_production_loader_tenant_scoped_active_in_order(string code, string[] values)
    {
        var set = Set(code);
        Assert.Equal("tenant", set.ScopeType);
        Assert.Equal("Active", set.Status);
        Assert.Equal(values, set.Values.Select(v => v.ValueCode));
        Assert.All(set.Values, v => Assert.True(v.IsActive));
        Assert.Equal(Enumerable.Range(1, values.Length).Select(i => i * 10), set.Values.Select(v => v.SortOrder));
    }

    [Theory]
    [MemberData(nameof(Sets))]
    public void Every_value_carries_all_seven_languages_without_echo(string code, string[] _)
    {
        foreach (var value in Set(code).Values)
        {
            Assert.False(string.IsNullOrWhiteSpace(value.DisplayName)); // en
            foreach (var language in Languages)
            {
                Assert.True(value.Attributes.TryGetValue("label_" + language, out var label) && !string.IsNullOrWhiteSpace(label),
                    $"{code}/{value.ValueCode}: label_{language}");
                Assert.NotEqual(value.ValueCode, label);
            }
        }
    }

    [Fact]
    public void The_turkish_labels_are_the_decision_record_s()
    {
        string Tr(string set, string value) => Set(set).Values.Single(v => v.ValueCode == value).Attributes["label_tr"];
        Assert.Equal("Savunucu", Tr("crm-adoption-stage", "advocate"));
        Assert.Equal("Yarı yarıya", Tr("crm-share-bucket", "half"));
        Assert.Equal("Erken benimseyen", Tr("crm-adoption-typology", "early_adopter"));
        Assert.Equal("Sonuç odaklı", Tr("crm-communication-style", "driver"));
        Assert.Equal("Uluslararası", Tr("crm-kol-level", "international"));
    }

    [Theory]
    [MemberData(nameof(Sets))]
    public void Each_set_passes_the_production_attribute_contract_a_draft_must_meet_before_publish(string code, string[] _)
    {
        var validate = typeof(BusinessReferenceDataValidationService)
            .GetMethod("ValidateAttributeContract", BindingFlags.NonPublic | BindingFlags.Static)!;
        var set = Set(code);
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

    [Theory]
    [MemberData(nameof(Sets))]
    public void Each_set_is_declared_once_required_by_the_development_load_and_consumable(string code, string[] _)
    {
        foreach (var file in Directory.EnumerateFiles(Path.Combine(ApiDirectory(), "Seed", "business-reference-data"), "*.json")
                     .Where(f => !f.EndsWith(CatalogFile, StringComparison.OrdinalIgnoreCase)))
        {
            using var json = JsonDocument.Parse(File.ReadAllText(file));
            if (json.RootElement.TryGetProperty("sets", out var sets))
            {
                Assert.DoesNotContain(sets.EnumerateArray().Select(s => s.GetProperty("set_code").GetString()), c => c == code);
            }
        }

        static JsonElement Read(string file) => JsonDocument.Parse(File.ReadAllText(Path.Combine(ApiDirectory(), file)),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }).RootElement;

        Assert.Contains(code, Read("appsettings.Development.json").GetProperty("BusinessReferenceData").GetProperty("CatalogLoad")
            .GetProperty("RequiredSetCodes").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains(code, Read("appsettings.json").GetProperty("BusinessReferenceData").GetProperty("ConsumableSets")
            .EnumerateArray().Select(x => x.GetString()));
        Assert.Contains(code, new BusinessReferenceDataConsumableSetsOptions().EffectiveConsumableSets());
    }

    [Fact]
    public void The_crm_manifest_declares_the_potential_thresholds_page_and_the_doctor_insights_keys()
    {
        var manifest = new CrmManifestProvider().GetManifest();
        var page = Assert.Single(manifest.Pages, p => p.PageCode == "CRM_DOCTOR_INSIGHT_THRESHOLDS");
        Assert.Equal("/CRM/PotentialThresholds", page.RoutePath);
        Assert.Equal("crm.doctor-insights.thresholds.manage", page.RequiredPermission);
        Assert.True(page.IsNavigationVisible);
        Assert.Equal(
            new[] { "crm.doctor-insights.read", "crm.doctor-insights.manage", "crm.doctor-insights.read-all", "crm.doctor-insights.read-aggregate" },
            page.Actions.OrderBy(a => a.SortOrder).Select(a => a.PermissionKey).ToArray());
        Assert.Equal("Potential Thresholds", page.DisplayName); // English

        // every doctor insights key is declared exactly once across the CRM manifest
        var keys = manifest.Pages.Select(p => p.RequiredPermission)
            .Concat(manifest.Pages.SelectMany(p => p.Actions).Select(a => a.PermissionKey))
            .Where(k => k is not null && k.StartsWith("crm.doctor-insights.", StringComparison.Ordinal))
            .ToList();
        Assert.Equal(5, keys.Count);
        Assert.Equal(5, keys.Distinct().Count());
    }
}
