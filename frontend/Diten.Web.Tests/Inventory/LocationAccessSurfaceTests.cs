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
using Xunit;

namespace Diten.Web.Tests.Inventory;

/// <summary>
/// UAS-001 on the two FU02 location pages, rendered by the REAL Razor views (body only — the shell is not the subject):
/// <list type="bullet">
/// <item>without <c>inventory.locations.read</c> the page draws <c>_AccessDenied</c> and NOTHING else;</item>
/// <item>with read only, the page is drawn but no write control exists — no New / Add first site button, no form, no
/// bulk dialog, no anti-forgery token (absent, not disabled — UAS-001 §6);</item>
/// <item>with manage, the one form (L-3) is on both pages and the bulk dialog (L-4) on L-1 only.</item>
/// </list>
/// The checks are display decisions — InventoryService's [HasPermission] stays the authority (UAS-001 §4).
/// </summary>
public sealed class LocationAccessSurfaceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Tree = "/Views/Inventory/LocationTree/Index.cshtml";
    private const string List = "/Views/Inventory/Locations/Index.cshtml";
    private const string Read = "inventory.locations.read";
    private const string Manage = "inventory.locations.manage";

    // What a page must not draw for a user who may not read it (heading, tabs, tree, table, filter, skeleton, buttons).
    private static readonly string[] Skeleton =
        ["locationTree", "dt-locations", "inlineFilterHost", "wc-tab-compact", "skeleton-loader", "locationTreeSkeleton",
         "data-dt-data-mode", "<h5 class=\"mb-0\">", "offcanvasLocationForm", "data-location-action"];

    // Write controls a read-only user must not get. (The shell's own sign-out form carries an anti-forgery token on
    // every page, so the page's token is asserted through the page's own form, formLocation.)
    private static readonly string[] WriteControls =
        ["offcanvasLocationForm", "modalBulkBins", "data-location-action=\"newLocation\"", "id=\"formLocation\"", "btnSaveLocation"];

    private readonly WebApplicationFactory<Program> _factory;

    public LocationAccessSurfaceTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Theory]
    [InlineData(Tree)]
    [InlineData(List)]
    public async Task Without_read_only_the_access_explanation_is_drawn(string view)
    {
        var html = await RenderAsync(view, "inventory.stock.read");

        Assert.Contains("diten-access-denied", html, StringComparison.Ordinal);
        Assert.All(Skeleton, s => Assert.DoesNotContain(s, html, StringComparison.Ordinal));
        Assert.DoesNotContain("Permission denied", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Forbidden", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_tree_page_draws_a_tree_not_a_table()
    {
        var html = await RenderAsync(Tree, Read);

        Assert.DoesNotContain("diten-access-denied", html, StringComparison.Ordinal);
        Assert.Contains("role=\"tree\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"locationDetail\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-dt-standard", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_list_page_is_a_server_mode_list_without_selection()
    {
        var html = await RenderAsync(List, Read);

        Assert.DoesNotContain("diten-access-denied", html, StringComparison.Ordinal);
        Assert.Contains("id=\"dt-locations\" data-dt-standard=\"v2\" data-dt-data-mode=\"server\"", html, StringComparison.Ordinal);
        Assert.Contains("inlineFilterHost", html, StringComparison.Ordinal);
        Assert.DoesNotContain("dt-checkboxes-select-all", html, StringComparison.Ordinal);
        Assert.DoesNotContain("bulkActionBar", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Tree)]
    [InlineData(List)]
    public async Task A_read_only_user_gets_no_write_control_at_all(string view)
    {
        var html = await RenderAsync(view, Read, "inventory.stock.read");

        Assert.DoesNotContain("diten-access-denied", html, StringComparison.Ordinal);
        foreach (var c in WriteControls) Assert.False(html.Contains(c, StringComparison.Ordinal), $"read-only page draws {c}: …{(html.IndexOf(c, StringComparison.Ordinal) is var i && i >= 0 ? html.Substring(Math.Max(0, i - 200), 260) : "")}…");
    }

    [Fact]
    public async Task A_manager_gets_the_one_form_on_both_pages_and_bulk_bins_on_the_tree()
    {
        var tree = await RenderAsync(Tree, Read, Manage);
        var list = await RenderAsync(List, Read, Manage);

        Assert.Contains("offcanvasLocationForm", tree, StringComparison.Ordinal);
        Assert.Contains("offcanvasLocationForm", list, StringComparison.Ordinal);
        // The writes (form, status change, bulk) are signed with the token inside the page's own form.
        Assert.Contains("__RequestVerificationToken", FormLocation(tree), StringComparison.Ordinal);
        Assert.Contains("__RequestVerificationToken", FormLocation(list), StringComparison.Ordinal);
        Assert.Contains("data-location-action=\"newLocation\"", tree, StringComparison.Ordinal);
        Assert.Contains("modalBulkBins", tree, StringComparison.Ordinal);
        Assert.DoesNotContain("modalBulkBins", list, StringComparison.Ordinal);
        // K-03 / K-14: the company is never a form field.
        Assert.DoesNotContain("name=\"legalEntityId\"", tree, StringComparison.OrdinalIgnoreCase);
    }

    private static string FormLocation(string html)
    {
        var start = html.IndexOf("id=\"formLocation\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "formLocation is not on the page");
        var end = html.IndexOf("</form>", start, StringComparison.Ordinal);
        return html[start..end];
    }

    private async Task<string> RenderAsync(string viewPath, params string[] permissions)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");

        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        // The token names its company, so the page needs no selector and makes no MDM call while rendering (W-0173-02).
        var claims = new List<Claim> { new("tenantId", Guid.NewGuid().ToString()), new("actor_type", "tenant_user"), new("legal_entity_id", Guid.NewGuid().ToString()) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services,
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
        };
        services.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;

        var engine = services.GetRequiredService<IRazorViewEngine>();
        var found = engine.GetView(executingFilePath: null, viewPath: viewPath, isMainPage: false);
        if (!found.Success)
            throw new InvalidOperationException($"Could not load {viewPath}. Searched: {string.Join(", ", found.SearchedLocations ?? [])}");

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
        var metadata = services.GetRequiredService<IModelMetadataProvider>();
        var viewData = new ViewDataDictionary(metadata, actionContext.ModelState);
        var tempData = new TempDataDictionary(httpContext, services.GetRequiredService<ITempDataProvider>());

        await using var writer = new StringWriter();
        var viewContext = new ViewContext(actionContext, found.View, viewData, tempData, writer, new HtmlHelperOptions());
        await found.View.RenderAsync(viewContext);
        return writer.ToString();
    }
}
