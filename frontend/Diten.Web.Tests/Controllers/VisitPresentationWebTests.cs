using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Controllers.CRM;
using Diten.Web.Models.CRM;
using Diten.Web.Views.CRM.VisitPresentation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VW-W3 (W3-WEB-b) — the Visit Presentation page on the REAL controller, view, resx and scripts (Acceptance):
/// (1) UAS-001 + no sidebar entry; (2) the pure rules in Node (navigation + skip product, swipe mirrored in Arabic,
/// timers paused while hidden, the target format with the culture's decimal comma, the §2.2 capture body, the signature
/// card rule, the PIN machine that never finishes); (3) the page in a fake DOM — the doctor area never carries the rep's
/// notes, the remote's tabs, objected → category, finish → POST finish → E4c, the signature → POST signature,
/// phase=summary finishes a started visit, a not-started visit is a note (no redirect); (4) the tablet PIN flow;
/// (5) every proxy → its CRM path with the CRM endpoint's keys; (6) seven languages.
/// </summary>
public sealed class VisitPresentationWebTests
{
    private const string GatewayUrl = "http://gateway.test";
    private const string Id = "11111111-2222-3333-4444-555555555555";
    private static readonly Guid TenantId = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");
    private static readonly string[] Cultures = ["", "en", "tr", "fr", "es", "zh", "ar", "ru"];

    private const string ReportRead = "crm.visit-report.read";
    private const string ExecRead = "crm.visit-execution.read";
    private const string ExecRecord = "crm.visit-execution.record";
    private const string PlanRead = "crm.planned-visit.read";
    private const string People = "platform.meetings.create";

    // ═══ 1 · UAS-001, no sidebar entry ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void A1_without_both_read_keys_the_page_draws_only_the_access_notice_and_has_no_sidebar_entry()
    {
        Assert.False(Model(Arrange(ReportRead).Controller.Index(null, null)).CanRead);
        Assert.False(Model(Arrange(ExecRead).Controller.Index(null, null)).CanRead);
        var reader = Model(Arrange(ReportRead, ExecRead).Controller.Index(Guid.Parse(Id), "summary"));
        Assert.True(reader.CanRead);
        Assert.True(reader.Summary);
        Assert.Equal(Guid.Parse(Id), reader.PlannedVisitId);
        Assert.False(reader.CanRecord || reader.CanReadVisit || reader.CanPickPeople);
        var full = Model(Arrange(ReportRead, ExecRead, ExecRecord, PlanRead, People).Controller.Index(null, "run"));
        Assert.True(full.CanRecord && full.CanReadVisit && full.CanPickPeople && !full.Summary);

        var view = View();
        var denied = view.IndexOf("<partial name=\"_AccessDenied\"", StringComparison.Ordinal);
        var bail = view.IndexOf("return;", denied, StringComparison.Ordinal);
        Assert.True(denied > 0 && bail > denied && view.IndexOf("id=\"vp-root\"", StringComparison.Ordinal) > bail);
        Assert.DoesNotContain("cdn.", view);
        Assert.DoesNotContain("pre-order", view, StringComparison.OrdinalIgnoreCase); // no pre-order (W6)
        Assert.Contains("@if (Model.CanPickPeople)", view);                            // the shared person picker
        Assert.Contains("diten-person-picker.js", view);

        // reached from the workspace only: no menu entry in the CRM manifest
        var manifest = File.ReadAllText(Path.Combine(RepoRoot(), "services", "Diten.Platform", "src", "Diten.Platform.Application",
            "Features", "Crm", "SelfRegistration", "CrmManifestProvider.cs"));
        Assert.DoesNotContain("/CRM/VisitPresentation", manifest);
        // no new JS library: the signature is a plain canvas
        var js = Script("visit-presentation.js");
        Assert.DoesNotContain("SignaturePad", js);
        Assert.Contains("getContext('2d')", js);
    }

    // ═══ 2 · the pure rules (Node) ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void A2_navigation_skip_swipe_timers_target_format_capture_body_signature_and_pin()
    {
        var node = RunNode(Core() + """
            const slides = C.deck({ slides: [
              { index: 2, productId: 'B', stepId: 's3', productName: 'Almiba', role: 'non-promo' },
              { index: 0, productId: 'A', stepId: 's1', productName: 'Tutukon', role: 'promo' },
              { index: 1, productId: 'A', stepId: 's2', productName: 'Tutukon', role: 'promo' } ] });
            const out = {};
            out.order = slides.map(s => s.stepId);
            out.first = slides.map(s => s.firstOfProduct);
            out.step = [C.step(0, -1, 3), C.step(2, 1, 3), C.step(1, 1, 3)];
            out.skip = [C.skipProduct(slides, 0), C.skipProduct(slides, 1), C.skipProduct(slides, 2)];
            out.swipe = [C.swipeStep(-60, false), C.swipeStep(60, false), C.swipeStep(-49, false), C.swipeStep(-60, true)];
            out.device = [C.deviceOf(375), C.deviceOf(1024), C.deviceOf(1440)];
            const t = C.timers({ productTimings: [{ productId: 'A', seconds: 10 }], slideTimings: [{ productId: 'A', stepId: 's1', seconds: 10 }] });
            C.tick(t, slides[0], true, 1); C.tick(t, slides[0], true, 1); C.tick(t, slides[0], false, 1);
            out.timer = [C.productSeconds(t, 'A'), C.slideSeconds(t, slides[0]), C.totalSeconds(t)];
            out.clock = [C.clock(0), C.clock(492), C.clock(3725)];
            const L = { TargetSeconds: '{0} sn', TargetMinutes: '{0} dk' };
            out.target = [C.target(45, 'tr', L), C.target(90, 'tr', L), C.target(90, 'en', { TargetMinutes: '{0} min' }), C.target(120, 'tr', L), C.target(null, 'tr', L)];
            out.bar = [C.slideBar(45, 90), C.slideBar(100, 90), C.slideBar(5, null)];
            const marks = {};
            C.setInterest(marks, 'A', 'interested');
            C.toggleFlag(marks, 'A', 'question');
            C.setObjection(marks, 'A', 'price');
            C.toggleFlag(marks, 'B', 'sample_request');
            C.toggleFlag(marks, 'B', 'gift');
            out.body = C.captureBody({ kind: 'remote', slides, timers: t, marks, note: '  not  ', personalNotes: { A: ' kendi ', B: '  ' },
              coVisitorUserIds: ['u1', 'u2', 'u3', 'u4'] });
            const off = JSON.parse(JSON.stringify(marks)); C.toggleFlag(off, 'A', 'objection');
            out.objectionCleared = off.A.objectionCategory;
            out.defaultKind = C.captureBody({ kind: 'phone', slides: [] }).kind;
            out.sig = [
              C.signatureCard('phone', { doctorConfirmation: true }, marks, null),
              C.signatureCard('tablet', { doctorConfirmation: true }, {}, null),
              C.signatureCard('desktop', { doctorConfirmation: false }, {}, null),
              C.signatureCard('desktop', { doctorConfirmation: false }, marks, null),
              C.signatureCard('desktop', { doctorConfirmation: true }, {}, { signature: { present: true } })];
            out.context = [C.signatureContext(marks), C.signatureContext({})];
            // the PIN: set → confirm → locked; request → unlock; wrong → still locked; right → panel; relock → locked
            let p = C.pinInitial();
            const effects = [];
            const run = ev => { p = C.pin(p, ev); if (p.effect) { effects.push(p.effect); } return p; };
            run({ type: 'request' }); out.requestBeforeSet = p.phase;
            run({ type: 'begin' }); '1234'.split('').forEach(d => run({ type: 'digit', digit: d }));
            out.afterFirst = p.phase;
            '1235'.split('').forEach(d => run({ type: 'digit', digit: d }));
            out.mismatch = [p.phase, p.error];
            '1234'.split('').forEach(d => run({ type: 'digit', digit: d })); '1234'.split('').forEach(d => run({ type: 'digit', digit: d }));
            out.locked = p.phase;
            out.digestOnly = !JSON.stringify(p).includes('1234');
            run({ type: 'request' }); '9999'.split('').forEach(d => run({ type: 'digit', digit: d }));
            out.wrong = [p.phase, p.error];
            '1234'.split('').forEach(d => run({ type: 'digit', digit: d }));
            out.open = p.phase;
            run({ type: 'relock' }); out.relocked = p.phase;
            run({ type: 'request' }); run({ type: 'cancel' }); out.cancelled = p.phase;
            out.effects = effects;
            out.rows = C.summaryRows(slides, { timers: t, marks, categories: { price: 'Fiyat' } },
              { Interest_interested: 'İlgili', Flag_question: 'Soru sordu', Flag_objection: 'İtiraz etti', Flag_sample_request: 'Numune istedi', ObjectionWith: '{0}: {1}' });
            out.brief = C.briefLines({ lastVisitDate: '2026-10-01', outcomeCode: 'positive', followUpNotes: 'x', samples: [{ quantity: 2 }, { quantity: 1 }] },
              { BriefLastVisit: 'Son: {0}', BriefOutcome: 'Sonuç: {0}', BriefFollowUp: 'Takip: {0}', BriefSamples: 'Numune: {0}' });
            out.noBrief = C.briefLines({}, {});
            out.err = [C.errorText(['msg', 'visit_execution_finished'], { Err_visit_execution_finished: 'Bitti', ActionFailed: 'Olmadı' }),
                       C.errorText(['msg', 'unknown_code'], { ActionFailed: 'Olmadı' })];
            process.stdout.write(JSON.stringify(out));
            """);
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement;
        string[] S(string name) => r.GetProperty(name).EnumerateArray().Select(x => x.ToString()).ToArray();

        Assert.Equal(new[] { "s1", "s2", "s3" }, S("order"));
        Assert.Equal(new[] { "True", "False", "True" }, S("first"));
        Assert.Equal(new[] { "0", "2", "2" }, S("step"));
        Assert.Equal(new[] { "2", "2", "-1" }, S("skip"));               // skip → next product; the last product → -1
        Assert.Equal(new[] { "1", "-1", "0", "-1" }, S("swipe"));        // 50 px; Arabic mirrors
        Assert.Equal(new[] { "phone", "tablet", "desktop" }, S("device"));
        Assert.Equal(new[] { "12", "12", "12" }, S("timer"));            // resumed at 10, +2 visible, the hidden second ignored
        Assert.Equal(new[] { "00:00", "08:12", "62:05" }, S("clock"));
        Assert.Equal("45 sn", S("target")[0]);
        Assert.Equal("1,5 dk", S("target")[1]);                          // the Turkish decimal comma
        Assert.Equal("1.5 min", S("target")[2]);
        Assert.Equal("2 dk", S("target")[3]);
        Assert.Equal(JsonValueKind.Null, r.GetProperty("target")[4].ValueKind);
        Assert.Equal(50, r.GetProperty("bar")[0].GetProperty("pct").GetInt32());
        Assert.True(r.GetProperty("bar")[1].GetProperty("over").GetBoolean());
        Assert.Equal(100, r.GetProperty("bar")[1].GetProperty("pct").GetInt32());

        // §2.2 — the capture body
        var body = r.GetProperty("body");
        Assert.Equal(new[] { "kind", "productTimings", "slideTimings", "marks", "note", "personalNotes", "coVisitorUserIds" },
            body.EnumerateObject().Select(p => p.Name));
        Assert.Equal("remote", body.GetProperty("kind").GetString());
        Assert.Equal("[{\"productId\":\"A\",\"seconds\":12},{\"productId\":\"B\",\"seconds\":0}]", body.GetProperty("productTimings").GetRawText());
        Assert.Equal(3, body.GetProperty("slideTimings").GetArrayLength());
        Assert.Equal("{\"productId\":\"A\",\"stepId\":\"s1\",\"seconds\":12}", body.GetProperty("slideTimings")[0].GetRawText());
        var markA = body.GetProperty("marks")[0];
        Assert.Equal("interested", markA.GetProperty("interest").GetString());
        Assert.Equal(new[] { "question", "objection" }, markA.GetProperty("flags").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal("price", markA.GetProperty("objectionCategory").GetString());
        Assert.Equal(new[] { "sample_request" }, body.GetProperty("marks")[1].GetProperty("flags").EnumerateArray().Select(x => x.GetString())); // an unknown flag is dropped
        Assert.Equal("not", body.GetProperty("note").GetString());
        Assert.Equal("[{\"productId\":\"A\",\"text\":\"kendi\"}]", body.GetProperty("personalNotes").GetRawText());
        Assert.Equal(3, body.GetProperty("coVisitorUserIds").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, r.GetProperty("objectionCleared").ValueKind);
        Assert.Equal("in_person", r.GetProperty("defaultKind").GetString());

        Assert.Equal(new[] { "False", "True", "False", "True", "False" }, S("sig")); // never on a phone; confirmation or a sample; once
        Assert.Equal(new[] { "sample", "approval" }, S("context"));

        Assert.Equal("none", r.GetProperty("requestBeforeSet").GetString());
        Assert.Equal("confirm", r.GetProperty("afterFirst").GetString());
        Assert.Equal(new[] { "set", "True" }, S("mismatch"));
        Assert.Equal("locked", r.GetProperty("locked").GetString());
        Assert.True(r.GetProperty("digestOnly").GetBoolean());            // only the digest is kept
        Assert.Equal(new[] { "unlock", "True" }, S("wrong"));
        Assert.Equal("open", r.GetProperty("open").GetString());
        Assert.Equal("locked", r.GetProperty("relocked").GetString());
        Assert.Equal("locked", r.GetProperty("cancelled").GetString());
        Assert.DoesNotContain("finish", S("effects"));                  // leaving the doctor screen NEVER finishes the visit
        Assert.Equal(new[] { "locked", "panel", "locked" }, S("effects"));

        var rowA = r.GetProperty("rows")[0];
        Assert.Equal("Tutukon", rowA.GetProperty("name").GetString());
        Assert.Equal(100, rowA.GetProperty("pct").GetInt32());
        Assert.Equal(new[] { "İlgili", "Soru sordu", "İtiraz etti: Fiyat" }, rowA.GetProperty("tags").EnumerateArray().Select(x => x.GetProperty("text").GetString()));
        Assert.Equal("reminder", r.GetProperty("rows")[1].GetProperty("role").GetString());
        Assert.Equal(new[] { "Son: 2026-10-01", "Sonuç: positive", "Takip: x", "Numune: 3" }, S("brief"));
        Assert.Empty(S("noBrief"));
        Assert.Equal(new[] { "Bitti", "Olmadı" }, S("err"));
    }

    // ═══ 3 · the page (desktop) in a fake DOM ═══════════════════════════════════════════════════════════════

    [Fact]
    public void A3_the_doctor_area_never_shows_rep_notes_and_the_remote_captures_finishes_and_takes_the_signature()
    {
        if (Smoke("run") is not { } r) { return; }
        var doc = r.GetProperty("doc").GetString()!;
        foreach (var secret in new[] { "SECRET TALK TRACK", "SECRET KEY MESSAGE", "SECRET TRANSITION", "SECRET ANSWER", "kendi notum" })
        {
            Assert.DoesNotContain(secret, doc); // E4a: the rep's notes are never in the doctor area
        }
        Assert.Contains("Onaylı iddia 1", doc);
        Assert.Contains("Ref 2024", doc);
        Assert.Contains("1 / 3", doc);

        var rep = r.GetProperty("rep").GetString()!;
        Assert.Contains("SECRET TALK TRACK", rep);
        Assert.Contains("SECRET KEY MESSAGE", rep);
        Assert.Contains("Son ziyaret:", rep);                   // "for this doctor" on the product's first slide
        foreach (var tab in new[] { "notes", "marks", "note" }) { Assert.Contains("data-tab=\"" + tab + "\"", rep); }
        Assert.True(r.GetProperty("myNoteRestored").GetBoolean());
        Assert.Equal("Dr. Ayşe Kaya", r.GetProperty("topbarName").GetString());

        Assert.Equal("Devam ediyor · 00:42", r.GetProperty("liveAfterTwo").GetString());
        Assert.Equal("Devam ediyor · 00:42", r.GetProperty("liveWhileHidden").GetString()); // hidden: the timers stop
        Assert.Equal(1, r.GetProperty("putsOnHide").GetInt32());                            // leaving sends what was counted

        Assert.True(r.GetProperty("categoryPicker").GetBoolean());   // "objected" asks the category
        Assert.True(r.GetProperty("flagLabel").GetBoolean());
        Assert.Contains("Klinik veri", r.GetProperty("docAfterNext").GetString());
        var body = r.GetProperty("captureBody");
        Assert.Equal(3, body.GetProperty("slideTimings").GetArrayLength());
        Assert.Equal("price", body.GetProperty("marks")[0].GetProperty("objectionCategory").GetString());
        Assert.Equal("3 / 3", r.GetProperty("curAfterSkip").GetString());

        Assert.True(r.GetProperty("confirmShown").GetBoolean());
        Assert.Equal(0, r.GetProperty("finishBeforeConfirm").GetInt32());
        Assert.Equal(1, r.GetProperty("finishCalls").GetInt32());
        Assert.True(r.GetProperty("summaryShown").GetBoolean());
        var summary = r.GetProperty("summary").GetString()!;
        Assert.Contains("Ziyaret bitti", summary);
        Assert.Contains("id=\"vp-sig\"", summary);                  // the doctor's confirmation (policy) → the card
        Assert.Contains("Numune teslimi · doktor imzası", summary);  // a sample was requested
        Assert.Contains("Selin Karaca", summary);
        Assert.DoesNotContain("cart", summary);                     // no pre-order button

        var sig = r.GetProperty("signatureCall");
        Assert.True(sig.GetProperty("isForm").GetBoolean());
        Assert.Equal("sample", sig.GetProperty("context").GetString());
        Assert.Equal("true", sig.GetProperty("confirmed").GetString());
        Assert.True(sig.GetProperty("file").GetBoolean());
        Assert.DoesNotContain("id=\"vp-sig\"", r.GetProperty("summaryAfterSign").GetString());

        var calls = r.GetProperty("calls").EnumerateArray().Select(c => c.GetString()!).ToList();
        Assert.Contains("GET /api/presentation/pv1?lang=tr", calls);
        Assert.Contains("GET /api/execution/by-plan/pv1/brief", calls);
        Assert.Contains("PUT /api/execution/ex1/capture", calls);
        Assert.Contains("POST /api/execution/ex1/signature", calls);
    }

    // ═══ 4 · tablet: the PIN; 5 · phase=summary; not started ═════════════════════════════════════════════════

    [Fact]
    public void A4_the_tablet_doctor_mode_locks_behind_the_pin_and_leaving_it_never_finishes_the_visit()
    {
        if (Smoke("tablet") is not { } r) { return; }
        Assert.True(r.GetProperty("panelOpenAtStart").GetBoolean());   // the rep sets the PIN first
        Assert.True(r.GetProperty("confirmPhase").GetBoolean());
        Assert.False(r.GetProperty("lockedPanelOpen").GetBoolean());
        Assert.True(r.GetProperty("handleShown").GetBoolean());
        Assert.Equal(650, r.GetProperty("holdMs").GetInt32());
        Assert.True(r.GetProperty("unlockPadShown").GetBoolean());
        Assert.False(r.GetProperty("wrongPinPanelOpen").GetBoolean());
        Assert.True(r.GetProperty("unlockedPanelOpen").GetBoolean());
        Assert.Equal(0, r.GetProperty("finishCallsAfterExit").GetInt32());
        Assert.False(r.GetProperty("relockedPanelOpen").GetBoolean());
    }

    [Fact]
    public void A5_phase_summary_finishes_a_started_visit_and_a_not_started_one_is_a_note_without_redirect()
    {
        if (Smoke("summary") is { } s)
        {
            Assert.Equal(1, s.GetProperty("finishCalls").GetInt32());
            Assert.True(s.GetProperty("summaryShown").GetBoolean());
            Assert.Contains("Ziyaret bitti", s.GetProperty("summary").GetString());
        }

        if (Smoke("notStarted") is { } n)
        {
            Assert.True(n.GetProperty("noticeShown").GetBoolean());
            Assert.False(n.GetProperty("splitShown").GetBoolean());
            Assert.Contains("Ziyaret başlatılmamış.", n.GetProperty("notice").GetString());
            Assert.Contains("href=\"/ws\"", n.GetProperty("notice").GetString());
            Assert.DoesNotContain(n.GetProperty("calls").EnumerateArray(), c => c.GetString()!.StartsWith("POST", StringComparison.Ordinal));
        }

        Assert.DoesNotContain("location.replace", Script("visit-presentation.js"));
    }

    // ═══ 6 · the proxies ═════════════════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("Presentation", "GET", "/api/crm/visit-presentation/" + Id, ReportRead)]
    [InlineData("Visit", "GET", "/api/crm/planned-visits/" + Id, PlanRead)]
    [InlineData("Contract", "GET", "/api/crm/visit-execution/contract", ExecRead)]
    [InlineData("ByPlan", "GET", "/api/crm/visit-execution/by-plan/" + Id, ExecRead)]
    [InlineData("Brief", "GET", "/api/crm/visit-execution/by-plan/" + Id + "/brief", ExecRead)]
    [InlineData("People", "GET", "/api/v1/meetings/lookups/attendees", People)]
    [InlineData("Capture", "PUT", "/api/crm/visit-execution/" + Id + "/capture", ExecRecord)]
    [InlineData("Finish", "POST", "/api/crm/visit-execution/" + Id + "/finish", ExecRecord)]
    [InlineData("Signature", "POST", "/api/crm/visit-execution/" + Id + "/signature", ExecRecord)]
    public async Task A6_every_proxy_goes_to_its_crm_path_with_exactly_its_key(string action, string method, string path, string key)
    {
        var (denied, deniedController) = Arrange(new[] { ReportRead, ExecRead }.Where(k => k != key).ToArray());
        Assert.Equal(403, Status(await Invoke(deniedController, action)));
        Assert.Empty(denied.Requests);

        var (gateway, controller) = Arrange(key);
        Assert.Equal(200, Status(await Invoke(controller, action)));
        var request = Assert.Single(gateway.Requests);
        Assert.Equal(method, request.Method);
        Assert.Equal(GatewayUrl + path, request.Uri.Split('?')[0]);
        Assert.Equal(TenantId.ToString(), request.Tenant);
        if (action == "Signature")
        {
            Assert.StartsWith("multipart/form-data", request.ContentType); // the browser's multipart is streamed as-is
        }
    }

    [Fact]
    public async Task A6_a_body_with_tenant_id_is_refused_and_the_signature_needs_multipart()
    {
        var (gateway, controller) = Arrange(new[] { ExecRecord }, "{\"tenantId\":\"x\"}", "application/json");
        Assert.Equal(400, Status(await controller.Capture(Guid.Parse(Id), CancellationToken.None)));
        Assert.Equal(400, Status(await controller.Signature(Guid.Parse(Id), CancellationToken.None)));
        Assert.Empty(gateway.Requests);
    }

    // ═══ 7 · seven languages ═════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void A7_every_key_is_filled_in_seven_languages_without_echo_and_with_the_same_arguments()
    {
        var keys = VisitPresentationIndex.ScriptKeys.Concat(VisitPresentationIndex.ViewKeys).ToArray();
        Assert.Equal(keys.Length, keys.Distinct().Count());
        var en = Resx("en");
        foreach (var culture in Cultures)
        {
            var resx = Resx(culture);
            foreach (var key in keys)
            {
                Assert.True(resx.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value), culture + ":" + key);
                Assert.NotEqual(key, value);
                Assert.Equal(Args(en[key]), Args(value));
            }
        }

        // every L.X the scripts read is a published key
        var scripts = Script("visit-presentation.js") + Script("presentation-core.js");
        foreach (Match m in Regex.Matches(scripts, @"\b(?:L|labels)\.([A-Z][A-Za-z_]+)"))
        {
            Assert.Contains(m.Groups[1].Value, VisitPresentationIndex.ScriptKeys);
        }

        var tr = Resx("tr");
        Assert.Equal("Ziyareti bitir", tr["FinishVisit"]);
        Assert.Equal("Kendi notum · {0} · yalnız siz görürsünüz", tr["MyNote"]);
        Assert.Equal("Görüşmeyi onaylıyorum (doktor)", tr["ConfirmVisit"]);
        Assert.Equal("عرض الزيارة", Resx("ar")["PageTitle"]);
        foreach (var culture in new[] { "fr", "es", "zh", "ar", "ru" })
        {
            var resx = Resx(culture);
            // a pure format ("{0} · {1}", "{0} min") or a shared word ("Notes", "Transition" in French) may match English;
            // more than eight matching sentences means the language was not translated
            var echoes = keys.Where(k => resx[k] == en[k] && Regex.Replace(en[k], @"\{\d\}|[\s·:–\-,.]|min|s\b", string.Empty).Length > 0).ToList();
            Assert.True(echoes.Count <= 8, culture + ": too many English echoes: " + string.Join(", ", echoes));
        }

        // RTL: logical properties only; the arrows mirror
        var css = File.ReadAllText(Path.Combine(ScriptDir(), "visit-presentation.css"));
        Assert.Contains("[dir=\"rtl\"] .vp-flip", css);
        Assert.DoesNotMatch(@"(?<![-\w])(margin|padding)-(left|right)\s*:", css);
        Assert.DoesNotMatch(@"(?<![-\w])(left|right)\s*:", css);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────

    private static string[] Args(string value) => Regex.Matches(value, @"\{\d\}").Select(m => m.Value).Distinct().OrderBy(x => x).ToArray();

    private static VisitPresentationIndexViewModel Model(IActionResult result)
        => Assert.IsType<VisitPresentationIndexViewModel>(Assert.IsType<ViewResult>(result).Model);

    private static int? Status(IActionResult result) => result switch
    {
        ContentResult c => c.StatusCode,
        ObjectResult o => o.StatusCode ?? 200,
        StatusCodeResult s => s.StatusCode,
        _ => null
    };

    private static Task<IActionResult> Invoke(VisitPresentationController c, string action) => action switch
    {
        "Presentation" => c.Presentation(Guid.Parse(Id), CancellationToken.None),
        "Visit" => c.Visit(Guid.Parse(Id), CancellationToken.None),
        "Contract" => c.Contract(CancellationToken.None),
        "ByPlan" => c.ByPlan(Guid.Parse(Id), CancellationToken.None),
        "Brief" => c.Brief(Guid.Parse(Id), CancellationToken.None),
        "People" => c.People(CancellationToken.None),
        "Capture" => c.Capture(Guid.Parse(Id), CancellationToken.None),
        "Finish" => c.Finish(Guid.Parse(Id), CancellationToken.None),
        "Signature" => c.Signature(Guid.Parse(Id), CancellationToken.None),
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };

    private static (Gateway Gateway, VisitPresentationController Controller) Arrange(params string[] permissions)
        => Arrange(permissions, "--b\r\nContent-Disposition: form-data; name=\"confirmed\"\r\n\r\ntrue\r\n--b--\r\n", "multipart/form-data; boundary=b");

    private static (Gateway Gateway, VisitPresentationController Controller) Arrange(string[] permissions, string body, string contentType)
    {
        var gateway = new Gateway();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = GatewayUrl }).Build();
        var controller = new VisitPresentationController(new HttpClient(gateway), configuration, NullLogger<VisitPresentationController>.Instance);
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        http.Request.ContentType = contentType;
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return (gateway, controller);
    }

    private static JsonElement? Smoke(string scenario)
    {
        var smoke = Path.Combine(Path.GetDirectoryName(WebRoot())!, "Diten.Web.Tests", "Js", "visit-presentation-smoke.js").Replace("\\", "/");
        var crm = Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM").Replace("\\", "/");
        var node = RunNode("process.argv[2] = '" + crm + "'; process.argv[3] = '" + scenario + "'; require('" + smoke + "');");
        return node is null ? null : JsonDocument.Parse(node).RootElement.Clone();
    }

    private static string Core()
        => "const C = require('" + Path.Combine(ScriptDir(), "presentation-core.js").Replace("\\", "/") + "');\n";

    private static string? RunNode(string code)
    {
        var temp = Path.Combine(Path.GetTempPath(), "vp3-" + Guid.NewGuid().ToString("N") + ".js");
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
            process.WaitForExit(30000);
            Assert.True(process.ExitCode == 0, error);
            return output;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null; // no Node: the code assertions stand alone
        }
        finally
        {
            File.Delete(temp);
        }
    }

    private static string ScriptDir() => Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", "VisitPresentation");

    private static string Script(string file) => File.ReadAllText(Path.Combine(ScriptDir(), file)).Replace("\r\n", "\n");

    private static string View() => File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "VisitPresentation", "Index.cshtml")).Replace("\r\n", "\n");

    private static Dictionary<string, string> Resx(string culture)
    {
        var file = "VisitPresentationIndex" + (culture.Length == 0 ? "" : "." + culture) + ".resx";
        return XDocument.Load(Path.Combine(WebRoot(), "Resources", "Views", "CRM", "VisitPresentation", file)).Root!
            .Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")?.Value ?? string.Empty, StringComparer.Ordinal);
    }

    private static string RepoRoot() => Path.GetDirectoryName(Path.GetDirectoryName(WebRoot())!)!;

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }

    private sealed class Gateway : HttpMessageHandler
    {
        public List<(string Method, string Uri, string? Tenant, string? ContentType)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request.Method.Method, request.RequestUri!.ToString(),
                request.Headers.TryGetValues("X-Tenant-Id", out var t) ? t.Single() : null,
                request.Content?.Headers.ContentType?.ToString()));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":{}}", Encoding.UTF8, "application/json")
            });
        }
    }
}
