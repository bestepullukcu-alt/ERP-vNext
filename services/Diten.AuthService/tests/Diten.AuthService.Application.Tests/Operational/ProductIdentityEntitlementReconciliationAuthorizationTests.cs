using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Diten.AuthService.Api.Operational;
using Diten.AuthService.Application.Common.Entitlements;
using Diten.AuthService.Infrastructure.Services;
using Diten.AuthService.Infrastructure.Settings;
using Diten.BuildingBlocks.Security.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text.Json;

namespace Diten.AuthService.Application.Tests.Operational;

public sealed class ProductIdentityEntitlementReconciliationAuthorizationTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("operator-inactive")]
    [InlineData("tenant-inactive")]
    [InlineData("module-missing")]
    [InlineData("malformed")]
    [InlineData("unavailable")]
    public async Task Authority_WhenPlatformReturnsNegativeOrUnavailable_DistinguishesFactsFromFailure(string mutation)
    {
        using var handler = new AuthorityHandler(mutation);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://authority.example.test") };
        var client = new PlatformTenantEntitlementClient(http, new HttpContextAccessor(),
            Options.Create(new PlatformServiceOptions { InternalApiKey = "test-only-key" }), NullLogger<PlatformTenantEntitlementClient>.Instance);
        if (mutation is "malformed" or "unavailable")
            await Assert.ThrowsAsync<InvalidOperationException>(() => client.ReadReconciliationAuthorityAsync(
                EntitlementReconciliationPlan.TargetTenant, "operator@example.test", CancellationToken.None));
        else
        {
            var snapshot = await client.ReadReconciliationAuthorityAsync(EntitlementReconciliationPlan.TargetTenant, "operator@example.test", CancellationToken.None);
            Assert.Equal(mutation != "operator-inactive", snapshot.OperatorActive);
            Assert.Equal(mutation != "tenant-inactive", snapshot.TenantActive);
            Assert.Equal(mutation == "module-missing" ? 0 : 1, snapshot.PermissionKeys.Count);
            Assert.Contains(EntitlementReconciliationPlan.TargetTenant.ToString("D"), snapshot.RequestedUri);
            Assert.Equal(3, handler.Requests.Count);
        }
        Assert.All(handler.Requests, request => Assert.Equal(HttpMethod.Get, request));
    }

    private sealed class AuthorityHandler(string mutation) : HttpMessageHandler
    {
        public List<HttpMethod> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request.Method); Assert.Equal("authority.example.test", request.RequestUri!.Host);
            Assert.True(request.Headers.Contains("X-Internal-Api-Key"));
            if (mutation == "unavailable") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            object data = request.RequestUri.AbsolutePath.EndsWith("entitled-modules-with-permissions", StringComparison.Ordinal)
                ? (mutation == "module-missing" ? Array.Empty<object>() : [new { moduleCode = EntitlementReconciliationPlan.Module, permissionKeys = new[] { "mdm.gskus.read" } }])
                : request.RequestUri.AbsolutePath.Contains("platform-administrators", StringComparison.Ordinal)
                    ? mutation == "malformed" ? new { wrong = true } : new { isActive = mutation != "operator-inactive" }
                    : new { exists = true, isActive = mutation != "tenant-inactive" };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(JsonSerializer.Serialize(new { isSuccessful = true, data }), Encoding.UTF8, "application/json") });
        }
    }
    private const string SigningSecret = "test-only-current-signing-key-64-characters-aaaaaaaaaaaaaaaaaaaaaa";
    private static TokenService Tokens() => new(Options.Create(new JwtSettings
        { Secret = SigningSecret, Issuer = "test-issuer", Audience = "test-audience" }),
        new JwtSecretRotationResolver(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["JwtSettings:Secret"] = SigningSecret }).Build()));

    [Theory]
    [InlineData("valid", true)]
    [InlineData("tenant", false)]
    [InlineData("actor", false)]
    [InlineData("missing-actor", false)]
    [InlineData("permission", false)]
    [InlineData("missing-permission", false)]
    [InlineData("password", false)]
    [InlineData("duplicate-sub", false)]
    [InlineData("alias", false)]
    [InlineData("expired", false)]
    [InlineData("deadline", false)]
    [InlineData("long-lived", false)]
    [InlineData("audience", false)]
    [InlineData("issuer", false)]
    [InlineData("signature", false)]
    public void ValidateOperatorToken_WhenRealSignedTokenChanges_EnforcesCurrentAuthorityContract(string mutation, bool accepted)
    {
        var plan = ReconciliationTestData.Plan(); var tokenService = Tokens();
        var runner = new EntitlementReconciliationOperationalRunner(new RecordingReconciliationStore(plan), tokenService,
            new RecordingEntitlementSource(plan), _ => Task.FromResult("test"));
        var claims = new List<Claim> { new("sub", plan.ActorId.ToString("D")),
            new("tenant_id", mutation == "tenant" ? plan.TenantId.ToString("D") : EntitlementReconciliationPlan.AdminTenant.ToString("D")),
            new("actor_type", mutation == "actor" ? "tenant_user" : "platform_admin"),
            new("pwd_change_required", mutation == "password" ? "true" : "false"),
            new("permission", mutation == "permission" ? "auth.roles.read" : EntitlementReconciliationPlan.RequiredPermission) };
        if (mutation == "duplicate-sub") claims.Add(new("sub", Guid.NewGuid().ToString("D")));
        if (mutation == "missing-actor") claims.RemoveAll(claim => claim.Type == "actor_type");
        if (mutation == "missing-permission") claims.RemoveAll(claim => claim.Type == "permission");
        if (mutation == "alias") claims.Add(new("tenantId", EntitlementReconciliationPlan.AdminTenant.ToString("D")));
        var expiry = DateTime.UtcNow.AddMinutes(mutation switch { "expired" => -10, "deadline" => 1, "long-lived" => 30, _ => 10 });
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(mutation == "signature" ? new string('x', 64) : SigningSecret));
        var jwt = new JwtSecurityToken(mutation == "issuer" ? "wrong" : "test-issuer", mutation == "audience" ? "wrong" : "test-audience",
            claims, expires: expiry, signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        var token = new JwtSecurityTokenHandler().WriteToken(jwt);
        if (accepted) Assert.Equal(plan.ActorId, runner.ValidateOperatorToken(token, DateTimeOffset.UtcNow.AddMinutes(2)));
        else Assert.ThrowsAny<Exception>(() => runner.ValidateOperatorToken(token, DateTimeOffset.UtcNow.AddMinutes(2)));
    }
}
