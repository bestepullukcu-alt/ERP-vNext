using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.Web.Controllers.CRM;
using Diten.Web.Views.CRM.VisitEvidenceFlags;
using Diten.Web.Views.CRM.VisitEvidenceSettings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VW-W3E · W3E-FIX on the evidence flags page (M) and the settings preview (E): G-1 the page draws no own root box
/// or paging buttons any more (W3E-FLAGS-STD: the standard table owns them), U-3 the list header standard, U-2 the custom range is the shared date
/// field, G-7 same-name institutions told apart (city / code, read once through the page's proxy), G-8 far distances in km
/// in the reader's language, CT-3 the smoke's clock pinned, G-5 the settings preview shows the QR row, its rule sentence
/// and the web device line.
/// </summary>
public sealed partial class VisitEvidenceFlagsWebTests
{
    /// <summary>G-1 (kept under W3E-FLAGS-STD): the footer rode over the table because the page's own root and its own paging
    /// buttons shared a class. The page no longer draws either — the table, its paging and the empty row are the standard's
    /// (_ListShell + the list factory) — so neither class may come back.</summary>
    [Fact]
    public void G1_the_page_draws_no_own_root_box_or_paging_buttons_the_standard_table_owns_them()
    {
        var css = FlagsScript("visit-evidence-flags.css");
        Assert.DoesNotMatch(@"\.vf-page\b|\.vf-pg\b|\.vf-pager\b|\.vf-table\b", css);
        var js = FlagsScript("index.js");
        Assert.DoesNotContain("vf-pg", js);
        Assert.DoesNotContain("vf-page", js);
        Assert.DoesNotContain("data-page=", js);
        Assert.DoesNotContain("class=\"vf-page", FlagsView());
        Assert.False(File.Exists(Path.Combine(FlagsScriptDir(), "visit-evidence-flags.js")));                    // the custom table is gone
        Assert.Contains("<partial name=\"~/Views/CRM/VisitEvidenceFlags/_DataTable.cshtml\" />", FlagsView());
    }

    [Fact]
    public void U3_the_flags_header_is_the_list_header_standard_and_the_crumb_is_gone()
    {
        var view = FlagsView();
        Assert.Matches(@"<div class=""mb-3"">\s*<h5 class=""mb-0"">@Localizer\[""PageTitle""\]</h5>\s*<p class=""mb-0 text-muted"">@Localizer\[""PageDescription""\]</p>\s*</div>", view);
        Assert.DoesNotContain("vf-crumb", view);
        Assert.DoesNotContain("vf-title", view);
        Assert.DoesNotContain("Crumb", VisitEvidenceFlagsIndex.ViewKeys);
        Assert.DoesNotContain("Intro", VisitEvidenceFlagsIndex.ViewKeys);
        var trResx = File.ReadAllText(Path.Combine(WebRoot(), "Resources", "Views", "CRM", "VisitEvidenceFlags", "VisitEvidenceFlagsIndex.tr.resx"));
        Assert.DoesNotContain("name=\"Crumb\"", trResx);
        Assert.DoesNotContain("name=\"Intro\"", trResx);
    }

    [Fact]
    public void U2_the_custom_range_is_the_shared_date_field_whose_icon_opens_the_calendar()
    {
        var filter = FlagsPartial("_Filter.cshtml");
        foreach (var id in new[] { "vf-from", "vf-to" })
        {
            Assert.Matches("<div class=\"diten-field\"><i class=\"bx bx-calendar diten-field-icon\" aria-hidden=\"true\"></i><input type=\"text\" class=\"form-control form-control-sm flatpickr-date\" id=\"" + id + "\"", filter);
        }

        Assert.DoesNotContain("type=\"date\"", filter);
        var view = FlagsView();
        Assert.Contains("~/assets/js/shared/diten-datefield.js", view);
        Assert.True(view.IndexOf("diten-datefield.js", StringComparison.Ordinal) < view.IndexOf("VisitEvidenceFlags/index.js", StringComparison.Ordinal));
        var js = FlagsScript("index.js");
        Assert.Contains("window.DitenDateField.enhance(byId('filterRange'))", js);
        Assert.Contains("if (n._flatpickr) { n._flatpickr.clear(); }", js);                                        // a preset clears the calendar too
    }

    [Fact]
    public void G7_G8_CT3_same_name_institutions_get_their_city_km_distances_and_the_clock_is_pinned()
    {
        var r = FlagsSmoke("team");
        if (r is null) { return; }
        var s = r.Value;
        var tr = FlagsResx("tr");

        // CT-3 — the harness's today is 2026-10-14 whatever day the suite runs: the baseline period label is 8–14 Oct
        Assert.Contains("8", s.GetProperty("lookups").GetProperty("filterPeriod").GetProperty("showAll").GetString()!);
        Assert.Contains("14 Eki", s.GetProperty("lookups").GetProperty("filterPeriod").GetProperty("showAll").GetString()!);
        Assert.Contains("month=Ekim 2026", s.GetProperty("lookups").GetProperty("filterPeriod").GetProperty("items").EnumerateArray().Select(x => x.GetString()));

        // G-8 — 198 290 m → "198,3 km" (Turkish decimal comma); under 1000 m stays metres
        var flags = string.Join("\n", s.GetProperty("cells").EnumerateArray().Select(c => c.GetProperty("flags").GetString()));
        Assert.Contains(Encode(tr["Flag_location_far"]) + " · " + Encode(Fmt(tr["UnitKm"], "198,3")), flags);
        Assert.Contains(Encode(tr["Flag_location_far"]) + " · 820 m", flags);
        Assert.DoesNotContain("198.290", flags);

        // G-7 — the two "100.YIL ECZANESİ" options carry their city; a unique name stays plain; ONE label read, the same-name ids only
        var account = s.GetProperty("accountOptions").EnumerateArray().Select(x => x.GetString()!).ToList();
        const string Fsi = "⁨", Pdi = "⁩";
        Assert.Contains("acc-3=" + Fsi + "100.YIL ECZANESİ" + Pdi + " · " + Fsi + "Konya" + Pdi, account);
        Assert.Contains("acc-4=" + Fsi + "100.YIL ECZANESİ" + Pdi + " · " + Fsi + "İstanbul" + Pdi, account);
        Assert.Contains("acc-1=" + Fsi + "Kardiyoloji Polikliniği" + Pdi, account);
        Assert.Equal("=" + tr["AccountAll"], account[0]);
        Assert.Equal(new[] { "/CRM/VisitEvidenceFlags/api/account-labels?ids=acc-3%2Cacc-4" }, s.GetProperty("labelCalls").EnumerateArray().Select(x => x.GetString()));

        var node = RunNode(FlagsCore() + """
            const cmp = (a, b) => a.localeCompare(b, 'tr');
            process.stdout.write(JSON.stringify({
              d: [C.distance(999.4), C.distance(1000), C.distance(198290), C.distance(null)],
              sameCity: C.accountOptions([{ id: '1', name: 'A', city: 'Konya', code: 'K1' }, { id: '2', name: 'A', city: 'Konya', code: 'K2' }, { id: '3', name: 'B' }], cmp).map(o => o.text),
              noCity: C.accountOptions([{ id: '1', name: 'A', code: 'K1' }, { id: '2', name: 'A' }], cmp).map(o => o.text),
              same: C.sameNameIds([{ id: '1', name: 'A' }, { id: '2', name: ' a' }, { id: '3', name: 'B' }])
            }));
            """);
        var core = JsonDocument.Parse(node!).RootElement;
        Assert.Equal("[{\"key\":\"UnitMeters\",\"args\":[999],\"digits\":0},{\"key\":\"UnitKm\",\"args\":[1],\"digits\":1},{\"key\":\"UnitKm\",\"args\":[198.3],\"digits\":1},{\"key\":\"UnitMeters\",\"args\":[0],\"digits\":0}]",
            core.GetProperty("d").GetRawText());
        Assert.Equal("[\"A · Konya · K1\",\"A · Konya · K2\",\"B\"]", core.GetProperty("sameCity").GetRawText());   // the same city → the code too
        Assert.Equal("[\"A\",\"A · K1\"]", core.GetProperty("noCity").GetRawText());                                 // no city → the code
        Assert.Equal("[\"1\",\"2\"]", core.GetProperty("same").GetRawText());
    }

    [Fact]
    public async Task G7_the_account_labels_proxy_reads_the_crm_account_per_id_tenant_scoped_and_needs_the_flags_key()
    {
        var gateway = new LabelGateway();
        var controller = LabelController(gateway, VisitEvidenceFlagsController.Read);
        var ok = Assert.IsType<OkObjectResult>(await controller.AccountLabels("33333333-3333-3333-3333-333333333333, x ,44444444-4444-4444-4444-444444444444,33333333-3333-3333-3333-333333333333", CancellationToken.None));
        var rows = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value)).RootElement.GetProperty("data");
        Assert.Equal(2, rows.GetArrayLength());
        Assert.Equal(("ECZ-0042", "Konya"), (rows[0].GetProperty("code").GetString(), rows[0].GetProperty("city").GetString())); // G-3: TR-42-KONYA → Konya
        Assert.Contains(gateway.Requests, q => q.Uri.EndsWith("/api/crm/accounts/33333333-3333-3333-3333-333333333333", StringComparison.Ordinal) && q.Tenant == TenantId.ToString());
        Assert.Equal(2, gateway.Requests.Count(q => q.Uri.Contains("/api/crm/accounts/", StringComparison.Ordinal)));

        var many = string.Join(",", Enumerable.Range(0, 80).Select(_ => Guid.NewGuid()));
        var capped = new LabelGateway();
        await LabelController(capped, VisitEvidenceFlagsController.ReadAll).AccountLabels(many, CancellationToken.None);
        Assert.Equal(VisitEvidenceFlagsController.MaxAccountLabels, capped.Requests.Count(q => q.Uri.Contains("/api/crm/accounts/", StringComparison.Ordinal)));

        var denied = new LabelGateway();
        Assert.Equal(403, Status(await LabelController(denied).AccountLabels("33333333-3333-3333-3333-333333333333", CancellationToken.None)));
        Assert.Empty(denied.Requests);
    }

    [Fact]
    public void G5_the_settings_preview_shows_the_qr_row_by_mode_its_rule_sentence_and_the_web_device_line()
    {
        var node = RunNode(SettingsCore() + """
            const base = { photoMode: 'optional', locationMode: 'info', accountWithoutLocation: 'allow_flagged', radiusMeters: 300, durationConsistency: false };
            const pick = p => ({ qr: p.qr, deviceWeb: p.deviceWeb, start: p.startDisabled, lines: p.lines.map(l => l.key) });
            process.stdout.write(JSON.stringify({
              off: pick(C.preview(Object.assign({}, base, { qrMode: 'off', antiFraudMode: 'off' }))),
              optional: pick(C.preview(Object.assign({}, base, { qrMode: 'optional', antiFraudMode: 'flag' }))),
              required: pick(C.preview(Object.assign({}, base, { qrMode: 'required', antiFraudMode: 'off' })))
            }));
            """);
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement;
        Assert.Equal(JsonValueKind.Null, r.GetProperty("off").GetProperty("qr").ValueKind);                 // off → no QR row
        Assert.DoesNotContain("D_qrMode_off", r.GetProperty("off").GetProperty("lines").GetRawText());
        Assert.False(r.GetProperty("off").GetProperty("deviceWeb").GetBoolean());
        Assert.Equal(("PvOptional", "info"), (r.GetProperty("optional").GetProperty("qr").GetProperty("modeKey").GetString(), r.GetProperty("optional").GetProperty("qr").GetProperty("tone").GetString()));
        Assert.Contains("D_qrMode_optional", r.GetProperty("optional").GetProperty("lines").GetRawText());      // the rule sentence
        Assert.True(r.GetProperty("optional").GetProperty("deviceWeb").GetBoolean());                          // "flag" → the web line (S7)
        Assert.False(r.GetProperty("optional").GetProperty("start").GetBoolean());
        Assert.Equal("PvRequired", r.GetProperty("required").GetProperty("qr").GetProperty("modeKey").GetString());
        Assert.Contains("D_qrMode_required", r.GetProperty("required").GetProperty("lines").GetRawText());
        Assert.True(r.GetProperty("required").GetProperty("start").GetBoolean());                              // "start" waits for the scan (S8)

        var view = File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "VisitEvidenceSettings", "Index.cshtml"));
        Assert.Contains("<div class=\"ve-pv-sign ve-pv-qr d-none\" id=\"ve-pv-qr\"><i class=\"bx bx-qr-scan\"></i> <span class=\"ve-grow\">@Localizer[\"PvQr\"]</span><span class=\"ve-pill\" id=\"ve-pv-qr-mode\"></span></div>", view);
        Assert.Contains("<div class=\"ve-pv-sign ve-pv-device d-none\" id=\"ve-pv-device\"><i class=\"bx bx-mobile-alt\"></i> <span>@Localizer[\"PvDeviceWeb\"]</span></div>", view);
        var js = File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", "VisitEvidenceSettings", "visit-evidence-settings.js"));
        Assert.Contains("show('ve-pv-qr', !!pv.qr);", js);
        Assert.Contains("show('ve-pv-device', pv.deviceWeb);", js);
        Assert.Contains("PvQr", VisitEvidenceSettingsIndex.ViewKeys);
        Assert.Contains("PvDeviceWeb", VisitEvidenceSettingsIndex.ViewKeys);
        foreach (var culture in new[] { "", "en", "tr", "fr", "es", "zh", "ar", "ru" })
        {
            var resx = SettingsResx(culture);
            Assert.False(string.IsNullOrWhiteSpace(resx["PvQr"]), culture);
            Assert.False(string.IsNullOrWhiteSpace(resx["PvDeviceWeb"]), culture);
        }

        // S7's own words: the line the rep reads on the start window is the same sentence
        Assert.Equal("Bu cihaz doğrulanamıyor; ziyaretiniz 'cihaz doğrulanamadı' olarak işaretlenecek. Mobil uygulamayı kullanmanız önerilir.", SettingsResx("tr")["PvDeviceWeb"]);
    }

    [Fact]
    public void G8_the_km_unit_is_in_seven_languages_with_its_placeholder()
    {
        Assert.Contains("UnitKm", VisitEvidenceFlagsIndex.ScriptKeys);
        foreach (var culture in new[] { "", "en", "tr", "fr", "es", "zh", "ar", "ru" })
        {
            Assert.Contains("{0}", FlagsResx(culture)["UnitKm"]);
        }
    }

    // ── helpers (W3E-FIX) ──────────────────────────────────────────────────────────────────────────────────────

    private static VisitEvidenceFlagsController LabelController(LabelGateway gateway, params string[] permissions)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = GatewayUrl }).Build();
        var controller = new VisitEvidenceFlagsController(new HttpClient(gateway), configuration, NullLogger<VisitEvidenceFlagsController>.Instance);
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()), new("sub", "user-1") };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) } };
        return controller;
    }

    private sealed class LabelGateway : HttpMessageHandler
    {
        public List<(string Uri, string? Tenant)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!.OriginalString;
            Requests.Add((uri, request.Headers.TryGetValues("X-Tenant-Id", out var t) ? t.Single() : null));
            var body = uri.Contains("/api/crm/accounts/", StringComparison.Ordinal)
                ? "{\"data\":{\"accountCode\":\"ECZ-0042\",\"cityRef\":\"TR-42-KONYA\"}}"
                : "{\"data\":{\"items\":[]}}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
