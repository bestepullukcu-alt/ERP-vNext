using System.Security.Claims;
using Diten.Platform.API.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

public sealed class VerifiedReferenceDataServiceTenantContextTests
{
    [Fact]
    public async Task ExactServicePrincipalDerivesOnlyTokenTenant()
    {
        var tenantId = Guid.NewGuid();
        var authentication = new RecordingAuthenticationService(Success(Principal(tenantId)));
        var http = Context(authentication);

        var result = await new VerifiedReferenceDataServiceTenantContext().ResolveAsync(http);

        Assert.True(result.IsAuthenticated);
        Assert.True(result.IsAuthorized);
        Assert.Equal(tenantId, result.TenantId);
        Assert.Equal(
            [TrustedServiceTokenValidationExtensions.ReferenceDataAuthenticationScheme],
            authentication.Schemes);
    }

    [Fact]
    public async Task TenantHeaderIsRejectedBeforeAuthentication()
    {
        var authentication = new RecordingAuthenticationService(AuthenticateResult.NoResult());
        var http = Context(authentication);
        http.Request.Headers["X-Tenant-Id"] = Guid.NewGuid().ToString("D");

        var result = await new VerifiedReferenceDataServiceTenantContext().ResolveAsync(http);

        Assert.True(result.IsAuthenticated);
        Assert.False(result.IsAuthorized);
        Assert.Empty(authentication.Schemes);
    }

    [Theory]
    [InlineData("Bearer token", "Bearer second")]
    [InlineData("bearer token", null)]
    [InlineData("Bearer  token", null)]
    public async Task AmbiguousOrMalformedAuthorizationHeaderIsForbidden(
        string first,
        string? second)
    {
        var authentication = new RecordingAuthenticationService(AuthenticateResult.NoResult());
        var http = Context(authentication);
        http.Request.Headers.Append("Authorization", first);
        if (second is not null)
        {
            http.Request.Headers.Append("Authorization", second);
        }

        var result = await new VerifiedReferenceDataServiceTenantContext().ResolveAsync(http);

        Assert.True(result.IsAuthenticated);
        Assert.False(result.IsAuthorized);
        Assert.Empty(authentication.Schemes);
    }

    [Theory]
    [InlineData("actor_type", "tenant_user")]
    [InlineData("service_name", "Other.Service")]
    [InlineData("aud", "TRUSTED_WORKFLOW_CONSUMER")]
    [InlineData("tenant_id", "00000000-0000-0000-0000-000000000000")]
    public async Task WrongPurposeOrIdentityClaimIsForbidden(string claimType, string value)
    {
        var principal = Principal(Guid.NewGuid());
        var claims = principal.Claims
            .Select(claim => string.Equals(claim.Type, claimType, StringComparison.Ordinal)
                ? new Claim(claim.Type, value)
                : claim)
            .ToArray();
        var authentication = new RecordingAuthenticationService(Success(new ClaimsPrincipal(
            new ClaimsIdentity(claims, TrustedServiceTokenValidationExtensions.ReferenceDataAuthenticationScheme))));

        var result = await new VerifiedReferenceDataServiceTenantContext().ResolveAsync(Context(authentication));

        Assert.True(result.IsAuthenticated);
        Assert.False(result.IsAuthorized);
        Assert.Null(result.TenantId);
    }

    [Fact]
    public async Task DuplicateTenantClaimIsForbidden()
    {
        var principal = Principal(Guid.NewGuid());
        var identity = Assert.IsType<ClaimsIdentity>(principal.Identity);
        identity.AddClaim(new Claim("tenant_id", Guid.NewGuid().ToString("D")));
        var authentication = new RecordingAuthenticationService(Success(principal));

        var result = await new VerifiedReferenceDataServiceTenantContext().ResolveAsync(Context(authentication));

        Assert.True(result.IsAuthenticated);
        Assert.False(result.IsAuthorized);
    }

    private static ClaimsPrincipal Principal(Guid tenantId) => new(new ClaimsIdentity(
    [
        new Claim("sub", Guid.NewGuid().ToString("D")),
        new Claim("tenant_id", tenantId.ToString("D")),
        new Claim("actor_type", "service"),
        new Claim("service_name", TrustedServiceTokenValidationExtensions.RequiredServiceName),
        new Claim("aud", TrustedServiceTokenValidationExtensions.ReferenceDataRequiredAudience)
    ], TrustedServiceTokenValidationExtensions.ReferenceDataAuthenticationScheme));

    private static AuthenticateResult Success(ClaimsPrincipal principal) => AuthenticateResult.Success(
        new AuthenticationTicket(principal, TrustedServiceTokenValidationExtensions.ReferenceDataAuthenticationScheme));

    private static DefaultHttpContext Context(IAuthenticationService authentication)
    {
        var services = new ServiceCollection()
            .AddSingleton(authentication)
            .AddSingleton(typeof(IAuthenticationService), authentication)
            .BuildServiceProvider();
        return new DefaultHttpContext { RequestServices = services };
    }

    private sealed class RecordingAuthenticationService(AuthenticateResult result) : IAuthenticationService
    {
        public List<string> Schemes { get; } = [];

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            Schemes.Add(scheme ?? string.Empty);
            return Task.FromResult(result);
        }

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            throw new NotSupportedException();
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            throw new NotSupportedException();
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) =>
            throw new NotSupportedException();
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            throw new NotSupportedException();
    }
}
