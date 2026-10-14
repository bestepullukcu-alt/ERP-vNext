using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Controllers.CRM;
using Diten.Web.Models.CRM;
using Diten.Web.Views.CRM.ObjectionResponses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VW-W3 · W3-FIX — the live E4 fixes on the REAL view, resx, controller and scripts: F1 the A2 example placeholders
/// (7 languages, no echo); F2 a record not saved yet names the session user (server-given, never guessed) and "not saved
/// yet"; F3 a Work Center machine reason code (WORKCENTER_*) is never a note on either timeline, a real comment and a CRM
/// refusal stay; F4 the shared toast announces the PLAIN sentence to screen readers (Notyf's live region is textContent);
/// F5 the evidence settings show CRM's display names, a short id when unresolved.
/// </summary>
public sealed class VwW3FixWebTests
{
    private static readonly string[] Cultures = ["", "en", "tr", "fr", "es", "zh", "ar", "ru"];
    private static readonly string[] Languages = ["tr", "fr", "es", "zh", "ar", "ru"];

    // ═══ F1 · placeholders ═════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void F1_the_question_and_answer_boxes_carry_the_mockup_examples_in_seven_languages()
    {
        var view = Source("Views", "CRM", "ObjectionResponses", "Edit.cshtml");
        Assert.Matches(@"<textarea id=""orQuestion""[^>]*placeholder=""@Localizer\[""QuestionPlaceholder""\]""", view);
        Assert.Matches(@"<textarea id=""orAnswer""[^>]*placeholder=""@Localizer\[""AnswerPlaceholder""\]""", view);
        Assert.Contains("QuestionPlaceholder", ObjectionResponsesIndex.ViewKeys);
        Assert.Contains("AnswerPlaceholder", ObjectionResponsesIndex.ViewKeys);
        Assert.Contains("NotSavedYet", ObjectionResponsesIndex.ScriptKeys);

        var tr = Resx("tr");
        Assert.Equal("ör. Tamsulin ile memnunum, neden değiştireyim?", tr["QuestionPlaceholder"]);
        Assert.Equal("Temsilcinin söyleyeceği yanıt. Yalnız onaylı iddialara dayanın.", tr["AnswerPlaceholder"]);
        Assert.Equal("Henüz kaydedilmedi", tr["NotSavedYet"]);
        var en = Resx("en");
        foreach (var culture in Cultures)
        {
            var values = Resx(culture);
            foreach (var key in new[] { "QuestionPlaceholder", "AnswerPlaceholder", "NotSavedYet" })
            {
                Assert.False(string.IsNullOrWhiteSpace(values.GetValueOrDefault(key)), $"{culture}:{key} missing");
                Assert.NotEqual(key, values[key]);                                        // no key echo
                if (Languages.Contains(culture)) Assert.NotEqual(en[key], values[key]);   // translated, not English
            }
        }

        Assert.Matches(@"\p{IsArabic}", Resx("ar")["AnswerPlaceholder"]);
    }

    // ═══ F2 · the creator of a record not saved yet ═══════════════════════════════════════════════════════

    [Fact]
    public void F2_a_new_record_names_the_session_user_given_by_the_server_and_says_not_saved_yet()
    {
        var model = Page(new Claim(ClaimTypes.GivenName, "Ayşe"), new Claim(ClaimTypes.Surname, "Yılmaz"), new Claim("email", "ayse@x.test"));
        Assert.Equal("Ayşe Yılmaz", model.MeName);
        Assert.Equal("ayse", Page(new Claim("email", "ayse@x.test")).MeName);           // no name claims → e-mail's local part
        Assert.Equal("Admin User", Page(new Claim("name", "Admin User")).MeName);
        Assert.Equal(string.Empty, KnowledgePathStudioReview.DisplayNameOf(new ClaimsPrincipal(new ClaimsIdentity())));

        Assert.Contains(@"data-me-name=""@Model.MeName""", Source("Views", "CRM", "ObjectionResponses", "Edit.cshtml"));
        var edit = Source("wwwroot", "assets", "js", "CRM", "ObjectionResponses", "objection-edit.js");
        Assert.Contains("root.dataset.meName", edit);
        Assert.Contains("C.editorMeta(d, names, meName, L)", edit);

        var node = RunNode(ObjectionCore() + """
            const L = { NotSavedYet: 'Henüz kaydedilmedi', UnknownUser: 'Bilinmeyen kullanıcı' };
            console.log(JSON.stringify({
              fresh: C.editorMeta(null, {}, ' Ayşe Yılmaz ', L),
              noName: C.editorMeta(null, {}, '', L),
              saved: C.editorMeta({ createdBy: 'AAAA-1' }, { 'aaaa-1': 'Sema Pullukcu' }, 'Ayşe Yılmaz', L),
              unknown: C.editorMeta({ createdBy: 'bbbb-2' }, {}, 'Ayşe Yılmaz', L)
            }));
            """);
        if (node is null) return;
        var r = JsonDocument.Parse(node).RootElement;
        Assert.Equal(("Ayşe Yılmaz", "Henüz kaydedilmedi"), (r.GetProperty("fresh").GetProperty("by").GetString(), r.GetProperty("fresh").GetProperty("at").GetString()));
        Assert.Equal("—", r.GetProperty("noName").GetProperty("by").GetString());
        Assert.Equal("Sema Pullukcu", r.GetProperty("saved").GetProperty("by").GetString());   // a saved record names its creator, not me
        Assert.Equal("Bilinmeyen kullanıcı", r.GetProperty("unknown").GetProperty("by").GetString());
    }

    // ═══ F3 · WORKCENTER_* is not a note ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void F3_neither_timeline_shows_a_work_center_machine_code_a_real_comment_and_a_refusal_stay()
    {
        var node = RunNode(ObjectionCore() + "const E = require('" + ScriptPath("VisitEvidenceSettings", "evidence-core.js") + "');\n" + """
            const at = i => '2026-10-13T0' + i + ':00:00Z';
            const answer = {
              status: 'approved', createdAt: at(1), createdBy: 'a',
              reviewRounds: [
                { roundNo: 1, submittedAt: at(2), submittedBy: 'a', closedAt: at(3), completedBy: 's', outcome: 'rejected', reasonCode: 'WORKCENTER_REJECT' },
                { roundNo: 2, submittedAt: at(4), submittedBy: 'a', closedAt: at(5), completedBy: 's', outcome: 'approved', reasonCode: 'WORKCENTER_APPROVE' },
                { roundNo: 3, submittedAt: at(6), submittedBy: 'a', closedAt: at(7), completedBy: 'a', outcome: 'approved', reasonCode: 'objection_response_sod' }],
              decisions: [
                { roundNo: 1, by: 's', outcome: 'return', comment: 'Kaynak belge eksik.', at: at(3) },
                { roundNo: 2, by: 's', outcome: 'approve', comment: 'WORKCENTER_APPROVE', at: at(5) }]
            };
            const draft = { status: 'draft', reviewRounds: [{ roundNo: 1, closedAt: at(3), reasonCode: 'WORKCENTER_REJECT' }],
              decisions: [{ roundNo: 1, by: 's', outcome: 'return', comment: 'WORKCENTER_REJECT', at: at(3) }] };
            const exception = { createdAt: at(1), createdBy: 'a',
              reviewRounds: [{ roundNo: 1, submittedAt: at(2), submittedBy: 'a', closedAt: at(3), outcome: 'approved', reasonCode: 'WORKCENTER_APPROVE' },
                             { roundNo: 2, submittedAt: at(4), submittedBy: 'a', closedAt: at(5), outcome: 'rejected', reasonCode: null }],
              decisions: [{ roundNo: 1, by: 's', outcome: 'approve', comment: 'WORKCENTER_APPROVE', at: at(3) },
                          { roundNo: 2, by: 's', outcome: 'return', comment: 'Tarih aralığı fazla uzun.', at: at(5) }] };
            const tl = C.timeline(answer, {}, '?');
            console.log(JSON.stringify({
              machine: [C.isMachineCode('WORKCENTER_APPROVE'), C.isMachineCode('workcenter_approve'), C.isMachineCode('objection_response_sod'),
                        E.isMachineCode(' WORKCENTER_REQUEST_INFO '), E.isMachineCode('Kaynak belge eksik.')],
              notes: tl.map(e => e.comment || null).filter(Boolean),
              reasons: tl.map(e => e.reasonCode || null).filter(Boolean),
              returned: C.returnNote(draft),
              evidence: E.timeline(exception).map(e => e.note).filter(Boolean)
            }));
            """);
        if (node is null) return;
        var r = JsonDocument.Parse(node).RootElement;
        Assert.Equal("[true,false,false,true,false]", r.GetProperty("machine").GetRawText());
        Assert.Equal(new[] { "Kaynak belge eksik." }, r.GetProperty("notes").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(new[] { "objection_response_sod" }, r.GetProperty("reasons").EnumerateArray().Select(e => e.GetString()).Distinct());
        var returned = r.GetProperty("returned");
        Assert.Equal("returned", returned.GetProperty("kind").GetString());           // a return, not a "reason" line with the code
        Assert.Equal(JsonValueKind.Null, returned.GetProperty("comment").ValueKind);
        Assert.Equal(JsonValueKind.Null, returned.GetProperty("reasonCode").ValueKind);
        Assert.Equal(new[] { "Tarih aralığı fazla uzun." }, r.GetProperty("evidence").EnumerateArray().Select(e => e.GetString()));
    }

    // ═══ F4 · the toast's screen-reader announcement ═════════════════════════════════════════════════════

    [Fact]
    public void F4_the_shared_toast_announces_the_plain_sentence_and_draws_it_escaped_once()
    {
        var view = Source("Views", "Shared", "_GlobalNotification.cshtml");
        var script = view[view.IndexOf("var _notyfInstance = null;", StringComparison.Ordinal)..view.LastIndexOf("</script>", StringComparison.Ordinal)];
        script = Regex.Replace(script, @"@Json\.Serialize\(SharedLocalizer\[""([^""]+)""\]\.Value\)", "\"$1\"");
        // A Notyf with the vendored one's two sinks: the toast (innerHTML) and the live region (_announce → textContent).
        var node = RunNode("""
            const shown = [];
            function Notyf() {
              const self = this;
              this.view = { announced: null, _announce(m) { this.announced = m; } };
              const open = m => { shown.push(m); self.view._announce(m); };
              this.success = open; this.error = open; this.open = o => open(o.message);
            }
            const window = {};
            const console = { log() {}, warn() {}, error() {} };
            const tf = (k, ...a) => a.reduce((t, v, i) => t.split('{' + i + '}').join(String(v)), k);
            """ + "\n" + script + "\n" + """
            const sentence = tf("{1} — '{0}' işlemi tamamlandı", 'Onayla', 'A&B "Q3" <taslak>');
            window.showToast(sentence, 'success');
            const notyf = (typeof _notyfInstance !== 'undefined') ? _notyfInstance : null;
            console.info = null;
            process.stdout.write(JSON.stringify({ sentence, shown: shown[0], announced: notyf.view.announced,
              roundTrip: window.DitenToastPlain(window.DitenToastText(sentence)) }));
            """);
        if (node is null) return;
        var r = JsonDocument.Parse(node).RootElement;
        var sentence = r.GetProperty("sentence").GetString();
        Assert.Equal("A&B \"Q3\" <taslak> — 'Onayla' işlemi tamamlandı", sentence);
        Assert.Equal(sentence, r.GetProperty("announced").GetString());                   // the live region: plain text
        Assert.DoesNotContain("&#39;", r.GetProperty("announced").GetString());
        Assert.Equal("A&amp;B &quot;Q3&quot; &lt;taslak&gt; — &#39;Onayla&#39; işlemi tamamlandı", r.GetProperty("shown").GetString()); // innerHTML: once
        Assert.Equal(sentence, r.GetProperty("roundTrip").GetString());

        // the Work Center hands showToast the raw sentence (no caller-side escaping) — the one door does it
        var app = Source("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
        Assert.Contains("toast(tf('ToastActionApplied', label, item.title));", app);
        Assert.Contains("rememberFlashToast(tf('ToastActionApplied', label, item.title));", app);
    }

    // ═══ F5 · display names on the evidence settings ═══════════════════════════════════════════════════════

    [Fact]
    public void F5_the_evidence_settings_name_people_from_crm_and_fall_back_to_a_short_id()
    {
        var page = Source("wwwroot", "assets", "js", "CRM", "VisitEvidenceSettings", "visit-evidence-settings.js");
        Assert.Contains("const nameOf = id => C.personLabel(id, state.people, state.repNames, me, L);", page);
        foreach (var read in new[] { "const p = absorb(data(r));", "const h = absorb(data(r));", "const list = absorb(data(r));",
                     "const x = absorb(data(r));", "state.current = absorb(data(r)) || x;" })
        {
            Assert.Contains(read, page);   // policy (last change), history, list, detail, after an action
        }

        var node = RunNode("const E = require('" + ScriptPath("VisitEvidenceSettings", "evidence-core.js") + "');\n" + """
            const L = { YouLabel: 'Siz', WorkflowActor: 'İş akışı' };
            const me = 'C5769C62-1111-4444-8888-000000000001';
            const people = E.mergeNames({}, { 'c5769c62-1111-4444-8888-000000000001': 'Admin User', 'd27fa4a6-24e3-42af-a0d8-1e285863f19a': 'Sema Pullukcu' });
            E.mergeNames(people, null);
            console.log(JSON.stringify([
              E.personLabel('D27FA4A6-24E3-42AF-A0D8-1E285863F19A', people, {}, me, L),
              E.personLabel(me, people, {}, me, L),
              E.personLabel(me, {}, {}, me, L),
              E.personLabel('99999999-0000-0000-0000-000000000000', people, {}, me, L),
              E.personLabel('rep-1', people, { 'rep-1': 'Ayşe Temsilci' }, me, L),
              E.personLabel('workflow:system', people, {}, me, L),
              E.personLabel(null, people, {}, me, L)
            ]));
            """);
        if (node is null) return;
        Assert.Equal("[\"Sema Pullukcu\",\"Admin User\",\"Siz\",\"99999999…\",\"Ayşe Temsilci\",\"İş akışı\",\"—\"]", node.Trim());
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────

    private static ObjectionResponsesPageViewModel Page(params Claim[] claims)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = "http://gateway.test" }).Build();
        var controller = new ObjectionResponsesController(new HttpClient(), configuration, NullLogger<ObjectionResponsesController>.Instance);
        var all = new List<Claim>(claims) { new("permission", "crm.objection-response.read"), new("permission", "crm.objection-response.manage") };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(all, "test")) }
        };
        return Assert.IsType<ObjectionResponsesPageViewModel>(Assert.IsType<ViewResult>(controller.Create(null)).Model);
    }

    private static string ObjectionCore() => "const C = require('" + ScriptPath("ObjectionResponses", "objection-core.js") + "');\n";

    private static string ScriptPath(string folder, string file)
        => Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", folder, file).Replace("\\", "/");

    private static string Source(params string[] parts) => File.ReadAllText(Path.Combine(new[] { WebRoot() }.Concat(parts).ToArray())).Replace("\r\n", "\n");

    private static Dictionary<string, string> Resx(string culture)
    {
        var file = "ObjectionResponsesIndex" + (culture.Length == 0 ? "" : "." + culture) + ".resx";
        return XDocument.Load(Path.Combine(WebRoot(), "Resources", "Views", "CRM", "ObjectionResponses", file)).Root!
            .Elements("data").ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")?.Value ?? string.Empty, StringComparer.Ordinal);
    }

    /// <summary>Runs <paramref name="code"/> in Node; null when Node is not on the machine (the source assertions stand alone).</summary>
    private static string? RunNode(string code)
    {
        var temp = Path.Combine(Path.GetTempPath(), "w3fix-" + Guid.NewGuid().ToString("N") + ".js");
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
