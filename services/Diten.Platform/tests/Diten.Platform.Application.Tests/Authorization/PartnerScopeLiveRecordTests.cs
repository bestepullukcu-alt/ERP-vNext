using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Diten.Platform.API.Security;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

/// <summary>
/// BL-521 FIX1 F7 / F8-2 — the scope names tenants only from a LIVE administrator record: not deleted AND active, for a
/// partner and a platform administrator alike. The production repository already leaves deleted records out of its
/// e-mail lookup, so an HTTP test cannot see the scope's own "deleted" check; this file hands the production scope a
/// repository that DOES return a deleted record, so each half of the rule is measured on its own.
/// </summary>
public sealed class PartnerScopeLiveRecordTests
{
    private const string Email = "live.record.probe@bl521.test";
    private static readonly Guid Allowed = Guid.Parse("52152152-0000-4000-8000-00000000f701");

    [Theory]
    [InlineData("partner_admin", "PartnerAdmin")]
    [InlineData("platform_admin", "PlatformAdmin")]
    [InlineData("platform_admin", "PartnerAdmin")]
    public async Task A_deleted_record_names_no_tenant_even_when_the_repository_returns_it(string token, string recordType)
    {
        var scope = await ScopeAsync(token, recordType, deleted: true, AdministratorStatus.Active);

        Assert.False(scope.IsUnrestricted);
        Assert.False(scope.Allows(Allowed));
        Assert.Empty(scope.TenantIds);
    }

    [Theory]
    [InlineData("partner_admin", "PartnerAdmin")]
    [InlineData("platform_admin", "PlatformAdmin")]
    public async Task A_suspended_record_names_no_tenant(string token, string recordType)
    {
        var scope = await ScopeAsync(token, recordType, deleted: false, AdministratorStatus.Suspended);

        Assert.False(scope.IsUnrestricted);
        Assert.False(scope.Allows(Allowed));
    }

    [Fact]
    public async Task A_live_record_names_its_tenants_or_everything()
    {
        Assert.Equal([Allowed], (await ScopeAsync("partner_admin", "PartnerAdmin", false, AdministratorStatus.Active)).TenantIds);
        Assert.True((await ScopeAsync("platform_admin", "PlatformAdmin", false, AdministratorStatus.Active)).IsUnrestricted);
    }

    private static Task<Diten.Platform.Application.Contracts.PlatformTenantScope> ScopeAsync(
        string token, string recordType, bool deleted, AdministratorStatus status)
    {
        var administrators = new Mock<IPlatformAdministratorRepository>();
        administrators.Setup(x => x.GetByNormalizedEmailAsync(Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlatformAdministrator
            {
                Email = Email, NormalizedEmail = Email, ActorType = Enum.Parse<ActorType>(recordType),
                AllowedTenantIds = [Allowed], Status = status, IsDeleted = deleted
            });
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("actor_type", token), new Claim(JwtRegisteredClaimNames.Email, Email)], "Bearer"))
        };

        return new PlatformActorTenantScope(new HttpContextAccessor { HttpContext = http }, administrators.Object).GetAsync();
    }
}
