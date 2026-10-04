using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Diten.Platform.API.Security;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

/// <summary>
/// BL-521 — the production "PlatformActor" and "PlatformAdminOnly" policies (DependencyInjection.AddPlatformActorPolicies)
/// on their own, over the production scope registration: a partner administrator passes "PlatformActor" only on an
/// endpoint that admits partners (PartnerAdminScopeAttribute), never "PlatformAdminOnly". FIX1 F1 — who is a partner is
/// the RECORD's answer: a platform_admin token whose record says PartnerAdmin (a demoted administrator) is held to the
/// partner rule, and a platform_admin token without a live record (F7) passes neither on a global endpoint.
/// </summary>
public sealed class PlatformActorPolicyPartnerTests
{
    private const string Email = "policy.probe@bl521.test";

    [Theory]
    [InlineData("platform_admin", "PlatformAdmin", false, true)]
    [InlineData("platform_admin", "PlatformAdmin", true, true)]
    [InlineData("partner_admin", "PartnerAdmin", false, false)]
    [InlineData("partner_admin", "PartnerAdmin", true, true)]
    [InlineData("tenant_user", "PlatformAdmin", true, false)]
    // FIX1 F1 — demoted: the token says platform_admin, the record says PartnerAdmin
    [InlineData("platform_admin", "PartnerAdmin", false, false)]
    [InlineData("platform_admin", "PartnerAdmin", true, true)]
    // FIX1 F7 — a platform_admin token with no record at all
    [InlineData("platform_admin", null, false, false)]
    public async Task A_partner_passes_the_platform_actor_policy_only_where_the_endpoint_admits_partners(
        string actorType, string? recordActorType, bool admitsPartners, bool expected)
    {
        Assert.Equal(expected, await AuthorizeAsync("PlatformActor", actorType, recordActorType, admitsPartners));
    }

    [Theory]
    [InlineData("platform_admin", "PlatformAdmin", true)]
    [InlineData("partner_admin", "PartnerAdmin", false)]
    [InlineData("platform_admin", "PartnerAdmin", false)] // FIX1 F1 — demoted
    [InlineData("platform_admin", null, false)] // FIX1 F7 — no record
    public async Task Only_a_platform_administrator_its_record_confirms_passes_the_platform_admin_only_policy(
        string actorType, string? recordActorType, bool expected)
    {
        // Even on an endpoint that admits partners: this policy is the "platform administrators only" door.
        Assert.Equal(expected, await AuthorizeAsync("PlatformAdminOnly", actorType, recordActorType, admitsPartners: true));
    }

    private static async Task<bool> AuthorizeAsync(string policy, string actorType, string? recordActorType, bool admitsPartners)
    {
        var administrators = new Mock<IPlatformAdministratorRepository>();
        if (recordActorType is not null)
        {
            administrators.Setup(x => x.GetByNormalizedEmailAsync(Email, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PlatformAdministrator
                {
                    Email = Email, NormalizedEmail = Email, ActorType = Enum.Parse<ActorType>(recordActorType),
                    AllowedTenantIds = [Guid.NewGuid()], Status = AdministratorStatus.Active
                });
        }

        var root = new ServiceCollection().AddLogging()
            .AddAuthorization(Diten.Platform.Infrastructure.DependencyInjection.AddPlatformActorPolicies)
            .AddPlatformActorTenantScope()
            .AddSingleton(administrators.Object)
            .BuildServiceProvider();
        using var request = root.CreateScope();

        var http = new DefaultHttpContext { RequestServices = request.ServiceProvider };
        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            admitsPartners ? new EndpointMetadataCollection(new PartnerAdminScopeAttribute("id")) : EndpointMetadataCollection.Empty, "probe"));
        http.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("actor_type", actorType), new Claim(JwtRegisteredClaimNames.Email, Email)], "Bearer"));
        request.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = http;

        var result = await root.GetRequiredService<IAuthorizationService>().AuthorizeAsync(http.User, http, policy);
        return result.Succeeded;
    }
}
