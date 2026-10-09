using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Diten.Web.Controllers;
using Diten.Web.Models.ProductLegalEntityScopes;
using Diten.Web.Views.MasterDataManagement.ProductLegalEntityScopes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-MDM-SCOPE-ACCEPT-01 FIX1 — the scope page's MVC proxy: UAS-001 / B5 (a coded 403, never a redirect), the list in the
/// list component's server protocol (BL-440), and the form attempt token bound to the person who opened the form.
/// </summary>
public sealed class ProductLegalEntityScopesControllerTests
{
    private const string Read = "mdm.product-legal-entity-scopes.read";
    private const string Configure = "mdm.product-legal-entity-scopes.configure";
    private const string Replace = "mdm.product-legal-entity-scopes.replace";
    private const string End = "mdm.product-legal-entity-scopes.end";
    private static readonly EphemeralDataProtectionProvider Protection = new();

    [Fact]
    public void A_reader_without_the_read_permission_gets_403_inside_the_shell_and_a_reader_gets_the_page()
    {
        var denied = Controller(new Wire(), Subject(), "unrelated");
        var deniedView = Assert.IsType<ViewResult>(denied.Index());
        Assert.Equal(StatusCodes.Status403Forbidden, denied.Response.StatusCode);
        Assert.Equal(false, deniedView.ViewData["CanReadProductScope"]);

        var reader = Controller(new Wire(), Subject(), Read);
        Assert.IsType<ViewResult>(reader.Index());
        Assert.Equal(StatusCodes.Status200OK, reader.Response.StatusCode);
        Assert.Equal(true, reader.ViewData["CanReadProductScope"]);
    }

    [Fact]
    public async Task Every_call_without_its_permission_answers_a_coded_403_and_never_reaches_the_gateway()
    {
        using var wire = new Wire();
        var controller = Controller(wire, Subject(), "unrelated");
        var product = Guid.NewGuid();
        var write = new ProductLegalEntityScopeWriteViewModel { GlobalProductId = product, Mode = 1, ExpectedVersion = 0 };

        var results = new[]
        {
            await controller.Products(default),
            await controller.Policy(product, default),
            await controller.History(product, default),
            await controller.Effective(product, default),
            await controller.CreateOptions(product, default),
            await controller.Create(write, "token", default),
            await controller.Replace(write, "token", default),
            await controller.End(new ProductLegalEntityScopeEndViewModel { GlobalProductId = product, ExpectedVersion = 0 }, "token", default)
        };

        foreach (var result in results)
        {
            var refused = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status403Forbidden, refused.StatusCode);
            Assert.Contains("PRODUCT_SCOPE_PERMISSION_DENIED", JsonSerializer.Serialize(refused.Value));
        }
        Assert.Empty(wire.Paths);
    }

    [Fact]
    public async Task The_list_asks_MDM_for_the_page_the_component_wants_and_answers_in_its_envelope()
    {
        using var wire = new Wire { Body = "{\"isSuccessful\":true,\"data\":{\"items\":[{\"id\":\"a\"},{\"id\":\"b\"}],\"totalCount\":42}}" };
        var controller = Controller(wire, Subject(), Read);
        controller.ControllerContext.HttpContext.Request.QueryString =
            new QueryString("?start=20&length=10&search=GP&orderBy=canonicalCode&orderDir=desc&lifecycleStatus=Draft&draw=3");

        var result = Assert.IsType<ContentResult>(await controller.Products(default));

        Assert.Equal("/api/global-products", Assert.Single(wire.Paths));
        Assert.Equal("?pageNumber=3&pageSize=10&search=GP&orderBy=canonicalCode&orderDir=desc&lifecycleStatus=Draft", wire.Queries.Single());
        using var page = JsonDocument.Parse(result.Content!);
        var data = page.RootElement.GetProperty("data");
        Assert.Equal(2, data.GetProperty("items").GetArrayLength());
        Assert.Equal(42, data.GetProperty("total").GetInt64());
        Assert.Equal(42, data.GetProperty("filteredTotal").GetInt64());
    }

    [Theory]
    [InlineData("<html>not json</html>")]
    [InlineData("{\"isSuccessful\":true,\"data\":{\"items\":[]}}")]
    public async Task A_page_MDM_answered_without_items_and_a_count_is_502(string body)
    {
        using var wire = new Wire { Body = body };

        var refused = Assert.IsType<ObjectResult>(await Controller(wire, Subject(), Read).Products(default));

        Assert.Equal(StatusCodes.Status502BadGateway, refused.StatusCode);
    }

    [Fact]
    public async Task A_form_attempt_opened_by_one_person_is_refused_for_another_and_never_reaches_the_gateway()
    {
        var opener = Subject();
        var token = Assert.IsType<string>(((ViewResult)Controller(new Wire(), opener, Read, Configure).Index())
            .ViewData["ProductScopeAttemptToken"]);
        var write = new ProductLegalEntityScopeWriteViewModel { GlobalProductId = Guid.NewGuid(), Mode = 1 };

        using var otherWire = new Wire();
        var other = await Controller(otherWire, Subject(), Read, Configure).Create(write, token, default);
        var refused = Assert.IsType<BadRequestObjectResult>(other);
        Assert.Contains("ErrorInvalidFormAttempt", JsonSerializer.Serialize(refused.Value));
        Assert.Empty(otherWire.Paths);

        using var ownWire = new Wire();
        await Controller(ownWire, opener, Read, Configure).Create(write, token, default);
        Assert.Single(ownWire.Paths);
    }

    private static string Subject() => Guid.NewGuid().ToString("D");

    private static ProductLegalEntityScopesController Controller(Wire wire, string subject, params string[] permissions)
    {
        var claims = new List<Claim> { new("sub", subject), new("tenant_id", Guid.NewGuid().ToString("D")) };
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "fixture")) };
        context.Request.Headers.Cookie = "access_token=synthetic-scope-fixture";
        var gateway = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["GatewayUrl"] = "https://gateway-fixture.invalid" }).Build();
        return new ProductLegalEntityScopesController(new HttpClient(wire), gateway, Protection, new Localizer())
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
    }

    private sealed class Wire : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        public List<string> Queries { get; } = [];
        public string Body { get; init; } = "{\"isSuccessful\":true,\"data\":{}}";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            Queries.Add(request.RequestUri.Query);
            Assert.Single(request.Headers.GetValues("X-Tenant-Id"));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Body) });
        }
    }

    private sealed class Localizer : IStringLocalizer<ProductLegalEntityScopesIndex>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
