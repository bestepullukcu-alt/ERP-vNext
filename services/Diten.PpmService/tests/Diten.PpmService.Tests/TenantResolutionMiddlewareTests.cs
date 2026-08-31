using System.Security.Claims;
using Diten.PpmService.Api.Security;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Diten.PpmService.Tests;

public sealed class TenantResolutionMiddlewareTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task Matching_authenticated_tenant_reaches_the_endpoint()
    {
        var (context, reached) = await Invoke(TenantA.ToString("D"), TenantA.ToString("D"));

        Assert.True(reached());
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task Missing_header_preserves_the_authenticated_tenant()
    {
        var (context, reached) = await Invoke(TenantA.ToString("D"), null);

        Assert.True(reached());
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task Conflicting_header_is_canonical_problem_details()
    {
        var (context, reached) = await Invoke(TenantA.ToString("D"), TenantB.ToString("D"));

        Assert.False(reached());
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        var payload = await ReadBody(context);
        Assert.Contains("\"title\":\"Tenant mismatch\"", payload, StringComparison.Ordinal);
        Assert.Contains("\"conflictingSignals\":[\"header\"]", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("reason_code", payload, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Invalid_header_is_not_reported_as_a_contradiction(string header)
    {
        var (context, reached) = await Invoke(TenantA.ToString("D"), header);

        Assert.False(reached());
        Assert.Equal("application/problem+json", context.Response.ContentType);
        var payload = await ReadBody(context);
        Assert.Contains("\"title\":\"Invalid Tenant Identity Format\"", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("conflictingSignals", payload, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, "Missing Tenant")]
    [InlineData("not-a-guid", "Invalid Tenant Identity Format")]
    [InlineData("00000000-0000-0000-0000-000000000000", "Invalid Tenant Identity Format")]
    public async Task Missing_or_invalid_authenticated_tenant_is_distinct_from_contradiction(
        string? claim,
        string expectedTitle)
    {
        var (context, reached) = await Invoke(claim, TenantA.ToString("D"));

        Assert.False(reached());
        Assert.Equal("application/problem+json", context.Response.ContentType);
        var payload = await ReadBody(context);
        Assert.Contains($"\"title\":\"{expectedTitle}\"", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("conflictingSignals", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unauthenticated_request_is_left_for_authorization()
    {
        var reached = false;
        var context = new DefaultHttpContext();
        var middleware = new TenantResolutionMiddleware(_ =>
        {
            reached = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.True(reached);
    }

    [Theory]
    [InlineData("OPTIONS", "/api/v1/ppm/portfolios")]
    [InlineData("GET", "/health")]
    [InlineData("GET", "/swagger/index.html")]
    public async Task Infrastructure_paths_bypass_tenant_resolution(string method, string path)
    {
        var reached = false;
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.User = new ClaimsPrincipal(new ClaimsIdentity([], "test"));
        var middleware = new TenantResolutionMiddleware(_ =>
        {
            reached = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.True(reached);
    }

    private static async Task<(DefaultHttpContext Context, Func<bool> Reached)> Invoke(
        string? tenantClaim,
        string? header)
    {
        var reached = false;
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var claims = tenantClaim is null ? Array.Empty<Claim>() : [new Claim("tenant_id", tenantClaim)];
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        if (header is not null)
        {
            context.Request.Headers["X-Tenant-Id"] = header;
        }

        var middleware = new TenantResolutionMiddleware(_ =>
        {
            reached = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);
        return (context, () => reached);
    }

    private static async Task<string> ReadBody(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        return await new StreamReader(context.Response.Body).ReadToEndAsync();
    }
}
