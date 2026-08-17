using System.Security.Claims;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Infrastructure.Authorization;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

public sealed class AnonymousTenantAuthorizationContextTests
{
    [Fact]
    public async Task Anonymous_context_returns_default_semantics()
    {
        var context = new AnonymousTenantAuthorizationContext();

        await context.InitializeAsync();

        AssertDefaults(context);
    }

    [Fact]
    public async Task Jwt_context_returns_anonymous_semantics_when_http_context_is_null()
    {
        var resolver = new Mock<IDataScopeResolver>(MockBehavior.Strict);
        var accessor = new HttpContextAccessor();
        var context = new JwtTenantAuthorizationContext(accessor, resolver.Object);

        await context.InitializeAsync();

        AssertDefaults(context);
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
    public async Task Jwt_context_returns_anonymous_semantics_when_user_is_not_authenticated()
    {
        var resolver = new Mock<IDataScopeResolver>(MockBehavior.Strict);
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("tenant_id", Guid.NewGuid().ToString()) }))
            }
        };
        var context = new JwtTenantAuthorizationContext(accessor, resolver.Object);

        await context.InitializeAsync();

        AssertDefaults(context);
        resolver.Verify(
            x => x.ResolveAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static void AssertDefaults(ITenantAuthorizationContext context)
    {
        Assert.Equal(Guid.Empty, context.TenantId);
        Assert.Equal(Guid.Empty, context.UserId);
        Assert.Null(context.ActorType);
        Assert.False(context.IsAuthenticated);
        Assert.False(context.IsPlatformAdmin);
        Assert.Empty(context.PermissionKeys);
        Assert.Empty(context.RoleIds);
        Assert.Empty(context.RoleNames);
        Assert.Empty(context.OrgUnitIds);
        Assert.Empty(context.PositionIds);
        Assert.Null(context.LegalEntityId);
        Assert.Null(context.Country);
        Assert.Empty(context.ManagerChain);
    }
}
