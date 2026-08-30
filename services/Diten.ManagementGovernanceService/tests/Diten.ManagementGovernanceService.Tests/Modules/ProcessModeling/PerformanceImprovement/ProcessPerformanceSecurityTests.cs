using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Diten.ManagementGovernanceService.Api.Controllers;
using Diten.ManagementGovernanceService.Api.LocalTestSecurity;
using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling;
using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.PerformanceImprovement;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.ManagementGovernanceService.Tests.Modules.ProcessModeling.PerformanceImprovement;

public sealed class ProcessPerformanceSecurityTests
{
    private static readonly string[] ExactPermissions =
    [
        PerformanceImprovementPermissions.KpiRead,
        PerformanceImprovementPermissions.KpiCreate,
        PerformanceImprovementPermissions.KpiArchive
    ];

    [Fact]
    public void Kpi_operations_are_an_ordinal_closed_set_and_do_not_reuse_core_permissions()
    {
        Assert.Equal(3, ExactPermissions.Length);
        Assert.Equal(3, ExactPermissions.Distinct(StringComparer.Ordinal).Count());
        Assert.All(ExactPermissions, permission =>
        {
            Assert.Equal(permission, PerformanceImprovementPermissions.ExactOperationMap[permission]);
            Assert.DoesNotContain(permission, ProcessModelingPermissions.ExactPermissions, StringComparer.Ordinal);
            Assert.False(PerformanceImprovementPermissions.ExactOperationMap.ContainsKey(permission.ToUpperInvariant()));
            Assert.False(PerformanceImprovementPermissions.ExactOperationMap.ContainsKey($"{permission}.*"));
            Assert.False(PerformanceImprovementPermissions.ExactOperationMap.ContainsKey($" {permission}"));
        });
    }

    [Theory]
    [MemberData(nameof(DependencyBlockedOperations))]
    public void Current_fu16_never_turns_a_kpi_operation_into_allow(string operation)
    {
        var gate = new ProcessKpiBindingDependencyGate();

        var result = gate.Evaluate(operation, Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal("kpi_reference_contract_unavailable", result.Code);
        Assert.Equal(0, gate.StoreInvocationCount);
    }

    [Fact]
    public async Task Local_identity_recognizes_only_exact_kpi_permissions_without_grant_inference()
    {
        foreach (var permission in ExactPermissions)
        {
            await using var exact = await AuthenticationHost.StartAsync([permission]);
            Assert.Equal(HttpStatusCode.NoContent, (await exact.Client.GetAsync("/secured")).StatusCode);

            foreach (var invalid in new[] { permission.ToUpperInvariant(), $"{permission}.*", $" {permission}", permission + " " })
            {
                await using var rejected = await AuthenticationHost.StartAsync([invalid]);
                Assert.Equal(HttpStatusCode.Unauthorized, (await rejected.Client.GetAsync("/secured")).StatusCode);
            }
        }

        await using var duplicate = await AuthenticationHost.StartAsync([ExactPermissions[0], ExactPermissions[0]]);
        Assert.Equal(HttpStatusCode.Unauthorized, (await duplicate.Client.GetAsync("/secured")).StatusCode);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"metricId\":\"guessed-free-text-identity\"}")]
    public async Task Create_binding_rejects_every_non_empty_contract_shape_before_dependency_gate(string body)
    {
        var controller = CreateController(PerformanceImprovementPermissions.KpiCreate, body);

        var result = await controller.CreateBinding(CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task Empty_create_body_with_exact_context_and_permission_remains_dependency_unavailable()
    {
        var controller = CreateController(PerformanceImprovementPermissions.KpiCreate, string.Empty);

        var result = await controller.CreateBinding(CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
    }

    public static IEnumerable<object[]> DependencyBlockedOperations() =>
        ExactPermissions.Select(permission => new object[] { permission });

    private static ProcessPerformanceLocalTestController CreateController(string permission, string body)
    {
        var tenantId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var actorId = Guid.Parse("22222222-2222-4222-8222-222222222222");
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Tenant-Id"] = tenantId.ToString("D");
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tenant_id", tenantId.ToString("D")),
            new Claim("sub", actorId.ToString("D")),
            new Claim("permission", permission)
        ], LocalTestAuthenticationHandler.SchemeName));

        return new ProcessPerformanceLocalTestController(new ProcessKpiBindingDependencyGate())
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
    }

    private sealed class IdentitySource(IReadOnlyList<string> permissions) : ILocalTestIdentitySource
    {
        public LocalTestIdentity? Resolve() => new(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            Guid.Parse("22222222-2222-4222-8222-222222222222"),
            Guid.Parse("33333333-3333-4333-8333-333333333333"),
            permissions);
    }

    private sealed class AuthenticationHost(WebApplication application, HttpClient client) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;

        public static async Task<AuthenticationHost> StartAsync(IReadOnlyList<string> permissions)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddSingleton<ILocalTestIdentitySource>(new IdentitySource(permissions));
            builder.Services.AddAuthentication(LocalTestAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, LocalTestAuthenticationHandler>(
                    LocalTestAuthenticationHandler.SchemeName, _ => { });
            builder.Services.AddAuthorization();

            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapGet("/secured", () => Results.NoContent()).RequireAuthorization(policy =>
            {
                policy.AddAuthenticationSchemes(LocalTestAuthenticationHandler.SchemeName);
                policy.RequireAuthenticatedUser();
            });
            await app.StartAsync();

            var client = app.GetTestClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(LocalTestAuthenticationHandler.SchemeName);
            return new AuthenticationHost(app, client);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await application.StopAsync();
            await application.DisposeAsync();
        }
    }
}
