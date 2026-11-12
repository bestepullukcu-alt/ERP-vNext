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
/// MOD-0181 DL-4 — the three cycle-count screens rendered by the REAL Razor views (pack §8.13, §11; AC-37, AC-38, AC-39 static half):
/// <list type="bullet">
/// <item>UAS-001 (AC-38): without <c>cycle-count.counts.read</c> the dashboard and the count sheet, without <c>cycle-count.variances.read</c> the variance
/// trace draw <c>_AccessDenied</c> and NOTHING else — no heading, card, filter, table, skeleton, line or button; no redirect;</item>
/// <item>every control whose key the reader lacks is ABSENT: the Draft form, the bulk bar, the cancel / release buttons, the sheet's entry bar and found
/// stock panel, the decision form;</item>
/// <item>the count sheet is phone first: 48 px touch targets, a decimal keypad, a sticky bottom bar (the 375 px measurement itself is live, AC-39);</item>
/// <item>the shell turns <c>ar</c> right-to-left on all three screens.</item>
/// </list>
/// Display decisions only — InventoryService's [HasPermission] and state rules stay the authority (UAS-001 §4).
/// </summary>
public sealed class CycleCountScreensTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Dashboard = "/Views/Warehouse/CycleCounts/Index.cshtml";
    private const string Sheet = "/Views/Warehouse/CycleCounts/CountSheet.cshtml";
    private const string Variances = "/Views/Warehouse/CountVariances/Index.cshtml";
    private const string Evidence = "/Views/Warehouse/CountEvidenceExports/Index.cshtml";
    private const string CountId = "b1111111-0000-0000-0000-000000000001";
    private static readonly string Web = TraceViewRenderer.WebRoot;

    // What a page must not draw for a reader without its key (heading, cards, filter, table, skeleton, panels, lines, buttons, selector).
    private static readonly string[] Skeleton =
        ["inlineFilterHost", "skeleton-loader", "data-dt-data-mode", "<h5 class=\"mb-0\"", "offcanvasDetailsPreview", "offcanvasCreateEdit", "offcanvasFound",
         "bulkActionBar", "dt-cyclecounts", "dt-countvariances", "data-cc-kpi", "cc-sheet-bins", "cc-sheet-bar", "btnSubmitCount", "inventory-company-selector",
         "countsheet.js", "CountVariances/index.js", "CycleCounts/index.js", "dt-countevidenceexports", "CountEvidenceExports/index.js", "formExport"];

    [Theory]
    [InlineData(Dashboard, CycleCountPermissionKeys.VariancesRead)]
    [InlineData(Sheet, CycleCountPermissionKeys.VariancesRead)]
    [InlineData(Variances, CycleCountPermissionKeys.CountsRead)]
    [InlineData(Evidence, CycleCountPermissionKeys.EvidenceExport)] // DL-5 AC-50: the export key alone does not open the page (evidence.read is its key)
    public async Task AC38_UAS001_without_the_page_key_only_the_access_explanation_is_drawn(string view, string otherKey)
    {
        var html = await RenderAsync(view, "en", false, otherKey);

        Assert.Contains("diten-access-denied", html, StringComparison.Ordinal);
        foreach (var marker in Skeleton) Assert.False(html.Contains(marker, StringComparison.Ordinal), $"{view} draws {marker} for a reader without its key");
        Assert.DoesNotContain("Forbidden", html, StringComparison.Ordinal);
        Assert.DoesNotContain("location.href", html, StringComparison.Ordinal); // no redirect
    }

    [Fact]
    public async Task A_read_only_count_reader_gets_the_list_and_the_cards_but_no_write_control()
    {
        var html = await RenderAsync(Dashboard, "en", false, CycleCountPermissionKeys.CountsRead);

        Assert.Contains("dt-cyclecounts", html, StringComparison.Ordinal);
        Assert.Contains("data-dt-data-mode=\"server\"", html, StringComparison.Ordinal);
        Assert.Equal(5, Regex.Matches(html, "data-cc-kpi=\"").Count);
        foreach (var marker in new[] { "offcanvasCreateEdit", "formCycleCount", "bulkActionBar", "oc-btn-cancel", "oc-btn-edit", "oc-btn-release", "ccReasonModal" })
            Assert.False(html.Contains(marker, StringComparison.Ordinal), $"a read-only reader gets {marker}");
    }

    [Fact]
    public async Task A_supervisor_gets_the_form_the_bulk_bar_and_the_cancel_dialog_and_no_delete_row_action()
    {
        var html = await RenderAsync(Dashboard, "en", false, CycleCountPermissionKeys.CountsRead, CycleCountPermissionKeys.CountsCreate,
            CycleCountPermissionKeys.CountsUpdate, CycleCountPermissionKeys.CountsCancel, CycleCountPermissionKeys.CountsBulkDelete);

        foreach (var marker in new[] { "formCycleCount", "bulkActionBar", "oc-btn-cancel", "oc-btn-edit", "oc-btn-release", "ccReasonModal" })
            Assert.Contains(marker, html, StringComparison.Ordinal);
        Assert.Equal(8, Regex.Matches(Between(html, "id=\"formCycleCount\"", "</form>"), "<(input|select|textarea)[^>]*id=\"cc").Count - 1); // 8 fields (two type radios)
        Assert.DoesNotContain("cycle-count.counts.delete", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_sheet_reader_without_the_record_key_gets_the_lines_but_no_entry_bar_and_no_found_stock()
    {
        var html = await RenderAsync(Sheet, "en", false, CycleCountPermissionKeys.CountsRead);

        Assert.Contains("cc-sheet-bins", html, StringComparison.Ordinal);
        Assert.Contains($"data-count-id=\"{CountId}\"", html, StringComparison.Ordinal);
        foreach (var marker in new[] { "cc-sheet-bar", "btnSubmitCount", "btnAddFound", "offcanvasFound", "formCountSheet" })
            Assert.False(html.Contains(marker, StringComparison.Ordinal), $"a reader without counts.record gets {marker}");
    }

    [Fact]
    public async Task DL4_FIX01_the_count_sheet_script_reads_exactly_the_root_names_the_rendered_l10n_JSON_has()
    {
        // the REAL page through the real shell (the Scripts section renders only on a main page), in Turkish
        var html = await RenderAsync(Sheet, "tr", true, CycleCountPermissionKeys.CountsRead, CycleCountPermissionKeys.CountsRecord);
        var json = Regex.Match(html, "<script id=\"countsheet-l10n\" type=\"application/json\">(.*?)</script>", RegexOptions.Singleline);
        Assert.True(json.Success, "the count sheet renders no countsheet-l10n payload");
        using var payload = System.Text.Json.JsonDocument.Parse(System.Net.WebUtility.HtmlDecode(json.Groups[1].Value));
        var rendered = payload.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

        // the production script: every root name it reads off the parsed payload is one the page really writes
        var script = File.ReadAllText(Path.Combine(Web, "wwwroot", "assets", "js", "Warehouse", "CycleCounts", "countsheet.js"));
        Assert.Contains("JSON.parse(byId('countsheet-l10n')", script, StringComparison.Ordinal);
        var read = Regex.Matches(script, @"\bpayload\.(\w+)").Select(m => m.Groups[1].Value).Distinct().ToList();
        Assert.Contains("words", read);
        Assert.All(read, name => Assert.True(rendered.Contains(name), $"countsheet.js reads payload.{name}; the page writes only {string.Join(", ", rendered)}"));

        // and the words it gets are the page's own, in the page's language (no hardcoded or fallback text in the script)
        var words = payload.RootElement.GetProperty("words");
        Assert.Equal("Sayım Formu", words.GetProperty("CountSheetTitle").GetString());
        Assert.False(string.IsNullOrWhiteSpace(words.GetProperty("ReadonlyDraft").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(words.GetProperty("CycleCount.Status.Draft").GetString()));
    }

    [Fact]
    public async Task AC39_the_count_sheet_has_48px_touch_targets_a_decimal_keypad_and_a_sticky_bottom_bar()
    {
        var html = await RenderAsync(Sheet, "en", false, CycleCountPermissionKeys.CountsRead, CycleCountPermissionKeys.CountsRecord);

        foreach (var id in new[] { "btnAddFound", "btnSubmitCount", "btnSaveFound" })
            Assert.Matches($"<button[^>]*class=\"[^\"]*cc-touch[^\"]*\"[^>]*id=\"{id}\"", html);
        Assert.Contains("inputmode=\"decimal\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"cc-sheet-bar\"", html, StringComparison.Ordinal);
        Assert.Contains("countsheet.css", html, StringComparison.Ordinal);

        var css = File.ReadAllText(Path.Combine(Web, "wwwroot", "assets", "css", "Warehouse", "countsheet.css"));
        var touch = Regex.Match(css, @"\.cc-touch,[^{]*\{([^}]*)\}").Groups[1].Value;
        Assert.Matches(@"min-height:\s*48px", touch);
        Assert.Matches(@"min-width:\s*48px", touch);
        var bar = Regex.Match(css, @"\.cc-sheet-bar\s*\{([^}]*)\}").Groups[1].Value;
        Assert.Matches(@"position:\s*sticky", bar);
        Assert.Matches(@"bottom:\s*0", bar);

        // the sheet script never invents an expected quantity: it draws one only when the service's line carries it (pack §8.2 rule 5)
        var script = File.ReadAllText(Path.Combine(Web, "wwwroot", "assets", "js", "Warehouse", "CycleCounts", "countsheet.js"));
        Assert.Contains("line.expectedQuantity !== null && line.expectedQuantity !== undefined", script, StringComparison.Ordinal);
        Assert.DoesNotContain("expectedAtCount", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_variance_trace_draws_each_decision_only_behind_its_key()
    {
        var reader = await RenderAsync(Variances, "en", false, CycleCountPermissionKeys.VariancesRead);
        Assert.Contains("dt-countvariances", reader, StringComparison.Ordinal);
        Assert.DoesNotContain("formDecision", reader, StringComparison.Ordinal);
        Assert.DoesNotContain("data-decision=", reader, StringComparison.Ordinal);

        var approver = await RenderAsync(Variances, "en", false, CycleCountPermissionKeys.VariancesRead, CycleCountPermissionKeys.VariancesApprove);
        Assert.Equal(["approve"], Regex.Matches(approver, "data-decision=\"([a-z-]+)\"").Select(m => m.Groups[1].Value));

        var all = await RenderAsync(Variances, "en", false, CycleCountPermissionKeys.VariancesRead, CycleCountPermissionKeys.VariancesApprove,
            CycleCountPermissionKeys.VariancesReject, CycleCountPermissionKeys.VariancesRecount, CycleCountPermissionKeys.VariancesPost, CycleCountPermissionKeys.VariancesResolve);
        Assert.Equal(["approve", "reject", "recount", "retry", "accept-holder", "reject-holder"],
            Regex.Matches(all, "data-decision=\"([a-z-]+)\"").Select(m => m.Groups[1].Value));
    }

    [Theory]
    [InlineData("ar", "rtl")]
    [InlineData("en", "ltr")]
    [InlineData("tr", "ltr")]
    [InlineData("ru", "ltr")]
    public async Task AC37_the_shell_turns_only_arabic_right_to_left_on_the_three_screens(string culture, string direction)
    {
        foreach (var (view, key) in new[]
                 {
                     (Dashboard, CycleCountPermissionKeys.CountsRead), (Sheet, CycleCountPermissionKeys.CountsRead), (Variances, CycleCountPermissionKeys.VariancesRead),
                     (Evidence, CycleCountPermissionKeys.EvidenceRead)
                 })
        {
            var html = await RenderAsync(view, culture, true, key);
            Assert.Matches($"<html lang=\"{culture}\"[^>]*dir=\"{direction}\"", html);
        }
    }

    /// <summary>DL-5 AC-50: the Evidence Pack list for evidence.read; the export form only with evidence.export (absent, not disabled).</summary>
    [Fact]
    public async Task AC50_the_evidence_page_draws_the_list_for_its_reader_and_the_export_form_only_for_the_exporter()
    {
        var reader = await RenderAsync(Evidence, "en", false, CycleCountPermissionKeys.EvidenceRead);
        Assert.Contains("dt-countevidenceexports", reader, StringComparison.Ordinal);
        Assert.Contains("data-dt-data-mode=\"server\"", reader, StringComparison.Ordinal);
        Assert.Contains("oc-btn-download", reader, StringComparison.Ordinal);
        foreach (var marker in new[] { "offcanvasCreateEdit", "formExport", "btnSaveExport", "bulkActionBar", "dt-checkboxes" })
            Assert.False(reader.Contains(marker, StringComparison.Ordinal), $"a reader gets {marker}");

        var exporter = await RenderAsync(Evidence, "tr", false, CycleCountPermissionKeys.EvidenceRead, CycleCountPermissionKeys.EvidenceExport);
        foreach (var marker in new[] { "formExport", "exportCount", "exportFormatPdf", "exportFormatCsv", "btnSaveExport" })
            Assert.Contains(marker, exporter, StringComparison.Ordinal);
        Assert.Contains("Sayım Kanıtları", exporter, StringComparison.Ordinal);
        Assert.Matches("<button[^>]*data-dt-primary=\"chord\"[^>]*id=\"btnSaveExport\"", exporter); // BL-586: an evidence export is consequential
    }

    [Fact]
    public void AC50_the_count_sheet_css_has_no_physical_left_or_right()
    {
        var css = File.ReadAllText(Path.Combine(Web, "wwwroot", "assets", "css", "Warehouse", "countsheet.css"));
        Assert.DoesNotMatch(@"(margin|padding)-(left|right)|\b(left|right)\s*:|text-align:\s*(left|right)", css);
    }

    private static string Between(string html, string start, string end)
    {
        var from = html.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, $"no {start}");
        var to = html.IndexOf(end, from, StringComparison.Ordinal);
        return to < 0 ? html[from..] : html[from..to];
    }

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
            httpContext.Request.Path = "/Warehouse/CycleCounts";
            services.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;

            var engine = services.GetRequiredService<IRazorViewEngine>();
            var found = engine.GetView(executingFilePath: null, viewPath: viewPath, isMainPage: mainPage);
            if (!found.Success) throw new InvalidOperationException($"Could not load {viewPath}.");
            var actionContext = new ActionContext(httpContext, new RouteData { Values = { ["controller"] = "CycleCounts", ["action"] = "Index" } },
                new ActionDescriptor(), new ModelStateDictionary());
            var viewData = new ViewDataDictionary(services.GetRequiredService<IModelMetadataProvider>(), actionContext.ModelState) { ["CountId"] = CountId };
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
