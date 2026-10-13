using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Models.Traceability;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Diten.Web.Tests.Traceability;

/// <summary>
/// WP-0174-FU5 (FU-0174-P5) — one concept, one word on every screen; a readable lot history; SKT visible in ru:
/// <list type="bullet">
/// <item>T-1 / T-2: the Stock Ledger screens (MOD-0173-FU01) call a lot what the MOD-0174 glossary calls it — tr "Lot"
/// (never "Parti"), ar <c>التشغيلة</c> (never <c>الدفعة</c>);</item>
/// <item>T-4: ru "product" is <c>Товар</c> everywhere (no <c>Продукт</c>);</item>
/// <item>T-5: a movement type reads the same on Lot Movements and Stock Movements, in all seven languages;</item>
/// <item>F-1: the Lot Detail history maps every field the SERVICE writes (read from the service code, not a copy) to the
/// screen's label, a status to its status label, a date to the page date format, a country to its name;</item>
/// <item>F-2: the Lot Explorer keeps SKT (Expiry) visible from tablet-landscape up, the secondary columns fold first.</item>
/// </list>
/// </summary>
public sealed class Fu5TermsAndHistoryTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string Web = TraceViewRenderer.WebRoot;
    private static readonly string Repo = Path.GetFullPath(Path.Combine(Web, "..", ".."));
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    private static readonly string[] Fu01Families = ["Inventory/StockLedger/StockLedgerIndex", "Inventory/StockMovements/StockMovementsIndex"];

    private static readonly string[] TraceFamilies =
    [
        "Traceability/Lots/LotsIndex", "Traceability/Lots/LotFieldIndex", "Traceability/Serials/SerialsIndex",
        "Traceability/LotMovements/LotMovementsIndex", "Traceability/LotTrace/LotTraceIndex", "Traceability/LotMigration/LotMigrationIndex"
    ];

    /// <summary>
    /// A value that uses the forbidden word in ANOTHER sense (tr "parti" = party / consignment, ar <c>الدفع</c> = payment) —
    /// exact family.lang.key with its reason. Empty: every hit measured in FU5 meant "lot".
    /// </summary>
    private static readonly Dictionary<string, string> NotTheLotSense = new(StringComparer.Ordinal);

    private readonly WebApplicationFactory<Program> _factory;

    public Fu5TermsAndHistoryTests(WebApplicationFactory<Program> factory) => _factory = factory;

    // ── T-1 / T-2 ────────────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("tr", @"(?i)\bparti")]
    [InlineData("ar", "دفع")]
    public void The_lot_has_the_glossary_name_on_the_stock_ledger_and_traceability_screens(string lang, string forbidden)
    {
        var hits = Fu01Families.Concat(TraceFamilies)
            .SelectMany(family => Read(family, lang).Select(pair => (Id: $"{Name(family)}.{lang}.{pair.Key}", pair.Value)))
            .Where(v => Regex.IsMatch(v.Value, forbidden) && !NotTheLotSense.ContainsKey(v.Id))
            .Select(v => $"{v.Id} = {v.Value}")
            .ToList();
        Assert.True(hits.Count == 0, "lot under another name:\n" + string.Join("\n", hits));

        // The ledger's lot column says the word the Traceability pages say (LotsIndex Tracking.Lot = the glossary's Lot).
        var lot = Read("Traceability/Lots/LotsIndex", lang)["Tracking.Lot"];
        foreach (var family in Fu01Families)
        {
            Assert.Equal(lot, Read(family, lang)["ColLot"]);
            Assert.Contains(lot.ToLowerInvariant(), Read(family, lang)["FilterLot"].ToLowerInvariant(), StringComparison.Ordinal);
        }
    }

    // ── T-4 ──────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Product_is_Tovar_in_russian_on_every_inventory_and_traceability_screen()
    {
        var hits = Fu01Families.Concat(TraceFamilies)
            .SelectMany(family => Read(family, "ru").Where(pair => Regex.IsMatch(pair.Value, "(?i)продукт")).Select(pair => $"{Name(family)}.ru.{pair.Key} = {pair.Value}"))
            .ToList();
        Assert.True(hits.Count == 0, "Продукт is still used:\n" + string.Join("\n", hits));
        Assert.Equal("Товар", Read("Inventory/StockMovements/StockMovementsIndex", "ru")["FieldProduct"]);
        Assert.Equal("Товар", Read("Traceability/Lots/LotsIndex", "ru")["Col.Product"]);
    }

    // ── T-5 ──────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(LanguageData))]
    public void A_movement_type_reads_the_same_on_lot_movements_and_stock_movements(string lang)
    {
        var ledger = Read("Inventory/StockMovements/StockMovementsIndex", lang)
            .Where(p => Regex.IsMatch(p.Key, "^Movement[A-Z_]+$")).ToDictionary(p => p.Key["Movement".Length..], p => p.Value);
        var trace = Read("Traceability/LotMovements/LotMovementsIndex", lang)
            .Where(p => p.Key.StartsWith("Movement.", StringComparison.Ordinal)).ToDictionary(p => p.Key["Movement.".Length..], p => p.Value);

        Assert.Equal(15, ledger.Count);
        Assert.Equal(ledger.Keys.Order(StringComparer.Ordinal), trace.Keys.Order(StringComparer.Ordinal));
        var differ = ledger.Where(p => trace[p.Key] != p.Value).Select(p => $"{p.Key}: ledger \"{p.Value}\" / lot movements \"{trace[p.Key]}\"").ToList();
        Assert.True(differ.Count == 0, $"{lang}:\n" + string.Join("\n", differ));
    }

    public static TheoryData<string> LanguageData()
    {
        var data = new TheoryData<string>();
        foreach (var lang in Languages) data.Add(lang);
        return data;
    }

    // ── F-1 ──────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The field names the service writes into a lot history row, read from the PRODUCTION handlers.</summary>
    private static (HashSet<string> All, HashSet<string> Dates) ServiceHistoryFields()
    {
        var root = Path.Combine(Repo, "services", "Diten.InventoryService", "src");
        var code = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => f.Contains($"{Path.DirectorySeparatorChar}Traceability{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(File.ReadAllText).ToList();
        string[] patterns =
        [
            @"\.History\(\s*[^,;]+,\s*[^,;]+,\s*""(?<f>[^""]+)""",          // _support.History(lotId, action, "field", …)
            @"\bApply(?:Date)?\(request\.\w+,\s*""(?<f>[^""]+)""",          // UpdateLotHandler Apply / ApplyDate
            @"\bField\s*=\s*""(?<f>[^""]+)"""                                // LotExpirySweeper (system expiry row)
        ];
        var all = code.SelectMany(c => patterns.SelectMany(p => Regex.Matches(c, p).Select(m => m.Groups["f"].Value))).ToHashSet(StringComparer.Ordinal);
        var dates = code.SelectMany(c => Regex.Matches(c, @"\bApplyDate\(request\.\w+,\s*""(?<f>[^""]+)""").Select(m => m.Groups["f"].Value)).ToHashSet(StringComparer.Ordinal);
        return (all, dates);
    }

    private static string DetailsJs() => File.ReadAllText(Path.Combine(Web, "wwwroot", "assets", "js", "Traceability", "Lots", "details.js"));

    private static Dictionary<string, (string Label, string? Value)> HistoryFieldMap()
    {
        var block = Regex.Match(DetailsJs(), @"const HISTORY_FIELDS = \{(?<b>[\s\S]*?)\n\s*\};").Groups["b"].Value;
        return Regex.Matches(block, @"(?<k>'[^']+'|\w+):\s*\{\s*label:\s*'(?<l>[^']+)'(?:,\s*value:\s*'(?<v>\w+)')?\s*\}")
            .ToDictionary(m => m.Groups["k"].Value.Trim('\''), m => (m.Groups["l"].Value, m.Groups["v"].Success ? m.Groups["v"].Value : null), StringComparer.Ordinal);
    }

    [Fact]
    public void Every_field_the_service_writes_into_the_history_has_the_screens_label_and_value_format()
    {
        var (fields, dates) = ServiceHistoryFields();
        var map = HistoryFieldMap();

        // the measurement itself: the service writes these ten (FU5) — a new one must be added to the map, not slip through raw
        Assert.True(fields.Count >= 10, "service history fields found: " + string.Join(", ", fields));
        Assert.Equal(3, dates.Count);
        Assert.All(fields, f => Assert.True(map.ContainsKey(f), $"details.js HISTORY_FIELDS has no label for the service field '{f}'"));

        Assert.Equal("status", map["status"].Value);
        Assert.Equal("country", map["countryOfOrigin"].Value);
        Assert.All(dates, f => Assert.Equal("date", map[f].Value));

        // every label is handed to the script and written in seven languages
        foreach (var (field, (label, _)) in map)
        {
            Assert.Contains(label, TraceL10nKeys.Lots);
            foreach (var lang in Languages)
                Assert.False(string.IsNullOrWhiteSpace(Read("Traceability/Lots/LotsIndex", lang).GetValueOrDefault(label)), $"LotsIndex.{lang}: {label} ({field})");
        }

        // the values the formatter looks up are handed over: every lot status, every country the form offers
        var statuses = Regex.Matches(File.ReadAllText(Path.Combine(Repo, "services", "Diten.InventoryService", "src", "Diten.InventoryService.Domain",
            "Traceability", "Enums", "LotStatus.cs")), @"^\s*(?<s>[A-Z]\w+),?\s*$", RegexOptions.Multiline).Select(m => m.Groups["s"].Value).ToList();
        Assert.Equal(4, statuses.Count);
        Assert.All(statuses, s => Assert.Contains("Status." + s, TraceL10nKeys.Lots));
        Assert.All(LotFormRules.Countries, c => Assert.Contains("Country." + c, TraceL10nKeys.Lots));
    }

    [Fact]
    public void A_history_row_never_prints_a_raw_field_name_or_value()
    {
        var js = DetailsJs();
        var row = Regex.Match(js, @"const historyRow = \(h\) => \{(?<b>[\s\S]*?)\n    \};").Groups["b"].Value;
        Assert.NotEmpty(row);

        // the only ways a row may touch the service's field / values: through the formatter
        var rest = row.Replace("historyValue(h.field, h.oldValue)", "").Replace("historyValue(h.field, h.newValue)", "")
            .Replace("historyField(h.field)", "").Replace("h.field ?", "");
        Assert.DoesNotContain("h.oldValue", rest, StringComparison.Ordinal);
        Assert.DoesNotContain("h.newValue", rest, StringComparison.Ordinal);
        Assert.DoesNotContain("h.field", rest, StringComparison.Ordinal);
        Assert.Contains("historyValue(h.field, h.oldValue)", row, StringComparison.Ordinal);
        Assert.Contains("historyValue(h.field, h.newValue)", row, StringComparison.Ordinal);
        Assert.Contains("historyField(h.field)", row, StringComparison.Ordinal);

        // the formatter: status → its status label, date → the page date format, country → its name
        var formatter = Regex.Match(js, @"const historyValue = \(field, value\) => \{(?<b>[\s\S]*?)\n    \};").Groups["b"].Value;
        Assert.Contains("case 'status': return t('Status.' + value)", formatter, StringComparison.Ordinal);
        Assert.Contains("case 'date': return ui.date(value)", formatter, StringComparison.Ordinal);
        Assert.Contains("case 'country': return t('Country.' + value)", formatter, StringComparison.Ordinal);
        Assert.Contains("const historyField = (field) => (HISTORY_FIELDS[field] ? t(HISTORY_FIELDS[field].label) : field);", js, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_lot_detail_page_hands_the_history_labels_to_its_script()
    {
        var html = await TraceViewRenderer.RenderAsync(_factory, "/Views/Traceability/Lots/Details.cshtml",
            LotPageViewModel.From(Guid.NewGuid().ToString()), "trace.lots.read", "inventory.stock.read");
        var payload = Regex.Match(html, @"<script id=""lots-l10n"" type=""application/json"">(?<j>[\s\S]*?)</script>").Groups["j"].Value;
        var l10n = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(payload)!;

        Assert.Equal("Parent lot", l10n["History.Field.GenealogyParent"]);
        Assert.Equal("Typed lot", l10n["History.Field.TypedText"]);
        Assert.Equal("Blocked", l10n["Status.Blocked"]);
        Assert.Equal("Germany", l10n["Country.DE"]);
        Assert.Equal("Expiry date", l10n["Form.ExpiryDate"]);
    }

    // ── F-2 ──────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_lot_explorer_keeps_expiry_visible_and_folds_the_secondary_columns_first()
    {
        var html = await TraceViewRenderer.RenderAsync(_factory, "/Views/Traceability/Lots/Index.cshtml", null, "trace.lots.read", "inventory.stock.read");
        var thead = Regex.Match(html, @"id=""dt-lots""[\s\S]*?<thead>(?<h>[\s\S]*?)</thead>").Groups["h"].Value;
        var headers = Regex.Matches(thead, @"<th(?: class=""(?<c>[^""]*)"")?>(?<t>[^<]*)</th>")
            .ToDictionary(m => System.Net.WebUtility.HtmlDecode(m.Groups["t"].Value).Trim(), m => m.Groups["c"].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
        var en = Read("Traceability/Lots/LotsIndex", "en");
        string[] Class(string key) => headers.TryGetValue(en[key], out var c) ? c : throw new Xunit.Sdk.XunitException($"no header {en[key]} ({key})");

        // Expiry is shown from tablet-landscape up, like Status and On hand (P3's _DataTable pattern)
        foreach (var key in new[] { "Col.ExpiryDate", "Col.Status", "Col.OnHand" })
            Assert.True(Class(key).Contains("min-tablet-l") || Class(key).Contains("all"), $"{key} has no visibility class");
        // the secondary columns carry none: Responsive folds them before a class-bound column
        foreach (var key in new[] { "Col.ManufacturerLot", "Col.SupplierLot", "Col.Origin", "Col.CoaRef", "Col.RetestDate" })
            Assert.Empty(Class(key));
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────────────

    private static string Name(string family) => family[(family.LastIndexOf('/') + 1)..];

    private static Dictionary<string, string> Read(string family, string lang) =>
        XDocument.Load(Path.Combine(Web, "Resources", "Views", $"{family}.{lang}.resx"))
            .Root!.Elements("data")
            .ToDictionary(e => (string)e.Attribute("name")!, e => (string?)e.Element("value") ?? string.Empty, StringComparer.Ordinal);
}
