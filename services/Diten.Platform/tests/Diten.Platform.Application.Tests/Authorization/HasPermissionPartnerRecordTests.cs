using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Diten.Platform.API.Security;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

/// <summary>
/// BL-521 FIX1 F1 — the permission filter takes "is this a partner?" from the administrator RECORD (through the
/// production scope registration), not from the token. The filter on its own — no policy in front of it — so a filter
/// that went back to reading the token is red here even where a policy would still have caught the request.
/// </summary>
public sealed class HasPermissionPartnerRecordTests
{
    private const string Email = "has.permission.probe@bl521.test";

    [Theory]
    // token, record, endpoint admits partners → allowed
    [InlineData("platform_admin", "PlatformAdmin", false, true)]
    [InlineData("platform_admin", "PartnerAdmin", false, false)] // demoted: the record makes it a partner
    [InlineData("platform_admin", "PartnerAdmin", true, true)] // …which still reaches the endpoints that admit partners
    [InlineData("partner_admin", "PartnerAdmin", false, false)]
    [InlineData("partner_admin", "PartnerAdmin", true, true)]
    [InlineData("platform_admin", null, false, false)] // no record
    public async Task HasPermission_decides_partner_by_the_record_not_the_token(string tokenActorType, string? recordActorType, bool admitsPartners, bool expected)
    {
        var administrators = new Mock<IPlatformAdministratorRepository>();
        if (recordActorType is not null)
        {
            administrators.Setup(x => x.GetByNormalizedEmailAsync(Email, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PlatformAdministrator
                {
                    Email = Email, NormalizedEmail = Email, ActorType = Enum.Parse<ActorType>(recordActorType),
                    AllowedTenantIds = [Guid.NewGuid()], Status = AdministratorStatus.Active,
                    InvitationStatus = AdministratorInvitationStatus.Accepted, LastLoginAtUtc = DateTimeOffset.UtcNow
                });
        }

        var root = new ServiceCollection()
            .AddPlatformActorTenantScope()
            .AddSingleton(administrators.Object)
            .BuildServiceProvider();
        using var request = root.CreateScope();
        var http = new DefaultHttpContext
        {
            RequestServices = request.ServiceProvider,
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("actor_type", tokenActorType), new Claim(JwtRegisteredClaimNames.Email, Email)], "Bearer"))
        };
        request.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = http;
        var descriptor = new ActionDescriptor { EndpointMetadata = admitsPartners ? [new PartnerAdminScopeAttribute("id")] : [] };
        var filter = new AuthorizationFilterContext(new ActionContext(http, new RouteData(), descriptor), new List<IFilterMetadata>());

        await new HasPermissionAttribute("platform.administrators.read").OnAuthorizationAsync(filter);

        Assert.Equal(expected, filter.Result is null);
        if (!expected)
        {
            Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(filter.Result).StatusCode);
        }
    }
}
