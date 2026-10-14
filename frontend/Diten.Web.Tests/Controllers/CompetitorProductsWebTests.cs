using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Controllers.MasterData;
using Diten.Web.Models.MasterData;
using Diten.Web.Views.MasterData.CompetitorProducts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VW-W4 (W4-WEB-c) — MDM competitor products on the REAL controller, views, resx and scripts (Acceptance):
/// (1) UAS-001, the pages, the menu page; (2) the pure rules in Node — the actions (status × manage × SoD), the country
/// duplicate, the price period (a new period of the same row), the merge steps (target, "I read it"), "&lt;5"; (3) the pages
/// in a fake DOM — the list, read-only, empty, the proposals (reject needs a reason), approve-and-complete → send, the
/// template missing (409 workflow_template_missing → a clear message), the proposal merge in 3 steps → send, the direct
/// merge, edit + 409 "Yenile", the preparer never decides (SoD) / the approver returns with a note, the details, create
/// (createError null); (4) every proxy → its path with exactly its keys; (5) seven languages, RTL, dark.
/// </summary>
public sealed class CompetitorProductsWebTests
{
    private const string GatewayUrl = "http://gateway.test";
    private const string Id = "11111111-2222-3333-4444-555555555555";
    private static readonly Guid TenantId = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");
    private static readonly string[] Cultures = ["", "en", "tr", "fr", "es", "zh", "ar", "ru"];

    private const string Read = "mdm.competitor-products.read";
    private const string Manage = "mdm.competitor-products.manage";
    private const string ProductRead = "mdm.global-products.read";
    private const string BrandRead = "mdm.brands.read";
    private const string InsightsRead = "crm.doctor-insights.read";
    private const string Thresholds = "crm.doctor-insights.thresholds.manage";
    private const string Usage = "crm.visit-report.read-aggregate";
    private const string People = "platform.meetings.create";

    // ═══ 1 · UAS-001, the pages, the menu ═════════════════════════════════════════════════════════════════════

    [Fact]
    public void A1_without_the_read_key_only_the_access_notice_every_page_and_the_menu_page()
    {
        var (_, none) = Arrange(Manage, ProductRead);
        Assert.False(Model(none.Index(null)).CanRead);
        var (_, reader) = Arrange(Read);
        var m = Model(reader.Index("proposals"));
        Assert.True(m.CanRead);
        Assert.False(m.CanManage || m.CanReadProducts || m.CanReadForms || m.CanReadIndications || m.CanReadUsage || m.CanReadPeople);
        Assert.Equal("proposals", m.Mode);
        Assert.Equal("records", Model(reader.Index(null)).Mode);
        var (_, full) = Arrange(Read, Manage, ProductRead, BrandRead, Thresholds, Usage, People);
        var f = Model(full.Index(null));
        Assert.True(f.CanManage && f.CanReadProducts && f.CanReadForms && f.CanReadIndications && f.CanReadUsage && f.CanReadPeople);
        Assert.Equal("u-1", f.Actor);

        Assert.Equal(("Edit", "create"), ViewAndMode(full.Create()));
        Assert.Equal(("Edit", "edit"), ViewAndMode(full.Edit(Guid.Parse(Id))));
        Assert.Equal(("Details", "details"), ViewAndMode(full.Details(Guid.Parse(Id))));
        Assert.Equal(("Merge", "merge"), ViewAndMode(full.Merge(Guid.Parse(Id))));
        Assert.Equal(("Proposal", "proposal"), ViewAndMode(full.Proposal(Guid.Parse(Id))));
        Assert.Equal(("Edit", "complete"), ViewAndMode(full.Complete(Guid.Parse(Id))));
        Assert.Equal(("Merge", "proposal-merge"), ViewAndMode(full.ProposalMerge(Guid.Parse(Id))));
        Assert.Equal(Guid.Parse(Id), Model(full.Proposal(Guid.Parse(Id))).ProposalId);

        foreach (var view in new[] { "Index", "Edit", "Details", "Merge", "Proposal" })
        {
            var text = View(view + ".cshtml");
            var denied = text.IndexOf("<partial name=\"_AccessDenied\"", StringComparison.Ordinal);
            var bail = text.IndexOf("return;", denied, StringComparison.Ordinal);
            Assert.True(denied > 0 && bail > denied && text.IndexOf("_Shell.cshtml", StringComparison.Ordinal) > bail, view);
            Assert.Contains("Context.Items[\"NavActivePath\"] = \"/MasterDataManagement/CompetitorProducts\";", text);
            Assert.DoesNotContain("cdn.", text);
        }

        // the Work Center deep link (BE-b2) is a page of this controller; the menu page is the BE-b2 manifest's
        var core = File.ReadAllText(Path.Combine(RepoRoot(), "services", "Diten.MdmService", "src", "Diten.MdmService.Application", "Features", "Approvals", "WorkflowApprovalCore.cs"));
        Assert.Contains("/MasterDataManagement/CompetitorProducts/Proposals/{id:D}", core);
        var manifest = File.ReadAllText(Path.Combine(RepoRoot(), "services", "Diten.MdmService", "src", "Diten.MdmService.Api", "ModuleRegistration", "ProductItemSkuMasterManifestProvider.cs"));
        Assert.Contains("RoutePath: \"/MasterDataManagement/CompetitorProducts\"", manifest);
        Assert.Contains("[Route(\"MasterDataManagement/CompetitorProducts\")]", File.ReadAllText(Path.Combine(WebRoot(), "Controllers", "MasterData", "CompetitorProductsController.cs")));
        Assert.DoesNotContain("Pazar payı", Script("competitor-pages.js")); // no market share / sales (W7 placeholder only)
    }

    // ═══ 2 · the pure rules (Node) ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void A2_actions_sod_country_duplicate_price_period_merge_steps_and_small_cells()
    {
        var node = RunNode(Core() + """
            const out = {};
            const M = { manage: true }, R = { manage: false };
            out.actions = ['active', 'inactive', 'merged', 'rejected', 'proposed'].map(s => C.recordActions({ status: s }, M).join('|'));
            out.readActions = C.recordActions({ status: 'active' }, R).join('|');
            const ab = (x) => Object.assign({ canEdit: false, canSubmit: false, canWithdraw: false, canDecide: false }, x);
            const dec = (status, extra, abilities) => Object.assign({ status, createdBy: 'me', updatedBy: 'me', submittedBy: status === 'in_review' ? 'me' : null,
              reviewRounds: status === 'in_review' ? [{ roundNo: 1, submittedBy: 'me', outcome: null, closedAt: null }] : [], decisions: [], abilities: ab(abilities) }, extra);
            out.dec = {
              none: C.decisionActions(null, M, 'me').join('|'),
              noneRead: C.decisionActions(null, R, 'me').join('|'),
              draft: C.decisionActions(dec('draft', {}, { canEdit: true, canSubmit: true }), M, 'me').join('|'),
              reviewOwn: C.decisionActions(dec('in_review', {}, { canWithdraw: true, canDecide: true }), M, 'me').join('|'),
              reviewOther: C.decisionActions(dec('in_review', {}, { canDecide: true }), M, 'other').join('|'),
              reviewOtherEditor: C.decisionActions(dec('in_review', { updatedBy: 'other' }, { canDecide: true }), M, 'OTHER ').join('|'),
              reviewNoAbility: C.decisionActions(dec('in_review', {}, {}), M, 'other').join('|'),
              applied: C.decisionActions(dec('applied', {}, { canDecide: true }), M, 'other').join('|'),
              returned: C.decisionActions(dec('returned', {}, { canSubmit: true }), M, 'me').join('|')
            };
            out.sod = [C.sodNote(dec('in_review'), M, 'me'), C.sodNote(dec('in_review'), M, 'other'), C.sodNote(dec('draft'), M, 'me')];
            out.states = [
              C.decisionState(null).key,
              C.decisionState(dec('draft', { action: 'merge' })).key + ':' + C.decisionState(dec('draft', { action: 'merge' })).action,
              C.decisionState(dec('in_review', { submittedAt: 'T1' })).by + '@' + C.decisionState(dec('in_review', { submittedAt: 'T1' })).at,
              JSON.stringify(C.decisionState(dec('returned', { reviewRounds: [{ roundNo: 1, outcome: 'rejected', closedAt: 'T2', completedBy: 'x' }], decisions: [{ roundNo: 1, outcome: 'return', comment: 'ATC eksik', by: 'x', at: 'T2' }] }))),
              C.decisionState(dec('returned', { reviewRounds: [{ roundNo: 2, outcome: 'approved', closedAt: 'T3', reasonCode: 'competitor_proposal_sod' }] })).reason,
              C.decisionState(dec('returned', { reviewRounds: [{ roundNo: 2, outcome: 'cancelled', closedAt: 'T3' }] })).reason,
              C.decisionState(dec('applied', { appliedBy: 'x', appliedAt: 'T4' })).key
            ];
            out.reject = [C.rejectReason('').error, C.rejectReason('  ').error, C.rejectReason('x'.repeat(501)).error, C.rejectReason(' neden ').value];
            out.review = [C.reviewBody('return', ' ').error, C.reviewBody('return', 'not').ok, C.reviewBody('approve', null).ok, C.reviewBody('maybe').error];
            const rows = [{ marketCode: 'TR' }];
            out.market = [C.canAddMarket(rows, 'tr'), C.canAddMarket(rows, 'AZ'), C.canAddMarket(rows, 'AZE'), C.canAddMarket(rows, '')];
            const added = C.addMarket(rows, 'ae', 'aed', '2026-10-15');
            out.added = added && added[1];
            out.addedDup = C.addMarket(rows, 'TR', 'TRY');
            const orig = { boxPrice: 172.4, currencyCode: 'TRY', reimbursementStatusCode: 'reimbursed', validFrom: '2026-01-01' };
            out.period = [
              C.pricePeriod(orig, Object.assign({}, orig)),
              C.pricePeriod(orig, Object.assign({}, orig, { boxPrice: '186,30' })),
              C.pricePeriod(orig, Object.assign({}, orig, { boxPrice: '186,30', validFrom: '2026-07-01' })),
              C.pricePeriod(orig, Object.assign({}, orig, { reimbursementStatusCode: 'not_reimbursed', validFrom: '2026-07-01' })),
              C.pricePeriod(null, orig)
            ];
            out.validate = [C.validate({}).errors, C.validate({ name: 'A', company: 'B', activeIngredients: ['x'], competesWithGlobalProductIds: ['p'],
              countryPrices: [{ marketCode: 'TR', boxPrice: 1, currencyCode: 'TRY' }, { marketCode: 'tr', boxPrice: 2, currencyCode: 'TRY' }] }).errors.markets,
              C.validate({ name: 'A', company: 'B', activeIngredients: ['x'], competesWithGlobalProductIds: ['p'], countryPrices: [{ marketCode: 'TR', boxPrice: null, currencyCode: 'TRY' }] }).errors.markets,
              C.validate({ name: 'A', company: 'B', activeIngredients: ['x'], competesWithGlobalProductIds: ['p'], countryPrices: [{ marketCode: 'TR', boxPrice: 0, currencyCode: 'TR' }] }).errors.markets,
              C.validate({ name: 'A', company: 'B', activeIngredients: ['x'], competesWithGlobalProductIds: ['p'], countryPrices: [{ marketCode: 'TR', boxPrice: 0, currencyCode: 'try' }] }).ok];
            const wb = C.writeBody({ name: ' A ', company: 'B', activeIngredients: ['x'], competesWithGlobalProductIds: ['p'], countryPrices: [{ marketCode: 'tr', boxPrice: '186,30', currencyCode: 'try', validFrom: '2026-07-01T00:00:00Z' }] }, 3);
            out.writeBody = [wb.name, wb.countryPrices[0].marketCode, wb.countryPrices[0].boxPrice, wb.countryPrices[0].currencyCode, wb.countryPrices[0].validFrom, wb.expectedVersion, 'tenantId' in wb];
            out.merge = [C.mergeCanAdvance(1, {}), C.mergeCanAdvance(1, { targetId: 't' }), C.mergeCanAdvance(2, { targetId: 't' }), C.mergeSubmitEnabled({ targetId: 't' }),
              C.mergeSubmitEnabled({ targetId: 't', ack: true }), C.mergeSubmitEnabled({ ack: true }), C.mergeBody({ targetId: 't' }).error, C.mergeBody({ ack: true }).error,
              JSON.stringify(C.mergeBody({ targetId: 't', ack: true, moveMarketCodes: ['az'] }, 5).body)];
            out.decisions = [C.decisionBody('merge', { targetId: 't' }).error, C.decisionBody('merge', { ack: true }).error, C.decisionBody('merge', { targetId: 't', ack: true, moveMarketCodes: ['az'] }, 2).body.moveMarketCodes[0],
              C.decisionBody('reject', { reason: '' }).error, C.decisionBody('reject', { reason: 'r' }).body.reason, C.decisionBody('complete', { form: {} }).error, C.decisionBody('x').error];
            out.diff = C.mergeDiffRows({ name: 'Tamsulin', company: 'Atlas', activeIngredients: ['tamsulosin'] }, { name: 'tamsulin', company: 'Atlas Generik', activeIngredients: ['tamsulosin'] }).filter(d => d.diff).map(d => d.field);
            out.markets = C.mergeMarketRows({ countryPrices: [{ marketCode: 'TR' }, { marketCode: 'AZ' }] }, { countryPrices: [{ marketCode: 'TR' }] }).map(r => r.code + ':' + r.movable);
            out.counts = [C.countText('<5'), C.countText('12'), C.countText(0), C.countText(null)];
            out.usage = C.usageOf({ items: [{ competitorProductId: 'a', reportCount: '<5' }] }, 'a').reportCount;
            out.tones = [C.similarityTone(80), C.similarityTone(79)];
            out.tags = [C.tags('a, b; A\nc').join('|'), C.addTag(['x'], ' X ').join('|')];
            out.err = [C.errorText(['workflow_template_missing', 'msg'], { Err_workflow_template_missing: 'Şablon yok', ActionFailed: 'Olmadı' }), C.errorText(['zzz'], { ActionFailed: 'Olmadı' })];
            process.stdout.write(JSON.stringify(out));
            """);
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement;
        string[] S(string name) => r.GetProperty(name).EnumerateArray().Select(x => x.ToString()).ToArray();
        string D(string k) => r.GetProperty("dec").GetProperty(k).GetString()!;

        Assert.Equal(new[] { "view|edit|deactivate|merge", "view|edit", "view", "view", "view" }, S("actions"));
        Assert.Equal("view", r.GetProperty("readActions").GetString());
        Assert.Equal("prepare", D("none"));
        Assert.Equal("", D("noneRead"));
        Assert.Equal("prepare|submit", D("draft"));
        Assert.Equal("withdraw", D("reviewOwn"));           // SoD: the preparer withdraws, never approves — even with a stale ability
        Assert.Equal("approve|return", D("reviewOther"));
        Assert.Equal("", D("reviewOtherEditor"));          // the last editor (written differently) is "own" too
        Assert.Equal("", D("reviewNoAbility"));
        Assert.Equal("", D("applied"));
        Assert.Equal("prepare|submit", D("returned"));
        Assert.Equal(new[] { "True", "False", "False" }, S("sod"));
        var states = S("states");
        Assert.Equal("waiting", states[0]);
        Assert.Equal("draft:merge", states[1]);
        Assert.Equal("me@T1", states[2]);
        Assert.Contains("\"note\":\"ATC eksik\"", states[3]);
        Assert.Contains("\"reason\":\"returned\"", states[3]);
        Assert.Equal("sod", states[4]);
        Assert.Equal("withdrawn", states[5]);
        Assert.Equal("applied", states[6]);
        Assert.Equal(new[] { "required", "required", "too_long", "neden" }, S("reject"));
        Assert.Equal(new[] { "comment_required", "True", "True", "decision_invalid" }, S("review"));

        Assert.Equal(new[] { "False", "True", "False", "False" }, S("market")); // a country once; an ISO 2-letter code
        Assert.Equal("AE", r.GetProperty("added").GetProperty("marketCode").GetString());
        Assert.Equal("AED", r.GetProperty("added").GetProperty("currencyCode").GetString());
        Assert.Equal(JsonValueKind.Null, r.GetProperty("addedDup").ValueKind);
        var period = r.GetProperty("period").EnumerateArray().ToArray();
        Assert.Equal(JsonValueKind.Null, period[0].ValueKind);                                  // nothing priced changed
        Assert.Equal(172.4m, period[1].GetProperty("from").GetDecimal());
        Assert.Equal(186.3m, period[1].GetProperty("to").GetDecimal());
        Assert.True(period[1].GetProperty("dateMustMove").GetBoolean());                        // same row, new period: the date must move
        Assert.False(period[2].GetProperty("dateMustMove").GetBoolean());
        Assert.False(period[3].GetProperty("dateMustMove").GetBoolean());                       // a reimbursement change is a period too
        Assert.Equal(JsonValueKind.Null, period[4].ValueKind);                                  // a new row is not a period
        var validate = r.GetProperty("validate").EnumerateArray().ToArray();
        foreach (var k in new[] { "name", "company", "ingredients", "products" }) { Assert.Equal("required", validate[0].GetProperty(k).GetString()); }
        Assert.Equal(new[] { "duplicate", "price", "currency", "True" }, validate.Skip(1).Select(x => x.ToString()).ToArray());
        Assert.Equal(new[] { "A", "TR", "186.3", "TRY", "2026-07-01", "3", "False" }, S("writeBody"));
        Assert.Equal(new[] { "False", "True", "True", "False", "True", "False", "ack_required", "target_required", "{\"targetId\":\"t\",\"moveMarketCodes\":[\"AZ\"],\"confirm\":true,\"expectedVersion\":5}" }, S("merge"));
        Assert.Equal(new[] { "ack_required", "target_required", "AZ", "reason_required", "r", "invalid", "decision_invalid" }, S("decisions"));
        Assert.Equal(new[] { "company" }, S("diff"));
        Assert.Equal(new[] { "TR:false", "AZ:true" }, S("markets"));
        Assert.Equal(new[] { "<5", "12", "0", "0" }, S("counts"));                             // "<5" is never turned into a number
        Assert.Equal("<5", r.GetProperty("usage").GetString());
        Assert.Equal(new[] { "likely", "other" }, S("tones"));
        Assert.Equal(new[] { "a|b|c", "x" }, S("tags"));
        Assert.Equal(new[] { "Şablon yok", "Olmadı" }, S("err"));
    }

    // ═══ 3 · the pages in a fake DOM ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void A3_the_list_read_only_and_empty()
    {
        if (Smoke("list") is not { } r) { return; }
        var page = r.GetProperty("page").GetString()!;
        Assert.Contains("Koronex 100", page);
        Assert.Contains("Koroneks", page);                                                   // the other names under the name
        Assert.Contains("TUTUKON", page);                                                    // our product chip
        Assert.Contains("1/1 fiyatlı", page);
        Assert.Contains("<td class=\"cp-strong\">61</td>", page);                             // the CRM usage (G2)
        Assert.Contains("<td class=\"cp-strong\">&lt;5</td>", page);                          // "<5" as it comes
        Assert.Contains("değiştirilmiş salımlı tablet", page);                                 // G4 form label
        Assert.Contains("Yeni rakip ürün", page);
        Assert.Contains("Temsilci önerileri<span class=\"cp-count\">4</span>", page);         // pending proposals (every one not applied)
        Assert.Contains("/api/records?page=1&pageSize=25&productId=aaaaaaaa-0000-0000-0000-00000000000a", r.GetProperty("filterCall").GetString());
        Assert.Contains("GET /api/usage?months=3&competitorIds=rrrrrrrr-0000-0000-0000-000000000001%2Crrrrrrrr-0000-0000-0000-000000000002", r.GetProperty("calls").EnumerateArray().Select(c => c.GetString()));

        if (Smoke("readonly") is { } ro)
        {
            var p = ro.GetProperty("page").GetString()!;
            Assert.Contains("Salt okuma", p);
            Assert.DoesNotContain("Yeni rakip ürün", p);
            Assert.DoesNotContain("Temsilci önerileri", p);
            Assert.DoesNotContain("/Edit\"", p);
            Assert.DoesNotContain("GET /api/proposals", ro.GetProperty("calls").EnumerateArray().Select(c => c.GetString()));
        }

        if (Smoke("empty") is { } e)
        {
            Assert.Contains("Henüz rakip ürün yok", e.GetProperty("page").GetString());
        }
    }

    [Fact]
    public void A4_proposals_reject_needs_a_reason_complete_sends_and_a_missing_template_is_said_clearly()
    {
        if (Smoke("proposals") is not { } p) { return; }
        var page = p.GetProperty("page").GetString()!;
        foreach (var s in new[] { "data-state=\"waiting\"", "data-state=\"draft\"", "data-state=\"returned\"", "data-state=\"in_review\"", "Karar bekliyor", "Taslak karar · Birleştir",
                     "Taslağa geri gönderildi · ATC eksik", "Onay bekliyor · Elif Kaya" })
        {
            Assert.Contains(s, page);
        }

        Assert.Contains("%100", page);                                // the similarity from the server (G5)
        Assert.Contains("cp-sim likely", page);                       // ≥ 80
        Assert.Contains("Murat Demir", page);                         // the proposer's name
        Assert.Equal(0, p.GetProperty("putsWithoutReason").GetInt32());
        Assert.Contains("Gerekçe zorunlu.", p.GetProperty("modalAfterEmpty").GetString());
        var reject = p.GetProperty("rejectBody");
        Assert.Equal("reject", reject.GetProperty("action").GetString());
        Assert.Equal("Türkiye pazarında bulunmuyor", reject.GetProperty("reason").GetString());
        Assert.Single(p.GetProperty("submits").EnumerateArray());     // "Onaya gönder" = save + submit
        Assert.Equal("/cp/Proposals/pppppppp-0000-0000-0000-000000000001", p.GetProperty("location").GetString());

        if (Smoke("complete") is { } c)
        {
            Assert.Equal(0, c.GetProperty("putsInvalid").GetInt32());                            // a required field missing → no PUT
            Assert.Contains("cp-banner cp-danger", c.GetProperty("afterInvalid").GetString());     // "not saved, fill the marked fields"
            var body = c.GetProperty("completeBody");
            Assert.Equal("complete", body.GetProperty("action").GetString());
            Assert.Equal("Prostavit", body.GetProperty("complete").GetProperty("name").GetString()); // filled from the proposal
            Assert.Equal("tamsulosin", body.GetProperty("complete").GetProperty("activeIngredients")[0].GetString());
            Assert.Equal(1, c.GetProperty("submits").GetInt32());
            Assert.False(c.GetProperty("alertShown").GetBoolean());
            Assert.Contains("cp-banner cp-info", c.GetProperty("page").GetString());              // the proposal banner (R4)
        }

        if (Smoke("template") is { } t)
        {
            Assert.Equal(1, t.GetProperty("submits").GetInt32());
            Assert.True(t.GetProperty("alertShown").GetBoolean());
            Assert.Equal("Onay akışı (MDM-COMP-PROPOSAL) henüz kurulmamış.", t.GetProperty("alert").GetString()); // 409 workflow_template_missing
            Assert.Equal("", t.GetProperty("location").GetString());                                            // stays on the page
        }
    }

    [Fact]
    public void A5_merge_in_three_steps_needs_a_target_and_the_acknowledgement()
    {
        if (Smoke("pmerge") is not { } p) { return; }
        Assert.Contains("%100", p.GetProperty("step1").GetString());
        Assert.True(p.GetProperty("stepAfterNoTarget").GetBoolean());          // no target → stays on step 1
        var step2 = p.GetProperty("step2").GetString()!;
        Assert.Contains("farklı", step2);                                      // the company differs
        Assert.Contains("data-act-change=\"move\" data-code=\"AZ\"", step2);   // AZ only on the proposal → movable
        var step3 = p.GetProperty("step3").GetString()!;
        Assert.Contains("data-act=\"decision-send\" data-kind=\"merge\" disabled", step3);
        Assert.Equal(0, p.GetProperty("callsBeforeAck").GetInt32());
        Assert.DoesNotContain("data-kind=\"merge\" disabled", p.GetProperty("step3Ack").GetString());
        var body = p.GetProperty("mergeBody");
        Assert.Equal("merge", body.GetProperty("action").GetString());
        Assert.Equal("rrrrrrrr-0000-0000-0000-000000000002", body.GetProperty("targetId").GetString());
        Assert.Equal("AZ", body.GetProperty("moveMarketCodes")[0].GetString());
        Assert.Equal(1, p.GetProperty("submits").GetInt32());

        if (Smoke("merge") is { } m)
        {
            Assert.Contains("data-act=\"merge-do\" disabled", m.GetProperty("step3").GetString());
            Assert.Equal(0, m.GetProperty("callsBeforeAck").GetInt32());
            var direct = m.GetProperty("mergeBody");
            Assert.True(direct.GetProperty("confirm").GetBoolean());
            Assert.Equal(3, direct.GetProperty("expectedVersion").GetInt32());
            Assert.Equal(0, m.GetProperty("submits").GetInt32());                 // a direct merge needs no approval
            Assert.Equal("/cp/rrrrrrrr-0000-0000-0000-000000000001", m.GetProperty("location").GetString());
        }
    }

    [Fact]
    public void A6_edit_opens_a_new_period_refuses_a_country_twice_and_a_409_offers_refresh()
    {
        if (Smoke("edit") is not { } e) { return; }
        Assert.Contains("Geçmiş (1)", e.GetProperty("form").GetString());
        Assert.Contains("Yeni dönem açılacak: 186,30 → 199,90 TRY.", e.GetProperty("period").GetString());
        Assert.True(e.GetProperty("dupError").GetBoolean());
        Assert.Equal(2, e.GetProperty("rowsAfterAdd").GetInt32());
        var body = e.GetProperty("putBody");
        Assert.Equal(3, body.GetProperty("expectedVersion").GetInt32());
        Assert.Equal(199.9m, body.GetProperty("countryPrices")[0].GetProperty("boxPrice").GetDecimal()); // the same TR row, a new price
        Assert.Equal("AZN", body.GetProperty("countryPrices")[1].GetProperty("currencyCode").GetString());
        Assert.Equal("/cp/rrrrrrrr-0000-0000-0000-000000000002", e.GetProperty("location").GetString());

        if (Smoke("conflict") is { } c)
        {
            Assert.Contains("Kayıt başkası tarafından değiştirildi, yenileyin.", c.GetProperty("afterSave").GetString());
            Assert.Contains("data-act=\"refresh\"", c.GetProperty("afterSave").GetString());
            Assert.Equal("", c.GetProperty("location").GetString());
            Assert.Equal(new[] { "GET /api/records/rrrrrrrr-0000-0000-0000-000000000002" }, c.GetProperty("refreshCalls").EnumerateArray().Select(x => x.GetString()));
            Assert.DoesNotContain("data-act=\"refresh\"", c.GetProperty("afterRefresh").GetString());
        }
    }

    [Fact]
    public void A7_the_preparer_never_decides_and_the_approver_returns_only_with_a_note()
    {
        if (Smoke("preparer") is not { } p) { return; }
        var page = p.GetProperty("page").GetString()!;
        Assert.Contains("Bu kararı siz hazırladınız", page);
        Assert.Contains("data-act=\"withdraw\"", page);
        Assert.DoesNotContain("data-act=\"approve\"", page);
        Assert.DoesNotContain("data-act=\"return-open\"", page);
        Assert.Equal(0, p.GetProperty("approveCalls").GetInt32());
        Assert.Equal(1, p.GetProperty("withdraws").GetInt32());

        if (Smoke("approver") is { } a)
        {
            var ap = a.GetProperty("page").GetString()!;
            Assert.Contains("data-act=\"approve\"", ap);
            Assert.Contains("data-act=\"return-open\"", ap);
            Assert.DoesNotContain("Bu kararı siz hazırladınız", ap);
            Assert.Contains("Türkiye pazarında yok", ap);                              // the decision's summary (the reason)
            Assert.Equal(0, a.GetProperty("returnWithoutNote").GetInt32());
            Assert.Contains("Not zorunlu.", a.GetProperty("modalError").GetString());
            Assert.Equal("return", a.GetProperty("returnBody").GetProperty("decision").GetString());
            Assert.Equal("Gerekçe belirsiz", a.GetProperty("returnBody").GetProperty("comment").GetString());
        }
    }

    [Fact]
    public void A8_details_and_create()
    {
        if (Smoke("details") is not { } d) { return; }
        var page = d.GetProperty("page").GetString()!;
        Assert.Contains("Pazar verisi · yakında", page);
        Assert.Contains("Doktor kimliği gösterilmez", page);
        Assert.Contains("Ülke fiyatı · TR", page);                                    // the change log's country row
        Assert.Contains("Zeynep Koç", page);
        Assert.Contains("172,40", d.GetProperty("withHistory").GetString());          // the previous period
        Assert.Contains("Pasife al", d.GetProperty("modal").GetString());
        Assert.Equal(3, d.GetProperty("deactivate").GetProperty("expectedVersion").GetInt32());

        if (Smoke("create") is { } c)
        {
            Assert.True(c.GetProperty("indications").GetBoolean());                    // G3: from the CRM for the chosen product
            Assert.Contains("cp-repcard", c.GetProperty("preview").GetString());       // the rep card preview
            Assert.Contains("42,00 AED", c.GetProperty("preview").GetString());
            var body = c.GetProperty("postBody");
            Assert.Equal("Valsen Plus", body.GetProperty("name").GetString());
            Assert.Equal("iiiiiiii-0000-0000-0000-000000000001", body.GetProperty("indicationNodeIds")[0].GetString());
            Assert.Equal("AE", body.GetProperty("countryPrices")[0].GetProperty("marketCode").GetString());
            Assert.Equal(JsonValueKind.Null, c.GetProperty("createError").ValueKind);
            Assert.Equal("/cp/nnnnnnnn-0000-0000-0000-000000000001", c.GetProperty("location").GetString());
        }
    }

    // ═══ 3b · W4-FIX-a — the local E4 findings F-1 / F-2 / F-3 / F-7 ═════════════════════════════════════════

    [Fact]
    public void FixA_F1_a_typed_field_never_redraws_the_form_and_a_redraw_keeps_the_focus()
    {
        if (Smoke("focus") is not { } f) { return; }
        // name → company → strength, each left by click / Tab (its change fires): the form markup is never rewritten
        Assert.Equal(f.GetProperty("writesStart").GetInt32(), f.GetProperty("writesAfterIdentity").GetInt32());
        Assert.Equal(0, f.GetProperty("writesAfterRow").GetInt32());                    // price / currency / date / source of a country row
        // …while the parts that show the values do change in place
        Assert.Contains("<strong>Valsen Plus</strong>", f.GetProperty("previewSlot").GetString());
        Assert.Contains("Selen İlaç", f.GetProperty("previewSlot").GetString());
        Assert.Contains("5/160 mg", f.GetProperty("previewSlot").GetString());
        Assert.Equal("11/200", f.GetProperty("nameCount").GetString());
        Assert.Equal("AZN", f.GetProperty("currencyShown").GetString());                 // upper-cased in the field itself
        Assert.Equal("AZN", f.GetProperty("curSlot").GetString());
        Assert.Contains("9,80 AZN", f.GetProperty("previewAfterRow").GetString());       // a typed "9,80" previews as a price, not NaN
        // a select may re-draw, but its own focus comes back; so does a tag input's after Enter
        Assert.True(f.GetProperty("selectRedrew").GetBoolean());
        Assert.Equal("[data-form=\"formCode\"]", f.GetProperty("selectFocus")[0].GetString());
        Assert.Equal("[data-row=\"0\"][data-k=\"reimbursementStatusCode\"]", f.GetProperty("rowSelectFocus")[0].GetString());
        Assert.Equal("[data-tag=\"activeIngredients\"]", f.GetProperty("tagFocus")[0].GetString());
        // no value is lost — neither in a later full re-draw nor in what is saved
        var form = f.GetProperty("formAfterRedraw").GetString()!;
        foreach (var v in new[] { "value=\"Valsen Plus\"", "value=\"Selen İlaç\"", "value=\"5/160 mg\"", "value=\"9,80\"", "value=\"AZN\"", "value=\"2026-10-01\"", "value=\"SGK\"" })
        {
            Assert.Contains(v, form);
        }
        var body = f.GetProperty("postBody");
        Assert.Equal("Valsen Plus", body.GetProperty("name").GetString());
        Assert.Equal("Selen İlaç", body.GetProperty("company").GetString());
        Assert.Equal("5/160 mg", body.GetProperty("strength").GetString());
        Assert.Equal("tablet", body.GetProperty("formCode").GetString());
        var row = body.GetProperty("countryPrices")[0];
        Assert.Equal(9.8m, row.GetProperty("boxPrice").GetDecimal());
        Assert.Equal("AZN", row.GetProperty("currencyCode").GetString());
        Assert.Equal("2026-10-01", row.GetProperty("validFrom").GetString());
        Assert.Equal("reimbursed", row.GetProperty("reimbursementStatusCode").GetString());

        // the code: the change listener re-draws only for a select
        var pages = Script("competitor-pages.js");
        Assert.Contains("const redraw = t.tagName === 'SELECT';", pages);
        Assert.Contains("restoreFocus(sel, caret);", pages);
    }

    [Fact]
    public void FixA_F2_every_one_of_177_products_is_findable_and_chosen_as_a_chip_also_when_completing()
    {
        var node = RunNode(Core() + """
            const products = [];
            for (let i = 0; i < 177; i++) products.push({ id: 'p' + i, canonicalCode: 'GP' + i, globalProductName: 'Ürün ' + i });
            products[150] = { id: 'tutu', canonicalCode: 'TUTU', globalProductName: 'TUTUKON' };
            const out = {
              all: C.productMatches(products, '', [], 30),
              tutu: C.productMatches(products, 'tutu', [], 30).items.map(p => p.id),
              byCode: C.productMatches(products, 'TUTU', [], 30).items.map(p => p.id),
              dotted: C.productMatches([{ id: 'x', globalProductName: 'İNDAPAMİD' }], 'indapamid', [], 30).items.length,
              chosenHidden: C.productMatches(products, 'tutu', ['tutu'], 30).items.length,
              everyOne: products.every(p => C.productMatches(products, p.globalProductName, [], 30).items.some(x => x.id === p.id))
            };
            out.all = { shown: out.all.items.length, more: out.all.more };
            process.stdout.write(JSON.stringify(out));
            """);
        if (node is not null)
        {
            var r = JsonDocument.Parse(node).RootElement;
            Assert.Equal(30, r.GetProperty("all").GetProperty("shown").GetInt32());
            Assert.Equal(147, r.GetProperty("all").GetProperty("more").GetInt32());
            Assert.Equal("tutu", Assert.Single(r.GetProperty("tutu").EnumerateArray()).GetString());
            Assert.Equal("tutu", Assert.Single(r.GetProperty("byCode").EnumerateArray()).GetString());
            Assert.Equal(1, r.GetProperty("dotted").GetInt32());
            Assert.Equal(0, r.GetProperty("chosenHidden").GetInt32());
            Assert.True(r.GetProperty("everyOne").GetBoolean());
        }

        if (Smoke("picker") is not { } p) { return; }
        Assert.Equal(new[] { "/api/products?pageSize=100&pageNumber=1", "/api/products?pageSize=100&pageNumber=2" },
            p.GetProperty("productCalls").EnumerateArray().Select(x => x.GetString()));   // every page, not the first 100
        Assert.Contains("147 ürün daha — aramayı daraltın", p.GetProperty("firstList").GetString());
        Assert.Contains("data-local=\"product-q\"", p.GetProperty("firstList").GetString());
        Assert.Equal(0, p.GetProperty("writesWhileTyping").GetInt32());                  // the search box keeps its focus
        Assert.Contains("TUTUKON", p.GetProperty("results").GetString());
        Assert.Contains("Aramayla eşleşen ürün yok.", p.GetProperty("noMatch").GetString());
        Assert.Contains("cp-chosen\"><span class=\"cp-tag p\">TUTUKON", p.GetProperty("afterPick").GetString());
        Assert.Equal("[data-local=\"product-q\"]", p.GetProperty("pickFocus")[0].GetString());
        Assert.Equal("aaaaaaaa-0000-0000-0000-00000000000a", p.GetProperty("postBody").GetProperty("competesWithGlobalProductIds")[0].GetString());

        if (Smoke("complete") is { } c)
        {
            Assert.Contains("data-local=\"product-q\"", c.GetProperty("page").GetString());   // the same picker when completing a proposal
        }
    }

    [Fact]
    public void FixA_F3_reference_options_read_the_real_web_contract_code_label_and_attributes()
    {
        // the Platform BusinessReferenceDataPublishedValuesModel as CrmReferenceSetReader passes it through (camelCase)
        const string real = """
            { "setCode": "mdm-reimbursement-status", "versionNumber": 1, "publishedAt": "2026-10-01T00:00:00Z", "items": [
              { "code": "unknown", "label": "Unknown", "description": null, "isActive": true, "sortOrder": 9, "attributes": { "label_tr": "Bilinmiyor" } },
              { "code": "reimbursed", "label": "Reimbursed", "description": null, "isActive": true, "sortOrder": 1, "attributes": { "label_tr": "Geri ödemede", "label_ar": "مُعوَّض" } },
              { "code": "not_reimbursed", "label": "Not reimbursed", "description": null, "isActive": false, "sortOrder": 2, "attributes": null },
              { "code": "reimbursed_conditional", "label": "Conditional", "description": null, "isActive": true, "sortOrder": 3, "attributes": null } ] }
            """;
        var node = RunNode(Core() + "const real = " + real + ";\n" + """
            process.stdout.write(JSON.stringify({ tr: C.referenceOptions(real, 'tr'), en: C.referenceOptions(real, 'en'), ar: C.referenceOptions(real, 'ar'),
              old: C.referenceOptions({ items: [{ valueCode: 'reimbursed', displayName: 'Reimbursed' }] }, 'tr') }));
            """);
        if (node is not null)
        {
            var r = JsonDocument.Parse(node).RootElement;
            string Pairs(string lang) => string.Join("|", r.GetProperty(lang).EnumerateArray().Select(o => o.GetProperty("code").GetString() + "=" + o.GetProperty("label").GetString()));
            Assert.Equal("reimbursed=Geri ödemede|reimbursed_conditional=Conditional|unknown=Bilinmiyor", Pairs("tr")); // sortOrder, inactive out, label fallback
            Assert.Equal("reimbursed=Reimbursed|reimbursed_conditional=Conditional|unknown=Unknown", Pairs("en"));        // English = label
            Assert.Equal("reimbursed=مُعوَّض|reimbursed_conditional=Conditional|unknown=Unknown", Pairs("ar"));
            Assert.Empty(r.GetProperty("old").EnumerateArray());                     // a row without `code` is not an option (no empty code saved)
        }

        // the page, fed the real format by the smoke fakes: the select's values are the codes, the labels the BRD's
        if (Smoke("edit") is { } e)
        {
            var select = e.GetProperty("reimbursementSelect").GetString()!;
            Assert.Contains("<option value=\"reimbursed\" selected>Geri ödemede</option>", select);
            Assert.Contains("<option value=\"unknown\">Bilinmiyor</option>", select);
            Assert.DoesNotContain("value=\"\"", select);
            Assert.DoesNotContain("not_reimbursed", select);                         // inactive
            Assert.Equal("reimbursed", e.GetProperty("putBody").GetProperty("countryPrices")[0].GetProperty("reimbursementStatusCode").GetString());
        }
        if (Smoke("focus") is { } f)
        {
            var menu = f.GetProperty("addMenu").GetString()!;
            Assert.Contains("data-act=\"add-country\" data-code=\"AE\"", menu);
            Assert.Contains("data-act=\"add-country\" data-code=\"TR\"", menu);
            Assert.DoesNotContain("data-act=\"add-country\" data-code=\"\"", menu);
        }

        // no fake in the smoke harness speaks the old shape any more
        var smoke = File.ReadAllText(Path.Combine(Path.GetDirectoryName(WebRoot())!, "Diten.Web.Tests", "Js", "competitor-products-smoke.js"));
        Assert.DoesNotContain("valueCode", smoke);
        Assert.DoesNotContain("displayName: 'Reimbursed'", smoke);
        Assert.DoesNotContain("v.valueCode", Script("competitor-pages.js"));
    }

    [Fact]
    public void FixA_F7_prepared_by_is_the_creation_and_applied_is_its_own_line()
    {
        var node = RunNode(Core() + """
            const rounds = [{ submittedAt: '2026-10-14T10:58:00Z' }, { submittedAt: '2026-10-14T10:53:00Z' }];
            process.stdout.write(JSON.stringify({
              created: C.preparedOf({ createdBy: 'a', createdAt: '2026-10-14T10:40:00Z', updatedBy: 'b', updatedAt: '2026-10-14T11:04:00Z', status: 'applied', reviewRounds: rounds }),
              rounds: C.preparedOf({ createdBy: 'a', updatedBy: 'b', updatedAt: '2026-10-14T11:04:00Z', status: 'applied', reviewRounds: rounds }),
              appliedNoRounds: C.preparedOf({ createdBy: 'a', updatedAt: '2026-10-14T11:04:00Z', status: 'applied', reviewRounds: [] }),
              draft: C.preparedOf({ createdBy: 'a', updatedAt: '2026-10-14T10:50:00Z', status: 'draft', reviewRounds: [] }),
              applied: C.appliedOf({ appliedBy: 'b', appliedAt: '2026-10-14T11:04:00Z' }),
              notApplied: C.appliedOf({ status: 'draft' })
            }));
            """);
        if (node is not null)
        {
            var r = JsonDocument.Parse(node).RootElement;
            Assert.Equal("2026-10-14T10:40:00Z", r.GetProperty("created").GetProperty("at").GetString());     // createdAt when the MDM sends it
            Assert.Equal("a", r.GetProperty("created").GetProperty("by").GetString());
            Assert.Equal("2026-10-14T10:53:00Z", r.GetProperty("rounds").GetProperty("at").GetString());      // else the first submission
            Assert.Equal(JsonValueKind.Null, r.GetProperty("appliedNoRounds").GetProperty("at").ValueKind);   // never the applying moment
            Assert.Equal("2026-10-14T10:50:00Z", r.GetProperty("draft").GetProperty("at").GetString());
            Assert.Equal("b", r.GetProperty("applied").GetProperty("by").GetString());
            Assert.Equal(JsonValueKind.Null, r.GetProperty("notApplied").ValueKind);
        }

        if (Smoke("applied") is not { } a) { return; }
        var page = a.GetProperty("page").GetString()!;
        var prepared = a.GetProperty("expPrepared").GetString()!;
        var applied = a.GetProperty("expApplied").GetString()!;
        Assert.Contains("<span class=\"cp-k\">Hazırlayan</span><span class=\"cp-v\">Elif Kaya <span class=\"cp-small cp-muted\">" + prepared + "</span>", page);
        Assert.Contains("<span class=\"cp-k\">Uygulandı</span><span class=\"cp-v\">Zeynep Koç <span class=\"cp-small cp-muted\">" + applied + "</span>", page);
        Assert.DoesNotContain("Hazırlayan</span><span class=\"cp-v\">Elif Kaya <span class=\"cp-small cp-muted\">" + applied, page);
    }

    [Fact]
    public void FixC_G11_the_change_history_shows_money_the_reimbursement_label_and_the_date_never_the_raw_row()
    {
        var node = RunNode(Core() + """
            process.stdout.write(JSON.stringify(['186.3 TRY · reimbursed · 2026-07-01', '172.4 TRY', '9 AZN · unknown', 'moved from Koroneks', 'Koronex', null].map(C.priceChange)));
            """);
        if (node is not null)
        {
            var r = JsonDocument.Parse(node).RootElement.EnumerateArray().ToArray();
            Assert.Equal(186.3m, r[0].GetProperty("price").GetDecimal());
            Assert.Equal("TRY", r[0].GetProperty("currency").GetString());
            Assert.Equal("reimbursed", r[0].GetProperty("reimbursementCode").GetString());
            Assert.Equal("2026-07-01", r[0].GetProperty("validFrom").GetString());
            Assert.Equal(JsonValueKind.Null, r[1].GetProperty("reimbursementCode").ValueKind);
            Assert.Equal("unknown", r[2].GetProperty("reimbursementCode").GetString());
            Assert.Equal("Koroneks", r[3].GetProperty("movedFrom").GetString());
            Assert.Equal(JsonValueKind.Null, r[4].ValueKind);
            Assert.Equal(JsonValueKind.Null, r[5].ValueKind);
        }

        if (Smoke("details") is not { } d) { return; }
        var page = d.GetProperty("page").GetString()!;
        Assert.Contains("172,40 TRY · Geri ödemede · 01.01.2026 → 186,30 TRY · Geri ödemede · 01.07.2026", page);
        Assert.Contains("Koroneks kaydından taşındı", page);
        Assert.DoesNotContain("186.3 TRY", page);
        Assert.DoesNotContain("· reimbursed", page);
        Assert.DoesNotContain("moved from", page);
    }

    // ═══ 4 · the proxies ═════════════════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("List", "GET", "/api/mdm/competitor-products", Read)]
    [InlineData("Get", "GET", "/api/mdm/competitor-products/" + Id, Read)]
    [InlineData("MergeCandidates", "GET", "/api/mdm/competitor-products/merge-candidates", Manage)]
    [InlineData("Create", "POST", "/api/mdm/competitor-products", Manage)]
    [InlineData("Update", "PUT", "/api/mdm/competitor-products/" + Id, Manage)]
    [InlineData("Deactivate", "POST", "/api/mdm/competitor-products/" + Id + "/deactivate", Manage)]
    [InlineData("MergeRecord", "POST", "/api/mdm/competitor-products/" + Id + "/merge", Manage)]
    [InlineData("Proposals", "GET", "/api/mdm/competitor-products/proposals", Manage)]
    [InlineData("GetProposal", "GET", "/api/mdm/competitor-products/proposals/" + Id, Manage)]
    [InlineData("SaveDecision", "PUT", "/api/mdm/competitor-products/proposals/" + Id + "/decision", Manage)]
    [InlineData("SubmitDecision", "POST", "/api/mdm/competitor-products/proposals/" + Id + "/decision/submit", Manage)]
    [InlineData("WithdrawDecision", "POST", "/api/mdm/competitor-products/proposals/" + Id + "/decision/withdraw", Manage)]
    [InlineData("ReviewDecision", "POST", "/api/mdm/competitor-products/proposals/" + Id + "/decision/approve-reject", Manage)]
    [InlineData("Products", "GET", "/api/global-products/selector", ProductRead)]
    [InlineData("Forms", "GET", "/api/mdm/brand-products/contract", BrandRead)]
    [InlineData("Indications", "GET", "/api/crm/doctor-insights/products/" + Id + "/indication-profiles", InsightsRead)]
    [InlineData("Usage", "GET", "/api/crm/visit-report/v2/competitor-usage", Usage)]
    [InlineData("People", "GET", "/api/v1/meetings/lookups/attendees", People)]
    public async Task A9_every_proxy_goes_to_its_path_with_exactly_its_key(string action, string method, string path, string key)
    {
        var (denied, deniedController) = Arrange(new[] { Read, Manage, ProductRead }.Where(k => k != key).ToArray());
        Assert.Equal(403, Status(await Invoke(deniedController, action)));
        Assert.Empty(denied.Requests);

        var (gateway, controller) = Arrange(key);
        Assert.Equal(200, Status(await Invoke(controller, action)));
        var request = Assert.Single(gateway.Requests);
        Assert.Equal(method, request.Method);
        Assert.Equal(GatewayUrl + path, request.Uri.Split('?')[0]);
        Assert.Equal(TenantId.ToString(), request.Tenant);
    }

    [Fact]
    public async Task A9_the_indications_take_thresholds_too_two_brd_sets_only_and_a_tenant_id_body_is_refused()
    {
        var (g1, t) = Arrange(Thresholds);
        Assert.Equal(200, Status(await t.Indications(Guid.Parse(Id), CancellationToken.None)));
        Assert.Single(g1.Requests);

        var (g2, reader) = Arrange(Read);
        Assert.Equal(404, Status(await reader.ReferenceValues("crm-visit-success", CancellationToken.None)));
        Assert.Empty(g2.Requests);
        Assert.Equal(200, Status(await reader.ReferenceValues("mdm-reimbursement-status", CancellationToken.None)));
        Assert.Equal(200, Status(await reader.ReferenceValues("market", CancellationToken.None)));
        var (g3, nobody) = Arrange(Manage);
        Assert.Equal(403, Status(await nobody.ReferenceValues("market", CancellationToken.None)));
        Assert.Empty(g3.Requests);

        var (g4, writer) = Arrange(new[] { Manage }, "{\"tenantId\":\"x\"}");
        Assert.Equal(400, Status(await writer.Create(CancellationToken.None)));
        Assert.Equal(400, Status(await writer.SaveDecision(Guid.Parse(Id), CancellationToken.None)));
        Assert.Equal(400, Status(await writer.ReviewDecision(Guid.Parse(Id), CancellationToken.None)));
        Assert.Empty(g4.Requests);
    }

    // ═══ 5 · seven languages, RTL, dark ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void A10_every_key_in_seven_languages_without_echo_the_families_rtl_and_dark()
    {
        var keys = CompetitorProductsIndex.ScriptKeys.Concat(CompetitorProductsIndex.ViewKeys).ToArray();
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

        var scripts = Script("competitor-pages.js") + Script("competitor-core.js");
        foreach (Match m in Regex.Matches(scripts, @"\bL\.([A-Z][A-Za-z_]+)"))
        {
            Assert.Contains(m.Groups[1].Value, CompetitorProductsIndex.ScriptKeys);
        }

        var families = new Dictionary<string, string[]>
        {
            ["Status_"] = ["proposed", "active", "inactive", "merged", "rejected"],
            ["Decision_"] = ["complete", "merge", "reject"],
            ["State_"] = ["waiting", "draft", "in_review", "returned", "returned_sod", "returned_withdrawn", "applied"],
            ["ErrMarkets_"] = ["duplicate", "price", "currency"],
            ["Field_"] = ["name", "company", "aliases", "activeIngredients", "formCode", "strength", "atcCode", "competesWith", "indications", "created", "proposed", "status", "countryPrices"],
            ["HistRound_"] = ["approved", "rejected", "cancelled", "timed_out", "closed"],
            ["HistDecision_"] = ["approve", "return"],
            ["Form_"] = ["tablet", "capsule", "syrup", "injection", "ointment", "cream", "drops", "inhaler", "patch", "other", "film_coated_tablet", "modified_release_tablet",
                "extended_release_tablet", "modified_release_capsule", "ampoule"],
            ["Err_"] = ["workflow_template_missing", "concurrency_conflict", "competitor_duplicate", "competitor_market_duplicate", "competitor_proposal_sod", "competitor_decision_locked",
                "competitor_not_selectable", "competitor_merge_confirm_required", "approval_forbidden", "workflow_unavailable", "rejection_comment_required", "reference_data_unavailable"]
        };
        foreach (var (prefix, codes) in families)
        {
            foreach (var code in codes) { Assert.Contains(prefix + code, CompetitorProductsIndex.ScriptKeys); }
        }

        // the MDM vocabulary (G4) is fully worded
        var vocabulary = File.ReadAllText(Path.Combine(RepoRoot(), "services", "Diten.MdmService", "src", "Diten.MdmService.Domain", "Vocabulary", "BrandProductVocabulary.cs"));
        var forms = Regex.Match(vocabulary, @"DosageForms\s*=\s*\[(?<list>[^\]]*)\]").Groups["list"].Value;
        foreach (Match m in Regex.Matches(forms, "\"([a-z-]+)\"")) { Assert.Contains("Form_" + m.Groups[1].Value.Replace('-', '_'), CompetitorProductsIndex.ScriptKeys); }

        var tr = Resx("tr");
        Assert.Equal("Onaya gönder", tr["SendForApproval"]);
        Assert.Equal("Etkisini okudum, birleştirmeyi onaylıyorum.", tr["AckText"]);
        Assert.Equal("Bu kararı siz hazırladınız; Görev Merkezi'nde başka biri onaylar.", tr["SodNote"]);
        Assert.Equal("منتجات المنافسين", Resx("ar")["PageTitle"]);
        foreach (var culture in new[] { "fr", "es", "zh", "ar", "ru" })
        {
            var resx = Resx(culture);
            var echoes = keys.Where(k => resx[k] == en[k] && Regex.Replace(en[k], @"\{\d\}|[\s·:–\-,.()/<>…]|MDM|ATC", string.Empty).Length > 0).ToList();
            Assert.True(echoes.Count <= 14, culture + ": too many English echoes: " + string.Join(", ", echoes));
        }

        var css = File.ReadAllText(Path.Combine(ScriptDir(), "competitor-products.css"));
        Assert.Contains("[dir=\"rtl\"] .cp-flip", css);
        Assert.Contains("[data-bs-theme=\"dark\"] #cp-root", css);
        Assert.DoesNotMatch(@"(?<![-\w])(margin|padding)-(left|right)\s*:", css);
        Assert.DoesNotMatch(@"(?<![-\w])(left|right)\s*:", css);
        var shell = View("_Shell.cshtml");
        Assert.True(shell.IndexOf("id=\"cp-modal\"", StringComparison.Ordinal) < shell.LastIndexOf("</div>", StringComparison.Ordinal)); // the modal is inside the root
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────

    private static string[] Args(string value) => Regex.Matches(value, @"\{\d\}").Select(m => m.Value).Distinct().OrderBy(x => x).ToArray();

    private static CompetitorProductsPageViewModel Model(IActionResult result) => Assert.IsType<CompetitorProductsPageViewModel>(Assert.IsType<ViewResult>(result).Model);

    private static (string, string) ViewAndMode(IActionResult result)
    {
        var view = Assert.IsType<ViewResult>(result);
        return (Path.GetFileNameWithoutExtension(view.ViewName!), Model(result).Mode);
    }

    private static int? Status(IActionResult result) => result switch
    {
        ContentResult c => c.StatusCode,
        ObjectResult o => o.StatusCode ?? 200,
        StatusCodeResult s => s.StatusCode,
        _ => null
    };

    private static Task<IActionResult> Invoke(CompetitorProductsController c, string action)
    {
        var id = Guid.Parse(Id);
        var ct = CancellationToken.None;
        return action switch
        {
            "List" => c.List(ct), "Get" => c.Get(id, ct), "MergeCandidates" => c.MergeCandidates(ct), "Create" => c.Create(ct), "Update" => c.Update(id, ct),
            "Deactivate" => c.Deactivate(id, ct), "MergeRecord" => c.MergeRecord(id, ct), "Proposals" => c.Proposals(ct), "GetProposal" => c.GetProposal(id, ct),
            "SaveDecision" => c.SaveDecision(id, ct), "SubmitDecision" => c.SubmitDecision(id, ct), "WithdrawDecision" => c.WithdrawDecision(id, ct),
            "ReviewDecision" => c.ReviewDecision(id, ct), "Products" => c.Products(ct), "Forms" => c.Forms(ct), "Indications" => c.Indications(id, ct),
            "Usage" => c.Usage(ct), "People" => c.People(ct),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
    }

    private static (Gateway Gateway, CompetitorProductsController Controller) Arrange(params string[] permissions) => Arrange(permissions, "{\"name\":\"x\"}");

    private static (Gateway Gateway, CompetitorProductsController Controller) Arrange(string[] permissions, string body)
    {
        var gateway = new Gateway();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = GatewayUrl }).Build();
        var controller = new CompetitorProductsController(new HttpClient(gateway), configuration, NullLogger<CompetitorProductsController>.Instance);
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()), new("sub", "u-1") };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        http.Request.ContentType = "application/json";
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return (gateway, controller);
    }

    private static JsonElement? Smoke(string scenario)
    {
        var smoke = Path.Combine(Path.GetDirectoryName(WebRoot())!, "Diten.Web.Tests", "Js", "competitor-products-smoke.js").Replace("\\", "/");
        var node = RunNode("process.argv[2] = '" + ScriptDir().Replace("\\", "/") + "'; process.argv[3] = '" + scenario + "'; require('" + smoke + "');");
        if (node is null) { return null; }
        var r = JsonDocument.Parse(node).RootElement.Clone();
        Assert.False(r.TryGetProperty("crash", out var crash), crash.ValueKind == JsonValueKind.String ? crash.GetString() : "");
        return r;
    }

    private static string Core() => "const C = require('" + Path.Combine(ScriptDir(), "competitor-core.js").Replace("\\", "/") + "');\n";

    private static string? RunNode(string code)
    {
        var temp = Path.Combine(Path.GetTempPath(), "cp4-" + Guid.NewGuid().ToString("N") + ".js");
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

    private static string ScriptDir() => Path.Combine(WebRoot(), "wwwroot", "assets", "js", "MasterData", "CompetitorProducts");

    private static string Script(string file) => File.ReadAllText(Path.Combine(ScriptDir(), file)).Replace("\r\n", "\n");

    private static string View(string file) => File.ReadAllText(Path.Combine(WebRoot(), "Views", "MasterData", "CompetitorProducts", file)).Replace("\r\n", "\n");

    private static Dictionary<string, string> Resx(string culture)
    {
        var file = "CompetitorProductsIndex" + (culture.Length == 0 ? "" : "." + culture) + ".resx";
        return XDocument.Load(Path.Combine(WebRoot(), "Resources", "Views", "MasterData", "CompetitorProducts", file)).Root!
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
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"data\":{}}", Encoding.UTF8, "application/json") });
        }
    }
}
