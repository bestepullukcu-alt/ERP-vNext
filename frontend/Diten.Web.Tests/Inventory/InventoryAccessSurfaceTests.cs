using System.Globalization;
using System.Security.Claims;
using Diten.Web.Models.Inventory;
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
/// UAS-001 on the two FU01 pages, rendered by the REAL Razor views (body only — the shell is not the subject):
/// without <c>inventory.stock.read</c> the page draws <c>_AccessDenied</c> and NOTHING else (no heading, no tabs, no
/// KPI, no filter, no table); with read the list surface is drawn; the Reversal POST token is issued only to a user
/// who may reverse. The UI checks are display decisions — InventoryService's [HasPermission] stays the authority.
/// </summary>
public sealed class InventoryAccessSurfaceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Ledger = "/Views/Inventory/StockLedger/Index.cshtml";
    private const string Movements = "/Views/Inventory/StockMovements/Index.cshtml";

    private readonly WebApplicationFactory<Program> _factory;

    public InventoryAccessSurfaceTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Theory]
    [InlineData(Ledger, "dt-stockledger")]
    [InlineData(Movements, "dt-stockmovements")]
    public async Task Without_read_only_the_access_explanation_is_drawn(string view, string tableId)
    {
        var html = await RenderAsync(view);

        Assert.Contains("diten-access-denied", html, StringComparison.Ordinal);
        foreach (var skeleton in new[] { tableId, "inlineFilterHost", "wc-tab-compact", "stockledger-kpis", "skeleton-loader", "data-dt-data-mode", "<h5 class=\"mb-0\">" })
            Assert.DoesNotContain(skeleton, html, StringComparison.Ordinal);
        Assert.DoesNotContain("Permission denied", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Forbidden", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Ledger, "dt-stockledger")]
    [InlineData(Movements, "dt-stockmovements")]
    public async Task With_read_the_server_mode_list_is_drawn_without_selection(string view, string tableId)
    {
        var html = await RenderAsync(view, "inventory.stock.read");

        Assert.DoesNotContain("diten-access-denied", html, StringComparison.Ordinal);
        Assert.Contains($"id=\"{tableId}\" data-dt-standard=\"v2\" data-dt-data-mode=\"server\"", html, StringComparison.Ordinal);
        Assert.Contains("inlineFilterHost", html, StringComparison.Ordinal);
        Assert.DoesNotContain("dt-checkboxes-select-all", html, StringComparison.Ordinal);
        Assert.DoesNotContain("bulkActionBar", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_reversal_token_is_issued_only_to_a_user_who_may_reverse()
    {
        var readOnly = await RenderAsync(Movements, "inventory.stock.read");
        var reverser = await RenderAsync(Movements, "inventory.stock.read", "inventory.movements.create");

        Assert.DoesNotContain("stockmovements-antiforgery", readOnly, StringComparison.Ordinal);
        Assert.Contains("stockmovements-antiforgery", reverser, StringComparison.Ordinal);
        Assert.Contains("__RequestVerificationToken", reverser, StringComparison.Ordinal);
    }

    // ── W-0173-02 company selector, rendered by the REAL partial with each state ─────────────────────────────

    [Fact]
    public async Task The_selector_lists_the_tenants_companies_and_marks_the_chosen_one()
    {
        var html = await RenderPartialAsync(new CompanySelectorViewModel
        {
            State = CompanySelectorState.Selected,
            Companies = [new CompanyOption("11111111-1111-1111-1111-111111111111", "ACME-TR · Acme Türkiye"), new CompanyOption("22222222-2222-2222-2222-222222222222", "ACME-DE · Acme GmbH")],
            SelectedId = "22222222-2222-2222-2222-222222222222",
            CookieName = "diten-inv-company-abc",
            Label = "Şirket", Placeholder = "Şirket seçin", SelectHint = "hint", UnavailableText = "unavailable"
        });

        Assert.Contains("data-company-state=\"selected\"", html, StringComparison.Ordinal);
        Assert.Contains("data-company-cookie=\"diten-inv-company-abc\"", html, StringComparison.Ordinal);
        Assert.Contains("""<option value="22222222-2222-2222-2222-222222222222" selected>ACME-DE · Acme GmbH</option>""", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Şirket seçin", html, StringComparison.Ordinal);
        Assert.Contains("company-selector.js", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_a_choice_the_selector_asks_for_one()
    {
        var html = await RenderPartialAsync(new CompanySelectorViewModel
        {
            State = CompanySelectorState.NotSelected,
            Companies = [new CompanyOption("11111111-1111-1111-1111-111111111111", "ACME-TR")],
            CookieName = "c", Label = "Şirket", Placeholder = "Şirket seçin", SelectHint = "Stokunu görmek istediğiniz şirketi seçin.", UnavailableText = "u"
        });

        Assert.Contains("""<option value="" selected>Şirket seçin</option>""", html, StringComparison.Ordinal);
        Assert.Contains("Stokunu görmek istediğiniz şirketi seçin.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unreadable_company_list_says_so_and_offers_no_choice()
    {
        var html = await RenderPartialAsync(new CompanySelectorViewModel
        {
            State = CompanySelectorState.Unavailable, Label = "Şirket", UnavailableText = "Şirket listesi alınamadı"
        });

        Assert.Contains("Şirket listesi alınamadı", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<select", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_token_company_draws_no_selector_at_all()
    {
        var html = await RenderPartialAsync(new CompanySelectorViewModel { State = CompanySelectorState.FromToken });

        Assert.Equal(string.Empty, html.Trim());
    }

    // Razor encodes non-ASCII text (ş, ·) as entities; the assertions read what the browser shows.
    private async Task<string> RenderPartialAsync(CompanySelectorViewModel model) =>
        System.Net.WebUtility.HtmlDecode(await RenderAsync("/Views/Inventory/Shared/_CompanySelector.cshtml", model, []));

    private Task<string> RenderAsync(string viewPath, params string[] permissions) => RenderAsync(viewPath, null, permissions);

    private async Task<string> RenderAsync(string viewPath, object? model, string[] permissions)
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
        var metadata = services.GetRequiredService<Microsoft.AspNetCore.Mvc.ModelBinding.IModelMetadataProvider>();
        var viewData = model is null
            ? new ViewDataDictionary(metadata, actionContext.ModelState)
            : new ViewDataDictionary<CompanySelectorViewModel>(metadata, actionContext.ModelState) { Model = (CompanySelectorViewModel)model };
        var tempData = new TempDataDictionary(httpContext, services.GetRequiredService<ITempDataProvider>());

        await using var writer = new StringWriter();
        var viewContext = new ViewContext(actionContext, found.View, viewData, tempData, writer, new HtmlHelperOptions());
        await found.View.RenderAsync(viewContext);
        return writer.ToString();
    }
}
