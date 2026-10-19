using System.Security.Claims;
using Diten.ManufacturingService.Infrastructure.UserContext;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Diten.ManufacturingService.Tests.Tenancy;

/// <summary>
/// The BOM history's "by" column. Measured in the browser (M-5): a real AuthService token has no <c>name</c> claim, so
/// every entry read "unknown". The claim shapes below are the ones AuthService issues.
/// </summary>
public sealed class CurrentUserContextTests
{
    private static string NameOf(params (string Type, string Value)[] claims)
    {
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "Bearer")) };
        return new CurrentUserContext(new HttpContextAccessor { HttpContext = http }).UserName;
    }

    [Fact]
    public void An_auth_service_token_is_named_by_given_and_family_name() =>
        Assert.Equal("BOM Admin", NameOf(("sub", Guid.NewGuid().ToString()), ("given_name", "BOM"), ("family_name", "Admin"), ("email", "bom.admin@example.test")));

    [Fact]
    public void Email_names_the_actor_when_the_token_has_no_person_name() =>
        Assert.Equal("bom.admin@example.test", NameOf(("sub", Guid.NewGuid().ToString()), ("email", "bom.admin@example.test")));

    [Fact]
    public void An_explicit_name_claim_still_wins() =>
        Assert.Equal("BOM Tester", NameOf(("name", "BOM Tester"), ("given_name", "BOM"), ("family_name", "Admin")));

    [Fact]
    public void Unknown_only_when_the_token_names_nobody() =>
        Assert.Equal("unknown", NameOf(("sub", Guid.NewGuid().ToString())));
}
