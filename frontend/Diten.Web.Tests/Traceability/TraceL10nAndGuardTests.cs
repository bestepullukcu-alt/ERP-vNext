using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Models.Traceability;
using Xunit;

namespace Diten.Web.Tests.Traceability;

/// <summary>
/// MOD-0174 P3 L10n (pack §11.6) and repo guards:
/// <list type="bullet">
/// <item>every family is written in the seven tenant languages (P3 en + tr, P5 fr / es / zh / ar / ru): the same keys in
/// the same order, every value real, a Turkish value never the key's own name; a P5 value keeps en's placeholders and is
/// never an English copy unless <see cref="SameAsEnglish"/> names it with its reason;</item>
/// <item>every key the views, the controllers' choice tables (errors, countries, statuses) and the scripts ask for exists
/// — a missing resx key prints its NAME at runtime and no compiler sees it;</item>
/// <item>the browser never calls the Gateway, 5067 or /api/trace; no raw Swal.fire; no code path into docs/analysis.</item>
/// </list>
/// </summary>
public sealed class TraceL10nAndGuardTests
{
    private static readonly string Resources = Path.Combine(TraceViewRenderer.WebRoot, "Resources", "Views", "Traceability");
    private static readonly string Scripts = Path.Combine(TraceViewRenderer.WebRoot, "wwwroot", "assets", "js", "Traceability");

    public static TheoryData<string> Families => new()
        { "Lots/LotsIndex", "Serials/SerialsIndex", "LotMovements/LotMovementsIndex", "LotTrace/LotTraceIndex", "Lots/LotFieldIndex", "LotMigration/LotMigrationIndex" };

    /// <summary>Values that are the same word in both languages (codes, formats, a loan word) — named, not a loophole.</summary>
    private static readonly HashSet<string> SameInBoth = new(StringComparer.Ordinal)
    {
        "Col.LotNumber", "Form.LotNumber", "Form.Sku", "Form.ReasonCounter", "Info.None", "Tracking.Lot", "Serial.Counter",
        "Filter.Lot", "Receipt.Lot", "Lot.Picker", "Col.Lot", "Country.TR"
    };

    private static readonly string[] Languages = ["ar", "en", "es", "fr", "ru", "tr", "zh"];
    private static readonly string[] P5Languages = ["fr", "es", "zh", "ar", "ru"];

    /// <summary>
    /// WP-0174-P5: P5 values that are deliberately the same as en — a placeholder-only counter, the dash, a word or a
    /// country name spelled the same. Exact key per language with its reason; no prefix, no "contains {" loophole.
    /// </summary>
    private static readonly Dictionary<string, Dictionary<string, string>> SameAsEnglish = new(StringComparer.Ordinal)
    {
        ["fr"] = new(StringComparer.Ordinal)
        {
            ["Form.ReasonCounter"] = "placeholder-only counter {0}/10", ["Serial.Counter"] = "placeholder-only counter {0} / {1}",
            ["Info.None"] = "the dash for an empty value", ["History.Page"] = "« Page {0} / {1} » is French",
            ["Col.Actions"] = "« Actions » is French", ["Col.Action"] = "« Action » is French",
            ["History.Date"] = "« Date » is French", ["Receipt.DateBy"] = "« Date » is French", ["Receipt.Document"] = "« Document » is French",
            ["Tracking.Lot"] = "glossary: Lot = Lot", ["Lot.Picker"] = "glossary: Lot = Lot", ["Filter.Lot"] = "glossary: Lot = Lot",
            ["Receipt.Lot"] = "glossary: Lot = Lot",
            ["Country.CA"] = "Canada", ["Country.FR"] = "France", ["Country.PT"] = "Portugal", ["Country.UA"] = "Ukraine"
        },
        ["es"] = new(StringComparer.Ordinal)
        {
            ["Form.ReasonCounter"] = "placeholder-only counter {0}/10", ["Serial.Counter"] = "placeholder-only counter {0} / {1}",
            ["Info.None"] = "the dash for an empty value",
            ["Country.AT"] = "Austria", ["Country.AU"] = "Australia", ["Country.CN"] = "China", ["Country.IL"] = "Israel",
            ["Country.IN"] = "India", ["Country.PT"] = "Portugal"
        },
        ["zh"] = new(StringComparer.Ordinal)
        {
            ["Form.ReasonCounter"] = "placeholder-only counter {0}/10", ["Serial.Counter"] = "placeholder-only counter {0} / {1}",
            ["Info.None"] = "the dash for an empty value"
        },
        ["ar"] = new(StringComparer.Ordinal)
        {
            ["Form.ReasonCounter"] = "placeholder-only counter {0}/10", ["Serial.Counter"] = "placeholder-only counter {0} / {1}",
            ["Info.None"] = "the dash for an empty value"
        },
        ["ru"] = new(StringComparer.Ordinal)
        {
            ["Form.ReasonCounter"] = "placeholder-only counter {0}/10", ["Serial.Counter"] = "placeholder-only counter {0} / {1}",
            ["Info.None"] = "the dash for an empty value"
        }
    };

    [Theory]
    [MemberData(nameof(Families))]
    public void Every_family_is_written_in_the_seven_languages_with_the_same_keys_and_real_text(string family)
    {
        var folder = Path.GetDirectoryName(Path.Combine(Resources, family))!;
        var name = Path.GetFileName(family);
        var files = Directory.EnumerateFiles(folder, name + ".*.resx").Select(Path.GetFileName).OrderBy(f => f, StringComparer.Ordinal).ToList();
        Assert.Equal(Languages.Select(l => $"{name}.{l}.resx"), files);

        var en = Read(family, "en");
        var tr = Read(family, "tr");
        Assert.True(en.Count >= 40, $"{family}: too few keys ({en.Count})");
        Assert.Equal(en.Keys.OrderBy(k => k, StringComparer.Ordinal), tr.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.All(en.Concat(tr), p => Assert.False(string.IsNullOrWhiteSpace(p.Value), $"{family}: {p.Key} is empty"));
        Assert.All(tr, p => Assert.NotEqual(p.Key, p.Value));
        Assert.All(tr.Where(p => !SameInBoth.Contains(p.Key) && !p.Key.StartsWith("Country.", StringComparison.Ordinal) && !p.Value.Contains('{')),
            p => Assert.True(p.Value != en[p.Key], $"{family}.tr: {p.Key} is an English copy"));

        var enOrder = KeyOrder(family, "en");
        foreach (var lang in P5Languages)
        {
            var values = Read(family, lang);
            Assert.True(enOrder.SequenceEqual(KeyOrder(family, lang)), $"{family}.{lang}: keys differ from en (set or order)");
            Assert.All(values, p => Assert.False(string.IsNullOrWhiteSpace(p.Value), $"{family}.{lang}: {p.Key} is empty"));
            Assert.All(values, p => Assert.False(p.Value.Contains("TODO", StringComparison.Ordinal), $"{family}.{lang}: {p.Key} is a TODO"));
            Assert.All(values, p => Assert.True(Placeholders(p.Value) == Placeholders(en[p.Key]),
                $"{family}.{lang}: {p.Key} placeholders {Placeholders(p.Value)} differ from en {Placeholders(en[p.Key])}"));
            Assert.All(values.Where(p => !SameAsEnglish[lang].ContainsKey(p.Key)),
                p => Assert.True(p.Value != en[p.Key], $"{family}.{lang}: {p.Key} is an English copy"));
        }
    }

    [Fact]
    public void Every_same_as_english_entry_is_still_needed()
    {
        var families = ((IEnumerable<object[]>)Families).Select(row => (string)row[0]).ToList();
        foreach (var (lang, entries) in SameAsEnglish)
            Assert.All(entries.Keys, key => Assert.True(
                families.Any(f => Read(f, lang).TryGetValue(key, out var v) && v == Read(f, "en")[key]),
                $"{lang}: {key} is allow-listed but no longer equals en — remove it"));
    }

    [Fact]
    public void Every_key_the_pages_and_controllers_choose_exists_in_its_family()
    {
        var lots = Read("Lots/LotsIndex", "en");
        Assert.All(TraceErrorMessages.AllKeys, k => Assert.Contains(k, lots.Keys));
        Assert.All(LotFormRules.Countries, c => Assert.Contains("Country." + c, lots.Keys));
        Assert.All(TraceListAdapter.LotStatuses, s => Assert.Contains("Status." + s, lots.Keys));
        Assert.All(TraceActions.KeyOf.Keys.Where(k => !TraceActions.MigrationOnly.Contains(k)), a => Assert.Contains("Action." + a, lots.Keys));
        var field = Read("Lots/LotFieldIndex", "en");
        Assert.All(new[] { "Lot.BlockedDisabled", "Lot.ExpiredDisabled", "Serial.GtinSkippedWarning", "Serial.Mismatch.Gtin", "Serial.Mismatch.Lot",
            "Serial.Mismatch.Expiry", "Serial.RangePrefix", "Serial.RangeOrder", "Serial.RangeMax", "Serial.BadFormat" }, k => Assert.Contains(k, field.Keys));
        foreach (var family in new[] { "Serials/SerialsIndex", "LotMovements/LotMovementsIndex", "LotTrace/LotTraceIndex" })
            Assert.All(TraceErrorMessages.AllKeys, k => Assert.Contains(k, Read(family, "en").Keys));

        // WP-0174-P4: the migration screen answers every P3 code plus the four migration codes, and draws its §14 actions.
        var migration = Read("LotMigration/LotMigrationIndex", "en");
        Assert.All(TraceErrorMessages.MigrationKeys, k => Assert.Contains(k, migration.Keys));
        Assert.All(TraceActions.MigrationOnly, a => Assert.Contains("Action." + a, migration.Keys));
        Assert.Contains("Lot.RetestRequired", Read("Lots/LotFieldIndex", "en").Keys); // FU-0174-P3-07 refusal

        foreach (var (folder, family) in new[] { ("Lots", "Lots/LotsIndex"), ("Serials", "Serials/SerialsIndex"), ("LotMovements", "LotMovements/LotMovementsIndex"),
                     ("LotTrace", "LotTrace/LotTraceIndex"), ("LotMigration", "LotMigration/LotMigrationIndex") })
        {
            var keys = Read(family, "en").Keys.ToHashSet(StringComparer.Ordinal);
            var asked = Directory.EnumerateFiles(Path.Combine(TraceViewRenderer.WebRoot, "Views", "Traceability", folder), "*.cshtml")
                .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"(?<![A-Za-z])Localizer\[""(?<k>[^""]+)""\]").Select(m => m.Groups["k"].Value)).Distinct().ToList();
            Assert.NotEmpty(asked);
            Assert.All(asked, k => Assert.True(keys.Contains(k), $"{folder} asks for {k}, which is not in {family}"));
        }
    }

    [Fact]
    public void Every_key_the_scripts_ask_for_is_handed_to_them()
    {
        var lists = new Dictionary<string, IReadOnlyList<string>>
        {
            ["Lots"] = TraceL10nKeys.Lots, ["Serials"] = TraceL10nKeys.Serials, ["LotMovements"] = TraceL10nKeys.LotMovements, ["LotTrace"] = TraceL10nKeys.LotTrace,
            ["LotMigration"] = TraceL10nKeys.LotMigration
        };
        foreach (var (folder, list) in lists)
        {
            var family = folder == "Lots" ? "Lots/LotsIndex" : $"{folder}/{folder}Index";
            Assert.All(list, k => Assert.Contains(k, Read(family, "en").Keys));
            var asked = Directory.EnumerateFiles(Path.Combine(Scripts, folder), "*.js")
                .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"\bt\('(?<k>[A-Za-z.]*[A-Za-z])'\s*[,)]").Select(m => m.Groups["k"].Value)).Distinct().ToList();
            Assert.All(asked, k => Assert.True(list.Contains(k), $"{folder} scripts ask for {k}, which TraceL10nKeys.{folder} does not hand over"));
        }

        var formJs = File.ReadAllText(Path.Combine(TraceViewRenderer.WebRoot, "wwwroot", "assets", "js", "Inventory", "StockMovements", "form.js"));
        var lotAsked = Regex.Matches(formJs, @"\blt\('(?<k>[A-Za-z.]*[A-Za-z])'\s*[,)]").Select(m => m.Groups["k"].Value).Distinct().ToList();
        Assert.True(lotAsked.Count >= 10);
        Assert.All(lotAsked, k => Assert.True(TraceL10nKeys.LotField.Contains(k), $"form.js asks for {k}, not in TraceL10nKeys.LotField"));
        Assert.All(TraceL10nKeys.LotField, k => Assert.Contains(k, Read("Lots/LotFieldIndex", "en").Keys));
    }

    [Fact]
    public void The_shared_nav_keys_are_written_in_the_seven_languages()
    {
        foreach (var lang in Languages)
        {
            var shared = XDocument.Load(Path.Combine(TraceViewRenderer.WebRoot, "Resources", $"SharedResource.{lang}.resx")).Root!.Elements("data")
                .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? "");
            foreach (var key in new[] { "Nav.Module.LOTSERIALTRACKING", "Nav.Page.LOTS", "Nav.Page.SERIALS", "Nav.Page.LOTTRACE", "Nav.Page.LOTMIGRATION" })
                Assert.False(string.IsNullOrWhiteSpace(shared.GetValueOrDefault(key)), $"{lang}: {key}");
        }
    }

    [Fact]
    public void The_browser_never_calls_the_gateway_or_the_service_and_dialogs_go_through_showConfirm()
    {
        var sources = Directory.EnumerateFiles(Scripts, "*.js", SearchOption.AllDirectories)
            .Append(Path.Combine(TraceViewRenderer.WebRoot, "wwwroot", "assets", "js", "Inventory", "StockMovements", "form.js"))
            .ToDictionary(f => f, f => StripComments(File.ReadAllText(f)));
        Assert.All(sources, s =>
        {
            Assert.DoesNotContain("/api/trace", s.Value, StringComparison.Ordinal);
            Assert.DoesNotContain(":5067", s.Value, StringComparison.Ordinal);
            Assert.DoesNotContain(":5000", s.Value, StringComparison.Ordinal);
            Assert.DoesNotContain("Swal.fire", s.Value, StringComparison.Ordinal);
        });
        var code = Directory.EnumerateFiles(Path.Combine(TraceViewRenderer.WebRoot), "*.*", SearchOption.AllDirectories)
            .Where(f => f.Contains("Traceability", StringComparison.Ordinal) && (f.EndsWith(".cs") || f.EndsWith(".cshtml") || f.EndsWith(".js")))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
        Assert.All(code, f => Assert.DoesNotContain("docs/analysis", File.ReadAllText(f), StringComparison.Ordinal));
    }

    /// <summary>Code only: the header comments NAME what the browser must not call; the guard reads what it does call.</summary>
    private static string StripComments(string js) => Regex.Replace(Regex.Replace(js, @"/\*[\s\S]*?\*/", string.Empty), @"(?m)^\s*//.*$", string.Empty);

    private static List<string> KeyOrder(string family, string lang) =>
        XDocument.Load(Path.Combine(Resources, $"{family}.{lang}.resx")).Root!.Elements("data").Select(d => (string)d.Attribute("name")!).ToList();

    /// <summary>The {n} placeholders as a sorted multiset — a translation may move them, never drop or add one.</summary>
    private static string Placeholders(string value) =>
        string.Join(",", Regex.Matches(value, @"\{\d+\}").Select(m => m.Value).OrderBy(p => p, StringComparer.Ordinal));

    private static Dictionary<string, string> Read(string family, string lang) =>
        XDocument.Load(Path.Combine(Resources, $"{family}.{lang}.resx")).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => ((string?)d.Element("value") ?? string.Empty).Trim(), StringComparer.Ordinal);
}
