using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VW-W6 · W6-FIX-e — the REAL page scripts under node:
/// <list type="bullet">
/// <item>G-15 — the Details cancellation line says "notified" only for a party the CRM recorded as notified, and "no e-mail
/// address, not notified" for a transmitted order's party it could not reach (7 languages);</item>
/// <item>G-16 — the Visit Workspace band, its buttons and the plan panel show the week's new state right after approve /
/// reopen, without waiting for the calendar read (it re-runs the planning engine), and an older calendar read answering
/// after a newer one never puts the old week back;</item>
/// <item>G-17 — beside the outcome badge a finding of that outcome leaves the badge's words out ("iskonto %12 &gt; tavan
/// %10" beside "Girilemez"); "Neden onayımda?" and the refusals keep the full words;</item>
/// <item>G-18 — screen C's history note says the order under decision is not in it (7 languages).</item>
/// </list>
/// </summary>
public sealed class W6FixeWebTests
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
    private static readonly string[] CancelKeys = ["CancelNotifiedDepot", "CancelNotifiedPharmacy", "CancelNoEmailDepot", "CancelNoEmailPharmacy"];

    // ═══ G-16 · the Visit Workspace week band ════════════════════════════════════════════════════════════════════

    [Fact]
    public void G16_after_approve_the_band_says_approved_before_the_calendar_read_and_the_actions_wait_for_it()
    {
        var r = Workspace("approve");
        if (r is null) return;

        Assert.Equal("Taslak", r.Value.GetProperty("start").GetString());
        Assert.Equal("Onaylandı", r.Value.GetProperty("beforeRead").GetString());              // no page refresh, no engine run
        Assert.Equal((false, false), Buttons(r.Value, "during"));                             // no action on the old version
        Assert.Equal("Onaylandı", r.Value.GetProperty("afterRead").GetString());
        Assert.Equal((false, true), Buttons(r.Value, "after"));                               // the CRM's canReopen
    }

    [Fact]
    public void G16_after_reopen_the_band_says_draft_before_the_calendar_read_and_approve_comes_back_with_it()
    {
        var r = Workspace("reopen");
        if (r is null) return;

        Assert.Equal("Onaylandı", r.Value.GetProperty("start").GetString());
        Assert.Equal("Taslak", r.Value.GetProperty("beforeRead").GetString());
        Assert.Equal((false, false), Buttons(r.Value, "during"));
        Assert.Equal("Taslak", r.Value.GetProperty("afterRead").GetString());
        Assert.Equal((true, false), Buttons(r.Value, "after"));
    }

    [Fact]
    public void G16_an_older_calendar_read_answering_last_never_replaces_the_newer_one()
    {
        var r = Workspace("stale");
        if (r is null) return;

        Assert.True(r.Value.GetProperty("headShown").GetBoolean());                           // the shown week is still the current one
        Assert.Equal("Taslak", r.Value.GetProperty("afterRead").GetString());
    }

    [Fact]
    public void G16_the_session_status_is_worded_as_the_calendar_words_it()
    {
        if (RunNode("process.stdout.write('ok')") is null) return;
        var output = RunNode("const C = require('" + Js("CRM/VisitWorkspace/workspace-core.js") + "');"
            + "const s = { weeks: [{ weekStart: '2026-10-19', status: 'approved' }, { weekStart: '2026-10-26', status: 'empty' }, { weekStart: '2026-10-12', status: 'past' }] };"
            + "process.stdout.write(JSON.stringify([C.weekStateOfSession(s, '2026-10-19'), C.weekStateOfSession(s, '2026-10-26'), C.weekStateOfSession(s, '2026-10-12'), C.weekStateOfSession(s, '2026-11-02'), C.weekStateOfSession(null, '2026-10-19')]));")!;

        Assert.Equal("[\"approved\",\"none\",\"past\",null,null]", output);
    }

    // ═══ G-15 · the cancellation line on Details ═════════════════════════════════════════════════════════════════

    [Fact]
    public void G15_notified_only_when_recorded_no_e_mail_on_a_transmitted_order_and_nothing_when_never_transmitted()
    {
        if (RunNode("process.stdout.write('ok')") is null) return;
        var tr = Page("tr");
        string State(string order) => JsonSerializer.Deserialize<string>(RunNode(Ui("tr") + "process.stdout.write(JSON.stringify(U.stateHtml(" + order + ")));")!)!;
        const string sent = "transmission: { transmittedAt: '2026-10-23T08:00:00Z' }, depot: { name: 'Selçuk' }";

        var both = State("{ " + sent + ", cancellation: { at: '2026-10-23T09:00:00Z', reason: 'x', notifiedDepot: true, notifiedPharmacy: true } }");
        Assert.Contains(tr["CancelNotifiedDepot"], both);
        Assert.Contains(tr["CancelNotifiedPharmacy"], both);
        Assert.DoesNotContain(tr["CancelNoEmailPharmacy"], both);

        var noPharmacyMail = State("{ " + sent + ", cancellation: { at: '2026-10-23T09:00:00Z', reason: 'x', notifiedDepot: true, notifiedPharmacy: false } }");
        Assert.Contains(tr["CancelNotifiedDepot"], noPharmacyMail);
        Assert.DoesNotContain(tr["CancelNotifiedPharmacy"], noPharmacyMail);                  // G-15: never "notified" without a mail
        Assert.Contains(tr["CancelNoEmailPharmacy"], noPharmacyMail);

        var noDepotMail = State("{ " + sent + ", cancellation: { at: '2026-10-23T09:00:00Z', reason: 'x', notifiedDepot: false, notifiedPharmacy: true } }");
        Assert.Contains(tr["CancelNoEmailDepot"], noDepotMail);
        Assert.Contains(tr["CancelNotifiedPharmacy"], noDepotMail);

        var never = State("{ cancellation: { at: '2026-10-23T09:00:00Z', reason: 'x', notifiedDepot: false, notifiedPharmacy: false } }");
        Assert.DoesNotContain("po-cancel-notice", never);                                     // nobody outside was told, nothing to say
    }

    [Fact]
    public void G15_the_four_cancellation_words_exist_in_every_language_and_travel_to_the_page()
    {
        var english = Page("en");
        foreach (var language in Languages)
        {
            var words = Page(language);
            foreach (var key in CancelKeys)
            {
                Assert.False(string.IsNullOrWhiteSpace(words.GetValueOrDefault(key)), language + " " + key);
                if (language != "en") Assert.NotEqual(english[key], words[key]);
            }
        }

        Assert.Equal(english.Where(x => CancelKeys.Contains(x.Key)).OrderBy(x => x.Key), Page("").Where(x => CancelKeys.Contains(x.Key)).OrderBy(x => x.Key));
        Assert.All(CancelKeys, k => Assert.Contains(k, Diten.Web.Views.Commercial.PreOrders.PreOrdersIndex.ScriptKeys));
    }

    // ═══ G-17 · the badge word is not repeated ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void G17_beside_the_badge_the_reason_has_no_badge_word_and_why_on_me_keeps_the_full_words()
    {
        if (RunNode("process.stdout.write('ok')") is null) return;
        var rules = Rules("tr");
        string Run(string expr) => JsonSerializer.Deserialize<string>(RunNode(Ui("tr") + "process.stdout.write(JSON.stringify(" + expr + "));")!)!;
        const string hard = "{ code: 'discount.hard-exceeded', severity: 'hard', params: { value: '12', limit: '10' } }";
        const string soft = "{ code: 'discount.soft-exceeded', severity: 'soft', params: { value: '7', limit: '5' } }";

        var line = Run("U.lineResult({ outcome: 'hard', findings: [" + hard + "] }, { priced: true, currency: 'TRY' })");
        Assert.Contains(rules["Rule.Outcome.hard"], line);                                    // the badge
        Assert.Contains("iskonto %<bdi>12</bdi> &gt; tavan %<bdi>10</bdi>", line);
        Assert.Equal(1, Count(line, rules["Rule.Outcome.hard"]));                             // "Girilemez" once, not twice

        var table = Run("U.linesTable({ priceMode: 'priced', currency: 'TRY', lines: [{ lineNo: 1, quantity: 20, sku: { name: 'TUTUKON' }, outcome: 'hard', findings: [" + hard + "] }] })");
        Assert.Equal(1, Count(table, rules["Rule.Outcome.hard"]));

        // a soft finding beside a HARD badge keeps "Yönetici onayı gerekir" (its own outcome is not the badge's)
        var mixed = Run("U.lineResult({ outcome: 'hard', findings: [" + hard + ", " + soft + "] }, { priced: true, currency: 'TRY' })");
        Assert.Contains("Yönetici onayı gerekir: iskonto %<bdi>7</bdi>", mixed);

        // "Neden onayımda?" has no badge: the full words stay
        var why = Run("U.whyHtml({ approvalRequirement: { reasons: ['soft-exceeded'] }, lines: [{ lineNo: 1, sku: { name: 'TUTUKON' }, outcome: 'soft', findings: [" + soft + "] }] })");
        Assert.Contains("Yönetici onayı gerekir: iskonto %<bdi>7</bdi> &gt; sınır %<bdi>5</bdi>", why);
        // the refusal words and the plain finding text are unchanged
        Assert.StartsWith(rules["Rule.Outcome.hard"] + ":", Run("window.CommercialRules.text(" + hard + ")"));
    }

    [Fact]
    public void G17_every_reason_code_has_its_badge_less_words_in_every_language_with_the_same_parameters()
    {
        foreach (var language in Languages)
        {
            var words = Rules(language);
            foreach (var reason in words.Keys.Where(k => k.StartsWith("Rule.Reason.", StringComparison.Ordinal)))
            {
                var full = words["Rule." + reason["Rule.Reason.".Length..]];
                var bare = words[reason];
                Assert.False(string.IsNullOrWhiteSpace(bare), language + " " + reason);
                Assert.EndsWith(bare, full);                                                   // the same message, the outcome words left out
                Assert.NotEqual(full, bare);
                Assert.DoesNotContain(words["Rule.Outcome.hard"], bare);
                Assert.DoesNotContain(words["Rule.Outcome.soft"], bare);
            }

            Assert.Equal(12, words.Keys.Count(k => k.StartsWith("Rule.Reason.", StringComparison.Ordinal)));
        }
    }

    // ═══ G-18 · the history note ═════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void G18_the_history_note_says_this_order_is_not_in_it_in_every_language()
    {
        var marker = new Dictionary<string, string>
        {
            ["en"] = "this order excluded", ["tr"] = "bu sipariş hariç", ["fr"] = "hors cette commande", ["es"] = "sin contar este pedido",
            ["zh"] = "不含本订单", ["ar"] = "باستثناء هذا الطلب", ["ru"] = "без учёта этого заказа"
        };
        foreach (var language in Languages)
        {
            Assert.Contains(marker[language], Page(language)["HistoryNote"]);
        }

        Assert.Contains("@Localizer[\"HistoryNote\"]", File.ReadAllText(Path.Combine(WebRoot(), "Views", "Commercial", "PreOrders", "Approve.cshtml")));
    }

    // ═══ helpers ═════════════════════════════════════════════════════════════════════════════════════════════════

    private static int Count(string text, string word)
    {
        var n = 0;
        for (var i = text.IndexOf(word, StringComparison.Ordinal); i >= 0; i = text.IndexOf(word, i + word.Length, StringComparison.Ordinal)) n++;
        return n;
    }

    private static (bool Approve, bool Reopen) Buttons(JsonElement r, string name)
        => (r.GetProperty(name).GetProperty("approve").GetBoolean(), r.GetProperty(name).GetProperty("reopen").GetBoolean());

    /// <summary>The pre-order UI over the page + rule words of <paramref name="lang"/> (the page's own payloads).</summary>
    private static string Ui(string lang)
    {
        var words = Page(lang).Concat(Rules(lang)).GroupBy(x => x.Key).ToDictionary(g => g.Key, g => g.First().Value);
        return "globalThis.L10n = " + JsonSerializer.Serialize(words) + "; globalThis.document = { documentElement: { lang: '" + lang + "' }, getElementById: () => null };"
               + "globalThis.window = globalThis;"
               + "require('" + Js("Commercial/PreOrders/preorders-core.js") + "');"
               + "require('" + Js("Commercial/commercial-rules.js") + "');"
               + "const U = require('" + Js("Commercial/PreOrders/preorders-ui.js") + "');\n";
    }

    /// <summary>
    /// The REAL visit-workspace.js over a stub DOM and a fetch whose calendar reads answer when the test says so (the CRM
    /// state at the moment of the read). approve / reopen: the band before and after the calendar read; stale: six reads
    /// in flight (‹ ›), the oldest answering last.
    /// </summary>
    private static JsonElement? Workspace(string scenario)
    {
        if (RunNode("process.stdout.write('ok')") is null) return null;
        var output = RunNode("const SCENARIO = '" + scenario + "'; const CORE = '" + Js("CRM/VisitWorkspace/workspace-core.js") + "'; const FORMAT = '"
                             + Js("CRM/VisitPlanning/format.js") + "'; const PAGE = '" + Js("CRM/VisitWorkspace/visit-workspace.js") + "';\n" + Harness);
        return JsonDocument.Parse(output!).RootElement.Clone();
    }

    private const string Harness = """
        const fs = require('fs');
        const els = {};
        const mk = id => {
          const cls = new Set(); const attrs = { 'data-can-apply': 'true' };
          return els[id] = { id, textContent: '', innerHTML: '', value: '', disabled: false, style: {}, dataset: {},
            get className() { return [...cls].join(' '); }, set className(v) { cls.clear(); String(v).split(/\s+/).filter(Boolean).forEach(c => cls.add(c)); },
            classList: { add: c => cls.add(c), remove: c => cls.delete(c), contains: c => cls.has(c), toggle: (c, on) => { if (on === undefined ? !cls.has(c) : on) cls.add(c); else cls.delete(c); } },
            setAttribute: (k, v) => { attrs[k] = String(v); }, getAttribute: k => (k in attrs ? attrs[k] : null),
            addEventListener: (n, f) => { (els[id].on = els[id].on || {})[n] = f; }, querySelectorAll: () => [], querySelector: () => null, closest: () => null };
        };
        globalThis.window = globalThis; globalThis.innerWidth = 1400; globalThis.addEventListener = () => {};
        globalThis.document = { getElementById: id => els[id] || mk(id), querySelectorAll: () => [], querySelector: () => null, addEventListener: () => {}, documentElement: { lang: 'tr' } };
        globalThis.localStorage = { getItem: () => null, setItem: () => {} };
        globalThis.VisitWorkspaceL10n = { WeekStateDraft: 'Taslak', WeekStateApproved: 'Onaylandı', WeekLabel: '{0}. Hafta' };
        require(CORE);
        new Function('window', 'document', fs.readFileSync(FORMAT, 'utf8'))(globalThis, globalThis.document);
        const C = globalThis.VisitWorkspaceCore;
        const monday = C.mondayOf(new Date().toISOString().slice(0, 10));
        let server = SCENARIO === 'reopen' ? 'approved' : 'draft', version = 3;
        const week = () => ({ weekStart: monday, weekNumber: 43, state: server, planningSessionId: '11111111-1111-1111-1111-111111111111', sessionVersion: version,
          canApprove: server === 'draft', canReopen: server === 'approved', plannedMinutes: 60, capacityMinutes: 600, unplacedCount: 0 });
        const pending = [];
        const respond = body => ({ ok: true, status: 200, text: () => Promise.resolve(JSON.stringify(body)) });
        globalThis.fetch = (url, init) => {
          const m = (init && init.method) || 'GET';
          if (url.indexOf('/calendar?') > -1) {
            const q = new URLSearchParams(url.slice(url.indexOf('?') + 1));
            const inWindow = q.get('from') <= monday && monday <= q.get('to');
            const snapshot = { data: { weeks: inWindow ? [week()] : [], days: [], visits: [] } };   // what the CRM reads NOW
            return new Promise(res => pending.push(() => res(respond(snapshot))));
          }
          if (m === 'GET' && url.indexOf('/sessions/') > -1) return Promise.resolve(respond({ data: { version, weeks: [{ weekStart: monday, status: server }] } }));
          if (url.indexOf('/apply') > -1) { server = 'approved'; version++; return Promise.resolve(respond({ data: { weekStart: monday, weekStatus: 'approved' } })); }
          if (url.indexOf('/reopen') > -1) { server = 'draft'; version++; return Promise.resolve(respond({ data: { weekStart: monday, status: 'reopened' } })); }
          return Promise.resolve(respond({ data: null }));
        };
        const tick = () => new Promise(r => setTimeout(r, 5));
        const band = () => els['vw-week-badge'] ? els['vw-week-badge'].textContent : null;
        const btns = () => ({ approve: !els['vw-approve'].classList.contains('d-none'), reopen: !els['vw-reopen'].classList.contains('d-none') });
        (async () => {
          new Function('window', 'document', fs.readFileSync(PAGE, 'utf8'))(globalThis, globalThis.document);
          await tick(); pending.shift()(); await tick();
          const out = { start: band() };
          if (SCENARIO === 'stale') {
            for (let i = 0; i < 3; i++) { els['vw-next'].on.click(); await tick(); }
            for (let i = 0; i < 3; i++) { els['vw-prev'].on.click(); await tick(); }
            const order = [5, 0, 1, 3, 4, 2];                     // the newest first, the one without this week last
            const calls = pending.splice(0); for (const i of order) { if (calls[i]) { calls[i](); await tick(); } }
            out.headShown = !els['vw-week-head'].classList.contains('d-none'); out.afterRead = band();
          } else {
            if (SCENARIO === 'approve') { els['vw-approve'].on.click(); }
            else { els['vw-reopen-reason'].value = 'Hekim izinli, plan değişti'; els['vw-reopen-confirm'].on.click(); }
            await tick(); await tick();
            out.beforeRead = band(); out.during = btns();
            while (pending.length) { pending.shift()(); await tick(); }
            out.afterRead = band(); out.after = btns();
          }
          process.stdout.write(JSON.stringify(out));
        })();
        """;

    private static Dictionary<string, string> Resx(string relative)
        => XDocument.Load(Path.Combine(WebRoot(), "Resources", relative)).Root!
            .Elements("data").ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")?.Value ?? string.Empty, StringComparer.Ordinal);

    private static Dictionary<string, string> Page(string culture)
        => Resx(Path.Combine("Views", "Commercial", "PreOrders", "PreOrdersIndex" + (culture.Length == 0 ? "" : "." + culture) + ".resx"));

    private static Dictionary<string, string> Rules(string culture)
        => Resx(Path.Combine("Views", "Commercial", "CommercialRules" + (culture.Length == 0 ? "" : "." + culture) + ".resx"));

    private static string Js(string relative) => Path.Combine(WebRoot(), "wwwroot", "assets", "js", relative).Replace("\\", "/");

    /// <summary>Runs <paramref name="code"/> in Node; null when Node is not on the machine (the C# assertions stand alone).</summary>
    private static string? RunNode(string code)
    {
        var temp = Path.Combine(Path.GetTempPath(), "w6fe-" + Guid.NewGuid().ToString("N") + ".js");
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

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }
}
