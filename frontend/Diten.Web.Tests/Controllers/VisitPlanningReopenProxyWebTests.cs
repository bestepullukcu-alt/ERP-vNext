using System.Net;
using System.Security.Claims;
using System.Text;
using Diten.Web.Controllers.CRM;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VP-4A (2) — the week-reopen proxy on the REAL controller: the CRM path and the body pass through unchanged, CRM's
/// refusal envelope (409 week_not_approved …) comes back as it is, and the keys are apply's (apply AND
/// planned-visit.manage) — without either nothing reaches the gateway.
/// </summary>
public sealed class VisitPlanningReopenProxyWebTests
{
    private const string GatewayUrl = "http://gateway.test";
    private static readonly Guid TenantId = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");

    [Fact]
    public async Task Reopen_forwards_path_and_body_and_returns_the_crm_envelope_unchanged()
    {
        const string refusal = "{\"data\":null,\"errors\":[\"week_not_approved\",\"Only an approved week can be reopened.\"],\"statusCode\":409,\"isSuccessful\":false}";
        var (gateway, controller) = Arrange(HttpStatusCode.Conflict, refusal, "crm.visit-plan.apply", "crm.planned-visit.manage");
        var session = Guid.NewGuid();
        const string body = "{\"reason\":\"doktor izinde\",\"expectedVersion\":7}";
        controller.ControllerContext.HttpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        controller.ControllerContext.HttpContext.Request.ContentType = "application/json";

        var result = await controller.ReopenWeek(session, "2026-10-12", default);

        var sent = Assert.Single(gateway.Requests);
        Assert.Equal(($"{GatewayUrl}/api/crm/visit-plan/sessions/{session}/weeks/2026-10-12/reopen", "POST", body, TenantId.ToString()),
            (sent.Uri, sent.Method, sent.Body, sent.Tenant));
        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(409, content.StatusCode);
        Assert.Equal(refusal, content.Content);
    }

    [Theory]
    [InlineData("crm.visit-plan.apply")]
    [InlineData("crm.planned-visit.manage")]
    [InlineData("crm.visit-plan.read")]
    public async Task Reopen_needs_both_apply_keys(string onlyKey)
    {
        var (gateway, controller) = Arrange(HttpStatusCode.OK, "{}", onlyKey);
        controller.ControllerContext.HttpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));

        var result = await controller.ReopenWeek(Guid.NewGuid(), "2026-10-12", default);

        Assert.Equal(403, ((ObjectResult)result).StatusCode);
        Assert.Empty(gateway.Requests);
    }

    private static (Gateway, VisitPlanningController) Arrange(HttpStatusCode status, string response, params string[] permissions)
    {
        var gateway = new Gateway(status, response);
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

    private sealed class Gateway(HttpStatusCode status, string response) : HttpMessageHandler
    {
        public List<(string Uri, string Method, string? Body, string? Tenant)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request.RequestUri!.ToString(), request.Method.Method,
                request.Content is null ? null : await request.Content.ReadAsStringAsync(ct),
                request.Headers.TryGetValues("X-Tenant-Id", out var t) ? t.Single() : null));
            return new HttpResponseMessage(status) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        }
    }
}
