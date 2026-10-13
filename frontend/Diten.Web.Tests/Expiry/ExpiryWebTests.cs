using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Controllers;
using Diten.Web.Tests.Traceability;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Diten.Web.Tests.Expiry;

/// <summary>
/// MOD-0176 F4 — the Web side of Son Kullanma / FEFO (pack §9, §11.1–§11.3, §11.6, §16 F4, §21.3), measured on the files the browser gets
/// and on the real Razor render:
/// <list type="bullet">
/// <item>vocabulary: the 7 Expiry resx carry exactly <c>Expiry.Keys</c> with real text (K3 sabotage (d): one <c>ar</c> key deleted → red);
/// every key a page or script asks for is delivered; the nav keys of the module and its 4 pages are in all 7 languages;</item>
/// <item>surface: every page is a tenant-shell page under /Expiry (never /Platform) and the manifest's routes are the Web's; UAS-001 draws
/// only the explanation for each of the 4 pages (K3 sabotage (a): the panel check removed → red);</item>
/// <item>FEFO Sırası is read-only — no reservation button, no reservation call (KF-F, D-1); "Dolduğundan beri", never "Dolalı" (D-5).</item>
/// </list>
/// </summary>
public sealed class ExpiryWebTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string[] Languages = ["ar", "en", "es", "fr", "ru", "tr", "zh"];
    private static readonly string Web = TraceViewRenderer.WebRoot;
    private static readonly string Js = Path.Combine(Web, "wwwroot", "assets", "js", "Expiry");

    /// <summary>The same word in a language and in English on purpose (loanwords, format-only templates, proper names).</summary>
    private static readonly HashSet<string> SameAsEnglishAllowed = new(StringComparer.Ordinal)
    {
        "Expiry.Col.Actions", "Expiry.Col.Exception", "Expiry.Severity.Ok", "Expiry.Modal.Description", "Expiry.Approaching.Horizon",
        "Expiry.Kpi.Lots", "Expiry.Col.Unit", "Expiry.Modal.Type.FEFO_BYPASS", "Expiry.Modal.Type.BELOW_MIN_REMAINING",
        "Expiry.Modal.Type.EXPIRY_DATE_OVERRIDE", "Expiry.Modal.Type.EXPIRED_ON_HAND", "Expiry.Order.SeeQuarantine"
    };

    /// <summary>The 4 pages of pack §11.1: view, page key (UAS-001), a marker the page must NOT draw without that key.</summary>
    public static readonly TheoryData<string, string, string> Pages = new()
    {
        { "Index", "expiry.dashboard.read", "ex-kpis" },
        { "Approaching", "expiry.dashboard.read", "dt-expiry-approaching" },
        { "Expired", "expiry.dashboard.read", "dt-expiry-expired" },
        { "FefoOrder", "expiry.fefo-order.read", "ex-order-form" }
    };

    private readonly WebApplicationFactory<Program> _factory;

    public ExpiryWebTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static Dictionary<string, string> Resx(string path)
        => XDocument.Load(path).Root!.Elements("data").ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty, StringComparer.Ordinal);

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(parts));

    // ── vocabulary ──────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Seven_languages_carry_exactly_the_Expiry_keys_with_real_text()
    {
        var keys = Diten.Web.Views.Expiry.Expiry.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
        var english = Resx(Path.Combine(Web, "Resources", "Views", "Expiry", "Expiry.en.resx"));
        foreach (var lang in Languages)
        {
            var resx = Resx(Path.Combine(Web, "Resources", "Views", "Expiry", $"Expiry.{lang}.resx"));
            Assert.Equal(keys, resx.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList());
            Assert.All(resx, p => Assert.False(string.IsNullOrWhiteSpace(p.Value), $"{lang}: {p.Key} is empty"));
            Assert.All(resx.Where(p => p.Value.Contains("{0}", StringComparison.Ordinal) != english[p.Key].Contains("{0}", StringComparison.Ordinal)),
                p => Assert.Fail($"{lang}: {p.Key} placeholders differ from en"));
            if (lang == "en") continue;
            var copies = resx.Where(p => p.Value == english[p.Key] && !SameAsEnglishAllowed.Contains(p.Key)).Select(p => p.Key).ToList();
            Assert.True(copies.Count == 0, $"{lang}: English copies {string.Join(", ", copies)}");
        }
    }

    [Fact]
    public void Every_key_a_page_or_script_asks_for_is_delivered()
    {
        var known = Diten.Web.Views.Expiry.Expiry.Keys.ToHashSet(StringComparer.Ordinal);
        var asked = Directory.EnumerateFiles(Path.Combine(Web, "Views", "Expiry"), "*.cshtml").Concat(Directory.EnumerateFiles(Js, "*.js"))
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"['""](Expiry\.[A-Za-z0-9_.]+)['""]").Select(m => m.Groups[1].Value))
            .Where(k => !k.EndsWith('.')) // prefixes built in code ("Expiry.Severity." + value) are checked below
            .Distinct().ToList();
        Assert.NotEmpty(asked);
        Assert.All(asked, k => Assert.Contains(k, known));
        foreach (var severity in new[] { "Expired", "Critical", "Approaching", "Ok" }) Assert.Contains("Expiry.Severity." + severity, known);
        foreach (var reason in new[] { "EXPIRED", "RETEST_DUE", "BLOCKED", "HELD", "QUARANTINED", "BELOW_MIN_REMAINING" }) Assert.Contains("Expiry.Reason." + reason, known);
        foreach (var code in new[] { "INVALID_REQUEST", "UNKNOWN_SKU", "PRODUCT_MASTER_UNAVAILABLE", "CROSS_LE_FORBIDDEN", "UNKNOWN_LOT", "UNKNOWN_LOCATION",
                     "EXPIRY_EXCEPTION_ALREADY_OPEN", "REASON_REQUIRED", "IDEMPOTENCY_KEY_REUSED", "COMPANY_NOT_SELECTED" })
            Assert.Contains("Expiry.Errors." + code, known);
    }

    [Fact]
    public void The_module_and_its_four_pages_have_nav_keys_in_all_seven_languages()
    {
        string[] nav = ["Nav.Module.EXPIRYFEFOMANAGEMENT", "Nav.Page.EXPIRYDASHBOARD", "Nav.Page.EXPIRYAPPROACHING", "Nav.Page.EXPIREDSTOCK", "Nav.Page.FEFOORDER"];
        foreach (var lang in Languages)
        {
            var shared = Resx(Path.Combine(Web, "Resources", $"SharedResource.{lang}.resx"));
            Assert.All(nav, k => Assert.False(string.IsNullOrWhiteSpace(shared.GetValueOrDefault(k)), $"{lang}: {k}"));
        }
    }

    // ── surface (tenant routes, UAS-001) ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Every_route_is_a_tenant_route_under_Expiry_and_the_manifest_pages_are_the_Webs()
    {
        var controller = typeof(ExpiryController);
        var prefix = controller.GetCustomAttribute<RouteAttribute>()!.Template;
        Assert.Equal("Expiry", prefix);
        var pages = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.ReturnType == typeof(IActionResult))
            .SelectMany(m => m.GetCustomAttributes<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>())
            .Select(a => "/" + prefix + (string.IsNullOrEmpty(a.Template) ? string.Empty : "/" + a.Template)).ToList();
        Assert.Equal(["/Expiry", "/Expiry/Approaching", "/Expiry/Expired", "/Expiry/FefoOrder"], pages);

        var manifest = Read(TraceViewRenderer.RepoRoot(), "services", "Diten.InventoryService", "src", "Diten.InventoryService.Api", "Expiry", "ExpiryFefoManifestProvider.cs");
        var routes = Regex.Matches(manifest, @"RoutePath:\s*""([^""]+)""").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(pages, routes);

        Assert.All(Directory.EnumerateFiles(Path.Combine(Web, "Views", "Expiry"), "*.cshtml").Where(f => !Path.GetFileName(f).StartsWith('_')),
            f => Assert.Contains("Layout = \"_LayoutTenantShell\"", File.ReadAllText(f), StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task UAS_001_without_the_page_key_only_the_access_explanation_is_drawn(string view, string pageKey, string skeleton)
    {
        var others = new[] { "expiry.dashboard.read", "expiry.fefo-order.read", "expiry.exceptions.create" }.Where(k => k != pageKey).ToArray();
        var html = await TraceViewRenderer.RenderAsync(_factory, $"/Views/Expiry/{view}.cshtml", null, others);
        Assert.Contains("diten-access-denied", html, StringComparison.Ordinal);
        Assert.DoesNotContain(skeleton, html, StringComparison.Ordinal);
        Assert.DoesNotContain("ex-tabs", html, StringComparison.Ordinal);

        var allowed = await TraceViewRenderer.RenderAsync(_factory, $"/Views/Expiry/{view}.cshtml", null, pageKey);
        Assert.DoesNotContain("diten-access-denied", allowed, StringComparison.Ordinal);
        Assert.Contains(skeleton, allowed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_lists_are_DataTable_v2_server_mode_and_buttons_follow_their_keys()
    {
        foreach (var (view, table) in new[] { ("Approaching", "dt-expiry-approaching"), ("Expired", "dt-expiry-expired") })
        {
            var html = await TraceViewRenderer.RenderAsync(_factory, $"/Views/Expiry/{view}.cshtml", null, "expiry.dashboard.read");
            Assert.Contains($"id=\"{table}\" data-dt-standard=\"v2\" data-dt-data-mode=\"server\"", html, StringComparison.Ordinal);
            Assert.Contains("id=\"expiry-l10n\"", html, StringComparison.Ordinal);
            Assert.Contains("id=\"ex-export\"", html, StringComparison.Ordinal);
            Assert.DoesNotContain("id=\"ex-open-modal\"", html, StringComparison.Ordinal); // no expiry.exceptions.create → no "İstisna aç"
            Assert.Contains("\"EXPIRY_EXCEPTION_OPEN\":false", html, StringComparison.Ordinal);

            var opener = await TraceViewRenderer.RenderAsync(_factory, $"/Views/Expiry/{view}.cshtml", null, "expiry.dashboard.read", "expiry.exceptions.create");
            Assert.Contains("id=\"ex-open-modal\"", opener, StringComparison.Ordinal);
            Assert.Contains("\"EXPIRY_EXCEPTION_OPEN\":true", opener, StringComparison.Ordinal);
        }

        foreach (var script in new[] { "approaching.js", "expired.js" })
        {
            Assert.Contains("dataMode: 'server'", Read(Js, script), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task The_FEFO_order_is_read_only_and_the_wording_follows_21_3()
    {
        var order = await TraceViewRenderer.RenderAsync(_factory, "/Views/Expiry/FefoOrder.cshtml", null, "expiry.fefo-order.read");
        var script = Read(Js, "fefo-order.js");
        foreach (var source in new[] { order, script })
        {
            // no control that reserves (a button / link / input whose attributes or text speak of reserving) and no reservation call
            Assert.DoesNotMatch(@"(?i)<(button|a|input)[^>]*(reserv|rezerv)[^>]*>", source);
            Assert.DoesNotMatch(@"(?i)<(button|a)[^>]*>[^<]*(reserv|rezerv)", source);
            Assert.DoesNotContain("/reservations", source, StringComparison.OrdinalIgnoreCase);
        }
        Assert.DoesNotContain(Diten.Web.Views.Expiry.Expiry.Keys, k => k.Contains("Reserv", StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain("'POST'", script, StringComparison.Ordinal);
        var tr = Resx(Path.Combine(Web, "Resources", "Views", "Expiry", "Expiry.tr.resx"));
        Assert.Equal("Dolduğundan beri", tr["Expiry.Col.SinceExpiry"]);
        Assert.DoesNotContain(tr.Values, v => v.Contains("Dolalı", StringComparison.Ordinal));
        Assert.Equal("Retest tarihi geçti", tr["Expiry.Reason.RETEST_DUE"]);
    }

    [Fact]
    public void The_proxy_forwards_bodies_without_logging_them_and_writes_carry_the_antiforgery_token()
    {
        var source = Read(Web, "Controllers", "ExpiryController.cs");
        Assert.Contains("[ValidateAntiForgeryToken]", source, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"Log\w*\([^;]*body", source);
        Assert.Contains("RequestVerificationToken", Read(Js, "expiry-ui.js"), StringComparison.Ordinal);
        Assert.DoesNotContain("\"/Platform", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Route(\"Platform", source, StringComparison.Ordinal);
    }
}
