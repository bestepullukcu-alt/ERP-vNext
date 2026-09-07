using System.Net;
using System.Security.Claims;
using Diten.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Navigation;

public sealed class ProductAbbreviationNavigationAuthorizationTests
{
    [Fact]
    public void Index_denies_an_authenticated_subject_without_read_permission()
    {
        var controller = CreateController([]);

        Assert.IsType<ForbidResult>(controller.Index());
    }

    [Fact]
    public void Index_allows_an_authenticated_subject_with_exact_read_permission()
    {
        var controller = CreateController(["mdm.product-abbreviations.read"]);

        var result = Assert.IsType<ViewResult>(controller.Index());
        Assert.Equal("~/Views/MDM/ProductAbbreviationRegister/Index.cshtml", result.ViewName);
    }

    private static ProductAbbreviationRegisterController CreateController(IEnumerable<string> permissions)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GatewayUrl"] = "http://localhost:5000"
            })
            .Build();
        var controller = new ProductAbbreviationRegisterController(
            new HttpClient(new NoCallHandler()),
            configuration,
            new EchoLocalizer(),
            NullLogger<ProductAbbreviationRegisterController>.Instance);
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "navigation-test-user") };
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "navigation-test"))
            }
        };
        return controller;
    }

    private sealed class NoCallHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
    }

    private sealed class EchoLocalizer : IStringLocalizer<Diten.Web.SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);

        public LocalizedString this[string name, params object[] arguments] =>
            new(name, string.Format(name, arguments), resourceNotFound: false);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
