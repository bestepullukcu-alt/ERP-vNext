using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Web.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace Diten.Web.Tests.Controllers;

public sealed class DemandPlanningForecastControllerTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Company = Guid.NewGuid();
    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Diten.Web";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
    private sealed class CaptureHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? Uri { get; private set; }
        public string? Body { get; private set; }
        public HttpMethod? Method { get; private set; }
        public Dictionary<string, string> Headers { get; } = new();
        public HttpStatusCode Status { get; set; } = HttpStatusCode.Created;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++; Uri = request.RequestUri!.OriginalString; Method = request.Method;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            foreach (var header in request.Headers) Headers[header.Key] = string.Join(',', header.Value);
            return new HttpResponseMessage(Status)
            { Content = new StringContent("{\"data\":{\"draft\":{}}}", Encoding.UTF8, "application/json") };
        }
    }
    private static (DemandPlanningController Controller, CaptureHandler Handler) Create(
        string[]? permissions = null, bool tenantClaim = true, bool token = true)
    {
        var handler = new CaptureHandler();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["GatewayUrl"] = "http://localhost:5000" }).Build();
        var controller = new DemandPlanningController(new TestEnvironment(), new HttpClient(handler), configuration);
        var claims = (permissions ?? ["demand.plans.read", "demand.forecast-runs.create", "demand.drafts.create"])
            .Select(permission => new Claim("permission", permission)).ToList();
        if (tenantClaim) claims.Add(new Claim("tenant_id", Tenant.ToString("D")));
        var context = new DefaultHttpContext
        { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        if (token) context.Request.Headers.Cookie = "access_token=fixture-http-only-token";
        context.Request.Headers["X-Tenant-Id"] = Guid.NewGuid().ToString("D");
        context.Request.Headers["Idempotency-Key"] = "explicit-run-key";
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        return (controller, handler);
    }
    private static JsonElement Body() => JsonSerializer.SerializeToElement(new
    { planningCycleId = Guid.NewGuid(), skuId = Guid.NewGuid(), warehouseId = "WH-1",
        method = "Naive", reason = "Planner choice", expectedCycleVersion = 0 });

    [Fact]
    public async Task Create_UsesGatewayJwtServerTenantCompanyAndStableRequestKey()
    {
        var (controller, handler) = Create();
        var body = Body();
        var result = Assert.IsType<ContentResult>(await controller.CreateForecast(Company, body, default));
        Assert.Equal(201, result.StatusCode);
        Assert.Equal("http://localhost:5000/api/v2/demand/forecast-runs", handler.Uri);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(Tenant.ToString("D"), handler.Headers["X-Tenant-Id"]);
        Assert.Equal(Company.ToString("D"), handler.Headers["X-Legal-Entity-Id"]);
        Assert.Equal("Bearer fixture-http-only-token", handler.Headers["Authorization"]);
        Assert.Equal("explicit-run-key", handler.Headers["Idempotency-Key"]);
        Assert.Equal(body.GetRawText(), handler.Body);
    }

    [Theory]
    [InlineData("demand.forecast-runs.create")]
    [InlineData("demand.drafts.create")]
    [InlineData("demand.plans.read")]
    public async Task Create_MissingPermission_NeverCallsGateway(string missing)
    {
        var (controller, handler) = Create(new[] { "demand.plans.read", "demand.forecast-runs.create", "demand.drafts.create" }
            .Where(permission => permission != missing).ToArray());
        Assert.IsType<ForbidResult>(await controller.CreateForecast(Company, Body(), default));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Create_MissingServerTenantOrSession_NeverCallsGateway()
    {
        var (noTenant, first) = Create(tenantClaim: false);
        Assert.IsType<ForbidResult>(await noTenant.CreateForecast(Company, Body(), default));
        Assert.Equal(0, first.Calls);
        var (noToken, second) = Create(token: false);
        Assert.IsType<UnauthorizedResult>(await noToken.CreateForecast(Company, Body(), default));
        Assert.Equal(0, second.Calls);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(409)]
    [InlineData(503)]
    public async Task Create_PreservesReplayConflictAndOutageStatuses(int upstream)
    {
        var (controller, handler) = Create(); handler.Status = (HttpStatusCode)upstream;
        Assert.Equal(upstream, Assert.IsType<ContentResult>(
            await controller.CreateForecast(Company, Body(), default)).StatusCode);
    }

    [Fact]
    public async Task EvaluationAndDraft_ReadOnlyRoutes_StayInsideDemandGatewayFamily()
    {
        var (controller, handler) = Create();
        await controller.EvaluateForecast(Company, Guid.NewGuid(), Guid.NewGuid(), "WH/A", default);
        Assert.StartsWith("http://localhost:5000/api/v2/demand/forecast-runs/evaluation?", handler.Uri);
        Assert.Contains("warehouseId=WH%2FA", handler.Uri);
        Assert.Equal(HttpMethod.Get, handler.Method);
        var revision = Guid.NewGuid();
        await controller.ForecastDraft(revision, Company, default);
        Assert.Equal("http://localhost:5000/api/v2/demand/revisions/" + revision.ToString("D"), handler.Uri);
        Assert.Null(handler.Body);
    }
}