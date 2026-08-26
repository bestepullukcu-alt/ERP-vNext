using System.Security.Claims;
using Diten.Platform.API.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

public sealed class VerifiedGskuResolverJwtTenantContextTests
{
    [Fact]
    public async Task ValidatedTenantUserClaim_IsTheOnlyTenantSource()
    {
        var tenantId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("actor_type", "tenant_user"),
            new Claim("tenant_id", tenantId.ToString())
        ], "Bearer"));
        var context = CreateContext(AuthenticateResult.Success(
            new AuthenticationTicket(principal, "Bearer")));

        var result = await new VerifiedGskuResolverJwtTenantContext().ResolveAsync(context);

        Assert.True(result.IsAuthorized);
        Assert.Equal(tenantId, result.TenantId);
    }

    [Theory]
    [InlineData("platform_admin")]
    [InlineData("partner_admin")]
    public async Task AdministrativeActor_IsRejected(string actorType)
    {
        var tenantId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [new Claim("actor_type", actorType), new Claim("tenant_id", tenantId.ToString())], "Bearer"));
        var context = CreateContext(AuthenticateResult.Success(new AuthenticationTicket(principal, "Bearer")));

        var result = await new VerifiedGskuResolverJwtTenantContext().ResolveAsync(context);

        Assert.True(result.IsAuthenticated);
        Assert.False(result.IsAuthorized);
    }

    [Fact]
    public async Task TenantHeader_IsRejectedEvenWithValidJwt()
    {
        var tenantId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [new Claim("actor_type", "tenant_user"), new Claim("tenant_id", tenantId.ToString())], "Bearer"));
        var context = CreateContext(AuthenticateResult.Success(new AuthenticationTicket(principal, "Bearer")));
        context.Request.Headers["X-Tenant-Id"] = tenantId.ToString();

        var result = await new VerifiedGskuResolverJwtTenantContext().ResolveAsync(context);

        Assert.False(result.IsAuthorized);
    }

    [Fact]
    public async Task MissingAuthentication_IsUnauthenticated()
    {
        var result = await new VerifiedGskuResolverJwtTenantContext().ResolveAsync(
            CreateContext(AuthenticateResult.NoResult()));

        Assert.False(result.IsAuthenticated);
        Assert.False(result.IsAuthorized);
    }

    [Fact]
    public async Task InvalidAuthentication_IsUnauthenticated()
    {
        var result = await new VerifiedGskuResolverJwtTenantContext().ResolveAsync(
            CreateContext(AuthenticateResult.Fail("invalid-test-token")));

        Assert.False(result.IsAuthenticated);
        Assert.False(result.IsAuthorized);
    }

    [Fact]
    public async Task MissingMalformedOrMultipleTenantClaim_IsForbidden()
    {
        var tenantId = Guid.NewGuid();
        var principals = new[]
        {
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("actor_type", "tenant_user")], "Bearer")),
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("actor_type", "tenant_user"), new Claim("tenant_id", tenantId.ToString()), new Claim("tenant_id", Guid.NewGuid().ToString())], "Bearer")),
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("actor_type", "tenant_user"), new Claim("tenant_id", "not-a-guid")], "Bearer")),
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("actor_type", "tenant_user"), new Claim("tenant_id", " ")], "Bearer"))
        };

        foreach (var principal in principals)
        {
            var result = await new VerifiedGskuResolverJwtTenantContext().ResolveAsync(
                CreateContext(AuthenticateResult.Success(new AuthenticationTicket(principal, "Bearer"))));
            Assert.True(result.IsAuthenticated);
            Assert.False(result.IsAuthorized);
        }
    }

    [Fact]
    public async Task TwoDifferentTenantUsers_AreResolvedWithoutCredentialTenantConstraint()
    {
        var resolver = new VerifiedGskuResolverJwtTenantContext();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var resultA = await resolver.ResolveAsync(CreateTenantUserContext(tenantA));
        var resultB = await resolver.ResolveAsync(CreateTenantUserContext(tenantB));

        Assert.True(resultA.IsAuthorized);
        Assert.True(resultB.IsAuthorized);
        Assert.Equal(tenantA, resultA.TenantId);
        Assert.Equal(tenantB, resultB.TenantId);
    }

    private static DefaultHttpContext CreateTenantUserContext(Guid tenantId)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("actor_type", "tenant_user"), new Claim("tenant_id", tenantId.ToString())],
            "Bearer"));
        return CreateContext(AuthenticateResult.Success(new AuthenticationTicket(principal, "Bearer")));
    }

    private static DefaultHttpContext CreateContext(AuthenticateResult authenticationResult)
    {
        var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(new StubAuthenticationService(authenticationResult))
            .BuildServiceProvider();
        return new DefaultHttpContext { RequestServices = services };
    }

    private sealed class StubAuthenticationService(AuthenticateResult result) : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => Task.FromResult(result);
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }
}
