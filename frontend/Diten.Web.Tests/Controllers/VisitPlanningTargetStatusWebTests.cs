using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Diten.Web.Controllers.CRM;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VP-3D — the Web side of the target-status reads on the REAL controller and script: the three read proxies onto
/// CRM (doctors of an institution, the plan's targets, related accounts in bulk) under the visit-plan read key, and the
/// Details opening reading the plan's targets ONCE and the related pharmacies in bulk instead of one
/// <c>GET /accounts/{id}</c> + one <c>related-accounts</c> per target (D5).
/// </summary>
public sealed class VisitPlanningTargetStatusWebTests
{
    private const string GatewayUrl = "http://gateway.test";
    private static readonly Guid TenantId = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");

    [Fact]
    public async Task Doctors_targets_and_bulk_related_are_read_proxies_onto_crm()
    {
        var (gateway, controller) = Arrange("crm.visit-plan.read");
        var account = Guid.NewGuid();
        var session = Guid.NewGuid();

        controller.ControllerContext.HttpContext.Request.QueryString = new QueryString("?quick=due&search=sirin");
        await controller.MyAccountDoctors(account, default);
        controller.ControllerContext.HttpContext.Request.QueryString = QueryString.Empty;
        await controller.SessionTargets(session, default);
        controller.ControllerContext.HttpContext.Request.QueryString = new QueryString("?accountIds=a,b&relationType=pharmacy");
        await controller.RelatedAccountsBulk(default);

        Assert.Equal(
            [
                $"{GatewayUrl}/api/crm/visit-plan/my-accounts/{account}/doctors?quick=due&search=sirin",
                $"{GatewayUrl}/api/crm/visit-plan/sessions/{session}/targets",
                $"{GatewayUrl}/api/crm/accounts/related?accountIds=a,b&relationType=pharmacy"
            ],
            gateway.Requests.Select(r => r.Uri).ToArray());
        Assert.All(gateway.Requests, r => Assert.Equal("GET", r.Method));
        Assert.All(gateway.Requests, r => Assert.Equal(TenantId.ToString(), r.Tenant));

        var (blocked, denied) = Arrange();
        Assert.Equal(403, ((ObjectResult)await denied.MyAccountDoctors(account, default)).StatusCode);
        Assert.Equal(403, ((ObjectResult)await denied.SessionTargets(session, default)).StatusCode);
        Assert.Equal(403, ((ObjectResult)await denied.RelatedAccountsBulk(default)).StatusCode);
        Assert.Empty(blocked.Requests);
    }

    [Fact]
    public void Details_opening_reads_the_plan_targets_once_and_related_pharmacies_in_bulk()
    {
        var js = Asset("VisitPlanning", "details.js");
        var seed = Regex.Match(js, @"const seedTargets = \(\) => \{.*?\n    \};", RegexOptions.Singleline).Value;
        Assert.NotEmpty(seed);

        // One targets read + bulk related (≤ 100 ids per request) feed the opening.
        Assert.Contains("api('/sessions/' + sessionId + '/targets' + weekQuery('?'))", js); // 4M (5): the selected week
        Assert.Contains("'/accounts/related?relationType=pharmacy&accountIds='", js);
        Assert.Contains("RELATED_BULK_MAX = 100", js);
        Assert.Contains("loadPlanTargets()", seed);
        Assert.Contains("loadRelatedPharmaciesBulk(ids)", seed);

        // No per-target GET /accounts/{id} on the opening: resolveAccount survives only as the failed-read fallback.
        Assert.DoesNotContain("ids.map(resolveAccount)", seed);
        Assert.Single(Regex.Matches(seed, @"resolveAccount\("));
        Assert.Contains("targetMap && targetMap[id] ? Promise.resolve(targetMap[id]) : resolveAccount(id)", seed);
    }

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

    private static string Asset(string module, string file) =>
        File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", module, file));

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }

    private sealed class Gateway : HttpMessageHandler
    {
        public List<(string Uri, string Method, string? Tenant)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request.RequestUri!.ToString(), request.Method.Method,
                request.Headers.TryGetValues("X-Tenant-Id", out var t) ? t.Single() : null));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":{}}", Encoding.UTF8, "application/json")
            });
        }
    }
}
