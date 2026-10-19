using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Diten.Web.Controllers.CRM;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VP-4M-WEB — the Targets numbers on the REAL scripts, controller and resources (Acceptance 1–5): "done / required"
/// over "N planned · M remaining", the plan's doctors at the head of the table whatever the quick filter says, the status
/// reads for the SELECTED week, the server's quickCounts. Where Node is on the machine the table and cell code is RUN.
/// </summary>
public sealed class VisitPlanningTargetsNumbersWebTests
{
    private const string GatewayUrl = "http://gateway.test";
    private static readonly Guid TenantId = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");
    private static readonly string[] Cultures = ["", "en", "tr", "fr", "es", "zh", "ar", "ru"];
    private const char Lri = '⁦', Pdi = '⁩';

    // ── 1 · "0 / 13" over "1 planlı · 12 kalan"; no frequency → "—"; the ratio isolated for RTL ───────────────────

    [Fact]
    public void The_done_cell_reads_done_of_required_over_planned_and_remaining()
    {
        var js = Script("details.js");
        var cell = Between(js, "    const doneCell = st => {", "\n    };");
        Assert.Contains("if (st.requiredVisitCount == null) return '<span class=\"text-muted vp-done-cell\" title=\"' + esc(L.DoneCellHint || '') + '\">—</span>';", cell);
        Assert.Contains("esc(VPF.ratio(st.done || 0, st.requiredVisitCount))", cell); // the 4I isolated pair
        Assert.Contains("fmt(L.DoneCellPlannedRemaining || '{0} · {1}', st.planned || 0, st.remaining != null ? st.remaining : '—')", cell);
        Assert.Contains("'<td class=\"text-nowrap\">' + doneCell(row.status) + '</td>'", js);

        var node = RunNode(FormatPrelude("tr") +
            "const L = { DoneCellPlannedRemaining: '{0} planlı · {1} kalan', DoneCellHint: 'ipucu' };\n" +
            "const esc = s => String(s); const fmt = (t, ...a) => a.reduce((x, v, i) => x.split('{' + i + '}').join(String(v)), String(t || ''));\n" +
            cell + "\n" +
            "process.stdout.write(JSON.stringify([doneCell({ requiredVisitCount: 13, done: 0, planned: 1, remaining: 12 }), doneCell({ requiredVisitCount: null, done: 0 })]));");
        if (node is not null)
        {
            var cells = JsonSerializer.Deserialize<string[]>(node)!;
            Assert.Contains("<span>" + Lri + "0 / 13" + Pdi + "</span>", cells[0]);
            Assert.Contains("1 planlı · 12 kalan", cells[0]);
            Assert.Contains("title=\"ipucu\"", cells[0]);
            Assert.Contains(">—</span>", cells[1]);
            Assert.DoesNotContain("planlı", cells[1]);
        }
    }

    // ── 2 · the plan's doctors head the table whatever the quick filter says; filter + counts are the others ─────

    [Fact]
    public void Plan_doctors_head_the_table_independent_of_the_quick_filter_and_the_counts_are_the_others()
    {
        var js = Script("details.js");
        var plan = Between(js, "    const planDoctors = () => {", "\n    };");
        Assert.Contains("const all = doctorRows[doctorKey(activeAccountId, 'all')] || docList;", plan); // NOT the filtered list
        // WP-VW-W2 (WEB-b) — the split is the shared Targets rule (targets-core.js), used by the Visit Workspace too
        Assert.Contains("return TC.splitPlanFirst(all, [], inPlanHere, row => docMatches(row, re)).plan;", plan);
        Assert.Contains("const otherDoctors = () => TC.splitPlanFirst([], visibleDoctors(), inPlanHere).others;", js);
        var split = Between(Script("targets-core.js"), "const splitPlanFirst = (all, visible, inPlan, matches) => {", "\n    };");
        Assert.Contains("plan: (all || []).filter(r => inPlan(r.contactId) && ok(r)),", split);
        Assert.Contains("others: (visible || []).filter(r => !inPlan(r.contactId))", split);
        Assert.Contains("const inPlanHere = cid => !!(activeAccountId && selectedContacts[selKey(activeAccountId, cid)]);", js);
        var draw = Between(js, "    const drawDoctors = () => {", "\n    };");
        Assert.Contains("groupRow('plan', fmt(L.PlanDoctorsHeading || '{0}', plan.length))", draw);
        Assert.Contains("groupRow('other', L.OtherDoctorsHeading || '')", draw);
        Assert.Contains("setText('vp-select-all-count', '(' + rows.filter(r => !r.blocked).length + ')');", draw);
        Assert.Contains("visibleDoctors().forEach(row =>", Between(js, "const selectAllDoctors = () => {", "\n    };")); // the 4H rule

        // RUN it: plan doctors p1 (not due — the "due" filter hides it) + p2; others o1 (due), o2 (never)
        var block = Between(js, "    const docMatches = ", "    const renderDoctorTable = ");
        block = block[..block.LastIndexOf("    const renderDoctorTable = ", StringComparison.Ordinal)];
        var counts = Between(js, "    const paintQuickCounts = ", "    const repaintQuickCounts = ");
        counts = counts[..counts.LastIndexOf("    const repaintQuickCounts = ", StringComparison.Ordinal)];
        var node = RunNode(
            "const TC = require('" + Path.Combine(ScriptDir(), "targets-core.js").Replace("\\", "/") + "');\n" +
            "const L = { PlanDoctorsHeading: 'Planda ({0})', OtherDoctorsHeading: 'Diğer doktorlar' };\n" +
            "const esc = s => String(s); const fmt = (t, ...a) => a.reduce((x, v, i) => x.split('{' + i + '}').join(String(v)), String(t || ''));\n" +
            "const trSearchPattern = t => t; const specLabel = s => s || ''; const selKey = (a, c) => a + '|' + c;\n" +
            "let activeAccountId = 'A', docTerm = '', activeSpecs = [];\n" +
            "const selectedContacts = { 'A|p1': {}, 'A|p2': {} };\n" +
            "const all = [{ contactId: 'p1', name: 'P1', status: {} }, { contactId: 'p2', name: 'P2', status: { dueThisWeek: true } }, { contactId: 'o1', name: 'O1', status: { dueThisWeek: true } }, { contactId: 'o2', name: 'O2', status: { neverVisited: true } }];\n" +
            "const doctorKey = (a, q) => a + '|' + q + '|'; const doctorRows = { 'A|all|': all };\n" +
            "let docList = all.filter(r => r.status.dueThisWeek);\n" +
            "const doctorRowHtml = r => '<tr data-cid=\"' + r.contactId + '\"></tr>';\n" +
            "const nodes = { 'vp-doc-tbody': { innerHTML: '' } }; const texts = {}; const el = id => nodes[id] || null; const setText = (id, v) => { texts[id] = v; };\n" +
            "const repaintQuickCounts = () => {}; const paintExtraBulk = () => {}; const page = null;\n" +
            "const pills = ['all', 'due', 'never'].map(q => ({ dataset: { quick: q }, textContent: '' }));\n" +
            "global.document = { querySelectorAll: () => pills };\n" +
            block + counts +
            "drawDoctors();\n" +
            "const order = [...nodes['vp-doc-tbody'].innerHTML.matchAll(/data-(?:cid|group)=\"([^\"]+)\"/g)].map(m => m[1]).join(',');\n" +
            "paintQuickCounts(all, null); const local = pills.map(p => p.dataset.quick + p.textContent).join(',');\n" +
            "paintQuickCounts(all, { all: 29, due: 10, never: 5 }); const server = pills.map(p => p.dataset.quick + p.textContent).join(',');\n" +
            "process.stdout.write(JSON.stringify([order, texts['vp-select-all-count'], local, server, nodes['vp-doc-tbody'].innerHTML.includes('Planda (2)')]));");
        if (node is not null)
        {
            var r = JsonSerializer.Deserialize<JsonElement[]>(node)!;
            Assert.Equal("plan,p1,p2,other,o1", r[0].GetString()); // p1 shown although the filter hides it; then the others
            Assert.Equal("(1)", r[1].GetString());                   // select all: the others only
            Assert.Equal("all(2),due(1),never(1)", r[2].GetString()); // counted, without the plan doctors
            Assert.Equal("all(27),due(9),never(5)", r[3].GetString()); // the server's quickCounts, without the plan doctors
            Assert.True(r[4].GetBoolean());
        }
    }

    // ── 3 · the reads send the selected week; a week change reloads them (the cache key carries the week) ────────

    [Fact]
    public void The_status_reads_send_the_selected_week_and_reload_on_a_week_change()
    {
        var js = Script("details.js");
        Assert.Contains("const statusWeek = () => { const ws = page ? page.state.weekStart : null; return /^\\d{4}-\\d{2}-\\d{2}$/.test(ws || '') ? ws : ''; };", js);
        Assert.Contains("const weekQuery = sep => (statusWeek() ? sep + 'weekStart=' + encodeURIComponent(statusWeek()) : '');", js);
        Assert.Contains("const doctorKey = (accountId, quick) => accountId + '|' + quick + '|' + statusWeek();", js);
        var fetch = Between(js, "const fetchAccountDoctors = (accountId, quick) => {", "\n    };");
        Assert.Contains("const key = doctorKey(accountId, quick);", fetch);
        Assert.Contains("return api(path + weekQuery('&')).then(r => (!r.ok && r.status === 400 && weekQuery('&') ? api(path) : r))", fetch);
        Assert.Contains("api('/sessions/' + sessionId + '/targets' + weekQuery('?'))", js);
        var reload = Between(js, "const reloadStatusForWeek = () => {", "\n    };");
        Assert.Contains("Object.keys(accountStats).forEach(k => delete accountStats[k]);", reload);
        Assert.Contains("loadAccountStats();", reload);
        Assert.Contains("loadDoctorTable(aid);", reload);
        Assert.Contains("if (page) page.on('week-change', reloadStatusForWeek);", js);
        // the Weeks tab's doctors (period status) too
        var weeks = Script("weeks.js");
        Assert.Contains("'/targets' + targetsWeekQuery()", weeks);
        Assert.Contains("page.on('week-change', () => { openDays.clear(); fullDays.clear(); allDoctorsShown = false; render(); loadTargets(); });", weeks);
    }

    [Fact]
    public async Task The_web_proxy_forwards_the_week_to_both_status_reads()
    {
        var calls = new List<string>();
        var controller = Controller(calls);
        controller.ControllerContext.HttpContext.Request.QueryString = new QueryString("?weekStart=2026-10-12");
        var session = Guid.NewGuid();
        var account = Guid.NewGuid();

        await controller.SessionTargets(session, default);
        await controller.MyAccountDoctors(account, default);

        Assert.Contains($"{GatewayUrl}/api/crm/visit-plan/sessions/{session}/targets?weekStart=2026-10-12", calls);
        Assert.Contains(calls, c => c.StartsWith($"{GatewayUrl}/api/crm/visit-plan/my-accounts/{account}/doctors?", StringComparison.Ordinal) && c.Contains("weekStart=2026-10-12"));
    }

    // ── 4 · quickCounts when the server sends them; the badge says "due this week" for the selected week ─────────

    [Fact]
    public void Quick_counts_come_from_the_server_when_sent_and_the_badge_follows_the_selected_week()
    {
        var js = Script("details.js");
        Assert.Contains("quickCountsBy[key] = d.quickCounts && typeof d.quickCounts === 'object' ? d.quickCounts : null;", js);
        Assert.Contains("const count = TC.quickCounts(all, server, inPlanHere);", Between(js, "const paintQuickCounts = (all, server) => {", "\n    };"));
        var paint = Between(Script("targets-core.js"), "const quickCounts = (all, server, inPlan) => {", "\n    };"); // the shared rule
        Assert.Contains("const base = server && server.all != null", paint);
        Assert.Contains(": { all: list.length, due: list.filter(isDue).length, never: list.filter(isNever).length };", paint); // without them: as before
        Assert.Contains("if (doctorRows[key]) paintQuickCounts(doctorRows[key], quickCountsBy[key]);", js);
        Assert.Contains("row.status && row.status.dueThisWeek ? '<span class=\"badge bg-label-primary\">' + esc(L.DueThisWeekBadge || '')", js);
        Assert.Equal("Bu hafta görülmeli", Resx("tr")["DueThisWeekBadge"]);
        Assert.Equal("Due this week", Resx("en")["DueThisWeekBadge"]);
    }

    // ── 5 · the new texts in 7 languages (+ neutral), bridged; an argless value has no {0} ──────────────────────

    [Fact]
    public void The_new_texts_exist_in_every_language_and_reach_the_page()
    {
        var withArg = new[] { "DoneCellPlannedRemaining", "PlanDoctorsHeading" };
        var argless = new[] { "DoneCellHint", "OtherDoctorsHeading", "DueThisWeekBadge" };
        var en = Resx("en");
        foreach (var culture in Cultures)
        {
            var resx = Resx(culture);
            foreach (var key in withArg.Concat(argless))
            {
                Assert.True(resx.TryGetValue(key, out var value) && value.Trim().Length > 0, $"{key} missing in '{culture}'");
                if (argless.Contains(key)) Assert.DoesNotContain("{0}", value);
                else Assert.Contains("{0}", value);
                if (culture is not ("" or "en")) Assert.NotEqual(en[key], value);
            }
            Assert.Contains("{1}", resx["DoneCellPlannedRemaining"]);
        }
        var tr = Resx("tr");
        Assert.Equal("{0} planlı · {1} kalan", tr["DoneCellPlannedRemaining"]);
        Assert.Equal("Planda ({0})", tr["PlanDoctorsHeading"]);
        Assert.Equal("Diğer doktorlar", tr["OtherDoctorsHeading"]);
        Assert.Equal("Yapılan: raporlu ziyaret · Planlı: raporu henüz olmayan planlı ziyaret · Kalan: gereken − yapılan − planlı", tr["DoneCellHint"]);
        var bridge = File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "VisitPlanning", "_IndexL10n.cshtml"));
        foreach (var key in withArg.Concat(argless))
        {
            Assert.Contains($"{key} = Localizer[\"{key}\"].Value", bridge);
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────────

    private static VisitPlanningController Controller(List<string> calls)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = GatewayUrl }).Build();
        var controller = new VisitPlanningController(new HttpClient(new Gateway(calls)), configuration, NullLogger<VisitPlanningController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim("tenantId", TenantId.ToString()), new Claim("permission", "crm.visit-plan.read")], "test"))
            }
        };
        return controller;
    }

    private static string FormatPrelude(string lang)
    {
        var file = Path.Combine(ScriptDir(), "format.js").Replace("\\", "/");
        return "global.window = {}; global.document = { documentElement: { lang: '" + lang + "' } }; require('" + file + "'); const VPF = window.VisitPlanningFormat;\n";
    }

    /// <summary>Runs <paramref name="code"/> in Node; null when Node is not on the machine (the code assertions stand alone).</summary>
    private static string? RunNode(string code)
    {
        var temp = Path.Combine(Path.GetTempPath(), "vp4m-" + Guid.NewGuid().ToString("N") + ".js");
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

    private static Dictionary<string, string> Resx(string culture)
    {
        var file = "VisitPlanningIndex" + (culture.Length == 0 ? "" : "." + culture) + ".resx";
        return XDocument.Load(Path.Combine(WebRoot(), "Resources", "Views", "CRM", "VisitPlanning", file)).Root!
            .Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")?.Value ?? string.Empty, StringComparer.Ordinal);
    }

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }

    private sealed class Gateway(List<string> calls) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            calls.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"isSuccessful":true,"data":{"items":[]}}""", Encoding.UTF8, "application/json")
            });
        }
    }
}
