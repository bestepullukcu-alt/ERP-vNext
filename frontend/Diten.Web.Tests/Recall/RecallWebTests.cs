using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Controllers;
using Diten.Web.Tests.Traceability;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Diten.Web.Tests.Recall;

/// <summary>
/// WP-0177-R6 — the three recall pages (pack §11.1): the vocabulary in seven languages (pack §11.6, lot glossary ar "التشغيلة", zh "批次"),
/// UAS-001 per page (no skeleton without the page key, no redirect), tenant routes = the manifest's pages, the UXH-001 help keys in the rule's
/// structure (bound to the "?" panel by FU-0177-HELP-PANEL).
/// </summary>
public sealed class RecallWebTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string[] Languages = ["ar", "en", "es", "fr", "ru", "tr", "zh"];
    private static readonly string Web = TraceViewRenderer.WebRoot;
    private static readonly string Js = Path.Combine(Web, "wwwroot", "assets", "js", "Recall");

    /// <summary>A value a language may share with English on purpose (a code, a unit, a word that is the same).</summary>
    private static readonly HashSet<string> SameAsEnglishAllowed = new(StringComparer.Ordinal)
    {
        "Recall.Kind.Lot", "Recall.Duration.HoursMinutes", "Recall.Duration.Minutes", "Recall.Col.Type", "Recall.Level.Patient", "Recall.Type.Real",
        "Recall.TCol.Date"
    };

    public static readonly TheoryData<string, string, string> Pages = new()
    {
        { "Index", RecallKeys.DashboardRead, "id=\"rc-kpis\"" },
        { "Trace", RecallKeys.TraceRun, "id=\"rc-trace-form\"" },
        { "Cases", RecallKeys.CasesRead, "id=\"rc-status-chips\"" }
    };

    private readonly WebApplicationFactory<Program> _factory;

    public RecallWebTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static Dictionary<string, string> Resx(string path)
        => XDocument.Load(path).Root!.Elements("data").ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty, StringComparer.Ordinal);

    private static string Code(string source) => Regex.Replace(Regex.Replace(source, @"/\*[\s\S]*?\*/", string.Empty), @"(?m)^\s*//.*$", string.Empty);

    // ── vocabulary ──────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Seven_languages_carry_exactly_the_Recall_keys_with_real_text()
    {
        var folder = Path.Combine(Web, "Resources", "Views", "Recall");
        Assert.Equal(Languages.Select(l => $"Recall.{l}.resx"), Directory.EnumerateFiles(folder, "Recall.*.resx").Select(Path.GetFileName).OrderBy(f => f, StringComparer.Ordinal));
        var keys = Diten.Web.Views.Recall.Recall.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.True(keys.Count > 150, $"too few keys: {keys.Count}");
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
        var en = Resx(Path.Combine(folder, "Recall.en.resx"));
        foreach (var lang in Languages)
        {
            var values = Resx(Path.Combine(folder, $"Recall.{lang}.resx"));
            Assert.True(keys.SequenceEqual(values.Keys.OrderBy(k => k, StringComparer.Ordinal)), $"{lang}: the key set differs from Recall.Keys "
                + $"(missing: {string.Join(", ", keys.Except(values.Keys))}; extra: {string.Join(", ", values.Keys.Except(keys))})");
            Assert.All(values, kv => Assert.False(string.IsNullOrWhiteSpace(kv.Value), $"{lang}: {kv.Key} is empty"));
            if (lang != "en")
            {
                var copies = values.Where(kv => kv.Value == en[kv.Key] && !SameAsEnglishAllowed.Contains(kv.Key)).Select(kv => kv.Key).ToList();
                Assert.True(copies.Count == 0, $"{lang} copies English: {string.Join(", ", copies)}");
            }

            // every {n} placeholder of English is in the translation too
            foreach (var (key, value) in values)
            {
                foreach (Match m in Regex.Matches(en[key], @"\{\d\}")) Assert.True(value.Contains(m.Value, StringComparison.Ordinal), $"{lang}: {key} lacks {m.Value}");
            }
        }

        // the lot glossary of the tenant modules
        Assert.Contains("التشغيلة", Resx(Path.Combine(folder, "Recall.ar.resx"))["Recall.Trace.Lot"], StringComparison.Ordinal);
        Assert.Contains("批次", Resx(Path.Combine(folder, "Recall.zh.resx"))["Recall.Trace.Lot"], StringComparison.Ordinal);
    }

    [Fact]
    public void Every_key_a_page_or_script_asks_for_is_delivered()
    {
        var keys = Diten.Web.Views.Recall.Recall.Keys.ToHashSet(StringComparer.Ordinal);
        var sources = Directory.EnumerateFiles(Path.Combine(Web, "Views", "Recall"), "*.cshtml").Concat(Directory.EnumerateFiles(Js, "*.js")).ToList();
        Assert.True(sources.Count >= 10, string.Join(", ", sources));
        foreach (var file in sources)
        {
            foreach (Match m in Regex.Matches(Code(File.ReadAllText(file)), @"[""'](Recall\.[A-Z][A-Za-z0-9_.]*[A-Za-z0-9_])[""']"))
            {
                Assert.True(keys.Contains(m.Groups[1].Value), $"{Path.GetFileName(file)} asks for {m.Groups[1].Value}, which is not delivered");
            }
        }

        // the prefixes a script completes at run time are delivered for every value the server can send
        foreach (var status in new[] { "Draft", "PendingScopeApproval", "Active", "EffectivenessCheck", "Closed", "Cancelled" }) Assert.Contains("Recall.Status." + status, keys);
        foreach (var status in new[] { "Running", "Completed", "Cancelled" }) Assert.Contains("Recall.DrillStatus." + status, keys);
        foreach (var level in new[] { "Wholesale", "Pharmacy", "Patient" }) Assert.Contains("Recall.Level." + level, keys);
        foreach (var kind in new[] { "RawMaterialLot", "FinishedGoodLot", "Lot", "Input", "Stock", "Shipment", "Customer", "Gap", "Disposal" }) Assert.Contains("Recall.Kind." + kind, keys);
        foreach (var gap in new[] { "LOTLESS_OUTBOUND", "UNRESOLVED_TYPED_LOT", "MISSING_GENEALOGY_LINK", "CONSUMED_WITHOUT_OUTPUT_LINK", "NO_CUSTOMER_REFERENCE", "LIMIT_REACHED", "OTHER_COMPANY_ACTIVITY" })
            Assert.Contains("Recall.Gap." + gap, keys);
    }

    /// <summary>UXH-001 key structure (rule from origin/main, read 2026-10-15): Purpose, Audience, Step1…n with Step{n}Title, 1–5 steps, no hole.</summary>
    [Theory]
    [InlineData("RECALLDASHBOARD")]
    [InlineData("RECALLTRACE")]
    [InlineData("RECALLCASES")]
    public void The_page_help_texts_follow_the_UXH_001_key_structure(string code)
    {
        var keys = Diten.Web.Views.Recall.Recall.Keys.Where(k => k.StartsWith($"Help.{code}.", StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);
        Assert.Contains($"Help.{code}.Purpose", keys);
        Assert.Contains($"Help.{code}.Audience", keys);
        var steps = Enumerable.Range(1, 5).TakeWhile(n => keys.Contains($"Help.{code}.Step{n}")).ToList();
        Assert.InRange(steps.Count, 1, 5);
        Assert.All(steps, n => Assert.Contains($"Help.{code}.Step{n}Title", keys));
        Assert.DoesNotContain($"Help.{code}.Step{steps.Count + 1}", keys);
        Assert.All(keys, k => Assert.Matches(new Regex($@"^Help\.{code}\.(Purpose|Audience|Immediate|Step[1-5](Title)?)$"), k));
    }

    // ── UAS-001 ─────────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task UAS_001_without_the_page_key_only_the_access_explanation_is_drawn(string view, string pageKey, string skeleton)
    {
        var others = new[] { RecallKeys.DashboardRead, RecallKeys.TraceRun, RecallKeys.TraceExport, RecallKeys.CasesRead, RecallKeys.CasesCreate }
            .Where(k => k != pageKey).ToArray();
        var denied = await TraceViewRenderer.RenderAsync(_factory, $"/Views/Recall/{view}.cshtml", null, others);
        Assert.Contains("diten-access-denied", denied, StringComparison.Ordinal);
        Assert.DoesNotContain(skeleton, denied, StringComparison.Ordinal);
        Assert.DoesNotContain("rc-tabs", denied, StringComparison.Ordinal);

        var allowed = await TraceViewRenderer.RenderAsync(_factory, $"/Views/Recall/{view}.cshtml", null, pageKey);
        Assert.DoesNotContain("diten-access-denied", allowed, StringComparison.Ordinal);
        Assert.Contains(skeleton, allowed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_trace_buttons_follow_their_actions_and_the_open_case_form_needs_its_key()
    {
        var plain = await TraceViewRenderer.RenderAsync(_factory, "/Views/Recall/Trace.cshtml", null, RecallKeys.TraceRun);
        Assert.DoesNotContain("rc-open-case-modal", plain, StringComparison.Ordinal);
        Assert.Contains("\"RECALL_CASE_OPEN\":false", plain.Replace(" ", ""), StringComparison.Ordinal);
        Assert.Contains("\"RECALL_TRACE_EXPORT\":false", plain.Replace(" ", ""), StringComparison.Ordinal);

        var full = await TraceViewRenderer.RenderAsync(_factory, "/Views/Recall/Trace.cshtml", null, RecallKeys.TraceRun, RecallKeys.CasesCreate, RecallKeys.TraceExport);
        Assert.Contains("rc-open-case-modal", full, StringComparison.Ordinal);
        Assert.Contains("\"RECALL_CASE_OPEN\":true", full.Replace(" ", ""), StringComparison.Ordinal);
        Assert.Contains("\"RECALL_TRACE_EXPORT\":true", full.Replace(" ", ""), StringComparison.Ordinal);
        Assert.DoesNotContain("Tatbikat olarak kaydet", full, StringComparison.Ordinal); // D-15: not built
    }

    [Fact]
    public async Task The_panel_draws_no_dead_link_to_R7_pages()
    {
        var html = await TraceViewRenderer.RenderAsync(_factory, "/Views/Recall/Index.cshtml", null, RecallKeys.DashboardRead, RecallKeys.CasesRead);
        foreach (var r7 in new[] { "/Recall/Drills", "/Recall/Evidence", "/Recall/Settings", "/Recall/Cases/Details" })
            Assert.DoesNotContain(r7, html, StringComparison.Ordinal);
        Assert.Contains("href=\"/Recall/Cases\"", html, StringComparison.Ordinal);
        foreach (var file in Directory.EnumerateFiles(Js, "*.js"))
        {
            var code = Code(File.ReadAllText(file));
            foreach (var r7 in new[] { "/Recall/Drills", "/Recall/Evidence", "/Recall/Settings", "/Recall/Cases/Details" })
                Assert.DoesNotContain(r7, code, StringComparison.Ordinal);
        }
    }

    // ── routes = manifest ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Every_route_is_a_tenant_route_under_Recall_and_the_manifest_pages_are_the_Webs()
    {
        Assert.Equal("Recall", typeof(RecallController).GetCustomAttribute<RouteAttribute>()!.Template);
        var pages = typeof(RecallController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.ReturnType == typeof(IActionResult))
            .Select(m => "/Recall" + (m.GetCustomAttribute<HttpGetAttribute>()!.Template is { Length: > 0 } t ? "/" + t : string.Empty))
            .OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.Equal(["/Recall", "/Recall/Cases", "/Recall/Trace"], pages);

        var manifest = File.ReadAllText(Path.Combine(TraceViewRenderer.RepoRoot(), "services", "Diten.InventoryService", "src", "Diten.InventoryService.Api", "Recall",
            "RecallReadinessManifestProvider.cs"));
        var routes = Regex.Matches(manifest, @"RoutePath:\s*""([^""]+)""").Select(m => m.Groups[1].Value).OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.Equal(pages, routes);
        Assert.DoesNotContain("/Platform", manifest, StringComparison.Ordinal);
        Assert.All(Directory.EnumerateFiles(Path.Combine(Web, "Views", "Recall"), "*.cshtml").Where(f => !Path.GetFileName(f).StartsWith('_')),
            f => Assert.Contains("Layout = \"_LayoutTenantShell\"", File.ReadAllText(f), StringComparison.Ordinal));
    }
}
