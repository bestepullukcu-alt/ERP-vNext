using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web;
using Diten.Web.Controllers.CRM;
using Diten.Web.Models.CRM;
using Diten.Web.Views.CRM.KnowledgePaths;
using Diten.Web.Views.CRM.ObjectionResponses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VW-W3 (W3-WEB-c) — the objection library (/CRM/ObjectionResponses) and the knowledge path studio's "Sunum notları"
/// tab on the REAL controllers, views, resx and scripts (Acceptance): (1) UAS-001 and no one-click approve in the editor
/// (K-A2); (2) the pure rules in Node — abilities × status actions, the required return note, the completeness counter, the
/// target hint (MINUTES × 60), the claim / objection pickers (approved + same product + at most 10), the COMPLETE presentation
/// object; (3) the pages in a fake DOM — the list, create (createError null), submit → awaiting approval, the return note,
/// the retire dialog, the step tab's save with the complete presentation; (4) every proxy → its CRM path with exactly its
/// keys, the decision note refused here too, the list's paging; (5) the studio's objection lookup and the step's notes;
/// (6) seven languages.
/// </summary>
public sealed class ObjectionLibraryWebTests
{
    private const string GatewayUrl = "http://gateway.test";
    private const string Id = "11111111-2222-3333-4444-555555555555";
    private const string ProductId = "aaaaaaaa-0000-0000-0000-00000000000a";
    private static readonly Guid TenantId = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");
    private static readonly string[] Cultures = ["", "en", "tr", "fr", "es", "zh", "ar", "ru"];

    private const string Read = "crm.objection-response.read";
    private const string Manage = "crm.objection-response.manage";
    private const string Submit = "crm.objection-response.submit";
    private const string ClaimRead = "crm.claim.read";
    private const string ProductRead = "mdm.global-products.read";

    // ═══ 1 · UAS-001, K-A2 ═══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void A1_without_the_read_key_every_page_draws_only_the_access_notice_and_the_editor_has_no_approve()
    {
        var (_, none) = Arrange();
        Assert.False(Model(none.Index()).CanRead);
        Assert.False(Model(none.Details(Guid.Parse(Id))).CanRead);
        var (_, reader) = Arrange(Read);
        var model = Model(reader.Index());
        Assert.True(model.CanRead);
        Assert.False(model.CanManage || model.CanSubmit || model.CanReadClaims || model.CanReadProducts);
        var (_, author) = Arrange(Read, Manage, Submit, ClaimRead, ProductRead);
        var create = Model(author.Create(Guid.Parse(ProductId)));
        Assert.True(create.CanManage && create.CanSubmit && create.CanReadClaims && create.CanReadProducts);
        Assert.Equal(Guid.Parse(ProductId), create.ProductId);

        foreach (var (file, root) in new[] { ("Index.cshtml", "id=\"orListRoot\""), ("Edit.cshtml", "id=\"orEditRoot\""), ("Details.cshtml", "id=\"orDetailsRoot\"") })
        {
            var view = View(file);
            var denied = view.IndexOf("<partial name=\"_AccessDenied\"", StringComparison.Ordinal);
            var bail = view.IndexOf("return;", denied, StringComparison.Ordinal);
            Assert.True(denied > 0 && bail > denied && view.IndexOf(root, StringComparison.Ordinal) > bail, file);
            Assert.DoesNotContain("cdn.", view);
        }

        Assert.Contains("@if (!Model.CanRead || !Model.CanManage)", View("Edit.cshtml"));
        // K-A2: the decision is the reviewer's, on the record page / the Work Center — never in the editor
        var editor = View("Edit.cshtml") + Script("ObjectionResponses", "objection-edit.js");
        Assert.DoesNotContain("/decision", editor);
        Assert.DoesNotContain("orApprove", editor);
        Assert.DoesNotContain("data-action=\"approve\"", editor);
        // no new JS library
        Assert.DoesNotMatch(@"<script src=""(?!~/assets/js/)", View("Index.cshtml") + View("Edit.cshtml") + View("Details.cshtml"));
    }

    // ═══ 2 · the pure rules (Node) ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void A2_actions_return_note_completeness_target_hint_pickers_and_the_complete_presentation()
    {
        var node = RunNode(Core() + """
            const ab = (o) => Object.assign({ canEdit: false, canSubmit: false, canWithdraw: false, canDecide: false, canRetire: false, canCopy: false }, o);
            const all = ab({ canEdit: true, canSubmit: true, canWithdraw: true, canDecide: true, canRetire: true, canCopy: true });
            const out = {};
            out.actions = C.STATUSES.map(s => C.actions({ status: s, abilities: all }));
            out.none = C.actions({ status: 'draft', abilities: ab({}) });
            out.staleAbility = C.actions({ status: 'approved', abilities: ab({ canWithdraw: true, canEdit: true, canDecide: true }) });
            out.decision = [C.decisionBody('return', '   '), C.decisionBody('return', ' not '), C.decisionBody('approve', ''), C.decisionBody('reject', 'x')];
            const items = [
              { kind: 'content', stepId: 'a', presentation: { keyMessage: 'k', talkTrack: 't' } },
              { kind: 'content', stepId: 'b', presentation: { keyMessage: 'k', talkTrack: '  ' } },
              { kind: 'content', stepId: 'c', presentation: null },
              { kind: 'claim', claimId: 'x' }];
            const c = C.completeness(items);
            out.completeness = [c.filled, c.total, c.missing.map(m => m.stepId)];
            out.hint = [C.targetHint(75, 2), C.targetHint(null, 2), C.targetHint('', 3), C.targetHint(null, null), C.targetHint(0, 1)];
            const claims = [
              { id: '1', status: 'approved', productId: 'P', isArchived: false },
              { id: '2', status: 'approved', productId: 'Q', isArchived: false },
              { id: '3', status: 'draft', productId: 'P', isArchived: false },
              { id: '4', status: 'approved', productId: 'p', isArchived: true }];
            out.claimOptions = C.claimOptions(claims, 'p').map(x => x.id);
            let sel = { selected: [] };
            for (let i = 0; i < 11; i++) sel = C.toggleClaim(sel.selected, 'c' + i);
            out.claimLimit = [sel.selected.length, sel.refused];
            out.untoggle = C.toggleClaim(['a', 'b'], 'A').selected;
            const rows = [
              { id: 'o1', status: 'approved', productId: 'P' }, { id: 'o2', status: 'draft', productId: 'P' },
              { id: 'o3', status: 'approved', productId: 'Q' }, { id: 'o4', status: 'retired', productId: 'P' }];
            out.objectionOptions = C.objectionOptions(rows, 'P').map(x => x.id);
            out.split = C.splitLinked(['o1', 'o4', 'o9'], rows, 'P');
            let os = { selected: [] };
            for (let i = 0; i < 12; i++) os = C.toggleObjection(os.selected, 'x' + i);
            out.objectionLimit = [os.selected.length, os.refused];
            out.body = [C.presentationBody({}), C.presentationBody({ keyMessage: ' a ', talkTrack: 'b', targetSeconds: '90', transition: '', objectionResponseIds: ['x', 'x', 'y'] })];
            out.invalid = [C.validatePresentation({ keyMessage: 'x'.repeat(501) }), C.validatePresentation({ targetSeconds: 9 }), C.validatePresentation({ targetSeconds: 3601 }),
              C.validatePresentation({ targetSeconds: '12.5' }), C.validatePresentation({ targetSeconds: '' }), C.validatePresentation({ talkTrack: 'x'.repeat(4001), transition: 'y'.repeat(501), objectionResponseIds: new Array(11).fill('z') })];
            out.edit = C.editBody({ productId: 'P', categoryCode: ' ', question: ' q ', answer: '', claimIds: ['1'] }, 4);
            out.tone = [C.categoryTone('price'), C.categoryTone('side_effect'), C.categoryTone('unknown')];
            const dto = { status: 'in_review', submittedBy: 'u1', submittedAt: '2026-10-10T09:18:00Z', createdBy: 'u1', createdAt: '2026-10-07T11:05:00Z',
              reviewRounds: [{ roundNo: 1, submittedBy: 'u1', submittedAt: '2026-10-08T10:00:00Z', closedAt: '2026-10-08T12:00:00Z', outcome: 'returned' },
                             { roundNo: 2, submittedBy: 'u1', submittedAt: '2026-10-10T09:18:00Z', closedAt: null }],
              decisions: [{ roundNo: 1, by: 'u2', outcome: 'return', comment: 'Kaynak', at: '2026-10-08T12:00:00Z' }] };
            out.pending = C.pendingLine(dto, { U1: 'Elif' }, '?');
            out.timeline = C.timeline(dto, { u1: 'Elif', u2: 'Murat' }, '?').map(e => [e.kind, e.by, e.comment || null]);
            out.returned = C.returnNote(Object.assign({}, dto, { status: 'draft', reviewRounds: dto.reviewRounds.slice(0, 1) }));
            out.sodReason = C.returnNote({ status: 'draft', decisions: [], reviewRounds: [{ roundNo: 1, closedAt: '2026-10-08T12:00:00Z', reasonCode: 'objection_response_sod' }] });
            process.stdout.write(JSON.stringify(out));
            """);
        if (node is null) return;
        var r = JsonDocument.Parse(node).RootElement;
        Assert.Equal("[[\"edit\",\"submit\",\"retire\",\"copy\"],[\"withdraw\",\"approve\",\"return\"],[\"retire\",\"copy\"],[\"copy\"]]", r.GetProperty("actions").GetRawText());
        Assert.Equal("[]", r.GetProperty("none").GetRawText());
        Assert.Equal("[]", r.GetProperty("staleAbility").GetRawText());
        var d = r.GetProperty("decision");
        Assert.Equal("comment_required", d[0].GetProperty("error").GetString());
        Assert.Equal("not", d[1].GetProperty("body").GetProperty("comment").GetString());
        Assert.True(d[2].GetProperty("ok").GetBoolean());
        Assert.Equal(JsonValueKind.Null, d[2].GetProperty("body").GetProperty("comment").ValueKind);
        Assert.Equal("decision_invalid", d[3].GetProperty("error").GetString());
        Assert.Equal("[1,3,[\"b\",\"c\"]]", r.GetProperty("completeness").GetRawText());
        var hint = r.GetProperty("hint");
        Assert.Equal((75, "target"), (hint[0].GetProperty("seconds").GetInt32(), hint[0].GetProperty("source").GetString()));
        Assert.Equal((120, "estimate"), (hint[1].GetProperty("seconds").GetInt32(), hint[1].GetProperty("source").GetString()));
        Assert.Equal(180, hint[2].GetProperty("seconds").GetInt32());
        Assert.Equal(JsonValueKind.Null, hint[3].GetProperty("seconds").ValueKind);
        Assert.Equal(60, hint[4].GetProperty("seconds").GetInt32());
        Assert.Equal("[\"1\"]", r.GetProperty("claimOptions").GetRawText());
        Assert.Equal("[10,true]", r.GetProperty("claimLimit").GetRawText());
        Assert.Equal("[\"b\"]", r.GetProperty("untoggle").GetRawText());
        Assert.Equal("[\"o1\"]", r.GetProperty("objectionOptions").GetRawText());
        Assert.Equal("[\"o1\"]", r.GetProperty("split").GetProperty("usable").GetRawText());
        Assert.Equal(2, r.GetProperty("split").GetProperty("unusable").GetArrayLength());
        Assert.Equal("[10,true]", r.GetProperty("objectionLimit").GetRawText());
        var body = r.GetProperty("body");
        Assert.Equal("{\"keyMessage\":null,\"talkTrack\":null,\"targetSeconds\":null,\"transition\":null,\"objectionResponseIds\":[]}", body[0].GetRawText());
        Assert.Equal("{\"keyMessage\":\"a\",\"talkTrack\":\"b\",\"targetSeconds\":90,\"transition\":null,\"objectionResponseIds\":[\"x\",\"y\"]}", body[1].GetRawText());
        Assert.Equal("[[\"keyMessage\"],[\"targetSeconds\"],[\"targetSeconds\"],[\"targetSeconds\"],[],[\"talkTrack\",\"transition\",\"objectionResponseIds\"]]", r.GetProperty("invalid").GetRawText());
        Assert.Equal("{\"productId\":\"P\",\"categoryCode\":null,\"question\":\"q\",\"answer\":null,\"claimIds\":[\"1\"],\"expectedVersion\":4}", r.GetProperty("edit").GetRawText());
        Assert.Equal("[\"warning\",\"danger\",\"secondary\"]", r.GetProperty("tone").GetRawText());
        Assert.Equal("Elif", r.GetProperty("pending").GetProperty("by").GetString());
        Assert.Equal("[[\"submitted\",\"Elif\",null],[\"returned\",\"Murat\",\"Kaynak\"],[\"submitted\",\"Elif\",null],[\"created\",\"Elif\",null]]", r.GetProperty("timeline").GetRawText());
        Assert.Equal("Kaynak", r.GetProperty("returned").GetProperty("comment").GetString());
        Assert.Equal("objection_response_sod", r.GetProperty("sodReason").GetProperty("reasonCode").GetString());
    }

    // ═══ 3 · the pages in a fake DOM ═════════════════════════════════════════════════════════════════════

    [Fact]
    public void A3_list_shows_the_columns_filters_and_the_empty_card_only_for_an_empty_library()
    {
        if (Smoke("list") is not { } r) return;
        Assert.Equal("/api/objection-responses", r.GetProperty("ajax").GetString());
        Assert.Equal("server", r.GetProperty("dataMode").GetString());
        Assert.Equal("[\"id\",\"productName\",\"categoryCode\",\"question\",\"claimIds\",\"usageCount\",\"status\",\"updatedAt\",\"id\"]", r.GetProperty("columns").GetRawText());
        Assert.Equal("[\"productId\",\"categoryCode\",\"status\"]", r.GetProperty("filterKeys").GetRawText());
        Assert.Equal("[\"draft\",\"in_review\",\"approved\",\"retired\"]", r.GetProperty("fills").GetProperty("filterStatus").GetRawText());
        Assert.Contains("bg-label-warning", r.GetProperty("category").GetString());
        Assert.Contains(">Fiyat<", r.GetProperty("category").GetString()); // the BRD label, not the code
        Assert.Contains("Onay bekliyor", r.GetProperty("status").GetString());
        Assert.Equal(2, r.GetProperty("claims").GetInt32());
        Assert.Equal("quickView", r.GetProperty("actionsReadOnly").GetString());
        Assert.Equal("quickView,edit", r.GetProperty("actionsDraft").GetString());
        Assert.Equal("[true,false]", r.GetProperty("emptyWhenNone").GetRawText());
        Assert.Equal("[false,true]", r.GetProperty("tableWhenFilteredEmpty").GetRawText());
    }

    [Fact]
    public void A3_create_saves_a_half_written_draft_with_only_approved_claims_of_the_product_and_no_error()
    {
        if (Smoke("create") is not { } r) return;
        Assert.Equal(JsonValueKind.Null, r.GetProperty("createError").ValueKind);
        Assert.Equal("[\"cccccccc-0000-0000-0000-000000000001\",\"cccccccc-0000-0000-0000-000000000002\"]", r.GetProperty("offered").GetRawText());
        var body = r.GetProperty("createBody");
        Assert.Equal(ProductId, body.GetProperty("productId").GetString());
        Assert.Equal("Fiyatı yüksek.", body.GetProperty("question").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("answer").ValueKind); // K-A3: a half-written draft
        Assert.False(body.TryGetProperty("expectedVersion", out _));
        Assert.Equal("/CRM/ObjectionResponses/Edit/22222222-2222-2222-2222-222222222222", r.GetProperty("location").GetString());
        Assert.True(r.GetProperty("noApproveButtonInEditor").GetBoolean());
    }

    [Fact]
    public void A3_submit_turns_the_draft_into_awaiting_approval_and_the_submitter_sees_the_sod_hint()
    {
        if (Smoke("submit") is not { } r) return;
        Assert.Equal("[\"data-action=\\\"retire\\\"\",\"data-action=\\\"submit\\\"\",\"data-action=\\\"copy\\\"\"]", r.GetProperty("actionsHtml").GetRawText());
        Assert.Equal(1, r.GetProperty("submitCalls").GetInt32());
        Assert.StartsWith("Onay bekliyor · Elif Kaya, ", r.GetProperty("bandAfter").GetString());
        Assert.Contains("Onay bekliyor", r.GetProperty("statusAfter").GetString());
        Assert.Equal("[\"data-action=\\\"withdraw\\\"\"]", r.GetProperty("actionsAfter").GetRawText());
        Assert.True(r.GetProperty("sodHint").GetBoolean());
        Assert.Equal(JsonValueKind.Null, r.GetProperty("loadError").ValueKind);
    }

    [Fact]
    public void A3_return_to_draft_needs_a_note_and_sends_exactly_decision_and_comment()
    {
        if (Smoke("return") is not { } r) return;
        Assert.Equal("[\"data-action=\\\"return\\\"\",\"data-action=\\\"approve\\\"\"]", r.GetProperty("actionsHtml").GetRawText());
        Assert.True(r.GetProperty("modalOpen").GetBoolean());
        Assert.True(r.GetProperty("confirmDisabledEmpty").GetBoolean());
        Assert.True(r.GetProperty("confirmDisabledBlank").GetBoolean());
        Assert.Equal(0, r.GetProperty("decisionCallsAfterBlank").GetInt32());
        Assert.False(r.GetProperty("confirmDisabledNote").GetBoolean());
        Assert.Equal("{\"decision\":\"return\",\"comment\":\"Kaynak eksik\"}", r.GetProperty("decision").GetRawText());
    }

    [Fact]
    public void A3_retire_dialog_names_the_steps_using_it_and_sends_the_version()
    {
        if (Smoke("retire") is not { } r) return;
        Assert.True(r.GetProperty("retireOpen").GetBoolean());
        Assert.Equal("[\"2 adımda kullanılıyor.\",true]", r.GetProperty("retireUsed").GetRawText());
        Assert.Equal("{\"expectedVersion\":3}", r.GetProperty("retire").GetRawText());
        Assert.True(r.GetProperty("usages").GetBoolean());
    }

    [Fact]
    public void A3_step_tab_saves_the_complete_presentation_and_drops_an_unusable_linked_answer()
    {
        if (Smoke("studio") is not { } r) return;
        Assert.Equal("Sunum notları: 1 / 2 adım dolu", r.GetProperty("bar").GetString());
        Assert.Equal("[\"data-step-id=\\\"s2\\\"\"]", r.GetProperty("barMissing").GetRawText());
        Assert.Equal("Beklenen itirazlar · 1 / 10", r.GetProperty("objectionsTitle").GetString());
        Assert.True(r.GetProperty("unusableShown").GetBoolean());
        var put = r.GetProperty("put");
        Assert.Equal("/paths/path1/steps/s1", put.GetProperty("url").GetString());
        Assert.Equal("[\"keyMessage\",\"talkTrack\",\"targetSeconds\",\"transition\",\"objectionResponseIds\"]", put.GetProperty("keys").GetRawText());
        Assert.Equal("{\"keyMessage\":\"Anahtar\",\"talkTrack\":\"Yeni metin\",\"targetSeconds\":null,\"transition\":null,\"objectionResponseIds\":[\"dddddddd-0000-0000-0000-000000000001\"]}",
            put.GetProperty("presentation").GetRawText());
        Assert.Equal("{\"chainStepId\":\"k1\",\"branchCode\":\"main\",\"position\":0}", put.GetProperty("arrangement").GetRawText());
        Assert.Equal("S-s1", put.GetProperty("stepCode").GetString()); // the full step body, not a patch
        Assert.Equal(1, r.GetProperty("reloaded").GetInt32());
        Assert.Equal(JsonValueKind.Null, r.GetProperty("studioError").ValueKind);
    }

    // ═══ 4 · proxies ═════════════════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("Get", "GET", "/api/crm/objection-responses/" + Id, new[] { Read })]
    [InlineData("Usages", "GET", "/api/crm/objection-responses/" + Id + "/usages", new[] { Read })]
    [InlineData("Create", "POST", "/api/crm/objection-responses", new[] { Manage })]
    [InlineData("Update", "PUT", "/api/crm/objection-responses/" + Id, new[] { Manage })]
    [InlineData("Submit", "POST", "/api/crm/objection-responses/" + Id + "/submit", new[] { Submit })]
    [InlineData("Withdraw", "POST", "/api/crm/objection-responses/" + Id + "/withdraw", new[] { Submit })]
    [InlineData("Retire", "POST", "/api/crm/objection-responses/" + Id + "/retire", new[] { Manage })]
    [InlineData("Copy", "POST", "/api/crm/objection-responses/" + Id + "/copy", new[] { Manage })]
    [InlineData("Decide", "POST", "/api/crm/objection-responses/" + Id + "/decision", new[] { Read })]
    [InlineData("Claims", "GET", "/api/crm/content-composition/claims", new[] { Read, ClaimRead })]
    public async Task A4_every_proxy_goes_to_its_crm_path_with_exactly_its_keys(string action, string method, string path, string[] keys)
    {
        foreach (var missing in keys)
        {
            var (deniedGateway, denied) = Arrange(keys.Where(k => k != missing).ToArray());
            Assert.Equal(403, Status(await Invoke(denied, action)));
            Assert.Empty(deniedGateway.Requests);
        }

        var (gateway, controller) = Arrange(keys);
        Assert.Equal(200, Status(await Invoke(controller, action)));
        var request = Assert.Single(gateway.Requests);
        Assert.Equal(method, request.Method);
        Assert.Equal(GatewayUrl + path, request.Uri.Split('?')[0]);
        Assert.Equal(TenantId.ToString(), request.Tenant);
        if (action == "Decide") Assert.Equal("{\"decision\":\"approve\",\"comment\":null}", request.Body);
        if (action == "Claims") Assert.Contains("productId=" + ProductId + "&status=approved&includeArchived=false", request.Uri);
    }

    [Fact]
    public async Task A4_a_return_without_a_note_and_a_tenant_id_body_are_refused_without_a_gateway_call()
    {
        var (gateway, controller) = Arrange(new[] { Read, Manage }, "{\"decision\":\"return\",\"comment\":\"  \"}");
        var result = await controller.Decide(Guid.Parse(Id), CancellationToken.None);
        Assert.Equal(400, Status(result));
        Assert.Contains(ObjectionResponsesController.DecisionCommentRequired, JsonSerializer.Serialize(((ObjectResult)result).Value));
        Assert.Equal(400, Status(await Arrange(new[] { Read }, "{\"decision\":\"publish\"}").Controller.Decide(Guid.Parse(Id), CancellationToken.None)));
        var (g2, c2) = Arrange(new[] { Manage }, "{\"tenantId\":\"x\",\"productId\":\"" + ProductId + "\"}");
        Assert.Equal(400, Status(await c2.CreateItem(CancellationToken.None)));
        Assert.Empty(gateway.Requests);
        Assert.Empty(g2.Requests);
    }

    [Fact]
    public async Task A4_the_list_maps_the_factory_request_to_crm_paging_and_asks_the_whole_total_when_filtered()
    {
        var (gateway, controller) = Arrange(new[] { Read }, query: "?start=20&length=10&search=fiyat&productId=" + ProductId + "&status=draft&categoryCode=price",
            reply: _ => "{\"data\":{\"items\":[{\"id\":\"1\"}],\"total\":7,\"page\":3,\"pageSize\":10}}");
        var ok = Assert.IsType<OkObjectResult>(await controller.List(CancellationToken.None));
        var data = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value)).RootElement.GetProperty("data");
        Assert.Equal(2, gateway.Requests.Count);
        Assert.Equal(GatewayUrl + "/api/crm/objection-responses?productId=" + ProductId + "&categoryCode=price&status=draft&q=fiyat&page=3&pageSize=10", gateway.Requests[0].Uri);
        Assert.Equal(GatewayUrl + "/api/crm/objection-responses?page=1&pageSize=1", gateway.Requests[1].Uri);
        Assert.Equal(1, data.GetProperty("items").GetArrayLength());
        Assert.Equal(7, data.GetProperty("filteredTotal").GetInt32());
        Assert.Equal(7, data.GetProperty("total").GetInt32());

        var (plain, plainController) = Arrange(new[] { Read }, query: "?start=0&length=10");
        await plainController.List(CancellationToken.None);
        Assert.Single(plain.Requests); // no filter → the page's total is the library's total
        Assert.Equal(400, Status(await Arrange(new[] { Read }, query: "?start=0&length=500").Controller.List(CancellationToken.None)));
    }

    // ═══ 5 · studio: the objection lookup + the step's notes ═════════════════════════════════════════════

    [Fact]
    public async Task A5_the_studio_lookup_offers_the_paths_product_approved_answers_with_brd_labels_and_reads_linked_ids()
    {
        const string pathId = "99999999-0000-0000-0000-000000000001";
        const string retired = "dddddddd-0000-0000-0000-000000000009";
        var gateway = new Gateway(uri =>
        {
            if (uri.Contains("/api/crm/knowledge/paths/" + pathId)) return "{\"data\":{\"pathId\":\"" + pathId + "\",\"derivedContext\":{\"productId\":\"" + ProductId + "\"}}}";
            if (uri.Contains("/api/crm/objection-responses?")) return "{\"data\":{\"items\":[{\"id\":\"o1\",\"productId\":\"" + ProductId + "\",\"status\":\"approved\",\"categoryCode\":\"price\",\"question\":\"Pahalı\",\"answer\":\"x\",\"claimIds\":[\"c1\",\"c2\"]}],\"total\":1}}";
            if (uri.Contains("/api/crm/objection-responses/" + retired)) return "{\"data\":{\"id\":\"" + retired + "\",\"productId\":\"" + ProductId + "\",\"status\":\"retired\",\"categoryCode\":\"other\",\"question\":\"Eski\",\"claimIds\":[]}}";
            if (uri.Contains("visit-objection-category/published-values")) return "{\"data\":{\"items\":[{\"valueCode\":\"price\",\"displayName\":\"Price\",\"attributes\":{\"label_tr\":\"Fiyat\"}},{\"valueCode\":\"other\",\"displayName\":\"Other\",\"attributes\":{}}]}}";
            return "{\"data\":null}";
        });
        using (new UiCulture("tr-TR"))
        {
            var controller = Studio(gateway, "crm.knowledge.path.read", Read);
            var json = JsonSerializer.Serialize(((JsonResult)await controller.PathObjectionLookup(Guid.Parse(pathId), retired + ",bogus", CancellationToken.None)).Value);
            var r = JsonDocument.Parse(json).RootElement;
            Assert.False(r.GetProperty("disabled").GetBoolean());
            Assert.Equal(ProductId, r.GetProperty("productId").GetString());
            var options = r.GetProperty("options");
            Assert.Equal(2, options.GetArrayLength());
            Assert.Equal("Fiyat", options[0].GetProperty("categoryLabel").GetString());
            Assert.Equal(2, options[0].GetProperty("claimCount").GetInt32());
            Assert.Equal("retired", options[1].GetProperty("status").GetString());
            Assert.Equal("Other", options[1].GetProperty("categoryLabel").GetString()); // no label_tr → the display name (data-owner item)
            Assert.Contains(gateway.Requests, x => x.Uri.Contains("productId=" + ProductId + "&status=approved"));
        }

        var noKey = Studio(new Gateway(_ => "{}"), "crm.knowledge.path.read");
        var denied = JsonDocument.Parse(JsonSerializer.Serialize(((JsonResult)await noKey.PathObjectionLookup(Guid.Parse(pathId), null, CancellationToken.None)).Value)).RootElement;
        Assert.True(denied.GetProperty("disabled").GetBoolean());
        Assert.Equal("ObjectionReadMissing", denied.GetProperty("reason").GetString());
    }

    [Fact]
    public void A5_the_studio_step_carries_its_complete_notes_and_the_path_product_and_the_tab_is_wired()
    {
        var method = typeof(KnowledgePathsController).GetMethod("Presentation", BindingFlags.NonPublic | BindingFlags.Static)!;
        using var doc = JsonDocument.Parse("{\"presentation\":{\"keyMessage\":\"K\",\"talkTrack\":\"T\",\"targetSeconds\":90,\"transition\":\" \",\"objectionResponseIds\":[\"o1\"]}}");
        var notes = JsonSerializer.Serialize(method.Invoke(null, [doc.RootElement]));
        Assert.Equal("{\"keyMessage\":\"K\",\"talkTrack\":\"T\",\"targetSeconds\":90,\"transition\":null,\"objectionResponseIds\":[\"o1\"]}", notes);
        using var empty = JsonDocument.Parse("{}");
        Assert.Null(method.Invoke(null, [empty.RootElement]));

        var studio = File.ReadAllText(Path.Combine(WebRoot(), "Controllers", "CRM", "KnowledgePathsController.Studio.cs"));
        Assert.Contains("presentation = Presentation(step)", studio);
        Assert.Contains("productId = derived is { } dp ? Str(dp, \"productId\") : null", studio);
        Assert.Contains("canReadObjections = HasAnyPermission(ObjectionReadPermission)", studio);

        var workspace = File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "KnowledgePaths", "Workspace.cshtml"));
        Assert.Contains("_PresentationNotes.cshtml", workspace);
        Assert.Contains("ObjectionResponses/objection-core.js", workspace);
        Assert.Contains("KnowledgePaths/presentation-notes.js", workspace);
        Assert.Contains("id=\"kpNotesBar\"", workspace);
        // the existing studio behaviour is untouched: the tab rides the workspace's own event, workspace.js is not edited
        Assert.DoesNotContain("presentation", Script("KnowledgePaths", "workspace.js").Replace("role=\"presentation\"", ""));
        var notesJs = Script("KnowledgePaths", "presentation-notes.js");
        Assert.Contains("document.addEventListener('kp:workspace'", notesJs);
        Assert.Contains("C.presentationBody(fields)", notesJs);
        Assert.Contains("_blank", notesJs); // "create in the library" opens a new tab after the unsaved warning
    }

    // ═══ 6 · seven languages ═════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void A6_every_key_is_filled_in_seven_languages_without_echo_and_with_the_same_arguments()
    {
        var keys = ObjectionResponsesIndex.ScriptKeys.Concat(ObjectionResponsesIndex.ViewKeys).ToArray();
        Assert.Equal(keys.Length, keys.Distinct().Count());
        var en = Resx(culture: "en");
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

        // every L.X the library scripts read is published, every Localizer[...] the views print is a key
        var scripts = string.Concat(new[] { "objection-list.js", "objection-edit.js", "objection-details.js", "objection-common.js" }.Select(f => Script("ObjectionResponses", f)));
        foreach (Match m in Regex.Matches(scripts, @"\bL\.([A-Z][A-Za-z_]+)"))
        {
            Assert.Contains(m.Groups[1].Value, ObjectionResponsesIndex.ScriptKeys);
        }

        foreach (var view in Directory.GetFiles(Path.Combine(WebRoot(), "Views", "CRM", "ObjectionResponses"), "*.cshtml").Select(File.ReadAllText))
        {
            foreach (Match m in Regex.Matches(view, @"(?<!Shared)Localizer\[""([^""]+)""\]"))
            {
                Assert.Contains(m.Groups[1].Value, keys);
            }
        }

        foreach (var culture in new[] { "fr", "es", "zh", "ar", "ru" })
        {
            var resx = Resx(culture);
            var echoes = keys.Where(k => resx[k] == en[k] && Regex.Replace(en[k], @"\{\d\}|[\s·:–\-,.()]", string.Empty).Length > 0).ToList();
            Assert.True(echoes.Count <= 3, culture + ": too many English echoes: " + string.Join(", ", echoes));
        }

        var tr = Resx("tr");
        Assert.Equal("İtiraz kütüphanesi", tr["PageTitle"]);
        Assert.Equal("Onay bekliyor", tr["Status_in_review"]);
        Assert.Equal("مكتبة الاعتراضات", Resx("ar")["PageTitle"]);

        // the studio tab: its keys in the seven studio resx files, and every t('Pn…') the script reads is one of them
        var studioEn = StudioResx("en");
        foreach (var culture in new[] { "en", "tr", "fr", "es", "zh", "ar", "ru" })
        {
            var resx = StudioResx(culture);
            foreach (var key in KnowledgePathStudio.PresentationKeys)
            {
                Assert.True(resx.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value), "studio " + culture + ":" + key);
                Assert.NotEqual(key, value);
                Assert.Equal(Args(studioEn[key]), Args(value));
            }
        }

        foreach (Match m in Regex.Matches(Script("KnowledgePaths", "presentation-notes.js") + File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "KnowledgePaths", "_PresentationNotes.cshtml")),
                     @"(?:\bt\(|Localizer\[)['""](Pn[A-Za-z]+|Err_path_step_[a-z_]+)['""]\s*[,)\]]"))
        {
            Assert.Contains(m.Groups[1].Value, KnowledgePathStudio.PresentationKeys);
        }

        Assert.Equal("Sunum notları: {0} / {1} adım dolu", StudioResx("tr")["PnBar"]);

        // RTL / dark: logical properties and theme variables only
        var css = Script("ObjectionResponses", "objection-responses.css");
        Assert.DoesNotMatch(@"(?<![-\w])(margin|padding)-(left|right)\s*:", css);
        Assert.DoesNotMatch(@"(?<![-\w])(left|right)\s*:", css);
        Assert.DoesNotMatch(@"#[0-9a-fA-F]{3,6}\b", css);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────

    private static string[] Args(string value) => Regex.Matches(value, @"\{\d\}").Select(m => m.Value).Distinct().OrderBy(x => x).ToArray();

    private static ObjectionResponsesPageViewModel Model(IActionResult result)
        => Assert.IsType<ObjectionResponsesPageViewModel>(Assert.IsType<ViewResult>(result).Model);

    private static int? Status(IActionResult result) => result switch
    {
        ContentResult c => c.StatusCode,
        ObjectResult o => o.StatusCode ?? 200,
        StatusCodeResult s => s.StatusCode,
        _ => null
    };

    private static Task<IActionResult> Invoke(ObjectionResponsesController c, string action) => action switch
    {
        "Get" => c.Get(Guid.Parse(Id), CancellationToken.None),
        "Usages" => c.Usages(Guid.Parse(Id), CancellationToken.None),
        "Create" => c.CreateItem(CancellationToken.None),
        "Update" => c.UpdateItem(Guid.Parse(Id), CancellationToken.None),
        "Submit" => c.SubmitItem(Guid.Parse(Id), CancellationToken.None),
        "Withdraw" => c.WithdrawItem(Guid.Parse(Id), CancellationToken.None),
        "Retire" => c.RetireItem(Guid.Parse(Id), CancellationToken.None),
        "Copy" => c.CopyItem(Guid.Parse(Id), CancellationToken.None),
        "Decide" => c.Decide(Guid.Parse(Id), CancellationToken.None),
        "Claims" => c.Claims(Guid.Parse(ProductId), null, CancellationToken.None),
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };

    private static (Gateway Gateway, ObjectionResponsesController Controller) Arrange(params string[] permissions)
        => Arrange(permissions, "{\"decision\":\"approve\",\"productId\":\"" + ProductId + "\"}");

    private static (Gateway Gateway, ObjectionResponsesController Controller) Arrange(
        string[] permissions, string? body = null, string? query = null, Func<string, string>? reply = null)
    {
        var gateway = new Gateway(reply ?? (_ => "{\"data\":{}}"));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = GatewayUrl }).Build();
        var controller = new ObjectionResponsesController(new HttpClient(gateway), configuration, NullLogger<ObjectionResponsesController>.Instance);
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body ?? "{\"decision\":\"approve\"}"));
        http.Request.ContentType = "application/json";
        if (query is not null) http.Request.QueryString = new QueryString(query);
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return (gateway, controller);
    }

    private static KnowledgePathsController Studio(Gateway gateway, params string[] permissions)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = GatewayUrl }).Build();
        var factory = new ResourceManagerStringLocalizerFactory(
            Options.Create(new LocalizationOptions { ResourcesPath = "Resources" }), NullLoggerFactory.Instance);
        var controller = new KnowledgePathsController(new HttpClient(gateway), configuration, new KeyLocalizer(),
            new StringLocalizer<KnowledgePathStudio>(factory), NullLogger<KnowledgePathsController>.Instance);
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary());
        return controller;
    }

    private static JsonElement? Smoke(string scenario)
    {
        var smoke = Path.Combine(Path.GetDirectoryName(WebRoot())!, "Diten.Web.Tests", "Js", "objection-library-smoke.js").Replace("\\", "/");
        var crm = Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM").Replace("\\", "/");
        var node = RunNode("process.argv[2] = '" + crm + "'; process.argv[3] = '" + scenario + "'; require('" + smoke + "');");
        return node is null ? null : JsonDocument.Parse(node).RootElement.Clone();
    }

    private static string Core()
        => "const C = require('" + Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", "ObjectionResponses", "objection-core.js").Replace("\\", "/") + "');\n";

    private static string? RunNode(string code)
    {
        var temp = Path.Combine(Path.GetTempPath(), "orw-" + Guid.NewGuid().ToString("N") + ".js");
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

    private static string Script(string folder, string file)
        => File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", folder, file)).Replace("\r\n", "\n");

    private static string View(string file)
        => File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "ObjectionResponses", file)).Replace("\r\n", "\n");

    private static Dictionary<string, string> Resx(string culture)
        => Load(Path.Combine(WebRoot(), "Resources", "Views", "CRM", "ObjectionResponses",
            "ObjectionResponsesIndex" + (culture.Length == 0 ? "" : "." + culture) + ".resx"));

    private static Dictionary<string, string> StudioResx(string culture)
        => Load(Path.Combine(WebRoot(), "Resources", "Views", "CRM", "KnowledgePaths", "KnowledgePathStudio." + culture + ".resx"));

    private static Dictionary<string, string> Load(string path)
        => XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")?.Value ?? string.Empty, StringComparer.Ordinal);

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }

    private sealed class Gateway(Func<string, string> reply) : HttpMessageHandler
    {
        public List<(string Method, string Uri, string? Tenant, string? Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!.ToString();
            Requests.Add((request.Method.Method, uri,
                request.Headers.TryGetValues("X-Tenant-Id", out var t) ? t.Single() : null,
                request.Content is null ? null : await request.Content.ReadAsStringAsync(ct)));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(reply(uri), Encoding.UTF8, "application/json") };
        }
    }

    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class UiCulture : IDisposable
    {
        private readonly CultureInfo _ui = CultureInfo.CurrentUICulture;
        private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

        public UiCulture(string name)
        {
            CultureInfo.CurrentUICulture = new CultureInfo(name);
            CultureInfo.CurrentCulture = new CultureInfo(name);
        }

        public void Dispose()
        {
            CultureInfo.CurrentUICulture = _ui;
            CultureInfo.CurrentCulture = _culture;
        }
    }
}
