using System.Security.Claims;
using Diten.PpmService.Api.Security;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Diten.PpmService.Tests;

public sealed class TenantHeaderConsistencyMiddlewareTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task Matching_authenticated_tenant_reaches_the_endpoint()
    {
        var reached = false;
        var context = Context(TenantA, TenantA.ToString("D"));
        var middleware = new TenantHeaderConsistencyMiddleware(_ =>
        {
            reached = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.True(reached);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("22222222-2222-2222-2222-222222222222")]
    [InlineData("not-a-guid")]
    public async Task Conflicting_or_malformed_tenant_is_rejected_before_the_endpoint(string header)
    {
        var reached = false;
        var context = Context(TenantA, header);
        context.Response.Body = new MemoryStream();
        var middleware = new TenantHeaderConsistencyMiddleware(_ =>
        {
            reached = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.False(reached);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var payload = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains("tenant_context_mismatch", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_header_does_not_invent_a_tenant()
    {
        var reached = false;
        var context = Context(TenantA, null);
        var middleware = new TenantHeaderConsistencyMiddleware(_ =>
        {
            reached = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.True(reached);
    }

    private static DefaultHttpContext Context(Guid tenantId, string? header)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("tenant_id", tenantId.ToString("D"))],
            "test"));
        if (header is not null)
        {
            context.Request.Headers["X-Tenant-Id"] = header;
        }

        return context;
    }
}
