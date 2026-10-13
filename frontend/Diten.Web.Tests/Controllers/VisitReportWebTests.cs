using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Controllers.CRM;
using Diten.Web.Models.CRM;
using Diten.Web.Views.CRM.VisitReport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VW-W4 (W4-WEB-a) — the visit report wizard v2 on the REAL controller, view, resx and scripts (Acceptance):
/// (1) UAS-001 + no sidebar entry; (2) the pure rules in Node — the completeness (the server's rules + the total rule),
/// the total rule, the opportunity preview = the §2.3 server table, the next-message suggestion, the 60-minute counter;
/// (3) the page in a fake DOM — the pre-fill, the "none" options, the competitor proposal, the total rule blocks the
/// send, submit → a refused 400 shows the server's missing answers, the amendment needs its reason, createError null;
/// (4) every proxy → its path with its keys; (5) seven languages, RTL, dark; (6) the entries from the workspace / the
/// presentation; (7) the journey editor's adoption stage picker.
/// </summary>
public sealed class VisitReportWebTests
{
    private const string GatewayUrl = "http://gateway.test";
    private const string Id = "11111111-2222-3333-4444-555555555555";
    private static readonly Guid TenantId = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");
    private static readonly string[] Cultures = ["", "en", "tr", "fr", "es", "zh", "ar", "ru"];

    private const string Read = "crm.visit-report.read";
    private const string Record = "crm.visit-report.record";
    private const string Amend = "crm.visit-report.amend";
    private const string PlanRead = "crm.planned-visit.read";
    private const string PlanManage = "crm.planned-visit.manage";
    private const string ExecRead = "crm.visit-execution.read";
    private const string InsightsRead = "crm.doctor-insights.read";
    private const string InsightsAll = "crm.doctor-insights.read-all";
    private const string CompRead = "mdm.competitor-products.read";
    private const string CompPropose = "mdm.competitor-products.propose";
    private const string GskuRead = "mdm.gskus.read";
    private const string ProductRead = "mdm.global-products.read";

    // ═══ 1 · UAS-001, no sidebar entry ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void A1_without_the_read_key_only_the_access_notice_and_every_flag_is_its_own_key()
    {
        Assert.False(Model(Arrange(Record, PlanManage, Amend, ExecRead).Controller.Index(null)).CanRead);
        var reader = Model(Arrange(Read).Controller.Index(Guid.Parse(Id)));
        Assert.True(reader.CanRead);
        Assert.Equal(Guid.Parse(Id), reader.PlannedVisitId);
        Assert.False(reader.CanRecord || reader.CanAmend || reader.CanReadVisit || reader.CanReadExecution || reader.CanReadInsights
                     || reader.CanLookupCompetitors || reader.CanProposeCompetitors || reader.CanReadGskus || reader.CanPickProducts);
        Assert.False(Model(Arrange(Read, Record).Controller.Index(null)).CanRecord); // the record needs planned-visit.manage too
        var full = Model(Arrange(Read, Record, PlanManage, Amend, PlanRead, ExecRead, InsightsAll, CompRead, CompPropose, GskuRead, ProductRead).Controller.Index(null));
        Assert.True(full.CanRecord && full.CanAmend && full.CanReadVisit && full.CanReadExecution && full.CanReadInsights && full.CanLookupCompetitors
                    && full.CanProposeCompetitors && full.CanReadGskus && full.CanPickProducts);

        var view = View("Index.cshtml");
        var denied = view.IndexOf("<partial name=\"_AccessDenied\"", StringComparison.Ordinal);
        var bail = view.IndexOf("return;", denied, StringComparison.Ordinal);
        Assert.True(denied > 0 && bail > denied && view.IndexOf("id=\"vr-root\"", StringComparison.Ordinal) > bail);
        Assert.DoesNotContain("cdn.", view);
        Assert.Contains("Context.Items[\"NavActivePath\"] = \"/CRM/VisitWorkspace\";", view);
        var manifest = File.ReadAllText(Path.Combine(RepoRoot(), "services", "Diten.Platform", "src", "Diten.Platform.Application",
            "Features", "Crm", "SelfRegistration", "CrmManifestProvider.cs"));
        Assert.DoesNotContain("/CRM/VisitReport", manifest); // reached from the workspace / the presentation only
        Assert.DoesNotContain("location.replace", Script("visit-report.js"));
        Assert.DoesNotContain("pharmacy-stock", Script("visit-report.js")); // the pharmacy report is WEB-b
    }

    // ═══ 2 · the pure rules (Node) ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void A2_completeness_total_rule_opportunity_suggestion_and_the_60_minute_counter()
    {
        var node = RunNode(Core() + """
            const P = 'p1', Q = 'p2';
            const prefill = { reportKind: 'doctor', contactId: 'c1',
              body: { general: { visitKindCode: 'in_person' }, productSections: [{ productId: P }, { productId: Q, unplanned: true }] },
              potentials: [{ productId: P, isDue: true, threshold: { lowBelow: 10, highAbove: 30 } }, { productId: Q, isDue: false, indicationTotal: 35 }],
              profile: { totalIsDue: false, totalMonthlyPatients: 40 },
              competitors: [{ productId: P, available: true, items: [] }, { productId: Q, available: false, items: [] }] };
            const out = {};
            const ctx = C.contextOf(prefill);
            out.ctx = ctx;
            const b = C.bodyOf(prefill);
            out.blank = C.missing(b, ctx, { prefill }).map(m => m.section + ':' + (m.productId || '') + ':' + m.field);
            // the adverse event: unanswered → missing; "yes" → its note; "no" → nothing
            b.safety.adverseEvent = true; out.aeYes = C.missing(b, ctx, { prefill }).filter(m => m.section === 'safety').map(m => m.field);
            b.safety.adverseEventNote = 'baş ağrısı'; out.aeNote = C.missing(b, ctx, { prefill }).filter(m => m.section === 'safety').map(m => m.field);
            // the total rule: asked rows above the doctor's 40 → product; a stored product above a lowered total → doctor
            b.productSections[0].potential = { rows: [{ indicationNodeId: 'i1', monthlyPatients: 25 }, { indicationNodeId: 'i2', monthlyPatients: 20 }] };
            out.rule45 = C.totalRule(b, prefill, ctx);
            b.productSections[0].potential.rows[1].monthlyPatients = 15;
            out.rule40 = C.totalRule(b, prefill, ctx);
            b.doctor.totalMonthlyPatients = 30;
            out.ruleLowered = C.totalRule(b, prefill, ctx).map(r => r.productId + ':' + r.asked);
            out.loweredMissing = C.missing(b, ctx, { prefill }).filter(m => /Total|total/.test(m.field)).map(m => m.section + ':' + m.field);
            b.doctor.totalMonthlyPatients = null;
            out.noTotal = C.totalRule(b, { profile: {} }, ctx);
            // "none" options stand for the lists
            const s = b.productSections[0];
            Object.assign(s, { minutes: 3, visitSuccessCode: 'reached', commitmentCode: 'none', interestCode: 'neutral', adoptionStageCode: 'aware', shareBucketCode: 'half',
              noCompetitor: true, noObjection: true, noRequest: true, noSample: true, nextMessage: 'x' });
            out.productAfterNone = C.missing(b, ctx, { prefill }).filter(m => m.productId === P).map(m => m.field);
            s.objections = [{ categoryCode: null, resolved: 'maybe' }]; s.noObjection = false;
            out.objectionRow = C.missing(b, ctx, { prefill }).filter(m => m.productId === P).map(m => m.field);
            // potential classes at the boundaries + THE matrix (every class × share)
            const th = { lowBelow: 10, highAbove: 30 };
            out.classes = [9, 10, 30, 31, null].map(n => C.potentialClass(n, th));
            out.matrix = ['high', 'medium', 'low'].map(c => C.SHARE_BUCKETS.map(x => C.opportunity(c, x)));
            out.matrixNull = [C.opportunity(null, 'half'), C.opportunity('high', null), C.opportunity('high', 'bogus')];
            // the next-message suggestion (the server's rule) and its words
            out.sug = [C.suggestionKey('will_try', 'aware'), C.suggestionKey('none', 'aware'), C.suggestionKey('none', 'advocate'), C.suggestionKey(null, null), C.suggestionKey('wants_info', null)];
            const L = { SuggestStage: 'Sonraki aşama: {0}', Suggest_commitment_will_try: 'Nasıl gitti?' };
            out.sugText = [C.suggestionText('stage:evaluating', L, c => 'TR-' + c), C.suggestionText('commitment:will_try', L), C.suggestionText(null, L)];
            // the 60-minute free correction
            const at = '2026-10-15T10:00:00Z', ms = m => Date.parse(at) + m * 60000;
            out.win = [0, 15, 59, 60, 61].map(m => C.editWindow(at, ms(m))).concat([C.editWindow('bogus', ms(0)), C.editWindow(at, ms(-5))]);
            out.hours = [C.hoursLeft('2026-10-17T10:00:00Z', ms(0)), C.hoursLeft('2026-10-15T09:00:00Z', ms(0)), C.hoursLeft(null, 0)];
            // the steps + where a missing answer jumps
            out.steps = C.steps('doctor', b, {}).map(x => x.key);
            out.stepOf = [C.stepOf({ section: 'product', productId: P }, 'doctor'), C.stepOf({ section: 'safety' }, 'doctor'), C.stepOf({ section: 'doctor' }, 'doctor'), C.stepOf({ section: 'summary' }, 'doctor')];
            out.byStep = C.missingByStep([{ section: 'product', productId: P }, { section: 'product', productId: P }, { section: 'safety' }], 'doctor');
            // the request body: no server member, the note of a "no" adverse event dropped, numbers as numbers
            b.safety.adverseEvent = false;
            s.presentedSteps = [{ stepId: 'x', seconds: 3 }]; s.matchedPlan = true; s.nextMessageSuggestion = 'stage:aware'; s.minutes = '7';
            const rb = C.requestBody('pv1', b, 3);
            out.rb = { v: rb.expectedVersion, steps: 'presentedSteps' in rb.productSections[0], plan: 'matchedPlan' in rb.productSections[0], sug: 'nextMessageSuggestion' in rb.productSections[0],
              minutes: rb.productSections[0].minutes, aeNote: rb.safety.adverseEventNote, tenant: 'tenantId' in rb };
            const ab = C.amendBody('  neden ', 'pv1', b, 5);
            out.ab = [ab.reason, ab.expectedVersion, ab.v2.plannedVisitId];
            out.amendNeedsReason = C.missing(b, ctx, { prefill, amend: true, reason: ' ' }).some(m => m.field === 'amendReason');
            // options in the page language, competitors, the library, the GSKUs
            const ps = { optionSets: { 'crm-share-bucket': [{ code: 'none', label: 'None', labels: { label_tr: 'Hiç' } }, { code: 'half', label: 'Half' }] } };
            out.optTr = C.options(ps, 'crm-share-bucket', 'tr').map(o => o.label);
            out.optEn = C.options(ps, 'crm-share-bucket', 'en').map(o => o.label);
            const sec = C.emptySection('p1');
            C.toggleCompetitor(sec, { id: 'k1', name: 'Uroflo', status: 'active' });
            const prop = C.addProposal(sec, ' Yeni ', ' Firma ', { id: 'np1', status: 'proposed', existing: false });
            const later = C.addProposal(sec, 'Sonra', 'F', null);
            out.comps = sec.competitors.map(c => [c.competitorProductId, c.nameSnapshot, c.company, c.proposed, c.proposalForwarded]);
            out.noBoth = C.addProposal(sec, 'Ad', '', null);
            const c0 = sec.competitors[0];
            out.adv = [C.cycleAdvantage(c0, 'price'), C.cycleAdvantage(c0, 'price'), C.cycleAdvantage(c0, 'price'), JSON.stringify([c0.advantageCodes, c0.disadvantageCodes])];
            out.lib = C.libraryAnswers({ slides: [{ productId: 'p1', objections: [{ id: 'o1', categoryCode: 'price', question: 'Q1' }, { id: 'o2', categoryCode: 'safety', question: 'Q2' }] },
              { productId: 'p1', objections: [{ id: 'o1', categoryCode: 'price', question: 'Q1' }] }, { productId: 'p9', objections: [{ id: 'o3', categoryCode: 'price' }] }] }, 'p1', 'price').map(a => a.id);
            out.gsku = C.gskuOptions([{ id: 'g1', globalProductId: 'p1', globalProductName: 'Ürofleks', packQuantity: 28, packUomCode: 'tablet', canonicalCode: 'G1' },
              { id: 'g2', globalProductId: 'p2' }, { id: 'g3', globalProductId: 'p1', lifecycleStatus: 'Retired' }], 'p1').map(g => g.id + '|' + g.label);
            out.changes = C.changesOf([{ at: 'x', reason: 'r', changes: [{ path: 'safety.adverseEvent', old: 'false', new: 'true' }, { path: 'p.nextMessage', old: null, new: '"Selam"' }] }])[0].changes;
            out.err = [C.errorText(['m', 'report_incomplete'], { Err_report_incomplete: 'Eksik', ActionFailed: 'Olmadı' }), C.errorText(['m', 'x'], { ActionFailed: 'Olmadı' })];
            process.stdout.write(JSON.stringify(out));
            """);
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement;
        string[] S(string name) => r.GetProperty(name).EnumerateArray().Select(x => x.ToString()).ToArray();

        // the completeness context from the pre-fill
        Assert.Equal(new[] { "p1" }, r.GetProperty("ctx").GetProperty("plannedProductIds").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(new[] { "p1" }, r.GetProperty("ctx").GetProperty("duePotentialProductIds").EnumerateArray().Select(x => x.GetString()));
        Assert.False(r.GetProperty("ctx").GetProperty("totalDue").GetBoolean());
        // a blank body: exactly the server's rules (VisitReportV2Rules.Missing) — competitor only where the list came
        var blank = S("blank");
        foreach (var expected in new[] { "safety::adverseEvent", "general::metContactId", "safety::offLabelRequest", "product:p1:minutes", "product:p1:visitSuccessCode",
                     "product:p1:commitmentCode", "product:p1:interestCode", "product:p1:adoptionStageCode", "product:p1:shareBucketCode", "product:p1:potential",
                     "product:p1:competitors", "product:p1:objections", "product:p1:requests", "product:p1:sample", "product:p1:nextMessage", "product:p2:minutes" })
        {
            Assert.Contains(expected, blank);
        }

        Assert.DoesNotContain("product:p2:competitors", blank); // MDM did not answer for p2: the competitor answer is not required
        Assert.DoesNotContain("product:p2:potential", blank);   // not due
        Assert.DoesNotContain("doctor::totalMonthlyPatients", blank);
        Assert.DoesNotContain("general::visitKindCode", blank);
        Assert.Equal(new[] { "adverseEventNote", "offLabelRequest" }, S("aeYes"));
        Assert.Equal(new[] { "offLabelRequest" }, S("aeNote"));

        // the total rule
        var rule45 = Assert.Single(r.GetProperty("rule45").EnumerateArray());
        Assert.Equal((45, 40, true), (rule45.GetProperty("sum").GetInt32(), rule45.GetProperty("total").GetInt32(), rule45.GetProperty("asked").GetBoolean()));
        Assert.Empty(r.GetProperty("rule40").EnumerateArray()); // equal is allowed
        Assert.Equal(new[] { "p1:true", "p2:false" }, S("ruleLowered")); // 40 > 30 asked; the stored 35 > 30
        Assert.Equal(new[] { "product:potentialExceedsTotal", "doctor:totalBelowProducts" }, S("loweredMissing"));
        Assert.Empty(r.GetProperty("noTotal").EnumerateArray());
        Assert.Empty(S("productAfterNone"));
        Assert.Equal(new[] { "objections.categoryCode", "objections.resolved" }, S("objectionRow"));

        // the classes and THE matrix (§2.3 / OpportunityRules.Classify)
        Assert.Equal(new[] { "low", "medium", "medium", "high", "" }, S("classes"));
        var matrix = r.GetProperty("matrix").EnumerateArray().Select(row => row.EnumerateArray().Select(x => x.GetString()).ToArray()).ToArray();
        Assert.Equal(new[] { "opportunity", "opportunity", "grow", "keep", "keep" }, matrix[0]);
        Assert.Equal(new[] { "grow", "grow", "grow", "keep", "keep" }, matrix[1]);
        Assert.Equal(new[] { "low_priority", "low_priority", "low_priority", "keep", "keep" }, matrix[2]);
        Assert.All(r.GetProperty("matrixNull").EnumerateArray(), x => Assert.Equal(JsonValueKind.Null, x.ValueKind));
        var server = File.ReadAllText(Path.Combine(RepoRoot(), "services", "Diten.CrmService", "src", "Diten.CrmService.Application", "Features", "DoctorInsights", "DoctorInsightsRules.cs"));
        Assert.Contains("PotentialClasses.High => low ? OpportunityCodes.Opportunity : OpportunityCodes.Grow", server); // the table the preview repeats
        Assert.Contains("PotentialClasses.Low => OpportunityCodes.LowPriority", server);

        // the suggestion
        Assert.Equal(new[] { "commitment:will_try", "stage:evaluating", "", "", "commitment:wants_info" }, S("sug"));
        Assert.Equal(new[] { "Sonraki aşama: TR-evaluating", "Nasıl gitti?", "" }, S("sugText"));

        // the 60 minutes
        var win = r.GetProperty("win").EnumerateArray().Select(w => w.GetProperty("open").GetBoolean() + ":" + w.GetProperty("minutesLeft").GetInt32()).ToArray();
        Assert.Equal(new[] { "True:60", "True:45", "True:1", "False:0", "False:0", "False:0", "False:0" }, win);
        Assert.Equal(new[] { "48", "0", "" }, S("hours"));

        Assert.Equal(new[] { "general", "p:p1", "p:p2", "safety", "doctor", "summary" }, S("steps"));
        Assert.Equal(new[] { "p:p1", "safety", "doctor", "summary" }, S("stepOf"));
        Assert.Equal(2, r.GetProperty("byStep").GetProperty("p:p1").GetInt32());

        var rb = r.GetProperty("rb");
        Assert.Equal(3, rb.GetProperty("v").GetInt32());
        Assert.False(rb.GetProperty("steps").GetBoolean() || rb.GetProperty("plan").GetBoolean() || rb.GetProperty("sug").GetBoolean() || rb.GetProperty("tenant").GetBoolean());
        Assert.Equal(7, rb.GetProperty("minutes").GetInt32());
        Assert.Equal(JsonValueKind.Null, rb.GetProperty("aeNote").ValueKind);
        Assert.Equal(new[] { "neden", "5", "pv1" }, S("ab"));
        Assert.True(r.GetProperty("amendNeedsReason").GetBoolean());

        Assert.Equal(new[] { "Hiç", "Half" }, S("optTr"));
        Assert.Equal(new[] { "None", "Half" }, S("optEn"));
        var comps = r.GetProperty("comps").EnumerateArray().Select(c => string.Join("|", c.EnumerateArray().Select(x => x.ToString()))).ToArray();
        Assert.Equal(new[] { "k1|Uroflo||False|False", "np1|Yeni|Firma|True|True", "|Sonra|F|True|False" }, comps);
        Assert.Equal(JsonValueKind.Null, r.GetProperty("noBoth").ValueKind);
        Assert.Equal(new[] { "+", "-", "", "[[],[]]" }, S("adv"));
        Assert.Equal(new[] { "o1" }, S("lib"));
        Assert.Equal(new[] { "g1|Ürofleks · 28 tablet · G1" }, S("gsku"));
        var changes = r.GetProperty("changes").EnumerateArray().Select(c => c.GetProperty("path").GetString() + ":" + c.GetProperty("old").GetString() + "→" + c.GetProperty("new").GetString()).ToArray();
        Assert.Equal(new[] { "safety.adverseEvent:false→true", "p.nextMessage:—→Selam" }, changes);
        Assert.Equal(new[] { "Eksik", "Olmadı" }, S("err"));
    }

    // ═══ 3 · the page in a fake DOM ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void A3_prefill_none_options_proposal_total_rule_safety_profile_draft_and_submit()
    {
        if (Smoke("doctor") is not { } r) { return; }
        Assert.False(r.TryGetProperty("crash", out _), r.TryGetProperty("crash", out var crash) ? crash.GetString() : "");
        // the pre-fill: the doctor met, the presentation's note, the timer minutes, the interest mark, the objection mark
        Assert.Equal("Dr. Ayşe Kaya · Hacettepe · 2026-10-15", r.GetProperty("title").GetString());
        Assert.True(r.GetProperty("metChecked").GetBoolean());
        Assert.True(r.GetProperty("noteFromPresentation").GetBoolean());
        var steps = r.GetProperty("steps").GetString()!;
        foreach (var s in new[] { "Genel", "Ürofleks", "Takip ve güvenlik", "Doktor profili", "Özet ve gönder" }) { Assert.Contains(s, steps); }
        var a = r.GetProperty("productA").GetString()!;
        Assert.Contains("Gece idrarı mesajı", a);                                  // the steps really shown (W3), read-only
        Assert.Contains("<span class=\"vr-tag vr-ok\">plana uydu</span>", a);      // the server's plan match
        Assert.Contains("data-path=\"productSections.0.minutes\" data-type=\"number\" value=\"2\"", a);
        Assert.Contains("vr-seg-btn active vr-t-ok\" data-act=\"set\" data-path=\"productSections.0.interestCode\" data-value=\"interested\"", a);
        Assert.Contains("TR-aware", a);                                           // the previous visit's stage
        Assert.Contains("Fiyat", a);                                              // the pre-filled objection category
        Assert.Contains("50+ erkek", a);                                          // the indication × profile row
        Assert.DoesNotContain("star", a);                                         // S1: no stars
        Assert.DoesNotContain("%", Regex.Replace(a, "<[^>]+>", " "));             // S2 / K-W4: no percentages
        Assert.True(r.GetProperty("suggestion").GetBoolean());                    // will_try → "how did it go"

        // the total rule: 50 > 40 warns and blocks the send (no POST); 30 clears it
        Assert.True(r.GetProperty("totalWarning").GetBoolean());
        Assert.Contains("toplamı aşıyor", r.GetProperty("summaryAtBlock").GetString());
        Assert.Equal(0, r.GetProperty("submitsBlockedByTotal").GetInt32());
        Assert.False(r.GetProperty("totalWarningAfter").GetBoolean());
        Assert.Equal("opportunity", r.GetProperty("preview").GetString());      // high (50 > 30) × none

        // the competitor: the proposal goes to MDM with name + company + our product; the card waits for approval
        var proposal = r.GetProperty("proposalBody");
        Assert.Equal("Yeni Rakip", proposal.GetProperty("name").GetString());
        Assert.Equal("Başka AŞ", proposal.GetProperty("company").GetString());
        Assert.Equal("aaaaaaaa-0000-0000-0000-00000000000a", proposal.GetProperty("productId").GetString());
        Assert.True(r.GetProperty("proposalCard").GetBoolean());
        var card = r.GetProperty("compCard").GetString()!;
        Assert.Contains("tamsulosin", card);
        Assert.Contains("Geri ödemede", card);                                    // the BRD label of the reimbursement status
        Assert.Contains("123,5", card);                                           // the TR box price (tr number format)
        Assert.DoesNotContain("Pazar payı", card);                                // market share is W7
        Assert.True(r.GetProperty("libraryShown").GetBoolean());                  // the W3 library answer in the row
        Assert.True(r.GetProperty("sampleSigned").GetBoolean());
        Assert.True(r.GetProperty("noRequestActive").GetBoolean());               // a "none" option
        Assert.Contains("2 alan eksik", r.GetProperty("missingAfterProduct").GetString()); // only the two safety questions left

        // safety: "yes" asks the note; the doctor card: a change is marked; the history opens
        var safety = r.GetProperty("safety").GetString()!;
        Assert.Contains("data-path=\"safety.adverseEvent\" data-value=\"true\"", safety);
        Assert.Contains("data-path=\"safety.offLabelRequest\"", safety);
        Assert.True(r.GetProperty("aeNoteAsked").GetBoolean());
        var doctor = r.GetProperty("doctor").GetString()!;
        Assert.Contains("kayıtta", doctor);
        Assert.Contains("Yüz yüze", doctor);                                       // the preferred channel (read, not copied)
        Assert.True(r.GetProperty("doctorChanged").GetBoolean());
        Assert.True(r.GetProperty("history").GetBoolean());

        // the draft saved itself; the first save raised no error
        Assert.True(r.GetProperty("drafts").GetInt32() >= 1);
        Assert.Equal(JsonValueKind.Null, r.GetProperty("createError").ValueKind);
        Assert.Contains("Gönderime hazır", r.GetProperty("missingBeforeSubmit").GetString());

        // the submit body
        var body = r.GetProperty("submitBody");
        Assert.Equal("pv1", body.GetProperty("plannedVisitId").GetString());
        Assert.False(body.TryGetProperty("tenantId", out _));
        var sec = body.GetProperty("productSections")[0];
        Assert.Equal("evaluating", sec.GetProperty("adoptionStageCode").GetString());
        Assert.Equal("none", sec.GetProperty("shareBucketCode").GetString());
        Assert.Equal(30, sec.GetProperty("potential").GetProperty("rows")[0].GetProperty("monthlyPatients").GetInt32());
        Assert.Equal("or1", sec.GetProperty("objections")[0].GetProperty("objectionResponseId").GetString());
        Assert.Equal("partly", sec.GetProperty("objections")[0].GetProperty("resolved").GetString());
        Assert.True(sec.GetProperty("noRequest").GetBoolean());
        Assert.Equal("Nasıl gitti? Deneme hastalarını sor", sec.GetProperty("nextMessage").GetString());
        Assert.False(sec.TryGetProperty("presentedSteps", out _));
        var compsSent = sec.GetProperty("competitors").EnumerateArray().ToArray();
        Assert.Equal("np1", compsSent[0].GetProperty("competitorProductId").GetString());
        Assert.True(compsSent[0].GetProperty("proposed").GetBoolean());
        Assert.Equal(new[] { "efficacy" }, compsSent[1].GetProperty("advantageCodes").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(new[] { "price" }, compsSent[1].GetProperty("disadvantageCodes").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal("national", body.GetProperty("doctor").GetProperty("kolLevelCode").GetString());
        Assert.False(body.GetProperty("safety").GetProperty("adverseEvent").GetBoolean());
        Assert.Contains("Rapor gönderildi", r.GetProperty("banner").GetString());
        var calls = r.GetProperty("calls").EnumerateArray().Select(c => c.GetString()!).ToList();
        Assert.Contains("GET /api/prefill/pv1", calls);
        Assert.Contains("PUT /api/draft", calls);
        Assert.Contains("POST /api/submit", calls);
        Assert.Contains("GET /api/gskus?pageSize=100&pageNumber=1&search=UROF", calls);
    }

    [Fact]
    public void A4_a_refused_submit_shows_the_servers_missing_answers_and_jumps_to_them()
    {
        if (Smoke("incomplete") is not { } r) { return; }
        Assert.True(r.GetProperty("serverMissing").GetBoolean());
        Assert.True(r.GetProperty("stepAfter400").GetBoolean());   // the summary step
        Assert.True(r.GetProperty("gotoProduct").GetBoolean());    // a missing row → its step
        Assert.Equal(JsonValueKind.Null, r.GetProperty("createError").ValueKind);
    }

    [Fact]
    public void A5_a_sent_report_is_read_only_inside_60_minutes_it_unlocks_after_it_the_amendment_needs_a_reason()
    {
        if (Smoke("unlock") is { } u)
        {
            Assert.Contains("Değiştir (50 dk)", u.GetProperty("banner").GetString());
            Assert.DoesNotContain("data-act=\"fix\"", u.GetProperty("banner").GetString());
        }

        if (Smoke("amend") is not { } r) { return; }
        Assert.Contains("data-act=\"fix\"", r.GetProperty("banner").GetString());
        Assert.DoesNotContain("data-act=\"unlock\"", r.GetProperty("banner").GetString());
        Assert.True(r.GetProperty("fieldsetDisabled").GetBoolean());
        Assert.Contains("Gerekçeli düzeltme", r.GetProperty("fixBanner").GetString());
        Assert.Equal(0, r.GetProperty("amendWithoutReason").GetInt32()); // no reason, no POST
        Assert.Contains("düzeltme gerekçesi", r.GetProperty("toastNoReason").GetString());
        Assert.Equal("/api/reports/r1/amend", r.GetProperty("amendUrl").GetString());
        var amend = r.GetProperty("amendBody");
        Assert.Equal("Yanlış pay girilmişti", amend.GetProperty("reason").GetString());
        Assert.Equal(4, amend.GetProperty("expectedVersion").GetInt32());
        Assert.Equal("half", amend.GetProperty("v2").GetProperty("productSections")[0].GetProperty("shareBucketCode").GetString());
        Assert.Contains("Düzeltmeler", r.GetProperty("afterAmend").GetString());     // the old → new rows
        Assert.Equal(0, r.GetProperty("drafts").GetInt32());                         // no draft save on a sent report
    }

    // ═══ 4 · the proxies ═════════════════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("Prefill", "GET", "/api/crm/visit-report/v2/prefill", new[] { Read })]
    [InlineData("Report", "GET", "/api/crm/visit-report/" + Id, new[] { Read })]
    [InlineData("Draft", "PUT", "/api/crm/visit-report/v2/draft", new[] { Record, PlanManage })]
    [InlineData("Submit", "POST", "/api/crm/visit-report/v2/submit", new[] { Record, PlanManage })]
    [InlineData("Amend", "POST", "/api/crm/visit-report/" + Id + "/amend", new[] { Amend })]
    [InlineData("Visit", "GET", "/api/crm/planned-visits/" + Id, new[] { PlanRead })]
    [InlineData("Execution", "GET", "/api/crm/visit-execution/by-plan/" + Id, new[] { ExecRead })]
    [InlineData("Presentation", "GET", "/api/crm/visit-presentation/" + Id, new[] { Read })]
    [InlineData("Profile", "GET", "/api/crm/doctor-insights/doctors/" + Id + "/profile", new[] { InsightsRead })]
    [InlineData("Potentials", "GET", "/api/crm/doctor-insights/doctors/" + Id + "/potentials", new[] { InsightsRead })]
    [InlineData("IndicationProfiles", "GET", "/api/crm/doctor-insights/products/" + Id + "/indication-profiles", new[] { InsightsRead })]
    [InlineData("CompetitorLookup", "GET", "/api/mdm/competitor-products/lookup", new[] { CompRead })]
    [InlineData("CompetitorProposal", "POST", "/api/mdm/competitor-products/proposals", new[] { CompPropose })]
    [InlineData("Gskus", "GET", "/api/gskus", new[] { GskuRead })]
    [InlineData("Products", "GET", "/api/global-products/selector", new[] { ProductRead })]
    public async Task A6_every_proxy_goes_to_its_path_with_exactly_its_keys(string action, string method, string path, string[] keys)
    {
        foreach (var missing in keys)
        {
            var (denied, deniedController) = Arrange(new[] { Read }.Concat(keys).Where(k => k != missing).Distinct().ToArray());
            Assert.Equal(403, Status(await Invoke(deniedController, action)));
            Assert.Empty(denied.Requests);
        }

        var (gateway, controller) = Arrange(keys);
        Assert.Equal(200, Status(await Invoke(controller, action)));
        var request = Assert.Single(gateway.Requests);
        Assert.Equal(method, request.Method);
        Assert.Equal(GatewayUrl + path, request.Uri.Split('?')[0]);
        Assert.Equal(TenantId.ToString(), request.Tenant);
    }

    [Fact]
    public async Task A6_insights_take_read_all_too_one_brd_set_only_and_a_tenant_id_body_is_refused()
    {
        var (g1, all) = Arrange(InsightsAll);
        Assert.Equal(200, Status(await all.Profile(Guid.Parse(Id), CancellationToken.None)));
        Assert.Single(g1.Requests);

        var (g2, reader) = Arrange(Read);
        Assert.Equal(404, Status(await reader.ReferenceValues("crm-visit-success", CancellationToken.None)));
        Assert.Empty(g2.Requests);
        Assert.Equal(200, Status(await reader.ReferenceValues("mdm-reimbursement-status", CancellationToken.None)));
        Assert.Contains("mdm-reimbursement-status", Assert.Single(g2.Requests).Uri);
        var (g3, nobody) = Arrange(Record);
        Assert.Equal(403, Status(await nobody.ReferenceValues("mdm-reimbursement-status", CancellationToken.None)));
        Assert.Empty(g3.Requests);

        var (g4, writer) = Arrange(new[] { Record, PlanManage, CompPropose }, "{\"tenantId\":\"x\"}");
        Assert.Equal(400, Status(await writer.Submit(CancellationToken.None)));
        Assert.Equal(400, Status(await writer.Draft(CancellationToken.None)));
        Assert.Equal(400, Status(await writer.CompetitorProposal(CancellationToken.None)));
        Assert.Empty(g4.Requests);
    }

    // ═══ 5 · seven languages, RTL, dark ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void A7_every_key_in_seven_languages_without_echo_the_dynamic_families_rtl_and_dark()
    {
        var keys = VisitReportIndex.ScriptKeys.Concat(VisitReportIndex.ViewKeys).ToArray();
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

        // every L.X the scripts read is a published key; the dynamic families are complete
        var scripts = Script("visit-report.js") + Script("report-core.js");
        foreach (Match m in Regex.Matches(scripts, @"\bL\.([A-Z][A-Za-z_]+)"))
        {
            Assert.Contains(m.Groups[1].Value, VisitReportIndex.ScriptKeys);
        }

        var families = new Dictionary<string, string[]>
        {
            ["Field_"] = ["visitKindCode", "metContactId", "adverseEvent", "adverseEventNote", "offLabelRequest", "totalMonthlyPatients", "section", "minutes",
                "visitSuccessCode", "commitmentCode", "interestCode", "adoptionStageCode", "shareBucketCode", "potential", "competitors", "objections",
                "objections_categoryCode", "objections_resolved", "requests", "requests_typeCode", "sample", "nextMessage", "potentialExceedsTotal",
                "totalBelowProducts", "amendReason"],
            ["Class_"] = ["low", "medium", "high"],
            ["Opp_"] = ["opportunity", "grow", "keep", "low_priority"],
            ["Resolved_"] = ["yes", "partly", "no"],
            ["Suggest_commitment_"] = ["will_try", "will_prescribe", "wants_info"],
            ["Role_"] = ["promo", "non_promo"],
            ["Day_"] = ["monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday"],
            ["Form_"] = ["tablet", "capsule", "syrup", "injection", "ointment", "cream", "drops", "inhaler", "patch", "other", "film_coated_tablet",
                "modified_release_tablet", "extended_release_tablet", "modified_release_capsule", "ampoule"],
            ["Err_"] = ["report_incomplete", "report_code_invalid", "report_v2_invalid", "reference_data_unavailable", "visit_report_concurrency_conflict",
                "visit_report_edit_window_closed", "visit_execution_not_finished", "potential_exceeds_total", "visit_report_amendment_reason_required"]
        };
        foreach (var (prefix, codes) in families)
        {
            foreach (var code in codes) { Assert.Contains(prefix + code, VisitReportIndex.ScriptKeys); }
        }

        // the core's missing fields all have words (a new rule without a label is caught here)
        foreach (Match m in Regex.Matches(Script("report-core.js"), @"need\([^;]*?'(?:general|safety|doctor|product|summary)'[^;]*?'([A-Za-z.]+)'\)"))
        {
            Assert.Contains("Field_" + m.Groups[1].Value.Replace('.', '_'), VisitReportIndex.ScriptKeys);
        }

        var tr = Resx("tr");
        Assert.Equal("Hekim ziyaret raporu", tr["TitleDoctor"]);
        Assert.Equal("Listede yok, rakip ekle", tr["NotInList"]);
        Assert.Equal("temsilci ekledi · onay bekliyor", tr["ProposalPending"]);
        Assert.Equal("Ziyarette bir yan etki / advers olay bildirildi mi?", tr["AdverseEventQ"]);
        Assert.Equal("تقرير الزيارة", Resx("ar")["PageTitle"]);
        foreach (var culture in new[] { "fr", "es", "zh", "ar", "ru" })
        {
            var resx = Resx(culture);
            var echoes = keys.Where(k => resx[k] == en[k] && Regex.Replace(en[k], @"\{\d\}|[\s·:–\-,.()/<>…]|min|s\b", string.Empty).Length > 0).ToList();
            Assert.True(echoes.Count <= 12, culture + ": too many English echoes: " + string.Join(", ", echoes));
        }

        var css = File.ReadAllText(Path.Combine(ScriptDir(), "visit-report.css"));
        Assert.Contains("[dir=\"rtl\"] .vr-flip", css);
        Assert.Contains("[data-bs-theme=\"dark\"] #vr-root", css);
        Assert.Contains("scroll-snap-type: x mandatory", css); // the phone's sideways sections
        Assert.DoesNotMatch(@"(?<![-\w])(margin|padding)-(left|right)\s*:", css);
        Assert.DoesNotMatch(@"(?<![-\w])(left|right)\s*:", css);
    }

    // ═══ 6 · the entries; 7 · the journey editor ═════════════════════════════════════════════════════════════

    [Fact]
    public void A8_the_workspace_and_the_presentation_open_the_wizard_and_v1_stays_for_reading()
    {
        var node = RunNode("const C = require('" + Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", "VisitWorkspace", "workspace-core.js").Replace("\\", "/")
                           + "'); process.stdout.write(JSON.stringify([C.reportUrl('/CRM/VisitReport', 'a b'), C.reportUrl(null, 'x')]));");
        if (node is not null)
        {
            Assert.Equal(new[] { "/CRM/VisitReport?plannedVisitId=a%20b", "/CRM/VisitReport?plannedVisitId=x" }, JsonSerializer.Deserialize<string[]>(node));
        }

        var ws = File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", "VisitWorkspace", "visit-workspace.js"));
        Assert.Contains("if (k === 'sendReport' && v.plannedVisitId) { return '<a class=\"btn btn-' + a.tone + ' flex-fill\" href=\"' + esc(C.reportUrl(reportBase, v.plannedVisitId))", ws);
        Assert.Contains("data-report-url=\"/CRM/VisitReport\"", File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "VisitWorkspace", "Index.cshtml")));
        var vp = File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", "VisitPresentation", "visit-presentation.js"));
        Assert.Contains("'plannedVisitId=' + encodeURIComponent(plannedVisitId)", vp);
        Assert.Contains("data-report-url=\"/CRM/VisitReport\"", File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "VisitPresentation", "Index.cshtml")));
        // the v1 screen stays (reading v1 reports; "result" of a visit without an execution)
        Assert.True(File.Exists(Path.Combine(WebRoot(), "Views", "CRM", "VisitExecution", "Index.cshtml")));
        Assert.Contains("if (k === 'result' || k === 'sendReport') { return '<a class=\"btn btn-' + a.tone + ' flex-fill\" href=\"' + esc(executionUrl)", ws);
    }

    [Fact]
    public void A9_the_journey_stage_editor_maps_an_adoption_stage_from_the_brd_set()
    {
        var form = File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "ContentEngagementJourneys", "_Form.cshtml"));
        Assert.Contains("id=\"stageAdoption\"", form);
        Assert.Contains("@Localizer[\"AdoptionStage\"]", form);
        var js = File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", "ContentEngagementJourneys", "form.js"));
        Assert.Contains("adoptionStageCode: document.getElementById('stageAdoption')?.value || null", js);
        Assert.Contains("`${endpoint}/adoption-stages`", js);
        Assert.Contains("adoption.innerHTML = adoptionHtml(stage ? stage.adoptionStageCode : '')", js);
        var controller = File.ReadAllText(Path.Combine(WebRoot(), "Controllers", "CRM", "ContentEngagementJourneysController.cs"));
        Assert.Contains("[HttpGet(\"api/adoption-stages\")]", controller);
        Assert.Contains("RequireJson(ReadPermission, ReadFallback)", controller);
        Assert.Equal("crm-adoption-stage", ContentEngagementJourneysController.AdoptionStageSet);
        var l10n = File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "ContentEngagementJourneys", "_IndexL10n.cshtml"));
        Assert.Contains("\"AdoptionStage\", \"AdoptionStageNone\", \"AdoptionStageUnavailable\"", l10n);
        foreach (var culture in new[] { "en", "tr", "fr", "es", "zh", "ar", "ru" })
        {
            var file = Path.Combine(WebRoot(), "Resources", "Views", "CRM", "ContentEngagementJourneys", "ContentEngagementJourneysIndex." + culture + ".resx");
            var values = XDocument.Load(file).Root!.Elements("data").ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")?.Value ?? string.Empty);
            foreach (var key in new[] { "AdoptionStage", "AdoptionStageHelp", "AdoptionStageNone", "AdoptionStageUnavailable" })
            {
                Assert.True(values.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) && v != key, culture + ":" + key);
            }
        }

        Assert.Equal("Kullanım aşaması", XDocument.Load(Path.Combine(WebRoot(), "Resources", "Views", "CRM", "ContentEngagementJourneys", "ContentEngagementJourneysIndex.tr.resx"))
            .Root!.Elements("data").First(d => (string)d.Attribute("name")! == "AdoptionStage").Element("value")!.Value);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────

    private static string[] Args(string value) => Regex.Matches(value, @"\{\d\}").Select(m => m.Value).Distinct().OrderBy(x => x).ToArray();

    private static VisitReportIndexViewModel Model(IActionResult result)
        => Assert.IsType<VisitReportIndexViewModel>(Assert.IsType<ViewResult>(result).Model);

    private static int? Status(IActionResult result) => result switch
    {
        ContentResult c => c.StatusCode,
        ObjectResult o => o.StatusCode ?? 200,
        StatusCodeResult s => s.StatusCode,
        _ => null
    };

    private static Task<IActionResult> Invoke(VisitReportController c, string action)
    {
        var id = Guid.Parse(Id);
        var ct = CancellationToken.None;
        return action switch
        {
            "Prefill" => c.Prefill(id, ct),
            "Report" => c.Report(id, ct),
            "Draft" => c.Draft(ct),
            "Submit" => c.Submit(ct),
            "Amend" => c.Amend(id, ct),
            "Visit" => c.Visit(id, ct),
            "Execution" => c.Execution(id, ct),
            "Presentation" => c.Presentation(id, ct),
            "Profile" => c.Profile(id, ct),
            "Potentials" => c.Potentials(id, ct),
            "IndicationProfiles" => c.IndicationProfiles(id, ct),
            "CompetitorLookup" => c.CompetitorLookup(ct),
            "CompetitorProposal" => c.CompetitorProposal(ct),
            "Gskus" => c.Gskus(ct),
            "Products" => c.Products(ct),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
    }

    private static (Gateway Gateway, VisitReportController Controller) Arrange(params string[] permissions) => Arrange(permissions, "{\"plannedVisitId\":\"" + Id + "\"}");

    private static (Gateway Gateway, VisitReportController Controller) Arrange(string[] permissions, string body)
    {
        var gateway = new Gateway();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = GatewayUrl }).Build();
        var controller = new VisitReportController(new HttpClient(gateway), configuration, NullLogger<VisitReportController>.Instance);
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        http.Request.ContentType = "application/json";
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return (gateway, controller);
    }

    private static JsonElement? Smoke(string scenario)
    {
        var smoke = Path.Combine(Path.GetDirectoryName(WebRoot())!, "Diten.Web.Tests", "Js", "visit-report-smoke.js").Replace("\\", "/");
        var crm = Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM").Replace("\\", "/");
        var node = RunNode("process.argv[2] = '" + crm + "'; process.argv[3] = '" + scenario + "'; require('" + smoke + "');");
        return node is null ? null : JsonDocument.Parse(node).RootElement.Clone();
    }

    private static string Core() => "const C = require('" + Path.Combine(ScriptDir(), "report-core.js").Replace("\\", "/") + "');\n";

    private static string? RunNode(string code)
    {
        var temp = Path.Combine(Path.GetTempPath(), "vr4-" + Guid.NewGuid().ToString("N") + ".js");
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

    private static string ScriptDir() => Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", "VisitReport");

    private static string Script(string file) => File.ReadAllText(Path.Combine(ScriptDir(), file)).Replace("\r\n", "\n");

    private static string View(string file) => File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "VisitReport", file)).Replace("\r\n", "\n");

    private static Dictionary<string, string> Resx(string culture)
    {
        var file = "VisitReportIndex" + (culture.Length == 0 ? "" : "." + culture) + ".resx";
        return XDocument.Load(Path.Combine(WebRoot(), "Resources", "Views", "CRM", "VisitReport", file)).Root!
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
        public List<(string Method, string Uri, string? Tenant)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request.Method.Method, request.RequestUri!.ToString(), request.Headers.TryGetValues("X-Tenant-Id", out var t) ? t.Single() : null));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":{}}", Encoding.UTF8, "application/json")
            });
        }
    }
}
