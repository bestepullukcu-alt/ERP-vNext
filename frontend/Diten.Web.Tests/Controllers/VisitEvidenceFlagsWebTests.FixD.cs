using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.Web.Controllers.CRM;
using Diten.Web.Views.CRM.VisitEvidenceSettings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VW-W3E · W3E-E4R2-FIX-d (U-7, K-STD, K-TABS) on the REAL views, scripts, resx and controller of the visit evidence
/// settings: the settings tab's inner panes (form · history · reps) on the shared tab card; the change history and the rep
/// roster as Golden Compact DataTables in SERVER mode (settings-tables.js — the factory's own wire pieces, FIX-c's method);
/// the Web proxy turning the list contract (start · length · search) into the CRM's (page · pageSize · q) with the totals —
/// more than 20 reps are paged, never silently cut (the picker read stays at 20) —; the history export; UAS-001; seven
/// languages; the page's tokens only.
/// </summary>
public sealed partial class VisitEvidenceFlagsWebTests
{
    private const string PolicyRead = "crm.visit-evidence-policy.read";
    private const string PolicyManage = "crm.visit-evidence-policy.manage";

    private static string VeView(string file) => File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "VisitEvidenceSettings", file)).Replace("\r\n", "\n");

    private static string VeScript(string file) => File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", "VisitEvidenceSettings", file)).Replace("\r\n", "\n");

    // ═══ the inner tab card ════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void FixD1_the_inner_panes_are_the_shared_tab_card_and_the_hand_made_strip_is_gone()
    {
        var view = VeView("Index.cshtml");
        // the three panes on the SAME component as the outer tabs, with the asked icons
        Assert.Contains("new() { Key = \"form\", Id = \"ve-ptab-form\", Icon = \"bx-slider-alt\", Text = Localizer[\"PaneForm\"].Value, Active = true, Controls = \"ve-pane-form\" }", view);
        Assert.Contains("new() { Key = \"history\", Id = \"ve-ptab-history\", Icon = \"bx-history\", Text = Localizer[\"PaneHistory\"].Value, Controls = \"ve-pane-history\" }", view);
        Assert.Contains("new() { Key = \"reps\", Id = \"ve-ptab-reps\", Icon = \"bx-group\", Text = Localizer[\"PaneReps\"].Value, Controls = \"ve-pane-reps\" }", view);
        Assert.Contains("<partial name=\"~/Views/Shared/Components/Tabs/_TabCard.cshtml\" model=\"paneCard\" />", view);
        // a second card INSIDE the outer tab's content: after the outer card, inside the settings pane
        var settingsPane = view.IndexOf("id=\"ve-pane-settings\"", StringComparison.Ordinal);
        var exceptionsPane = view.IndexOf("id=\"ve-pane-exceptions\"", StringComparison.Ordinal);
        var inner = view.IndexOf("model=\"paneCard\"", StringComparison.Ordinal);
        Assert.True(view.IndexOf("model=\"tabCard\"", StringComparison.Ordinal) < settingsPane && settingsPane < inner && inner < exceptionsPane);
        // each pane is a tab-pane its tab controls (diten-tabs.js shows it); the form shows first
        Assert.Contains("<div class=\"tab-pane fade show active\" id=\"ve-pane-form\" role=\"tabpanel\" aria-labelledby=\"ve-ptab-form\">", view);
        Assert.Contains("<div class=\"tab-pane fade\" id=\"ve-pane-history\" role=\"tabpanel\" aria-labelledby=\"ve-ptab-history\">", view);
        Assert.Contains("<div class=\"tab-pane fade\" id=\"ve-pane-reps\" role=\"tabpanel\" aria-labelledby=\"ve-ptab-reps\">", view);
        Assert.Contains("<div class=\"tab-content p-0\" id=\"ve-scope-panes\">", view);
        // the former strip: nowhere (view, script, styles)
        foreach (var text in new[] { view, VeScript("visit-evidence-settings.js"), VeScript("visit-evidence-settings.css") })
        {
            Assert.DoesNotMatch(@"class=""[^""]*\bve-pane-tab|\.ve-pane-tab\b|class=""[^""]*\bve-panes\b|\.ve-panes\b|data-pane=|\[data-pane\]", text);
        }

        // the script follows the shared tab card's event (both cards) — no click handler of its own
        var js = VeScript("visit-evidence-settings.js");
        Assert.Contains("if (key === 'form' || key === 'history' || key === 'reps') { setPane(key); }", js);
        Assert.Contains("<script src=\"~/assets/js/CRM/VisitEvidenceSettings/settings-tables.js\" asp-append-version=\"true\"></script>\n    <script src=\"~/assets/js/CRM/VisitEvidenceSettings/visit-evidence-settings.js\"", view);
    }

    // ═══ the two tables ════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void FixD2_history_and_reps_are_v2_server_shells_without_selection_or_actions()
    {
        foreach (var (file, id, slug, headers) in new[]
                 {
                     ("_HistoryTable.cshtml", "dt-ve-history", "ve-history", new[] { "ColDate", "ColScope", "ColPerson", "ColField", "ColChange" }), // WP-VW-W3L: + the scope
                     ("_RepsTable.cshtml", "dt-ve-reps", "ve-reps", new[] { "ColRep", "ColScope", "ColLocation", "ColPhoto", "ColQr", "ColDevice" })
                 })
        {
            var shell = VeView(file);
            Assert.Contains("TableId = \"" + id + "\"", shell);
            Assert.Contains("DataMode = \"server\"", shell);
            Assert.Contains("Slug = \"" + slug + "\"", shell);
            Assert.Contains("HasSelection = false", shell);
            Assert.Contains("ActionsHeader = null", shell);
            Assert.Contains("<partial name=\"~/Views/Shared/Components/DataTable/_ListShell.cshtml\"", shell); // v2 + the shaped placeholder
            Assert.Equal(headers, Regex.Matches(shell, "Localizer\\[\"([A-Za-z]+)\"\\]\\.Value").Select(m => m.Groups[1].Value).ToArray());
        }

        // the hand tables are gone; the shells sit in their panes
        var view = VeView("Index.cshtml");
        Assert.DoesNotContain("ve-rep-rows", view);
        Assert.DoesNotContain("ve-history-rows", view);
        Assert.Contains("<partial name=\"~/Views/CRM/VisitEvidenceSettings/_RepsTable.cshtml\" />", view);

        // FIX-c's method: the factory's table builder + its OWN wire pieces; the shared file untouched; FU-DT-SKELETON-ID
        var tables = VeScript("settings-tables.js");
        Assert.Contains("D.createCrudTable({", tables);
        Assert.Contains("D.toServerQuery(request, [], {})", tables);
        Assert.Contains("D.toDataTablesResponse(json, drawOf(this, lastDraw))", tables);
        Assert.Contains("D.toExportQuery({}, [], {}, format, keys)", tables);
        Assert.DoesNotContain(".createList(", tables);                                       // the page's one createList is the exceptions list
        Assert.Contains("card.querySelector('[data-table-skeleton]')", tables);
        Assert.Equal(2, Regex.Matches(tables, @"dropOwnSkeleton\(o\.tableEl\);\n\s*dt = D\.createCrudTable").Count);
        Assert.DoesNotContain("VisitEvidenceSettings", File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "js", "diten-datatable.js")));
    }

    [Fact]
    public void FixD3_the_smoke_builds_each_table_when_its_pane_shows_in_server_mode_and_measures_it_on_tab_change()
    {
        var r = SettingsSmoke("qr");
        if (r is null) { return; }
        var s = r.Value;
        Assert.Equal(JsonValueKind.Null, s.GetProperty("createError").ValueKind);
        Assert.Empty(s.GetProperty("builtBeforePane").EnumerateArray());                 // nothing drawn while hidden
        Assert.Equal(new[] { "dt-ve-reps", "dt-ve-history" }, s.GetProperty("tablesBuilt").EnumerateArray().Select(x => x.GetString()).ToArray());

        Assert.Equal("server", s.GetProperty("repsDataMode").GetString());
        Assert.True(s.GetProperty("repsServerSide").GetBoolean());
        Assert.True(s.GetProperty("repsSearching").GetBoolean());
        Assert.False(s.GetProperty("repsExportPermitted").GetBoolean());
        Assert.Equal("server", s.GetProperty("historyDataMode").GetString());
        Assert.True(s.GetProperty("historyServerSide").GetBoolean());
        Assert.False(s.GetProperty("historySearching").GetBoolean());

        // the wire: the list contract (start · length · draw), the answer's draw kept
        Assert.Equal("/api/policy/reps/roster?start=0&length=10&draw=1", s.GetProperty("repsFirst").GetProperty("url").GetString());
        Assert.Equal(1, s.GetProperty("repsFirst").GetProperty("draw").GetInt32());
        Assert.Equal("/api/policy/history?start=0&length=10&draw=1", s.GetProperty("historyFirst").GetProperty("url").GetString());
        Assert.Equal(3, s.GetProperty("historyFirst").GetProperty("total").GetInt32());

        // diten:tab-change: each pane's table measured when it shows, again when the settings tab comes back
        Assert.True(s.GetProperty("repsAdjusted").GetInt32() >= 1);
        Assert.True(s.GetProperty("historyAdjusted").GetInt32() >= 1);
        Assert.True(s.GetProperty("repsAdjustedAfterSettings").GetInt32() > s.GetProperty("repsAdjusted").GetInt32());
    }

    [Fact]
    public void FixD4_more_than_20_reps_page_with_the_right_total_the_search_asks_the_crm_and_the_exception_mark_stays()
    {
        var r = SettingsSmoke("qr");
        if (r is null) { return; }
        var s = r.Value;
        var tr = SettingsResx("tr");
        var first = s.GetProperty("repsFirst");
        Assert.Equal((26, 26, 10), (first.GetProperty("total").GetInt32(), first.GetProperty("filtered").GetInt32(), first.GetProperty("rows").GetInt32()));
        var third = s.GetProperty("repsPage3");                                          // reps 21–26: past the picker's 20
        Assert.Equal("/api/policy/reps/roster?start=20&length=10&draw=2", third.GetProperty("url").GetString());
        Assert.Equal((6, 26), (third.GetProperty("rows").GetInt32(), third.GetProperty("total").GetInt32()));
        Assert.Contains("Temsilci 23", third.GetProperty("html").GetString());
        var search = s.GetProperty("repsSearch");
        Assert.Equal("/api/policy/reps/roster?start=0&length=10&search=elif&draw=3", search.GetProperty("url").GetString());
        Assert.Equal((1, 26, 1), (search.GetProperty("rows").GetInt32(), search.GetProperty("total").GetInt32(), search.GetProperty("filtered").GetInt32()));

        // the cells: the page's rule per row over the exception list it already reads (no read per rep)
        var reps = s.GetProperty("reps").GetString()!;
        Assert.Contains("<td class=\"ve-col-new\">" + Encode(tr["Opt_qrMode_optional"]) + " <span class=\"ve-mu\">· " + Encode(tr["SourceExceptionShort"]) + "</span></td>", reps);
        Assert.Contains("<td>" + Encode(tr["Opt_locationMode_info"]) + " <span class=\"ve-mu\">· " + Encode(tr["SourceExceptionShort"]) + "</span></td>", search.GetProperty("html").GetString());
        Assert.Contains("<td class=\"ve-col-new\">" + Encode(tr["Opt_qrMode_required"]) + "</td>", reps); // no exception: the tenant value
    }

    // ═══ the proxy ═════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task FixD5_the_history_proxy_turns_start_length_into_page_page_size_and_refuses_before_the_crm()
    {
        const string Answer = "{\"data\":{\"items\":[{\"at\":\"2026-10-14T09:12:00Z\",\"by\":\"u2\",\"field\":\"qrMode\",\"oldValue\":\"off\",\"newValue\":\"required\",\"kind\":\"set\"}],\"total\":37,\"page\":3,\"pageSize\":10,\"displayNames\":{\"u2\":\"Ayşe Demir\"}}}";
        var (gateway, controller) = VeArrange([PolicyRead], _ => Answer);
        var ok = Assert.IsType<OkObjectResult>(await controller.History(20, 10, null, null, null, CancellationToken.None));
        Assert.Equal(GatewayUrl + "/api/crm/visit-evidence-policy/history?page=3&pageSize=10", Assert.Single(gateway.Requests));
        var body = JsonSerializer.SerializeToElement(ok.Value).GetProperty("data");
        Assert.Equal((37, 37), (body.GetProperty("total").GetInt32(), body.GetProperty("filteredTotal").GetInt32()));
        Assert.Equal("Ayşe Demir", body.GetProperty("displayNames").GetProperty("u2").GetString());
        Assert.Single(body.GetProperty("items").EnumerateArray());

        var (g2, c2) = VeArrange([PolicyManage], _ => Answer);
        await c2.History(0, 25, null, null, null, CancellationToken.None);
        await c2.History(100, 100, "", null, null, CancellationToken.None);
        Assert.Equal(new[] { GatewayUrl + "/api/crm/visit-evidence-policy/history?page=1&pageSize=25", GatewayUrl + "/api/crm/visit-evidence-policy/history?page=2&pageSize=100" }, g2.Requests.ToArray());

        var (g3, c3) = VeArrange([PolicyRead], _ => Answer);
        Assert.Equal(400, Status(await c3.History(0, 0, null, null, null, CancellationToken.None)));
        Assert.Equal(400, Status(await c3.History(0, 101, null, null, null, CancellationToken.None)));
        Assert.Equal(400, Status(await c3.History(0, 10, "x", null, null, CancellationToken.None)));
        Assert.Equal(400, Status(await c3.History(0, 10, null, "at", "asc", CancellationToken.None)));
        Assert.Equal(400, Status(await c3.History(0, 10, null, null, "up", CancellationToken.None)));
        Assert.Empty(g3.Requests);

        // a CRM answer without the page shape is not passed on as a page (502), never a silent empty list
        var (_, c4) = VeArrange([PolicyRead], _ => "{\"data\":{}}");
        Assert.Equal(502, Status(await c4.History(0, 10, null, null, null, CancellationToken.None)));
        // UAS-001 / the key: nothing asked
        var (g5, c5) = VeArrange(["crm.visit-evidence-exception.read"], _ => Answer);
        Assert.Equal(403, Status(await c5.History(0, 10, null, null, null, CancellationToken.None)));
        Assert.Empty(g5.Requests);
    }

    [Fact]
    public async Task FixD6_the_roster_proxy_pages_26_reps_with_the_whole_total_and_the_search_as_q()
    {
        static string Page(int from, int count, long total) => "{\"data\":{\"items\":[" + string.Join(",", Enumerable.Range(from, count).Select(i =>
            "{\"resourceId\":\"rep-" + i + "\",\"displayName\":\"Temsilci " + i + "\"}")) + "],\"total\":" + total + ",\"page\":1,\"pageSize\":10}}";
        var (gateway, controller) = VeArrange([PolicyRead], path => path.Contains("q=", StringComparison.Ordinal) ? Page(0, 1, 1) : path.Contains("pageSize=1", StringComparison.Ordinal) && !path.Contains("pageSize=10", StringComparison.Ordinal) ? Page(0, 1, 26) : Page(20, 6, 26));

        // page 3 of 10 — reps 21–26, the total 26 (the picker's 20 would have cut it)
        var third = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await controller.Roster(20, 10, null, null, null, CancellationToken.None)).Value).GetProperty("data");
        Assert.Equal(GatewayUrl + "/api/crm/visit-evidence-policy/reps/roster?page=3&pageSize=10", Assert.Single(gateway.Requests));
        Assert.Equal((26, 26, 6), (third.GetProperty("total").GetInt32(), third.GetProperty("filteredTotal").GetInt32(), third.GetProperty("items").GetArrayLength()));

        // a search: q (trimmed, escaped) for the matching page, one more one-row read for the whole roster's total
        gateway.Requests.Clear();
        var found = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await controller.Roster(0, 10, " Elif Ş ", null, null, CancellationToken.None)).Value).GetProperty("data");
        Assert.Equal(new[] { GatewayUrl + "/api/crm/visit-evidence-policy/reps/roster?q=Elif%20%C5%9E&page=1&pageSize=10", GatewayUrl + "/api/crm/visit-evidence-policy/reps/roster?page=1&pageSize=1" }, gateway.Requests.ToArray());
        Assert.Equal((26, 1), (found.GetProperty("total").GetInt32(), found.GetProperty("filteredTotal").GetInt32()));

        // the refusals never reach the CRM; the key
        var (g2, c2) = VeArrange([PolicyRead], _ => Page(0, 1, 1));
        Assert.Equal(400, Status(await c2.Roster(0, 0, null, null, null, CancellationToken.None)));
        Assert.Equal(400, Status(await c2.Roster(0, 101, null, null, null, CancellationToken.None)));
        Assert.Equal(400, Status(await c2.Roster(0, 10, null, "displayName", "asc", CancellationToken.None)));
        Assert.Empty(g2.Requests);
        var (g3, c3) = VeArrange(["crm.visit-evidence-exception.manage"], _ => Page(0, 1, 1));
        Assert.Equal(403, Status(await c3.Roster(0, 10, null, null, null, CancellationToken.None)));
        Assert.Empty(g3.Requests);
        Assert.Equal("/api/crm/visit-evidence-policy/reps/roster?page=1&pageSize=100", VisitEvidenceSettingsController.RosterPath("  ", 0, 500));

        // the picker read beside it is the plain pass-through it was (the pickers keep their 20)
        var (g4, c4) = VeArrange([PolicyRead], _ => "{\"data\":[]}");
        c4.ControllerContext.HttpContext.Request.QueryString = new QueryString("?q=el");
        Assert.Equal(200, Status(await c4.Reps(CancellationToken.None)));
        Assert.Equal(GatewayUrl + "/api/crm/visit-evidence-policy/reps?q=el", Assert.Single(g4.Requests));
    }

    [Fact]
    public async Task FixD7_the_history_export_writes_every_change_in_the_readers_words_and_refuses_over_50000()
    {
        static string Page(int from, int count, long total) => "{\"data\":{\"total\":" + total + ",\"displayNames\":{\"u2\":\"Ayşe Demir\"},\"items\":[" + string.Join(",", Enumerable.Range(from, count).Select(i => i == 0
            ? "{\"at\":\"2026-10-11T09:00:00Z\",\"by\":\"u2\",\"field\":null,\"oldValue\":null,\"newValue\":null,\"kind\":\"created\"}"
            : "{\"at\":\"2026-10-11T09:00:00Z\",\"by\":\"u2\",\"field\":\"" + (i % 3 == 0 ? "qrMode" : i % 3 == 1 ? "radiusMeters" : "doctorConfirmation") + "\",\"oldValue\":\"" + (i % 3 == 0 ? "off" : i % 3 == 1 ? "300" : "false") + "\",\"newValue\":\"" + (i % 3 == 0 ? "required" : i % 3 == 1 ? "500" : "true") + "\",\"kind\":\"set\"}")) + "]}}";
        var tr = SettingsResx("tr");
        var (gateway, controller) = VeArrange([PolicyRead], path => path.Contains("page=1&", StringComparison.Ordinal) ? Page(0, 200, 201) : Page(200, 1, 201), "tr");
        var file = Assert.IsType<FileContentResult>(await controller.HistoryExport("csv", ["at,by,field,newValue"], CancellationToken.None));
        Assert.Equal(new[] { GatewayUrl + "/api/crm/visit-evidence-policy/history?page=1&pageSize=200", GatewayUrl + "/api/crm/visit-evidence-policy/history?page=2&pageSize=200" }, gateway.Requests.ToArray());
        var csv = Encoding.UTF8.GetString(file.FileContents).TrimStart((char)0xFEFF).Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        Assert.Equal(202, csv.Length);                                                                 // the header + EVERY change
        Assert.Equal(string.Join(",", tr["ColDate"], tr["ColPerson"], tr["ColField"], tr["ColChange"]), csv[0]);
        Assert.EndsWith(",Ayşe Demir,—,— → " + tr["HistCreated"], csv[1]);                           // the creation, the person by name
        Assert.EndsWith("," + tr["F_radiusMeters"] + "," + tr["UnitMeters"].Replace("{0}", "300") + " → " + tr["UnitMeters"].Replace("{0}", "500"), csv[2]);
        Assert.EndsWith("," + tr["F_doctorConfirmation"] + "," + tr["ValueOff"] + " → " + tr["ValueOn"], csv[3]);
        Assert.EndsWith("," + tr["F_qrMode"] + "," + tr["Opt_qrMode_off"] + " → " + tr["Opt_qrMode_required"], csv[4]);
        var all = string.Join("\n", csv);
        Assert.DoesNotContain("u2", all);                                                              // never the id
        Assert.DoesNotContain("qrMode", all);                                                          // never the raw field
        Assert.StartsWith("visit-evidence-policy-history-", file.FileDownloadName);

        var (g2, c2) = VeArrange([PolicyRead], _ => Page(0, 1, 50_001));
        var big = Assert.IsType<ObjectResult>(await c2.HistoryExport("xlsx", null, CancellationToken.None));
        Assert.Equal(413, big.StatusCode);
        Assert.Contains("EXPORT_TOO_LARGE", JsonSerializer.Serialize(big.Value));
        Assert.Single(g2.Requests);

        var (g3, c3) = VeArrange([PolicyRead], _ => Page(0, 1, 1));
        Assert.Equal(400, Status(await c3.HistoryExport("csv", ["at,secret"], CancellationToken.None)));
        Assert.Equal(400, Status(await c3.HistoryExport("pdf", null, CancellationToken.None)));
        Assert.Empty(g3.Requests);
        Assert.Equal(403, Status(await VeArrange(["crm.visit-evidence-exception.read"], _ => "{}").Controller.HistoryExport("csv", null, CancellationToken.None)));
    }

    // ═══ UAS-001, seven languages, the page's tokens ═══════════════════════════════════════════════════════

    [Fact]
    public void FixD8_without_a_policy_read_key_no_pane_and_no_table_is_drawn()
    {
        var view = VeView("Index.cshtml");
        var denied = view.IndexOf("<partial name=\"_AccessDenied\"", StringComparison.Ordinal);
        var stop = view.IndexOf("return;", denied, StringComparison.Ordinal);
        Assert.True(denied > 0 && stop > denied);
        foreach (var drawn in new[] { "model=\"paneCard\"", "_HistoryTable.cshtml", "_RepsTable.cshtml" })
        {
            Assert.True(view.IndexOf(drawn, StringComparison.Ordinal) > stop, drawn + " must come after the access notice's return");
        }
    }

    [Fact]
    public void FixD9_every_word_the_new_tables_and_the_export_use_exists_in_seven_languages_without_echo()
    {
        var keys = new[] { "PaneForm", "PaneHistory", "PaneReps", "TenantDefault", "HistoryTitle", "AuditReadOnly", "RepsTitle", "RepsNote",
            "ColDate", "ColPerson", "ColField", "ColChange", "ColRep", "ColLocation", "ColPhoto", "ColQr", "ColDevice",
            "HistCreated", "UnitMeters", "UnitSeconds", "ValueOn", "ValueOff", "SourceExceptionShort",
            // WP-VW-W3L — the export's scope column and a layer's rows
            "ColScope", "HistScopeCreated", "HistArchived", "HistInherited" };
        var controller = File.ReadAllText(Path.Combine(WebRoot(), "Controllers", "CRM", "VisitEvidenceSettingsController.cs"));
        foreach (Match m in Regex.Matches(controller, "Text\\(\"([A-Za-z]+)\", c"))
        {
            Assert.Contains(m.Groups[1].Value, keys.Concat(["PaneHistory"]));
        }

        foreach (var culture in Cultures)
        {
            var resx = SettingsResx(culture);
            foreach (var key in keys)
            {
                Assert.True(resx.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value), culture + ": " + key);
                Assert.NotEqual(key, value);
            }
        }
    }

    [Fact]
    public void FixD10_the_new_rules_use_no_colour_of_their_own_and_the_dark_block_stays()
    {
        var css = VeScript("visit-evidence-settings.css");
        var added = css[css.IndexOf("W3E-E4R2-FIX-d", StringComparison.Ordinal)..];
        var note = added.IndexOf(".ve-pane-note", StringComparison.Ordinal);
        added = added[..added.IndexOf('\n', note)];                                          // the FIX-d block, up to its last rule
        Assert.Contains("#ve-scope > .wcn-tabcard { margin-bottom: 0 !important; }", css);
        Assert.DoesNotMatch("#[0-9a-fA-F]{3,6}\\b|rgba?\\(", added);
        Assert.Contains("--ve-p: var(--bs-primary)", css);
        Assert.Contains("[data-bs-theme=\"dark\"] #ve-root", css);
        Assert.DoesNotMatch("#[0-9a-fA-F]{3,6}\\b|rgba?\\(", VeScript("settings-tables.js"));
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────

    private static (VeGateway Gateway, VisitEvidenceSettingsController Controller) VeArrange(string[] permissions, Func<string, string> answer, string? acceptLanguage = null)
    {
        var gateway = new VeGateway(answer);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = GatewayUrl }).Build();
        var controller = new VisitEvidenceSettingsController(new HttpClient(gateway), configuration, NullLogger<VisitEvidenceSettingsController>.Instance);
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()), new("sub", "user-1") };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization(o => o.ResourcesPath = "Resources");
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")), RequestServices = services.BuildServiceProvider() };
        if (acceptLanguage is not null) http.Request.Headers.AcceptLanguage = acceptLanguage;
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return (gateway, controller);
    }

    private sealed class VeGateway(Func<string, string> answer) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!.OriginalString;
            Requests.Add(uri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer(uri[GatewayUrl.Length..]), Encoding.UTF8, "application/json") });
        }
    }
}
