using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// EK item 10 (FIX5): renders a page the way a request does — the view AND its layout (_LayoutTenantShell), so what
/// the layout draws from the page's sections (@section Scripts: the l10n block, the page script) is measured too. The
/// page-only rendering never wrote a section, so "no script for an unauthorized reader" was never measured.
/// </summary>
internal static class LayoutRenderer
{
    public static async Task<string> RenderAsync<TProgram>(WebApplicationFactory<TProgram> factory, string viewPath,
        string controller, string permission, IReadOnlyDictionary<string, object?>? viewDataValues = null,
        params string[] extraPermissions) where TProgram : class
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services,
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("tenant_id", Guid.NewGuid().ToString("D")),
                new Claim("actor_type", "tenant_user"),
                new Claim("sub", Guid.NewGuid().ToString("D")),
                new Claim("permission", permission),
                .. extraPermissions.Select(extra => new Claim("permission", extra))
            ], "fixture"))
        };
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("tenant.test");
        httpContext.Request.Path = "/MasterDataManagement/" + controller;
        services.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;

        var engine = services.GetRequiredService<IRazorViewEngine>();
        var found = engine.GetView(executingFilePath: null, viewPath: viewPath, isMainPage: true);
        var view = found.View ?? throw new InvalidOperationException($"Could not load {viewPath}.");
        var actionContext = new ActionContext(httpContext,
            new RouteData { Values = { ["controller"] = controller, ["action"] = "Index" } },
            new ActionDescriptor(), new ModelStateDictionary());
        var viewData = new ViewDataDictionary(services.GetRequiredService<IModelMetadataProvider>(), actionContext.ModelState);
        // FG FIX1 item 5: what the controller puts in ViewData before the view (e.g. the create permission and its token).
        foreach (var (key, value) in viewDataValues ?? new Dictionary<string, object?>())
            viewData[key] = value;
        var tempData = new TempDataDictionary(httpContext, services.GetRequiredService<ITempDataProvider>());
        await using var writer = new StringWriter();
        var viewContext = new ViewContext(actionContext, view, viewData, tempData, writer, new HtmlHelperOptions());
        await view.RenderAsync(viewContext);
        return writer.ToString();
    }
}
