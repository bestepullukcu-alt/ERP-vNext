using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Diten.Web.Tests.JavaScript;

/*
 * MOD-0183 DL-2 (pack r7 §23.3, S23-7): every closed-list inventoryCode, the module codes INVENTORY_REFUSED,
 * SHIPMENT_LINE_STOCK_INCOMPLETE, DISPATCH_IN_PROGRESS, DISPATCH_COMPENSATION_FAILED, DEPENDENCY_UNAVAILABLE and the
 * dispatchState labels InProgress / CompensationFailed have a non-empty text in all 7 languages of
 * ShipmentsIndex.{lang}.resx. This class proves the chain view payload → resx key → 7 texts; the Node file proves
 * details.js picks the right payload text (and never the validation text) and runs here, because CI runs only
 * `dotnet test` for the frontend.
 */
public sealed class ShipmentDispatchTextTests
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    // Pack §23.3 closed list, written out by hand from the pack — not read from the code under test.
    private static readonly string[] ClosedList =
    [
        "FEFO_BYPASS_REASON_REQUIRED", "FEFO_ORDER_VIOLATION", "INSUFFICIENT_AVAILABLE", "INSUFFICIENT_STOCK", "ITEM_NOT_ACTIVE",
        "LOT_BELOW_MIN_REMAINING_SHELF_LIFE", "LOT_BLOCKED", "LOT_EXPIRED", "LOT_NOT_ALLOWED", "LOT_REQUIRED", "LOT_RETEST_DUE",
        "LOT_SKU_MISMATCH", "LOT_UNDER_RECALL", "PRODUCT_MASTER_DATA_INCOMPLETE", "RESERVATION_CONFLICT", "SERIAL_ALREADY_EXISTS",
        "SERIAL_COUNT_MISMATCH", "SERIAL_LOT_MISMATCH", "SERIAL_NOT_AVAILABLE", "SKU_ITEM_MISMATCH", "SKU_LEVEL_NOT_STOCKABLE",
        "UNKNOWN_ITEM", "UNKNOWN_LOCATION", "UNKNOWN_LOT", "UOM_CONVERSION_MISSING"
    ];

    // The payload properties details.js reads for the module codes; each is filled from a resx key in _IndexL10n.cshtml.
    private static readonly string[] ModuleTextProperties =
        ["inventoryRefused", "lineStockIncomplete", "dispatchInProgress", "dispatchCompensationFailed", "dependencyUnavailable", "inventoryCodeUnknown"];
    private static readonly string[] LineTemplates = ["inventoryRefused", "lineStockIncomplete", "inventoryCodeUnknown"];

    private readonly string _view = Read("frontend/Diten.Web/Views/SupplyChain/Shipments/_IndexL10n.cshtml");
    private readonly string _details = Read("frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Shipments/details.js");

    [Fact]
    public void TheViewMapsEveryClosedListCodeToAKeyWithATextInAllSevenLanguages()
    {
        var map = Dictionary("inventoryCodes");
        Assert.Equal(ClosedList.OrderBy(c => c, StringComparer.Ordinal), map.Keys.OrderBy(c => c, StringComparer.Ordinal));
        Assert.Equal(map.Count, map.Values.Distinct().Count());
        foreach (var (code, key) in map) AssertTextInAllLanguages(key, code);
    }

    // Review F-1: each code must reach ITS key — a swap of two keys keeps every count and text check green.
    // Expected keys are derived here (closed list: "InventoryRefused" + PascalCase of the code) or written out by hand.
    [Fact]
    public void EachCodeAndStateIsMappedToItsOwnKey()
    {
        var map = Dictionary("inventoryCodes");
        foreach (var code in ClosedList)
        {
            var expected = "InventoryRefused" + string.Concat(code.Split('_').Select(w => w[..1] + w[1..].ToLowerInvariant()));
            Assert.Equal(expected, map[code]);
        }
        var properties = new Dictionary<string, string>
        {
            ["inventoryRefused"] = "ErrInventoryRefused", ["lineStockIncomplete"] = "ErrShipmentLineStockIncomplete",
            ["dispatchInProgress"] = "ErrDispatchInProgress", ["dispatchCompensationFailed"] = "ErrDispatchCompensationFailed",
            ["dependencyUnavailable"] = "ErrDependencyUnavailable", ["inventoryCodeUnknown"] = "InventoryRefusedUnknown",
            ["accessDenied"] = "ErrForbidden", ["validationError"] = "ErrInvalidRequest"
        };
        foreach (var (property, key) in properties) Assert.Equal(key, Property(property));
        var states = Dictionary("dispatchStates");
        Assert.Equal("DispatchStateInProgress", states["InProgress"]);
        Assert.Equal("DispatchStateCompensationFailed", states["CompensationFailed"]);
    }

    [Fact]
    public void TheModuleCodeTextsAndTheDispatchStateLabelsHaveATextInAllSevenLanguages()
    {
        foreach (var property in ModuleTextProperties)
        {
            Assert.Contains($"L.{property}", _details);
            AssertTextInAllLanguages(Property(property), property);
        }
        foreach (var template in LineTemplates)
            foreach (var language in Languages) Assert.Contains("{0}", Resx(language)[Property(template)]);
        var states = Dictionary("dispatchStates");
        Assert.Equal(["CompensationFailed", "InProgress"], states.Keys.OrderBy(k => k, StringComparer.Ordinal));
        foreach (var (state, key) in states) AssertTextInAllLanguages(key, state);
        // The view keeps the existing permission text for an INVENTORY 403.
        Assert.Equal("ErrForbidden", Property("accessDenied"));
    }

    [Fact]
    public void TheSevenShipmentResxFilesHoldTheSameKeys()
    {
        var english = Resx("en");
        foreach (var language in Languages)
        {
            var keys = Resx(language).Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
            Assert.True(english.Keys.OrderBy(k => k, StringComparer.Ordinal).SequenceEqual(keys), $"{language}: key set differs from en");
        }
    }

    [Fact]
    public async Task DetailsJsMapsEveryCodeToItsOwnTextUnderNode()
    {
        var start = new ProcessStartInfo("node") { WorkingDirectory = Root(), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("--test");
        start.ArgumentList.Add("--test-reporter=tap");
        start.ArgumentList.Add(Path.Combine("frontend", "Diten.Web.Tests", "JavaScript", "Node", "shipment-details-error-text.test.mjs"));
        using var node = Process.Start(start)!;
        var error = node.StandardError.ReadToEndAsync();
        var output = await node.StandardOutput.ReadToEndAsync();
        await node.WaitForExitAsync();
        var report = $"{output}\n{await error}";
        Assert.True(node.ExitCode == 0, report);
        // A run that found no tests also exits 0; require the five cases the file defines.
        Assert.Matches(new Regex(@"^# pass 5$", RegexOptions.Multiline), output);
        Assert.Matches(new Regex(@"^# fail 0$", RegexOptions.Multiline), output);
    }

    private void AssertTextInAllLanguages(string key, string label)
    {
        var english = Resx("en").GetValueOrDefault(key);
        Assert.False(string.IsNullOrWhiteSpace(english), $"{label}: en has no text for {key}");
        foreach (var language in Languages.Skip(1))
        {
            var value = Resx(language).GetValueOrDefault(key);
            Assert.False(string.IsNullOrWhiteSpace(value), $"{label}: {language} has no text for {key}");
            Assert.NotEqual(english, value);
        }
    }

    private string Property(string name)
    {
        var match = Regex.Match(_view, $@"\b{name} = Localizer\[""(?<key>[A-Za-z0-9]+)""\]\.Value");
        Assert.True(match.Success, $"_IndexL10n.cshtml does not fill {name} from a Localizer key");
        return match.Groups["key"].Value;
    }

    private Dictionary<string, string> Dictionary(string name)
    {
        var block = Regex.Match(_view, $@"\b{name} = new Dictionary<string, string> \{{(?<body>[^}}]*)\}}");
        Assert.True(block.Success, $"_IndexL10n.cshtml has no {name} dictionary");
        return Regex.Matches(block.Groups["body"].Value, @"\[""(?<code>[^""]+)""\] = Localizer\[""(?<key>[A-Za-z0-9]+)""\]\.Value")
            .ToDictionary(m => m.Groups["code"].Value, m => m.Groups["key"].Value);
    }

    private static readonly Dictionary<string, Dictionary<string, string>> ResxCache = new();
    private static Dictionary<string, string> Resx(string language)
    {
        lock (ResxCache)
        {
            if (ResxCache.TryGetValue(language, out var cached)) return cached;
            var path = Path.Combine(Root(), "frontend/Diten.Web/Resources/Views/SupplyChain/Shipments", $"ShipmentsIndex.{language}.resx");
            var values = XDocument.Load(path).Root!.Elements("data")
                .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty);
            return ResxCache[language] = values;
        }
    }

    private static string Read(string path) => File.ReadAllText(Path.Combine(Root(), path));
    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root (AGENTS.md) not found.");
    }
}
