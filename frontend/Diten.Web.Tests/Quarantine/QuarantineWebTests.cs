using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Controllers;
using Diten.Web.Tests.Traceability;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Diten.Web.Tests.Quarantine;

/// <summary>
/// MOD-0175 Q5 — the Web side of Karantina / Bloke Stok (pack §9, §11, §14, §16 Q5, §21.3), measured on the files the browser gets
/// and on the real Razor render:
/// <list type="bullet">
/// <item>vocabulary: the 7 Quarantine resx carry exactly <c>Quarantine.Keys</c> with real text (K3 sabotage (c): one <c>zh</c> key
/// deleted → red); every error code of the frozen contract has its sentence; the Lots page keys of §11.4 are in all 7 languages;</item>
/// <item>surface: every page is a tenant-shell page under /Quarantine (never /Platform — K3 sabotage (a)) and the manifest's routes are
/// the Web's; UAS-001 draws only the explanation for each of the 8 pages (K3 sabotage (b): the Karar Ver check removed → red);</item>
/// <item>signature: 0175 draws no password field (KQ-G) and signs only through MOD-0022's dialog with the CT-D17 subject; a
/// cancelled signature sends nothing;</item>
/// <item>Lots (MOD-0174 Web, §11.4): a 202 PendingApproval answers "Onaya gönderildi" with the Karar Gönder link, and the lot page
/// reads getLotQualityStatus for its "Onay bekliyor" badge.</item>
/// </list>
/// </summary>
public sealed class QuarantineWebTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string[] Languages = ["ar", "en", "es", "fr", "ru", "tr", "zh"];
    private static readonly string Web = TraceViewRenderer.WebRoot;
    private static readonly string Js = Path.Combine(Web, "wwwroot", "assets", "js", "Quarantine");

    /// <summary>The same word in a language and in English on purpose (the loanword "Disposition", "Lot", format-only templates…).</summary>
    private static readonly HashSet<string> SameAsEnglishAllowed = new(StringComparer.Ordinal)
    {
        "Quarantine.Col.Lot", "Quarantine.BlockStock.Lot", "Quarantine.DecisionKind.Disposition", "Quarantine.Disposition.Step",
        "Quarantine.Disposition.Field.Kind", "Quarantine.Disposition.Move.Line", "Quarantine.Col.Actions", "Quarantine.Col.Date",
        "Quarantine.Source.Production", "Quarantine.Workflow.Box", "Quarantine.Workflow.Instance", "Quarantine.BlockStock.Note",
        "Quarantine.Settings.Approvers.Kind", "Quarantine.Settings.BR.Code", "Quarantine.Common.No", "Quarantine.Source.Manual",
        "Quarantine.Settings.Section.General"
    };

    /// <summary>The 8 pages of pack §11.1: view, page key (UAS-001), markers the page must NOT draw without that key.</summary>
    public static readonly TheoryData<string, string, string> Pages = new()
    {
        { "Index", "quarantine.holds.read", "dt-quarantine-holds" },
        { "Details", "quarantine.holds.read", "qr-wf-refresh" },
        { "Decide", "quarantine.decisions.create", "qr-decide-form" },
        { "Blocked", "quarantine.holds.read", "dt-quarantine-blocked" },
        { "Disposition", "quarantine.blocked.dispose", "qr-disposition-form" },
        { "Retest", "quarantine.holds.read", "dt-quarantine-retest" },
        { "Submit", "quarantine.decisions.create", "qr-sign-step" },
        { "Settings", "quarantine.settings.manage", "qr-settings-form" }
    };

    private readonly WebApplicationFactory<Program> _factory;

    public QuarantineWebTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static Dictionary<string, string> Resx(string path)
        => XDocument.Load(path).Root!.Elements("data").ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty, StringComparer.Ordinal);

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(parts));

    private static string Code(string source) => Regex.Replace(Regex.Replace(source, @"/\*[\s\S]*?\*/", string.Empty), @"(?m)^\s*//.*$", string.Empty);

    // ── vocabulary ──────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Seven_languages_carry_exactly_the_Quarantine_keys_with_real_text()
    {
        var folder = Path.Combine(Web, "Resources", "Views", "Quarantine");
        Assert.Equal(Languages.Select(l => $"Quarantine.{l}.resx"), Directory.EnumerateFiles(folder, "Quarantine.*.resx").Select(Path.GetFileName).OrderBy(f => f, StringComparer.Ordinal));
        var keys = Diten.Web.Views.Quarantine.Quarantine.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.True(keys.Count > 350, $"too few keys: {keys.Count}");
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
        var en = Resx(Path.Combine(folder, "Quarantine.en.resx"));
        foreach (var lang in Languages)
        {
            var values = Resx(Path.Combine(folder, $"Quarantine.{lang}.resx"));
            Assert.True(keys.SequenceEqual(values.Keys.OrderBy(k => k, StringComparer.Ordinal)), $"{lang}: the key set differs from Quarantine.Keys "
                + $"(missing: {string.Join(", ", keys.Except(values.Keys))}; extra: {string.Join(", ", values.Keys.Except(keys))})");
            Assert.All(values, kv => Assert.False(string.IsNullOrWhiteSpace(kv.Value), $"{lang}: {kv.Key} is empty"));
            if (lang != "en")
            {
                var copies = values.Where(kv => kv.Value == en[kv.Key] && !SameAsEnglishAllowed.Contains(kv.Key)).Select(kv => kv.Key).ToList();
                Assert.True(copies.Count == 0, $"{lang} copies English: {string.Join(", ", copies)}");
            }
        }
    }

    [Fact]
    public void Every_error_code_of_the_frozen_contract_has_its_sentence()
    {
        var contract = File.ReadAllText(Directory.EnumerateFiles(Path.Combine(TraceViewRenderer.RepoRoot(), "docs"), "quarantine.openapi.yaml", SearchOption.AllDirectories).Single());
        var codes = Regex.Matches(contract, @"\b(QUARANTINE_[A-Z_]*[A-Z]|LOT_[A-Z_]*[A-Z]|INSUFFICIENT_STOCK|POSTING_PERIOD_CLOSED|UNKNOWN_LOCATION|UNKNOWN_LOT|CROSS_LE_FORBIDDEN|IDEMPOTENCY_KEY_REUSED|REASON_REQUIRED|INVALID_REQUEST)\b")
            .Select(m => m.Value).Where(c => c != "QUARANTINE_BLOCK" && c != "QUARANTINE_HOLD").Distinct().ToList();
        Assert.True(codes.Count >= 40, $"too few codes read from the contract: {codes.Count}");
        var keys = Diten.Web.Views.Quarantine.Quarantine.Keys.ToHashSet(StringComparer.Ordinal);
        Assert.All(codes, code => Assert.True(keys.Contains($"Quarantine.Errors.{code}"), $"no sentence for {code}"));
    }

    [Fact]
    public void Every_key_a_page_or_script_asks_for_is_delivered()
    {
        var keys = Diten.Web.Views.Quarantine.Quarantine.Keys.ToHashSet(StringComparer.Ordinal);
        var sources = Directory.EnumerateFiles(Path.Combine(Web, "Views", "Quarantine"), "*.cshtml").Concat(Directory.EnumerateFiles(Js, "*.js"));
        foreach (var file in sources)
        {
            foreach (Match m in Regex.Matches(Code(File.ReadAllText(file)), @"[""'](Quarantine\.[A-Z][A-Za-z0-9_.]*[A-Za-z0-9_])[""']"))
            {
                Assert.True(keys.Contains(m.Groups[1].Value), $"{Path.GetFileName(file)} asks for {m.Groups[1].Value}, which is not delivered");
            }
        }
    }

    [Fact]
    public void The_Lots_page_keys_and_the_quarantine_nav_keys_are_in_all_seven_languages()
    {
        string[] lots = ["Status.Dialog.Pending.Title", "Status.Dialog.Pending.Text", "Status.Dialog.Pending.Link", "Quality.PendingApproval", "Quality.PendingApproval.Title"];
        string[] nav = ["Nav.Module.QUARANTINEBLOCKEDSTOCK", "Nav.Page.QUARANTINEQUEUE", "Nav.Page.BLOCKEDSTOCK", "Nav.Page.RETESTQUEUE", "Nav.Page.QUARANTINESETTINGS"];
        Assert.All(lots, k => Assert.Contains(k, Diten.Web.Models.Traceability.TraceL10nKeys.Lots));
        foreach (var lang in Languages)
        {
            var lotsResx = Resx(Path.Combine(Web, "Resources", "Views", "Traceability", "Lots", $"LotsIndex.{lang}.resx"));
            var shared = Resx(Path.Combine(Web, "Resources", $"SharedResource.{lang}.resx"));
            Assert.All(lots, k => Assert.False(string.IsNullOrWhiteSpace(lotsResx.GetValueOrDefault(k)), $"{lang}: {k}"));
            Assert.All(nav, k => Assert.False(string.IsNullOrWhiteSpace(shared.GetValueOrDefault(k)), $"{lang}: {k}"));
        }
    }

    // ── surface (tenant routes, UAS-001) ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Every_route_is_a_tenant_route_under_Quarantine_and_the_manifest_pages_are_the_Webs()
    {
        var controller = typeof(QuarantineController);
        var prefix = controller.GetCustomAttribute<RouteAttribute>()!.Template;
        Assert.Equal("Quarantine", prefix);
        var pages = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.ReturnType == typeof(IActionResult))
            .SelectMany(m => m.GetCustomAttributes<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>())
            .Select(a => "/" + prefix + (string.IsNullOrEmpty(a.Template) ? string.Empty : "/" + a.Template)).ToList();
        Assert.Equal(8, pages.Count);
        Assert.All(pages, p => Assert.False(p.StartsWith("/Platform", StringComparison.OrdinalIgnoreCase), p));

        // the manifest (InventoryService, pack §14) names the routes this controller serves
        var manifest = Read(TraceViewRenderer.RepoRoot(), "services", "Diten.InventoryService", "src", "Diten.InventoryService.Api", "ModuleRegistration", "QuarantineBlockedStockManifestProvider.cs");
        var routes = Regex.Matches(manifest, @"RoutePath:\s*""([^""]+)""").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(8, routes.Count);
        foreach (var route in routes)
        {
            Assert.StartsWith("/Quarantine", route, StringComparison.Ordinal);
            var served = pages.Any(p => Regex.Replace(p, @"/\{[^}]+\}", string.Empty).Equals(route, StringComparison.Ordinal)
                                        || p.Replace("/{decisionId}", string.Empty).Equals(route, StringComparison.Ordinal));
            Assert.True(served, $"manifest route {route} has no page in QuarantineController");
        }

        Assert.All(Directory.EnumerateFiles(Path.Combine(Web, "Views", "Quarantine"), "*.cshtml").Where(f => !Path.GetFileName(f).StartsWith('_')),
            f => Assert.Contains("Layout = \"_LayoutTenantShell\"", File.ReadAllText(f), StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task UAS_001_without_the_page_key_only_the_access_explanation_is_drawn(string view, string pageKey, string skeleton)
    {
        var others = new[] { "quarantine.holds.read", "quarantine.decisions.create", "quarantine.blocked.dispose", "quarantine.settings.manage" }
            .Where(k => k != pageKey).ToArray();
        var model = view is "Index" or "Blocked" or "Retest" or "Settings" ? null : Guid.NewGuid().ToString("D");
        var html = await TraceViewRenderer.RenderAsync(_factory, $"/Views/Quarantine/{view}.cshtml", model, others);
        Assert.Contains("diten-access-denied", html, StringComparison.Ordinal);
        Assert.DoesNotContain(skeleton, html, StringComparison.Ordinal);
        Assert.DoesNotContain("qr-tabs", html, StringComparison.Ordinal);

        var allowed = await TraceViewRenderer.RenderAsync(_factory, $"/Views/Quarantine/{view}.cshtml", model, pageKey);
        Assert.DoesNotContain("diten-access-denied", allowed, StringComparison.Ordinal);
        Assert.Contains(skeleton, allowed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_three_lists_are_DataTable_v2_server_mode_and_buttons_follow_their_keys()
    {
        foreach (var (view, table) in new[] { ("Index", "dt-quarantine-holds"), ("Blocked", "dt-quarantine-blocked"), ("Retest", "dt-quarantine-retest") })
        {
            var html = await TraceViewRenderer.RenderAsync(_factory, $"/Views/Quarantine/{view}.cshtml", null, "quarantine.holds.read");
            Assert.Contains($"id=\"{table}\" data-dt-standard=\"v2\" data-dt-data-mode=\"server\"", html, StringComparison.Ordinal);
            Assert.Contains("id=\"quarantine-l10n\"", html, StringComparison.Ordinal);
        }

        // Kayıt Detayı with read only: no "Karar Ver", no withdraw, no re-apply, no review (absent, not disabled)
        var details = await TraceViewRenderer.RenderAsync(_factory, "/Views/Quarantine/Details.cshtml", Guid.NewGuid().ToString("D"), "quarantine.holds.read");
        foreach (var button in new[] { "qr-d-decide", "qr-wf-withdraw", "qr-wf-reapply", "qr-d-review" })
        {
            Assert.DoesNotContain($"id=\"{button}\"", details, StringComparison.Ordinal);
        }

        Assert.Contains("id=\"qr-wf-refresh\"", details, StringComparison.Ordinal);
        foreach (var script in new[] { "queue.js", "blocked.js", "retest.js" })
        {
            Assert.Contains("dataMode: 'server'", Read(Js, script), StringComparison.Ordinal);
        }
    }

    // ── signature (KQ-G, CT-D17) ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Quarantine_draws_no_password_field_and_signs_only_through_the_MOD_0022_dialog()
    {
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Web, "Views", "Quarantine"), "*.cshtml").Concat(Directory.EnumerateFiles(Js, "*.js")))
        {
            var text = Code(File.ReadAllText(file));
            Assert.DoesNotMatch(new Regex(@"type\s*=\s*[""']password|autocomplete\s*=\s*[""']current-password|localStorage|sessionStorage", RegexOptions.IgnoreCase), text);
        }

        var ui = Code(Read(Js, "quarantine-ui.js"));
        Assert.Matches(new Regex(@"ESign\.open\(\{\s*module:\s*'quarantine',\s*subjectType:\s*'quarantine\.decision',\s*subjectId:\s*decision\.decisionId"), ui);
        // a cancelled dialog sends nothing: the return happens BEFORE the submit call
        var cancel = ui.IndexOf("if (!signature?.signatureId) return { ok: false, cancelled: true };", StringComparison.Ordinal);
        var submit = ui.IndexOf("decisions/${encodeURIComponent(decision.decisionId)}/submit", StringComparison.Ordinal);
        Assert.True(cancel > 0 && submit > cancel, "the signature must be taken (or the flow stopped) before submitDecision");
        Assert.Matches(new Regex(@"if \(await signatureRequired\(decision\)\)"), ui);
        // the draft has no signatureRequired yet: the MOD-0022 effective policy is read, and an unreadable one counts as required
        Assert.Matches(new Regex(@"decision\.signatureRequired = !\(reply\.ok && policy && policy\.required === false\)"), ui);
        foreach (var page in new[] { "Decide", "Disposition", "Submit", "Blocked" })
        {
            Assert.Contains("~/Views/Shared/ESignature/_ESignatureDialog.cshtml", Read(Web, "Views", "Quarantine", $"{page}.cshtml"), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_proxy_forwards_bodies_without_logging_them_and_writes_carry_the_antiforgery_token()
    {
        var controller = Code(Read(Web, "Controllers", "QuarantineController.cs"));
        foreach (Match log in Regex.Matches(controller, @"_logger\.Log[A-Za-z]*\(([\s\S]*?)\);"))
        {
            Assert.DoesNotMatch(new Regex(@"\b(body|Body|text|Content)\b"), log.Groups[1].Value);
        }

        var write = typeof(QuarantineController).GetMethod(nameof(QuarantineController.ProxyWrite))!;
        Assert.NotNull(write.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        Assert.Equal(new[] { "POST", "PUT", "PATCH" }, write.GetCustomAttributes<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>().SelectMany(a => a.HttpMethods).OrderBy(m => m == "POST" ? 0 : m == "PUT" ? 1 : 2));
    }

    // ── Lots (MOD-0174 Web, pack §11.4) ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_Lots_page_answers_a_202_with_the_submit_link_and_shows_the_pending_badge()
    {
        var controller = Code(Read(Web, "Controllers", "LotsController.cs"));
        Assert.Matches(new Regex(@"reply\.StatusCode == StatusCodes\.Status202Accepted[\s\S]*?pending = true[\s\S]*?/Quarantine/Decisions/\{Uri\.EscapeDataString\(pendingRequestId\)\}/Submit"), controller);
        var status = Code(Read(Web, "wwwroot", "assets", "js", "Traceability", "Lots", "status.js"));
        Assert.True(status.IndexOf("result.json?.pending", StringComparison.Ordinal) is > 0 and var p
                    && p < status.IndexOf("t('Status.Dialog.Done')", StringComparison.Ordinal), "the 202 branch must come before the 200 one");
        Assert.Contains("Status.Dialog.Pending.Link", status, StringComparison.Ordinal);
        var details = Code(Read(Web, "wwwroot", "assets", "js", "Traceability", "Lots", "details.js"));
        Assert.Contains("/Quarantine/api/lots/${encodeURIComponent(lotId)}/quality-status", details, StringComparison.Ordinal);
        Assert.Contains("pendingLotRelease", details, StringComparison.Ordinal);
    }
}
