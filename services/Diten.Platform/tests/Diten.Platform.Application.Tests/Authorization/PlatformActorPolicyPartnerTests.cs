using System.Security.Claims;
using Diten.Platform.API.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

/// <summary>
/// BL-521 — the production "PlatformActor" policy (DependencyInjection.AddPlatformActorPolicies) on its own: a partner
/// administrator passes it only on an endpoint that admits partners (PartnerAdminScopeAttribute); a platform
/// administrator passes it everywhere. (No endpoint relies on this policy ALONE today — this pins it for the one that will.)
/// </summary>
public sealed class PlatformActorPolicyPartnerTests
{
    [Theory]
    [InlineData("platform_admin", false, true)]
    [InlineData("platform_admin", true, true)]
    [InlineData("partner_admin", false, false)]
    [InlineData("partner_admin", true, true)]
    [InlineData("tenant_user", true, false)]
    public async Task A_partner_passes_the_platform_actor_policy_only_where_the_endpoint_admits_partners(string actorType, bool admitsPartners, bool expected)
    {
        var services = new ServiceCollection().AddLogging()
            .AddAuthorization(Diten.Platform.Infrastructure.DependencyInjection.AddPlatformActorPolicies)
            .BuildServiceProvider();
        var authorization = services.GetRequiredService<IAuthorizationService>();
        var http = new DefaultHttpContext();
        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            admitsPartners ? new EndpointMetadataCollection(new PartnerAdminScopeAttribute("id")) : EndpointMetadataCollection.Empty, "probe"));
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("actor_type", actorType)], "Bearer"));

        var result = await authorization.AuthorizeAsync(user, http, "PlatformActor");

        Assert.Equal(expected, result.Succeeded);
    }
}
