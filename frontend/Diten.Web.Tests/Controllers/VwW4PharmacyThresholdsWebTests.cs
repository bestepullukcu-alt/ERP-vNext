using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Controllers.CRM;
using Diten.Web.Models.CRM;
using Diten.Web.Views.CRM.PharmacyReport;
using Diten.Web.Views.CRM.PotentialThresholds;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VW-W4 · W4-WEB-b — the pharmacy report and the potential thresholds pages on the REAL controllers, views, resx and
/// scripts: (1) UAS-001 and the page flags; (2) every proxy → its CRM / MDM path with its keys (tenantId refused, the
/// threshold target checked, the shelf photo multipart buffered with its boundary); (3) the pure rules in Node — the
/// pharmacy missing answers (the TREND is required, boxes / month optional), the step map, the request body, the
/// threshold validation (0 &lt; Low ≤ High ≤ 100000), the save difference, the band class; (4) both pages in a fake DOM
/// (pharmacy submit, photo, passive order, read-only, the 60-minute correction; threshold save + history, read-only);
/// (5) seven languages, no echo, placeholders, Arabic, RTL and dark tokens, the menu label, the extension point.
/// </summary>
public sealed partial class VwW4PharmacyThresholdsWebTests
{
    private const string GatewayUrl = "http://gateway.test";
    private static readonly Guid TenantId = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");
    private static readonly string[] Cultures = ["", "en", "tr", "fr", "es", "zh", "ar", "ru"];
    private const string Id = "11111111-1111-1111-1111-111111111111";

    private const string ReportRead = "crm.visit-report.read";
    private const string ReportRecord = "crm.visit-report.record";
    private const string ReportAmend = "crm.visit-report.amend";
    private const string PlanManage = "crm.planned-visit.manage";
    private const string GskuRead = "mdm.gskus.read";
    private const string ContactRead = "crm.contact.read";
    private const string InsightRead = "crm.doctor-insights.read";
    private const string InsightReadAll = "crm.doctor-insights.read-all";
    private const string ThresholdManage = "crm.doctor-insights.thresholds.manage";
    private const string ProductRead = "mdm.global-products.read";

    // ═══ 1 · UAS-001 + flags ═══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void B1_pharmacy_report_without_the_read_key_draws_only_the_access_notice()
    {
        var view = View("PharmacyReport");
        Assert.True(view.IndexOf("@if (!Model.CanRead)", StringComparison.Ordinal) < view.IndexOf("id=\"ph-root\"", StringComparison.Ordinal));
        Assert.Contains("<partial name=\"_AccessDenied\"", view);
        Assert.DoesNotContain("Redirect", view);

        Assert.False(PharmacyModel(Pharmacy(ReportRecord, PlanManage).Index(Guid.Parse(Id))).CanRead); // write keys alone never open it
        var reader = PharmacyModel(Pharmacy(ReportRead).Index(Guid.Parse(Id)));
        Assert.True(reader.CanRead);
        Assert.False(reader.CanRecord || reader.CanAmend || reader.CanReadGskus || reader.CanReadContacts);
        Assert.False(PharmacyModel(Pharmacy(ReportRead, ReportRecord).Index(Guid.Parse(Id))).CanRecord); // record needs the plan key too
        var rep = PharmacyModel(Pharmacy(ReportRead, ReportRecord, PlanManage, ReportAmend, GskuRead, ContactRead).Index(Guid.Parse(Id)));
        Assert.True(rep.CanRecord && rep.CanAmend && rep.CanReadGskus && rep.CanReadContacts);
        Assert.Equal(Guid.Parse(Id), rep.PlannedVisitId);
    }

    [Fact]
    public void B2_thresholds_page_opens_with_any_doctor_insights_read_key_and_writes_only_with_manage()
    {
        var view = View("PotentialThresholds");
        Assert.True(view.IndexOf("@if (!Model.CanRead)", StringComparison.Ordinal) < view.IndexOf("id=\"pt-root\"", StringComparison.Ordinal));
        Assert.Contains("<partial name=\"_AccessDenied\"", view);

        Assert.False(ThresholdModel(Thresholds(ProductRead).Index()).CanRead);
        foreach (var key in new[] { InsightRead, InsightReadAll, ThresholdManage })
        {
            Assert.True(ThresholdModel(Thresholds(key).Index()).CanRead, key);
        }

        Assert.False(ThresholdModel(Thresholds(InsightRead).Index()).CanManage);
        Assert.True(ThresholdModel(Thresholds(ThresholdManage).Index()).CanManage);
        // the add action (the toolbar's Add New since W3E-E4R2-FIX-c) exists only with manage + the product picker key
        Assert.Contains("data-can-add=\"@((Model.CanManage && Model.CanReadProducts).ToString().ToLowerInvariant())\"", view);
        Assert.Contains("addNewText: canAdd ? L.AddProduct : ''", Script("PotentialThresholds", "index.js"));
        // the menu entry (BE-c) points here
        Assert.Equal("CRM/PotentialThresholds", typeof(PotentialThresholdsController).GetCustomAttribute<RouteAttribute>()!.Template);
    }

    // ═══ 2 · the proxies ════════════════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("Prefill", "GET", "/api/crm/visit-report/v2/prefill?plannedVisitId=" + Id, new[] { ReportRead })]
    [InlineData("Draft", "PUT", "/api/crm/visit-report/v2/draft", new[] { ReportRecord, PlanManage })]
    [InlineData("Submit", "POST", "/api/crm/visit-report/v2/submit", new[] { ReportRecord, PlanManage })]
    [InlineData("AmendReport", "POST", "/api/crm/visit-report/" + Id + "/amend", new[] { ReportAmend })]
    [InlineData("ShelfPhotoImage", "GET", "/api/crm/visit-report/v2/" + Id + "/shelf-photo", new[] { ReportRead })]
    public async Task B3_every_pharmacy_proxy_reaches_its_crm_path_with_its_keys(string action, string method, string path, string[] keys)
    {
        var (gateway, controller) = PharmacyArrange(keys, "{\"plannedVisitId\":\"" + Id + "\"}");
        var result = await InvokePharmacy(controller, action);
        Assert.NotEqual(403, Status(result));
        var call = Assert.Single(gateway.Requests);
        Assert.Equal(method, call.Method);
        Assert.Equal(GatewayUrl + path, call.Uri);
        Assert.Equal(TenantId.ToString(), call.Tenant);

        // one key short → 403 and nothing sent
        foreach (var missing in keys)
        {
            var (g2, c2) = PharmacyArrange(keys.Where(k => k != missing).ToArray(), "{}");
            Assert.Equal(403, Status(await InvokePharmacy(c2, action)));
            Assert.Empty(g2.Requests);
        }
    }

    [Fact]
    public async Task B4_a_body_naming_tenantId_is_refused_and_the_version_travels_as_if_match()
    {
        var (gateway, controller) = PharmacyArrange([ReportRecord, PlanManage], "{\"tenantId\":\"x\",\"plannedVisitId\":\"" + Id + "\"}");
        Assert.Equal(400, Status(await controller.Draft(CancellationToken.None)));
        Assert.Empty(gateway.Requests);

        var (g2, c2) = PharmacyArrange([ReportRecord, PlanManage], "{\"plannedVisitId\":\"" + Id + "\"}");
        c2.ControllerContext.HttpContext.Request.Headers.IfMatch = "\"4\"";
        await c2.Submit(CancellationToken.None);
        Assert.Equal("\"4\"", Assert.Single(g2.Requests).IfMatch);
    }

    [Fact]
    public async Task B5_the_shelf_photo_proxy_buffers_the_multipart_body_with_its_boundary_and_needs_the_write_keys()
    {
        var method = typeof(PharmacyReportController).GetMethod(nameof(PharmacyReportController.ShelfPhoto))!;
        Assert.NotNull(method.GetCustomAttribute<Diten.Web.Filters.DisableFormValueModelBindingAttribute>());
        Assert.NotNull(method.GetCustomAttribute<RequestSizeLimitAttribute>());
        Assert.Equal(11L * 1024 * 1024, PharmacyReportController.MaxPhotoRequestBytes); // a 10 MB photo + the envelope (the CRM's ceiling)

        const string boundary = "----w4b";
        var body = "--" + boundary + "\r\nContent-Disposition: form-data; name=\"file\"; filename=\"s.jpg\"\r\nContent-Type: image/jpeg\r\n\r\nJPEGBYTES\r\n--" + boundary + "--\r\n";
        var (gateway, controller) = PharmacyArrange([ReportRecord, PlanManage], body);
        controller.ControllerContext.HttpContext.Request.ContentType = "multipart/form-data; boundary=" + boundary;
        await controller.ShelfPhoto(Guid.Parse(Id), CancellationToken.None);
        var call = Assert.Single(gateway.Requests);
        Assert.Equal("POST", call.Method);
        Assert.Equal(GatewayUrl + "/api/crm/visit-report/v2/" + Id + "/shelf-photo", call.Uri);
        Assert.Equal("multipart/form-data; boundary=" + boundary, call.ContentType);
        Assert.Equal(body, call.Body);

        // not multipart → 400, nothing sent; record without the plan key → 403
        var (g2, c2) = PharmacyArrange([ReportRecord, PlanManage], "{}");
        c2.ControllerContext.HttpContext.Request.ContentType = "application/json";
        Assert.Equal(400, Status(await c2.ShelfPhoto(Guid.Parse(Id), CancellationToken.None)));
        Assert.Empty(g2.Requests);
        var (g3, c3) = PharmacyArrange([ReportRecord, ReportRead], body);
        c3.ControllerContext.HttpContext.Request.ContentType = "multipart/form-data; boundary=" + boundary;
        Assert.Equal(403, Status(await c3.ShelfPhoto(Guid.Parse(Id), CancellationToken.None)));
        Assert.Empty(g3.Requests);
    }

    [Fact]
    public async Task B6_the_lookups_read_mdm_and_crm_with_their_own_keys_and_map_the_rows()
    {
        var (gateway, controller) = PharmacyArrange([ReportRead, GskuRead], "",
            "{\"data\":{\"items\":[{\"id\":\"g1\",\"canonicalCode\":\"URO-30\",\"globalProductId\":\"p1\",\"globalProductName\":\"Ürofleks\",\"packQuantity\":30,\"packUomCode\":\"TAB\"}],\"totalCount\":1}}");
        var ok = Assert.IsType<OkObjectResult>(await controller.Gskus("uro", CancellationToken.None));
        Assert.Equal(GatewayUrl + "/api/gskus?pageNumber=1&pageSize=100&search=uro", Assert.Single(gateway.Requests).Uri);
        var json = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"productId\":\"p1\"", json);
        Assert.Contains("\"pack\":\"30 TAB\"", json);
        Assert.Equal(403, Status(await PharmacyArrange([ReportRead], "").Controller.Gskus(null, CancellationToken.None)));

        var (g2, c2) = PharmacyArrange([ReportRead, ContactRead], "", "{\"data\":[{\"id\":\"" + Id + "\",\"displayName\":\"Dr. Ayşe Kaya\"}]}");
        Assert.Contains("Dr. Ayşe Kaya", JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(await c2.Contacts("ay", CancellationToken.None)).Value,
            new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        // the search first; W4-FIX-c (G-10) then completes each hit from the contact read (+ the BRD labels) — see FixC_G10
        Assert.Equal(GatewayUrl + "/api/crm/contacts/search?search=ay&limit=10", g2.Requests[0].Uri);
        var (g3, c3) = PharmacyArrange([ReportRead, ContactRead], "");
        await c3.Contacts("a", CancellationToken.None); // < 2 letters: no call
        Assert.Empty(g3.Requests);
    }

    [Theory]
    [InlineData("Thresholds", "GET", "/api/crm/doctor-insights/thresholds")]
    [InlineData("History", "GET", "/api/crm/doctor-insights/thresholds/history?page=1&pageSize=10")]
    public async Task B7_the_threshold_reads_open_with_any_read_key(string action, string method, string path)
    {
        foreach (var key in new[] { InsightRead, InsightReadAll, ThresholdManage })
        {
            var (gateway, controller) = ThresholdArrange([key], "");
            await (action == "Thresholds" ? controller.Thresholds(CancellationToken.None) : controller.History(null, null, null, null, null, CancellationToken.None));
            var call = Assert.Single(gateway.Requests);
            Assert.Equal(method, call.Method);
            Assert.Equal(GatewayUrl + path, call.Uri);
        }

        var (g2, c2) = ThresholdArrange([ProductRead], "");
        Assert.Equal(403, Status(await (action == "Thresholds" ? c2.Thresholds(CancellationToken.None) : c2.History(null, null, null, null, null, CancellationToken.None))));
        Assert.Empty(g2.Requests);
    }

    [Fact]
    public async Task B8_the_threshold_save_needs_manage_and_a_product_id_or_default()
    {
        var (gateway, controller) = ThresholdArrange([ThresholdManage], "{\"lowBelow\":5,\"highAbove\":20,\"expectedVersion\":2}");
        await controller.Save("default", CancellationToken.None);
        await controller.Save(Id, CancellationToken.None);
        Assert.Equal(new[] { GatewayUrl + "/api/crm/doctor-insights/thresholds/default", GatewayUrl + "/api/crm/doctor-insights/thresholds/" + Id },
            gateway.Requests.Select(r => r.Uri).ToArray());
        Assert.All(gateway.Requests, r => Assert.Equal("PUT", r.Method));

        foreach (var bad in new[] { "DEFAULT", "x", "../history", Guid.Empty.ToString() })
        {
            var (g, c) = ThresholdArrange([ThresholdManage], "{}");
            Assert.Equal(400, Status(await c.Save(bad, CancellationToken.None)));
            Assert.Empty(g.Requests);
        }

        var (g2, c2) = ThresholdArrange([InsightRead, InsightReadAll], "{\"lowBelow\":5,\"highAbove\":20}");
        Assert.Equal(403, Status(await c2.Save("default", CancellationToken.None)));
        var (g3, c3) = ThresholdArrange([ThresholdManage], "{\"tenantId\":\"x\",\"lowBelow\":5,\"highAbove\":20}");
        Assert.Equal(400, Status(await c3.Save("default", CancellationToken.None)));
        Assert.Empty(g2.Requests);
        Assert.Empty(g3.Requests);
    }

    // ═══ 3 · the pure rules (Node) ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void B9_pharmacy_missing_answers_mirror_the_crm_and_the_trend_is_required_while_boxes_are_optional()
    {
        var node = RunNode(PharmacyCore() + """
            const full = { general: { visitKindCode: 'in_person', metRoleCode: 'pharmacist' },
              pharmacy: { stock: [{ gskuId: 'G1', levelCode: 'in_stock' }], sales: [{ gskuId: 'G1', trendCode: 'up', boxesPerMonth: null }], competitors: [], noCompetitor: true },
              safety: { adverseEvent: false } };
            const clone = o => JSON.parse(JSON.stringify(o));
            const noTrend = clone(full); noTrend.pharmacy.sales[0].trendCode = null;
            const noLevel = clone(full); noLevel.pharmacy.stock[0].levelCode = '';
            const aeYes = clone(full); aeYes.safety.adverseEvent = true;
            const aeOpen = clone(full); aeOpen.safety.adverseEvent = null;
            const noRole = clone(full); noRole.general.metRoleCode = null;
            const noKind = clone(full); noKind.general.visitKindCode = 'phone';
            const noComp = clone(full); noComp.pharmacy.noCompetitor = false;
            const f = (b, listed) => C.missing(b, { competitorsListed: listed }).map(m => m.section + ':' + (m.productId || '') + ':' + m.field);
            console.log(JSON.stringify({
              full: f(full, true), noTrend: f(noTrend, true), noLevel: f(noLevel, true), aeYes: f(aeYes, true), aeOpen: f(aeOpen, true),
              noRole: f(noRole, true), noKind: f(noKind, true), noCompListed: f(noComp, true), noCompUnlisted: f(noComp, false),
              steps: [C.stepOf({ section: 'pharmacy', field: 'sales.trendCode' }), C.stepOf({ section: 'pharmacy', field: 'stock.levelCode' }),
                C.stepOf({ section: 'pharmacy', field: 'competitors' }), C.stepOf({ section: 'safety', field: 'adverseEvent' }), C.stepOf({ section: 'general', field: 'metRoleCode' })],
              boxes: [C.boxesOf(''), C.boxesOf('12'), C.boxesOf('-1'), C.boxesOf('1.5'), C.boxesOf('100001')],
              listed: [C.competitorsListed({ competitors: [{ productId: 'P1', available: true, items: [] }] }), C.competitorsListed({ competitors: [{ productId: 'P1', available: false }] }), C.competitorsListed({ competitors: [] })],
              body: C.requestBody('PV1', { general: { metContactId: 'x' }, pharmacy: {}, safety: { offLabelRequest: true } }, 3),
              seeded: C.seedRows({ stock: [{ gskuId: 'G1', levelCode: 'low' }], sales: [] }, [{ id: 'G1', productId: 'P1' }, { id: 'G2', productId: 'P1' }, { id: 'G9', productId: 'P9' }], ['P1']),
              mode: [C.modeOf({ reportStatus: null }), C.modeOf({ reportStatus: 'draft' }), C.modeOf({ reportStatus: 'submitted' }), C.modeOf({ reportStatus: 'amended' })],
              proposal: [C.proposalErrors('', ''), C.proposalErrors('Tensilox', 'X')],
              adv: [C.cycleAdvantage({ advantageCodes: [], disadvantageCodes: [] }, 'price'), C.cycleAdvantage({ advantageCodes: ['price'], disadvantageCodes: [] }, 'price'), C.cycleAdvantage({ advantageCodes: [], disadvantageCodes: ['price'] }, 'price')],
              err: [C.errorKey({ status: 409, body: { errors: ['x', 'visit_report_edit_window_closed'] } }), C.errorKey({ status: 400, body: { errors: ['shelf_photo_pharmacy_only'] } }), C.errorKey({ status: 0 })],
              amend: C.isAmendRefusal({ status: 409, body: { errors: ['visit_report_edit_window_closed'] } })
            }));
            """);
        if (node is null) return;
        var r = JsonDocument.Parse(node).RootElement;
        Assert.Empty(r.GetProperty("full").EnumerateArray());
        Assert.Equal(["pharmacy:G1:sales.trendCode"], Strings(r.GetProperty("noTrend")));
        Assert.Equal(["pharmacy:G1:stock.levelCode"], Strings(r.GetProperty("noLevel")));
        Assert.Equal(["safety::adverseEventNote"], Strings(r.GetProperty("aeYes")));
        Assert.Equal(["safety::adverseEvent"], Strings(r.GetProperty("aeOpen")));
        Assert.Equal(["general::metRoleCode"], Strings(r.GetProperty("noRole")));
        Assert.Equal(["general::visitKindCode"], Strings(r.GetProperty("noKind")));
        Assert.Equal(["pharmacy::competitors"], Strings(r.GetProperty("noCompListed")));
        Assert.Empty(r.GetProperty("noCompUnlisted").EnumerateArray());
        Assert.Equal(["order", "stock", "market", "safety", "general"], Strings(r.GetProperty("steps")));
        var boxes = r.GetProperty("boxes").EnumerateArray().ToArray();
        Assert.Equal(JsonValueKind.Null, boxes[0].GetProperty("value").ValueKind); // optional
        Assert.Equal(JsonValueKind.Null, boxes[0].GetProperty("error").ValueKind);
        Assert.Equal(12, boxes[1].GetProperty("value").GetInt32());
        Assert.All(boxes.Skip(2), b => Assert.Equal("ErrBoxes", b.GetProperty("error").GetString()));
        Assert.Equal([true, false, false], r.GetProperty("listed").EnumerateArray().Select(x => x.GetBoolean()).ToArray());
        var body = r.GetProperty("body");
        Assert.Empty(body.GetProperty("productSections").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("doctor").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("safety").GetProperty("offLabelRequest").ValueKind); // never a pharmacy question
        Assert.Equal(JsonValueKind.Null, body.GetProperty("general").GetProperty("metContactId").ValueKind);
        Assert.Equal(3, body.GetProperty("expectedVersion").GetInt32());
        Assert.False(body.TryGetProperty("tenantId", out _));
        var seeded = r.GetProperty("seeded");
        Assert.Equal(["G1", "G2"], seeded.GetProperty("stock").EnumerateArray().Select(x => x.GetProperty("gskuId").GetString()!).ToArray());
        Assert.Equal("low", seeded.GetProperty("stock")[0].GetProperty("levelCode").GetString()); // the rep's answer kept
        Assert.Equal(["G1", "G2"], seeded.GetProperty("sales").EnumerateArray().Select(x => x.GetProperty("gskuId").GetString()!).ToArray());
        Assert.Equal(["draft", "draft", "resubmit", "resubmit"], Strings(r.GetProperty("mode")));
        Assert.Equal("ErrProposalName", r.GetProperty("proposal")[0].GetProperty("name").GetString());
        Assert.Equal("ErrProposalCompany", r.GetProperty("proposal")[0].GetProperty("company").GetString());
        Assert.Empty(r.GetProperty("proposal")[1].EnumerateObject());
        var adv = r.GetProperty("adv").EnumerateArray().ToArray();
        Assert.Equal("price", adv[0].GetProperty("advantageCodes")[0].GetString());
        Assert.Equal("price", adv[1].GetProperty("disadvantageCodes")[0].GetString());
        Assert.Empty(adv[2].GetProperty("advantageCodes").EnumerateArray());
        Assert.Empty(adv[2].GetProperty("disadvantageCodes").EnumerateArray());
        Assert.Equal(["Err_visit_report_edit_window_closed", "Err_shelf_photo_pharmacy_only", "Err_unavailable"], Strings(r.GetProperty("err")));
        Assert.True(r.GetProperty("amend").GetBoolean());
    }

    [Fact]
    public void B10_threshold_validation_is_0_below_low_at_most_high_at_most_100000_whole_numbers()
    {
        var node = RunNode(ThresholdCore() + """
            const v = (a, b) => { const x = C.validate(a, b); return [x.ok, x.errors.low || null, x.errors.high || null]; };
            console.log(JSON.stringify({
              cases: [v(5, 20), v(10, 10), v(1, 100000), v(20, 5), v(0, 5), v(5, 100001), v('', 5), v(5, ''), v('2.5', 5), v('-3', 5), v('abc', 5)],
              diff: [C.diff({ lowBelow: 5, highAbove: 20, source: 'product', key: 'P1' }, 5, 30), C.diff({ lowBelow: 5, highAbove: 20, source: 'product', key: 'P1' }, 5, 20),
                C.diff({ lowBelow: 5, highAbove: 20, source: 'default', key: 'P2' }, 5, 20), C.diff({ lowBelow: 5, highAbove: 20, source: 'default', key: 'default' }, 5, 20)],
              cls: [C.classOf(4, { lowBelow: 5, highAbove: 20 }), C.classOf(5, { lowBelow: 5, highAbove: 20 }), C.classOf(20, { lowBelow: 5, highAbove: 20 }), C.classOf(21, { lowBelow: 5, highAbove: 20 })],
              rows: C.rowsOf({ default: { lowBelow: 5, highAbove: 20, source: 'built_in' }, products: [{ productId: 'b', lowBelow: 1, highAbove: 2, source: 'product' }, { productId: 'a', lowBelow: 1, highAbove: 2, source: 'product' }] }, { a: 'Alfa', b: 'Beta' }).map(x => x.key),
              body: C.putBody(5, 20, 2),
              hist: C.historyRows([{ at: '2026-10-01T00:00:00Z', productId: null, field: 'lowBelow' }, { at: '2026-10-02T00:00:00Z', productId: 'a', field: 'highAbove' }], { a: 'Alfa' }).map(x => [x.target, x.field]),
              err: [C.errorKey({ status: 400, body: { errors: ['x', 'potential_threshold_invalid'] } }), C.errorKey({ status: 409, body: {} }), C.errorKey({ status: 503, body: { errors: ['reference_data_unavailable'] } })]
            }));
            """);
        if (node is null) return;
        var r = JsonDocument.Parse(node).RootElement;
        var cases = r.GetProperty("cases").EnumerateArray().Select(c => (c[0].GetBoolean(), c[1].ValueKind == JsonValueKind.Null ? null : c[1].GetString(), c[2].ValueKind == JsonValueKind.Null ? null : c[2].GetString())).ToArray();
        Assert.Equal((true, null, null), cases[0]);
        Assert.Equal((true, null, null), cases[1]);      // Low == High is allowed (≤)
        Assert.Equal((true, null, null), cases[2]);
        Assert.Equal((false, null, "ErrOrder"), cases[3]); // LowBelow ≤ HighAbove
        Assert.Equal((false, "ErrRange", null), cases[4]); // 0 < low
        Assert.Equal((false, null, "ErrRange"), cases[5]); // ≤ 100000
        Assert.Equal((false, "ErrRequired", null), cases[6]);
        Assert.Equal((false, null, "ErrRequired"), cases[7]);
        Assert.All(cases.Skip(8), c => Assert.Equal((false, "ErrRange", null), c));
        var diff = r.GetProperty("diff").EnumerateArray().ToArray();
        Assert.Equal("highAbove", Assert.Single(diff[0].EnumerateArray()).GetProperty("field").GetString());
        Assert.Empty(diff[1].EnumerateArray());
        Assert.Equal("own", Assert.Single(diff[2].EnumerateArray()).GetProperty("field").GetString()); // an inherited product row saved as is
        Assert.Empty(diff[3].EnumerateArray());
        Assert.Equal(["low", "medium", "medium", "high"], Strings(r.GetProperty("cls")));
        Assert.Equal(["default", "a", "b"], Strings(r.GetProperty("rows")));
        Assert.Equal("{\"lowBelow\":5,\"highAbove\":20,\"expectedVersion\":2}", r.GetProperty("body").GetRawText());
        Assert.Equal("Alfa", r.GetProperty("hist")[0][0].GetString()); // newest first
        Assert.Equal("default", r.GetProperty("hist")[1][0].GetString());
        Assert.Equal(["Err_potential_threshold_invalid", "Err_version_conflict", "Err_reference_data_unavailable"], Strings(r.GetProperty("err")));
    }

    // ═══ 4 · the pages in a fake DOM ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void B11_pharmacy_smoke_submit_photo_passive_order_and_the_trend_gate()
    {
        var r = Smoke("pharmacy-report-smoke.js", PharmacyReportIndex.ScriptKeys, "PharmacyReport", "PharmacyReportIndex", "submit");
        if (r is null) return;
        var o = r.Value;
        Assert.Equal(JsonValueKind.Null, o.GetProperty("createError").ValueKind);
        Assert.Equal(["G1"], Strings(o.GetProperty("stockRows")));  // only the plan product's SKU
        Assert.Equal(["G1"], Strings(o.GetProperty("salesRows")));
        Assert.Equal(0, o.GetProperty("gifCalls").GetInt32());
        Assert.Contains("JPEG", o.GetProperty("gifError").GetString());
        Assert.True(o.GetProperty("photoMultipart").GetBoolean());
        Assert.Equal(JsonValueKind.Null, o.GetProperty("photoContentType").ValueKind); // the browser writes the boundary
        Assert.Equal("file", o.GetProperty("photoParts")[0][0].GetString());
        Assert.Equal("E1", o.GetProperty("draftPhoto").GetString());
        // the pre-order is passive with its "soon" badge (W6)
        var order = o.GetProperty("orderHtml").GetString()!;
        Assert.Contains("id=\"ph-order-btn\" disabled aria-disabled=\"true\"", order);
        Assert.Contains("Yakında", order);
        Assert.DoesNotContain("href", order);
        // without the trend: no POST, the summary names the missing trend
        Assert.Equal(0, o.GetProperty("postsWithoutTrend").GetInt32());
        Assert.Equal("summary", o.GetProperty("stepAfterTry").GetString());
        Assert.Contains("satış eğilimi", o.GetProperty("summaryHtml").GetString());
        Assert.Empty(o.GetProperty("missingAfterTrend").EnumerateArray());
        var body = o.GetProperty("submitBody");
        Assert.Equal("up", body.GetProperty("pharmacy").GetProperty("sales")[0].GetProperty("trendCode").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("pharmacy").GetProperty("sales")[0].GetProperty("boxesPerMonth").ValueKind);
        Assert.Equal("C1", body.GetProperty("pharmacy").GetProperty("competitors")[0].GetProperty("competitorProductId").GetString());
        Assert.Equal("price", body.GetProperty("pharmacy").GetProperty("competitors")[0].GetProperty("advantageCodes")[0].GetString());
        Assert.Equal("E1", body.GetProperty("pharmacy").GetProperty("shelf").GetProperty("photoEvidenceId").GetString());
        Assert.False(body.GetProperty("safety").GetProperty("adverseEvent").GetBoolean());
        Assert.True(o.GetProperty("sentShown").GetBoolean());
        Assert.Contains("Rapor gönderildi.", Strings(o.GetProperty("toasts")));
    }

    [Fact]
    public void B12_pharmacy_smoke_read_only_writes_nothing_and_the_60_minute_rule_moves_to_the_reasoned_correction()
    {
        var ro = Smoke("pharmacy-report-smoke.js", PharmacyReportIndex.ScriptKeys, "PharmacyReport", "PharmacyReportIndex", "readonly");
        if (ro is null) return;
        Assert.Equal(JsonValueKind.Null, ro.Value.GetProperty("metAfterTap").ValueKind);
        Assert.True(ro.Value.GetProperty("draftHidden").GetBoolean());
        Assert.True(ro.Value.GetProperty("submitHidden").GetBoolean());
        Assert.Equal(0, ro.Value.GetProperty("writes").GetInt32());
        Assert.Contains("disabled", ro.Value.GetProperty("generalHtml").GetString());

        var w = Smoke("pharmacy-report-smoke.js", PharmacyReportIndex.ScriptKeys, "PharmacyReport", "PharmacyReportIndex", "window")!.Value;
        Assert.True(w.GetProperty("sentShown").GetBoolean());
        Assert.True(w.GetProperty("editing").GetBoolean());
        Assert.Equal("fix", w.GetProperty("modeAfter409").GetString());
        Assert.True(w.GetProperty("fixShown").GetBoolean());
        Assert.Equal(0, w.GetProperty("amendWithoutReason").GetInt32());
        Assert.Equal("Gerekçe zorunlu.", w.GetProperty("fixError").GetString());
        Assert.Equal("Stok yanlış girildi", w.GetProperty("amendBody").GetProperty("reason").GetString());
        Assert.Equal("PV1", w.GetProperty("amendBody").GetProperty("v2").GetProperty("plannedVisitId").GetString());
        Assert.Equal("sent", w.GetProperty("modeAfterAmend").GetString());
    }

    [Fact]
    public void B13_thresholds_smoke_save_confirm_put_and_history_and_read_only()
    {
        var r = Smoke("potential-thresholds-smoke.js", PotentialThresholdsIndex.ScriptKeys, "PotentialThresholds", "PotentialThresholdsIndex", "manage");
        // W3E-E4R2-FIX-c: the list is the shared factory (client mode), the history its second table (server mode); the
        // drawer, the checks and the save confirm are as they were
        if (r is null) return;
        var o = r.Value;
        Assert.Equal(JsonValueKind.Null, o.GetProperty("createError").ValueKind);
        var rows = o.GetProperty("rows").GetString()!;
        Assert.True(rows.IndexOf("Kiracı varsayılanı", StringComparison.Ordinal) < rows.IndexOf("Ürofleks", StringComparison.Ordinal)); // default first
        Assert.Contains("data-row-action=\"edit\"", rows);
        Assert.Equal(("client", "server", true), (o.GetProperty("dataMode").GetString(), o.GetProperty("historyMode").GetString(), o.GetProperty("historyServerSide").GetBoolean()));
        Assert.Contains("Ayşe Demir", o.GetProperty("historyRows").GetString());
        Assert.True(o.GetProperty("drawerShown").GetBoolean());
        // High below Low: refused before any call
        Assert.Equal("Çok sınırı (Y) Az sınırından (X) küçük olamaz.", o.GetProperty("orderError").GetString());
        Assert.True(o.GetProperty("orderErrorShown").GetBoolean());
        Assert.False(o.GetProperty("modalAfterInvalid").GetBoolean());
        Assert.Contains("Orta 50–60", o.GetProperty("bands").GetString());
        Assert.Contains("Orta", o.GetProperty("tryClass").GetString());
        Assert.True(o.GetProperty("modalShown").GetBoolean());
        Assert.Contains("Ürofleks", o.GetProperty("saveText").GetString());
        Assert.Contains("Az sınırı", o.GetProperty("saveDiff").GetString());
        var put = Assert.Single(o.GetProperty("put").EnumerateArray());
        Assert.Equal("/CRM/PotentialThresholds/api/thresholds/P1", put[0].GetString());
        Assert.Equal("{\"lowBelow\":50,\"highAbove\":60,\"expectedVersion\":1}", put[1].GetRawText());
        Assert.Contains("Eşikler kaydedildi.", Strings(o.GetProperty("toasts")));                       // the standard reload-with-toast
        Assert.Contains("&gt; 60", o.GetProperty("rowsAfter").GetString());
        Assert.Contains("50", o.GetProperty("historyAfter").GetString());
        Assert.Equal(2, o.GetProperty("historyReads").GetInt32());                                            // a save reloads the history
        // a new row offers only products without a row, and needs one
        Assert.DoesNotContain("value=\"P1\"", o.GetProperty("newOptions").GetString());
        Assert.Contains("value=\"P2\"", o.GetProperty("newOptions").GetString());
        Assert.Equal("Bir ürün seçin.", o.GetProperty("productError").GetString());
        Assert.Equal(0, o.GetProperty("callsWithoutProduct").GetInt32());

        var ro = Smoke("potential-thresholds-smoke.js", PotentialThresholdsIndex.ScriptKeys, "PotentialThresholds", "PotentialThresholdsIndex", "readonly")!.Value;
        Assert.DoesNotContain("data-row-action=\"edit\"", ro.GetProperty("rows").GetString());
        Assert.Equal("", ro.GetProperty("addNewText").GetString());
        Assert.False(ro.GetProperty("drawerShown").GetBoolean());
        Assert.Equal(0, ro.GetProperty("writes").GetInt32());
    }

    // ═══ 5 · languages, RTL, dark, the extension point ═════════════════════════════════════════════════════

    [Theory]
    [InlineData("PharmacyReport", "PharmacyReportIndex")]
    [InlineData("PotentialThresholds", "PotentialThresholdsIndex")]
    public void B14_every_key_in_seven_languages_without_echo_with_its_placeholders_and_arabic(string folder, string baseName)
    {
        var (script, viewKeys) = folder == "PharmacyReport"
            ? (PharmacyReportIndex.ScriptKeys, PharmacyReportIndex.ViewKeys)
            : (PotentialThresholdsIndex.ScriptKeys, PotentialThresholdsIndex.ViewKeys);
        var all = script.Concat(viewKeys).Distinct().ToArray();
        var neutral = Resx(folder, baseName, "");
        foreach (var culture in Cultures)
        {
            var values = Resx(folder, baseName, culture);
            foreach (var key in all)
            {
                Assert.True(values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value), $"{culture}:{key}");
                Assert.NotEqual(key, value);
                Assert.Equal(Placeholders(neutral[key]), Placeholders(value!));
            }
        }

        Assert.All(Resx(folder, baseName, "ar").Values, v => Assert.Matches("[؀-ۿ]|^[\\d\\s{}<>·:/–()+−-]+$", v));

        // every key the view writes is a ViewKey; every L.X / T('X') the page script reads is a ScriptKey
        var view = View(folder);
        foreach (Match m in Regex.Matches(view, "Localizer\\[\"([A-Za-z_]+)\"\\]")) Assert.Contains(m.Groups[1].Value, viewKeys);
        var js = folder == "PharmacyReport" ? Script(folder, "pharmacy-report.js") : Script(folder, "index.js") + Script(folder, "history.js");
        // W3E-E4R2-FIX-c: the thresholds page reads window.L10n (the standard payload): its own ScriptKeys + the shared words
        // the payload carries beside them (_IndexL10n.cshtml)
        var shared = folder == "PharmacyReport" ? Array.Empty<string>()
            : Regex.Matches(File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", folder, "_IndexL10n.cshtml")), "(\\w+) = (?:Shared)?Localizer\\[").Select(x => x.Groups[1].Value).ToArray();
        foreach (Match m in Regex.Matches(js, "\\bL(?:\\(\\))?\\.([A-Za-z_]+)|\\bT\\('([A-Za-z_]+)'"))
        {
            var key = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
            Assert.True(script.Contains(key) || shared.Contains(key), $"{folder}: L.{key} is not a ScriptKey");
        }
    }

    [Fact]
    public void B15_the_core_codes_have_texts_and_the_pharmacy_core_joins_report_core_only_through_its_extension_point()
    {
        foreach (var code in CodesOf(Script("PharmacyReport", "report-pharmacy-core.js"))) Assert.Contains("Err_" + code, PharmacyReportIndex.ScriptKeys);
        foreach (var code in CodesOf(Script("PotentialThresholds", "thresholds-core.js"))) Assert.Contains("Err_" + code, PotentialThresholdsIndex.ScriptKeys);
        foreach (var field in new[] { "visitKindCode", "adverseEvent", "adverseEventNote", "metRoleCode", "stock_levelCode", "sales_trendCode", "competitors" })
        {
            Assert.Contains("Miss_" + field, PharmacyReportIndex.ScriptKeys);
        }

        var core = Script("PharmacyReport", "report-pharmacy-core.js");
        Assert.Contains("root.ReportCore && typeof root.ReportCore.registerKind === 'function'", core);
        Assert.Contains("root.ReportCore.registerKind('pharmacy', api)", core);
        Assert.DoesNotContain("ReportCore.", core.Replace("root.ReportCore.registerKind", string.Empty).Replace("root.ReportCore &&", string.Empty));
        // the pharmacy page never loads or writes report-core.js itself (WEB-a owns it)
        Assert.DoesNotContain("report-core.js", View("PharmacyReport"));
    }

    [Fact]
    public void B16_names_are_isolated_rtl_mirrors_dark_tokens_exist_and_the_rep_never_sees_a_campaign()
    {
        foreach (var (folder, file, css, rootId) in new[]
                 {
                     ("PharmacyReport", "pharmacy-report.js", "pharmacy-report.css", "#ph-root"),
                     ("PotentialThresholds", "index.js", "potential-thresholds.css", "#pt-root")
                 })
        {
            var js = Script(folder, file);
            Assert.Contains("F.bidi(", js);
            Assert.DoesNotContain("toLocaleDateString", js);
            Assert.DoesNotContain("localStorage", js);
            Assert.DoesNotContain("campaign", js, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("strategyTemplate", js, StringComparison.OrdinalIgnoreCase);
            var sheet = Script(folder, css);
            Assert.Contains("[data-bs-theme=\"dark\"] " + rootId, sheet);
            Assert.Contains("[dir=\"rtl\"] .", sheet);
            foreach (var physical in new[] { "margin-left", "margin-right", "padding-left", "padding-right", " left:", " right:", "text-align: left", "text-align: right" })
            {
                Assert.DoesNotContain(physical, sheet);
            }
        }

        // the menu label (BE-c) in every language
        foreach (var culture in new[] { "en", "tr", "fr", "es", "zh", "ar", "ru" })
        {
            var shared = XDocument.Load(Path.Combine(WebRoot(), "Resources", $"SharedResource.{culture}.resx")).Root!
                .Elements("data").ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")?.Value ?? "");
            Assert.True(shared.TryGetValue("Nav.Page.CRMDOCTORINSIGHTTHRESHOLDS", out var nav) && !string.IsNullOrWhiteSpace(nav), culture);
        }
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────

    private static IEnumerable<string> CodesOf(string core)
    {
        var block = Regex.Match(core, "const ERROR_CODES = \\[(.*?)\\];", RegexOptions.Singleline).Groups[1].Value;
        return Regex.Matches(block, "'([a-z0-9_]+)'").Select(m => m.Groups[1].Value);
    }

    private static string[] Strings(JsonElement array) => array.EnumerateArray().Select(x => x.GetString()!).ToArray();

    private static string Placeholders(string text) => string.Join(",", Regex.Matches(text, "\\{\\d\\}").Select(m => m.Value).OrderBy(x => x));

    // ═══ W4-FIX-c — E4 round 2: G-6 labels, G-7 the pharmacy in the header, G-10 same-named doctors ═════════

    [Fact]
    public void FixC_G6_option_labels_read_the_real_prefill_contract_label_lang_then_label_then_code()
    {
        var node = RunNode(PharmacyCore() + """
            // the CRM VisitReportOptionDto: the BRD value's English label + its attributes (label_<lang>, and any other attribute)
            const o = { code: 'in_stock', label: 'In stock', labels: { label_tr: 'Var', label_ar: 'متوفر', note: 'x' } };
            process.stdout.write(JSON.stringify([
              C.optionLabel(o, 'tr'), C.optionLabel(o, 'tr-TR'), C.optionLabel(o, 'en'), C.optionLabel(o, 'ar'), C.optionLabel(o, 'fr'),
              C.optionLabel({ code: 'low', label: null, labels: null }, 'tr'),
              C.optionLabel({ code: 'low', label: 'Low', labels: { tr: 'Az' } }, 'tr'),   // the OLD fake shape is not the contract
              C.contactDetail({ specialty: 'Kardiyoloji', institution: 'Şehir Hastanesi', city: 'Ankara' }),
              C.contactDetail({ specialty: 'Üroloji', institution: null, city: '' }),
              C.headerParts({ name: 'Hayat Eczanesi', city: null }, '13 Eki Salı').join('|'),
              C.headerParts(null, '13 Eki Salı').join('|')
            ]));
            """);
        if (node is null) return;
        var r = Strings(JsonDocument.Parse(node).RootElement);
        Assert.Equal(["Var", "Var", "In stock", "متوفر", "In stock", "low", "Low",
            "Kardiyoloji · Şehir Hastanesi · Ankara", "Üroloji", "Hayat Eczanesi|13 Eki Salı", "13 Eki Salı"], r);
    }

    [Fact]
    public void FixC_G6_G7_G10_pharmacy_smoke_turkish_options_and_summary_the_pharmacy_in_the_header_distinguishable_doctors()
    {
        var r = Smoke("pharmacy-report-smoke.js", PharmacyReportIndex.ScriptKeys, "PharmacyReport", "PharmacyReportIndex", "submit");
        if (r is null) return;
        var o = r.Value;
        // G-6: every option button + the summary in Turkish (the pre-fill sends English labels + labels.label_tr)
        var general = o.GetProperty("generalHtml0").GetString()!;
        Assert.Contains("data-val=\"pharmacist\" aria-pressed=\"false\">Eczacı</button>", general);
        Assert.DoesNotContain(">Pharmacist<", general);
        var stock = o.GetProperty("stockHtml").GetString()!;
        Assert.Contains(">Var</button>", stock);
        Assert.Contains(">Yok</button>", stock);
        Assert.DoesNotContain("In stock", stock);
        Assert.Contains("Eczacı", o.GetProperty("summaryHtml").GetString());
        var market = o.GetProperty("marketHtml").GetString()!;
        Assert.Contains("data-code=\"price\">+ Fiyat</button>", market);               // the competitor advantage
        Assert.DoesNotContain("Price</button>", market);
        Assert.Contains("120,50 TRY · Geri ödemede", market);                            // the card: money + the reimbursement LABEL
        Assert.DoesNotContain("· reimbursed", market);
        // G-7: "Hayat Eczanesi · İstanbul · <day>" (mockup E6), the name isolated for RTL
        Assert.StartsWith("<bdi>Hayat Eczanesi</bdi> · İstanbul · ", o.GetProperty("headerSub").GetString());
        // G-10: two "Dr. Ali Yılmaz" told apart — specialty label + institution + city, only what is known
        Assert.Contains("<bdi>Kardiyoloji · Şehir Hastanesi · Ankara</bdi>", market);
        Assert.Contains("<bdi>Üroloji</bdi>", market);
        Assert.DoesNotContain("cardiology", market);                                    // never the code
    }

    [Fact]
    public async Task FixC_G10_the_contact_search_completes_each_hit_from_existing_reads_with_brd_labels()
    {
        const string c1 = "aaaaaaaa-0000-0000-0000-000000000001", c2 = "aaaaaaaa-0000-0000-0000-000000000002";
        var routes = new Dictionary<string, string>
        {
            ["/api/crm/contacts/search"] = "{\"data\":[{\"id\":\"" + c1 + "\",\"displayName\":\"Dr. Ali Yılmaz\"},{\"id\":\"" + c2 + "\",\"displayName\":\"Dr. Ali Yılmaz\"}]}",
            ["/api/crm/contacts/" + c1 + "/accounts"] = "{\"data\":[{\"accountName\":\"Eski Klinik\",\"isPrimary\":false,\"status\":\"active\"},{\"accountName\":\"Şehir Hastanesi\",\"isPrimary\":true,\"status\":\"active\"}]}",
            ["/api/crm/contacts/" + c2 + "/accounts"] = "{\"data\":[]}",
            ["/api/crm/contacts/" + c1] = "{\"data\":{\"id\":\"" + c1 + "\",\"specialty\":\"cardiology\",\"cityRef\":\"tr-06\"}}",
            ["/api/crm/contacts/" + c2] = "{\"data\":{\"id\":\"" + c2 + "\",\"specialty\":\"urology\",\"cityRef\":null}}",
            ["/api/lookups/reference-data/consumable-sets/medical-specialty/"] = "{\"data\":{\"items\":[{\"code\":\"cardiology\",\"label\":\"Cardiology\",\"attributes\":{\"label_tr\":\"Kardiyoloji\"}},{\"code\":\"urology\",\"label\":\"Urology\",\"attributes\":{}}]}}",
            ["/api/lookups/reference-data/consumable-sets/city/"] = "{\"data\":{\"items\":[{\"code\":\"tr-06\",\"label\":\"Ankara\",\"attributes\":{\"label_tr\":\"Ankara\"}}]}}"
        };
        var previous = System.Globalization.CultureInfo.CurrentUICulture;
        System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("tr-TR");
        try
        {
            var gateway = new RouteGateway(routes);
            var controller = new PharmacyReportController(new HttpClient(gateway), Config(), NullLogger<PharmacyReportController>.Instance)
            {
                ControllerContext = Context([ReportRead, ContactRead, PharmacyReportController.AccountContactRead], "")
            };
            var json = JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(await controller.Contacts("Ali", CancellationToken.None)).Value,
                new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            using var doc = JsonDocument.Parse(json);
            var rows = doc.RootElement.GetProperty("data").EnumerateArray().ToArray();
            Assert.Equal("Kardiyoloji", rows[0].GetProperty("specialty").GetString());
            Assert.Equal("cardiology", rows[0].GetProperty("specialtyCode").GetString());
            Assert.Equal("Ankara", rows[0].GetProperty("city").GetString());
            Assert.Equal("Şehir Hastanesi", rows[0].GetProperty("institution").GetString());     // the PRIMARY institution
            Assert.Equal("Urology", rows[1].GetProperty("specialty").GetString());              // no Turkish label → the label
            Assert.Equal(JsonValueKind.Null, rows[1].GetProperty("city").ValueKind);
            Assert.Equal(JsonValueKind.Null, rows[1].GetProperty("institution").ValueKind);
            Assert.All(gateway.Paths, p => Assert.DoesNotContain("tenantId", p));

            // without crm.account-contact.read: no linked-accounts read, the institution stays empty
            var g2 = new RouteGateway(routes);
            var c3 = new PharmacyReportController(new HttpClient(g2), Config(), NullLogger<PharmacyReportController>.Instance) { ControllerContext = Context([ReportRead, ContactRead], "") };
            var json2 = JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(await c3.Contacts("Ali", CancellationToken.None)).Value);
            Assert.DoesNotContain(g2.Paths, p => p.EndsWith("/accounts"));
            Assert.Contains("\"institution\":null", json2);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public async Task FixC_G7_the_header_target_reads_the_planned_visit_and_the_account_city_with_their_keys()
    {
        const string account = "bbbbbbbb-0000-0000-0000-000000000001";
        var routes = new Dictionary<string, string>
        {
            ["/api/crm/planned-visits/" + Id] = "{\"data\":{\"plannedVisitId\":\"" + Id + "\",\"targetType\":\"pharmacy\",\"targetId\":\"" + account + "\",\"accountId\":null,\"targetDisplayName\":\"Hayat Eczanesi\"}}",
            ["/api/crm/accounts/" + account] = "{\"data\":{\"id\":\"" + account + "\",\"accountName\":\"Hayat Eczanesi\",\"cityRef\":\"tr-34\"}}",
            ["/api/lookups/reference-data/consumable-sets/city/"] = "{\"data\":{\"items\":[{\"code\":\"tr-34\",\"label\":\"Istanbul\",\"attributes\":{\"label_tr\":\"İstanbul\"}}]}}"
        };
        var previous = System.Globalization.CultureInfo.CurrentUICulture;
        System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("tr-TR");
        try
        {
            var gateway = new RouteGateway(routes);
            var controller = new PharmacyReportController(new HttpClient(gateway), Config(), NullLogger<PharmacyReportController>.Instance)
            {
                ControllerContext = Context([ReportRead, PharmacyReportController.PlannedVisitRead, PharmacyReportController.AccountRead], "")
            };
            var json = JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(await controller.Target(Guid.Parse(Id), CancellationToken.None)).Value,
                new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            Assert.Contains("\"name\":\"Hayat Eczanesi\"", json);
            Assert.Contains("\"city\":\"İstanbul\"", json);

            // no account read: the name only (no account call)
            var g2 = new RouteGateway(routes);
            var c2 = new PharmacyReportController(new HttpClient(g2), Config(), NullLogger<PharmacyReportController>.Instance) { ControllerContext = Context([ReportRead, PharmacyReportController.PlannedVisitRead], "") };
            var json2 = JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(await c2.Target(Guid.Parse(Id), CancellationToken.None)).Value);
            Assert.Contains("\"city\":null", json2);
            Assert.DoesNotContain(g2.Paths, p => p.Contains("/api/crm/accounts/"));

            // no plan read: 403, nothing sent
            var g3 = new RouteGateway(routes);
            var c3 = new PharmacyReportController(new HttpClient(g3), Config(), NullLogger<PharmacyReportController>.Instance) { ControllerContext = Context([ReportRead], "") };
            Assert.Equal(403, Status(await c3.Target(Guid.Parse(Id), CancellationToken.None)));
            Assert.Empty(g3.Paths);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public async Task FixC_G6_the_reimbursement_set_is_readable_with_the_page_key_and_no_other_set()
    {
        var (gateway, controller) = PharmacyArrange([ReportRead], "");
        Assert.Equal(200, Status(await controller.ReferenceValues("mdm-reimbursement-status", CancellationToken.None)));
        Assert.Contains("/consumable-sets/mdm-reimbursement-status/", gateway.Requests[0].Uri);
        var (g2, c2) = PharmacyArrange([ReportRead], "");
        Assert.Equal(404, Status(await c2.ReferenceValues("medical-specialty", CancellationToken.None)));
        Assert.Empty(g2.Requests);
        var (g3, c3) = PharmacyArrange([ContactRead], "");
        Assert.Equal(403, Status(await c3.ReferenceValues("mdm-reimbursement-status", CancellationToken.None)));
        Assert.Empty(g3.Requests);
    }

    [Fact]
    public void FixC_the_city_reference_reads_the_city_set_like_visit_planning_and_is_never_shown_raw()
    {
        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["adana"] = "Adana", ["izmir"] = "İzmir", ["tr-34"] = "İstanbul" };
        Assert.Equal("Adana", PharmacyReportController.CityLabel(labels, "TR-01-ADANA"));   // the master "TR-01-ADANA" ↔ the set "adana"
        Assert.Equal("İstanbul", PharmacyReportController.CityLabel(labels, "tr-34"));
        Assert.Equal("Kırşehir", PharmacyReportController.CityLabel(labels, "TR-40-KIRŞEHİR")); // no label: the Turkish way
        Assert.Equal("Kocaeli", PharmacyReportController.CityLabel(new Dictionary<string, string>(), "TR-41-KOCAELİ"));
        Assert.Null(PharmacyReportController.CityLabel(labels, "TR-99"));                   // a number alone is not a name
        Assert.Null(PharmacyReportController.CityLabel(labels, null));
    }

    /// <summary>A Gateway answering by path (the longest matching prefix); an unknown path is 404.</summary>
    private sealed class RouteGateway(IReadOnlyDictionary<string, string> routes) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.PathAndQuery;
            lock (Paths) { Paths.Add(path); }
            var bare = request.RequestUri!.AbsolutePath;
            var hit = routes.Keys.Where(k => k.EndsWith('/') ? bare.StartsWith(k, StringComparison.Ordinal) : bare == k).OrderByDescending(k => k.Length).FirstOrDefault();
            return Task.FromResult(hit is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}", Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(routes[hit], Encoding.UTF8, "application/json") });
        }
    }

    private JsonElement? Smoke(string smokeFile, string[] keys, string folder, string baseName, string mode)
    {
        var smoke = Path.Combine(Path.GetDirectoryName(WebRoot())!, "Diten.Web.Tests", "Js", smokeFile).Replace("\\", "/");
        var crm = Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM").Replace("\\", "/");
        var tr = Resx(folder, baseName, "tr");
        var texts = Path.Combine(Path.GetTempPath(), "w4b-tr-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(texts, JsonSerializer.Serialize(keys.ToDictionary(k => k, k => tr[k])), new UTF8Encoding(false));
        try
        {
            var node = RunNode("process.argv[2] = '" + crm + "'; process.argv[3] = '" + texts.Replace("\\", "/") + "'; process.argv[4] = '" + mode + "'; require('" + smoke + "');");
            return node is null ? null : JsonDocument.Parse(node).RootElement.Clone();
        }
        finally
        {
            File.Delete(texts);
        }
    }

    private static PharmacyReportViewModel PharmacyModel(IActionResult result)
        => Assert.IsType<PharmacyReportViewModel>(Assert.IsType<ViewResult>(result).Model);

    private static PotentialThresholdsViewModel ThresholdModel(IActionResult result)
        => Assert.IsType<PotentialThresholdsViewModel>(Assert.IsType<ViewResult>(result).Model);

    private static int? Status(IActionResult result) => result switch
    {
        ContentResult c => c.StatusCode,
        ObjectResult o => o.StatusCode ?? 200,
        StatusCodeResult s => s.StatusCode,
        FileContentResult => 200,
        _ => null
    };

    private static Task<IActionResult> InvokePharmacy(PharmacyReportController c, string action) => action switch
    {
        "Prefill" => c.Prefill(Guid.Parse(Id), CancellationToken.None),
        "Draft" => c.Draft(CancellationToken.None),
        "Submit" => c.Submit(CancellationToken.None),
        "AmendReport" => c.AmendReport(Guid.Parse(Id), CancellationToken.None),
        "ShelfPhotoImage" => c.ShelfPhotoImage(Guid.Parse(Id), CancellationToken.None),
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };

    private static PharmacyReportController Pharmacy(params string[] permissions) => PharmacyArrange(permissions, "{}").Controller;

    private static PotentialThresholdsController Thresholds(params string[] permissions) => ThresholdArrange(permissions, "{}").Controller;

    private static (Gateway Gateway, PharmacyReportController Controller) PharmacyArrange(string[] permissions, string body, string? answer = null)
    {
        var gateway = new Gateway(answer);
        var controller = new PharmacyReportController(new HttpClient(gateway), Config(), NullLogger<PharmacyReportController>.Instance);
        controller.ControllerContext = Context(permissions, body);
        return (gateway, controller);
    }

    private static (Gateway Gateway, PotentialThresholdsController Controller) ThresholdArrange(string[] permissions, string body, string? answer = null)
    {
        var gateway = new Gateway(answer);
        var controller = new PotentialThresholdsController(new HttpClient(gateway), Config(), NullLogger<PotentialThresholdsController>.Instance);
        controller.ControllerContext = Context(permissions, body);
        return (gateway, controller);
    }

    private static IConfiguration Config()
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = GatewayUrl }).Build();

    private static ControllerContext Context(string[] permissions, string body)
    {
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()), new("sub", "user-1") };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        return new ControllerContext { HttpContext = http };
    }

    private static string PharmacyCore() => "const C = require('" + Path.Combine(ScriptDir("PharmacyReport"), "report-pharmacy-core.js").Replace("\\", "/") + "');\n";

    private static string ThresholdCore() => "const C = require('" + Path.Combine(ScriptDir("PotentialThresholds"), "thresholds-core.js").Replace("\\", "/") + "');\n";

    /// <summary>Runs <paramref name="code"/> in Node; null when Node is not on the machine (the code assertions stand alone).</summary>
    private static string? RunNode(string code)
    {
        var temp = Path.Combine(Path.GetTempPath(), "w4b-" + Guid.NewGuid().ToString("N") + ".js");
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

    private static string ScriptDir(string folder) => Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", folder);

    private static string Script(string folder, string file) => File.ReadAllText(Path.Combine(ScriptDir(folder), file)).Replace("\r\n", "\n");

    private static string View(string folder) => File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", folder, "Index.cshtml")).Replace("\r\n", "\n");

    private static Dictionary<string, string> Resx(string folder, string baseName, string culture)
    {
        var file = baseName + (culture.Length == 0 ? "" : "." + culture) + ".resx";
        return XDocument.Load(Path.Combine(WebRoot(), "Resources", "Views", "CRM", folder, file)).Root!
            .Elements("data").ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")?.Value ?? string.Empty, StringComparer.Ordinal);
    }

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }

    private sealed class Gateway(string? answer) : HttpMessageHandler
    {
        public List<(string Method, string Uri, string? Tenant, string? IfMatch, string? ContentType, string? Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request.Method.Method, request.RequestUri!.ToString(),
                request.Headers.TryGetValues("X-Tenant-Id", out var t) ? t.Single() : null,
                request.Headers.TryGetValues("If-Match", out var m) ? m.Single() : null,
                request.Content?.Headers.ContentType?.ToString(),
                request.Content is null ? null : await request.Content.ReadAsStringAsync(ct)));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer ?? "{\"data\":{}}", Encoding.UTF8, "application/json") };
        }
    }
}
