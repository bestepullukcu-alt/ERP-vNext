using System.Reflection;
using System.Security.Claims;
using Diten.Web;
using Diten.Web.Controllers.CRM;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-CL-FE-3 — the claim Create / Edit pages are JS shells over the v2 proxy: a manager gets the page (kind / id handed
/// to the script), anyone else a plain 403 without a skeleton (UAS-001), and the former MVC form post is gone.
/// </summary>
public sealed class ClaimsFormPageTests
{
    private static readonly Guid TenantId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    [Theory]
    [InlineData("core", "core")]
    [InlineData("local", "local")]
    [InlineData("LOCAL", "local")]
    [InlineData(null, "core")]
    [InlineData("anything", "core")]
    public void Create_hands_the_kind_to_the_page(string? kind, string expected)
    {
        var controller = ControllerWith("crm.claim.read", "crm.claim.manage");

        var view = Assert.IsType<ViewResult>(controller.Create(kind));

        Assert.Equal("~/Views/CRM/Claims/Create.cshtml", view.ViewName);
        Assert.Equal(expected, view.ViewData["ClaimKind"]);
        Assert.Equal(string.Empty, view.ViewData["ClaimId"]);
    }

    [Fact]
    public void Edit_hands_the_id_to_the_page()
    {
        var id = Guid.NewGuid();
        var view = Assert.IsType<ViewResult>(ControllerWith("crm.claim.manage").Edit(id));

        Assert.Equal("~/Views/CRM/Claims/Edit.cshtml", view.ViewName);
        Assert.Equal(id.ToString(), view.ViewData["ClaimId"]);
    }

    [Fact]
    public void Without_manage_the_pages_are_a_plain_403()
    {
        var reader = ControllerWith("crm.claim.read");

        Assert.Equal(403, Assert.IsType<StatusCodeResult>(reader.Create("core")).StatusCode);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(reader.Edit(Guid.NewGuid())).StatusCode);
    }

    [Fact]
    public void The_mvc_form_post_is_gone()
    {
        var posts = typeof(ClaimsController).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .SelectMany(m => m.GetCustomAttributes<HttpPostAttribute>())
            .Select(a => a.Template ?? string.Empty)
            .ToList();

        Assert.DoesNotContain("Create", posts);
        Assert.DoesNotContain("Edit/{id:guid}", posts);
        Assert.Null(typeof(ClaimsController).Assembly.GetType("Diten.Web.Models.CRM.ClaimEditViewModel"));
    }

    private static ClaimsController ControllerWith(params string[] permissions)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = "http://gateway.test" }).Build();
        var controller = new ClaimsController(new HttpClient(), configuration, new KeyLocalizer(),
            NullLogger<ClaimsController>.Instance);
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.ViewData = new ViewDataDictionary(new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(),
            new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary());
        return controller;
    }

    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
