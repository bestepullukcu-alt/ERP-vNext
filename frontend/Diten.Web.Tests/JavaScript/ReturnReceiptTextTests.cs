using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Diten.Web.Tests.JavaScript;

/*
 * MOD-0186 DL-2 (Q504, pack r8 §34.3, R34-3): every closed-list inventoryCode, the receipt codes and the line-state labels
 * have a non-empty text in all 7 languages of ReturnsIndex.{lang}.resx. This class proves the chain index.js map → resx key
 * → 7 texts; the Node file proves index.js picks the right text (and never the validation text) and runs here, because CI
 * runs only `dotnet test` for the frontend.
 */
public sealed class ReturnReceiptTextTests
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    // Pack MOD-0183 §23.3 closed list, written out by hand from the pack — not read from the code under test.
    private static readonly string[] ClosedList =
    [
        "FEFO_BYPASS_REASON_REQUIRED", "FEFO_ORDER_VIOLATION", "INSUFFICIENT_AVAILABLE", "INSUFFICIENT_STOCK", "ITEM_NOT_ACTIVE",
        "LOT_BELOW_MIN_REMAINING_SHELF_LIFE", "LOT_BLOCKED", "LOT_EXPIRED", "LOT_NOT_ALLOWED", "LOT_REQUIRED", "LOT_RETEST_DUE",
        "LOT_SKU_MISMATCH", "LOT_UNDER_RECALL", "PRODUCT_MASTER_DATA_INCOMPLETE", "RESERVATION_CONFLICT", "SERIAL_ALREADY_EXISTS",
        "SERIAL_COUNT_MISMATCH", "SERIAL_LOT_MISMATCH", "SERIAL_NOT_AVAILABLE", "SKU_ITEM_MISMATCH", "SKU_LEVEL_NOT_STOCKABLE",
        "UNKNOWN_ITEM", "UNKNOWN_LOCATION", "UNKNOWN_LOT", "UOM_CONVERSION_MISSING"
    ];

    private static readonly string[] ReceiptCodes =
        ["INVENTORY_REFUSED", "RETURN_SOURCE_MOVEMENT_MISSING", "RETURN_RECEIPT_IN_PROGRESS", "RETURN_LINE_NOT_UNPOSTABLE"];

    // Keys index.js and Index.cshtml read directly; {0} = the template takes a value.
    private static readonly string[] DirectKeys =
    [
        "InventoryRefusedUnknown", "ReceiptPartialRetryHint", "ReceiptStoredValues", "ReceiptLinePosted", "ReceiptLineNotPosted",
        "ReceiptPostingHeader", "ReceiptReasonHeader", "ReceiptReasonPreWpP1", "ReceiptReasonIntegrityFault",
        "ReceivedPostsQuarantineNote", "ToWarehouseId", "ToLocationId", "ActionReceiveWithoutPosting", "ConfirmReceiveWithoutPosting"
    ];
    private static readonly string[] OneValueTemplates =
    [
        "ErrInventoryRefused", "ErrReturnSourceMovementMissing", "ErrReturnLineNotUnpostable", "InventoryRefusedUnknown", "ConfirmReceiveWithoutPosting"
    ];

    private readonly string _script = Read("frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Returns/index.js");
    private readonly string _bridge = Read("frontend/Diten.Web/Views/SupplyChain/Returns/_IndexL10n.cshtml");

    [Fact]
    public void TheScriptMapsEveryClosedListCodeToAKeyWithATextInAllSevenLanguages()
    {
        var map = ScriptMap("INVENTORY_CODE_KEY");
        Assert.Equal(ClosedList.OrderBy(c => c, StringComparer.Ordinal), map.Keys.OrderBy(c => c, StringComparer.Ordinal));
        Assert.Equal(map.Count, map.Values.Distinct().Count());
        foreach (var (code, key) in map) AssertBridgedTextInAllLanguages(key, code);
    }

    [Fact]
    public void TheReceiptCodesAndLineStateTextsHaveATextInAllSevenLanguages()
    {
        var errors = ScriptMap("ERROR_KEY");
        foreach (var code in ReceiptCodes) AssertBridgedTextInAllLanguages(errors[code], code);
        foreach (var (state, key) in ScriptMap("POSTING_KEY")) AssertBridgedTextInAllLanguages(key, state);
        foreach (var (reason, key) in ScriptMap("POSTING_REASON_KEY")) AssertBridgedTextInAllLanguages(key, reason);
        foreach (var key in DirectKeys) AssertBridgedTextInAllLanguages(key, key);
        foreach (var language in Languages)
        {
            foreach (var key in OneValueTemplates) Assert.Contains("{0}", Resx(language)[key]);
            foreach (var slot in new[] { "{0}", "{1}", "{2}" }) Assert.Contains(slot, Resx(language)["ReceiptStoredValues"]);
        }
    }

    [Fact]
    public void TheSevenReturnResxFilesHoldTheSameKeysWithNoEmptyValue()
    {
        var english = Resx("en").Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
        foreach (var language in Languages)
        {
            var values = Resx(language);
            Assert.True(english.SequenceEqual(values.Keys.OrderBy(k => k, StringComparer.Ordinal)), $"{language}: key set differs from en");
            Assert.All(values, pair => Assert.False(string.IsNullOrWhiteSpace(pair.Value), $"{language}: {pair.Key} is empty"));
        }
    }

    [Fact]
    public async Task IndexJsMapsEveryCodeToItsOwnTextUnderNode()
    {
        var start = new ProcessStartInfo("node") { WorkingDirectory = Root(), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("--test");
        start.ArgumentList.Add("--test-reporter=tap");
        start.ArgumentList.Add(Path.Combine("frontend", "Diten.Web.Tests", "JavaScript", "Node", "return-receipt-text.test.mjs"));
        using var node = Process.Start(start)!;
        var error = node.StandardError.ReadToEndAsync();
        var output = await node.StandardOutput.ReadToEndAsync();
        await node.WaitForExitAsync();
        var report = $"{output}\n{await error}";
        Assert.True(node.ExitCode == 0, report);
        // A run that found no tests also exits 0; require the eight cases the file defines.
        Assert.Matches(new Regex(@"^# pass 8$", RegexOptions.Multiline), output);
        Assert.Matches(new Regex(@"^# fail 0$", RegexOptions.Multiline), output);
    }

    private void AssertBridgedTextInAllLanguages(string key, string label)
    {
        Assert.Contains($"[\"{key}\"] = Localizer[\"{key}\"].Value", _bridge);
        var english = Resx("en").GetValueOrDefault(key);
        Assert.False(string.IsNullOrWhiteSpace(english), $"{label}: en has no text for {key}");
        foreach (var language in Languages.Skip(1))
        {
            var value = Resx(language).GetValueOrDefault(key);
            Assert.False(string.IsNullOrWhiteSpace(value), $"{label}: {language} has no text for {key}");
            Assert.NotEqual(english, value);
        }
    }

    private Dictionary<string, string> ScriptMap(string name)
    {
        var block = Regex.Match(_script, $@"const {name} = Object\.freeze\(\{{(?<body>.*?)\}}\);", RegexOptions.Singleline);
        Assert.True(block.Success, $"index.js has no {name} map");
        return Regex.Matches(block.Groups["body"].Value, @"(?<code>[A-Za-z0-9_]+): '(?<key>[A-Za-z0-9]+)'")
            .ToDictionary(m => m.Groups["code"].Value, m => m.Groups["key"].Value);
    }

    private static readonly Dictionary<string, Dictionary<string, string>> ResxCache = new();
    private static Dictionary<string, string> Resx(string language)
    {
        lock (ResxCache)
        {
            if (ResxCache.TryGetValue(language, out var cached)) return cached;
            var path = Path.Combine(Root(), "frontend/Diten.Web/Resources/Views/SupplyChain/Returns", $"ReturnsIndex.{language}.resx");
            var values = XDocument.Load(path).Root!.Elements("data")
                .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty);
            return ResxCache[language] = values;
        }
    }

    private static string Read(string path) => File.ReadAllText(Path.Combine(Root(), path));
    private static string Root() { var dir = new DirectoryInfo(AppContext.BaseDirectory); while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent; return dir?.FullName ?? throw new DirectoryNotFoundException(); }
}
