using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Controllers.CRM;
using Diten.Web.Models.CRM;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VP-FIX-1 — the Visit Planning console on the REAL controller, views, scripts and resx: the list's target column
/// reads the counts the list DTO actually carries (D1); a committed / archived plan opens read-only and its Edit page
/// returns to Details (D2); the browser no longer reads the working calendar (C3 — the planner's nonWorkingDates /
/// calendarStatus drive the route tabs); the strategy-template picker and its proxy are gone (A1); every visible text is
/// a seven-language resx key with real Turkish (D6 / A5); institution type + specialty labels come from the reference sets.
/// </summary>
public sealed class VisitPlanningQuickFixesWebTests
{
    private const string GatewayUrl = "http://gateway.test";
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
    private static readonly Guid TenantId = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");
    private static readonly Guid SessionId = Guid.Parse("0848afed-0000-0000-0000-000000000001");

    private const string Read = "crm.visit-plan.read";
    private const string Generate = "crm.visit-plan.generate";
    private const string Apply = "crm.visit-plan.apply";
    private const string PlannedVisitManage = "crm.planned-visit.manage";

    // ============================================================ 1 · D1 list target count

    [Fact]
    public void The_list_target_column_reads_the_doctor_and_pharmacy_counts_of_the_list_dto()
    {
        var sTargets = Function(Asset("index.js"), "sTargets");

        Assert.Contains("s.selectedContactCount", sTargets);
        Assert.Contains("s.selectedPharmacyCount", sTargets);
        Assert.Contains("L.TargetCountFormat", sTargets);
        Assert.Contains("return '0'", sTargets);
        // The fields the old reader looked for are not on the list DTO — reading them is what made the column 0.
        Assert.DoesNotContain("selectedContacts", sTargets);
        Assert.DoesNotContain("targetCount", sTargets);

        Assert.Equal("{0} doktor · {1} eczane", Resx("tr")["TargetCountFormat"]);
        Assert.Equal("{0} doctors · {1} pharmacies", Resx("en")["TargetCountFormat"]);
        Assert.Matches(@"(?<![A-Za-z0-9_])TargetCountFormat\s*=", Bridge());
    }

    // ============================================================ 2 · D2 committed plan is read-only

    [Theory]
    [InlineData("committed")]
    [InlineData("archived")]
    public async Task A_locked_plan_opens_details_with_every_write_flag_off(string status)
    {
        var (gateway, controller) = Arrange(SessionBody(status), Read, Generate, Apply, PlannedVisitManage);

        var view = Assert.IsType<ViewResult>(await controller.Details(SessionId, default));
        var model = Assert.IsType<VisitPlanningSessionPageViewModel>(view.Model);

        Assert.True(model.IsReadOnly);
        Assert.False(model.CanGenerate);
        Assert.False(model.CanApply);
        Assert.Equal($"{GatewayUrl}/api/crm/visit-plan/sessions/{SessionId}", Assert.Single(gateway.Requests).Uri);
        Assert.Equal(TenantId.ToString(), gateway.Requests[0].Tenant);
    }

    [Fact]
    public async Task A_draft_plan_keeps_its_permission_flags()
    {
        var (_, controller) = Arrange(SessionBody("draft"), Read, Generate, Apply, PlannedVisitManage);

        var model = Assert.IsType<VisitPlanningSessionPageViewModel>(
            Assert.IsType<ViewResult>(await controller.Details(SessionId, default)).Model);

        Assert.False(model.IsReadOnly);
        Assert.True(model.CanGenerate);
        Assert.True(model.CanApply);
    }

    [Fact]
    public async Task The_edit_page_of_a_committed_plan_returns_to_details()
    {
        var (_, controller) = Arrange(SessionBody("committed"), Read, Generate);

        var redirect = Assert.IsType<RedirectToActionResult>(await controller.Edit(SessionId, default));

        Assert.Equal(nameof(VisitPlanningController.Details), redirect.ActionName);
        Assert.Equal(SessionId, redirect.RouteValues!["planningSessionId"]);
        Assert.IsType<ViewResult>(await Arrange(SessionBody("draft"), Read, Generate).Controller.Edit(SessionId, default));
    }

    [Fact]
    public void The_details_view_hides_every_write_action_behind_the_flags_and_says_read_only()
    {
        var details = View("Details.cshtml");

        Assert.Contains("data-read-only=\"@Model.IsReadOnly", details);
        Assert.Matches(@"@if \(Model\.IsReadOnly\)\s*\{\s*<div[^>]*id=""vp-readonly-notice""[^>]*>.*Localizer\[""ReadOnlyNotice""\]", details);
        foreach (var id in new[] { "vp-apply", "vp-replan", "vp-preview", "vp-save-targets", "vp-add-account", "vp-select-all-doctors", "vp-clear-selection" })
        {
            var at = details.IndexOf($"id=\"{id}\"", StringComparison.Ordinal);
            Assert.True(at > 0, id);
            var guard = details.LastIndexOf("@if (Model.Can", at, StringComparison.Ordinal);
            Assert.True(guard > 0 && at - guard < 900, $"{id} is not behind a permission flag");
        }

        Assert.Contains("@if (Model.CanGenerate)\n            {\n                <a href=\"/CRM/VisitPlanning/Edit/", details.Replace("\r\n", "\n"));
        Assert.Equal("Bu plan onaylı ve salt okunur.", Resx("tr")["ReadOnlyNotice"]);

        // The script never re-enables a write on a read-only page.
        var js = Asset("details.js");
        Assert.Contains("const readOnly = root.dataset.readOnly === 'true';", js);
        Assert.Contains("if (!sessionData || readOnly) return;", js);
        Assert.Contains("if (window.Sortable && el('vp-visit-cards') && !readOnly)", js);
    }

    // ============================================================ C3 — the browser no longer reads the calendar

    [Fact]
    public void The_route_tab_runs_on_the_planners_non_working_days_and_the_calendar_proxy_is_gone()
    {
        Assert.Null(typeof(VisitPlanningController).GetMethod("WorkingCalendar"));
        var controller = File.ReadAllText(Path.Combine(WebRoot(), "Controllers", "CRM", "VisitPlanningController.cs"));
        Assert.DoesNotContain("api/working-calendar\"", controller);
        Assert.DoesNotContain("/api/platform/working-calendars", controller);

        var js = Asset("details.js");
        Assert.DoesNotContain("/working-calendar", js);
        Assert.Contains("nonWorkingDates = new Set(p.nonWorkingDates || [])", js);
        Assert.Contains("st.status === 'unresolved'", js);
        Assert.Contains("L.CalendarUnresolvedWarning", js);
        Assert.Contains("id=\"vp-calendar-warning\"", View("Details.cshtml"));
        Assert.Equal("Çalışma takvimi okunamadı; tatiller dikkate alınmadı.", Resx("tr")["CalendarUnresolvedWarning"]);
    }

    // ============================================================ 7 · A1 strategy picker removed

    [Fact]
    public void The_strategy_template_picker_and_its_proxy_are_gone()
    {
        Assert.Null(typeof(VisitPlanningController).GetMethod("StrategyTemplates"));
        Assert.DoesNotContain("strategy-templates", File.ReadAllText(Path.Combine(WebRoot(), "Controllers", "CRM", "VisitPlanningController.cs")));

        var form = View("_Form.cshtml");
        Assert.DoesNotContain("vp-strategy", form);
        Assert.DoesNotContain("StrategyTemplate", form);

        var formJs = Asset("form.js");
        Assert.DoesNotContain("/strategy-templates", formJs);
        Assert.DoesNotContain("vp-strategy", formJs);
        // WP-VP-2 (B-3) superseded the back-send: the form sends no play at all (the server derives it), and the
        // segment field went with K-4 (WP-VP-2 VisitPlanningPhase2WebTests pins both).
        Assert.DoesNotContain("strategyTemplateId", formJs);
        Assert.DoesNotContain("id=\"vp-segment\"", form);
        Assert.All(Languages, l => Assert.False(Resx(l).ContainsKey("StrategyTemplate"), l));
    }

    // ============================================================ 6 · D6 / A5 seven languages

    [Fact]
    public void The_resx_family_has_the_same_non_empty_keys_in_seven_languages_without_echo()
    {
        var english = Resx("en");
        var reference = english.Keys.ToHashSet(StringComparer.Ordinal);
        Assert.True(reference.SetEquals(Resx(null).Keys), "neutral resx differs from en");
        foreach (var language in Languages)
        {
            var values = Resx(language);
            Assert.True(reference.SetEquals(values.Keys),
                $"{language}: missing [{string.Join(", ", reference.Except(values.Keys))}] extra [{string.Join(", ", values.Keys.Except(reference))}]");
            Assert.All(values, kv =>
            {
                Assert.False(string.IsNullOrWhiteSpace(kv.Value), $"{language}: {kv.Key} is empty");
                if (english[kv.Key] != kv.Key) Assert.NotEqual(kv.Key, kv.Value);
            });
        }
    }

    [Fact]
    public void Turkish_is_real_turkish_so_no_english_word_is_upper_cased_into_a_dotted_capital_i()
    {
        var english = Resx("en");
        var turkish = Resx("tr");
        // "Plan" is the Turkish word too. Everything else differs from English — an English text left in tr is exactly
        // what CSS upper-cases into "PERİOD" / "HOSPİTAL".
        var same = turkish.Where(kv => kv.Value == english[kv.Key]).Select(kv => kv.Key).ToList();
        Assert.Equal(["Plan"], same);

        Assert.Equal("Dönem", turkish["CyclePeriod"]);
        Assert.Equal("Rota oluştur", turkish["RouteAction"]);
        Assert.Equal("Yeniden planla", turkish["Replan"]);
        Assert.Equal("Yeni plan", turkish["NewSession"]);
        Assert.Equal("Doktorlar", turkish["ContactsPanel"]);
        Assert.Equal("Kurumlar (klinik / hastane)", turkish["AccountsPanel"]);
        Assert.Equal("Bölge dışı hesaplar gizlenmez; uyarıyla gösterilir.", turkish["AccountsHint"]);
        Assert.DoesNotContain(turkish.Values, v => v.Contains("backend", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Every_key_the_views_and_scripts_read_exists_in_the_family_and_the_bridge()
    {
        var keys = Resx("en");
        var views = Directory.GetFiles(Path.Combine(WebRoot(), "Views", "CRM", "VisitPlanning"), "*.cshtml")
            .Select(File.ReadAllText).ToList();
        var used = views.SelectMany(v => Regex.Matches(v, @"(?<!Shared)Localizer\[""([^""]+)""\]").Select(m => m.Groups[1].Value))
            .Distinct().ToList();
        Assert.True(used.Count > 90);
        Assert.All(used, k => Assert.True(keys.ContainsKey(k), $"a view reads '{k}' which the resx lacks"));

        var bridge = Bridge();
        var scripts = Asset("index.js") + Asset("form.js") + Asset("details.js");
        var read = Regex.Matches(scripts, @"(?<![\w.])L\.([A-Z][A-Za-z0-9_]*)").Select(m => m.Groups[1].Value).Distinct().ToList();
        Assert.True(read.Count > 60);
        Assert.All(read, k => Assert.Matches($@"(?<![A-Za-z0-9_]){Regex.Escape(k)}\s*=", bridge));
    }

    // ============================================================ D6 reference labels (account-type, medical-specialty)

    [Fact]
    public async Task Institution_type_and_specialty_labels_come_from_their_reference_sets()
    {
        var (gateway, controller) = Arrange((_, uri) => uri.Contains("/account-type/")
                ? (HttpStatusCode.OK, """{"isSuccessful":true,"data":{"setCode":"account-type","items":[{"code":"hospital","label":"Hastane","isActive":true}]}}""")
                : uri.Contains("/medical-specialty/")
                    ? (HttpStatusCode.OK, """{"isSuccessful":true,"data":{"setCode":"medical-specialty","items":[{"code":"urology","label":"Üroloji","isActive":true}]}}""")
                    : (HttpStatusCode.NotFound, "{}"),
            Read);

        var result = Assert.IsType<OkObjectResult>(await controller.ReferenceLabels(default));
        var data = JsonDocument.Parse(JsonSerializer.Serialize(result.Value)).RootElement.GetProperty("data");

        Assert.Equal("Hastane", data.GetProperty("accountTypes").GetProperty("hospital").GetString());
        Assert.Equal("Üroloji", data.GetProperty("specialties").GetProperty("urology").GetString());
        Assert.Contains(gateway.Requests, r => r.Uri.EndsWith("/consumable-sets/account-type/published-values", StringComparison.Ordinal));
        Assert.Contains(gateway.Requests, r => r.Uri.EndsWith("/consumable-sets/medical-specialty/published-values", StringComparison.Ordinal));

        var js = Asset("details.js");
        Assert.Contains("api('/reference-labels')", js);
        Assert.Contains("esc(typeLabel(at))", js);
        Assert.Contains("esc(specLabel(specialty))", js);
        Assert.DoesNotContain("text-uppercase\">' + esc(specialty)", js);
    }

    [Fact]
    public async Task Reference_labels_need_the_read_key()
    {
        var (gateway, controller) = Arrange(SessionBody("draft"));

        Assert.Equal(403, Assert.IsType<ObjectResult>(await controller.ReferenceLabels(default)).StatusCode);
        Assert.Empty(gateway.Requests);
    }

    // ============================================================ 5 · menu labels (D7)

    [Fact]
    public void The_two_visit_pages_are_named_in_seven_languages_without_echo()
    {
        foreach (var language in Languages)
        {
            var values = XDocument.Load(Path.Combine(WebRoot(), "Resources", $"SharedResource.{language}.resx")).Root!
                .Elements("data")
                .ToDictionary(d => (string)d.Attribute("name")!, d => ((string?)d.Element("value") ?? string.Empty).Trim());
            foreach (var key in new[] { "Nav.Page.VISITPLANNING", "Nav.Page.VISITEXECUTION" })
            {
                Assert.True(values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value), $"{language}: {key}");
                Assert.NotEqual(key, value);
            }

            if (language == "tr")
            {
                Assert.Equal("Ziyaret Planlama", values["Nav.Page.VISITPLANNING"]);
                Assert.Equal("Ziyaret Yürütme", values["Nav.Page.VISITEXECUTION"]);
            }
        }
    }

    // ============================================================ helpers

    private static (HttpStatusCode, string) SessionBody(string status) =>
        (HttpStatusCode.OK, $$$"""{"isSuccessful":true,"data":{"planningSessionId":"{{{SessionId}}}","status":"{{{status}}}"}}""");

    private static (Gateway Gateway, VisitPlanningController Controller) Arrange(
        (HttpStatusCode, string) answer, params string[] permissions) => Arrange((_, _) => answer, permissions);

    private static (Gateway Gateway, VisitPlanningController Controller) Arrange(
        Func<HttpMethod, string, (HttpStatusCode, string)> route, params string[] permissions)
    {
        var gateway = new Gateway(route);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = GatewayUrl }).Build();
        var controller = new VisitPlanningController(
            new HttpClient(gateway), configuration, NullLogger<VisitPlanningController>.Instance);
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
        };
        return (gateway, controller);
    }

    /// <summary>The body of a <c>const name = s =&gt; { … };</c> arrow function (first balanced brace block).</summary>
    private static string Function(string script, string name)
    {
        var start = script.IndexOf($"const {name} = ", StringComparison.Ordinal);
        Assert.True(start >= 0, $"{name} not found");
        var open = script.IndexOf('{', start);
        var depth = 0;
        for (var i = open; i < script.Length; i++)
        {
            if (script[i] == '{') depth++;
            else if (script[i] == '}' && --depth == 0) return script[start..(i + 1)];
        }

        throw new InvalidOperationException($"{name} is not balanced");
    }

    private static string View(string file) =>
        File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "VisitPlanning", file));

    private static string Bridge() => View("_IndexL10n.cshtml");

    private static string Asset(string file) =>
        File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", "VisitPlanning", file));

    private static Dictionary<string, string> Resx(string? language) =>
        XDocument.Load(Path.Combine(WebRoot(), "Resources", "Views", "CRM", "VisitPlanning",
                language is null ? "VisitPlanningIndex.resx" : $"VisitPlanningIndex.{language}.resx")).Root!
            .Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty, StringComparer.Ordinal);

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }

    private sealed class Gateway(Func<HttpMethod, string, (HttpStatusCode Status, string Body)> route) : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Uri, string? Tenant)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!.ToString();
            Requests.Add((request.Method, uri, request.Headers.TryGetValues("X-Tenant-Id", out var t) ? t.Single() : null));
            var (status, payload) = route(request.Method, uri);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(payload, Encoding.UTF8, "application/json") });
        }
    }
}
