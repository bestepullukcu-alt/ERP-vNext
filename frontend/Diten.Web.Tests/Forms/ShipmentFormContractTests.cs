using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Models.SupplyChain.Shipments;
using Xunit;

namespace Diten.Web.Tests.Forms;

public sealed class ShipmentFormContractTests
{
    // MOD-0183 r9 §24.2 SB-5: the Golden Compact field set — 5 header + 5 per line = 10 (form_field_count: 10). sourceModule,
    // sourceType, shipToReference, lineNumber, uomId and inventoryReferenceId are not user inputs; the company is not a field.
    private static readonly string[] HeaderFields = ["sourceDocumentId", "warehouseReferenceId", "customerRef", "plannedShipAt", "plannedDeliverAt"];
    private static readonly string[] LineFields = ["sku", "lotId", "quantity", "fromLocationId", "fefoBypassReason"];

    [Fact]
    public void CreateSurface_IsTheGoldenCompactFieldSet_TenFields_AndNoScopeOrAdapterFields()
    {
        // Razor comments (@* … *@) name the fields that are gone; only the markup is measured.
        var form = Regex.Replace(Read("frontend/Diten.Web/Views/SupplyChain/Shipments/_Form.cshtml"), @"@\*.*?\*@", string.Empty, RegexOptions.Singleline);
        foreach (var id in HeaderFields) Assert.Equal(1, Count(form, $"id=\"{id}\""));
        foreach (var field in LineFields) Assert.Equal(1, Count(form, $"data-field=\"{field}\""));
        var controls = Regex.Matches(form, "<(input|select|textarea)\\b(?![^>]*type=\"hidden\")", RegexOptions.IgnoreCase).Count;
        Assert.Equal(HeaderFields.Length + LineFields.Length, controls);
        foreach (var gone in new[] { "sourceModule", "sourceType", "shipToReference", "inventoryReferenceId", "formLegalEntity", "data-field=\"lineNumber\"", "data-field=\"uomId\"", "data-field=\"skuLevel\"" })
            Assert.DoesNotContain(gone, form);
        Assert.DoesNotContain("TenantId", form); Assert.DoesNotContain("LegalEntityId", form); Assert.DoesNotContain("Status\"", form);
    }

    [Fact]
    public void RepeatableLines_HaveUniqueLabelBindingsAndDeterministicFocusRecovery()
    {
        var form = Read("frontend/Diten.Web/Views/SupplyChain/Shipments/_Form.cshtml");
        var script = Read("frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Shipments/create.js");
        foreach (var field in LineFields)
            Assert.Equal(1, Count(form, $"data-field-label=\"{field}\""));
        Assert.Contains("const syncLines", script);
        Assert.Contains("input.id = `shipmentLine_${index}_${field}`", script);
        Assert.Contains("fieldLabel.htmlFor = input.id", script);
        Assert.Contains("focusTarget?.focus()", script);
        // SB-6: the visible line number is the position, renumbered after a removal; the adapter numbers the wire lines.
        Assert.Contains("line.querySelector('[data-line-number]').textContent = String(index + 1)", script);
        Assert.DoesNotContain("input.name =", script);
    }

    [Fact]
    public void ALineCarriesTheStockFieldsDispatchReads()
    {
        var script = Read("frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Shipments/create.js");
        var model = Read("frontend/Diten.Web/Models/SupplyChain/Shipments/ShipmentViewModels.cs");
        foreach (var property in new[] { "SkuLevel", "LotId", "FromLocationId", "FefoBypassReason" })
            Assert.Contains($"public string? {property} {{ get; set; }}", model);
        Assert.Contains("line.itemId && line.skuId && line.skuLevel && line.uomId", script);
        Assert.Contains("if (lot) body.lotId = lot;", script);
    }

    // SB-7: changing warehouse clears every From location; changing SKU clears Lot and UoM; the layer never asks isStockable.
    [Fact]
    public void Pickers_ClearDependentFields_AndTheTreeNeverFiltersOnIsStockable()
    {
        var script = Read("frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Shipments/create.js");
        Assert.Contains("jq?.(select).on('change', () => document.querySelectorAll('.shipment-line').forEach(resetLocation));", script);
        Assert.Contains("clearSelect(lot);", script);
        Assert.Contains("line.querySelector('[data-uom]').textContent = chosen?.uomId || '—';", script);
        Assert.DoesNotContain("isStockable=", script);
    }

    // SB-8: the FEFO reason is hidden until a FEFO refusal names the line.
    [Fact]
    public void FefoReason_IsHiddenUntilAFefoRefusal()
    {
        var form = Read("frontend/Diten.Web/Views/SupplyChain/Shipments/_Form.cshtml");
        var script = Read("frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Shipments/create.js");
        Assert.Contains("class=\"col-12 col-md-6 d-none\" data-fefo", form);
        Assert.Contains("const fefoCodes = ['FEFO_BYPASS_REASON_REQUIRED', 'FEFO_ORDER_VIOLATION'];", script);
        Assert.Contains("if (code === 'INVENTORY_REFUSED' && fefoCodes.includes(details.inventoryCode)) revealFefo(details.lineNumber);", script);
    }

    // SB-3: without crm.account.read the customer field is disabled and says why.
    [Fact]
    public void CustomerField_IsGatedOnCrmAccountRead()
    {
        var form = Read("frontend/Diten.Web/Views/SupplyChain/Shipments/_Form.cshtml");
        Assert.Contains("var canReadCustomers = Perms.Has(\"crm.account.read\");", form);
        Assert.Contains("disabled=\"@(!canReadCustomers)\"", form);
        Assert.Contains("Localizer[\"CustomerNoAccess\"]", form);
    }

    // VER F-3 (2026-11-03): every SKU level label shown on the details lines comes from the resource, never the wire value.
    [Theory]
    [InlineData("Gsku")]
    [InlineData("Lsku")]
    [InlineData("FinishedGood")]
    [InlineData("Item")]
    public void EverySkuLevelLabelIsLocalized(string level)
    {
        var l10n = Read("frontend/Diten.Web/Views/SupplyChain/Shipments/_IndexL10n.cshtml");
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

    // SB-26: the r9 texts have a non-empty value in all seven languages, and a non-English language does not copy English.
    [Theory]
    [InlineData("OldRecord")]
    [InlineData("NameNotFound")]
    [InlineData("NameUnavailable")]
    [InlineData("ErrInvalidCustomerAccount")]
    [InlineData("ErrCustomerAddressMissing")]
    [InlineData("Customer")]
    [InlineData("CustomerAddress")]
    public void R9Texts_HaveAValueInSevenLanguages(string key)
    {
        var directory = Path.Combine(Root(), "frontend/Diten.Web/Resources/Views/SupplyChain/Shipments");
        string? english = null;
        foreach (var language in new[] { "en", "tr", "fr", "es", "zh", "ar", "ru" })
        {
            var value = XDocument.Load(Path.Combine(directory, $"ShipmentsIndex.{language}.resx")).Root!.Elements("data")
                .SingleOrDefault(element => element.Attribute("name")!.Value == key)?.Element("value")?.Value;
            Assert.False(string.IsNullOrWhiteSpace(value), $"{language}: {key}");
            if (language == "en") english = value; else Assert.NotEqual(english, value);
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
