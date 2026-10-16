using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Controllers.CRM;
using Diten.Web.Models.CRM;
using Diten.Web.Views.CRM.VisitEvidenceSettings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VW-W3 · W3-WEB-d — the visit evidence settings page on the REAL controller, view, resx and scripts: (1) UAS-001 and
/// the page flags; (2) the pure rules in Node (evidence-core.js): the day counter (both ends included, ≤ 30, no past start,
/// end required), the rule offer, the preview sentences, the save difference + PUT body, the status / phase chip, the
/// list counts, the drawer actions (SoD), the timeline, the refusal texts; (3) the page in a fake DOM (save confirm,
/// exception create → send for approval, the SoD button disabled, read-only hides the buttons, createError null); (4) every
/// proxy → its CRM path with its keys; (5) seven languages, no echo, placeholders, Arabic, RTL and dark tokens.
/// </summary>
public sealed class VisitEvidenceSettingsWebTests
{
    private const string GatewayUrl = "http://gateway.test";
    private static readonly Guid TenantId = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");
    private static readonly string[] Cultures = ["", "en", "tr", "fr", "es", "zh", "ar", "ru"];
    private static readonly string[] AllKeys = VisitEvidenceSettingsIndex.ScriptKeys.Concat(VisitEvidenceSettingsIndex.ViewKeys).Distinct().ToArray();
    private const string PolicyRead = "crm.visit-evidence-policy.read";
    private const string PolicyManage = "crm.visit-evidence-policy.manage";
    private const string ExcRead = "crm.visit-evidence-exception.read";
    private const string ExcManage = "crm.visit-evidence-exception.manage";
    private const string Id = "11111111-1111-1111-1111-111111111111";

    // ═══ 1 · UAS-001 + flags ═══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void A1_without_a_policy_read_key_the_page_draws_only_the_access_notice()
    {
        var view = ViewSource();
        Assert.True(view.IndexOf("@if (!Model.CanRead)", StringComparison.Ordinal) < view.IndexOf("id=\"ve-root\"", StringComparison.Ordinal));
        Assert.Contains("<partial name=\"_AccessDenied\"", view);

        Assert.False(Model(Arrange(ExcRead, ExcManage).Controller.Index()).CanRead); // exception keys alone never open the page
        var reader = Model(Arrange(PolicyRead).Controller.Index());
        Assert.True(reader.CanRead);
        Assert.False(reader.CanManagePolicy);
        var manager = Model(Arrange(PolicyManage, ExcRead, ExcManage).Controller.Index());
        Assert.True(manager.CanRead && manager.CanManagePolicy && manager.CanReadExceptions && manager.CanManageExceptions);
        Assert.Equal("user-1", manager.Me);

        // this round: the tenant default only; the legal entity layer is a disabled button with its hint
        Assert.Contains("class=\"ve-add\" disabled", view);
        Assert.Contains("Localizer[\"AddLegalEntityHint\"]", view);
        Assert.DoesNotContain("AddBusinessUnit", view);
        // WP-VW-W3E (W3E-WEB-b, E1) — the former "Additional evidence · Coming soon" card is replaced by the live section
        Assert.DoesNotContain("ve-soon", view);
        Assert.Contains("<div id=\"ve-extra\"></div>", view);
    }

    // ═══ 2 · the pure rules (Node) ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void A2_the_day_counter_includes_both_ends_and_an_exception_is_at_most_30_days_from_today()
    {
        var node = RunNode(Core() + """
            const t = '2026-10-12';
            const f = (o) => Object.assign({ resourceId: 'rep-1', rules: ['location_info'], startDate: t, endDate: '2026-10-12', reason: 'r' }, o);
            const keys = o => Object.fromEntries(Object.entries(C.exceptionErrors(f(o), t)).map(([k, v]) => [k, v.key + (v.args.length ? ':' + v.args.join(',') : '')]));
            process.stdout.write(JSON.stringify({
              same: C.daysInclusive(t, t), d30: C.daysInclusive(t, '2026-11-10'), d31: C.daysInclusive(t, '2026-11-11'), back: C.daysInclusive('2026-10-13', t), none: C.daysInclusive(t, ''),
              ok30: keys({ endDate: '2026-11-10' }), no31: keys({ endDate: '2026-11-11' }), past: keys({ startDate: '2026-10-11' }),
              noEnd: keys({ endDate: '' }), before: keys({ startDate: '2026-10-15', endDate: '2026-10-14' }),
              empty: keys({ resourceId: '', rules: [], reason: '  ' }), long: keys({ reason: 'x'.repeat(501) }), max: keys({ reason: 'x'.repeat(500) })
            }));
            """);
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement;
        Assert.Equal((1, 30, 31, 0, 0), (r.GetProperty("same").GetInt32(), r.GetProperty("d30").GetInt32(), r.GetProperty("d31").GetInt32(), r.GetProperty("back").GetInt32(), r.GetProperty("none").GetInt32()));
        Assert.Empty(r.GetProperty("ok30").EnumerateObject());
        Assert.Equal("XErrTooLong:31", r.GetProperty("no31").GetProperty("to").GetString());
        Assert.Equal("XErrPast", r.GetProperty("past").GetProperty("from").GetString());
        Assert.Equal("XErrToRequired", r.GetProperty("noEnd").GetProperty("to").GetString());
        Assert.Equal("XErrToBefore", r.GetProperty("before").GetProperty("to").GetString());
        var empty = r.GetProperty("empty");
        Assert.Equal(("XErrRep", "XErrRules", "XErrWhy"), (empty.GetProperty("rep").GetString(), empty.GetProperty("rules").GetString(), empty.GetProperty("why").GetString()));
        Assert.Equal("XErrWhyLong:500", r.GetProperty("long").GetProperty("why").GetString());
        Assert.Empty(r.GetProperty("max").EnumerateObject());
    }

    [Fact]
    public void A3_the_rules_are_offered_only_with_a_rep_and_only_when_they_would_relax_something()
    {
        var node = RunNode(Core() + """
            const fields = [{ field: 'locationMode', value: 'required', source: 'tenant' }, { field: 'photoMode', value: 'optional', source: 'tenant' },
                            { field: 'accountWithoutLocation', value: 'allow_flagged', source: 'exception' }];
            process.stdout.write(JSON.stringify(C.RULES.map(r => [C.ruleState(r, null), C.ruleState(r, fields)])));
            """);
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement.EnumerateArray().ToList();
        Assert.All(r, x => Assert.Equal(("XRuleNoRep", false), (x[0].GetProperty("reason").GetString(), x[0].GetProperty("enabled").GetBoolean())));
        Assert.True(r[0][1].GetProperty("enabled").GetBoolean());                       // location required → can relax to info
        Assert.Equal("XRuleAlready", r[1][1].GetProperty("reason").GetString());       // photo already optional
        Assert.Equal(("XRuleAlready", "exception"), (r[2][1].GetProperty("reason").GetString(), r[2][1].GetProperty("source").GetString()));
    }

    [Fact]
    public void A4_the_preview_sentences_follow_the_setting()
    {
        var node = RunNode(Core() + """
            const base = { locationMode: 'required', photoMode: 'required', radiusMeters: 300, maxAccuracyMeters: 150, accountWithoutLocation: 'allow_flagged', doctorConfirmation: true, durationConsistency: true, shortVisitSeconds: 120 };
            const keys = o => C.preview(Object.assign({}, base, o)).lines.map(l => l.key + (l.args.length ? ':' + l.args.join(',') : ''));
            process.stdout.write(JSON.stringify({
              both: keys({}), loc: keys({ photoMode: 'optional' }), photo: keys({ locationMode: 'info' }),
              none: keys({ locationMode: 'info', photoMode: 'optional', accountWithoutLocation: 'block', durationConsistency: false }),
              chips: C.preview(Object.assign({}, base, { locationMode: 'info', photoMode: 'optional', doctorConfirmation: false }))
            }));
            """);
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement;
        string[] A(string n) => r.GetProperty(n).EnumerateArray().Select(x => x.GetString()!).ToArray();
        Assert.Equal(new[] { "PvOpenBoth:300", "PvAccuracy:150", "PvNoLocationFlag", "PvShort:120" }, A("both"));
        Assert.Equal("PvOpenLocation:300", A("loc")[0]);
        Assert.Equal(new[] { "PvOpenPhoto:300", "PvNoLocationFlag", "PvShort:120" }, A("photo")); // no accuracy line under info
        Assert.Equal(new[] { "PvOpenNone:300", "PvNoLocationBlock" }, A("none"));               // no short line while duration is off
        var chips = r.GetProperty("chips");
        Assert.Equal(("PvOptional", "PvInfo", "info", false, false), (chips.GetProperty("photoKey").GetString(), chips.GetProperty("locationKey").GetString(),
            chips.GetProperty("locationTone").GetString(), chips.GetProperty("sign").GetBoolean(), chips.GetProperty("startDisabled").GetBoolean()));
    }

    [Fact]
    public void A5_the_save_difference_and_the_put_body_never_send_a_hidden_out_of_range_threshold()
    {
        var node = RunNode(Core() + """
            const p = { locationMode: 'required', radiusMeters: 300, maxAccuracyMeters: 150, photoMode: 'optional', doctorConfirmation: true, durationConsistency: true, shortVisitSeconds: 120, accountWithoutLocation: 'allow_flagged', version: 4 };
            const d = Object.assign(C.draftOf(p), { locationMode: 'info', radiusMeters: '500' });
            const off = Object.assign(C.draftOf(p), { durationConsistency: false, shortVisitSeconds: '5' });
            const offOk = Object.assign(C.draftOf(p), { durationConsistency: false, shortVisitSeconds: '90' });
            process.stdout.write(JSON.stringify({
              changes: C.changes(C.draftOf(p), d), none: C.changes(C.draftOf(p), C.draftOf(p)),
              errs: C.fieldErrors(Object.assign(C.draftOf(p), { radiusMeters: '49', maxAccuracyMeters: 'x' })), offErrs: C.fieldErrors(off),
              body: C.putBody(d, 4), offBody: C.putBody(off, 4), offOkBody: C.putBody(offOk, 4), firstBody: C.putBody(d, 0),
              values: [C.valueKey('locationMode', 'info'), C.valueKey('doctorConfirmation', false), C.valueKey('radiusMeters', 500), C.valueKey('shortVisitSeconds', 90), C.valueKey('accountWithoutLocation', 'block')]
            }));
            """);
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement;
        Assert.Equal(new[] { "locationMode", "radiusMeters" }, r.GetProperty("changes").EnumerateArray().Select(c => c.GetProperty("field").GetString()));
        Assert.Empty(r.GetProperty("none").EnumerateArray());
        Assert.Equal(new[] { "radiusMeters", "maxAccuracyMeters" }, r.GetProperty("errs").EnumerateObject().Select(p => p.Name));
        Assert.Empty(r.GetProperty("offErrs").EnumerateObject());
        var body = r.GetProperty("body");
        Assert.Equal(("info", 500, 4, 120), (body.GetProperty("locationMode").GetString(), body.GetProperty("radiusMeters").GetInt32(), body.GetProperty("expectedVersion").GetInt32(), body.GetProperty("shortVisitSeconds").GetInt32()));
        Assert.False(r.GetProperty("offBody").TryGetProperty("shortVisitSeconds", out _)); // out of range while hidden ⇒ not sent
        Assert.Equal(90, r.GetProperty("offOkBody").GetProperty("shortVisitSeconds").GetInt32());
        Assert.Equal(JsonValueKind.Null, r.GetProperty("firstBody").GetProperty("expectedVersion").ValueKind);
        Assert.Equal(new[] { "Opt_locationMode_info", "ValueOff", "UnitMeters", "UnitSeconds", "Opt_accountWithoutLocation_block" },
            r.GetProperty("values").EnumerateArray().Select(v => v.GetProperty("key").GetString()));
    }

    [Fact]
    public void A6_the_chip_the_counts_the_actions_and_the_timeline_follow_the_crm_state()
    {
        var node = RunNode(Core() + """
            const x = (status, phase, extra) => Object.assign({ status, phase, abilities: {} }, extra || {});
            const items = [x('draft'), x('in_review'), x('approved', 'active'), x('approved', 'planned'), x('approved', 'expired'), x('cancelled'), x('ended_early')];
            const list = { items, statusCounts: { in_review: 1 }, phaseCounts: { active: 1 } };
            const all = { canEdit: true, canSubmit: true, canWithdraw: true, canDecide: true, canCancel: true, canEndEarly: true };
            const own = x('in_review', null, { createdBy: 'me', abilities: all });
            const theirs = x('in_review', null, { createdBy: 'other', submittedBy: 'other', abilities: all });
            const tl = C.timeline({ status: 'cancelled', createdBy: 'a', createdAt: '2026-10-01T08:00:00Z', cancelledBy: 'c', cancelledAt: '2026-10-05T08:00:00Z', cancelReason: 'gerek kalmadı',
              reviewRounds: [{ roundNo: 1, submittedBy: 'a', submittedAt: '2026-10-01T09:00:00Z', outcome: 'rejected', closedAt: '2026-10-02T08:00:00Z' }, { roundNo: 2, submittedBy: 'a', submittedAt: '2026-10-03T09:00:00Z', outcome: 'approved', closedAt: '2026-10-04T08:00:00Z' }],
              decisions: [{ roundNo: 1, by: 'b', outcome: 'return', comment: 'tarih yanlış', at: '2026-10-02T08:00:00Z' }, { roundNo: 2, by: 'b', outcome: 'approve', at: '2026-10-04T08:00:00Z' }] });
            process.stdout.write(JSON.stringify({
              chips: items.map(i => C.chipOf(i).key), counts: C.FILTERS.map(f => C.filterCount(list, f.key)), badge: C.tabBadge(list),
              filtered: C.filterExceptions(items, 'planned').length,
              own: C.actionsOf(own, { manage: true }, 'ME'), theirs: C.actionsOf(theirs, { manage: true }, 'me'), viewer: C.actionsOf(theirs, { manage: false }, 'me'),
              tl: tl.map(e => e.key + (e.note ? ':' + e.note : '')),
              errs: [C.errorKey({ status: 409, body: { errors: ['x', 'visit_evidence_exception_overlap'] } }), C.errorKey({ status: 409, body: null }), C.errorKey({ status: 0 }), C.errorKey({ status: 403, body: null }), C.errorKey({ status: 400, body: { errors: ['zzz'] } })]
            }));
            """);
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement;
        Assert.Equal(new[] { "XSt_draft", "XSt_in_review", "XSt_active", "XSt_planned", "XSt_expired", "XSt_cancelled", "XSt_ended_early" },
            r.GetProperty("chips").EnumerateArray().Select(c => c.GetString()));
        Assert.Equal(new[] { 7, 1, 1, 1, 1, 1, 1, 1 }, r.GetProperty("counts").EnumerateArray().Select(c => c.GetInt32()));
        Assert.Equal(2, r.GetProperty("badge").GetInt32()); // in review + active
        Assert.Equal(1, r.GetProperty("filtered").GetInt32());
        var own = r.GetProperty("own");
        Assert.True(own.GetProperty("sod").GetBoolean() && own.GetProperty("approveDisabled").GetBoolean() && !own.GetProperty("decide").GetBoolean());
        var theirs = r.GetProperty("theirs");
        Assert.True(theirs.GetProperty("decide").GetBoolean() && !theirs.GetProperty("sod").GetBoolean() && theirs.GetProperty("submit").GetBoolean());
        var viewer = r.GetProperty("viewer");
        Assert.True(viewer.GetProperty("decide").GetBoolean()); // the decision is the Work Center task's, not the manage key
        Assert.False(viewer.GetProperty("submit").GetBoolean() || viewer.GetProperty("cancel").GetBoolean() || viewer.GetProperty("endEarly").GetBoolean() || viewer.GetProperty("edit").GetBoolean());
        Assert.Equal(new[] { "TlCreated", "TlSubmitted", "TlReturned:tarih yanlış", "TlSubmitted", "TlApproved", "TlCancelled:gerek kalmadı" },
            r.GetProperty("tl").EnumerateArray().Select(t => t.GetString()));
        Assert.Equal(new[] { "Err_visit_evidence_exception_overlap", "Err_conflict", "Err_unavailable", "Err_permission_denied", "Err_generic" },
            r.GetProperty("errs").EnumerateArray().Select(t => t.GetString()));
    }

    // ═══ 3 · the page in a fake DOM (Node) ═════════════════════════════════════════════════════════════════

    [Fact]
    public void A7_a_manager_saves_through_the_confirm_and_sends_an_exception_for_approval_the_own_one_cannot_be_approved()
    {
        var r = Smoke("manage");
        if (r is null) { return; }
        var o = r.Value;
        string S(string n) => o.GetProperty(n).GetString() ?? string.Empty;
        Assert.Equal(JsonValueKind.Null, o.GetProperty("createError").ValueKind);
        Assert.Equal("Tüm alanlar dolu · 148 temsilci", S("nodeMeta"));
        Assert.Contains("Konum kontrolü", S("groups"));
        Assert.Contains("Kısa ziyaret eşiği", S("groups"));
        Assert.False(o.GetProperty("saveBarHidden").GetBoolean());
        Assert.Contains("kuruma ⁨300⁩ m'den yakın", WebUtility.HtmlDecode(S("pvLines")));

        // a change → unsaved, Save on; the preview follows; the confirm lists the change and the reps
        Assert.True(o.GetProperty("unsavedShown").GetBoolean() && o.GetProperty("saveEnabled").GetBoolean());
        Assert.Contains("hemen açıktır", WebUtility.HtmlDecode(S("pvAfterChange")));
        Assert.True(o.GetProperty("saveModalShown").GetBoolean());
        Assert.Equal("Değişiklik hemen bu kapsamdaki ⁨148⁩ temsilciye uygulanır.", S("saveText"));
        Assert.Contains("<s class=\"ve-mu\">Zorunlu</s>", S("saveDiff"));
        Assert.Contains("<strong>Bilgi</strong>", S("saveDiff"));
        var put = o.GetProperty("put");
        Assert.Equal(("info", 4), (put.GetProperty("locationMode").GetString(), put.GetProperty("expectedVersion").GetInt32()));

        // exceptions: the shared tab card's corner count (in review + active), New opens, 31 days refused, 30 days created → sent
        Assert.Equal("⁨2⁩", S("badge"));
        Assert.Equal("badge rounded-pill bg-danger wcn-tab-count position-absolute top-0 start-100 translate-middle", S("badgeClass"));
        Assert.Equal("function,function", S("pageApi"));
        Assert.True(o.GetProperty("newShown").GetBoolean());
        Assert.Contains("Önce temsilci seçin.", S("rulesBeforeRep"));
        Assert.Contains("Şu an: Zorunlu · Kiracı", S("rulesWithRep"));
        Assert.Contains("data-rule=\"photo_optional\" disabled", S("rulesWithRep")); // already optional
        Assert.Equal("⁨31⁩ gün · en çok ⁨30⁩ gün", S("dayCounter31"));
        Assert.Equal("En çok 30 gün; seçilen aralık ⁨31⁩ gün.", S("tooLongError"));
        Assert.Equal(0, o.GetProperty("postsAfter31").GetInt32());
        var calls = o.GetProperty("submitCalls").EnumerateArray().ToList();
        Assert.Equal(new[] { "/api/exceptions", "/api/exceptions/x-new/submit" }, calls.Select(c => c[0].GetString()));
        var body = calls[0][1];
        Assert.Equal(("rep-1", "location_info", "Tablet GPS modülü arızalı"), (body.GetProperty("resourceId").GetString(), body.GetProperty("rules")[0].GetString(), body.GetProperty("reason").GetString()));
        Assert.Contains("İstisna onaya gönderildi.", o.GetProperty("submittedToast").EnumerateArray().Select(t => t.GetString()));
        Assert.True(o.GetProperty("detailShown").GetBoolean());

        // the caller's own exception in review: SoD hint + Approve disabled
        Assert.Contains("başka biri onaylamalı", S("ownBody"));
        Assert.Contains("data-xact=\"approve\" disabled", S("ownFoot"));
        Assert.DoesNotContain("data-xact=\"return\"", S("ownFoot"));
    }

    [Fact]
    public void A8_read_only_hides_save_and_new_and_disables_every_control()
    {
        var r = Smoke("readonly");
        if (r is null) { return; }
        var o = r.Value;
        Assert.Equal(JsonValueKind.Null, o.GetProperty("createError").ValueKind);
        Assert.True(o.GetProperty("saveBarHidden").GetBoolean());
        Assert.False(o.GetProperty("newShown").GetBoolean()); // without the manage key "New exception" opens nothing
        Assert.False(o.GetProperty("unsavedShown").GetBoolean());
        Assert.False(o.GetProperty("saveModalShown").GetBoolean());
        Assert.Equal(0, o.GetProperty("readonlyPuts").GetInt32());
        var groups = o.GetProperty("groups").GetString()!;
        Assert.Equal(Regex.Matches(groups, "data-(opt|switch|num)=").Count, Regex.Matches(groups, " disabled").Count);
    }

    // ═══ 4 · proxies ═══════════════════════════════════════════════════════════════════════════════════════

    public static TheoryData<string, string, string, string[]> ProxyMap => new()
    {
        { "GetPolicy", "GET", "/api/crm/visit-evidence-policy", new[] { PolicyRead } },
        { "GetPolicy", "GET", "/api/crm/visit-evidence-policy", new[] { PolicyManage } },
        { "PutPolicy", "PUT", "/api/crm/visit-evidence-policy", new[] { PolicyManage } },
        { "History", "GET", "/api/crm/visit-evidence-policy/history?page=1&pageSize=10", new[] { PolicyRead } },
        { "Roster", "GET", "/api/crm/visit-evidence-policy/reps/roster?page=1&pageSize=10", new[] { PolicyManage } },
        { "Effective", "GET", "/api/crm/visit-evidence-policy/effective?resourceId=rep-1", new[] { PolicyManage } },
        { "Reps", "GET", "/api/crm/visit-evidence-policy/reps?q=ok", new[] { PolicyRead } },
        { "ListExceptions", "GET", "/api/crm/visit-evidence-exceptions?status=draft", new[] { ExcRead } },
        { "GetException", "GET", "/api/crm/visit-evidence-exceptions/" + Id, new[] { ExcRead } },
        { "CreateException", "POST", "/api/crm/visit-evidence-exceptions", new[] { ExcManage } },
        { "UpdateException", "PUT", "/api/crm/visit-evidence-exceptions/" + Id, new[] { ExcManage } },
        { "SubmitException", "POST", "/api/crm/visit-evidence-exceptions/" + Id + "/submit", new[] { ExcManage } },
        { "WithdrawException", "POST", "/api/crm/visit-evidence-exceptions/" + Id + "/withdraw", new[] { ExcManage } },
        { "DecideException", "POST", "/api/crm/visit-evidence-exceptions/" + Id + "/decision", new[] { ExcRead } },
        { "CancelException", "POST", "/api/crm/visit-evidence-exceptions/" + Id + "/cancel", new[] { ExcManage } },
        { "EndExceptionEarly", "POST", "/api/crm/visit-evidence-exceptions/" + Id + "/end-early", new[] { ExcManage } }
    };

    [Theory]
    [MemberData(nameof(ProxyMap))]
    public async Task A9_each_proxy_reaches_its_crm_path_only_with_its_key(string action, string verb, string path, string[] keys)
    {
        var query = path.Contains('?') ? path[path.IndexOf('?')..] : string.Empty;
        var (gateway, controller) = Arrange(keys);
        controller.ControllerContext.HttpContext.Request.QueryString = new QueryString(query);
        Assert.Equal(200, Status(await Invoke(controller, action)));
        var sent = Assert.Single(gateway.Requests);
        Assert.Equal((verb, GatewayUrl + path, TenantId.ToString()), (sent.Method, sent.Uri, sent.Tenant));

        // every other key of the page is not enough
        var others = new[] { PolicyRead, PolicyManage, ExcRead, ExcManage }.Where(k => !keys.Contains(k)
            && !(action is "GetPolicy" or "History" or "Roster" or "Effective" or "Reps" && k is PolicyRead or PolicyManage)).ToArray();
        var (none, refused) = Arrange(others);
        refused.ControllerContext.HttpContext.Request.QueryString = new QueryString(query);
        Assert.Equal(403, Status(await Invoke(refused, action)));
        Assert.Empty(none.Requests);
    }

    [Fact]
    public async Task A9_a_body_naming_the_tenant_is_refused()
    {
        var (gateway, controller) = Arrange([ExcManage], "{\"tenantId\":\"x\",\"resourceId\":\"rep-1\"}");
        Assert.Equal(400, Status(await controller.CreateException(CancellationToken.None)));
        Assert.Empty(gateway.Requests);
    }

    // ═══ 5 · seven languages, RTL, dark ════════════════════════════════════════════════════════════════════

    [Fact]
    public void A10_every_key_in_seven_languages_without_echo_with_its_placeholders_and_every_code_the_core_names()
    {
        var english = Resx("en");
        foreach (var culture in Cultures)
        {
            var resx = Resx(culture);
            foreach (var key in AllKeys)
            {
                Assert.True(resx.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value), $"{culture}:{key}");
                Assert.NotEqual(key, value);
                var expected = Regex.Matches(english[key], "\\{\\d\\}").Select(m => m.Value).OrderBy(x => x).ToArray();
                Assert.Equal(expected, Regex.Matches(value!, "\\{\\d\\}").Select(m => m.Value).OrderBy(x => x).ToArray());
            }
        }

        foreach (var culture in new[] { "tr", "fr", "es", "zh", "ar", "ru" })
        {
            var resx = Resx(culture);
            var same = AllKeys.Count(k => resx[k] == english[k] && !resx[k].Contains('{'));
            // French shares real cognates here (Exception, Photo, Date, Information, Active) and the unit symbols m / s
            Assert.True(same <= 12, $"{culture}: {same} values identical to English");
        }

        Assert.Equal("إثبات الزيارة", Resx("ar")["PageTitle"]);
        Assert.All(new[] { "TabExceptions", "XSod", "PvOpenBoth", "Err_visit_evidence_exception_overlap" }, k => Assert.Matches("[؀-ۿ]", Resx("ar")[k]));
        Assert.Equal("Görev ayrılığı: bu istisnayı siz onaylayamazsınız. Gönderdiğinizde başka biri Görev Merkezi'nde onaylar ve tarih aralığında uygulanır.", Resx("tr")["XSodNew"]);

        // the view writes only declared view keys; the script reads only declared script keys
        var view = ViewSource();
        Assert.Contains("VisitEvidenceSettingsIndex.ScriptKeys.ToDictionary(k => k, k => Localizer[k].Value)", view);
        foreach (Match m in Regex.Matches(view, "Localizer\\[\"([A-Za-z0-9_]+)\"\\]")) { Assert.Contains(m.Groups[1].Value, VisitEvidenceSettingsIndex.ViewKeys); }
        foreach (Match m in Regex.Matches(Script("visit-evidence-settings.js"), "\\bL\\.([A-Za-z0-9_]+)")) { Assert.Contains(m.Groups[1].Value, VisitEvidenceSettingsIndex.ScriptKeys); }

        // every key the pure rules produce exists
        var node = RunNode(Core() + """
            const keys = [];
            C.FIELDS.forEach(f => keys.push('F_' + f));
            Object.keys(C.OPTIONS).forEach(f => C.OPTIONS[f].forEach(v => keys.push('Opt_' + f + '_' + v)));
            Object.keys(C.RANGES).forEach(f => keys.push('R_' + f));
            C.GROUPS.forEach(g => keys.push('G_' + g.key));
            C.RULES.forEach(r => keys.push('XRule_' + r));
            C.FILTERS.forEach(f => keys.push('XFilter_' + f.key));
            C.ERROR_CODES.forEach(c => keys.push('Err_' + c));
            ['draft','in_review','cancelled','ended_early'].forEach(s => keys.push(C.chipOf({ status: s }).key));
            ['active','planned','expired'].forEach(p => keys.push(C.chipOf({ status: 'approved', phase: p }).key));
            [{}, { locationMode: 'required', photoMode: 'required', accountWithoutLocation: 'block', durationConsistency: true }, { locationMode: 'required' }, { photoMode: 'required' }].forEach(d => {
              const p = C.preview(d); keys.push(p.photoKey, p.locationKey, p.locationTextKey); p.lines.forEach(l => keys.push(l.key)); });
            Object.values(C.exceptionErrors({ reason: 'x'.repeat(501) }, '2099-01-01')).forEach(e => keys.push(e.key));
            Object.values(C.exceptionErrors({ startDate: '2000-01-01', endDate: '1999-01-01', reason: '' }, '2099-01-01')).forEach(e => keys.push(e.key));
            Object.values(C.exceptionErrors({ startDate: '2099-01-01', endDate: '2099-03-01', reason: 'x' }, '2099-01-01')).forEach(e => keys.push(e.key));
            ['XRuleNoRep','XRuleAlready','XRuleNow','TlCreated','TlSubmitted','TlApproved','TlReturned','TlWithdrawn','TlTimedOut','TlCancelled','TlEndedEarly',
             'TlRefused_visit_evidence_exception_sod','TlRefused_visit_evidence_exception_overlap','Err_conflict','Err_unavailable','Err_generic','ValueOn','ValueOff','UnitMeters','UnitSeconds'].forEach(k => keys.push(k));
            process.stdout.write(JSON.stringify(Array.from(new Set(keys))));
            """);
        if (node is null) { return; }
        foreach (var key in JsonDocument.Parse(node).RootElement.EnumerateArray().Select(k => k.GetString()!))
        {
            Assert.Contains(key, VisitEvidenceSettingsIndex.ScriptKeys);
        }
    }

    [Fact]
    public void A11_names_are_isolated_the_layout_mirrors_in_arabic_and_the_tokens_have_a_dark_set()
    {
        var js = Script("visit-evidence-settings.js");
        Assert.Contains("F.bidi(x.repDisplayName || x.resourceId)", js);
        Assert.Contains("F.bidi(x.reason || '')", js);
        Assert.Contains("F.ratio(f.reason.length, C.MAX_REASON)", js);
        Assert.DoesNotContain("toLocaleDateString", js);
        Assert.DoesNotContain("localStorage", js);
        Assert.DoesNotContain("campaign", js, StringComparison.OrdinalIgnoreCase);

        var css = File.ReadAllText(Path.Combine(ScriptDir(), "visit-evidence-settings.css"));
        Assert.Contains("[dir=\"rtl\"] .ve-flip", css);
        Assert.Contains("[data-bs-theme=\"dark\"] #ve-root", css);
        foreach (var physical in new[] { "margin-left", "margin-right", "padding-left", "padding-right", " left:", " right:", "text-align: left", "text-align: right" })
        {
            Assert.DoesNotContain(physical, css);
        }

        // the menu label (CT, W3-BE-c) is there in every language
        foreach (var culture in new[] { "en", "tr", "fr", "es", "zh", "ar", "ru" })
        {
            var shared = XDocument.Load(Path.Combine(WebRoot(), "Resources", $"SharedResource.{culture}.resx")).Root!
                .Elements("data").ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")?.Value ?? "");
            Assert.True(shared.TryGetValue("Nav.Page.VISITEVIDENCESETTINGS", out var nav) && !string.IsNullOrWhiteSpace(nav), culture);
        }
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────

    private JsonElement? Smoke(string mode)
    {
        var smoke = Path.Combine(Path.GetDirectoryName(WebRoot())!, "Diten.Web.Tests", "Js", "visit-evidence-settings-smoke.js").Replace("\\", "/");
        var crm = Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM").Replace("\\", "/");
        var tr = Resx("tr");
        var texts = Path.Combine(Path.GetTempPath(), "ve-tr-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(texts, JsonSerializer.Serialize(VisitEvidenceSettingsIndex.ScriptKeys.ToDictionary(k => k, k => tr[k])), new UTF8Encoding(false));
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

    private static VisitEvidenceSettingsIndexViewModel Model(IActionResult result)
        => Assert.IsType<VisitEvidenceSettingsIndexViewModel>(Assert.IsType<ViewResult>(result).Model);

    private static int? Status(IActionResult result) => result switch
    {
        ContentResult c => c.StatusCode,
        ObjectResult o => o.StatusCode ?? 200,
        StatusCodeResult s => s.StatusCode,
        _ => null
    };

    private static Task<IActionResult> Invoke(VisitEvidenceSettingsController c, string action) => action switch
    {
        "GetPolicy" => c.GetPolicy(CancellationToken.None),
        "PutPolicy" => c.PutPolicy(CancellationToken.None),
        "History" => c.History(null, null, null, null, null, CancellationToken.None),
        "Roster" => c.Roster(null, null, null, null, null, CancellationToken.None),
        "Effective" => c.Effective(CancellationToken.None),
        "Reps" => c.Reps(CancellationToken.None),
        "ListExceptions" => c.ListExceptions(CancellationToken.None),
        "GetException" => c.GetException(Guid.Parse(Id), CancellationToken.None),
        "CreateException" => c.CreateException(CancellationToken.None),
        "UpdateException" => c.UpdateException(Guid.Parse(Id), CancellationToken.None),
        "SubmitException" => c.SubmitException(Guid.Parse(Id), CancellationToken.None),
        "WithdrawException" => c.WithdrawException(Guid.Parse(Id), CancellationToken.None),
        "DecideException" => c.DecideException(Guid.Parse(Id), CancellationToken.None),
        "CancelException" => c.CancelException(Guid.Parse(Id), CancellationToken.None),
        "EndExceptionEarly" => c.EndExceptionEarly(Guid.Parse(Id), CancellationToken.None),
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };

    private static (Gateway Gateway, VisitEvidenceSettingsController Controller) Arrange(params string[] permissions)
        => Arrange(permissions, "{\"reason\":\"x\"}");

    private static (Gateway Gateway, VisitEvidenceSettingsController Controller) Arrange(string[] permissions, string body)
    {
        var gateway = new Gateway();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = GatewayUrl }).Build();
        var controller = new VisitEvidenceSettingsController(new HttpClient(gateway), configuration, NullLogger<VisitEvidenceSettingsController>.Instance);
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()), new("sub", "user-1") };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return (gateway, controller);
    }

    private static string Core() => "const C = require('" + Path.Combine(ScriptDir(), "evidence-core.js").Replace("\\", "/") + "');\n";

    /// <summary>Runs <paramref name="code"/> in Node; null when Node is not on the machine (the code assertions stand alone).</summary>
    private static string? RunNode(string code)
    {
        var temp = Path.Combine(Path.GetTempPath(), "ve-" + Guid.NewGuid().ToString("N") + ".js");
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

    private static string ViewSource() => File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "VisitEvidenceSettings", "Index.cshtml")).Replace("\r\n", "\n");

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

    private sealed class Gateway : HttpMessageHandler
    {
        public List<(string Method, string Uri, string? Tenant)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request.Method.Method, request.RequestUri!.ToString(), request.Headers.TryGetValues("X-Tenant-Id", out var t) ? t.Single() : null));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"data\":{\"items\":[],\"total\":0}}", Encoding.UTF8, "application/json") });
        }
    }
}
