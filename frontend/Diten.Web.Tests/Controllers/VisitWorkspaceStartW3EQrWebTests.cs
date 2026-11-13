using System.Text;
using System.Text.Json;
using Diten.Web.Views.CRM.VisitWorkspace;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VW-W3E · W3E-WEB-a — the institution QR at the visit start (S1–S8) on the REAL workspace scripts, view and resx:
/// (1) the pure rules: the mode (off / remote → nothing), "Missing: QR code" and the Start button for a required QR
/// (ok, no code printed, read-unchecked let it go; idle, wrong, invalid do not), optional never blocks, off is the W3
/// window unchanged, the start fields (the text as read + the scan instant), the QR refusals in the user's language;
/// (2) the window in a fake DOM: required (idle → "I can't find" → back → wrong institution → ok via the parent → start),
/// required with no code printed, optional (skip), off (no box, no device line, no qr-check), camera refused (S5).
/// </summary>
public sealed partial class VisitWorkspaceStartWebTests
{
    [Fact]
    public void W3E_Q1_a_required_qr_blocks_the_start_until_read_optional_never_blocks_and_off_is_the_w3_window()
    {
        var node = RunNode(Core() + """
            const pol = m => ({ locationMode: 'info', photoMode: 'optional', qrMode: m });
            const q = state => ({ state });
            const out = {};
            for (const st of ['idle', 'ok', 'noPrinted', 'scanned', 'wrong', 'invalid', 'unverifiable', 'nocodeReq', 'skipped', 'checking'])
              out[st] = { missing: C.startMissing(pol('required'), { qr: q(st) }).join('+'), begin: C.canBegin(pol('required'), { qr: q(st) }), optional: C.canBegin(pol('optional'), { qr: q(st) }) };
            out.modes = [C.qrMode(null), C.qrMode({}), C.qrMode(pol('off')), C.qrMode(pol('weird')), C.qrMode(pol('optional')), C.qrMode(pol('required')), C.qrMode(pol('required'), 'remote')];
            out.remoteMissing = C.startMissing(pol('required'), { kind: 'remote', qr: q('idle') }).join('+');
            out.offMissing = C.startMissing({ locationMode: 'required', photoMode: 'required' }, { qr: q('idle') }).join('+');
            out.noPolicy = C.startMissing(null, {}).join('+');
            out.device = [C.showDeviceLine({ antiFraudMode: 'flag' }), C.showDeviceLine({ antiFraudMode: 'off' }), C.showDeviceLine(null)];
            process.stdout.write(JSON.stringify(out));
            """);
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement;
        foreach (var go in new[] { "ok", "noPrinted", "scanned" })
        {
            Assert.Equal(("", true), (r.GetProperty(go).GetProperty("missing").GetString(), r.GetProperty(go).GetProperty("begin").GetBoolean()));
        }

        foreach (var stop in new[] { "idle", "wrong", "invalid", "unverifiable", "nocodeReq", "skipped", "checking" })
        {
            Assert.True(r.GetProperty(stop).GetProperty("missing").GetString() == "qr", stop);  // S8: "Missing: QR code"
            Assert.False(r.GetProperty(stop).GetProperty("begin").GetBoolean(), stop);           // the start stays off
        }

        foreach (var any in new[] { "idle", "ok", "wrong", "invalid", "skipped" })
        {
            Assert.True(r.GetProperty(any).GetProperty("optional").GetBoolean(), any);          // optional never blocks
        }

        Assert.Equal("[\"off\",\"off\",\"off\",\"off\",\"optional\",\"required\",\"off\"]", r.GetProperty("modes").GetRawText());
        Assert.Equal("", r.GetProperty("remoteMissing").GetString());                          // a remote visit: no QR
        Assert.Equal("photo+location", r.GetProperty("offMissing").GetString());               // off: the W3 rules exactly
        Assert.Equal("location", r.GetProperty("noPolicy").GetString());
        Assert.Equal("[true,false,false]", r.GetProperty("device").GetRawText());
    }

    [Fact]
    public void W3E_Q2_the_box_follows_the_servers_pre_check_and_the_fields_carry_the_text_as_read()
    {
        var node = RunNode(Core() + """
            const v = (c, m) => C.qrView(c, m || 'required');
            const fields = qr => C.startFields('p1', { location: { status: 'denied' }, qr }).map(f => f[0] + '=' + f[1]).join('&');
            process.stdout.write(JSON.stringify({
              ok: v({ status: 'ok', accountName: 'Acıbadem', matchedVia: 'parent' }), self: v({ status: 'ok', matchedVia: 'self' }),
              wrong: v({ status: 'wrong_account', accountName: 'Ankara Şehir Hastanesi' }), revoked: v({ status: 'revoked' }).state, invalid: v({ status: 'invalid' }).state,
              unv: v({ status: 'unverifiable', wouldRefuse: true }), notIssuedReq: v({ status: 'not_issued' }).state, notIssuedOpt: v({ status: 'not_issued' }, 'optional').state,
              missing: v({ status: 'missing' }).state, none: v(null).state, na: v({ status: 'not_applicable' }).state,
              nocode: [C.qrNoCodeState('optional', null), C.qrNoCodeState('required', { expectedHasActiveCode: true }), C.qrNoCodeState('required', { expectedHasActiveCode: false }), C.qrNoCodeState('required', null)],
              withQr: fields({ state: 'ok', payload: 'DQR1.x y', scannedAt: '2026-10-14T08:31:00.000Z' }),
              skipped: fields({ state: 'skipped', payload: 'DQR1.x' }), none2: fields({ state: 'idle' }), noQr: fields(undefined),
              errors: C.QR_START_CODES.map(code => C.startError({ status: 409, body: { errors: ['x', code] } }).key), startCodes: C.START_CODES.length
            }));
            """);
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement;
        Assert.Equal(("ok", "Acıbadem", true), (r.GetProperty("ok").GetProperty("state").GetString(), r.GetProperty("ok").GetProperty("accountName").GetString(), r.GetProperty("ok").GetProperty("viaParent").GetBoolean()));
        Assert.False(r.GetProperty("self").GetProperty("viaParent").GetBoolean());
        Assert.Equal(("wrong", "Ankara Şehir Hastanesi"), (r.GetProperty("wrong").GetProperty("state").GetString(), r.GetProperty("wrong").GetProperty("accountName").GetString()));
        Assert.Equal(("invalid", "invalid"), (r.GetProperty("revoked").GetString(), r.GetProperty("invalid").GetString())); // S4: invalid / revoked
        Assert.Equal("unverifiable", r.GetProperty("unv").GetProperty("state").GetString());
        Assert.Equal(("noPrinted", "idle", "idle", "idle", "off"), (r.GetProperty("notIssuedReq").GetString(), r.GetProperty("notIssuedOpt").GetString(),
            r.GetProperty("missing").GetString(), r.GetProperty("none").GetString(), r.GetProperty("na").GetString()));
        Assert.Equal("[\"nocodeOpt\",\"nocodeReq\",\"noPrinted\",\"nocodeReq\"]", r.GetProperty("nocode").GetRawText()); // S6
        Assert.Equal("plannedVisitId=p1&kind=in_person&locationError=denied&qrPayload=DQR1.x y&qrScannedAt=2026-10-14T08:31:00.000Z", r.GetProperty("withQr").GetString());
        Assert.Equal("plannedVisitId=p1&kind=in_person&locationError=denied", r.GetProperty("skipped").GetString());
        Assert.Equal(r.GetProperty("skipped").GetString(), r.GetProperty("none2").GetString());
        Assert.Equal(r.GetProperty("skipped").GetString(), r.GetProperty("noQr").GetString());
        Assert.Equal(new[] { "StartErr_visit_qr_required", "StartErr_visit_qr_invalid", "StartErr_visit_qr_wrong_account", "StartErr_visit_qr_revoked", "StartErr_visit_qr_signing_unavailable" },
            r.GetProperty("errors").EnumerateArray().Select(x => x.GetString()).ToArray());
        Assert.All(r.GetProperty("errors").EnumerateArray(), k => Assert.Contains(k.GetString()!, VisitWorkspaceIndex.ScriptKeys));
        Assert.Equal(14, r.GetProperty("startCodes").GetInt32());                               // the W3 list unchanged
    }

    [Theory]
    [InlineData("required")]
    [InlineData("requiredNoCode")]
    [InlineData("optional")]
    [InlineData("off")]
    [InlineData("denied")]
    public void W3E_Q3_the_start_window_scans_checks_and_starts_as_the_mockup(string scenario)
    {
        var r = QrSmoke(scenario);
        if (r is null) { return; }
        var root = r.Value;
        var open = root.GetProperty("open");
        string Box(string step) => root.GetProperty(step).GetProperty("box").GetString()!;
        var fields = root.GetProperty("startFields").ValueKind == JsonValueKind.Null ? Array.Empty<string>()
            : root.GetProperty("startFields").EnumerateArray().Select(f => f[0].GetString() + "=" + f[1].GetString()).ToArray();
        var checks = root.GetProperty("qrChecks").EnumerateArray().Select(c => c.GetRawText()).ToArray();
        switch (scenario)
        {
            case "required":
                Assert.Equal("· zorunlu", root.GetProperty("need").GetString());
                Assert.Contains("Kabuldeki QR kodunu okutun.", Box("open"));                       // S8
                Assert.Contains("Kodu bulamıyorum", Box("open"));
                Assert.Equal(("Eksik: QR kodu", true), (open.GetProperty("hint").GetString(), open.GetProperty("beginDisabled").GetBoolean()));
                Assert.True(open.GetProperty("device").GetBoolean());                                 // S7: antiFraudMode flag
                Assert.Contains("İstisna için yöneticinize başvurun", Box("nocode"));                 // S6 required + a code exists
                Assert.Contains("Bu kod başka bir kuruma ait (<bdi>Ankara Şehir Hastanesi</bdi>)", Box("wrong")); // S3
                Assert.True(root.GetProperty("wrong").GetProperty("beginDisabled").GetBoolean());
                Assert.Contains("<bdi>Acıbadem Altunizade Hastanesi</bdi> · kod doğrulandı", Box("ok")); // S2
                Assert.Contains("<bdi>Göğüs Hastalıkları Polikliniği</bdi> için üst kurumun kodu · okundu", Box("ok"));
                Assert.False(root.GetProperty("ok").GetProperty("beginDisabled").GetBoolean());
                Assert.Contains("qrPayload=DQR1.read-as-is", fields);                                 // sent as read, never parsed
                Assert.Contains(fields, f => f.StartsWith("qrScannedAt=", StringComparison.Ordinal));
                Assert.Equal(new[] { "{\"plannedVisitId\":\"v1\"}", "{\"plannedVisitId\":\"v1\",\"qrPayload\":\"DQR1.read-as-is\"}", "{\"plannedVisitId\":\"v1\",\"qrPayload\":\"DQR1.read-as-is\"}" }, checks);
                Assert.All(root.GetProperty("constraints").EnumerateArray(), c => Assert.Equal("environment", c.GetProperty("video").GetProperty("facingMode").GetProperty("ideal").GetString())); // the back camera
                Assert.Equal(2, root.GetProperty("streamStopped").GetInt32());                         // the camera off after each read
                Assert.Equal("[\"/CRM/VisitPresentation?plannedVisitId=v1\"]", root.GetProperty("assigned").GetRawText());
                break;
            case "requiredNoCode":
                Assert.Contains("Bu kuruma henüz QR kodu basılmadı.", Box("open"));                   // S6 no code printed: no penalty
                Assert.False(open.GetProperty("beginDisabled").GetBoolean());
                Assert.DoesNotContain(fields, f => f.StartsWith("qr", StringComparison.Ordinal));
                break;
            case "optional":
                Assert.Equal("· isteğe bağlı", root.GetProperty("need").GetString());
                Assert.False(open.GetProperty("beginDisabled").GetBoolean());
                Assert.Contains("QR olmadan devam et", Box("nocode"));
                Assert.Contains("QR olmadan devam ediliyor.", Box("after"));
                Assert.False(root.GetProperty("wrong").GetProperty("beginDisabled").GetBoolean());     // optional: a flag, not a block
                break;
            case "off":
                Assert.False(open.GetProperty("section").GetBoolean());                               // no box at all
                Assert.False(open.GetProperty("device").GetBoolean());
                Assert.Equal(0, root.GetProperty("calls").EnumerateArray().Count(c => c.GetString()!.Contains("qr-check", StringComparison.Ordinal)));
                Assert.Equal(new[] { "plannedVisitId=v1", "kind=in_person", "latitude=41.0123", "longitude=28.9876", "accuracyMeters=15" }, fields); // the W3 start
                break;
            case "denied":
                Assert.Contains("Kameraya erişim izni yok", Box("denied"));                            // S5
                Assert.Contains("Adres çubuğundaki kilit simgesine dokunun.", Box("denied"));
                Assert.Contains("Tekrar dene", Box("denied"));
                Assert.Contains("Kodu bulamıyorum", Box("denied"));
                Assert.True(root.GetProperty("denied").GetProperty("beginDisabled").GetBoolean());
                break;
        }
    }

    [Fact]
    public void W3E_Q4_the_view_draws_the_qr_box_hidden_the_camera_is_local_and_the_page_never_parses_the_text()
    {
        var view = ViewSource();
        var e3 = view[view.IndexOf("id=\"vw-e3\"", StringComparison.Ordinal)..view.IndexOf("WP-VW-W2 (WEB-b) — apply products", StringComparison.Ordinal)];
        Assert.Contains("<section class=\"vw-e3-qr d-none\" id=\"vw-e3-qr\">", e3);                 // hidden until the setting says so
        Assert.Contains("<div class=\"vw-e3-device d-none\" id=\"vw-e3-device\">", e3);
        Assert.Contains("data-jsqr=\"@Url.Content(\"~/assets/vendor/libs/jsqr/jsQR.js\")\"", view);   // local, no CDN
        Assert.Contains("id=\"vw-e3-qr-full\"", e3);                                                  // the phone: full screen
        var js = Script("visit-workspace.js");
        Assert.Contains("post('/execution/qr-check'", js);
        Assert.Contains("window.BarcodeDetector", js);
        Assert.Contains("window.jsQR", js);
        Assert.DoesNotContain("https://", js);
        Assert.DoesNotContain("split('.')", js[js.IndexOf("function qrFound", StringComparison.Ordinal)..js.IndexOf("function qrStopCamera", StringComparison.Ordinal)]); // never parsed
        var css = File.ReadAllText(Path.Combine(ScriptDir(), "visit-workspace.css"));
        var qrCss = css[css.IndexOf("WP-VW-W3E (W3E-WEB-a, S1–S8)", StringComparison.Ordinal)..];
        foreach (var physical in new[] { "margin-left", "padding-left", " left:", " right:", "margin-right", "padding-right" })
        {
            Assert.DoesNotContain(physical, qrCss);                                                     // Arabic mirrors
        }
    }

    private static JsonElement? QrSmoke(string scenario)
    {
        var smoke = Path.Combine(Path.GetDirectoryName(WebRoot())!, "Diten.Web.Tests", "Js", "visit-workspace-qr-smoke.js").Replace("\\", "/");
        var crm = Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM").Replace("\\", "/");
        var tr = Resx("tr");
        var textsFile = Path.Combine(Path.GetTempPath(), "vw3e-tr-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(textsFile, JsonSerializer.Serialize(VisitWorkspaceIndex.ScriptKeys.ToDictionary(k => k, k => tr[k])), new UTF8Encoding(false));
        try
        {
            var node = RunNode("process.argv[2] = '" + crm + "'; process.argv[3] = '" + textsFile.Replace("\\", "/") + "'; process.argv[4] = '" + scenario + "'; require('" + smoke + "');");
            return node is null ? null : JsonDocument.Parse(node).RootElement.Clone();
        }
        finally
        {
            File.Delete(textsFile);
        }
    }
}
