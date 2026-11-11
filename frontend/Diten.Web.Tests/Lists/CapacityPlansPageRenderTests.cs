using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Diten.Web.Tests.Lists;

/// <summary>
/// MOD-0192 Capacity (G6, 2026-10-18) — the entry page and the plan workspace through the REAL web tier: routing, the token
/// bridge, the Razor views with the tenant shell. Nothing is called beyond the web tier (gateway and Platform point at a
/// closed local port). UAS-001: without read, the denied card and nothing of the page; with read, the surfaces, and the
/// action buttons only for the permissions held. The workspace hands the page only a scope the adapter would accept.
/// </summary>
public sealed class CapacityPlansPageRenderTests : IClassFixture<WebApplicationFactory<Program>>
{
    // Test-only signing material for a token this test makes and this test's web host checks; not a credential.
    private const string SigningKey = "capacity-render-test-signing-key-0123456789abcdef";
    private const string Issuer = "capacity-render-test";
    private const string Audience = "capacity-render-test";
    private const string ClosedPort = "http://127.0.0.1:9";
    private const string Read = "supplychain.capacity-plans.read";
    private const string PlanId = "44444444-4444-4444-8444-444444444444";
    private const string LegalEntity = "22222222-2222-4222-8222-222222222222";

    private readonly WebApplicationFactory<Program> _factory;

    public CapacityPlansPageRenderTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Theory]
    [InlineData("/SupplyChain/CapacityPlans")]
    [InlineData("/SupplyChain/CapacityPlans/Details/" + PlanId + "?legalEntityId=" + LegalEntity)]
    public async Task Without_read_only_the_denied_card_is_drawn(string path)
    {
        var (status, html) = await GetPageAsync(path, "supplychain.capacity-plans.create", "supplychain.capacity-plans.evaluate");
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("diten-access-denied", html);
        Assert.DoesNotContain("capacity-plans-entry", html);
        Assert.DoesNotContain("capacity-plan-details", html);
        Assert.DoesNotContain("Skeleton", html);
        Assert.DoesNotContain("capacity-plans-l10n", html);
        Assert.DoesNotContain("CapacityPlans/index.js", html);
        Assert.DoesNotContain("CapacityPlans/details.js", html);
    }

    [Fact]
    public async Task A_reader_without_create_gets_open_by_id_and_no_create_surface()
    {
        var (status, html) = await GetPageAsync("/SupplyChain/CapacityPlans", Read);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("id=\"capacity-plans-entry\"", html);
        Assert.Contains("id=\"formOpenPlan\"", html);
        Assert.Contains("id=\"scopeLegalEntity\"", html);
        Assert.DoesNotContain("btnCreatePlan", html);
        Assert.DoesNotContain("offcanvasCreateEdit", html);
        Assert.Contains("id=\"capacity-plans-l10n\"", html);
        Assert.Contains("CapacityPlans/index.js", html);
    }

    [Fact]
    public async Task A_creator_gets_the_seven_field_create_offcanvas()
    {
        var (_, html) = await GetPageAsync("/SupplyChain/CapacityPlans", Read, "supplychain.capacity-plans.create");
        Assert.Contains("id=\"btnCreatePlan\"", html);
        Assert.Contains("id=\"offcanvasCreateEdit\"", html);
        foreach (var field in new[] { "planName", "planHorizonStart", "planHorizonEnd", "planDemandPlanId", "planDemandPlanVersion", "planSourceCapturedAt", "planSourceChecksum" })
            Assert.Contains($"id=\"{field}\"", html);
        Assert.Contains("__RequestVerificationToken", html);
    }

    [Fact]
    public async Task The_workspace_carries_the_plan_and_the_scope_and_only_the_held_actions()
    {
        var (status, html) = await GetPageAsync($"/SupplyChain/CapacityPlans/Details/{PlanId}?legalEntityId={LegalEntity}", Read, "supplychain.capacity-plans.scenario.create");
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains($"data-plan-id=\"{PlanId}\"", html);
        Assert.Contains($"data-legal-entity-id=\"{LegalEntity}\"", html);
        Assert.Contains("id=\"btnCreateScenario\"", html);
        Assert.Contains("id=\"offcanvasScenario\"", html);
        Assert.Contains("id=\"evaluationSection\"", html);
        Assert.DoesNotContain("btnEvaluate", html);
        Assert.DoesNotContain("offcanvasEvaluate", html);
        Assert.Contains("CapacityPlans/details.js", html);
    }

    [Fact]
    public async Task A_malformed_scope_reaches_the_workspace_as_empty_so_the_script_fails_closed()
    {
        var (_, html) = await GetPageAsync($"/SupplyChain/CapacityPlans/Details/{PlanId}?legalEntityId=not-a-uuid", Read);
        Assert.Contains("data-legal-entity-id=\"\"", html);
        Assert.DoesNotContain("not-a-uuid", html);
    }

    private async Task<(HttpStatusCode Status, string Html)> GetPageAsync(string path, params string[] permissions)
    {
        using var client = _factory
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("JwtSettings:Secret", SigningKey);
                builder.UseSetting("JwtSettings:Issuer", Issuer);
                builder.UseSetting("JwtSettings:Audience", Audience);
                builder.UseSetting("GatewayUrl", ClosedPort);
                builder.UseSetting("PlatformServiceUrl", ClosedPort);
            })
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var claims = new List<Claim>
        {
            new("sub", Guid.NewGuid().ToString("D")),
            new("tenant_id", Guid.NewGuid().ToString("D")),
            new("actor_type", "tenant_user")
        };
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256)));

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"access_token={token}");
        request.Headers.Add("Accept", "text/html");
        using var response = await client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }
}
