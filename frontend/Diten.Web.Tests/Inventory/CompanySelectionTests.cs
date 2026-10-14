using System.Security.Claims;
using Diten.Web.Models.Inventory;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Diten.Web.Tests.Inventory;

/// <summary>
/// WP-0173-LE1 — the company selector's server half (<see cref="CompanySelection"/>, W-0173-02): the token's company
/// wins, the cookie is per user and holds a UUID only, and only ACTIVE (referenceable) companies of the MDM lookup are
/// offered.
/// </summary>
public sealed class CompanySelectionTests
{
    private static ClaimsPrincipal User(string sub, Guid? legalEntity = null)
    {
        var claims = new List<Claim> { new("sub", sub), new("tenantId", Guid.NewGuid().ToString()) };
        if (legalEntity is not null) claims.Add(new Claim("legal_entity_id", legalEntity.Value.ToString()));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    [Fact]
    public void The_token_company_is_read_from_the_claim_and_only_when_it_is_one_UUID()
    {
        var company = Guid.NewGuid();
        Assert.Equal(company, CompanySelection.TokenLegalEntity(User("u1", company)));
        Assert.Null(CompanySelection.TokenLegalEntity(User("u1")));
        var malformed = new ClaimsPrincipal(new ClaimsIdentity([new Claim("legal_entity_id", "le-tr")], "test"));
        Assert.Null(CompanySelection.TokenLegalEntity(malformed));
    }

    [Fact]
    public void The_cookie_is_named_per_user_so_a_second_user_does_not_inherit_the_choice()
    {
        var a = CompanySelection.CookieName(User("user-a"));
        var b = CompanySelection.CookieName(User("user-b"));

        Assert.StartsWith("diten-inv-company-", a);
        Assert.NotEqual(a, b);
        Assert.Equal(a, CompanySelection.CookieName(User("user-a")));
        Assert.DoesNotContain("user-a", a);
    }

    [Theory]
    [InlineData("3fa85f64-5717-4562-b3fc-2c963f66afa6", true)]
    [InlineData("not-a-uuid", false)]
    [InlineData("00000000-0000-0000-0000-000000000000", false)]
    public void The_selected_company_is_a_UUID_from_this_users_cookie_or_nothing(string value, bool accepted)
    {
        var user = User("user-a");
        var http = new DefaultHttpContext { User = user };
        http.Request.Headers.Cookie = $"{CompanySelection.CookieName(user)}={value}";

        Assert.Equal(accepted, CompanySelection.SelectedCompany(http.Request, user) is not null);

        // the same cookie means nothing to another user
        var other = User("user-b");
        Assert.Null(CompanySelection.SelectedCompany(http.Request, other));
    }

    [Fact]
    public void Only_referenceable_companies_of_the_lookup_are_offered_with_code_and_name()
    {
        var active = Guid.NewGuid();
        var suspended = Guid.NewGuid();
        var body = $$"""
            {"data":[
              {"legalEntityId":"{{active}}","code":"ACME-TR","legalName":"Acme Ticaret A.Ş.","displayName":"Acme Türkiye","lifecycleState":"ACTIVE","referenceable":true},
              {"legalEntityId":"{{suspended}}","code":"ACME-OLD","legalName":"Old","lifecycleState":"SUSPENDED","referenceable":false},
              {"legalEntityId":"not-a-uuid","code":"X","legalName":"X","referenceable":true}
            ],"isSuccessful":true}
            """;

        var options = CompanySelection.ParseCompanies(body);

        var option = Assert.Single(options!);
        Assert.Equal(active.ToString("D"), option.Id);
        Assert.Equal("ACME-TR · Acme Türkiye", option.Name);
        Assert.Null(CompanySelection.ParseCompanies("not json"));
        Assert.Null(CompanySelection.ParseCompanies("""{"data":{"items":[]}}"""));
    }
}
