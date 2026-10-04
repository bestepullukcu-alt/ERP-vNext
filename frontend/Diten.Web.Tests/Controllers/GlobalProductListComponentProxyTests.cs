using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Web.Controllers;
using Diten.Web.Views.MasterDataManagement.GlobalProducts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-MDM-GP-LIST-01 (BL-440): the Global Product register on the list component. The real controller actions are
/// called with MDM as a recording handler — the list component's request becomes MDM's page query and MDM's page becomes
/// the component's envelope. The unauthorized face is the real Index.cshtml, executed by the application's own Razor
/// engine: a 403 with the access-denied surface and nothing of the list (UAS-001).
/// </summary>
public sealed class GlobalProductListComponentProxyTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Gateway = "https://gateway-fixture.invalid";
    private readonly WebApplicationFactory<Program> _factory;

    public GlobalProductListComponentProxyTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task The_list_request_becomes_MDMs_page_query_and_the_page_becomes_the_component_envelope()
    {
        var wire = new RecordingHandler(HttpStatusCode.OK,
            "{\"isSuccessful\":true,\"statusCode\":200,\"data\":{\"items\":[{\"id\":\"11111111-1111-1111-1111-111111111111\",\"canonicalCode\":\"GP-1\"}],\"pageNumber\":3,\"pageSize\":10,\"totalCount\":27}}");
        var controller = Controller(wire, "mdm.global-products.read",
            "?start=20&length=10&search=asp%C4%B1rin&orderBy=canonicalCode&orderDir=desc&draw=4&lifecycleStatus=Draft&columns%5B0%5D%5Bdata%5D=id&tenantId=evil");

        var result = await controller.List(CancellationToken.None);

        Assert.Equal(
            $"{Gateway}/api/global-products?pageNumber=3&pageSize=10&search=asp%C4%B1rin&orderBy=canonicalCode&orderDir=desc&lifecycleStatus=Draft",
            Assert.Single(wire.Urls));
        using var body = JsonDocument.Parse(Assert.IsType<ContentResult>(result).Content!);
        var data = body.RootElement.GetProperty("data");
        Assert.Equal("GP-1", Assert.Single(data.GetProperty("items").EnumerateArray()).GetProperty("canonicalCode").GetString());
        Assert.Equal(27, data.GetProperty("total").GetInt64());
        Assert.Equal(27, data.GetProperty("filteredTotal").GetInt64());
    }

    [Theory]
    [InlineData("?start=0&length=500", "pageNumber=1&pageSize=100")]
    [InlineData("?start=-5&length=0", "pageNumber=1&pageSize=20")]
    [InlineData("", "pageNumber=1&pageSize=20")]
    [InlineData("?start=250&length=100", "pageNumber=3&pageSize=100")]
    [InlineData("?start=9223372036854775807&length=1", "pageNumber=1000001&pageSize=1")]
    public async Task The_page_size_is_capped_and_a_bad_or_huge_start_never_overflows(string query, string expected)
    {
        var wire = new RecordingHandler(HttpStatusCode.OK, "{\"data\":{\"items\":[],\"totalCount\":0}}");

        await Controller(wire, "mdm.global-products.read", query).List(CancellationToken.None);

        Assert.Equal($"{Gateway}/api/global-products?{expected}", Assert.Single(wire.Urls));
    }

    [Fact]
    public async Task The_list_without_the_read_permission_is_a_JSON_403_and_MDM_is_never_asked()
    {
        var wire = new RecordingHandler(HttpStatusCode.OK, "{}");

        var result = await Controller(wire, "mdm.global-products.create", "?start=0&length=10").List(CancellationToken.None);

        var refusal = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, refusal.StatusCode);
        Assert.Contains("\"success\":false", JsonSerializer.Serialize(refusal.Value));
        Assert.Empty(wire.Urls);
    }

    [Fact]
    public async Task A_page_without_a_count_is_a_gateway_failure_not_an_empty_list()
    {
        var wire = new RecordingHandler(HttpStatusCode.OK, "{\"data\":{\"items\":[]}}");

        var result = await Controller(wire, "mdm.global-products.read", "?start=0&length=10").List(CancellationToken.None);

        Assert.Equal(StatusCodes.Status502BadGateway, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    // MDM refuses an unknown sort key (400): the reader gets a sentence, never MDM's code or an empty "success".
    [Fact]
    public async Task An_unknown_sort_key_refused_by_MDM_is_a_400_with_a_sentence()
    {
        var wire = new RecordingHandler(HttpStatusCode.BadRequest,
            "{\"isSuccessful\":false,\"statusCode\":400,\"errors\":[\"GLOBAL_PRODUCT_LIST_ORDER_BY_INVALID\"]}");

        var result = await Controller(wire, "mdm.global-products.read", "?start=0&length=10&orderBy=createdBy").List(CancellationToken.None);

        var refusal = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, refusal.StatusCode);
        Assert.DoesNotContain("GLOBAL_PRODUCT_LIST_ORDER_BY_INVALID", JsonSerializer.Serialize(refusal.Value));
        Assert.EndsWith("orderBy=createdBy", Assert.Single(wire.Urls));
    }

    [Fact]
    public void The_index_without_the_read_permission_answers_403_with_the_register_view_not_a_redirect()
    {
        var controller = Controller(new RecordingHandler(HttpStatusCode.OK, "{}"), "mdm.global-products.create", "");

        var result = controller.Index();

        Assert.Equal(StatusCodes.Status403Forbidden, controller.Response.StatusCode);
        Assert.Equal("~/Views/MasterDataManagement/GlobalProducts/Index.cshtml", Assert.IsType<ViewResult>(result).ViewName);
    }

    [Fact]
    public async Task The_unauthorized_face_is_the_access_denied_surface_with_no_list_skeleton()
    {
        var html = await RenderIndexAsync("mdm.global-products.create");

        Assert.Contains("diten-access-denied", html);
        Assert.DoesNotContain("dt-globalproducts", html);
        Assert.DoesNotContain("skeleton-loader", html);
        Assert.DoesNotContain("filterLifecycleStatus", html);
        Assert.DoesNotContain("globalproducts-l10n", html);
    }

    // The probe above is only meaningful if the same rendering draws the list for a reader who may read it.
    [Fact]
    public async Task A_reader_with_the_read_permission_gets_the_list_shell_and_no_access_denied_surface()
    {
        var html = await RenderIndexAsync("mdm.global-products.read");

        Assert.Contains("id=\"dt-globalproducts\"", html);
        Assert.Contains("data-dt-data-mode=\"server\"", html);
        Assert.Contains("datatables-globalproducts", html);
        Assert.Contains("skeleton-loader", html);
        Assert.DoesNotContain("dt-checkboxes-select-all", html);
        Assert.DoesNotContain("diten-access-denied", html);
    }

    private async Task<string> RenderIndexAsync(string permission)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var httpContext = new DefaultHttpContext { RequestServices = services, User = Principal(permission) };
        services.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;

        // The page is executed as RazorView executes it (activate, execute) but without the shell's layout, which needs
        // the whole signed-in navigation stack; what the PAGE draws is exactly what is measured.
        var engine = services.GetRequiredService<IRazorViewEngine>();
        const string path = "/Views/MasterDataManagement/GlobalProducts/Index.cshtml";
        var view = engine.GetView(executingFilePath: null, viewPath: path, isMainPage: true);
        var page = engine.GetPage(executingFilePath: null, pagePath: path).Page
            ?? throw new InvalidOperationException($"Could not load {path}.");
        var actionContext = new ActionContext(httpContext,
            new RouteData { Values = { ["controller"] = "GlobalProducts", ["action"] = "Index" } },
            new ActionDescriptor(), new ModelStateDictionary());
        var viewData = new ViewDataDictionary(services.GetRequiredService<IModelMetadataProvider>(), actionContext.ModelState);
        var tempData = new TempDataDictionary(httpContext, services.GetRequiredService<ITempDataProvider>());
        await using var writer = new StringWriter();
        var viewContext = new ViewContext(actionContext, view.View!, viewData, tempData, writer, new HtmlHelperOptions())
        {
            ExecutingFilePath = path
        };
        page.ViewContext = viewContext;
        services.GetRequiredService<IRazorPageActivator>().Activate(page, viewContext);
        await page.ExecuteAsync();
        return writer.ToString();
    }

    private static GlobalProductsController Controller(RecordingHandler wire, string permission, string query)
    {
        var gateway = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["GatewayUrl"] = Gateway }).Build();
        var context = new DefaultHttpContext { User = Principal(permission) };
        // Synthetic fixture credential: only seen by the recording handler, never sent to a network.
        context.Request.Headers.Cookie = "access_token=synthetic-gp-list";
        context.Request.QueryString = new QueryString(query);
        return new GlobalProductsController(new HttpClient(wire), gateway,
            new EchoLocalizer<Diten.Web.SharedResource>(), new EchoLocalizer<GlobalProductsIndex>(),
            NullLogger<GlobalProductsController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = context } };
    }

    private static ClaimsPrincipal Principal(string permission) => new(new ClaimsIdentity(
    [
        new Claim("tenant_id", Guid.NewGuid().ToString("D")),
        new Claim("actor_type", "tenant_user"),
        new Claim("sub", Guid.NewGuid().ToString("D")),
        new Claim("permission", permission)
    ], "fixture"));

    private sealed class RecordingHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<string> Urls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Urls.Add(request.RequestUri!.OriginalString);
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class EchoLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
