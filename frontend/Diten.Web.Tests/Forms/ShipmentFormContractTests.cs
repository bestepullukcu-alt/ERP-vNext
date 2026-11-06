using System.Text.Json;
using System.Xml.Linq;
using Diten.Web.Models.SupplyChain.Shipments;
using Xunit;

namespace Diten.Web.Tests.Forms;

public sealed class ShipmentFormContractTests
{
    [Fact]
    public void CreateSurface_ContainsExactlySixteenUserInputKindsAndNoScopeFields()
    {
        var form = Read("frontend/Diten.Web/Views/SupplyChain/Shipments/_Form.cshtml");
        foreach (var id in new[] { "sourceModule", "sourceType", "sourceDocumentId", "warehouseReferenceId", "shipToReference", "plannedShipAt", "plannedDeliverAt" })
            Assert.Equal(1, Count(form, $"id=\"{id}\""));
        foreach (var field in new[] { "lineNumber", "itemId", "skuId", "quantity", "uomId", "inventoryReferenceId", "skuLevel", "fromLocationId", "lotId" })
            Assert.Equal(1, Count(form, $"data-field=\"{field}\""));
        Assert.DoesNotContain("TenantId", form); Assert.DoesNotContain("LegalEntityId", form); Assert.DoesNotContain("Status\"", form);
    }

    [Fact]
    public void RepeatableLines_HaveUniqueLabelBindingsAndDeterministicFocusRecovery()
    {
        var form = Read("frontend/Diten.Web/Views/SupplyChain/Shipments/_Form.cshtml");
        var script = Read("frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Shipments/create.js");
        foreach (var field in new[] { "lineNumber", "itemId", "skuId", "quantity", "uomId", "inventoryReferenceId", "skuLevel", "fromLocationId", "lotId" })
            Assert.Equal(1, Count(form, $"data-field-label=\"{field}\""));
        Assert.Contains("const syncLineAccessibility", script);
        Assert.Contains("input.id = `shipmentLine_${index}_${field}`", script);
        Assert.Contains("label.htmlFor = input.id", script);
        Assert.Contains("focusTarget?.focus()", script);
        Assert.DoesNotContain("input.name =", script);
    }

    [Fact]
    public void ALineCarriesTheStockFieldsDispatchReads()
    {
        var script = Read("frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Shipments/create.js");
        var model = Read("frontend/Diten.Web/Models/SupplyChain/Shipments/ShipmentViewModels.cs");
        foreach (var property in new[] { "SkuLevel", "LotId", "FromLocationId" })
            Assert.Contains($"public string? {property} {{ get; set; }}", model);
        Assert.Contains("&& line.skuLevel && line.fromLocationId.trim()", script);
        Assert.Contains("const optionalStockFields = ['lotId']", script);
    }

    // VER F-3 (2026-11-03): every SKU level label on the form and the details lines comes from the resource, never the wire value.
    [Theory]
    [InlineData("Gsku")]
    [InlineData("Lsku")]
    [InlineData("FinishedGood")]
    [InlineData("Item")]
    public void EverySkuLevelLabelIsLocalized(string level)
    {
        var form = Read("frontend/Diten.Web/Views/SupplyChain/Shipments/_Form.cshtml");
        var l10n = Read("frontend/Diten.Web/Views/SupplyChain/Shipments/_IndexL10n.cshtml");
        Assert.Contains($"<option value=\"{level}\">@Localizer[\"SkuLevel{level}\"]</option>", form);
        Assert.Contains($"[\"{level}\"] = Localizer[\"SkuLevel{level}\"].Value", l10n);
        var directory = Path.Combine(Root(), "frontend/Diten.Web/Resources/Views/SupplyChain/Shipments");
        foreach (var language in new[] { "en", "tr", "fr", "es", "zh", "ar", "ru" })
        {
            var value = XDocument.Load(Path.Combine(directory, $"ShipmentsIndex.{language}.resx")).Root!.Elements("data")
                .SingleOrDefault(element => element.Attribute("name")!.Value == $"SkuLevel{level}")?.Element("value")?.Value;
            Assert.False(string.IsNullOrWhiteSpace(value), $"{language}: SkuLevel{level}");
            Assert.NotEqual(level.ToUpperInvariant(), value);
        }
    }

    [Fact]
    public void TheWebModelForwardsTheStockFieldsToTheGateway()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var line = JsonSerializer.Deserialize<CreateShipmentLineViewModel>("""{"skuLevel":"Lsku","lotId":"lot-1","fromLocationId":"loc-1"}""", options)!;
        var forwarded = JsonSerializer.SerializeToNode(line, options)!;

        Assert.Equal(("Lsku", "lot-1", "loc-1"), (forwarded["skuLevel"]!.GetValue<string>(), forwarded["lotId"]!.GetValue<string>(), forwarded["fromLocationId"]!.GetValue<string>()));
    }

    [Fact]
    public void UasGatePrecedesEveryProtectedPageSurface()
    {
        foreach (var file in new[] { "Index.cshtml", "Create.cshtml", "Details.cshtml" })
        {
            var text = Read($"frontend/Diten.Web/Views/SupplyChain/Shipments/{file}");
            Assert.True(text.IndexOf("<partial name=\"_AccessDenied\"", StringComparison.Ordinal) >= 0);
            Assert.True(text.IndexOf("return;", StringComparison.Ordinal) > text.IndexOf("_AccessDenied", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void SevenResourcesHaveIdenticalNonEmptyKeys()
    {
        var directory = Path.Combine(Root(), "frontend/Diten.Web/Resources/Views/SupplyChain/Shipments");
        var dictionaries = new[] { "en", "tr", "fr", "es", "zh", "ar", "ru" }.Select(language =>
            XDocument.Load(Path.Combine(directory, $"ShipmentsIndex.{language}.resx")).Root!.Elements("data")
                .ToDictionary(element => element.Attribute("name")!.Value, element => element.Element("value")?.Value ?? "")).ToArray();
        Assert.All(dictionaries, dictionary => Assert.All(dictionary.Values, value => Assert.False(string.IsNullOrWhiteSpace(value))));
        Assert.All(dictionaries.Skip(1), dictionary => Assert.Equal(dictionaries[0].Keys.Order(), dictionary.Keys.Order()));
    }

    [Fact]
    public void Index_UsesExplicitNestedPartialPaths()
    {
        var index = Read("frontend/Diten.Web/Views/SupplyChain/Shipments/Index.cshtml");
        foreach (var partial in new[] { "_Filter.cshtml", "_DataTable.cshtml", "_IndexL10n.cshtml" })
            Assert.Contains($"~/Views/SupplyChain/Shipments/{partial}", index);
    }

    private static string Read(string path) => File.ReadAllText(Path.Combine(Root(), path));
    private static int Count(string text, string token) => text.Split(token, StringSplitOptions.None).Length - 1;
    private static string Root() { var dir = new DirectoryInfo(AppContext.BaseDirectory); while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent; return dir?.FullName ?? throw new DirectoryNotFoundException(); }
}
