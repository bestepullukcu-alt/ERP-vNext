using System.Net;
using System.Text;
using Diten.Web.Services.Navigation;
using Diten.Web.ViewComponents;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Navigation;

/// <summary>
/// UX-COMMON-FIX1 — the tenant menu component records the page's stable code for the help panel (U1), and a failure
/// in that recording can never take the sidebar down with it (it used to run inside the menu's own try, where any
/// exception turned the whole sidebar into "failed to load"). Measured on the real component with a stubbed gateway.
/// </summary>
public sealed class DynamicModuleMenuPageCodeTests
{
    private const string Menu = """
        {"data":[{"moduleCode":"ACCESS-GOVERNANCE","moduleDisplayName":"Access","domain":"PLATFORM","domainDisplayName":"Platform",
          "items":[{"pageCode":"USERS","displayName":"Users","routePath":"/Users","requiredPermission":null,"parentPageCode":null,"iconHint":null,"sortOrder":1},
                   {"pageCode":"ROLES","displayName":"Roles","routePath":"/Roles","requiredPermission":null,"parentPageCode":null,"iconHint":null,"sortOrder":2}]}]}
        """;

    [Fact]
    public async Task The_page_code_of_the_drawn_page_is_recorded_for_the_help_panel()
    {
        var (model, context) = await InvokeAsync("/Users", matcher: null);

        Assert.True(model.HasItems);
        Assert.Equal("USERS", context.Items[CurrentPage.PageCodeItem]);
    }

    [Fact]
    public async Task The_menu_pages_are_recorded_for_the_help_panel_related_links()
    {
        // WP-UX-HELP-PANEL-02 — "Related pages" link where the menu links (code → address, permission).
        var (_, context) = await InvokeAsync("/Users", matcher: null);

        var pages = CurrentPage.MenuPages(context);
        Assert.Equal("/Users", pages["USERS"].RoutePath);
        Assert.Equal("/Roles", pages["ROLES"].RoutePath);
        Assert.Null(pages["ROLES"].RequiredPermission);
    }

    [Fact]
    public async Task A_failing_page_code_never_takes_the_sidebar_down()
    {
        var (model, context) = await InvokeAsync("/Users", matcher: (_, _) => throw new InvalidOperationException("boom"));

        Assert.False(model.LoadFailed, "the help code's failure turned the sidebar into 'failed to load'");
        Assert.True(model.HasItems);
        Assert.Null(context.Items[CurrentPage.PageCodeItem]);
    }

    private static async Task<(DynamicModuleMenuViewModel Model, HttpContext Context)> InvokeAsync(
        string path, Func<string?, IEnumerable<(string? PageCode, string? RoutePath)>, string?>? matcher)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = "http://gateway.test" }).Build();
        var component = new DynamicModuleMenuViewComponent(new HttpClient(new MenuGateway()), configuration,
            NullLogger<DynamicModuleMenuViewComponent>.Instance, new PassThroughNames(), matcher);

        var http = new DefaultHttpContext();
        http.Request.Path = path;
        http.Request.Headers.Cookie = "access_token=token";
        var viewContext = new ViewContext(
            new ActionContext(http, new RouteData(), new ActionDescriptor()),
            new FakeView(), new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()),
            new TempDataDictionary(http, new NullTempDataProvider()), TextWriter.Null, new HtmlHelperOptions());
        component.ViewComponentContext = new ViewComponentContext(new ViewComponentDescriptor(),
            new Dictionary<string, object?>(), System.Text.Encodings.Web.HtmlEncoder.Default, viewContext, TextWriter.Null);

        var result = Assert.IsType<ViewViewComponentResult>(await component.InvokeAsync());
        return (Assert.IsType<DynamicModuleMenuViewModel>(result.ViewData!.Model), http);
    }

    private sealed class MenuGateway : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Menu, Encoding.UTF8, "application/json") });
    }

    private sealed class PassThroughNames : INavNameLocalizer
    {
        public string Domain(string? domainCode, string serverName, bool isOverride) => serverName;
        public string Module(string? moduleCode, string serverName, bool isOverride) => serverName;
        public string Page(string? pageCode, string serverName) => serverName;
    }

    private sealed class FakeView : IView
    {
        public string Path => "/Views/Fake.cshtml";
        public Task RenderAsync(ViewContext context) => Task.CompletedTask;
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
