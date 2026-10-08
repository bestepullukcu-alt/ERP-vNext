using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Controllers.CRM;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VP-4I-WEB — the strip card and the day row, the English month, the Targets account cards per the mockup, data names
/// isolated for right-to-left pages, province labels and the reference-label script, on the REAL scripts, views,
/// controller, resources and script file (Acceptance 1–8). Where Node is on the machine the formatter is also RUN.
/// </summary>
public sealed class VisitPlanningPolishRtlWebTests
{
    private const string GatewayUrl = "http://gateway.test";
    private static readonly Guid TenantId = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");
    private static readonly string[] Cultures = ["", "en", "tr", "fr", "es", "zh", "ar", "ru"];

    // ── 1 · the strip card: content on the leading edge, "41. Hafta" + TODAY on one line ─────────────────────────

    [Fact]
    public void The_strip_card_starts_at_the_leading_edge_and_keeps_its_title_and_today_on_one_line()
    {
        var card = Between(Script("weeks.js"), "const stripCard = m => {", "\n    };");
        // the theme's .btn centres its content (align-items:center); the card stretches its rows and starts them at the edge
        Assert.Contains("btn border rounded text-start d-flex flex-column align-items-stretch justify-content-start gap-1", card);
        Assert.Contains("'<span class=\"d-flex justify-content-between align-items-center gap-1 w-100 text-nowrap\"><span class=\"fw-semibold\">' + esc(m.title)", card);
        Assert.Contains("'<span class=\"fw-semibold text-primary\" style=\"font-size:11px\">' + esc(L.TodayLabel || '')", card);
        Assert.Contains("const STRIP_CARD = 'flex:0 0 128px;';", Script("weeks.js"));
    }

    // ── 2 · the day row: "6 / 57" + its badge on one line; the day name never wraps ───────────────────────────────

    [Fact]
    public void The_day_row_reads_a_short_count_with_its_badge_on_one_line_and_never_wraps_the_day_name()
    {
        var js = Script("weeks.js");
        Assert.Contains("const DAY_GRID = 'display:grid;grid-template-columns:18px minmax(96px,auto) minmax(0,1fr) auto;gap:12px;align-items:center';", js);
        var row = Between(js, "const dayRow = (day, movable) => {", "\n    };");
        Assert.Contains("'<span class=\"text-nowrap\"><strong class=\"fw-semibold\">' + esc(dayName(day.d)) + '</strong> ' + esc(dm(day.d)) + '</span>'", row);
        // the short count, the word "visits" in the title
        Assert.Contains("esc(day.cap != null ? day.slots.length + ' / ' + day.cap : String(day.slots.length))", row);
        Assert.Contains("title=\"' + esc(day.cap != null ? fmt(L.DayCapacityFormat || '{0} / {1}', day.slots.length, day.cap) : '') + '\"", row);
        var count = Between(row, "vp-wk-daycount", "badges.join('') + '</span>'");
        Assert.Contains("text-nowrap", Between(row, "'<span class=\"d-flex justify-content-end", "vp-wk-daycount"));
        Assert.DoesNotContain("flex-wrap", Between(row, "'<span class=\"d-flex justify-content-end", "vp-wk-daycount"));
        Assert.NotEmpty(count);
    }

    // ── 3 · English: "28 Sep–2 Oct" (en-GB's CLDR writes "Sept") ─────────────────────────────────────────────────

    [Fact]
    public void English_dates_use_the_three_letter_month_day_first()
    {
        var js = Script("format.js");
        Assert.Contains("const isEnglish = () => /^en(-|$)/i.test(culture());", js);
        Assert.Contains("if (!isEnglish() || options.month !== 'short' || typeof f.formatToParts !== 'function') return f.format(d);", js);
        Assert.Contains("return f.formatToParts(d).map(p => (p.type === 'month' ? p.value.replace(/\\.$/, '').slice(0, 3) : p.value)).join('');", js);
        Assert.Contains("return /^en(-|$)/i.test(c) ? 'en-GB' : c;", js); // day first stays
        Assert.DoesNotMatch(@"['""]en-US['""]", js);

        var run = RunFormatter("en", "[F.range('2026-09-28','2026-10-02'), F.dayLabel('2026-09-28'), F.range('2026-10-05','2026-10-09')].join('|')");
        if (run is not null)
        {
            Assert.Equal("28 Sep–2 Oct|Mon 28 Sep|5–9 Oct", run);
        }

        var tr = RunFormatter("tr", "F.range('2026-09-28','2026-10-02')");
        if (tr is not null)
        {
            Assert.Equal("28 Eyl–2 Eki", tr); // other languages unchanged
        }
    }

    // ── 4 · the account card per the mockup; "Remove" on the Selected group's header ────────────────────────────

    [Fact]
    public void The_account_card_follows_the_mockup_and_only_the_open_account_is_highlighted()
    {
        var js = Script("details.js");
        var row = Between(js, "const accountRow = a => {", "\n    };");
        // line 1: the name, the type badge at the far end
        Assert.Contains("'<span class=\"d-flex justify-content-between align-items-start gap-2\"><span class=\"fw-medium text-heading\" style=\"min-width:0;font-size:14px\">' + bidi(a.name) + outBadge(a) + '</span>' + typeBadge(a.type) + '</span>'", row);
        Assert.Contains("const TYPE_TONE = { clinic: 'background:#e0e2f3;color:#0b1a8c', hospital: 'background:#d7f5fc;color:#028aa6' };", js);
        Assert.Contains("badge flex-shrink-0 text-nowrap", Between(js, "const typeBadge = type =>", ": '';"));
        // line 2: province · "x / y selected" · amber "N this week" — separate items, gap 10px, no dots
        Assert.Contains("'<span class=\"d-flex flex-wrap text-muted vp-acc-stats\" style=\"gap:10px;font-size:13px\"", row);
        var stats = Between(js, "const accountStatsHtml = (id, city) => {", "\n    };");
        Assert.Contains("'<span class=\"vp-acc-due\" style=\"color:#b27800\">' + esc(st.due) + '</span>'", stats);
        Assert.DoesNotContain("' · '", stats);
        Assert.Contains("n.innerHTML = accountStatsHtml(n.dataset.aid, n.dataset.city);", js);
        // the highlight: the OPEN account only (light bg + 3px bar on the leading edge — logical, so the right one in RTL)
        Assert.Contains("const ACTIVE_ACCOUNT_STYLE = 'background:#f3f4fb;border-inline-start:3px solid #0b1a8c !important;';", js);
        Assert.Contains("' style=\"cursor:pointer;padding:12px 14px;' + (active ? ACTIVE_ACCOUNT_STYLE : '') + '\">'", row);
        Assert.DoesNotContain("inPlan ?", Between(row, "' style=\"cursor:pointer", "'\">'"));
        Assert.DoesNotContain("bg-label-primary", row);
        // no remove "×" on the row …
        Assert.DoesNotContain("js-remove-account", row);
        Assert.DoesNotContain("bx-x", row);
        // … the Selected group's header removes the institution (the existing removeAccount)
        var head = Between(js, "const groupHead = (key, title, count) => {", "\n    };");
        Assert.Contains("const removable = key !== 'pharmacies' && canEditTargets();", head);
        Assert.Contains("js-remove-account\" data-id=\"' + esc(key) + '\"", head);
        Assert.Contains("esc(L.RemoveTarget || 'Remove') + '</button>'", head);
        Assert.Contains("targetAccounts.forEach(a => { groups[a.id] = []; });", Between(js, "const renderSelectionChips = () => {", "\n    };"));
        var chips = Between(js, "el('vp-selection-chips')?.addEventListener('click', e => {", "});");
        Assert.Contains("const rm = e.target.closest('.js-remove-account');", chips);
        Assert.Contains("if (rm) { removeAccount(rm.dataset.id); return; }", chips);
        Assert.Contains("Object.keys(selectedContacts).forEach(k => { if (k.indexOf(id + '|') === 0) delete selectedContacts[k]; });", Between(js, "const removeAccount = id => {", "\n    };"));
        // "+ Add out-of-territory": white, dashed, a plus; the dialog unchanged
        var view = View("Details.cshtml");
        var button = Between(view, "class=\"btn btn-sm w-100 bg-white", "</button>");
        Assert.Contains("style=\"border:1px dashed #b4bdc6\"", button);
        Assert.Contains("id=\"vp-out-territory-open\" data-bs-toggle=\"modal\" data-bs-target=\"#vp-out-territory-modal\"><i class=\"bx bx-plus\"></i>", button);
        Assert.DoesNotContain("btn-label-warning w-100\" id=\"vp-out-territory-open\"", view);
        // the list box: bordered, ≤ 560px, rows divided (list-group-flush)
        Assert.Contains("class=\"list-group list-group-flush border rounded overflow-auto\" id=\"vp-acc-list\" role=\"listbox\"", view);
        Assert.Contains("style=\"max-height:560px\"", view);
    }

    // ── 5 · bidi: every data name of Visit Planning goes into the page inside <bdi> ─────────────────────────────

    [Fact]
    public void Institution_doctor_product_period_and_rep_names_are_isolated_with_bdi()
    {
        var format = Script("format.js");
        Assert.Contains("const escapeHtml = s => String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/\"/g, '&quot;').replace(/'/g, '&#39;');", format);
        Assert.Contains("const bidi = text => '<bdi>' + escapeHtml(text) + '</bdi>';", format);
        Assert.Contains("culture, dateCulture, asDate, bidi,", format);

        var run = RunFormatter("ar", "F.bidi('018 <KLİNİK> & \"x\"')");
        if (run is not null)
        {
            Assert.Equal("<bdi>018 &lt;KLİNİK&gt; &amp; &quot;x&quot;</bdi>", run);
        }

        // the shared helper in every page script that draws names …
        Assert.Contains("const bidi = VPF.bidi;", Script("weeks.js"));
        Assert.Contains("const bidi = VPF.bidi;", Script("details.js"));
        Assert.Contains("const bidi = VPF.bidi;", Script("doctor-panel.js"));
        Assert.Contains("const bidi = s => window.VisitPlanningFormat.bidi(s);", Script("targets.js"));
        Assert.Contains("const bidi = v => window.VisitPlanningFormat.bidi(v);", Script("index.js"));

        // … and the scan: no data name is merely escaped into the page any more
        var forbidden = new[]
        {
            "esc(name)", "esc(a.name)", "esc(b.name)", "esc(x.name)", "esc(x.acc)", "esc(v) + '</span>'", "esc(title)", "esc(sub) + '</span>' : '')",
            "esc(visitName(", "esc(groupName(", "esc(whoName(", "esc(VPF.productLabel(", "esc(window.VisitPlanningFormat.productLabel(",
            "esc(it.productName", "esc(r.productName", "esc(sName(row))", "esc(sRep(row)", "esc(periodMap[", "esc(sname)",
            "<span class=\"fw-medium text-truncate\">' + esc(p.name)"
        };
        foreach (var file in new[] { "weeks.js", "details.js", "doctor-panel.js", "targets.js", "index.js" })
        {
            var js = Script(file);
            foreach (var pattern in forbidden)
            {
                Assert.False(js.Contains(pattern, StringComparison.Ordinal), $"{file}: {pattern} — a data name must go through bidi()");
            }
        }

        var weeks = Script("weeks.js");
        foreach (var expected in new[] { "bidi(groupName(g.slots))", "bidi(visitName(s))", "bidi(VPF.productLabel(c))", "bidi(whoName(h.by))", "bidi(x.name)", "bidi(name)" })
        {
            Assert.Contains(expected, weeks);
        }

        var details = Script("details.js");
        foreach (var expected in new[] { "bidi(a.name)", "bidi(b.name)", "bidi(title)", "bidi(p.name)", "bidi(city)" })
        {
            Assert.Contains(expected, details);
        }

        Assert.Contains("bidi(it.productName || it.productCode || '—')", Script("targets.js"));
        Assert.Contains("${bidi(sName(row))}", Script("index.js"));
        // the names written as text (period, rep, the open institution) isolate themselves
        var view = View("Details.cshtml");
        Assert.Contains("id=\"vp-d-period\" dir=\"auto\"", view);
        Assert.Contains("id=\"vp-d-rep\" dir=\"auto\"", view);
        Assert.Contains("id=\"vp-contacts-for\" dir=\"auto\"", view);
    }

    // ── 6 · the province from its reference label; without one the last part with a Turkish capital ───────────

    [Fact]
    public async Task The_province_reads_its_reference_label_in_the_ui_language()
    {
        var (requests, controller) = Arrange((_, uri) => uri.Contains("/city/")
            ? (HttpStatusCode.OK, """{"isSuccessful":true,"data":{"setCode":"city","items":[{"code":"TR-34-ISTANBUL","label":"İstanbul","isActive":true},{"code":"TR-63-SANLIURFA","label":"Şanlıurfa","isActive":true}]}}""")
            : uri.Contains("/account-type/")
                ? (HttpStatusCode.OK, """{"isSuccessful":true,"data":{"setCode":"account-type","items":[{"code":"clinic","label":"Clinic","isActive":true,"attributes":{"label_tr":"Klinik","label_ar":"عيادة"}},{"code":"hospital","label":"Hospital","isActive":true}]}}""")
                : (HttpStatusCode.NotFound, "{}"));

        var data = await LabelsIn("tr", controller);

        Assert.Equal("İstanbul", data.GetProperty("cities").GetProperty("TR-34-ISTANBUL").GetString());
        Assert.Equal("Şanlıurfa", data.GetProperty("cities").GetProperty("TR-63-SANLIURFA").GetString());
        Assert.Contains(requests, r => r.EndsWith("/consumable-sets/city/published-values", StringComparison.Ordinal));
        Assert.Equal(JsonValueKind.Object, data.GetProperty("specialties").ValueKind); // an unread set is an empty map

        var js = Script("details.js");
        Assert.Contains("typeLabels = d.accountTypes || {}; specLabels = d.specialties || {}; cityLabels = d.cities || {};", js);
        var label = Between(js, "const cityLabel = code => {", "\n    };");
        Assert.Contains("labelOf(cityLabels, code)", label);
        Assert.Contains("return hit || titleTr(parts[parts.length - 1]);", label);
        // the fallback: an ASCII code is transliterated ("ISTANBUL" → "İstanbul"), a Turkish one by Turkish rules
        var title = Between(js, "const titleTr = s =>", "}).join(' ');");
        Assert.Contains("const lower = /[çğıöşüÇĞİÖŞÜ]/.test(w) ? w.toLocaleLowerCase('tr') : w.toLowerCase();", title);
        Assert.Contains("return lower.charAt(0).toLocaleUpperCase('tr') + lower.slice(1);", title);
        Assert.Contains("const city = typeof rawCity === 'string' ? cityLabel(rawCity) : rawCity;", js);
        Assert.DoesNotContain("rawCity.split('-').pop() : rawCity", js);
        Assert.Contains("const cityOnly = a => (a.cityRef ? cityLabel(a.cityRef) : String(a.city || '').split(' · ')[0]);", js);

        var node = RunNode("const titleTr = " + title.Substring("const titleTr = ".Length) + "\nprocess.stdout.write(['ISTANBUL','KONYA','IĞDIR','ŞANLIURFA'].map(titleTr).join('|'));");
        if (node is not null)
        {
            Assert.Equal("İstanbul|Konya|Iğdır|Şanlıurfa", node);
        }
    }

    // ── 9 (root cause) · the label in the UI language: label_<lang>, else the label ─────────────────────────────

    [Fact]
    public async Task Institution_type_reads_label_of_the_ui_language_and_falls_back_to_the_label()
    {
        static (HttpStatusCode, string) Route(HttpMethod _, string uri) => uri.Contains("/account-type/")
            ? (HttpStatusCode.OK, """{"isSuccessful":true,"data":{"setCode":"account-type","items":[{"code":"clinic","label":"Clinic","isActive":true,"attributes":{"label_tr":"Klinik","label_ar":"عيادة"}},{"code":"hospital","label":"Hospital","isActive":true}]}}""")
            : (HttpStatusCode.NotFound, "{}");

        var tr = await LabelsIn("tr", Arrange(Route).Controller);
        Assert.Equal("Klinik", tr.GetProperty("accountTypes").GetProperty("clinic").GetString());
        Assert.Equal("Hospital", tr.GetProperty("accountTypes").GetProperty("hospital").GetString()); // no label_tr yet → the label

        var ar = await LabelsIn("ar", Arrange(Route).Controller);
        Assert.Equal("عيادة", ar.GetProperty("accountTypes").GetProperty("clinic").GetString());

        var en = await LabelsIn("en", Arrange(Route).Controller);
        Assert.Equal("Clinic", en.GetProperty("accountTypes").GetProperty("clinic").GetString());
    }

    // ── 8 · texts: every key the new code reads is in the 7 languages; an argless value has no {0} ─────────────

    [Fact]
    public void The_texts_the_new_code_reads_exist_in_every_language_without_a_stray_placeholder()
    {
        var keys = new[] { "RemoveTarget", "DayCapacityFormat", "TodayLabel", "AccountSelectedOf", "AccountSelectedCount", "AccountDueThisWeek", "AddOutOfTerritoryShort", "IdleTime" };
        var withArgs = new HashSet<string> { "DayCapacityFormat", "AccountSelectedOf", "AccountSelectedCount", "AccountDueThisWeek", "IdleTime" };
        foreach (var culture in Cultures)
        {
            var resx = Resx(culture);
            foreach (var key in keys)
            {
                Assert.True(resx.TryGetValue(key, out var value) && value.Length > 0, $"{key} missing in '{culture}'");
                if (!withArgs.Contains(key)) Assert.DoesNotContain("{0}", value);
            }
        }
        Assert.Equal("Kaldır", Resx("tr")["RemoveTarget"]);
        Assert.Equal("{0} bu hafta", Resx("tr")["AccountDueThisWeek"]);
        Assert.Equal("Bölge dışı ekle", Resx("tr")["AddOutOfTerritoryShort"]);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────────

    private static async Task<JsonElement> LabelsIn(string culture, VisitPlanningController controller)
    {
        var before = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        try
        {
            var result = Assert.IsType<OkObjectResult>(await controller.ReferenceLabels(default));
            return JsonDocument.Parse(JsonSerializer.Serialize(result.Value)).RootElement.GetProperty("data").Clone();
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }

    private static (List<string> Requests, VisitPlanningController Controller) Arrange(Func<HttpMethod, string, (HttpStatusCode, string)> route)
    {
        var gateway = new Gateway(route);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = GatewayUrl }).Build();
        var controller = new VisitPlanningController(new HttpClient(gateway), configuration, NullLogger<VisitPlanningController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim("tenantId", TenantId.ToString()), new Claim("permission", "crm.visit-plan.read")], "test"))
            }
        };
        return (gateway.Requests, controller);
    }

    /// <summary>format.js run in Node for <paramref name="lang"/>; null when Node is not on the machine.</summary>
    private static string? RunFormatter(string lang, string expression)
    {
        var file = Path.Combine(ScriptDir(), "format.js").Replace("\\", "/");
        return RunNode("global.window={};global.document={documentElement:{lang:'" + lang + "'}};require('" + file + "');" +
                       "const F=window.VisitPlanningFormat;process.stdout.write(String(" + expression + "));");
    }

    private static string? RunNode(string code)
    {
        var temp = Path.Combine(Path.GetTempPath(), "vp4i-" + Guid.NewGuid().ToString("N") + ".js");
        File.WriteAllText(temp, code, new UTF8Encoding(false));
        try
        {
            using var process = Process.Start(new ProcessStartInfo("node", "\"" + temp + "\"")
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, StandardOutputEncoding = Encoding.UTF8
            });
            if (process is null) return null;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(20000);
            Assert.True(process.ExitCode == 0, process.StandardError.ReadToEnd());
            return output;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null; // no Node: the code assertions above stand alone
        }
        finally
        {
            File.Delete(temp);
        }
    }

    private static string Between(string text, string start, string end)
    {
        var from = text.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, start);
        var to = text.IndexOf(end, from + start.Length, StringComparison.Ordinal);
        Assert.True(to > from, end);
        return text[from..(to + end.Length)];
    }

    private static string ScriptDir() => Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", "VisitPlanning");

    private static string Script(string file) => File.ReadAllText(Path.Combine(ScriptDir(), file)).Replace("\r\n", "\n");

    private static string View(string file) =>
        File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "VisitPlanning", file)).Replace("\r\n", "\n");

    private static Dictionary<string, string> Resx(string culture)
    {
        var file = "VisitPlanningIndex" + (culture.Length == 0 ? "" : "." + culture) + ".resx";
        return XDocument.Load(Path.Combine(WebRoot(), "Resources", "Views", "CRM", "VisitPlanning", file)).Root!
            .Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")?.Value ?? string.Empty, StringComparer.Ordinal);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }

    private static string WebRoot() => Path.Combine(RepoRoot(), "frontend", "Diten.Web");

    private sealed class Gateway(Func<HttpMethod, string, (HttpStatusCode Status, string Body)> route) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!.ToString();
            Requests.Add(uri);
            var (status, payload) = route(request.Method, uri);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(payload, Encoding.UTF8, "application/json") });
        }
    }
}
