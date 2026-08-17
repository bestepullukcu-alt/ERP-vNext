using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Infrastructure.Authorization;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

public sealed class TenantAuthorizationContextHydrationTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public void Constructor_hydrates_jwt_claims_synchronously()
    {
        var resolver = new Mock<IDataScopeResolver>(MockBehavior.Strict);

        var context = CreateContext(
            CreatePrincipal(
                ("tenant_id", TenantId.ToString()),
                (JwtRegisteredClaimNames.Sub, UserId.ToString()),
                ("actor_type", "PLATFORM_ADMIN"),
                ("permission", "Platform.Tenants.Read"),
                ("permission", "Platform.Tenants.Update"),
                (ClaimTypes.Role, "PlatformOwner"),
                (ClaimTypes.Role, "SecurityAdmin")),
            resolver.Object);

        Assert.True(context.IsAuthenticated);
        Assert.Equal(TenantId, context.TenantId);
        Assert.Equal(UserId, context.UserId);
        Assert.Equal("PLATFORM_ADMIN", context.ActorType);
        Assert.True(context.IsPlatformAdmin);
        Assert.Equal(new[] { "Platform.Tenants.Read", "Platform.Tenants.Update" }, context.PermissionKeys);
        Assert.Equal(new[] { "PlatformOwner", "SecurityAdmin" }, context.RoleNames);
        Assert.Empty(context.RoleIds);
        Assert.Empty(context.OrgUnitIds);
        Assert.Empty(context.PositionIds);
        Assert.Null(context.LegalEntityId);
        Assert.Null(context.Country);
        Assert.Empty(context.ManagerChain);
        resolver.Verify(
            x => x.ResolveAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void Constructor_defaults_invalid_or_missing_claims_without_throwing()
    {
        var context = CreateContext(
            CreatePrincipal(
                ("tenant_id", "not-a-guid"),
                (JwtRegisteredClaimNames.Sub, "also-not-a-guid")));

        Assert.True(context.IsAuthenticated);
        Assert.Equal(Guid.Empty, context.TenantId);
        Assert.Equal(Guid.Empty, context.UserId);
        Assert.Null(context.ActorType);
        Assert.False(context.IsPlatformAdmin);
        Assert.Empty(context.PermissionKeys);
        Assert.Empty(context.RoleIds);
        Assert.Empty(context.RoleNames);
    }

    [Theory]
    [InlineData("platform_admin", true)]
    [InlineData("PLATFORM_ADMIN", true)]
    [InlineData("Platform_Admin", true)]
    [InlineData("partner_admin", false)]
    [InlineData("service", false)]
    public void IsPlatformAdmin_uses_ordinal_ignore_case_actor_type_match(
        string actorType,
        bool expected)
    {
        var context = CreateContext(CreatePrincipal(("actor_type", actorType)));

        Assert.Equal(expected, context.IsPlatformAdmin);
    }

    private static JwtTenantAuthorizationContext CreateContext(
        ClaimsPrincipal principal,
        IDataScopeResolver? resolver = null)
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return new JwtTenantAuthorizationContext(accessor, resolver ?? new NoOpDataScopeResolver());
    }

    private static ClaimsPrincipal CreatePrincipal(params (string Type, string Value)[] claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
            claims.Select(claim => new Claim(claim.Type, claim.Value)),
            "Test"));
    }
}
