using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Diten.Web.Controllers.CRM;
using Diten.Web.Models.CRM;
using Diten.Web.Views.CRM.VisitWorkspace;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VW-W3 · W3-WEB-a — the visit workspace starts a visit, on the REAL controller, view, resx and scripts:
/// (1) the pure rules in Node (workspace-core.js): the actions by status (today / in_progress / finished), the E3 Start
/// button (the tenant setting × what is missing), a refusal code → the user's text (+ the distance), the in_progress
/// look, the multipart fields, the JPEG size, the back camera; (2) the page in a fake DOM: Start → E3, the refused
/// location's text, a photo → JPEG 1600 / 0.85, the start's multipart fields, a too-far refusal, success → the
/// presentation page, the in_progress actions, createError null; (3) the proxies: start (multipart streamed) and the
/// contract only with their keys, nothing else accepted; UAS-001 stays; (4) seven languages, no echo, Arabic RTL.
/// </summary>
public sealed partial class VisitWorkspaceStartWebTests
{
    private const string GatewayUrl = "http://gateway.test";
    private static readonly Guid TenantId = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");
    private const string ExecRead = "crm.visit-execution.read";
    private const string ExecRecord = "crm.visit-execution.record";
    private const string ReportRead = "crm.visit-report.read";
    private const string PlanRead = "crm.visit-plan.read";

    private static readonly string[] W3ScriptKeys =
    [
        "Status_in_progress", "AlertTitle_in_progress", "AlertText_in_progress", "AlertText_today_start", "CardLiveMinutes",
        "Action_start", "Action_presentation", "Action_finish", "E3Missing", "E3HintReady", "Loc_deniedDetail", "Loc_requiredMissing",
        "StartErr_visit_location_too_far", "StartErr_visit_photo_required", "UploadFailed"
    ];

    // ═══ 1 · the pure rules (Node) ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void A1_the_detail_actions_follow_today_in_progress_and_finished()
    {
        var node = RunNode(Core() + """
            const t = '2026-10-12', all = { manage: true, record: true, execute: true };
            const v = (s, extra) => Object.assign({ workStatus: s, plannedVisitId: 'x', plannedDate: t }, extra || {});
            process.stdout.write(JSON.stringify({
              today: C.visitActions(v('today'), t, all),
              todayRepOnly: C.visitActions(v('today'), t, { execute: true }),
              todayStarted: C.visitActions(v('today', { executionId: 'e' }), t, all),
              todayNoExec: C.visitActions(v('today'), t, { manage: true, record: true }),
              live: C.visitActions(v('in_progress', { executionId: 'e' }), t, all),
              liveNoExec: C.visitActions(v('in_progress', { executionId: 'e' }), t, { manage: true, record: true }),
              finished: C.visitActions(v('report_missing', { executionId: 'e', executionStatus: 'finished' }), t, all),
              planned: C.visitActions(v('planned', { plannedDate: '2026-10-13' }), t, all),
              draft: C.visitActions({ workStatus: 'draft', plannedVisitId: null, plannedDate: t }, t, all),
              tabsToday: C.dialogTabs(v('today'), t, all).map(x => x.key + ':' + x.enabled).join(','),
              tabsLive: C.dialogTabs(v('in_progress', { executionId: 'e' }), t, all).map(x => x.key + ':' + x.enabled).join(','),
              url: C.presentationUrl('/CRM/VisitPresentation', 'a b', false),
              urlSummary: C.presentationUrl('/CRM/VisitPresentation', 'p1', true)
            }));
            """);
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement;
        string[] A(string name) => r.GetProperty(name).EnumerateArray().Select(x => x.GetString()!).ToArray();

        Assert.Equal(new[] { "start", "notDone", "reschedule", "cancel" }, A("today"));   // the mockup's "hazir"
        Assert.Equal(new[] { "start" }, A("todayRepOnly"));
        Assert.Equal(new[] { "cancel", "result" }, A("todayStarted"));                    // an execution exists → W2 unchanged
        Assert.Equal(new[] { "cancel", "result" }, A("todayNoExec"));                     // without the execution keys → W2 unchanged
        Assert.Equal(new[] { "presentation", "finish" }, A("live"));                      // the mockup's "devam"
        Assert.Empty(A("liveNoExec"));
        Assert.Equal(new[] { "sendReport" }, A("finished"));
        Assert.Equal(new[] { "cancel" }, A("planned"));
        Assert.Equal(new[] { "plan" }, A("draft"));
        Assert.Equal("cancel:true,notDone:true,reschedule:true", r.GetProperty("tabsToday").GetString());
        Assert.Equal("cancel:false,notDone:false,reschedule:false", r.GetProperty("tabsLive").GetString()); // a started visit is not cancelled
        Assert.Equal("/CRM/VisitPresentation?plannedVisitId=a%20b", r.GetProperty("url").GetString());
        Assert.Equal("/CRM/VisitPresentation?plannedVisitId=p1&phase=summary", r.GetProperty("urlSummary").GetString());
    }

    [Fact]
    public void A2_the_start_button_follows_the_tenant_setting_and_what_is_missing()
    {
        var node = RunNode(Core() + """
            const pol = (photo, loc) => ({ photoMode: photo, locationMode: loc, maxAccuracyMeters: 150 });
            const fix = { status: 'ok', latitude: 41, longitude: 29, accuracyMeters: 12 };
            const cases = [];
            for (const photo of ['required', 'optional'])
              for (const loc of ['required', 'info'])
                for (const ev of [{}, { photo: {} }, { location: fix }, { photo: {}, location: fix }, { location: { status: 'denied' } }, { photo: {}, location: { status: 'denied' } }])
                  cases.push([photo, loc, !!ev.photo, ev.location ? ev.location.status : 'none', C.canBegin(pol(photo, loc), ev), C.startMissing(pol(photo, loc), ev).join('+')]);
            process.stdout.write(JSON.stringify({
              cases,
              busy: C.canBegin(pol('optional', 'info'), { busy: true }),
              defaults: C.evidenceNeeds(null),
              noPolicyStart: C.canBegin(null, { photo: {} }),
              noPolicyFix: C.canBegin(null, { location: fix })
            }));
            """);
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement;
        foreach (var c in r.GetProperty("cases").EnumerateArray())
        {
            var photoRequired = c[0].GetString() == "required";
            var locationRequired = c[1].GetString() == "required";
            var hasPhoto = c[2].GetBoolean();
            var hasFix = c[3].GetString() == "ok";
            var expected = (!photoRequired || hasPhoto) && (!locationRequired || hasFix);
            Assert.True(expected == c[4].GetBoolean(), $"photo={c[0]} loc={c[1]} hasPhoto={hasPhoto} fix={c[3]}");
            var missing = new List<string>();
            if (photoRequired && !hasPhoto) missing.Add("photo");
            if (locationRequired && !hasFix) missing.Add("location");
            Assert.Equal(string.Join("+", missing), c[5].GetString());
        }

        Assert.False(r.GetProperty("busy").GetBoolean()); // a start in flight disables it
        // no setting (contract not loaded / no tenant document) = the CT defaults: location required, photo optional
        Assert.False(r.GetProperty("defaults").GetProperty("photo").GetBoolean());
        Assert.True(r.GetProperty("defaults").GetProperty("location").GetBoolean());
        Assert.False(r.GetProperty("noPolicyStart").GetBoolean());
        Assert.True(r.GetProperty("noPolicyFix").GetBoolean());
    }

    [Fact]
    public void A3_a_refused_start_reads_in_the_users_language_with_the_distance()
    {
        var node = RunNode(Core() + """
            const R = (status, errors) => ({ status, body: errors ? { errors } : null });
            process.stdout.write(JSON.stringify({
              far: C.startError(R(409, ['You are 3400 m from the institution (at most 300 m).', 'visit_location_too_far', 'distance_meters=3400'])),
              photo: C.startError(R(400, ['A photo is required.', 'visit_photo_required'])),
              store: C.startError(R(503, ['The store is down.', 'visit_evidence_store_unavailable'])),
              foreign: C.startError(R(404, ['Not found.'])),
              unknown: C.startError(R(500, ['boom'])),
              network: C.startError(R(0, null)),
              everyCode: C.START_CODES.map(code => C.startError(R(409, ['x', code])).key),
              m: C.distanceParts(240), km: C.distanceParts(3400), none: C.distanceParts(null)
            }));
            """);
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement;
        var far = r.GetProperty("far");
        Assert.Equal("StartErr_visit_location_too_far", far.GetProperty("key").GetString());
        Assert.Equal(3400, far.GetProperty("distanceMeters").GetInt32());
        Assert.Equal("Loc_requiredNote", far.GetProperty("hintKey").GetString());
        Assert.Equal("StartErr_visit_photo_required", r.GetProperty("photo").GetProperty("key").GetString());
        Assert.Equal("StartErr_visit_evidence_store_unavailable", r.GetProperty("store").GetProperty("key").GetString());
        Assert.Equal("StartErr_visit_execution_not_found", r.GetProperty("foreign").GetProperty("key").GetString());
        Assert.Equal("ActionFailed", r.GetProperty("unknown").GetProperty("key").GetString());
        Assert.Equal("UploadFailed", r.GetProperty("network").GetProperty("key").GetString());

        // every code the CRM sends on a start has its text in every language (never a raw code)
        var keys = r.GetProperty("everyCode").EnumerateArray().Select(x => x.GetString()!).ToList();
        Assert.Equal(14, keys.Count);
        Assert.All(keys, k => Assert.Contains(k, VisitWorkspaceIndex.ScriptKeys));

        Assert.Equal("DistanceMeters", r.GetProperty("m").GetProperty("key").GetString());
        Assert.Equal(240, r.GetProperty("m").GetProperty("value").GetInt32());
        Assert.Equal("DistanceKm", r.GetProperty("km").GetProperty("key").GetString());
        Assert.Equal(3.4, r.GetProperty("km").GetProperty("value").GetDouble());
        Assert.Equal(JsonValueKind.Null, r.GetProperty("none").ValueKind);
    }

    [Fact]
    public void A4_in_progress_is_the_info_tone_with_a_pulse_and_the_running_minutes()
    {
        var node = RunNode(Core() + """
            const now = Date.parse('2026-10-12T10:38:20Z');
            const v = { workStatus: 'in_progress', startedAt: '2026-10-12T10:31:05Z', startTime: '10:30', endTime: '10:55', plannedContent: [] };
            process.stdout.write(JSON.stringify({
              style: C.statusStyle('in_progress'),
              order: [C.STATUSES.indexOf('in_progress'), C.STATUSES.indexOf('today')],
              legend: C.LEGEND.indexOf('in_progress') > -1,
              card: C.cardModel(v, { nowMs: now }),
              alert: C.alertFor(v, now, 180),
              alertUtc: C.alertFor(v, now, 0),
              filtered: C.filterVisits([v, { workStatus: 'today' }], { statuses: ['in_progress'] }).length,
              never: C.minutesSince('2026-10-12T11:00:00Z', now)
            }));
            """);
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement;
        var style = r.GetProperty("style");
        Assert.Equal("info", style.GetProperty("tone").GetString());
        Assert.True(style.GetProperty("live").GetBoolean());
        Assert.Equal("vw-st-in_progress", style.GetProperty("cssClass").GetString());
        Assert.Equal("Status_in_progress", style.GetProperty("labelKey").GetString());
        var order = r.GetProperty("order").EnumerateArray().Select(x => x.GetInt32()).ToArray();
        Assert.True(order[0] < order[1]); // before today (W3-BE-a)
        Assert.True(r.GetProperty("legend").GetBoolean());
        var card = r.GetProperty("card");
        Assert.True(card.GetProperty("live").GetBoolean());
        Assert.Equal("CardLiveMinutes", card.GetProperty("bottom").GetProperty("key").GetString());
        Assert.Equal(7, card.GetProperty("bottom").GetProperty("args")[0].GetInt32());
        var alert = r.GetProperty("alert");
        Assert.Equal("AlertTitle_in_progress", alert.GetProperty("titleKey").GetString());
        Assert.Equal(7, alert.GetProperty("args")[0].GetInt32());
        Assert.Equal("13:31", alert.GetProperty("args")[1].GetString()); // the server instant on the browser's clock (+3 h)
        Assert.Equal("10:31", r.GetProperty("alertUtc").GetProperty("args")[1].GetString());
        Assert.Equal(1, r.GetProperty("filtered").GetInt32());
        Assert.Equal(0, r.GetProperty("never").GetInt32()); // never negative

        var css = File.ReadAllText(Path.Combine(ScriptDir(), "visit-workspace.css"));
        Assert.Contains(".vw-st-in_progress", css);
        Assert.Contains("animation: vw-pulse", css);
        Assert.Contains("prefers-reduced-motion", css);
    }

    [Fact]
    public void A5_the_start_fields_the_jpeg_and_the_camera()
    {
        var node = RunNode(Core() + """
            const photo = { size: 1 };
            process.stdout.write(JSON.stringify({
              fix: C.startFields('p1', { location: { status: 'ok', latitude: 41.0123, longitude: 28.9876, accuracyMeters: 15 }, photo, photoCapturedAt: '2026-10-12T08:31:00.000Z' })
                .map(f => [f[0], f[1] === photo ? 'BLOB' : f[1], f[2] || null]),
              denied: C.startFields('p1', { location: { status: 'denied' } }),
              pending: C.startFields('p1', { location: { status: 'pending' } }),
              big: C.photoSize(4000, 3000), tall: C.photoSize(1200, 2400), small: C.photoSize(800, 600),
              q: C.PHOTO_QUALITY, t: C.PHOTO_TYPE, edge: C.PHOTO_MAX_EDGE,
              phone: C.cameraConstraints('list'), desk: C.cameraConstraints('grid'),
              cam: ['NotAllowedError', 'NotFoundError', 'AbortError'].map(n => C.cameraErrorKey({ name: n })),
              geo: [1, 2, 3].map(code => C.geoErrorStatus({ code })),
              loc: ['pending', 'denied', 'unavailable', 'timeout'].map(s => C.locationView({ status: s }, { locationMode: 'required' }).labelKey),
              locBad: C.locationView({ status: 'ok', latitude: 1, longitude: 2, accuracyMeters: 400 }, { maxAccuracyMeters: 150 }),
              locOk: C.locationView({ status: 'ok', latitude: 1, longitude: 2, accuracyMeters: 12.4 }, { maxAccuracyMeters: 150, locationMode: 'info' })
            }));
            """);
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement;
        var fix = r.GetProperty("fix").EnumerateArray().Select(f => f[0].GetString() + "=" + f[1].GetString() + (f[2].ValueKind == JsonValueKind.String ? "|" + f[2].GetString() : "")).ToArray();
        Assert.Equal(new[]
        {
            "plannedVisitId=p1", "kind=in_person", "latitude=41.0123", "longitude=28.9876", "accuracyMeters=15",
            "photo=BLOB|visit-photo.jpg", "photoCapturedAt=2026-10-12T08:31:00.000Z"
        }, fix);
        var denied = r.GetProperty("denied").EnumerateArray().Select(f => f[0].GetString() + "=" + f[1].GetString()).ToArray();
        Assert.Equal(new[] { "plannedVisitId=p1", "kind=in_person", "locationError=denied" }, denied);
        Assert.Equal(2, r.GetProperty("pending").GetArrayLength()); // nothing about a position still on its way

        Assert.Equal(1600, r.GetProperty("big").GetProperty("width").GetInt32());
        Assert.Equal(1200, r.GetProperty("big").GetProperty("height").GetInt32());
        Assert.Equal(800, r.GetProperty("tall").GetProperty("width").GetInt32());
        Assert.Equal(1600, r.GetProperty("tall").GetProperty("height").GetInt32());
        Assert.Equal(800, r.GetProperty("small").GetProperty("width").GetInt32()); // never enlarged
        Assert.Equal(0.85, r.GetProperty("q").GetDouble());
        Assert.Equal("image/jpeg", r.GetProperty("t").GetString());
        Assert.Equal(1600, r.GetProperty("edge").GetInt32());

        Assert.Equal("environment", r.GetProperty("phone").GetProperty("video").GetProperty("facingMode").GetProperty("ideal").GetString());
        Assert.False(r.GetProperty("phone").GetProperty("audio").GetBoolean());
        Assert.True(r.GetProperty("desk").GetProperty("video").GetBoolean());
        Assert.Equal(new[] { "Cam_denied", "Cam_unavailable", "Cam_failed" }, r.GetProperty("cam").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(new[] { "denied", "unavailable", "timeout" }, r.GetProperty("geo").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(new[] { "Loc_pending", "Loc_denied", "Loc_unavailable", "Loc_timeout" }, r.GetProperty("loc").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal("Loc_inaccurate", r.GetProperty("locBad").GetProperty("labelKey").GetString());
        Assert.Equal("Loc_ok", r.GetProperty("locOk").GetProperty("labelKey").GetString());
        Assert.Equal(12, r.GetProperty("locOk").GetProperty("args")[0].GetInt32());
        Assert.False(r.GetProperty("locOk").GetProperty("required").GetBoolean());
    }

    // ═══ 2 · the page in a fake DOM (Node) ══════════════════════════════════════════════════════════════════

    [Fact]
    public void A6_start_opens_E3_and_posts_the_evidence_then_the_presentation_opens()
    {
        var smoke = Path.Combine(Path.GetDirectoryName(WebRoot())!, "Diten.Web.Tests", "Js", "visit-workspace-start-smoke.js").Replace("\\", "/");
        var crm = Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM").Replace("\\", "/");
        var tr = Resx("tr");
        var textsFile = Path.Combine(Path.GetTempPath(), "vw3-tr-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(textsFile, JsonSerializer.Serialize(VisitWorkspaceIndex.ScriptKeys.ToDictionary(k => k, k => tr[k])), new UTF8Encoding(false));
        string? node;
        try
        {
            node = RunNode("process.argv[2] = '" + crm + "'; process.argv[3] = '" + textsFile.Replace("\\", "/") + "'; require('" + smoke + "');");
        }
        finally
        {
            File.Delete(textsFile);
        }

        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement;
        string S(string name) => r.GetProperty(name).GetString()!;
        bool B(string name) => r.GetProperty(name).GetBoolean();

        Assert.Equal(JsonValueKind.Null, r.GetProperty("createError").ValueKind); // the CT order stays (lookup before create)
        Assert.Contains("vw-st-in_progress", S("liveCard"));
        Assert.Contains("vw-live", S("liveCard"));
        Assert.Contains(">7 dk<", S("liveCard"));

        // the today visit: Start (primary, full width) next to not done / reschedule / cancel
        Assert.Contains("data-start=\"1\"", S("todayActions"));
        Assert.Contains("vw-act-full", S("todayActions"));
        Assert.Contains("Ziyarete başla", S("todayActions"));
        Assert.Contains("data-dialog=\"notDone\"", S("todayActions"));
        Assert.Contains("data-dialog=\"reschedule\"", S("todayActions"));
        Assert.Contains("data-dialog=\"cancel\"", S("todayActions"));
        Assert.Contains("Kuruma vardığınızda ziyarete başlayın; kanıt adımından sonra sunum açılır.", S("todayBand"));

        // E3: open, the names isolated, photo required (the setting)
        Assert.True(B("e3Shown"));
        Assert.Contains("<bdi>Dr. Kerem Aslan</bdi>", S("target"));
        Assert.Equal("· zorunlu", S("photoNeed"));

        // the location refused: its words, the retry, "Missing: photo, location", Start disabled
        Assert.Equal("İzin verilmedi", S("deniedChip"));
        Assert.Contains("Tarayıcı konum iznini reddetti.", S("deniedText"));
        Assert.Contains("Konum zorunlu: konum alınmadan ziyaret başlatılamaz.", S("deniedText"));
        Assert.True(B("deniedRetry"));
        Assert.Equal("Eksik: fotoğraf, konum", S("deniedHint"));
        Assert.True(B("deniedBeginDisabled"));
        Assert.StartsWith("Kamera izni verilmedi.", S("camDeniedText")); // the webcam refused: the upload stays

        // a photo file → JPEG, the longest edge 1600 px, quality 0.85
        var jpeg = r.GetProperty("jpeg");
        Assert.Equal("image/jpeg", jpeg.GetProperty("type").GetString());
        Assert.Equal(0.85, jpeg.GetProperty("quality").GetDouble());
        Assert.Equal(1600, jpeg.GetProperty("width").GetInt32());
        Assert.Equal(1200, jpeg.GetProperty("height").GetInt32());
        Assert.True(B("photoTaken"));
        Assert.Equal("Eksik: konum", S("photoHint"));
        Assert.True(B("photoBeginDisabled"));

        // the location granted → Start enabled, the server-time hint
        Assert.Equal("Konum alındı", S("okChip"));
        Assert.Contains("kuruma uzaklığı sunucu ölçer", S("okText"));
        Assert.Equal("Başlangıç saati sunucudan alınır.", S("readyHint"));
        Assert.True(B("readyBeginEnabled"));

        // POST start: multipart (no JSON Content-Type of ours), the fields in the invariant culture, the JPEG
        Assert.Equal("POST /api/execution/start", S("startUrl"));
        Assert.Equal(JsonValueKind.Null, r.GetProperty("startContentType").ValueKind);
        var fields = r.GetProperty("startFields").EnumerateArray().Select(f => f[0].GetString() + "=" + f[1].GetString()).ToArray();
        Assert.Equal(new[]
        {
            "plannedVisitId=v1", "kind=in_person", "latitude=41.0123", "longitude=28.9876", "accuracyMeters=15.4",
            "photo=blob:image/jpeg:1600x1200"
        }, fields);
        Assert.Equal("visit-photo.jpg", r.GetProperty("startFields")[5][2].GetString());

        // 409 too far: "Kurumdan 3,4 km uzaktasınız" + the setting's hint; nothing opened
        Assert.True(B("farErrorShown"));
        Assert.Contains("Kurumdan ⁨3,4 km⁩ uzaktasınız.", S("farError"));
        Assert.Contains("Konum zorunlu: kuruma yaklaşınca başlatabilirsiniz.", S("farError"));
        Assert.Equal(0, r.GetProperty("assignedAfterFar").GetInt32());

        // success → the presentation page
        Assert.Equal(new[] { "/CRM/VisitPresentation?plannedVisitId=v1" }, r.GetProperty("assigned").EnumerateArray().Select(x => x.GetString()));

        // in_progress: back to the presentation / finish (the summary), "Devam ediyor · 7 dk"
        Assert.Contains("href=\"/CRM/VisitPresentation?plannedVisitId=v2\"", S("liveActions"));
        Assert.Contains("href=\"/CRM/VisitPresentation?plannedVisitId=v2&amp;phase=summary\"", S("liveActions"));
        Assert.Contains("Sunuma dön", S("liveActions"));
        Assert.Contains("Ziyareti bitir", S("liveActions"));
        Assert.Contains("Devam ediyor · 7 dk", S("liveBand"));
        Assert.Contains("(sunucu saati)", S("liveBand"));

        var calls = r.GetProperty("calls").EnumerateArray().Select(c => c.GetString()!).ToList();
        Assert.Contains("GET /api/execution/contract", calls);
    }

    [Fact]
    public void A7_the_dialog_draws_no_design_note_no_qr_no_anti_fraud_and_keeps_the_rep_away_from_play_and_targeting()
    {
        var view = ViewSource();
        var e3 = view[view.IndexOf("id=\"vw-e3\"", StringComparison.Ordinal)..view.IndexOf("WP-VW-W2 (WEB-b) — apply products", StringComparison.Ordinal)];
        Assert.True(view.IndexOf("@if (Model.CanExecute)", StringComparison.Ordinal) < view.IndexOf("id=\"vw-e3\"", StringComparison.Ordinal));
        Assert.Contains("modal-fullscreen-md-down", e3); // a phone: full screen
        Assert.Contains("Localizer[\"E3Kvkk\"]", e3);
        Assert.Contains("id=\"vw-e3-webcam\"", e3);
        Assert.Contains("id=\"vw-e3-upload\"", e3);
        Assert.Contains("Localizer[\"E3StartValue\"]", e3);
        // WP-VW-W3E (W3E-WEB-a) — the QR section and the web device line belong to the window now (S1–S8); they are
        // drawn only with contract.policy.qrMode / antiFraudMode (VisitWorkspaceStartW3EQrWebTests). No design note ever.
        foreach (var banned in new[] { "anti-fraud", "antifraud", "kiracı ayarı", "tenant setting", "photoReq", "locReq" })
        {
            Assert.DoesNotContain(banned, e3, StringComparison.OrdinalIgnoreCase);
        }

        var all = Script("workspace-core.js") + Script("visit-workspace.js");
        Assert.DoesNotContain("strategyTemplate", all, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("campaign", all, StringComparison.OrdinalIgnoreCase);
        // nothing of the visit is kept in the browser (the photo stays a blob in memory)
        Assert.DoesNotContain("localStorage.setItem", Script("visit-workspace.js"));
    }

    // ═══ 3 · proxies ════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task A8_start_is_streamed_as_multipart_only_with_the_record_key()
    {
        const string boundary = "----vw3boundary";
        var body = "--" + boundary + "\r\nContent-Disposition: form-data; name=\"plannedVisitId\"\r\n\r\n" + Id + "\r\n"
                   + "--" + boundary + "\r\nContent-Disposition: form-data; name=\"photo\"; filename=\"visit-photo.jpg\"\r\nContent-Type: image/jpeg\r\n\r\nJPEGDATA\r\n"
                   + "--" + boundary + "--\r\n";
        var contentType = "multipart/form-data; boundary=" + boundary;

        var (gateway, controller) = Arrange([ExecRecord], body, contentType);
        var result = await controller.StartVisit(CancellationToken.None);
        Assert.Equal(200, Status(result));
        var sent = Assert.Single(gateway.Requests);
        Assert.Equal("POST", sent.Method);
        Assert.Equal(GatewayUrl + "/api/crm/visit-execution/start", sent.Uri);
        Assert.Equal(TenantId.ToString(), sent.Tenant);
        // W3E-BE-b (K-W3E-10): the body is rebuilt — every field as it came, plus clientPlatform=web (a new boundary)
        Assert.StartsWith("multipart/form-data", sent.ContentType);
        var parts = await PartsAsync(sent.ContentType!, sent.Body!);
        Assert.Equal(new[] { "plannedVisitId", "photo", "clientPlatform", "appVersion" }, parts.Select(x => x.Name).ToArray()); // + W3E-BE-c (D-2)
        Assert.Equal(Id, parts[0].Value);
        Assert.Equal(("JPEGDATA", "visit-photo.jpg", "image/jpeg"), (parts[1].Value, parts[1].FileName, parts[1].ContentType));
        Assert.Equal("web", parts[2].Value);

        // without the record key: 403, nothing reaches the gateway (the read key is not enough)
        var (noKeyGateway, noKey) = Arrange([ExecRead, ReportRead, PlanRead], body, contentType);
        Assert.Equal(403, Status(await noKey.StartVisit(CancellationToken.None)));
        Assert.Empty(noKeyGateway.Requests);

        // anything but multipart is refused here
        var (jsonGateway, json) = Arrange([ExecRecord], "{\"plannedVisitId\":\"" + Id + "\"}", "application/json");
        Assert.Equal(400, Status(await json.StartVisit(CancellationToken.None)));
        Assert.Empty(jsonGateway.Requests);
    }

    [Fact]
    public async Task A8_the_execution_contract_needs_the_execution_read_key()
    {
        var (gateway, controller) = Arrange([ExecRead], "", null);
        Assert.Equal(200, Status(await controller.ExecutionContract(CancellationToken.None)));
        var sent = Assert.Single(gateway.Requests);
        Assert.Equal("GET", sent.Method);
        Assert.Equal(GatewayUrl + "/api/crm/visit-execution/contract", sent.Uri);

        var (noGateway, no) = Arrange([ExecRecord, ReportRead, PlanRead], "", null);
        Assert.Equal(403, Status(await no.ExecutionContract(CancellationToken.None)));
        Assert.Empty(noGateway.Requests);

        Assert.Equal("crm.visit-execution.read", VisitWorkspaceController.VisitExecutionRead);
        Assert.Equal("crm.visit-execution.record", VisitWorkspaceController.VisitExecutionRecord);
    }

    [Fact]
    public void A8_the_page_offers_the_start_only_with_both_execution_keys_and_uas001_stays()
    {
        static VisitWorkspaceIndexViewModel M(params string[] keys)
            => Assert.IsType<VisitWorkspaceIndexViewModel>(Assert.IsType<ViewResult>(Arrange(keys, "", null).Controller.Index()).Model);

        Assert.True(M(ReportRead, PlanRead, ExecRead, ExecRecord).CanExecute);
        Assert.False(M(ReportRead, PlanRead, ExecRead).CanExecute);
        Assert.False(M(ReportRead, PlanRead, ExecRecord).CanExecute);
        // the execution keys never open the page on their own (UAS-001: the read keys decide)
        var only = M(ExecRead, ExecRecord);
        Assert.False(only.CanRead);
        Assert.Contains("data-can-execute=\"@Model.CanExecute", ViewSource());
        Assert.True(ViewSource().IndexOf("@if (!Model.CanRead)", StringComparison.Ordinal) < ViewSource().IndexOf("id=\"vw-root\"", StringComparison.Ordinal));
    }

    // ═══ 4 · seven languages + RTL ══════════════════════════════════════════════════════════════════════════

    private static readonly Dictionary<string, string[]> Placeholders = new()
    {
        ["AlertTitle_in_progress"] = ["{0}"], ["AlertText_in_progress"] = ["{1}"], ["CardLiveMinutes"] = ["{0}"], ["E3Missing"] = ["{0}"],
        ["Loc_okDetail"] = ["{0}"], ["Loc_inaccurateDetail"] = ["{0}", "{1}"], ["DistanceMeters"] = ["{0}"], ["DistanceKm"] = ["{0}"],
        ["StartErr_visit_location_too_far"] = ["{0}"]
    };

    [Fact]
    public void A9_seven_languages_no_echo_no_stray_placeholder_and_arabic_rtl()
    {
        var w3 = VisitWorkspaceIndex.ScriptKeys.Where(k => k.StartsWith("StartErr_", StringComparison.Ordinal) || k.StartsWith("Loc_", StringComparison.Ordinal)
                || k.StartsWith("Cam_", StringComparison.Ordinal) || k.StartsWith("E3", StringComparison.Ordinal) || k.StartsWith("Photo_", StringComparison.Ordinal)
                || k.Contains("in_progress", StringComparison.Ordinal) || k.StartsWith("Distance", StringComparison.Ordinal))
            .Concat(W3ScriptKeys).Concat(VisitWorkspaceIndex.ViewKeys.Where(k => k.StartsWith("E3", StringComparison.Ordinal))).Distinct().ToList();
        Assert.True(w3.Count >= 60, w3.Count.ToString());

        foreach (var culture in new[] { "", "en", "tr", "fr", "es", "zh", "ar", "ru" })
        {
            var resx = Resx(culture);
            foreach (var key in w3)
            {
                Assert.True(resx.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value), $"{culture}:{key}");
                Assert.NotEqual(key, value);
                var expected = Placeholders.TryGetValue(key, out var p) ? p : [];
                foreach (var ph in expected) Assert.Contains(ph, value);
                if (expected.Length == 0) Assert.DoesNotContain("{", value); // an argless text never carries {0}
            }
        }

        var tr = Resx("tr");
        Assert.Equal("Ziyarete başla", tr["Action_start"]);
        Assert.Equal("Sunuma dön", tr["Action_presentation"]);
        Assert.Equal("Ziyareti bitir", tr["Action_finish"]);
        Assert.Equal("Sunumu başlat", tr["E3Begin"]);
        Assert.Equal("Kurum girişi ya da bekleme alanı. Doktorun ya da hastaların yüzü çekilmez (KVKK).", tr["E3Kvkk"]);
        Assert.Equal("Kurumdan {0} uzaktasınız.", tr["StartErr_visit_location_too_far"]);

        var ar = Resx("ar");
        Assert.All(new[] { "Action_start", "E3Title", "E3Kvkk", "Loc_denied", "StartErr_visit_location_too_far" },
            k => Assert.Matches("[؀-ۿ]", ar[k]));

        // RTL: the names isolated, the numbers isolated, the dialog's sides logical
        var js = Script("visit-workspace.js");
        Assert.Contains("F.bidi(v.targetDisplayName || '—'), v.accountDisplayName ? F.bidi(v.accountDisplayName)", js);
        Assert.Contains("F.isolate(distance)", js);
        Assert.Contains("lv.args.map(x => F.isolate(x))", js);
        var css = File.ReadAllText(Path.Combine(ScriptDir(), "visit-workspace.css"));
        var e3Css = css[css.IndexOf("WP-VW-W3 (WEB-a) — E3", StringComparison.Ordinal)..];
        Assert.DoesNotContain("margin-left", e3Css);
        Assert.DoesNotContain("padding-left", e3Css);
        Assert.DoesNotContain(" left:", e3Css);
        Assert.DoesNotContain(" right:", e3Css);
        Assert.Contains("inset-inline", e3Css);     // the overlays sit by logical sides (mirror in Arabic)
        Assert.DoesNotContain("translateX", e3Css); // nothing pushed by a physical offset
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────

    private const string Id = "11111111-1111-1111-1111-111111111111";

    private static int? Status(IActionResult result) => result switch
    {
        ContentResult c => c.StatusCode,
        ObjectResult o => o.StatusCode ?? 200,
        StatusCodeResult s => s.StatusCode,
        _ => null
    };

    private static (Gateway Gateway, VisitWorkspaceController Controller) Arrange(string[] permissions, string body, string? contentType)
    {
        var gateway = new Gateway();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = GatewayUrl }).Build();
        var controller = new VisitWorkspaceController(new HttpClient(gateway), configuration, NullLogger<VisitWorkspaceController>.Instance);
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        var bytes = Encoding.UTF8.GetBytes(body);
        http.Request.Body = new MemoryStream(bytes);
        http.Request.ContentLength = bytes.Length;
        if (contentType is not null) http.Request.ContentType = contentType;
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return (gateway, controller);
    }

    private static string Core()
    {
        var file = Path.Combine(ScriptDir(), "workspace-core.js").Replace("\\", "/");
        return "const C = require('" + file + "');\n";
    }

    /// <summary>Runs <paramref name="code"/> in Node; null when Node is not on the machine (the code assertions stand alone).</summary>
    private static string? RunNode(string code)
    {
        var temp = Path.Combine(Path.GetTempPath(), "vw3-" + Guid.NewGuid().ToString("N") + ".js");
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

    private static string ScriptDir() => Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", "VisitWorkspace");

    private static string Script(string file) => File.ReadAllText(Path.Combine(ScriptDir(), file)).Replace("\r\n", "\n");

    private static string ViewSource() => File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "VisitWorkspace", "Index.cshtml")).Replace("\r\n", "\n");

    private static Dictionary<string, string> Resx(string culture)
    {
        var file = "VisitWorkspaceIndex" + (culture.Length == 0 ? "" : "." + culture) + ".resx";
        return XDocument.Load(Path.Combine(WebRoot(), "Resources", "Views", "CRM", "VisitWorkspace", file)).Root!
            .Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")?.Value ?? string.Empty, StringComparer.Ordinal);
    }

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }

    private sealed class Gateway : HttpMessageHandler
    {
        public List<(string Method, string Uri, string? Tenant, string? ContentType, string? Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request.Method.Method, request.RequestUri!.ToString(),
                request.Headers.TryGetValues("X-Tenant-Id", out var t) ? t.Single() : null,
                request.Content?.Headers.ContentType?.ToString(),
                request.Content is null ? null : await request.Content.ReadAsStringAsync(ct)));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":{}}", Encoding.UTF8, "application/json")
            };
        }
    }
}
