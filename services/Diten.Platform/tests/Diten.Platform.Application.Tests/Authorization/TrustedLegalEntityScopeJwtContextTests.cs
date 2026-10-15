using System.Security.Claims;
using Diten.Platform.API.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.Platform.Application.Tests.AccessGovernance;

public sealed class TrustedLegalEntityScopeJwtContextTests
{
    [Fact]
    public async Task Independently_validated_exact_claims_preserve_all_exact_permissions()
    {
        var tenant = Guid.NewGuid(); var subject = Guid.NewGuid();
        var result = await new TrustedLegalEntityScopeJwtContext().ResolveAsync(Context([
            new("tenant_id", tenant.ToString()), new("sub", subject.ToString()), new("actor_type", "tenant_user"),
            new("permission", "mdm.gskus.read"), new("permission", "mdm.lskus.read") ]));

        Assert.True(result.Authenticated); Assert.True(result.Authorized);
        Assert.Equal(tenant, result.TenantId); Assert.Equal(subject, result.SubjectId);
        Assert.True(result.HasExactPermission("mdm.gskus.read")); Assert.False(result.HasExactPermission("MDM.GSKUS.READ"));
    }

    [Theory]
    [InlineData("Tenant_Id", "sub", "actor_type")]
    [InlineData("tenant_id", "SUB", "actor_type")]
    [InlineData("tenant_id", "sub", "Actor_Type")]
    public async Task Claim_type_aliases_or_case_variants_are_denied(string tenantClaim, string subjectClaim, string actorClaim)
    {
        var result = await new TrustedLegalEntityScopeJwtContext().ResolveAsync(Context([
            new(tenantClaim, Guid.NewGuid().ToString()), new(subjectClaim, Guid.NewGuid().ToString()),
            new(actorClaim, "tenant_user"), new("permission", "mdm.gskus.read") ]));
        Assert.True(result.Authenticated); Assert.False(result.Authorized);
    }

    [Fact]
    public async Task Duplicate_missing_malformed_admin_and_scope_headers_are_denied()
    {
        var baseClaims = new[] { new Claim("tenant_id", Guid.NewGuid().ToString()), new Claim("sub", Guid.NewGuid().ToString()), new Claim("actor_type", "tenant_user") };
        var duplicate = baseClaims.Append(new Claim("tenant_id", Guid.NewGuid().ToString()));
        Assert.False((await new TrustedLegalEntityScopeJwtContext().ResolveAsync(Context(duplicate))).Authorized);
        Assert.False((await new TrustedLegalEntityScopeJwtContext().ResolveAsync(Context([new("tenant_id", "bad"), new("sub", Guid.NewGuid().ToString()), new("actor_type", "tenant_user")]))).Authorized);
        Assert.False((await new TrustedLegalEntityScopeJwtContext().ResolveAsync(Context([new("tenant_id", Guid.NewGuid().ToString()), new("sub", Guid.NewGuid().ToString()), new("actor_type", "platform_admin")]))).Authorized);
        foreach (var header in new[] { "X-Tenant-Id", "X-Legal-Entity-Id", "X-Legal-Entity-Ids" })
        {
            var context = Context(baseClaims); context.Request.Headers[header] = Guid.NewGuid().ToString();
            Assert.False((await new TrustedLegalEntityScopeJwtContext().ResolveAsync(context)).Authorized);
        }
    }

    [Fact]
    public async Task Missing_or_failed_authentication_is_unauthenticated()
    {
        foreach (var auth in new[] { AuthenticateResult.NoResult(), AuthenticateResult.Fail("bad") })
        {
            var result = await new TrustedLegalEntityScopeJwtContext().ResolveAsync(Context(auth));
            Assert.False(result.Authenticated); Assert.False(result.Authorized);
        }
    }

    private static DefaultHttpContext Context(IEnumerable<Claim> claims) => Context(AuthenticateResult.Success(
        new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")), "Bearer")));
    private static DefaultHttpContext Context(AuthenticateResult result) => new()
    { RequestServices = new ServiceCollection().AddSingleton<IAuthenticationService>(new StubAuthenticationService(result)).BuildServiceProvider() };

    private sealed class StubAuthenticationService(AuthenticateResult result) : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => Task.FromResult(result);
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }
}
