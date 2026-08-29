using System.Security.Claims;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Infrastructure.Workflow;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Diten.MdmService.Application.Tests.Workflow;

public sealed class ProductIdentityDelegatedTokenAccessorTests
{
    [Fact]
    public void Exact_authenticated_human_bearer_is_returned_without_mutation()
    {
        var context = Context([new("sub", Guid.NewGuid().ToString("D"))]);
        context.Request.Headers.Authorization = "Bearer opaque.jwt.value";

        Assert.Equal("opaque.jwt.value", Accessor(context).GetRequiredToken());
    }

    [Fact]
    public void Service_actor_is_never_accepted_as_delegated_maker()
    {
        var context = Context([new("sub", Guid.NewGuid().ToString("D")), new("actor_type", "service")]);
        context.Request.Headers.Authorization = "Bearer secret-token";

        var error = Assert.Throws<ProductIdentityDelegatedTokenException>(() => Accessor(context).GetRequiredToken());
        Assert.DoesNotContain("secret-token", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Conflicting_subjects_or_authority_headers_fail_closed()
    {
        var context = Context([new("sub", Guid.NewGuid().ToString("D")), new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString("D"))]);
        context.Request.Headers.Authorization = "Bearer token";
        Assert.Throws<ProductIdentityDelegatedTokenException>(() => Accessor(context).GetRequiredToken());

        context = Context([new("sub", Guid.NewGuid().ToString("D"))]);
        context.Request.Headers.Authorization = "Bearer token";
        context.Request.Headers["X-Tenant-Id"] = Guid.NewGuid().ToString("D");
        Assert.Throws<ProductIdentityDelegatedTokenException>(() => Accessor(context).GetRequiredToken());
    }

    [Theory]
    [InlineData("")]
    [InlineData("Basic abc")]
    [InlineData("bearer token")]
    [InlineData("Bearer  token")]
    public void Missing_or_non_exact_authorization_fails_closed(string authorization)
    {
        var context = Context([new("sub", Guid.NewGuid().ToString("D"))]);
        if (authorization.Length > 0) context.Request.Headers.Authorization = authorization;
        Assert.Throws<ProductIdentityDelegatedTokenException>(() => Accessor(context).GetRequiredToken());
    }

    private static HttpContextProductIdentityDelegatedTokenAccessor Accessor(HttpContext context) =>
        new(new HttpContextAccessor { HttpContext = context });
    private static DefaultHttpContext Context(IEnumerable<Claim> claims)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        return context;
    }
}
