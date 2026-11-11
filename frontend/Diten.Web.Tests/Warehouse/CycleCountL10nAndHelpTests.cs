using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Models.Warehouse;
using Diten.Web.Services.Help;
using Diten.Web.Tests.Controllers;
using Diten.Web.Tests.Traceability;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Diten.Web.Tests.Warehouse;

/// <summary>
/// MOD-0181 DL-4 — localization, page help and the explicit layout of the three cycle-count screens, measured on the real files (pack §11, §25;
/// AC-35, AC-37, AC-42; tenant module → 7 languages):
/// <list type="bullet">
/// <item>each screen family (CycleCountsIndex, CountSheet, CountVariancesIndex) has the SAME key set in en, tr, fr, es, zh, ar, ru; no value is empty,
/// none of the six repeats the English text, the format holes match;</item>
/// <item>every key a view, a controller or a script reads is in all seven — the scripts' literal <c>W('…')</c> keys and their enumerated
/// <c>W(`Prefix.${…}`)</c> families, and every error key <see cref="CycleCountErrorMessages.AllKeys"/>;</item>
/// <item>the nav keys of the three manifest pages are in SharedResource in all seven;</item>
/// <item><c>Help.CYCLECOUNTS</c> (4 steps + Immediate), <c>Help.COUNTSHEET</c> (3 steps), <c>Help.COUNTVARIANCES</c> (4 steps + Immediate +
/// Note.ManualCount) in all seven, one catalog icon per step, the related pages' sentences, no tour; the shell panel draws them;</item>
/// <item>AC-35: every <c>.cshtml</c> of the two folders says <c>Layout = "_LayoutTenantShell"</c> itself (the partials included — pack AC-35 names
/// "each of the files").</item>
/// </list>
/// </summary>
public sealed class CycleCountL10nAndHelpTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string Web = TraceViewRenderer.WebRoot;
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    public static readonly TheoryData<string, string> Families = new()
    {
        { "CycleCounts", "CycleCountsIndex" },
        { "CycleCounts", "CountSheet" },
        { "CountVariances", "CountVariancesIndex" }
    };

    // The enumerations the scripts and views read by prefix (the service's enum values, draft §3).
    private static readonly IReadOnlyDictionary<string, string[]> Enumerations = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["CycleCount.Status."] = ["Draft", "Released", "Counting", "Submitted", "Closed", "Cancelled"],
        ["CycleCount.Type."] = ["Cycle", "Full"],
        ["CycleCount.LineStatus."] = ["Open", "Counted", "NoVariance", "AwaitingApproval", "Approved", "Rejected", "Recounted"],
        ["CycleCount.Event."] = ["Created", "Updated", "Released", "Cancelled", "LineCounted", "LineAdded", "Submitted", "VarianceComputed", "Approved", "Rejected",
            "RecountRequested", "AdjustmentPending", "AdjustmentPosted", "AdjustmentRefused", "AdjustmentIntegrityError", "IntegrityResolved", "Closed"],
        ["CycleCount.Decision."] = ["Approved", "Rejected", "Recount"],
        ["CycleCount.Decision.Source."] = ["User", "Tolerance"],
        ["CycleCount.Adjustment."] = ["Pending", "Posted", "Refused", "IntegrityError"],
        ["CycleCount.Integrity."] = ["AcceptHolder", "Reject"],
        ["MovementType."] = ["COUNT_GAIN", "COUNT_LOSS"]
    };

    private static Dictionary<string, string> Rows(string path) =>
        XDocument.Load(path).Root!.Elements("data").ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")?.Value ?? string.Empty, StringComparer.Ordinal);

    private static Dictionary<string, Dictionary<string, string>> Family(string folder, string marker) =>
        Languages.ToDictionary(l => l, l => Rows(Path.Combine(Web, "Resources", "Views", "Warehouse", folder, $"{marker}.{l}.resx")));

    /// <summary>A value that is only numbers, units, percent signs or placeholders may equal the English one.</summary>
    private static bool Neutral(string value) => Regex.IsMatch(value, @"^[\d{}\s:/.,–\-%]*$");

    /// <summary>The files whose words a family carries.</summary>
    private static IEnumerable<string> Sources(string marker)
    {
        var views = Path.Combine(Web, "Views", "Warehouse");
        var js = Path.Combine(Web, "wwwroot", "assets", "js", "Warehouse");
        return marker switch
        {
            "CycleCountsIndex" => Directory.GetFiles(Path.Combine(views, "CycleCounts"), "*.cshtml").Where(f => !f.EndsWith("CountSheet.cshtml", StringComparison.Ordinal))
                .Append(Path.Combine(js, "CycleCounts", "index.js")).Append(Path.Combine(Web, "Controllers", "CycleCountsController.cs")),
            "CountSheet" => [Path.Combine(views, "CycleCounts", "CountSheet.cshtml"), Path.Combine(js, "CycleCounts", "countsheet.js")],
            _ => Directory.GetFiles(Path.Combine(views, "CountVariances"), "*.cshtml")
                .Append(Path.Combine(js, "CountVariances", "index.js")).Append(Path.Combine(Web, "Controllers", "CountVariancesController.cs"))
        };
    }

    private static List<string> UsedKeys(string marker)
    {
        var keys = new List<string>();
        foreach (var file in Sources(marker))
        {
            var text = File.ReadAllText(file);
            keys.AddRange(Regex.Matches(text, @"(?<!Shared)Localizer\[""([^""]+)""\]").Select(m => m.Groups[1].Value));
            keys.AddRange(Regex.Matches(text, @"_localizer\[""([^""]+)""\]").Select(m => m.Groups[1].Value));
            keys.AddRange(Regex.Matches(text, @"""(CycleCount\.Saved\.\w+)""").Select(m => m.Groups[1].Value));
            keys.AddRange(Regex.Matches(text, @"\bW\('([^']+)'\)").Select(m => m.Groups[1].Value));
            foreach (Match prefix in Regex.Matches(text, @"(?:\bW\(`|Localizer\["")([A-Za-z.]+\.)(?:\$\{|""\s*\+)"))
            {
                Assert.True(Enumerations.TryGetValue(prefix.Groups[1].Value, out var values), $"{file}: enumerated prefix {prefix.Groups[1].Value} is not known to this test");
                keys.AddRange(values!.Select(v => prefix.Groups[1].Value + v));
            }
        }

        return keys.Distinct(StringComparer.Ordinal).ToList();
    }

    [Theory]
    [MemberData(nameof(Families))]
    public void AC37_the_family_has_one_key_set_in_all_seven_languages_and_no_english_left_behind(string folder, string marker)
    {
        var family = Family(folder, marker);
        var en = family["en"];
        Assert.True(en.Count > 90, $"{marker}: only {en.Count} keys");
        foreach (var lang in Languages)
        {
            Assert.Equal(en.Keys.Order(StringComparer.Ordinal), family[lang].Keys.Order(StringComparer.Ordinal));
            Assert.All(family[lang], kv => Assert.False(string.IsNullOrWhiteSpace(kv.Value), $"{lang}: {kv.Key} is empty"));
            if (lang == "en") continue;
            var same = family[lang].Where(kv => kv.Value == en[kv.Key] && !Neutral(kv.Value)).Select(kv => kv.Key).ToList();
            Assert.True(same.Count == 0, $"{lang} repeats the English text: {string.Join(", ", same)}");
        }

        foreach (var (key, value) in en)
        {
            var holes = Regex.Matches(value, @"\{\d\}").Select(m => m.Value).Order().ToList();
            foreach (var lang in Languages) Assert.Equal(holes, Regex.Matches(family[lang][key], @"\{\d\}").Select(m => m.Value).Order().ToList());
        }
    }

    [Theory]
    [MemberData(nameof(Families))]
    public void AC37_every_key_a_view_a_controller_or_a_script_reads_is_there_with_every_error_text(string folder, string marker)
    {
        var family = Family(folder, marker);
        var used = UsedKeys(marker);
        Assert.True(used.Count > 40, $"{marker}: only {used.Count} keys read — the scan is broken");
        foreach (var lang in Languages)
        {
            var missing = used.Concat(CycleCountErrorMessages.AllKeys).Distinct().Where(k => !family[lang].ContainsKey(k)).ToList();
            Assert.True(missing.Count == 0, $"{marker}.{lang} lacks: {string.Join(", ", missing)}");
        }
    }

    [Fact]
    public void AC37_action_texts_are_title_case_in_english()
    {
        foreach (var (folder, marker) in new[] { ("CycleCounts", "CycleCountsIndex"), ("CycleCounts", "CountSheet"), ("CountVariances", "CountVariancesIndex") })
        {
            foreach (var (key, value) in Family(folder, marker)["en"].Where(kv => kv.Key.StartsWith("CycleCount.Action.", StringComparison.Ordinal)))
            {
                var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 2);
                Assert.All(words, w => Assert.True(char.IsUpper(w[0]), $"{marker}: {key} = \"{value}\" is not Title Case"));
            }
        }
    }

    [Fact]
    public void AC41_the_manifest_nav_keys_are_in_all_seven_languages()
    {
        foreach (var lang in Languages)
        {
            var shared = Rows(Path.Combine(Web, "Resources", $"SharedResource.{lang}.resx"));
            foreach (var key in new[] { "Nav.Module.CYCLECOUNTING", "Nav.Page.CYCLECOUNTS", "Nav.Page.COUNTSHEET", "Nav.Page.COUNTVARIANCES", "Perm.Entity.VARIANCES" })
                Assert.False(string.IsNullOrWhiteSpace(shared.GetValueOrDefault(key)), $"{lang}: {key}");
        }
    }

    [Fact]
    public void DUSUK3_the_variances_permission_entity_reads_count_variances()
    {
        Assert.Equal("Count Variances", Rows(Path.Combine(Web, "Resources", "SharedResource.en.resx"))["Perm.Entity.VARIANCES"]);
        Assert.Equal("Sayım Farkları", Rows(Path.Combine(Web, "Resources", "SharedResource.tr.resx"))["Perm.Entity.VARIANCES"]);
    }

    public static readonly TheoryData<string, int, bool, string> Pages = new()
    {
        { "CYCLECOUNTS", 4, true, "Warehouse/CycleCounts/Index.cshtml" },
        { "COUNTSHEET", 3, false, "Warehouse/CycleCounts/CountSheet.cshtml" },
        { "COUNTVARIANCES", 4, true, "Warehouse/CountVariances/Index.cshtml" }
    };

    [Theory]
    [MemberData(nameof(Pages))]
    public void AC42_the_page_help_has_purpose_audience_and_titled_steps_in_all_seven_languages_and_no_tour(string code, int steps, bool immediate, string view)
    {
        foreach (var lang in Languages)
        {
            var help = Rows(Path.Combine(Web, "Resources", $"PageHelpResource.{lang}.resx"));
            var parts = new[] { "Purpose", "Audience" }.Concat(Enumerable.Range(1, steps).SelectMany(n => new[] { $"Step{n}", $"Step{n}Title" }));
            foreach (var part in parts) Assert.False(string.IsNullOrWhiteSpace(help.GetValueOrDefault($"Help.{code}.{part}")), $"{lang}: Help.{code}.{part}");
            Assert.False(help.ContainsKey($"Help.{code}.Step{steps + 1}"), $"{lang}: Help.{code} has more than {steps} steps");
            Assert.Equal(immediate, help.ContainsKey($"Help.{code}.Immediate"));
            Assert.All(help.Keys.Where(k => k.StartsWith($"Help.{code}.", StringComparison.Ordinal)),
                k => Assert.Matches($@"^Help\.{code}\.(Purpose|Audience|Immediate|Step[1-5](Title)?|Related\.[A-Z]+|Note\.[A-Za-z]+)$", k)); // no Tour key (pack §25)
            foreach (var related in PageHelpCatalog.RelatedCodes.GetValueOrDefault(code) ?? [])
                Assert.False(string.IsNullOrWhiteSpace(help.GetValueOrDefault($"Help.{code}.Related.{related}")), $"{lang}: Help.{code}.Related.{related}");
            if (code == "COUNTVARIANCES") Assert.False(string.IsNullOrWhiteSpace(help.GetValueOrDefault("Help.COUNTVARIANCES.Note.ManualCount")), $"{lang}: Note.ManualCount");
        }

        for (var n = 1; n <= steps; n++) Assert.NotEqual(PageHelpCatalog.DefaultStepIcon, PageHelpCatalog.StepIcon(code, n));
        Assert.Equal(PageHelpCatalog.DefaultStepIcon, PageHelpCatalog.StepIcon(code, steps + 1));
        Assert.Contains($"ViewData[\"PageCode\"] = \"{code}\";", File.ReadAllText(Path.Combine([Web, "Views", .. view.Split('/')])), StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task AC42_the_shell_panel_draws_the_pages_purpose_and_its_step_cards(string code, int steps, bool immediate, string view)
    {
        _ = immediate;
        var page = await LayoutRenderer.RenderAsync(factory, "/Views/MasterDataManagement/Lskus/Index.cshtml", "Lskus", "mdm.lskus.read",
            new Dictionary<string, object?> { ["PageCode"] = code });
        var start = page.IndexOf("id=\"diten-help-panel\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"{code}: no help panel ({view})");
        var panel = page[start..];
        Assert.Contains("data-diten-help-purpose", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("data-diten-help-preparing", panel, StringComparison.Ordinal);
        Assert.Equal(steps, Regex.Matches(panel, "data-diten-help-step=\"").Count);
    }

    [Fact]
    public void AC35_every_page_view_names_the_tenant_shell_explicitly_and_the_partials_set_no_layout()
    {
        var files = new[] { "CycleCounts", "CountVariances" }
            .SelectMany(folder => Directory.GetFiles(Path.Combine(Web, "Views", "Warehouse", folder), "*.cshtml")).ToList();
        Assert.Equal(13, files.Count);
        var pages = files.Where(f => !Path.GetFileName(f).StartsWith('_')).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(["CountSheet.cshtml", "Index.cshtml", "Index.cshtml"], pages);
        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            // a partial that set a layout would draw the shell a second time inside the page (the MOD-0178 AC-21 rule)
            if (Path.GetFileName(file).StartsWith('_')) Assert.DoesNotMatch(@"\bLayout\s*=", source);
            else Assert.True(Regex.IsMatch(source, @"^\s*Layout = ""_LayoutTenantShell"";", RegexOptions.Multiline), $"{Path.GetFileName(file)} does not name the tenant shell");
        }
    }
}
