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
/// MOD-0178 DL-3 — localization and page help of the two S1 screens, measured on the real resx files (AC-23, AC-24; tenant module → 7 languages):
/// <list type="bullet">
/// <item>each screen family has the SAME key set in en, tr, fr, es, zh, ar, ru; no value is empty, none of the six repeats the English text;</item>
/// <item>every key a view, a controller or a script reads is in all seven (the scripts' words: <see cref="WarehouseVocabulary"/>);</item>
/// <item>the nav keys of the manifest (module + two pages) are in SharedResource in all seven;</item>
/// <item><c>Help.PUTAWAYTASKS</c> (4 titled steps + Immediate) and <c>Help.WAREHOUSELOCATIONROLES</c> (3 titled steps) in all seven, one catalog icon per
/// step, the related pages' sentences, no tour; the shell panel draws them.</item>
/// </list>
/// </summary>
public sealed class WarehouseL10nAndHelpTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string Web = TraceViewRenderer.WebRoot;
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    public static readonly TheoryData<string, string> Families = new()
    {
        { "PutawayTasks", "PutawayTasksIndex" },
        { "WarehouseLocationRoles", "WarehouseLocationRolesIndex" }
    };

    private static Dictionary<string, string> Rows(string path) =>
        XDocument.Load(path).Root!.Elements("data").ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")?.Value ?? string.Empty, StringComparer.Ordinal);

    private static Dictionary<string, Dictionary<string, string>> Family(string folder, string marker) =>
        Languages.ToDictionary(l => l, l => Rows(Path.Combine(Web, "Resources", "Views", "Warehouse", folder, $"{marker}.{l}.resx")));

    /// <summary>A value that is only numbers, units or placeholders may equal the English one.</summary>
    private static bool Neutral(string value) => Regex.IsMatch(value, @"^[\d{}\s:/.,–\-°C]*$");

    [Theory]
    [MemberData(nameof(Families))]
    public void AC23_the_family_has_one_key_set_in_all_seven_languages_and_no_english_left_behind(string folder, string marker)
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

        // the format holes match across languages ({0}, {1})
        foreach (var (key, value) in en)
        {
            var holes = Regex.Matches(value, @"\{\d\}").Select(m => m.Value).Order().ToList();
            foreach (var lang in Languages) Assert.Equal(holes, Regex.Matches(family[lang][key], @"\{\d\}").Select(m => m.Value).Order().ToList());
        }
    }

    [Theory]
    [MemberData(nameof(Families))]
    public void AC23_every_key_a_view_a_controller_or_the_script_reads_is_there(string folder, string marker)
    {
        var en = Family(folder, marker)["en"];
        var views = Directory.GetFiles(Path.Combine(Web, "Views", "Warehouse", folder), "*.cshtml")
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"(?<!Shared)Localizer\[""([^""]+)""\]").Select(m => m.Groups[1].Value));
        var controller = File.ReadAllText(Path.Combine(Web, "Controllers", folder == "PutawayTasks" ? "PutawayTasksController.cs" : "WarehouseLocationRolesController.cs"));
        var controllerKeys = Regex.Matches(controller, @"""(Warehouse\.Saved\.\w+|Col\w+|Field\w+)""").Select(m => m.Groups[1].Value);
        var vocabulary = folder == "PutawayTasks" ? WarehouseVocabulary.PutawayKeys : WarehouseVocabulary.RoleKeys;

        var missing = views.Concat(controllerKeys).Concat(vocabulary).Concat(WarehouseErrorMessages.AllKeys).Distinct().Where(k => !en.ContainsKey(k)).ToList();
        Assert.True(missing.Count == 0, $"{marker} lacks: {string.Join(", ", missing)}");
        Assert.True(vocabulary.Count > 40);
    }

    [Fact]
    public void AC23_the_manifest_nav_keys_are_in_all_seven_languages()
    {
        foreach (var lang in Languages)
        {
            var shared = Rows(Path.Combine(Web, "Resources", $"SharedResource.{lang}.resx"));
            foreach (var key in new[] { "Nav.Module.PUTAWAYPICKINGPACKING", "Nav.Page.PUTAWAYTASKS", "Nav.Page.WAREHOUSELOCATIONROLES", "Nav.Domain.SUPPLYCHAINEXECUTION" })
                Assert.False(string.IsNullOrWhiteSpace(shared.GetValueOrDefault(key)), $"{lang}: {key}");
        }
    }

    public static readonly TheoryData<string, int, bool, string> Pages = new()
    {
        { "PUTAWAYTASKS", 4, true, "Warehouse/PutawayTasks/Index.cshtml" },
        { "WAREHOUSELOCATIONROLES", 3, false, "Warehouse/WarehouseLocationRoles/Index.cshtml" }
    };

    [Theory]
    [MemberData(nameof(Pages))]
    public void AC24_the_page_help_has_purpose_audience_and_titled_steps_in_all_seven_languages_and_no_tour(string code, int steps, bool immediate, string view)
    {
        foreach (var lang in Languages)
        {
            var help = Rows(Path.Combine(Web, "Resources", $"PageHelpResource.{lang}.resx"));
            var parts = new[] { "Purpose", "Audience" }.Concat(Enumerable.Range(1, steps).SelectMany(n => new[] { $"Step{n}", $"Step{n}Title" }));
            foreach (var part in parts) Assert.False(string.IsNullOrWhiteSpace(help.GetValueOrDefault($"Help.{code}.{part}")), $"{lang}: Help.{code}.{part}");
            Assert.False(help.ContainsKey($"Help.{code}.Step{steps + 1}"), $"{lang}: Help.{code} has more than {steps} steps");
            Assert.Equal(immediate, help.ContainsKey($"Help.{code}.Immediate"));
            Assert.All(help.Keys.Where(k => k.StartsWith($"Help.{code}.", StringComparison.Ordinal)),
                k => Assert.Matches($@"^Help\.{code}\.(Purpose|Audience|Immediate|Step[1-5](Title)?|Related\.[A-Z]+)$", k)); // no Tour key (pack §24)
            foreach (var related in PageHelpCatalog.RelatedCodes[code])
                Assert.False(string.IsNullOrWhiteSpace(help.GetValueOrDefault($"Help.{code}.Related.{related}")), $"{lang}: Help.{code}.Related.{related}");
        }

        for (var n = 1; n <= steps; n++) Assert.NotEqual(PageHelpCatalog.DefaultStepIcon, PageHelpCatalog.StepIcon(code, n));
        Assert.Equal(PageHelpCatalog.DefaultStepIcon, PageHelpCatalog.StepIcon(code, steps + 1));
        Assert.Contains($"ViewData[\"PageCode\"] = \"{code}\";", File.ReadAllText(Path.Combine([Web, "Views", .. view.Split('/')])), StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task AC24_the_shell_panel_draws_the_pages_purpose_and_its_step_cards(string code, int steps, bool immediate, string view)
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
}
