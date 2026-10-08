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
/// WP-VP-2 (Acceptance 7) — the Web side of visit planning phase 2 on the REAL controllers, views, scripts and resx:
/// the planning form has a read-only rep and no segment / play / user picker (and no user / segment / strategy proxy);
/// the Targets search reads the rep's territory universe ("my accounts"), the whole-tenant search is a separate
/// out-of-territory picker, the unassigned banner is a seven-language key, and the doctor table lost its link-GUID
/// column; Planned Visits shows the target by name (list + detail), sends neither a campaign nor — without read-all — a
/// resource; the execution card shows the target name.
/// </summary>
public sealed class VisitPlanningPhase2WebTests
{
    private const string GatewayUrl = "http://gateway.test";
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
    private static readonly Guid TenantId = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");

    // ============================================================ Visit Planning form (B-1 / B-3)

    [Fact]
    public void The_planning_form_has_a_read_only_rep_and_no_segment_play_or_user_picker()
    {
        var form = View("VisitPlanning", "_Form.cshtml");
        Assert.Matches(@"<input[^>]*id=""vp-resource-name""[^>]*readonly", form);
        foreach (var gone in new[] { "id=\"vp-resource\"", "vp-segment", "vp-strategy", "vp-users-note" })
        {
            Assert.DoesNotContain(gone, form);
        }

        var js = Asset("VisitPlanning", "form.js");
        Assert.Contains("api('/me')", js);
        foreach (var gone in new[] { "/users", "/segments", "/strategy-templates", "segmentId", "strategyTemplateId", "resourceId:", "campaignId" })
        {
            Assert.DoesNotContain(gone, js);
        }

        // No user-directory / segment / play proxy any more; "me" and "my accounts" read CRM.
        foreach (var gone in new[] { "Users", "Segments", "StrategyTemplates" })
        {
            Assert.Null(typeof(VisitPlanningController).GetMethod(gone));
        }
    }

    [Fact]
    public async Task Me_and_my_accounts_are_read_proxies_onto_crm()
    {
        var (gateway, controller) = Arrange("crm.visit-plan.read");
        controller.ControllerContext.HttpContext.Request.QueryString = new QueryString("?search=kli&pageSize=30");

        await controller.Me(default);
        await controller.MyAccounts(default);

        Assert.Equal(
            [$"{GatewayUrl}/api/crm/resources/me", $"{GatewayUrl}/api/crm/visit-plan/my-accounts?search=kli&pageSize=30"],
            gateway.Requests.Select(r => r.Uri).ToArray());
        Assert.All(gateway.Requests, r => Assert.Equal(TenantId.ToString(), r.Tenant));

        var (blocked, denied) = Arrange();
        Assert.Equal(403, ((ObjectResult)await denied.MyAccounts(default)).StatusCode);
        Assert.Empty(blocked.Requests);
    }

    // ============================================================ Targets (B-2 / B-8)

    [Fact]
    public void Targets_search_the_territory_universe_with_a_separate_out_of_territory_picker_and_no_link_column()
    {
        var details = View("VisitPlanning", "Details.cshtml");
        // WP-VP-4H (6) — the territory universe is the "My accounts" list itself (no separate dropdown any more)
        Assert.Contains("id=\"vp-acc-list\"", details);
        Assert.DoesNotContain("id=\"vp-add-account\"", details);
        Assert.Contains("id=\"vp-add-account-out\"", details);
        Assert.Contains("id=\"vp-territory-banner\"", details);
        Assert.DoesNotContain("ColLinked", details);

        var js = Asset("VisitPlanning", "details.js");
        Assert.Contains("api('/my-accounts?search=' + encodeURIComponent(term) + '&page=' + next + '&pageSize=' + ACCOUNT_PAGE)", js);
        Assert.DoesNotContain("initAccountPicker('vp-add-account', '/my-accounts', false)", js);
        Assert.Contains("initAccountPicker('vp-add-account-out', '/accounts', true)", js);
        Assert.Contains("status !== 'unassigned'", js);
        Assert.DoesNotContain("{ data: 'linkId' }", js);
        // The summary never falls back to a raw id.
        Assert.DoesNotContain("return c ? c.name : cid;", js);
        Assert.Contains("c.contactDisplayName", js);
    }

    [Fact]
    public void The_new_visit_planning_keys_are_in_seven_languages_and_the_bridge()
    {
        var bridge = View("VisitPlanning", "_IndexL10n.cshtml");
        foreach (var key in new[] { "TerritoryUnassignedBanner", "AddOutOfTerritory", "RepSelfHint" })
        {
            foreach (var language in Languages)
            {
                var values = Resx("VisitPlanning", "VisitPlanningIndex", language);
                Assert.True(values.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) && v != key, $"{language}: {key}");
            }
        }

        Assert.Matches(@"(?<![A-Za-z0-9_])TerritoryUnassignedBanner\s*=", bridge);
        Assert.Matches(@"(?<![A-Za-z0-9_])AddOutOfTerritory\s*=", bridge);
        Assert.Equal("Size bölge atanmamış; tüm hesaplar gösteriliyor. Yöneticinize başvurun.",
            Resx("VisitPlanning", "VisitPlanningIndex", "tr")["TerritoryUnassignedBanner"]);
        Assert.Equal("Bölge dışı hesap ekle", Resx("VisitPlanning", "VisitPlanningIndex", "tr")["AddOutOfTerritory"]);
        Assert.All(Languages, l => Assert.False(Resx("VisitPlanning", "VisitPlanningIndex", l).ContainsKey("Segments"), l));
    }

    // ============================================================ Planned Visits (B-8 / B-1 / B-3)

    [Fact]
    public void The_planned_visit_list_shows_the_target_by_name_and_searches_by_it()
    {
        var js = Asset("PlannedVisits", "index.js");
        Assert.Contains("row.targetDisplayName", js);
        Assert.DoesNotContain("(row.targetId || '').slice(0, 8)", js);
        Assert.Contains("t === 'display' ? targetCell(row) : [targetName(row), row.accountDisplayName || ''].join(' ').trim()", js);
        Assert.Matches(@"(?<![A-Za-z0-9_])TargetUnknown\s*=", View("PlannedVisits", "_IndexL10n.cshtml"));
    }

    [Fact]
    public void The_planned_visit_detail_names_and_links_the_target_instead_of_a_guid()
    {
        var details = View("PlannedVisits", "Details.cshtml");
        Assert.DoesNotContain("<div>@Model.TargetId</div>", details);
        Assert.Contains("Model.TargetDisplayName", details);
        Assert.Contains("/CRM/Contacts/Details/", details);
        Assert.Contains("/CRM/Accounts/Details/", details);
        Assert.DoesNotContain("Model.CampaignId", details);
    }

    [Fact]
    public void Without_read_all_the_form_payload_carries_no_resource_and_never_a_campaign()
    {
        var model = new PlannedVisitEditViewModel { VisitCode = "PV-1", ResourceId = "someone-else", ResourceType = "person", CanReadAll = false };
        var rep = JsonSerializer.Serialize(PlannedVisitsController.ToPayload(model, includeExpectedVersion: false));
        Assert.Contains("\"resourceId\":null", rep);
        Assert.Contains("\"resourceType\":null", rep);
        Assert.DoesNotContain("campaignId", rep);

        model.CanReadAll = true;
        var manager = JsonSerializer.Serialize(PlannedVisitsController.ToPayload(model, includeExpectedVersion: false));
        Assert.Contains("\"resourceId\":\"someone-else\"", manager);

        var form = View("PlannedVisits", "_Form.cshtml");
        Assert.DoesNotContain("asp-for=\"CampaignId\"", form);
        Assert.Contains("@if (Model.CanReadAll)", form);
        Assert.Null(typeof(PlannedVisitEditViewModel).GetProperty("CampaignId"));
        foreach (var language in Languages)
        {
            var values = Resx("PlannedVisits", "PlannedVisitsIndex", language);
            foreach (var key in new[] { "TargetUnknown", "TargetInactive", "ResourceSelfHint" })
            {
                Assert.True(values.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v), $"{language}: {key}");
            }
        }

        Assert.Equal("Pasif", Resx("PlannedVisits", "PlannedVisitsIndex", "tr")["TargetInactive"]);
    }

    [Fact]
    public void The_execution_card_shows_the_target_name_under_the_visit_code()
    {
        var js = Asset("VisitExecution", "visit-execution.js");
        var code = js.IndexOf("esc(it.visitCode)", StringComparison.Ordinal);
        var name = js.IndexOf("esc(it.targetDisplayName)", StringComparison.Ordinal);
        Assert.True(code > 0 && name > code, "the target name follows the visit code");
    }

    // ============================================================ helpers

    private static (Gateway, VisitPlanningController) Arrange(params string[] permissions)
    {
        var gateway = new Gateway();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = GatewayUrl }).Build();
        var controller = new VisitPlanningController(new HttpClient(gateway), configuration, NullLogger<VisitPlanningController>.Instance);
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
        };
        return (gateway, controller);
    }

    private static string View(string module, string file) =>
        File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", module, file));

    private static string Asset(string module, string file) =>
        File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", module, file));

    private static Dictionary<string, string> Resx(string module, string family, string language) =>
        XDocument.Load(Path.Combine(WebRoot(), "Resources", "Views", "CRM", module, $"{family}.{language}.resx")).Root!
            .Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty, StringComparer.Ordinal);

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }

    private sealed class Gateway : HttpMessageHandler
    {
        public List<(string Uri, string? Tenant)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request.RequestUri!.ToString(), request.Headers.TryGetValues("X-Tenant-Id", out var t) ? t.Single() : null));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"data\":{}}", Encoding.UTF8, "application/json") });
        }
    }
}
