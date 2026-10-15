using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Controllers.CRM;
using Diten.Web.Models.CRM;
using Diten.Web.Views.CRM.VisitEvidenceFlags;
using Diten.Web.Views.CRM.VisitEvidenceSettings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VW-W3E · W3E-WEB-b — on the REAL controllers, views, resx and scripts: (1) the settings page's "additional evidence"
/// (E1: QR off / optional / required, device off / flag + a disabled "block · soon", the PUT carries both, "block" is never
/// taken), the qr_optional exception rule (E2), the effective / reps / history fields in the user's words (E3, never the raw
/// field name); (2) the manager's visit evidence flags page (M1–M6): UAS-001 and the flags, every proxy → its CRM path with
/// its keys, the pure rules in Node (the period ≤ 92 days, the query, "QR not scanned" is not a flag, the bars + "Other",
/// the chips, the drawer lines), the page in a fake DOM (team / tenant-wide / empty), seven languages, Arabic, RTL, dark.
/// </summary>
public sealed partial class VisitEvidenceFlagsWebTests
{
    private const string GatewayUrl = "http://gateway.test";
    private static readonly Guid TenantId = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");
    private static readonly string[] Cultures = ["", "en", "tr", "fr", "es", "zh", "ar", "ru"];
    private static readonly string[] AllKeys = VisitEvidenceFlagsIndex.ScriptKeys.Concat(VisitEvidenceFlagsIndex.ViewKeys).Distinct().ToArray();
    private const string Read = "crm.visit-evidence-flags.read";
    private const string ReadAll = "crm.visit-evidence-flags.read-all";
    private static readonly string[] PhotoKeys = ["crm.visit-execution.read", "crm.planned-visit.read-all", "platform.document-repository.visit-evidence.read-all"];
    private const string Id = "11111111-1111-1111-1111-111111111111";

    // ═══ 1 · the settings page (E1–E3) ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void B1_the_additional_evidence_rules_block_is_never_selectable_and_the_put_body_carries_both_fields()
    {
        var node = RunNode(SettingsCore() + """
            const put = C.putBody({ locationMode: 'required', radiusMeters: 300, maxAccuracyMeters: 150, photoMode: 'optional', doctorConfirmation: true,
              durationConsistency: true, shortVisitSeconds: 120, accountWithoutLocation: 'allow_flagged', qrMode: 'optional', antiFraudMode: 'flag' }, 7);
            const forged = C.putBody({ locationMode: 'required', radiusMeters: 300, maxAccuracyMeters: 150, photoMode: 'optional', doctorConfirmation: true,
              durationConsistency: true, shortVisitSeconds: 120, accountWithoutLocation: 'allow_flagged', qrMode: 'required', antiFraudMode: 'block' }, 7);
            process.stdout.write(JSON.stringify({
              put, forged,
              block: C.canSelect('antiFraudMode', 'block'), flag: C.canSelect('antiFraudMode', 'flag'), qr: ['off','optional','required'].map(v => C.canSelect('qrMode', v)),
              soon: C.EXTRA.antiFraudMode.soon, extraOpts: C.EXTRA.antiFraudMode.options, newFields: C.NEW_FIELDS,
              required: C.ruleState('qr_optional', [{ field: 'qrMode', value: 'required', source: 'tenant' }]),
              off: C.ruleState('qr_optional', [{ field: 'qrMode', value: 'off', source: 'tenant' }]),
              already: C.ruleState('qr_optional', [{ field: 'qrMode', value: 'optional', source: 'exception' }]),
              rules: C.RULES, newRules: C.NEW_RULES, cols: C.REP_COLUMNS,
              reps: C.repRows([{ resourceId: 'r1', displayName: 'A' }, { resourceId: 'r2', displayName: 'B' }], { locationMode: 'required', photoMode: 'required', qrMode: 'required', antiFraudMode: 'flag' },
                [{ resourceId: 'r1', status: 'approved', phase: 'active', rules: ['qr_optional'] }, { resourceId: 'r2', status: 'approved', phase: 'planned', rules: ['qr_optional'] }])
            }));
            """);
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement;
        Assert.Equal("optional", r.GetProperty("put").GetProperty("qrMode").GetString());
        Assert.Equal("flag", r.GetProperty("put").GetProperty("antiFraudMode").GetString());
        Assert.Equal("required", r.GetProperty("forged").GetProperty("qrMode").GetString());
        Assert.False(r.GetProperty("forged").TryGetProperty("antiFraudMode", out _)); // "block" is never sent
        Assert.False(r.GetProperty("block").GetBoolean());
        Assert.True(r.GetProperty("flag").GetBoolean());
        Assert.All(r.GetProperty("qr").EnumerateArray(), x => Assert.True(x.GetBoolean()));
        Assert.Equal(["block"], r.GetProperty("soon").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(["off", "flag", "block"], r.GetProperty("extraOpts").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(["qrMode", "antiFraudMode"], r.GetProperty("newFields").EnumerateArray().Select(x => x.GetString()));

        // E2 — qr_optional relaxes only a required QR
        Assert.True(r.GetProperty("required").GetProperty("enabled").GetBoolean());
        Assert.Equal("XRuleQrNotRequired", r.GetProperty("off").GetProperty("reason").GetString());
        Assert.Equal("XRuleAlready", r.GetProperty("already").GetProperty("reason").GetString());
        Assert.Contains("qr_optional", r.GetProperty("rules").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(["qr_optional"], r.GetProperty("newRules").EnumerateArray().Select(x => x.GetString()));

        // E3 — the reps pane: four columns; only an ACTIVE approved exception changes a cell (and says so)
        Assert.Equal(["locationMode", "photoMode", "qrMode", "antiFraudMode"], r.GetProperty("cols").EnumerateArray().Select(x => x.GetString()));
        var reps = r.GetProperty("reps").EnumerateArray().ToArray();
        Assert.Equal("optional", reps[0].GetProperty("cells").GetProperty("qrMode").GetProperty("value").GetString());
        Assert.True(reps[0].GetProperty("cells").GetProperty("qrMode").GetProperty("fromException").GetBoolean());
        Assert.Equal("required", reps[1].GetProperty("cells").GetProperty("qrMode").GetProperty("value").GetString());
        Assert.False(reps[1].GetProperty("cells").GetProperty("qrMode").GetProperty("fromException").GetBoolean());
    }

    [Fact]
    public void B2_the_settings_page_in_a_fake_dom_takes_optional_refuses_block_and_names_the_history_fields()
    {
        var r = SettingsSmoke("qr");
        if (r is null) { return; }
        var s = r.Value;
        Assert.Equal(JsonValueKind.Null, s.GetProperty("createError").ValueKind);
        var tr = SettingsResx("tr");

        // E1 — the card: the three QR choices with their effect sentence, the device segment with a DISABLED "block · soon"
        var extra = s.GetProperty("extra").GetString()!;
        foreach (var v in new[] { "off", "optional", "required" })
        {
            Assert.Contains("data-field=\"qrMode\" data-opt=\"" + v + "\"", extra);
            Assert.Contains(Encode(tr["D_qrMode_" + v]), extra);
        }
        Assert.Contains("data-field=\"antiFraudMode\" data-opt=\"off\"", extra);
        Assert.Contains("data-field=\"antiFraudMode\" data-opt=\"flag\"", extra);
        Assert.DoesNotContain("data-opt=\"block\"", extra);
        Assert.Matches("<button[^>]*disabled[^>]*data-soon=\"block\"", extra);
        Assert.Contains(Encode(tr["SoonBadge"]), extra);
        Assert.Contains("href=\"/CRM/VisitQrCodes\"", extra);
        Assert.Contains(Encode(tr["ManageQrLink"]), extra);

        // a click on "optional" takes it; a FORGED "block" click changes nothing
        Assert.Matches("ve-choice active[^\"]*\"[^>]*data-field=\"qrMode\" data-opt=\"optional\"", s.GetProperty("extraAfterOptional").GetString()!);
        Assert.Equal(s.GetProperty("extraAfterOptional").GetString(), s.GetProperty("extraAfterBlock").GetString());
        var put = s.GetProperty("put");
        Assert.Equal("optional", put.GetProperty("qrMode").GetString());
        Assert.Equal("flag", put.GetProperty("antiFraudMode").GetString());
        Assert.Contains(Encode(tr["F_qrMode"]), s.GetProperty("saveDiff").GetString()!);

        // E3 — the effective rules: the two new rows ("new"), the exception source on the QR row
        var eff = s.GetProperty("effective").GetString()!;
        Assert.Contains(Encode(tr["F_qrMode"]), eff);
        Assert.Contains(Encode(tr["F_antiFraudMode"]), eff);
        Assert.Contains("ve-row-new", eff);
        Assert.Contains(Encode(tr["NewBadge"]), eff);

        // E3 — the reps pane: the four columns, "optional · exception" for the rep under the QR exception
        Assert.True(s.GetProperty("repsShown").GetBoolean());
        var reps = s.GetProperty("reps").GetString()!;
        Assert.Contains("Elif Şahin", reps);
        Assert.Contains("<td class=\"ve-col-new\">" + Encode(tr["Opt_qrMode_optional"]) + " <span class=\"ve-mu\">· " + Encode(tr["SourceExceptionShort"]) + "</span></td>", reps);
        Assert.Contains(Encode(tr["Opt_antiFraudMode_flag"]), reps);

        // E3 — the history in the user's words: the field name and its values, never "qrMode" / "antiFraudMode"
        var history = s.GetProperty("history").GetString()!;
        Assert.Contains(Encode(tr["F_qrMode"]), history);
        Assert.Contains(Encode(tr["F_antiFraudMode"]), history);
        Assert.Contains(Encode(tr["Opt_qrMode_required"]), history);
        Assert.Contains(Encode(tr["Opt_antiFraudMode_flag"]), history);
        Assert.DoesNotContain("qrMode", Regex.Replace(history, "data-[a-z-]+=\"[^\"]*\"", ""));
        Assert.DoesNotContain("antiFraudMode", Regex.Replace(history, "data-[a-z-]+=\"[^\"]*\"", ""));

        // E2 — qr_optional ("new" + its description) offered for a required rep, closed with its reason for an off rep
        var req = s.GetProperty("rulesRequired").GetString()!;
        Assert.Contains("qr_optional", req);
        Assert.Contains(Encode(tr["XRule_qr_optional"]), req);
        Assert.Contains(Encode(tr["XRuleDesc_qr_optional"]), req);
        Assert.Contains(Encode(tr["NewBadge"]), req);
        Assert.Contains(Encode(tr["XRuleQrNotRequired"]), s.GetProperty("rulesOff").GetString()!);
        Assert.DoesNotContain(Encode(tr["XRuleQrNotRequired"]), req);
    }

    [Fact]
    public void B3_without_a_qr_key_the_manage_link_is_not_drawn_and_the_view_never_draws_the_layered_scope()
    {
        var r = SettingsSmoke("noqr");
        if (r is not null)
        {
            Assert.DoesNotContain("/CRM/VisitQrCodes", r.Value.GetProperty("extra").GetString()!);
            Assert.Contains("data-field=\"qrMode\"", r.Value.GetProperty("extra").GetString()!);
        }

        var view = File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "VisitEvidenceSettings", "Index.cshtml")).Replace("\r\n", "\n");
        Assert.Contains("<div id=\"ve-extra\"></div>", view);
        Assert.Contains("data-pane=\"reps\"", view);
        Assert.DoesNotContain("ve-soon", view);
        Assert.DoesNotContain("business-unit", view, StringComparison.OrdinalIgnoreCase); // D-1: no layered scope
        Assert.DoesNotContain("legal-entity", view, StringComparison.OrdinalIgnoreCase);
        var controller = File.ReadAllText(Path.Combine(WebRoot(), "Controllers", "CRM", "VisitEvidenceSettingsController.cs"));
        Assert.Contains("CanReadQrCodes = HasAny(\"crm.visit-qr.read\", \"crm.visit-qr.manage\")", controller);

        // the new settings keys in seven languages (the full A10 rules of the W3-WEB-d test stay on all keys)
        foreach (var culture in Cultures)
        {
            var resx = SettingsResx(culture);
            foreach (var key in new[] { "G_extra", "F_qrMode", "F_antiFraudMode", "Opt_qrMode_off", "Opt_qrMode_optional", "Opt_qrMode_required", "Opt_antiFraudMode_off",
                         "Opt_antiFraudMode_flag", "Opt_antiFraudMode_block", "D_qrMode_off", "D_qrMode_optional", "D_qrMode_required", "XRule_qr_optional", "XRuleDesc_qr_optional",
                         "XRuleQrNotRequired", "SourceExceptionShort", "NewBadge", "SoonBadge", "PaneReps", "ColQr", "ColDevice" })
            {
                Assert.True(resx.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) && v != key, $"{culture}:{key}");
                Assert.True(VisitEvidenceSettingsIndex.ScriptKeys.Contains(key) || VisitEvidenceSettingsIndex.ViewKeys.Contains(key), key);
            }
        }
        Assert.Matches("[؀-ۿ]", SettingsResx("ar")["F_qrMode"]);
    }

    // ═══ 2 · the flags page (M1–M6) ═════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void C1_without_a_flags_read_key_the_page_draws_only_the_access_notice_and_the_flags_follow_the_keys()
    {
        var view = FlagsView();
        Assert.True(view.IndexOf("@if (!Model.CanRead)", StringComparison.Ordinal) < view.IndexOf("id=\"vf-root\"", StringComparison.Ordinal));
        Assert.Matches("<partial name=\"_AccessDenied\"[^\n]*\n\\s*return;", view);

        Assert.False(Model(Arrange().Controller.Index()).CanRead);
        Assert.False(Model(Arrange(PhotoKeys).Controller.Index()).CanRead); // photo keys alone never open the page
        var team = Model(Arrange(Read).Controller.Index());
        Assert.True(team.CanRead);
        Assert.False(team.CanReadAll);
        Assert.False(team.CanSeePhotos);
        var all = Model(Arrange([ReadAll, .. PhotoKeys]).Controller.Index());
        Assert.True(all.CanRead);
        Assert.True(all.CanReadAll);
        Assert.True(all.CanSeePhotos);
        Assert.False(Model(Arrange(Read, PhotoKeys[0], PhotoKeys[1]).Controller.Index()).CanSeePhotos); // all three photo keys

        Assert.Contains("data-can-read-all=\"@Model.CanReadAll.ToString().ToLowerInvariant()\"", view);
        Assert.Contains("data-can-see-photos=\"@Model.CanSeePhotos.ToString().ToLowerInvariant()\"", view);
        Assert.Contains("data-visit-url=\"/CRM/VisitReport\"", view);
    }

    [Fact]
    public async Task C3_the_photo_goes_through_the_existing_evidence_path_and_needs_the_flags_key_and_all_three_photo_keys()
    {
        foreach (var keys in new[] { new[] { Read }, PhotoKeys, new[] { Read, PhotoKeys[0], PhotoKeys[1] }, new[] { Read, PhotoKeys[1], PhotoKeys[2] } })
        {
            var denied = Arrange(keys);
            Assert.Equal(403, Status(await Invoke(denied.Controller, "Photo")));
            Assert.Empty(denied.Gateway.Requests);
        }

        var (gateway, controller) = Arrange([Read, .. PhotoKeys]);
        Assert.IsType<FileContentResult>(await Invoke(controller, "Photo"));
        Assert.Equal(GatewayUrl + "/api/crm/visit-execution/" + Id + "/photo", Assert.Single(gateway.Requests).Uri);
    }

    [Fact]
    public void C4_the_pure_rules_period_query_chips_bars_and_drawer_lines()
    {
        var node = RunNode(FlagsCore() + """
            const row = { flags: ['short_visit', 'location_far', 'device_unverified'], durationSeconds: 100, location: { distanceMeters: 820.4, accuracyMeters: 240 }, qr: { status: 'missing' } };
            const bars = C.summaryBars({ device_unverified: 12, location_far: 7, short_visit: 5, qr_not_issued: 4, started_with_exception: 3, location_inaccurate: 3, qr_revoked: 2, mock_location: 2, location_missing: 1, qr_wrong_account: 1 }, 6);
            process.stdout.write(JSON.stringify({
              max: C.MAX_RANGE_DAYS,
              ok92: C.rangeError('2026-07-15', '2026-10-14'), long93: C.rangeError('2026-07-14', '2026-10-14'), order: C.rangeError('2026-10-14', '2026-10-01'), missing: C.rangeError('', '2026-10-01'),
              last7: C.rangeOf('last7', '2026-10-14'), last30: C.rangeOf('last30', '2026-10-14'), month: C.rangeOf('month', '2026-10-14'), periods: C.PERIODS,
              summary: C.summaryQuery({ period: 'last30', from: '2026-01-01', to: '2026-01-31', resourceId: 'rep-2', flags: ['location_far'], accountId: 'a', scope: 'all' }),
              summaryCustom: C.summaryQuery({ period: 'custom', from: '2026-09-01', to: '2026-09-30', resourceId: '' }),
              summaryBaseline: C.summaryQuery({ period: '', from: '', to: '', resourceId: '', flags: [], accountId: '', scope: '' }),
              custom: [C.customRangeError('custom', '2026-07-15', '2026-10-14'), C.customRangeError('custom', '2026-07-14', '2026-10-14'), C.customRangeError('last30', '2026-01-01', '2026-12-31'), C.customRangeError('', '', '')],
              codes: C.FLAG_CODES, chips: C.chipsOf(row), noQrChip: C.chipsOf({ flags: [], qr: { status: 'ok' } }), bars,
              d1: C.duration(100), d2: C.duration(1080), d3: C.duration(40), dx: C.durationExact(710),
              qr: [C.qrResult({ status: 'missing' }, 'in_person', { id: 'x' }), C.qrResult({ status: 'missing' }, 'in_person', null), C.qrResult({ status: 'ok' }, 'remote', null), C.qrResult(null, 'in_person', null), C.qrResult({ status: 'wrong_account' }, 'in_person', null)],
              web: C.device({ kind: 'in_person', device: { platform: 'web', appVersion: 'Safari 18 · iPadOS' } }),
              android: C.device({ kind: 'in_person', device: { platform: 'android', appVersion: '2.4.1', integrityVerdict: 'passed' } }),
              mock: C.device({ kind: 'in_person', device: { platform: 'android', appVersion: '2.4.1', mockLocation: true, integrityVerdict: 'passed' } }),
              rate: C.repRate(412, 37),
              err: [C.errorKey({ status: 400, body: { errors: ['visit_evidence_flags_range_invalid'] } }), C.errorKey({ status: 403, body: {} }), C.errorKey({ status: 500, body: {} })]
            }));
            """);
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement;
        Assert.Equal(92, r.GetProperty("max").GetInt32());
        Assert.Equal(JsonValueKind.Null, r.GetProperty("ok92").ValueKind);
        Assert.Equal("ErrRangeLong", r.GetProperty("long93").GetString());
        Assert.Equal("ErrRangeOrder", r.GetProperty("order").GetString());
        Assert.Equal("ErrRangeMissing", r.GetProperty("missing").GetString());
        Assert.Equal("2026-10-08", r.GetProperty("last7").GetProperty("from").GetString());
        Assert.Equal("2026-09-15", r.GetProperty("last30").GetProperty("from").GetString());
        Assert.Equal("2026-10-01", r.GetProperty("month").GetProperty("from").GetString());
        Assert.Equal(["", "last30", "month", "custom"], r.GetProperty("periods").EnumerateArray().Select(x => x.GetString()));   // "" (the baseline) = the last 7 days

        // W3E-FLAGS-STD — the summary read of the APPLIED filters: the period, the custom range only for "custom", the rep —
        // never a page size (the rep summary is client mode), never a flag / institution / scope
        Assert.Equal("?period=last30&resourceId=rep-2", r.GetProperty("summary").GetString());
        Assert.Equal("?period=custom&from=2026-09-01&to=2026-09-30", r.GetProperty("summaryCustom").GetString());
        Assert.Equal("?", r.GetProperty("summaryBaseline").GetString());
        Assert.Equal("[null,\"ErrRangeLong\",null,null]", r.GetProperty("custom").GetRawText());                                   // 92 ok · 93 refused · a preset never
        var codes = r.GetProperty("codes").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Equal(14, codes.Length);
        Assert.DoesNotContain("qr_missing", codes);

        // the chips: device → location → duration, the values, then "QR not scanned" (not a flag)
        var chips = r.GetProperty("chips").EnumerateArray().ToArray();
        Assert.Equal(["device_unverified", "location_far", "short_visit", "qr_missing"], chips.Select(c => c.GetProperty("code").GetString()));
        Assert.Equal(820, chips[1].GetProperty("value").GetProperty("args")[0].GetInt32());
        Assert.Equal("MinSec", chips[2].GetProperty("value").GetProperty("key").GetString());
        Assert.False(chips[3].GetProperty("flag").GetBoolean());
        Assert.Equal("Chip_qr_missing", chips[3].GetProperty("key").GetString());
        Assert.Empty(r.GetProperty("noQrChip").EnumerateArray());

        // the bars: top 6 + "Other (4 types)"
        Assert.Equal(6, r.GetProperty("bars").GetProperty("bars").GetArrayLength());
        Assert.Equal("device_unverified", r.GetProperty("bars").GetProperty("bars")[0].GetProperty("code").GetString());
        Assert.Equal(100, r.GetProperty("bars").GetProperty("bars")[0].GetProperty("pct").GetInt32());
        Assert.Equal(4, r.GetProperty("bars").GetProperty("other").GetProperty("types").GetInt32());
        Assert.Equal(6, r.GetProperty("bars").GetProperty("other").GetProperty("count").GetInt32());

        Assert.Equal("MinSec", r.GetProperty("d1").GetProperty("key").GetString());
        Assert.Equal("Minutes", r.GetProperty("d2").GetProperty("key").GetString());
        Assert.Equal(18, r.GetProperty("d2").GetProperty("args")[0].GetInt32());
        Assert.Equal("Seconds", r.GetProperty("d3").GetProperty("key").GetString());
        Assert.Equal("[11,50]", r.GetProperty("dx").GetProperty("args").GetRawText());

        Assert.Equal(["QrSt_missing_exception", "QrSt_missing", "QrSt_remote", "QrSt_not_applicable", "QrSt_wrong_account"], r.GetProperty("qr").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal("Plat_web", r.GetProperty("web").GetProperty("platformKey").GetString());
        Assert.Equal("Safari 18 · iPadOS", r.GetProperty("web").GetProperty("platformExtra").GetString());
        Assert.Equal("Verdict_web", r.GetProperty("web").GetProperty("verdictKey").GetString());
        Assert.Equal("2.4.1", r.GetProperty("android").GetProperty("appVersion").GetString());
        Assert.Equal("Verdict_passed", r.GetProperty("android").GetProperty("verdictKey").GetString());
        Assert.Equal("Verdict_mock", r.GetProperty("mock").GetProperty("verdictKey").GetString());
        Assert.Equal(9.0, r.GetProperty("rate").GetDouble());
        Assert.Equal(["Err_visit_evidence_flags_range_invalid", "Err_permission_denied", "Err_unavailable"], r.GetProperty("err").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public void C7_every_key_in_seven_languages_without_echo_with_its_placeholders_and_every_key_the_script_and_core_name()
    {
        var english = FlagsResx("en");
        foreach (var culture in Cultures)
        {
            var resx = FlagsResx(culture);
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
            var resx = FlagsResx(culture);
            var same = AllKeys.Count(k => resx[k] == english[k] && !resx[k].Contains('{'));
            Assert.True(same <= 12, $"{culture}: {same} values identical to English");
        }
        Assert.All(new[] { "PageTitle", "Flag_location_far", "Chip_qr_missing", "EmptyTitle" }, k => Assert.Matches("[؀-ۿ]", FlagsResx("ar")[k]));
        Assert.Equal("Uzak konum", FlagsResx("tr")["Flag_location_far"]);
        Assert.Equal("QR okutulmadı", FlagsResx("tr")["Chip_qr_missing"]);

        // the views write only declared view keys (the page's own Localizer; the shared words are SharedLocalizer's); the
        // L10n payload carries EVERY script key; the scripts read only the page's script keys or a shared payload word
        var l10n = FlagsPartial("_IndexL10n.cshtml");
        Assert.Contains("foreach (var key in VisitEvidenceFlagsIndex.ScriptKeys)", l10n);
        Assert.Contains("payload[key] = Localizer[key].Value;", l10n);
        foreach (var file in new[] { "Index.cshtml", "_Filter.cshtml", "_DataTable.cshtml" })
        {
            foreach (Match m in Regex.Matches(FlagsPartial(file), "(?<!Shared)Localizer\\[\"([A-Za-z0-9_]+)\"\\]")) { Assert.Contains(m.Groups[1].Value, VisitEvidenceFlagsIndex.ViewKeys); }
        }

        var sharedWords = Regex.Matches(l10n, "^\\s+([A-Za-z]+) = ", RegexOptions.Multiline).Select(m => m.Groups[1].Value).ToHashSet();
        foreach (var script in new[] { "index.js", "rep-summary.js" })
        {
            var js = FlagsScript(script);
            foreach (Match m in Regex.Matches(js, "\\b(?:L\\(\\)|l)\\.([A-Z][A-Za-z0-9_]+)"))
            {
                Assert.True(VisitEvidenceFlagsIndex.ScriptKeys.Contains(m.Groups[1].Value) || sharedWords.Contains(m.Groups[1].Value), script + ": " + m.Groups[1].Value);
            }
        }

        var node = RunNode(FlagsCore() + """
            const keys = [];
            C.FLAGS.forEach(f => keys.push('Flag_' + f.code));
            C.ERROR_CODES.forEach(c => keys.push('Err_' + c));
            ['ok','missing','invalid','wrong_account','revoked','not_issued','unverifiable'].forEach(s => keys.push(C.qrResult({ status: s }, 'in_person', null)));
            keys.push(C.qrResult({ status: 'missing' }, 'in_person', {}), C.qrResult(null, 'in_person', null), C.qrResult(null, 'remote', null));
            ['web','android','ios'].forEach(p => { const d = C.device({ kind: 'in_person', device: { platform: p } }); keys.push(d.platformKey, d.verdictKey); if (d.appKey) keys.push(d.appKey); });
            [{ mockLocation: true }, { integrityVerdict: 'failed' }, { integrityVerdict: 'passed' }].forEach(x => keys.push(C.device({ kind: 'in_person', device: Object.assign({ platform: 'android' }, x) }).verdictKey));
            keys.push(C.device({ kind: 'remote', device: null }).verdictKey, C.device({ kind: 'in_person', device: null }).verdictKey);
            [40, 100, 1080].forEach(s => keys.push(C.duration(s).key, C.durationExact(s).key));
            [['', ''], ['2026-10-14', '2026-10-01'], ['2026-01-01', '2026-10-01']].forEach(p => keys.push(C.rangeError(p[0], p[1])));
            keys.push(C.chipsOf({ flags: [], qr: { status: 'missing' } })[0].key, 'Kind_in_person', 'Kind_remote');
            process.stdout.write(JSON.stringify(Array.from(new Set(keys))));
            """);
        if (node is null) { return; }
        foreach (var key in JsonDocument.Parse(node).RootElement.EnumerateArray().Select(k => k.GetString()!))
        {
            Assert.Contains(key, VisitEvidenceFlagsIndex.ScriptKeys);
        }
    }

    [Fact]
    public void C8_the_layout_mirrors_in_arabic_has_a_dark_set_names_are_isolated_and_the_menu_label_exists()
    {
        var css = File.ReadAllText(Path.Combine(FlagsScriptDir(), "visit-evidence-flags.css"));
        Assert.Contains("[dir=\"rtl\"] .vf-flip", css);
        Assert.Contains("[data-bs-theme=\"dark\"] .vf-summary, [data-bs-theme=\"dark\"] .datatables-visitevidenceflags, [data-bs-theme=\"dark\"] .datatables-vf-reps, [data-bs-theme=\"dark\"] .vf-drawer", css);
        foreach (var physical in new[] { "margin-left", "margin-right", "padding-left", "padding-right", " left:", " right:", "text-align: left", "text-align: right" })
        {
            Assert.DoesNotContain(physical, css);
        }

        var js = FlagsScript("index.js") + FlagsScript("rep-summary.js");
        Assert.Contains("F.bidi(row.repName || row.resourceId)", js);
        Assert.Contains("F.bidi(name || id)", js);
        Assert.DoesNotContain("localStorage", js);
        Assert.DoesNotContain("toLocaleDateString", js);
        Assert.DoesNotContain("campaign", js, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("method: 'POST'", js); // read only: the page never writes
        Assert.DoesNotContain("method: 'PUT'", js);

        foreach (var culture in new[] { "en", "tr", "fr", "es", "zh", "ar", "ru" })
        {
            var shared = XDocument.Load(Path.Combine(WebRoot(), "Resources", $"SharedResource.{culture}.resx")).Root!
                .Elements("data").ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")?.Value ?? "");
            Assert.True(shared.TryGetValue("Nav.Page.CRMVISITEVIDENCEFLAGS", out var nav) && !string.IsNullOrWhiteSpace(nav) && nav != "Nav.Page.CRMVISITEVIDENCEFLAGS", culture);
        }
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────

    private static string Encode(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private static string Fmt(string template, params object[] args)
    {
        var s = template;
        for (var i = 0; i < args.Length; i++) { s = s.Replace("{" + i + "}", Convert.ToString(args[i], System.Globalization.CultureInfo.InvariantCulture)); }
        return s;
    }

    /// <summary>The shared words the page reads from the L10n payload (SharedResource in the real page); fixed here.</summary>
    private static readonly Dictionary<string, string> SharedWords = new() { ["ShowAll"] = "Tümü", ["Apply"] = "Uygula", ["Reset"] = "Sıfırla", ["SaveView"] = "Görünümü kaydet" };

    private static JsonElement? FlagsSmoke(string mode)
    {
        var words = FlagsResx(mode == "ar" ? "ar" : "tr");
        var texts = VisitEvidenceFlagsIndex.ScriptKeys.ToDictionary(k => k, k => words[k]);
        foreach (var (k, v) in SharedWords) { texts.TryAdd(k, v); }
        return SmokeTexts("visit-evidence-flags-list-smoke.js", texts, mode);
    }

    private static JsonElement? SettingsSmoke(string mode)
        => Smoke("visit-evidence-settings-w3e-smoke.js", VisitEvidenceSettingsIndex.ScriptKeys, SettingsResx("tr"), mode);

    private static JsonElement? Smoke(string file, string[] keys, Dictionary<string, string> tr, string mode)
        => SmokeTexts(file, keys.ToDictionary(k => k, k => tr[k]), mode);

    private static JsonElement? SmokeTexts(string file, Dictionary<string, string> words, string mode)
    {
        var smoke = Path.Combine(Path.GetDirectoryName(WebRoot())!, "Diten.Web.Tests", "Js", file).Replace("\\", "/");
        var crm = Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM").Replace("\\", "/");
        var texts = Path.Combine(Path.GetTempPath(), "vf-tr-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(texts, JsonSerializer.Serialize(words), new UTF8Encoding(false));
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

    private static VisitEvidenceFlagsViewModel Model(IActionResult result)
        => Assert.IsType<VisitEvidenceFlagsViewModel>(Assert.IsType<ViewResult>(result).Model);

    private static int? Status(IActionResult result) => result switch
    {
        ContentResult c => c.StatusCode,
        ObjectResult o => o.StatusCode ?? 200,
        StatusCodeResult s => s.StatusCode,
        FileContentResult => 200,
        _ => null
    };

    private static Task<IActionResult> Invoke(VisitEvidenceFlagsController c, string action) => action switch
    {
        "List" => c.List(0, 10, null, null, null, null, null, null, null, null, null, null, CancellationToken.None),
        "Summary" => c.Summary(null, null, null, null, CancellationToken.None),
        "Detail" => c.Detail(Guid.Parse(Id), CancellationToken.None),
        "Photo" => c.Photo(Guid.Parse(Id), CancellationToken.None),
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };

    private static (Gateway Gateway, VisitEvidenceFlagsController Controller) Arrange(params string[] permissions)
    {
        var gateway = new Gateway();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = GatewayUrl }).Build();
        var controller = new VisitEvidenceFlagsController(new HttpClient(gateway), configuration, NullLogger<VisitEvidenceFlagsController>.Instance);
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()), new("sub", "user-1") };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        http.Request.QueryString = new QueryString("?from=2026-10-08&to=2026-10-14&onlyFlagged=true");
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return (gateway, controller);
    }

    private static string SettingsCore() => "const C = require('" + Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", "VisitEvidenceSettings", "evidence-core.js").Replace("\\", "/") + "');\n";

    private static string FlagsCore() => "const C = require('" + Path.Combine(FlagsScriptDir(), "flags-core.js").Replace("\\", "/") + "');\n";

    /// <summary>Runs <paramref name="code"/> in Node; null when Node is not on the machine (the code assertions stand alone).</summary>
    private static string? RunNode(string code)
    {
        var temp = Path.Combine(Path.GetTempPath(), "vf-" + Guid.NewGuid().ToString("N") + ".js");
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

    private static string FlagsScriptDir() => Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", "VisitEvidenceFlags");

    private static string FlagsScript(string file) => File.ReadAllText(Path.Combine(FlagsScriptDir(), file)).Replace("\r\n", "\n");

    private static string FlagsView() => FlagsPartial("Index.cshtml");

    private static string FlagsPartial(string file) => File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "VisitEvidenceFlags", file)).Replace("\r\n", "\n");

    private static Dictionary<string, string> FlagsResx(string culture) => Resx("VisitEvidenceFlags", "VisitEvidenceFlagsIndex", culture);

    private static Dictionary<string, string> SettingsResx(string culture) => Resx("VisitEvidenceSettings", "VisitEvidenceSettingsIndex", culture);

    private static Dictionary<string, string> Resx(string folder, string name, string culture)
    {
        var file = name + (culture.Length == 0 ? "" : "." + culture) + ".resx";
        return XDocument.Load(Path.Combine(WebRoot(), "Resources", "Views", "CRM", folder, file)).Root!
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
            var photo = request.RequestUri!.AbsolutePath.EndsWith("/photo", StringComparison.Ordinal);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = photo
                    ? new ByteArrayContent([0xFF, 0xD8, 0xFF]) { Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg") } }
                    : new StringContent("{\"data\":{}}", Encoding.UTF8, "application/json")
            });
        }
    }
}
