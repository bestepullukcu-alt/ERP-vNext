using System.Reflection;
using System.Text.Json;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Application.Features.Crm.SelfRegistration;
using Diten.Platform.Domain.Entities;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

/// <summary>
/// WP-VW-W4 (W4-BE-a, §2.1) — the ten visit report v2 sets (in <c>crm-visit-report-v2-reference.json</c>, after BE-c's five
/// profile sets) through the SAME production loader code, the attribute contract a draft must pass before publish, their
/// development-load / consumable registration, and the new competitor usage key on the CRM manifest.
/// </summary>
public sealed class CrmVisitReportV2CatalogTests
{
    private const string CatalogFile = "crm-visit-report-v2-reference.json";
    private static readonly string[] Languages = ["tr", "fr", "es", "zh", "ar", "ru"];

    public static readonly TheoryData<string, string[]> Sets = new()
    {
        { "crm-visit-success", ["reached", "partial", "not_reached"] },
        { "crm-doctor-commitment", ["will_try", "will_prescribe", "wants_info", "none"] },
        { "crm-visit-interest", ["interested", "neutral", "not_interested"] },
        { "crm-visit-request-type", ["literature", "sample", "patient_material", "training_invite", "msl_meeting", "other"] },
        { "crm-competitor-advantage", ["efficacy", "safety", "dosing", "price", "reimbursement", "access", "evidence", "brand"] },
        { "crm-pharmacy-met-role", ["pharmacist", "technician", "owner", "other"] },
        { "crm-pharmacy-stock-level", ["none", "low", "ok", "high"] },
        { "crm-shelf-position", ["eye_level", "upper", "lower", "behind_counter", "not_visible"] },
        { "crm-sales-trend", ["up", "flat", "down"] },
        { "crm-recommendation-level", ["high", "medium", "low", "none"] }
    };

    private static string ApiDirectory() => Path.Combine(RepoPaths.Root(), "services", "Diten.Platform", "src", "Diten.Platform.API");

    private static T InvokeLoader<T>(string method, params object[] args)
    {
        var info = typeof(BusinessReferenceDataCatalogLoaderService)
            .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(nameof(BusinessReferenceDataCatalogLoaderService), method);
        return (T)info.Invoke(null, args)!;
    }

    private static BusinessReferenceDataCatalogDocument Document()
    {
        var payload = File.ReadAllText(Path.Combine(ApiDirectory(), "Seed", "business-reference-data", CatalogFile));
        typeof(BusinessReferenceDataCatalogLoaderService)
            .GetMethod("ValidateNoDuplicateJsonProperties", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [payload]);
        return InvokeLoader<BusinessReferenceDataCatalogDocument>("Parse", payload);
    }

    private static BusinessReferenceDataCatalogSetDocument Set(string code)
        => InvokeLoader<BusinessReferenceDataCatalogSetDocument>("NormalizeSet", Assert.Single(Document().Sets, s => s.SetCode == code));

    [Fact]
    public void The_file_holds_the_five_profile_sets_and_the_ten_report_sets()
        => Assert.Equal(15, Document().Sets.Count);

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
            Assert.False(string.IsNullOrWhiteSpace(value.DisplayName));
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
        Assert.Equal("Ulaşılamadı", Tr("crm-visit-success", "not_reached"));
        Assert.Equal("Hastasına yazacak", Tr("crm-doctor-commitment", "will_prescribe"));
        Assert.Equal("İlgisiz", Tr("crm-visit-interest", "not_interested"));
        Assert.Equal("MSL görüşmesi", Tr("crm-visit-request-type", "msl_meeting"));
        Assert.Equal("Doz kolaylığı", Tr("crm-competitor-advantage", "dosing"));
        Assert.Equal("Eczane teknisyeni", Tr("crm-pharmacy-met-role", "technician"));
        Assert.Equal("Yeterli", Tr("crm-pharmacy-stock-level", "ok"));
        Assert.Equal("Tezgâh arkası", Tr("crm-shelf-position", "behind_counter"));
        Assert.Equal("Azalıyor", Tr("crm-sales-trend", "down"));
        Assert.Equal("Yok", Tr("crm-recommendation-level", "none"));
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
    public void The_crm_manifest_declares_the_report_usage_key_once()
    {
        var manifest = new CrmManifestProvider().GetManifest();
        var keys = manifest.Pages.Select(p => p.RequiredPermission)
            .Concat(manifest.Pages.SelectMany(p => p.Actions).Select(a => a.PermissionKey))
            .Where(k => k == "crm.visit-report.read-aggregate")
            .ToList();
        Assert.Single(keys);
        var page = Assert.Single(manifest.Pages, p => p.Actions.Any(a => a.PermissionKey == "crm.visit-report.read-aggregate"));
        Assert.Equal("VISIT_EXECUTION", page.PageCode);
    }
}
