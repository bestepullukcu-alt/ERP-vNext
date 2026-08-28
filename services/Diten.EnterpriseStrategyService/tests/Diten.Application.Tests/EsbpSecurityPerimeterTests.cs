using System.Reflection;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Diten.Application.DeliveryExecutionManagement.Shared;
using Diten.Application.EnterpriseStrategy.Shared;
using Diten.WebAPI.Controllers;
using Diten.WebAPI.Middleware;
using Diten.WebAPI.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Diten.Application.Tests;

public sealed class EsbpSecurityPerimeterTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string TestSecret = "test-only-esbp-security-key-32-bytes-minimum";

    [Fact]
    public async Task Valid_bound_context_and_exact_permission_still_fail_closed_before_business_execution()
    {
        var nextCalls = 0;
        var middleware = new EsbpSecurityPerimeterMiddleware(_ =>
        {
            nextCalls++;
            return Task.CompletedTask;
        });
        var context = CreateContext(
            new EnterpriseStrategyPermissionAttribute(EnterpriseStrategyPermissions.GoalView),
            EnterpriseStrategyPermissions.GoalView);
        var tenantContext = new EsbpTenantContext();

        await middleware.InvokeAsync(context, tenantContext);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.Equal(0, nextCalls);
        Assert.True(tenantContext.IsResolved);
        Assert.Equal(TenantId, tenantContext.TenantId);
        Assert.Equal("actor-1", tenantContext.ActorId);
        Assert.Equal(EsbpSecurityPerimeterMiddleware.TenantSafetyCode, await ReadCodeAsync(context));
    }

    [Fact]
    public async Task Missing_permission_metadata_is_forbidden_and_never_executes_business_code()
    {
        var nextCalls = 0;
        var middleware = new EsbpSecurityPerimeterMiddleware(_ =>
        {
            nextCalls++;
            return Task.CompletedTask;
        });
        var context = CreateContext(permissionMetadata: null, grantedPermission: "unmapped.permission");

        await middleware.InvokeAsync(context, new EsbpTenantContext());

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Equal("FORBIDDEN", await ReadCodeAsync(context));
        Assert.Equal(0, nextCalls);
    }

    [Theory]
    [InlineData(null, "11111111-1111-1111-1111-111111111111", "INVALID_TENANT_CONTEXT")]
    [InlineData("bad-guid", "11111111-1111-1111-1111-111111111111", "INVALID_TENANT_CONTEXT")]
    [InlineData("22222222-2222-2222-2222-222222222222", "11111111-1111-1111-1111-111111111111", "TENANT_CONTEXT_MISMATCH")]
    public async Task Tenant_binding_is_exact_and_fail_closed(string? headerTenant, string claimTenant, string code)
    {
        var context = CreateContext(
            new EnterpriseStrategyPermissionAttribute(EnterpriseStrategyPermissions.GoalView),
            EnterpriseStrategyPermissions.GoalView,
            headerTenant,
            claimTenant);
        var middleware = new EsbpSecurityPerimeterMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, new EsbpTenantContext());

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal(code, await ReadCodeAsync(context));
    }

    [Fact]
    public async Task Anonymous_endpoint_metadata_is_the_only_perimeter_bypass()
    {
        var nextCalls = 0;
        var middleware = new EsbpSecurityPerimeterMiddleware(_ =>
        {
            nextCalls++;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new AllowAnonymousAttribute()),
            "health"));

        await middleware.InvokeAsync(context, new EsbpTenantContext());

        Assert.Equal(1, nextCalls);
    }

    [Fact]
    public async Task Permission_services_are_authenticated_exact_ordinal_and_have_no_bootstrap()
    {
        var exactPrincipal = Principal(EnterpriseStrategyPermissions.GoalView);
        var wrongCasePrincipal = Principal(EnterpriseStrategyPermissions.GoalView.ToUpperInvariant());
        var unauthenticated = new ClaimsPrincipal(new ClaimsIdentity());
        var strategy = new DefaultEnterpriseStrategyAuthorizationService();
        var delivery = new DefaultDeliveryExecutionManagementAuthorizationService();

        Assert.True(await strategy.HasPermissionAsync(EnterpriseStrategyPermissions.GoalView, exactPrincipal));
        Assert.False(await strategy.HasPermissionAsync(EnterpriseStrategyPermissions.GoalView, wrongCasePrincipal));
        Assert.False(await strategy.HasPermissionAsync(EnterpriseStrategyPermissions.GoalView, unauthenticated));
        Assert.False(await delivery.HasPermissionAsync(
            DeliveryExecutionManagementPermissions.InitiativeView,
            Principal(EnterpriseStrategyPermissions.InitiativeView)));
        Assert.True(await delivery.HasPermissionAsync(
            DeliveryExecutionManagementPermissions.InitiativeView,
            Principal(DeliveryExecutionManagementPermissions.InitiativeView)));
    }

    [Fact]
    public void All_147_actions_are_classified_and_only_health_is_anonymous()
    {
        var actions = typeof(HealthController).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any())
            .ToArray();

        var anonymous = actions.Where(HasAllowAnonymous).ToArray();
        var permissionMapped = actions.Where(HasPermissionMetadata).ToArray();

        Assert.Equal(147, actions.Length);
        Assert.Single(anonymous);
        Assert.Equal(nameof(HealthController), anonymous[0].DeclaringType?.Name);
        Assert.Equal(80, permissionMapped.Length);
        Assert.Equal(67, actions.Length - permissionMapped.Length);
        Assert.All(actions.Where(action => !HasAllowAnonymous(action)), action =>
            Assert.True(HasPermissionMetadata(action) || IsExplicitlyDefaultDenied(action)));
    }

    [Fact]
    public async Task Shared_http_pipeline_enforces_401_400_403_and_tenant_safety_503()
    {
        using var server = CreateServer();
        using var client = server.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/mapped")).StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/mapped")).StatusCode);

        client.DefaultRequestHeaders.Add(EsbpSecurityPerimeterMiddleware.TenantHeader, TenantId.ToString());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/mapped")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/unmapped")).StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken(permission: EnterpriseStrategyPermissions.GoalEdit));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/mapped")).StatusCode);
    }

    [Fact]
    public async Task Shared_http_pipeline_rejects_wrong_issuer_and_allows_only_explicit_health()
    {
        using var server = CreateServer();
        using var client = server.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken(issuer: "wrong-issuer"));
        client.DefaultRequestHeaders.Add(EsbpSecurityPerimeterMiddleware.TenantHeader, TenantId.ToString());

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/mapped")).StatusCode);
    }

    private static bool HasAllowAnonymous(MethodInfo method)
        => method.IsDefined(typeof(AllowAnonymousAttribute), inherit: true) ||
           method.DeclaringType?.IsDefined(typeof(AllowAnonymousAttribute), inherit: true) == true;

    private static bool HasPermissionMetadata(MethodInfo method)
        => method.IsDefined(typeof(EnterpriseStrategyPermissionAttribute), inherit: true) ||
           method.IsDefined(typeof(DeliveryExecutionManagementPermissionAttribute), inherit: true);

    private static bool IsExplicitlyDefaultDenied(MethodInfo method)
        => !HasAllowAnonymous(method) && !HasPermissionMetadata(method);

    private static DefaultHttpContext CreateContext(
        object? permissionMetadata,
        string grantedPermission,
        string? headerTenant = "11111111-1111-1111-1111-111111111111",
        string claimTenant = "11111111-1111-1111-1111-111111111111")
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.User = Principal(grantedPermission, claimTenant);
        if (headerTenant is not null)
        {
            context.Request.Headers[EsbpSecurityPerimeterMiddleware.TenantHeader] = headerTenant;
        }

        var metadata = permissionMetadata is null
            ? new EndpointMetadataCollection()
            : new EndpointMetadataCollection(permissionMetadata);
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, metadata, "business"));
        return context;
    }

    private static ClaimsPrincipal Principal(
        string permission,
        string tenantId = "11111111-1111-1111-1111-111111111111")
        => new(new ClaimsIdentity(
            new[]
            {
                new Claim(EsbpSecurityPerimeterMiddleware.ActorClaim, "actor-1"),
                new Claim(EsbpSecurityPerimeterMiddleware.ActorTypeClaim, "tenant_user"),
                new Claim(EsbpSecurityPerimeterMiddleware.TenantClaim, tenantId),
                new Claim(EsbpSecurityPerimeterMiddleware.PermissionClaim, permission)
            },
            "test"));

    private static async Task<string> ReadCodeAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        return document.RootElement.GetProperty("code").GetString()!;
    }

    private static TestServer CreateServer()
    {
        var builder = new WebHostBuilder()
            .ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["JwtSettings:Issuer"] = "diten-auth-service",
                    ["JwtSettings:Audience"] = "diten-erp",
                    ["JwtSettings:Secret"] = TestSecret
                }))
            .ConfigureServices((context, services) =>
            {
                services.AddRouting();
                services.AddEsbpSecurityPerimeter(context.Configuration);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseMiddleware<EsbpSecurityPerimeterMiddleware>();
                app.UseAuthorization();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/health", () => Results.Ok()).AllowAnonymous();
                    endpoints.MapGet("/mapped", () => Results.Ok())
                        .WithMetadata(new EnterpriseStrategyPermissionAttribute(EnterpriseStrategyPermissions.GoalView))
                        .RequireAuthorization();
                    endpoints.MapGet("/unmapped", () => Results.Ok()).RequireAuthorization();
                });
            });

        return new TestServer(builder);
    }

    private static string CreateToken(
        string permission = EnterpriseStrategyPermissions.GoalView,
        string issuer = "diten-auth-service")
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSecret)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer,
            "diten-erp",
            new[]
            {
                new Claim(EsbpSecurityPerimeterMiddleware.ActorClaim, "actor-1"),
                new Claim(EsbpSecurityPerimeterMiddleware.ActorTypeClaim, "tenant_user"),
                new Claim(EsbpSecurityPerimeterMiddleware.TenantClaim, TenantId.ToString()),
                new Claim(EsbpSecurityPerimeterMiddleware.PermissionClaim, permission)
            },
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(5),
            credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
