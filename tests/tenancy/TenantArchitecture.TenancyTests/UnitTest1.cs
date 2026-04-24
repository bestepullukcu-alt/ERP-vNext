using System.Security.Claims;
using Diten.ApiGateway.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace TenantArchitecture.TenancyTests;

public class GatewayTenantResolutionTests
{
    [Fact]
    public async Task ProtectedPath_WithoutTenant_Returns400()
    {
        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, NullLogger<TenantResolutionMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/products";

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task PlatformLoginPath_WithoutTenant_AllowsRequest()
    {
        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, NullLogger<TenantResolutionMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/platform-auth/login";

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task JwtTenant_OverridesConflictingHeader()
    {
        var jwtTenant = Guid.NewGuid();
        var headerTenant = Guid.NewGuid();

        var identity = new ClaimsIdentity(new[] { new Claim("tenant_id", jwtTenant.ToString()) }, "test-auth");
        var principal = new ClaimsPrincipal(identity);

        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, NullLogger<TenantResolutionMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.User = principal;
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/items";
        context.Request.Headers["X-Tenant-Id"] = headerTenant.ToString();

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Equal(jwtTenant.ToString(), context.Request.Headers["X-Tenant-Id"].ToString());
    }
}
