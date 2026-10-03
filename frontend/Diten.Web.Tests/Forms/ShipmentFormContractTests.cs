using System.Xml.Linq;
using Xunit;

namespace Diten.Web.Tests.Forms;

public sealed class ShipmentFormContractTests
{
    [Fact]
    public void CreateSurface_ContainsExactlyThirteenUserInputKindsAndNoScopeFields()
    {
        var form = Read("frontend/Diten.Web/Views/SupplyChain/Shipments/_Form.cshtml");
        foreach (var id in new[] { "sourceModule", "sourceType", "sourceDocumentId", "warehouseReferenceId", "shipToReference", "plannedShipAt", "plannedDeliverAt" })
            Assert.Equal(1, Count(form, $"id=\"{id}\""));
        foreach (var field in new[] { "lineNumber", "itemId", "skuId", "quantity", "uomId", "inventoryReferenceId" })
            Assert.Equal(1, Count(form, $"data-field=\"{field}\""));
        Assert.DoesNotContain("TenantId", form); Assert.DoesNotContain("LegalEntityId", form); Assert.DoesNotContain("Status\"", form);
    }

    [Fact]
    public void RepeatableLines_HaveUniqueLabelBindingsAndDeterministicFocusRecovery()
    {
        var form = Read("frontend/Diten.Web/Views/SupplyChain/Shipments/_Form.cshtml");
        var script = Read("frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Shipments/create.js");
        foreach (var field in new[] { "lineNumber", "itemId", "skuId", "quantity", "uomId", "inventoryReferenceId" })
            Assert.Equal(1, Count(form, $"data-field-label=\"{field}\""));
        Assert.Contains("const syncLineAccessibility", script);
        Assert.Contains("input.id = `shipmentLine_${index}_${field}`", script);
        Assert.Contains("label.htmlFor = input.id", script);
        Assert.Contains("focusTarget?.focus()", script);
        Assert.DoesNotContain("input.name =", script);
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
