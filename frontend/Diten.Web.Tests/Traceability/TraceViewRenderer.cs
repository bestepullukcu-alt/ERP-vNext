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

namespace Diten.Web.Tests.Traceability;

/// <summary>
/// Renders a REAL Razor view of the Web app (body only, the shell is not the subject) for a user holding exactly the given
/// permission keys — the InventoryAccessSurfaceTests renderer, with a typed model. The token names its company, so the
/// pages need no selector and make no MDM call while rendering (W-0173-02).
/// </summary>
internal static class TraceViewRenderer
{
    public static string WebRoot { get; } = FindWebRoot();

    public static async Task<string> RenderAsync(WebApplicationFactory<Program> factory, string viewPath, object? model, params string[] permissions)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");

        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var claims = new List<Claim> { new("tenantId", Guid.NewGuid().ToString()), new("actor_type", "tenant_user"), new("legal_entity_id", Guid.NewGuid().ToString()) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var httpContext = new DefaultHttpContext { RequestServices = services, User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        services.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;

        var engine = services.GetRequiredService<IRazorViewEngine>();
        var found = engine.GetView(executingFilePath: null, viewPath: viewPath, isMainPage: false);
        if (!found.Success)
            throw new InvalidOperationException($"Could not load {viewPath}. Searched: {string.Join(", ", found.SearchedLocations ?? [])}");

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
        var metadata = services.GetRequiredService<IModelMetadataProvider>();
        var viewData = new ViewDataDictionary(metadata, actionContext.ModelState) { Model = model };
        var tempData = new TempDataDictionary(httpContext, services.GetRequiredService<ITempDataProvider>());

        await using var writer = new StringWriter();
        var viewContext = new ViewContext(actionContext, found.View, viewData, tempData, writer, new HtmlHelperOptions());
        await found.View.RenderAsync(viewContext);
        return writer.ToString();
    }

    public static string RepoRoot() => Path.GetFullPath(Path.Combine(WebRoot, "..", ".."));

    private static string FindWebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return dir is null ? throw new InvalidOperationException("repo root not found") : Path.Combine(dir.FullName, "frontend", "Diten.Web");
    }
}
