using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;
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
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Diten.Web.Tests.Inventory;

/// <summary>
/// MOD-0173-FU01 UI-2 pages rendered by the REAL Razor views (body only â€” the shell is not the subject):
/// <list type="bullet">
/// <item>UAS-001: without <c>inventory.movements.create</c> each of the four pages draws <c>_AccessDenied</c> and nothing
/// else â€” no heading, no form, no button;</item>
/// <item>S-3 offers exactly the 7 manual types (no STATUS_TRANSFER, no automatic type), the inbound target status
/// exactly AVAILABLE / QUALITY_INSPECTION / QUARANTINE, and no Reason field while BG-03 is open;</item>
/// <item>the stock entry page (KD-2) offers the two inbound types only; Correct without a history row draws no form;</item>
/// <item>every form field sits in a <c>.diten-field</c> with a glyph that exists in the icon set (form template).</item>
/// </list>
/// </summary>
public sealed class MovementPagesSurfaceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Create = "/Views/Inventory/StockMovements/Create.cshtml";
    private const string Edit = "/Views/Inventory/StockMovements/Edit.cshtml";
    private const string Details = "/Views/Inventory/StockMovements/Details.cshtml";
    private const string Entry = "/Views/Inventory/StockLedger/Create.cshtml";
    private const string PostPermission = "inventory.movements.create";

    private static readonly string[] ManualTypes =
        ["OPENING_BALANCE", "COUNT_GAIN", "COUNT_LOSS", "SCRAP", "WRITE_OFF", "WAREHOUSE_TRANSFER", "LOCATION_TRANSFER"];

    private readonly WebApplicationFactory<Program> _factory;

    public MovementPagesSurfaceTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Theory]
    [InlineData(Create)]
    [InlineData(Edit)]
    [InlineData(Details)]
    [InlineData(Entry)]
    public async Task Without_the_post_permission_only_the_access_explanation_is_drawn(string view)
    {
        var html = await RenderAsync(view, ModelFor(view), "inventory.stock.read");

        Assert.Contains("diten-access-denied", html, StringComparison.Ordinal);
        foreach (var skeleton in new[] { "formStockMovement", "btnMovementSubmit", "<h5 class=\"mb-0\">", "movement-receipt", "movement-effect", "idempotencyKey" })
            Assert.DoesNotContain(skeleton, html, StringComparison.Ordinal);
        Assert.DoesNotContain("Permission denied", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Forbidden", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task New_movement_offers_the_seven_manual_types_plus_customer_return_and_a_customer_return_only_held_statuses()
    {
        var html = await RenderAsync(Create, StockMovementFormViewModel.ForCreate(new QueryCollection()), "inventory.stock.read", PostPermission);

        Assert.DoesNotContain("diten-access-denied", html, StringComparison.Ordinal);
        // WP-0177-R7c (KR-H, CT decision A): the 7 manual types + CUSTOMER_RETURN — exactly MovementRules.FormTypes, nothing else
        Assert.Equal([.. ManualTypes, "CUSTOMER_RETURN"], MovementRules.FormTypes);
        Assert.Equal(MovementRules.FormTypes.OrderBy(t => t), OptionsOf(html, "movementType").OrderBy(t => t));
        // the target statuses: the three inbound ones as before + BLOCKED; each option names the types that may use it
        Assert.Equal(["AVAILABLE", "QUALITY_INSPECTION", "QUARANTINE", "BLOCKED"], OptionsOf(html, "toStockStatus"));
        string Types(string status) => Regex.Match(html, $@"<option value=""{status}"" data-types=""(?<t>[^""]*)""").Groups["t"].Value;
        Assert.Equal("OPENING_BALANCE COUNT_GAIN", Types("AVAILABLE"));                              // a customer return never into AVAILABLE
        Assert.Equal("OPENING_BALANCE COUNT_GAIN CUSTOMER_RETURN", Types("QUALITY_INSPECTION"));
        Assert.Equal("OPENING_BALANCE COUNT_GAIN CUSTOMER_RETURN", Types("QUARANTINE"));
        Assert.Equal("CUSTOMER_RETURN", Types("BLOCKED"));                                           // BLOCKED belongs to it alone
        Assert.DoesNotContain("STATUS_TRANSFER", html, StringComparison.Ordinal);
        Assert.DoesNotContain("GOODS_RECEIPT", html, StringComparison.Ordinal);
        // BG-03: the Reason field is not drawn while no MOD-0173 movement-reason set exists.
        Assert.DoesNotContain("id=\"movementReason\"", html, StringComparison.Ordinal);
        Assert.Contains("data-post-url=\"/StockMovements/Create\"", html, StringComparison.Ordinal);
        Assert.Matches("id=\"idempotencyKey\" value=\"[0-9a-f-]{36}\"", html);
        Assert.Contains("__RequestVerificationToken", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_stock_entry_page_offers_the_inbound_types_only_and_posts_through_the_movement_form()
    {
        var html = await RenderAsync(Entry, StockMovementFormViewModel.ForEntry(new QueryCollection()), "inventory.stock.read", PostPermission);

        Assert.Equal(["OPENING_BALANCE", "COUNT_GAIN"], OptionsOf(html, "movementType"));
        Assert.Contains("data-mode=\"entry\"", html, StringComparison.Ordinal);
        Assert.Contains("data-post-url=\"/StockMovements/Create\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/StockLedger\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Correct_without_a_history_row_draws_no_form_and_with_one_carries_both_keys()
    {
        var missing = await RenderAsync(Edit, StockMovementFormViewModel.ForCorrection(new QueryCollection()), "inventory.stock.read", PostPermission);
        Assert.Contains("movement-original-missing", missing, StringComparison.Ordinal);
        Assert.DoesNotContain("formStockMovement", missing, StringComparison.Ordinal);

        var row = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["id"] = Guid.NewGuid().ToString(), ["no"] = "INV-2026-0000007", ["type"] = "SCRAP",
            ["itemId"] = Guid.NewGuid().ToString(), ["skuId"] = Guid.NewGuid().ToString(), ["skuLevel"] = "Gsku",
            ["quantity"] = "3", ["uomId"] = "ADET", ["lotId"] = "L-1"
        });
        var html = await RenderAsync(Edit, StockMovementFormViewModel.ForCorrection(row), "inventory.stock.read", PostPermission);
        Assert.Contains("data-post-url=\"/StockMovements/Edit\"", html, StringComparison.Ordinal);
        Assert.Matches("id=\"reversalKey\" value=\"[0-9a-f-]{36}\"", html);
        Assert.Matches("id=\"reversalAt\" value=\"\\d{4}-\\d\\d-\\d\\dT\\d\\d:\\d\\d:\\d\\dZ\"", html);
        Assert.Contains("INV-2026-0000007", html, StringComparison.Ordinal);
        Assert.Contains("movement-partial", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_receipt_prints_the_lot_number_and_an_unresolved_lot_as_its_id_with_the_hint()
    {
        // WP-MVP1-FIX-LIVE2 (finding 9)
        const string lot = "d4e5f6a7-0000-4000-8000-000000000042";
        var named = await RenderAsync(Details, new MovementReceiptViewModel { TransactionNumber = "INV-1", LotId = lot, LotNumber = "LOT-2026-042", BalanceLotId = lot, BalanceLotNumber = "LOT-2026-042" }, PostPermission);
        Assert.Contains("LOT-2026-042", named, StringComparison.Ordinal);
        Assert.DoesNotContain(lot, named, StringComparison.Ordinal);
        Assert.DoesNotContain("data-lot-unresolved", named, StringComparison.Ordinal);

        var bare = await RenderAsync(Details, new MovementReceiptViewModel { TransactionNumber = "INV-1", LotId = lot, LotUnresolved = true }, PostPermission);
        Assert.Contains(lot, bare, StringComparison.Ordinal);
        Assert.Contains("data-lot-unresolved", bare, StringComparison.Ordinal);
        Assert.Matches("(lot number could not be read|lot numarası okunamadı)", bare);
    }

    [Fact]
    public async Task The_receipt_shows_the_number_the_balance_and_the_replay_banner_and_an_empty_one_says_so()
    {
        var empty = await RenderAsync(Details, new MovementReceiptViewModel(), PostPermission);
        Assert.Contains("movement-receipt-missing", empty, StringComparison.Ordinal);

        var html = await RenderAsync(Details, new MovementReceiptViewModel
        {
            TransactionNumber = "INV-2026-0000009", IdempotentReplay = true, MovementType = "OPENING_BALANCE",
            Quantity = "2", UomId = "KOLI", BaseQuantity = "24", BaseUomId = "ADET", BalanceOnHand = "24", LotId = "L-9",
            ReversalNumber = "INV-2026-0000010", CorrectedNumber = "INV-2026-0000008"
        }, PostPermission);
        Assert.Contains("INV-2026-0000009", html, StringComparison.Ordinal);
        Assert.Contains("movement-replay-banner", html, StringComparison.Ordinal);
        Assert.Contains("movement-correction-done", html, StringComparison.Ordinal);
        Assert.Contains("data-quantity=\"24\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Every_form_field_carries_an_icon_that_exists_in_the_icon_set()
    {
        var html = await RenderAsync(Create, StockMovementFormViewModel.ForCreate(new QueryCollection()), "inventory.stock.read", PostPermission);
        var icons = File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "vendor", "fonts", "iconify-icons.css"));

        string[] fields = ["movementType", "itemId", "sku", "source", "toWarehouseId", "toLocationId", "toStockStatus", "lotId",
            "serialNumbers", "quantity", "uomId", "documentNo", "transactionDate", "postingDate"];
        foreach (var id in fields)
        {
            var at = html.IndexOf($"id=\"{id}\"", StringComparison.Ordinal);
            Assert.True(at > 0, $"{id} is not drawn");
            var opened = html.LastIndexOf("class=\"diten-field", at, StringComparison.Ordinal);
            Assert.True(opened > 0, $"{id} has no .diten-field");
            var between = html[opened..at];
            Assert.DoesNotContain("</div>", between, StringComparison.Ordinal);
            var glyph = Regex.Match(between, "<i class=\"bx (?<g>bx-[a-z0-9-]+) diten-field-icon").Groups["g"].Value;
            Assert.False(string.IsNullOrEmpty(glyph), $"{id} has no icon");
            Assert.Matches($@"\.{Regex.Escape(glyph)}(?![a-z0-9-])", icons);
        }

        // â€¦and no field slipped in without being listed (the count is pinned, as diten-field-icons.test.js does).
        Assert.Equal(fields.Length, Regex.Matches(html, "class=\"diten-field[ \"]").Count);
    }

    // â”€â”€ harness (the InventoryAccessSurfaceTests renderer, with a typed model) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static object ModelFor(string view) =>
        view == Details ? new MovementReceiptViewModel() : StockMovementFormViewModel.ForCreate(new QueryCollection());

    private static IEnumerable<string> OptionsOf(string html, string selectId)
    {
        var start = html.IndexOf($"id=\"{selectId}\"", StringComparison.Ordinal);
        var end = html.IndexOf("</select>", start, StringComparison.Ordinal);
        return Regex.Matches(html[start..end], "<option value=\"(?<v>[^\"]+)\"").Select(m => m.Groups["v"].Value).ToList();
    }

    private static string WebRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "frontend", "Diten.Web");
            if (Directory.Exists(Path.Combine(candidate, "wwwroot"))) return candidate;
            current = current.Parent;
        }

        throw new InvalidOperationException("frontend/Diten.Web not found above the test binary.");
    }

    private async Task<string> RenderAsync(string viewPath, object model, params string[] permissions)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");

        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        // The token names its company, so the form needs no selector and makes no MDM call while rendering (W-0173-02).
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
        var viewData = new ViewDataDictionary(metadata, actionContext.ModelState) { Model = model };
        var tempData = new TempDataDictionary(httpContext, services.GetRequiredService<ITempDataProvider>());

        await using var writer = new StringWriter();
        var viewContext = new ViewContext(actionContext, found.View, viewData, tempData, writer, new HtmlHelperOptions());
        await found.View.RenderAsync(viewContext);
        return writer.ToString();
    }
}
