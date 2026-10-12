using System.Reflection;
using System.Text.Json;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Application.Features.Crm.SelfRegistration;
using Diten.Platform.Domain.Entities;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

/// <summary>
/// WP-VW-W3 (W3-BE-b) — the doctor objection category set (<c>visit-objection-category</c>, in
/// <c>crm-visit-reference.json</c>) read through the SAME production code the startup catalog loader runs, the attribute
/// contract a draft must pass before publish, its development-load / consumable registration, and the objection library
/// keys on the CRM manifest (declared, not in the navigation until W3-WEB-c).
/// </summary>
public sealed class CrmVisitObjectionCategoryCatalogTests
{
    private const string CatalogFile = "crm-visit-reference.json";
    private const string SetCode = "visit-objection-category";
    private static readonly string[] Languages = ["tr", "fr", "es", "zh", "ar", "ru"];

    private static readonly string[] Codes =
    [
        "price", "reimbursement", "efficacy", "side_effect", "dosage", "access_stock", "habit_competitor",
        "insufficient_evidence", "other"
    ];

    private static string ApiDirectory() => Path.Combine(RepoPaths.Root(), "services", "Diten.Platform", "src", "Diten.Platform.API");

    private static T InvokeLoader<T>(string method, params object[] args)
    {
        var info = typeof(BusinessReferenceDataCatalogLoaderService)
            .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(nameof(BusinessReferenceDataCatalogLoaderService), method);
        return (T)info.Invoke(null, args)!;
    }

    private static BusinessReferenceDataCatalogSetDocument Set()
    {
        var payload = File.ReadAllText(Path.Combine(ApiDirectory(), "Seed", "business-reference-data", CatalogFile));
        typeof(BusinessReferenceDataCatalogLoaderService)
            .GetMethod("ValidateNoDuplicateJsonProperties", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [payload]);
        var document = InvokeLoader<BusinessReferenceDataCatalogDocument>("Parse", payload);
        Assert.Equal("BusinessReferenceData", document.Module);
        Assert.Equal(2, document.Sets.Count); // visit-outcome-reason + visit-objection-category
        var set = Assert.Single(document.Sets, s => s.SetCode == SetCode);
        return InvokeLoader<BusinessReferenceDataCatalogSetDocument>("NormalizeSet", set);
    }

    [Fact]
    public void The_set_parses_through_the_production_loader_with_nine_active_categories_in_order()
    {
        var set = Set();
        Assert.Equal("tenant", set.ScopeType);
        Assert.Equal("Active", set.Status);
        Assert.Equal(Codes, set.Values.Select(v => v.ValueCode));
        Assert.All(set.Values, v => Assert.True(v.IsActive));
        Assert.Equal(Enumerable.Range(1, 9).Select(i => i * 10), set.Values.Select(v => v.SortOrder));
    }

    [Fact]
    public void Every_category_carries_all_seven_languages()
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
        }

        var byCode = Set().Values.ToDictionary(v => v.ValueCode!);
        Assert.Equal(("Fiyat", "Price"), (byCode["price"].Attributes["label_tr"], byCode["price"].DisplayName));
        Assert.Equal("Yan etki", byCode["side_effect"].Attributes["label_tr"]);
        Assert.Equal("Alışkanlık / rakip ürün", byCode["habit_competitor"].Attributes["label_tr"]);
        Assert.Equal("Yetersiz kanıt", byCode["insufficient_evidence"].Attributes["label_tr"]);
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
                Attributes = v.Attributes.ToDictionary(x => x.Key, x => x.Value)
            }).ToList()
        };

        var issues = (List<string>)validate.Invoke(null, [version])!;
        Assert.True(issues.Count == 0, string.Join("; ", issues));
        version.Values[0].Attributes!.Remove("label_ru");
        Assert.Contains((List<string>)validate.Invoke(null, [version])!, i => i.Contains("missing required attribute"));
    }

    [Fact]
    public void The_set_is_declared_once_required_by_the_development_load_and_consumable()
    {
        foreach (var file in Directory.EnumerateFiles(Path.Combine(ApiDirectory(), "Seed", "business-reference-data"), "*.json")
                     .Where(f => !f.EndsWith(CatalogFile, StringComparison.OrdinalIgnoreCase)))
        {
            using var json = JsonDocument.Parse(File.ReadAllText(file));
            if (json.RootElement.TryGetProperty("sets", out var sets))
            {
                Assert.DoesNotContain(sets.EnumerateArray().Select(s => s.GetProperty("set_code").GetString()), c => c == SetCode);
            }
        }

        static JsonElement Read(string file) => JsonDocument.Parse(File.ReadAllText(Path.Combine(ApiDirectory(), file)),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }).RootElement;

        Assert.Contains(SetCode, Read("appsettings.Development.json").GetProperty("BusinessReferenceData").GetProperty("CatalogLoad")
            .GetProperty("RequiredSetCodes").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains(SetCode, Read("appsettings.json").GetProperty("BusinessReferenceData").GetProperty("ConsumableSets")
            .EnumerateArray().Select(x => x.GetString()));
        Assert.Contains(SetCode, new BusinessReferenceDataConsumableSetsOptions().EffectiveConsumableSets());
    }

    [Fact]
    public void The_crm_manifest_declares_the_objection_library_keys_outside_the_navigation()
    {
        var manifest = new CrmManifestProvider().GetManifest();
        var page = Assert.Single(manifest.Pages, p => p.PageCode == "OBJECTION_RESPONSES");
        Assert.Equal("crm.objection-response.read", page.RequiredPermission);
        Assert.False(page.IsNavigationVisible); // W3-WEB-c brings the screen (mockup decision first)
        Assert.Equal(
            new[] { "crm.objection-response.manage", "crm.objection-response.approve" },
            page.Actions.OrderBy(a => a.SortOrder).Select(a => a.PermissionKey).ToArray());
        Assert.Equal("Objection Responses", page.DisplayName); // English
    }
}
