using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Diten.Web.Models.Warehouse;
using Diten.Web.Tests.Traceability;
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

namespace Diten.Web.Tests.Warehouse;

/// <summary>
/// MOD-0178 DL-3 — the two S1 warehouse screens rendered by the REAL Razor views (pack §9, §11; AC-21, AC-22, AC-45, AC-50):
/// <list type="bullet">
/// <item>UAS-001: without the page key the page draws <c>_AccessDenied</c> and NOTHING else — no heading, filter, table, skeleton or button;</item>
/// <item>every control whose key the reader lacks is ABSENT (not disabled): the form, the confirm panel, the reason / assign dialogs, the bulk bar;</item>
/// <item>the page views say <c>Layout = "_LayoutTenantShell"</c> themselves; the partials set no layout;</item>
/// <item>the shell turns <c>ar</c> right-to-left and the six other tenant languages left-to-right; the page CSS is logical only;</item>
/// <item>the confirm panel's touch targets carry the 48 px class, the quantity has −/+ and the action bar is sticky at the bottom.</item>
/// </list>
/// Display decisions only — InventoryService's [HasPermission] and state rules stay the authority (UAS-001 §4).
/// </summary>
public sealed class WarehouseScreensSurfaceTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Putaway = "/Views/Warehouse/PutawayTasks/Index.cshtml";
    private const string Roles = "/Views/Warehouse/WarehouseLocationRoles/Index.cshtml";
    private static readonly string Web = TraceViewRenderer.WebRoot;

    private static readonly string[] PutawayAll =
    [
        WarehousePermissionKeys.PutawayTasksRead, WarehousePermissionKeys.PutawayTasksCreate, WarehousePermissionKeys.PutawayTasksUpdate,
        WarehousePermissionKeys.PutawayTasksConfirm, WarehousePermissionKeys.PutawayTasksCancel, WarehousePermissionKeys.PutawayTasksResolve
    ];

    // What a page must not draw for a reader without its key (heading, filter, table, skeleton, panels, buttons, scripts).
    private static readonly string[] Skeleton =
        ["inlineFilterHost", "skeleton-loader", "data-dt-data-mode", "<h5 class=\"mb-0\">", "offcanvasDetailsPreview", "offcanvasCreateEdit", "offcanvasConfirm",
         "bulkActionBar", "dt-putawaytasks", "dt-warehouselocationroles", "inventory-company-selector"];

    [Theory]
    [InlineData(Putaway, "warehouse.location-roles.read")]
    [InlineData(Roles, "warehouse.putaway-tasks.read")]
    public async Task AC22_UAS001_without_the_page_key_only_the_access_explanation_is_drawn(string view, string otherKey)
    {
        var html = await RenderAsync(view, "en", otherKey);

        Assert.Contains("diten-access-denied", html, StringComparison.Ordinal);
        foreach (var marker in Skeleton) Assert.False(html.Contains(marker, StringComparison.Ordinal), $"{view} draws {marker} for a reader without its key");
        Assert.DoesNotContain("Forbidden", html, StringComparison.Ordinal);
        Assert.DoesNotContain("location.href", html, StringComparison.Ordinal); // no redirect
    }

    [Fact]
    public async Task AC22_the_putaway_list_is_a_server_mode_v2_table_with_the_company_scoped_filter()
    {
        var html = await RenderAsync(Putaway, "en", WarehousePermissionKeys.PutawayTasksRead);

        Assert.DoesNotContain("diten-access-denied", html, StringComparison.Ordinal);
        Assert.Contains("id=\"dt-putawaytasks\" data-dt-standard=\"v2\" data-dt-data-mode=\"server\"", html, StringComparison.Ordinal);
        Assert.Contains("inlineFilterHost", html, StringComparison.Ordinal);
        Assert.Contains("id=\"offcanvasDetailsPreview\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_read_only_putaway_reader_gets_no_write_control_at_all()
    {
        var html = await RenderAsync(Putaway, "en", WarehousePermissionKeys.PutawayTasksRead);

        foreach (var control in new[] { "offcanvasCreateEdit", "offcanvasConfirm", "modalPutawayReason", "modalPutawayAssign", "bulkActionBar", "putawayActionToken", "formPutawayTask" })
            Assert.False(html.Contains(control, StringComparison.Ordinal), $"read-only page draws {control}");
    }

    [Fact]
    public async Task An_operator_gets_the_confirm_panel_and_the_reason_dialog_but_no_form_and_no_bulk_assign()
    {
        var html = await RenderAsync(Putaway, "en", WarehousePermissionKeys.PutawayTasksRead, WarehousePermissionKeys.PutawayTasksConfirm);

        Assert.Contains("id=\"offcanvasConfirm\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"modalPutawayReason\"", html, StringComparison.Ordinal);
        Assert.Contains("__RequestVerificationToken", Between(html, "id=\"putawayActionToken\"", "</form>"), StringComparison.Ordinal);
        foreach (var control in new[] { "offcanvasCreateEdit", "modalPutawayAssign", "bulkActionBar" })
            Assert.False(html.Contains(control, StringComparison.Ordinal), $"an operator gets {control}");
    }

    [Fact]
    public async Task A_supervisor_gets_the_form_the_bulk_assign_bar_and_every_dialog()
    {
        var html = await RenderAsync(Putaway, "en", PutawayAll);

        foreach (var control in new[] { "offcanvasCreateEdit", "offcanvasConfirm", "modalPutawayReason", "modalPutawayAssign", "bulkActionBar", "data-bulk-action=\"assign\"" })
            Assert.Contains(control, html, StringComparison.Ordinal);
        Assert.Contains("__RequestVerificationToken", Between(html, "id=\"formPutawayTask\"", "</form>"), StringComparison.Ordinal);
        Assert.Contains("id=\"btnApplySuggestion\"", html, StringComparison.Ordinal); // "Uygula" is a form control (AC-42), never auto-applied
        Assert.DoesNotContain("name=\"legalEntityId\"", html, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// DL-3 CT F-2 (§11.3a): the create form carries the "Saklama koşulu bilinmiyor" badge and the save reason, hidden until rules.js
    /// applyConditionGate opens them (measured in warehouse-putaway-rules.test.mjs); the badge text is the page's localized word, never hardcoded.
    /// </summary>
    [Fact]
    public async Task F2_the_create_form_carries_the_unknown_condition_badge_and_the_save_reason_hidden_until_needed()
    {
        var html = await RenderAsync(Putaway, "tr", PutawayAll);

        var badge = Between(html, "id=\"putawayConditionsUnknown\"", "</div>");
        Assert.Matches(@"class=""[^""]*\bd-none\b", Between(html, "<div id=\"putawayConditionsUnknown\"", ">"));
        Assert.Contains("Saklama koşulu bilinmiyor", System.Net.WebUtility.HtmlDecode(badge), StringComparison.Ordinal);
        Assert.Matches(@"<small id=""putawaySaveReason"" class=""[^""]*\bd-none\b", html);
        Assert.Contains("id=\"btnSavePutawayTask\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_role_screen_draws_writes_only_for_manage()
    {
        var read = await RenderAsync(Roles, "en", WarehousePermissionKeys.LocationRolesRead);
        var manage = await RenderAsync(Roles, "en", WarehousePermissionKeys.LocationRolesRead, WarehousePermissionKeys.LocationRolesManage);

        Assert.Contains("id=\"dt-warehouselocationroles\" data-dt-standard=\"v2\" data-dt-data-mode=\"server\"", read, StringComparison.Ordinal);
        foreach (var control in new[] { "offcanvasCreateEdit", "formLocationRole", "bulkActionBar" })
            Assert.False(read.Contains(control, StringComparison.Ordinal), $"read-only role page draws {control}");
        Assert.Contains("id=\"formLocationRole\"", manage, StringComparison.Ordinal);
        Assert.Contains("bulkActionBar", manage, StringComparison.Ordinal);
        // §11.3a: no "storage" role — the choices are the contract's two roles and five condition codes
        Assert.Equal(2, Regex.Matches(manage, "data-role-choice").Count);
        Assert.Equal(5, Regex.Matches(manage, "data-condition-choice").Count);
    }

    /// <summary>AC-21: every page view of the two folders names the tenant shell itself; a partial sets no layout (a layout in a partial nests the shell).</summary>
    [Fact]
    public void AC21_the_page_views_name_the_tenant_shell_explicitly_and_the_partials_set_no_layout()
    {
        var files = new[] { "PutawayTasks", "WarehouseLocationRoles" }
            .SelectMany(folder => Directory.GetFiles(Path.Combine(Web, "Views", "Warehouse", folder), "*.cshtml")).ToList();
        Assert.Equal(12, files.Count);
        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            if (Path.GetFileName(file).StartsWith('_')) Assert.DoesNotMatch(@"\bLayout\s*=", source);
            else Assert.Contains("Layout = \"_LayoutTenantShell\";", source, StringComparison.Ordinal);
        }
    }

    /// <summary>AC-50: through the real shell, ar is right-to-left and the other six tenant languages left-to-right, on both screens.</summary>
    [Theory]
    [InlineData("ar", "rtl")]
    [InlineData("en", "ltr")]
    [InlineData("tr", "ltr")]
    [InlineData("fr", "ltr")]
    [InlineData("es", "ltr")]
    [InlineData("zh", "ltr")]
    [InlineData("ru", "ltr")]
    public async Task AC50_the_shell_turns_only_arabic_right_to_left(string culture, string direction)
    {
        foreach (var (view, key) in new[] { (Putaway, WarehousePermissionKeys.PutawayTasksRead), (Roles, WarehousePermissionKeys.LocationRolesRead) })
        {
            var html = await RenderAsync(view, culture, mainPage: true, key);
            Assert.Matches($"<html lang=\"{culture}\"[^>]*dir=\"{direction}\"", html);
            Assert.Contains("dt-", html, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AC50_the_page_css_uses_logical_properties_only()
    {
        var css = File.ReadAllText(Path.Combine(Web, "wwwroot", "assets", "css", "Warehouse", "warehouse.css"));
        Assert.DoesNotMatch(@"(margin|padding)-(left|right)\s*:|(?<![-\w])(left|right)\s*:|text-align\s*:\s*(left|right)|float\s*:\s*(left|right)", css);
    }

    /// <summary>AC-45: the confirm panel's controls carry the touch class (≥ 48 px in the CSS), −/+ around the quantity, a sticky bottom bar.</summary>
    [Fact]
    public async Task AC45_the_confirm_panel_has_48px_touch_targets_a_stepper_and_a_sticky_action_bar()
    {
        var html = await RenderAsync(Putaway, "en", WarehousePermissionKeys.PutawayTasksRead, WarehousePermissionKeys.PutawayTasksConfirm);
        var panel = Between(html, "id=\"offcanvasConfirm\"", "id=\"modalPutawayReason\"");

        Assert.Equal(2, Regex.Matches(panel, "data-wh-step=\"(-1|1)\"").Count);
        foreach (var id in new[] { "confirmQuantity", "confirmBinCode", "btnConfirmSubmit", "btnConfirmRetry" })
            Assert.Matches($"<[^>]*(class=\"[^\"]*wh-touch[^\"]*\"[^>]*id=\"{id}\"|id=\"{id}\"[^>]*class=\"[^\"]*wh-touch)", panel);
        Assert.Equal(2, Regex.Matches(panel, "<button[^>]*wh-touch[^>]*data-wh-step").Count);
        Assert.Contains("class=\"wh-action-bar", panel, StringComparison.Ordinal);
        Assert.Contains("inputmode=\"decimal\"", panel, StringComparison.Ordinal);

        var css = File.ReadAllText(Path.Combine(Web, "wwwroot", "assets", "css", "Warehouse", "warehouse.css"));
        var touch = Regex.Match(css, @"\.wh-touch\s*\{([^}]*)\}").Groups[1].Value;
        Assert.Matches(@"min-block-size:\s*48px", touch);
        Assert.Matches(@"min-inline-size:\s*48px", touch);
        var bar = Regex.Match(css, @"\.wh-action-bar\s*\{([^}]*)\}").Groups[1].Value;
        Assert.Matches(@"position:\s*sticky", bar);
        Assert.Matches(@"inset-block-end:\s*0", bar);
    }

    [Fact]
    public async Task AC44_the_bin_fields_take_keyboard_and_scanner_input()
    {
        var html = await RenderAsync(Putaway, "en", PutawayAll);
        foreach (var id in new[] { "confirmBinCode", "putawayBinCode" })
            Assert.Matches($"<input type=\"text\" id=\"{id}\"[^>]*data-wh-bin-input[^>]*autocomplete=\"off\"", html);
    }

    private static string Between(string html, string start, string end)
    {
        var from = html.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, $"{start} is not on the page");
        var to = html.IndexOf(end, from, StringComparison.Ordinal);
        return to < 0 ? html[from..] : html[from..to];
    }

    private Task<string> RenderAsync(string viewPath, string culture, params string[] permissions) => RenderAsync(viewPath, culture, false, permissions);

    private async Task<string> RenderAsync(string viewPath, string culture, bool mainPage, params string[] permissions)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            using var scope = factory.Services.CreateScope();
            var services = scope.ServiceProvider;
            // The token names its company, so the page needs no selector and makes no MDM call while rendering (W-0173-02).
            var claims = new List<Claim>
            {
                new("tenantId", Guid.NewGuid().ToString()), new("tenant_id", Guid.NewGuid().ToString()), new("actor_type", "tenant_user"),
                new("sub", Guid.NewGuid().ToString()), new("legal_entity_id", Guid.NewGuid().ToString())
            };
            claims.AddRange(permissions.Select(p => new Claim("permission", p)));
            var httpContext = new DefaultHttpContext { RequestServices = services, User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new HostString("tenant.test");
            httpContext.Request.Path = "/Warehouse/PutawayTasks";
            services.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;

            var engine = services.GetRequiredService<IRazorViewEngine>();
            var found = engine.GetView(executingFilePath: null, viewPath: viewPath, isMainPage: mainPage);
            if (!found.Success) throw new InvalidOperationException($"Could not load {viewPath}.");
            var actionContext = new ActionContext(httpContext, new RouteData { Values = { ["controller"] = "PutawayTasks", ["action"] = "Index" } },
                new ActionDescriptor(), new ModelStateDictionary());
            var viewData = new ViewDataDictionary(services.GetRequiredService<IModelMetadataProvider>(), actionContext.ModelState);
            var tempData = new TempDataDictionary(httpContext, services.GetRequiredService<ITempDataProvider>());
            await using var writer = new StringWriter();
            await found.View.RenderAsync(new ViewContext(actionContext, found.View, viewData, tempData, writer, new HtmlHelperOptions()));
            return writer.ToString();
        }
        finally
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        }
    }
}
