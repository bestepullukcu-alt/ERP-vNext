using System.Net;
using System.Security.Claims;
using System.Text;
using Diten.Web.Controllers.CRM;
using Diten.Web.Models.CRM;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VW-W1 (F-RBAC) — on the REAL Visit Execution proxy: the page and every proxy use the canonical
/// <c>crm.visit-report.read / record / amend</c> keys (record also needs <c>crm.planned-visit.manage</c>, as the CRM
/// endpoint does) and the old territory fallback opens nothing.
/// </summary>
public sealed class VisitExecutionRbacWebTests
{
    private const string GatewayUrl = "http://gateway.test";
    private static readonly Guid TenantId = Guid.Parse("97c5aaaa-0000-0000-0000-000000000001");

    private const string Read = "crm.visit-report.read";
    private const string Record = "crm.visit-report.record";
    private const string Amend = "crm.visit-report.amend";
    private const string PlannedVisitManage = "crm.planned-visit.manage";
    private static readonly string[] OldFallback = { "crm.territory.read", "crm.territory.model.manage" };

    [Fact]
    public void Page_with_only_the_old_territory_fallback_is_forbidden()
    {
        var (_, controller) = Arrange(OldFallback);
        var result = Assert.IsType<StatusCodeResult>(controller.Index());
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    [Fact]
    public void Page_flags_follow_the_canonical_keys()
    {
        var reader = Model(Arrange(Read).Controller.Index());
        Assert.False(reader.CanRecord);
        Assert.False(reader.CanAmend);

        // record needs BOTH keys (the CRM endpoint requires both).
        Assert.False(Model(Arrange(Read, Record).Controller.Index()).CanRecord);
        Assert.True(Model(Arrange(Read, Record, PlannedVisitManage).Controller.Index()).CanRecord);
        Assert.True(Model(Arrange(Read, Amend).Controller.Index()).CanAmend);

        // the fallback next to read opens neither action.
        var withFallback = Model(Arrange(new[] { Read }.Concat(OldFallback).ToArray()).Controller.Index());
        Assert.False(withFallback.CanRecord);
        Assert.False(withFallback.CanAmend);
    }

    [Fact]
    public async Task Proxies_refuse_the_old_fallback_and_accept_the_canonical_keys()
    {
        var (fallbackGateway, fallback) = Arrange(OldFallback);
        Assert.Equal(403, Status(await fallback.Calendar(CancellationToken.None)));
        Assert.Equal(403, Status(await fallback.Contract(CancellationToken.None)));
        Assert.Equal(403, Status(await fallback.RecordOutcome(CancellationToken.None)));
        Assert.Equal(403, Status(await fallback.Submit(CancellationToken.None)));
        Assert.Equal(403, Status(await fallback.Amend(Guid.NewGuid(), CancellationToken.None)));
        Assert.Empty(fallbackGateway.Requests);

        var (gateway, rep) = Arrange(Read, Record, PlannedVisitManage, Amend);
        Assert.Equal(200, Status(await rep.Calendar(CancellationToken.None)));
        Assert.Equal(200, Status(await rep.RecordOutcome(CancellationToken.None)));
        Assert.Equal(200, Status(await rep.Amend(Guid.NewGuid(), CancellationToken.None)));
        Assert.Equal(3, gateway.Requests.Count);
    }

    [Fact]
    public void Controller_source_carries_no_territory_fallback()
    {
        var source = File.ReadAllText(Path.Combine(WebRoot(), "Controllers", "CRM", "CrmVisitExecutionController.cs"));
        Assert.DoesNotContain("crm.territory", source);
        Assert.DoesNotContain("Fallback", source);
    }

    // ============================================================ helpers

    private static VisitExecutionIndexViewModel Model(IActionResult result)
        => Assert.IsType<VisitExecutionIndexViewModel>(Assert.IsType<ViewResult>(result).Model);

    private static int? Status(IActionResult result) => result switch
    {
        ContentResult c => c.StatusCode,
        ObjectResult o => o.StatusCode,
        StatusCodeResult s => s.StatusCode,
        _ => null
    };

    private static (Gateway Gateway, CrmVisitExecutionController Controller) Arrange(params string[] permissions)
    {
        var gateway = new Gateway();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = GatewayUrl }).Build();
        var controller = new CrmVisitExecutionController(
            new HttpClient(gateway), configuration, NullLogger<CrmVisitExecutionController>.Instance);
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"plannedVisitId\":\"" + Guid.NewGuid() + "\"}"));
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return (gateway, controller);
    }

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }

    private sealed class Gateway : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":{}}", Encoding.UTF8, "application/json")
            });
        }
    }
}
