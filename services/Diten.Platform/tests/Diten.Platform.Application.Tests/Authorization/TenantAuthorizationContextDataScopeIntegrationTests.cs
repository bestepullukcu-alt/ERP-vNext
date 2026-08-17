using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Infrastructure.Authorization;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

public sealed class TenantAuthorizationContextDataScopeIntegrationTests
{
    private static readonly Guid TenantId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid UserId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    [Fact]
    public async Task InitializeAsync_hydrates_org_fields_from_data_scope_resolver_once()
    {
        var orgUnitId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var positionId = Guid.Parse("22222222-3333-4444-5555-666666666666");
        var legalEntityId = Guid.Parse("33333333-4444-5555-6666-777777777777");
        var managerId = Guid.Parse("44444444-5555-6666-7777-888888888888");
        var resolver = new Mock<IDataScopeResolver>();
        resolver
            .Setup(x => x.ResolveAsync(TenantId, UserId, string.Empty, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntitlementDataScope[]
            {
                new EntitlementDataScope(EntitlementDataScopeKind.OrgUnit, orgUnitId, "ORG-001"),
                new EntitlementDataScope(EntitlementDataScopeKind.Position, positionId, "POS-001"),
                new EntitlementDataScope(EntitlementDataScopeKind.LegalEntity, legalEntityId, "LE-001"),
                new EntitlementDataScope(EntitlementDataScopeKind.Country, "TR"),
                new EntitlementDataScope(EntitlementDataScopeKind.ManagerChain, managerId, "MGR-001")
            });
        var context = CreateContext(resolver.Object);

        await context.InitializeAsync();
        await context.InitializeAsync();

        Assert.Equal(new[] { orgUnitId }, context.OrgUnitIds);
        Assert.Equal(new[] { positionId }, context.PositionIds);
        Assert.Equal(legalEntityId, context.LegalEntityId);
        Assert.Equal("TR", context.Country);
        Assert.Equal(new[] { managerId }, context.ManagerChain);
        resolver.Verify(
            x => x.ResolveAsync(TenantId, UserId, string.Empty, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_keeps_defaults_when_data_scope_resolver_throws()
    {
        var resolver = new Mock<IDataScopeResolver>();
        resolver
            .Setup(x => x.ResolveAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("resolver unavailable"));
        var context = CreateContext(resolver.Object);

        await context.InitializeAsync();
        await context.InitializeAsync();

        Assert.Empty(context.OrgUnitIds);
        Assert.Empty(context.PositionIds);
        Assert.Null(context.LegalEntityId);
        Assert.Null(context.Country);
        Assert.Empty(context.ManagerChain);
        resolver.Verify(
            x => x.ResolveAsync(TenantId, UserId, string.Empty, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void Org_fields_remain_default_until_explicit_initialize_is_called()
    {
        var resolver = new Mock<IDataScopeResolver>(MockBehavior.Strict);
        var context = CreateContext(resolver.Object);

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

    private static JwtTenantAuthorizationContext CreateContext(IDataScopeResolver resolver)
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[]
                    {
                        new Claim("tenant_id", TenantId.ToString()),
                        new Claim(JwtRegisteredClaimNames.Sub, UserId.ToString()),
                        new Claim("actor_type", "tenant_user")
                    },
                    "Test"))
            }
        };

        return new JwtTenantAuthorizationContext(accessor, resolver);
    }
}
