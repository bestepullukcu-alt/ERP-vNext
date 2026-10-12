using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text;
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
/// <para>
/// WP-0174-P4: a view that asks the Gateway (the typed-lot status of the Stock Ledger form, CT K4-1) gets a NETWORK-FREE
/// <see cref="HttpClient"/>: <c>/api/trace/migration/status</c> answers <paramref name="typingClosed"/> when given, every
/// other call 404 — a test never reaches a real Gateway (the owner's 5000 included).
/// </para>
/// </summary>
internal static class TraceViewRenderer
{
    public static string WebRoot { get; } = FindWebRoot();

    public static Task<string> RenderAsync(WebApplicationFactory<Program> factory, string viewPath, object? model, params string[] permissions)
        => RenderAsync(factory, viewPath, model, typingClosed: null, permissions);

    public static async Task<string> RenderAsync(WebApplicationFactory<Program> factory, string viewPath, object? model, bool? typingClosed, params string[] permissions)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");

        using var scope = factory.Services.CreateScope();
        var services = new NoNetworkProvider(scope.ServiceProvider, new HttpClient(new StatusGateway(typingClosed)));
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

    /// <summary>Every service of the scope, except <see cref="HttpClient"/>: the network-free stub.</summary>
    private sealed class NoNetworkProvider(IServiceProvider inner, HttpClient http) : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(HttpClient) ? http : inner.GetService(serviceType);
    }

    public sealed class StatusGateway(bool? closed) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            lock (Paths) Paths.Add(path);
            if (closed is { } c && path == "/api/trace/migration/status")
            {
                var body = c ? "{\"manualLotEntryClosed\":true,\"completedAt\":\"2026-10-06T10:00:00Z\",\"contractVersion\":\"v1\"}"
                             : "{\"manualLotEntryClosed\":false,\"contractVersion\":\"v1\"}";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    public static string RepoRoot() => Path.GetFullPath(Path.Combine(WebRoot, "..", ".."));

    private static string FindWebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return dir is null ? throw new InvalidOperationException("repo root not found") : Path.Combine(dir.FullName, "frontend", "Diten.Web");
    }
}
