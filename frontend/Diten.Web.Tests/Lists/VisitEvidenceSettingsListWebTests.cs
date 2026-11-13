using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Views.CRM.VisitEvidenceSettings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.Web.Tests.Lists;

/// <summary>
/// WP-VW-W3E · W3E-E4R2-FIX-b — the visit evidence settings page on the project standard, measured on the PRODUCTION view,
/// partials, resx and scripts: (1) the Golden Compact header (h5 + p.text-muted, no page breadcrumb, no h4); (2) the two tabs
/// on the shared tab card (K-TABS) and its corner count written with the card's own badge classes; (3) the time-boxed
/// exceptions on the Golden Compact DataTable — the shared list shell (v2, client mode, no selection), the standard filter
/// (status + phase = the former chips, sent as the proxy's ?status / ?phase), the quick view, Save View, CSV / Excel in the
/// reader's words (index.js in Node on a stub list factory); (4) UAS-001, seven languages, the theme tokens.
/// </summary>
public sealed class VisitEvidenceSettingsListWebTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string[] Cultures = ["", "en", "tr", "fr", "es", "zh", "ar", "ru"];
    private readonly WebApplicationFactory<Program> _factory;

    public VisitEvidenceSettingsListWebTests(WebApplicationFactory<Program> factory) => _factory = factory;

    // ═══ 1 · the header ═══════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void L1_the_header_is_the_golden_compact_one_without_the_page_breadcrumb_or_an_h4()
    {
        var view = View("Index.cshtml");
        const string header = "<div class=\"mb-3\">\n    <h5 class=\"mb-0\">@Localizer[\"PageTitle\"]</h5>\n    <p class=\"mb-0 text-muted\">@Localizer[\"PageDescription\"]</p>\n</div>";
        Assert.Contains(header, view);
        Assert.DoesNotContain("ve-crumb", view);
        Assert.DoesNotContain("<h4", view);
        Assert.DoesNotContain("ve-head", view);
        Assert.DoesNotContain("ve-intro", view);
        Assert.DoesNotContain("Localizer[\"Crumb\"]", view);
        // UAS-001: the header (like everything else) comes after the access guard; then the tab card
        var guard = view.IndexOf("@if (!Model.CanRead)", StringComparison.Ordinal);
        Assert.True(guard > 0 && guard < view.IndexOf(header, StringComparison.Ordinal));
        Assert.True(view.IndexOf(header, StringComparison.Ordinal) < view.IndexOf("_TabCard.cshtml\" model=\"tabCard\"", StringComparison.Ordinal));

        // the page CSS keeps nothing of the former header / tabs / chips
        var css = Script("visit-evidence-settings.css");
        foreach (var gone in new[] { ".ve-crumb", ".ve-title", ".ve-head", ".ve-intro", ".ve-tabs", ".ve-tab ", ".ve-tab.", ".ve-badge", ".ve-chip" })
        {
            Assert.DoesNotContain(gone, css);
        }

        // the description is the former intro (same words), the breadcrumb text is gone from every language
        foreach (var culture in Cultures)
        {
            var resx = Resx(culture);
            Assert.False(resx.ContainsKey("Crumb"), culture);
            Assert.False(resx.ContainsKey("Intro"), culture);
            Assert.False(string.IsNullOrWhiteSpace(resx["PageDescription"]), culture);
        }

        Assert.StartsWith("Temsilcinin \"Ziyarete başla\" kuralları.", Resx("tr")["PageDescription"]);
    }

    // ═══ 2 · the tabs ═════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void L2_the_tabs_are_the_shared_tab_card_and_the_corner_count_is_its_own_badge()
    {
        var view = View("Index.cshtml");
        Assert.Contains("new() { Key = \"settings\", Id = \"ve-tab-settings\", Icon = \"bx-layer\", Text = Localizer[\"TabSettings\"].Value, Active = true, Controls = \"ve-pane-settings\" }", view);
        const string exceptionsTab = "tabs.Add(new() { Key = \"exceptions\", Id = \"ve-tab-exceptions\", Icon = \"bx-time-five\", Text = Localizer[\"TabExceptions\"].Value, Controls = \"ve-pane-exceptions\" });";
        // the exceptions tab (and its pane) only with an exception read key
        var guard = view.IndexOf("if (Model.CanReadExceptions)\n    {\n        " + exceptionsTab, StringComparison.Ordinal);
        Assert.True(guard > 0, "the exceptions tab is added only under CanReadExceptions");
        Assert.Contains("<partial name=\"~/Views/Shared/Components/Tabs/_TabCard.cshtml\" model=\"tabCard\" />", view);
        Assert.Contains("@if (Model.CanReadExceptions)\n    {\n        <div class=\"tab-pane fade\" id=\"ve-pane-exceptions\" role=\"tabpanel\" aria-labelledby=\"ve-tab-exceptions\">", view);
        Assert.Contains("<div class=\"tab-pane fade show active\" id=\"ve-pane-settings\" role=\"tabpanel\" aria-labelledby=\"ve-tab-settings\">", view);
        Assert.DoesNotContain("ve-tabs", view);
        Assert.DoesNotContain("data-tab=\"", view);
        Assert.DoesNotContain("ve-tab-badge", view);
        // diten-tabs.js is there before the page script that selects a tab from code ("?exception=")
        Assert.True(view.IndexOf("~/assets/js/diten-tabs.js", StringComparison.Ordinal) is > 0 and var t
                    && t < view.IndexOf("VisitEvidenceSettings/visit-evidence-settings.js", StringComparison.Ordinal));

        // the corner count: the SAME classes the shared tab card draws for a Count (read from _TabCard.cshtml, not copied)
        var card = File.ReadAllText(Path.Combine(WebRoot(), "Views", "Shared", "Components", "Tabs", "_TabCard.cshtml"));
        var cardBadge = Regex.Match(card, "<span class=\"(badge [^\"]*wcn-tab-count[^\"]*)\">@tab.Count</span>").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(cardBadge));
        var js = Script("visit-evidence-settings.js");
        Assert.Contains("const BADGE_CLASS = '" + cardBadge + "';", js);
        Assert.Contains("tab.querySelector('.wcn-tab-count')", js);
        Assert.Contains("renderBadge(C.tabBadge(list));", js); // the same count: in review + active today
        Assert.DoesNotContain("ve-tab-badge", js);
        Assert.DoesNotContain("'.ve-tab'", js);
        Assert.Contains("window.DitenTabs.activate(el('ve-tab-' + tab))", js);
    }

    // ═══ 3 · the exceptions list ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task L3_the_exceptions_table_is_the_shared_list_shell_v2_client_without_selection()
    {
        var html = Normalize(await RenderAsync("/Views/CRM/VisitEvidenceSettings/_DataTable.cshtml", "tr"));
        Assert.Equal("<table id=\"dt-visitevidenceexceptions\" data-dt-standard=\"v2\" data-dt-data-mode=\"client\" class=\"datatables-visitevidenceexceptions table border-top\">",
            Regex.Match(html, "<table [^>]*>").Value);
        Assert.Contains("data-table-skeleton", html);                     // the shaped placeholder
        Assert.DoesNotContain("dt-checkboxes-select-all", html);          // no selection
        var headers = Regex.Matches(Regex.Match(html, "<thead>[\\s\\S]*?</thead>").Value, "<th[^>]*>([^<]*)</th>").Select(m => System.Net.WebUtility.HtmlDecode(m.Groups[1].Value.Trim())).Where(h => h.Length > 0).ToArray();
        var tr = Resx("tr");
        Assert.Equal(new[] { "XColRep", "XColRules", "XColRange", "XColReason", "XColPeople", "XColStatus", "ColActions" }.Select(k => tr[k]), headers);
        Assert.Equal("Durum", headers[5]);

        var source = View("_DataTable.cshtml");
        Assert.Contains("HasSelection = false,", source);
        Assert.Contains("the CRM has no bulk endpoint", source);          // the stated reason
        Assert.Contains("<partial name=\"~/Views/Shared/Components/DataTable/_ListShell.cshtml\" model=\"listShell\" />", source);

        // the former hand-written table and its chips are gone; the list markers are in the view
        var view = View("Index.cshtml");
        foreach (var gone in new[] { "ve-x-rows", "ve-x-filters", "ve-chips", "ve-x-drawer", "ve-x-body", "ve-x-foot", "ve-new-exception" })
        {
            Assert.DoesNotContain(gone, view);
        }

        Assert.Contains("<partial name=\"~/Views/CRM/VisitEvidenceSettings/_Filter.cshtml\" />", view);
        Assert.Contains("<partial name=\"~/Views/CRM/VisitEvidenceSettings/_DataTable.cshtml\" />", view);
        Assert.Contains("<partial name=\"~/Views/CRM/VisitEvidenceSettings/_IndexL10n.cshtml\" />", view);
        Assert.Contains("id=\"offcanvasDetailsPreview\"", view);
        Assert.Contains("<div class=\"offcanvas-body ve-col ve-gap\" id=\"oc-details-list\"></div>", view);
        Assert.Contains("id=\"oc-actions\"", view);
        Assert.True(view.IndexOf("VisitEvidenceSettings/visit-evidence-settings.js", StringComparison.Ordinal) < view.IndexOf("VisitEvidenceSettings/index.js", StringComparison.Ordinal));
    }

    [Fact]
    public async Task L4_the_filter_is_the_former_chips_status_and_phase_as_select2_and_the_proxy_takes_them_as_its_query()
    {
        var html = await RenderAsync("/Views/CRM/VisitEvidenceSettings/_Filter.cshtml", "tr");
        string[] Options(string id) => Regex.Matches(Regex.Match(html, "<select id=\"" + id + "\"[\\s\\S]*?</select>").Value, "<option value=\"([^\"]*)\">([^<]*)</option>").Select(m => m.Groups[1].Value + "=" + m.Groups[2].Value.Trim()).ToArray();
        Assert.Equal(new[] { "=", "in_review=Onay bekliyor", "draft=Taslak", "cancelled=İptal", "ended_early=Erken bitti" }, Options("filterStatus"));
        Assert.Equal(new[] { "=", "active=Aktif", "planned=Planlı", "expired=Süresi doldu" }, Options("filterPhase"));
        Assert.Contains("<select id=\"filterStatus\" class=\"form-select form-select-sm select2\" data-placeholder=\"Durum\">", html);
        Assert.Contains("<select id=\"filterPhase\" class=\"form-select form-select-sm select2\" data-placeholder=\"Aşama\">", html);
        Assert.Contains("id=\"inlineFilterHost\"", html);
        Assert.Contains("id=\"btnFilterApply\"", html);
        Assert.Contains("id=\"btnFilterReset\"", html);

        // the options are EXACTLY the former chips (evidence-core's FILTERS): a status chip → the status select, a phase chip → the phase select
        var node = RunNode("const C = require('" + Path.Combine(ScriptDir(), "evidence-core.js").Replace("\\", "/") + "');\n"
            + "process.stdout.write(JSON.stringify({ status: C.FILTERS.filter(f => f.status).map(f => f.status), phase: C.FILTERS.filter(f => f.phase).map(f => f.phase) }));");
        if (node is not null)
        {
            var chips = JsonDocument.Parse(node).RootElement;
            Assert.Equal(chips.GetProperty("status").EnumerateArray().Select(x => x.GetString()).OrderBy(x => x), Options("filterStatus").Skip(1).Select(o => o.Split('=')[0]).OrderBy(x => x));
            Assert.Equal(chips.GetProperty("phase").EnumerateArray().Select(x => x.GetString()).OrderBy(x => x), Options("filterPhase").Skip(1).Select(o => o.Split('=')[0]).OrderBy(x => x));
        }

        // the proxy hands ?status / ?phase to the CRM unchanged (its contract: GET api/crm/visit-evidence-exceptions?status&phase)
        var controller = File.ReadAllText(Path.Combine(WebRoot(), "Controllers", "CRM", "VisitEvidenceSettingsController.cs"));
        Assert.Contains("=> ProxyAsync(HttpMethod.Get, $\"{Exceptions}{Request.QueryString}\", null, ct, ExceptionRead);", controller);
    }

    [Fact]
    public void L5_index_js_is_one_client_list_with_the_query_the_quick_view_save_view_and_export_in_the_readers_words()
    {
        var o = ListSmoke("manage");
        if (o is null) { return; }
        var r = o.Value;
        string S(string n) => r.GetProperty(n).GetString() ?? string.Empty;
        string J(string n) => r.GetProperty(n).GetRawText();
        Assert.Equal(JsonValueKind.Null, r.GetProperty("createError").ValueKind);
        Assert.Equal("client", S("dataMode"));
        Assert.True(r.GetProperty("tableIsTheListShells").GetBoolean());
        Assert.Equal("/CRM/VisitEvidenceSettings/api/exceptions", S("ajaxUrl"));
        Assert.Equal("{\"Authorization\":\"Bearer t\"}", J("ajaxHeaders"));
        Assert.Equal(JsonValueKind.Null, r.GetProperty("serverExport").ValueKind); // client mode: CSV / Excel from the rows on the table

        // the proxy query = the APPLIED filters only
        Assert.Equal("{}", J("queryBaseline"));
        Assert.Equal("{\"status\":\"in_review\"}", J("queryStatus"));
        Assert.Equal("{\"phase\":\"active\"}", J("queryPhase"));
        Assert.Equal("[[\"filterStatus\",\"status\",\"single\"],[\"filterPhase\",\"phase\",\"single\"]]", J("fields"));
        Assert.Equal("[true,false,true,true,false]", J("matches"));
        Assert.Equal("[\"inlineFilterHost\",\"inlineFilterCollapse\"]", J("filterHost"));
        // an Apply and a Reset each re-read (keeping the page)
        Assert.Equal("[false,false]", J("reloads"));
        Assert.Equal("function", S("reloadApi"));

        // Save View, quick view, the add-new action, export / column visibility over the six data columns
        var saved = r.GetProperty("savedView");
        Assert.Equal(("CRM", "VisitEvidenceExceptions"), (saved.GetProperty("moduleKey").GetString(), saved.GetProperty("pageKey").GetString()));
        Assert.Equal("[1,2,3,4,5,6]", saved.GetProperty("saveViewColumnIndexes").GetRawText());
        Assert.Equal("{\"id\":\"offcanvasDetailsPreview\",\"key\":\"quickView\"}", J("quickView"));
        var toolbar = r.GetProperty("toolbar");
        Assert.Equal("Yeni istisna", toolbar.GetProperty("addNewText").GetString());
        Assert.Equal("[1,2,3,4,5,6]", toolbar.GetProperty("exportColumns").GetRawText());
        Assert.Equal("[1,2,3,4,5,6]", toolbar.GetProperty("colvisColumns").GetRawText());
        Assert.Equal("[[\"openException\",\"x-1\",true],[\"openNew\"]]", J("pageCalls"));
        Assert.Equal("Bu görünümde istisna yok.", r.GetProperty("config").GetProperty("emptyTable").GetString());
        Assert.Equal("[\"id\",\"repDisplayName\",\"rules\",\"startDate\",\"reason\",\"createdBy\",\"status\",\"id\"]", r.GetProperty("config").GetProperty("columns").GetRawText());

        // CSV / Excel / sort: the plain words in the reader's language (names from the CRM's displayNames, "Siz" for me)
        Assert.Equal(2, r.GetProperty("rowCount").GetInt32());
        var row = r.GetProperty("exportRow").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Equal("Okan Tekin", row[0]);
        Assert.StartsWith("Konum kontrolü: Bilgi, ", row[1]);
        Assert.Matches("^10 Eki 2026 – 13 Eki 2026$", row[2]);
        Assert.Equal("GPS arızalı", row[3]);
        Assert.Equal("Oluşturan: Siz · Onaylayan: —", row[4]);
        Assert.Equal("Onay bekliyor", row[5]);
        Assert.Equal("[\"Ayşe Demir\",\"Oluşturan: Deniz Er · Onaylayan: Selin Ak\",\"Aktif\"]", J("exportRow2"));
        Assert.Equal("2026-10-10", S("sortRange"));
        Assert.Contains("<bdi>Okan Tekin</bdi>", S("displayRep"));                 // isolated for RTL
        Assert.Contains("ve-pill ve-tone-success", S("displayStatus"));
        Assert.Contains("data-row-action=\"quickView\"", S("actions"));
        Assert.Contains("title=\"Hızlı görünüm\"", S("actions"));
        // a phone keeps the rep, the status and the actions first
        var priorities = r.GetProperty("priorities").EnumerateArray().ToDictionary(p => p[0].GetInt32(), p => (p[1].GetString(), p[2].ValueKind == JsonValueKind.Null ? (int?)null : p[2].GetInt32()));
        Assert.Equal("all", priorities[1].Item1);
        Assert.Equal("cell-fit all", priorities[-1].Item1);
        Assert.Equal(2, priorities[6].Item2);

        // without the manage key there is no add-new action
        var read = ListSmoke("read");
        if (read is null) { return; }
        Assert.Equal(string.Empty, read.Value.GetProperty("toolbar").GetProperty("addNewText").GetString());
    }

    // ═══ 4 · seven languages, the bridge, the theme ═══════════════════════════════════════════════════════

    [Fact]
    public async Task L6_the_list_words_are_in_seven_languages_and_the_payload_carries_every_word_index_js_reads()
    {
        foreach (var culture in Cultures)
        {
            var resx = Resx(culture);
            foreach (var key in new[] { "PageDescription", "FilterPhase", "QuickView", "ColActions", "NewException", "XEmpty", "XColStatus" })
            {
                Assert.True(resx.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) && v != key, culture + ":" + key);
            }
        }

        Assert.Matches("[؀-ۿ]", Resx("ar")["FilterPhase"]);
        Assert.Matches("[؀-ۿ]", Resx("ar")["QuickView"]);

        // every L().X index.js reads arrives in the payload (the page's ScriptKeys or the shared toolbar words)
        var js = Script("index.js");
        var read = Regex.Matches(js, "L\\(\\)(?:\\.([A-Za-z0-9_]+)|\\[\\s*'([A-Za-z0-9_]+)'\\s*\\])").Select(m => m.Groups[1].Success && m.Groups[1].Value.Length > 0 ? m.Groups[1].Value : m.Groups[2].Value).Distinct().ToArray();
        Assert.NotEmpty(read);
        foreach (var culture in new[] { "tr", "ar" })
        {
            var l10n = await RenderAsync("/Views/CRM/VisitEvidenceSettings/_IndexL10n.cshtml", culture);
            var json = Regex.Match(l10n, "<script id=\"visitevidencesettings-l10n\" type=\"application/json\">([\\s\\S]*?)</script>").Groups[1].Value;
            var payload = JsonDocument.Parse(json).RootElement.EnumerateObject().ToDictionary(p => char.ToUpperInvariant(p.Name[0]) + p.Name[1..], p => p.Value.GetString());
            foreach (var key in read)
            {
                Assert.True(payload.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v), culture + ":" + key);
            }

            Assert.Equal(Resx(culture)["NewException"], payload["NewException"]);
            Assert.Equal(Resx(culture)["ColActions"], payload["Actions"]);
        }

        // the word keys the T() helper reads are declared too
        foreach (Match m in Regex.Matches(js, "T\\('([A-Za-z0-9_]+)'"))
        {
            Assert.Contains(m.Groups[1].Value, VisitEvidenceSettingsIndex.ScriptKeys);
        }
    }

    [Fact]
    public void L7_the_list_cells_use_the_page_tokens_only_the_primary_comes_from_the_layout_and_the_dark_block_stays()
    {
        var css = Script("visit-evidence-settings.css");
        var added = css[css.IndexOf("W3E-E4R2-FIX-b", StringComparison.Ordinal)..];
        Assert.DoesNotMatch("#[0-9a-fA-F]{3,6}\\b", added);              // no hard-coded colour in the new rules
        Assert.Contains("var(--ve-pt)", added);
        Assert.Contains("[data-bs-theme=\"dark\"] #ve-root", css);
        Assert.Contains("--ve-p: var(--bs-primary)", css);
        // the quick view keeps the page's tokens (light + dark) through .ve-drawer
        Assert.Contains("class=\"offcanvas offcanvas-end ve-drawer\" tabindex=\"-1\" id=\"offcanvasDetailsPreview\"", View("Index.cshtml"));
        Assert.Contains("#ve-root, .ve-modal, .ve-drawer {", css);
        // the change history on the settings tab: no longer a hand-written table (W3E-E4R2-FIX-d — the Golden Compact DataTable)
        Assert.DoesNotContain("ve-history-rows", View("Index.cshtml"));
        Assert.Contains("<partial name=\"~/Views/CRM/VisitEvidenceSettings/_HistoryTable.cshtml\" />", View("Index.cshtml"));
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────

    private JsonElement? ListSmoke(string mode)
    {
        var smoke = Path.Combine(Path.GetDirectoryName(WebRoot())!, "Diten.Web.Tests", "Js", "visit-evidence-exceptions-list-smoke.js").Replace("\\", "/");
        var crm = Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM").Replace("\\", "/");
        var tr = Resx("tr");
        var words = VisitEvidenceSettingsIndex.ScriptKeys.ToDictionary(k => k, k => tr[k]);
        words["Actions"] = tr["ColActions"];
        var texts = Path.Combine(Path.GetTempPath(), "vel-tr-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(texts, JsonSerializer.Serialize(words), new UTF8Encoding(false));
        try
        {
            var node = RunNode("process.argv[2] = '" + crm + "'; process.argv[3] = '" + texts.Replace("\\", "/") + "'; process.argv[4] = '" + mode + "'; require('" + smoke + "');");
            return node is null ? null : JsonDocument.Parse(node).RootElement;
        }
        finally
        {
            File.Delete(texts);
        }
    }

    private static string Normalize(string html) => Regex.Replace(Regex.Replace(html, "\\s+", " "), ">\\s+<", "><").Trim();

    private async Task<string> RenderAsync(string viewPath, string culture)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var engine = services.GetRequiredService<IRazorViewEngine>();
        var found = engine.GetView(executingFilePath: null, viewPath: viewPath, isMainPage: false);
        if (!found.Success) throw new InvalidOperationException($"Could not load {viewPath}.");
        var httpContext = new DefaultHttpContext { RequestServices = services };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
        var viewData = new ViewDataDictionary(services.GetRequiredService<IModelMetadataProvider>(), actionContext.ModelState);
        var tempData = new TempDataDictionary(httpContext, services.GetRequiredService<ITempDataProvider>());
        using var writer = new StringWriter();
        await found.View.RenderAsync(new ViewContext(actionContext, found.View, viewData, tempData, writer, new HtmlHelperOptions()));
        return writer.ToString();
    }

    private static string? RunNode(string code)
    {
        var temp = Path.Combine(Path.GetTempPath(), "vel-" + Guid.NewGuid().ToString("N") + ".js");
        File.WriteAllText(temp, code, new UTF8Encoding(false));
        try
        {
            using var process = Process.Start(new ProcessStartInfo("node", "\"" + temp + "\"")
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, StandardOutputEncoding = Encoding.UTF8
            });
            if (process is null) return null;
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(20000);
            Assert.True(process.ExitCode == 0, error);
            return output;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
        finally
        {
            File.Delete(temp);
        }
    }

    private static string ScriptDir() => Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", "VisitEvidenceSettings");

    private static string Script(string file) => File.ReadAllText(Path.Combine(ScriptDir(), file)).Replace("\r\n", "\n");

    private static string View(string file) => File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "VisitEvidenceSettings", file)).Replace("\r\n", "\n");

    private static Dictionary<string, string> Resx(string culture)
    {
        var file = "VisitEvidenceSettingsIndex" + (culture.Length == 0 ? "" : "." + culture) + ".resx";
        return XDocument.Load(Path.Combine(WebRoot(), "Resources", "Views", "CRM", "VisitEvidenceSettings", file)).Root!
            .Elements("data").ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")?.Value ?? string.Empty, StringComparer.Ordinal);
    }

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }
}
