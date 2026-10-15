using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.Web.Infrastructure;
using Diten.Web.Models.Tabs;
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

namespace Diten.Web.Tests.Lists;

/// <summary>
/// K-TABS (owner decision, 2026-10-15) — the shared tab card, rendered for REAL through the Razor engine: (1) its markup is
/// the Work Center's tab strip class for class (the classes are read from WorkCenterNext/app.js, not copied here); (2) the
/// ARIA of the tabs pattern (tablist / tab / aria-selected / aria-controls, roving tabindex) and, because the text hides
/// below md, aria-label + title on every button; (3) diten-tabs.js in Node: click, → / ← / Home / End, the direction swap in
/// right-to-left, the diten:tab-change event; (4) the evidence flags page's partials rendered: the rep summary table (v2,
/// client) and the L10n payload's date format from the culture (tr + ar).
/// </summary>
public sealed class TabCardWebTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string TabCard = "/Views/Shared/Components/Tabs/_TabCard.cshtml";
    private readonly WebApplicationFactory<Program> _factory;

    public TabCardWebTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static TabCardViewModel Model() => new()
    {
        AriaLabel = "Evidence flags",
        ToolsId = "tc-tools",
        Tabs =
        [
            new TabCardItem { Key = "visits", Id = "tc-visits", Icon = "bx-list-ul", Text = "Visits", Active = true, Controls = "pane-visits" },
            new TabCardItem { Key = "reps", Id = "tc-reps", Icon = "bx-group", Text = "Rep summary", Count = 3, Controls = "pane-reps" }
        ]
    };

    [Fact]
    public async Task T1_the_tab_card_renders_the_work_center_tab_strip_class_for_class()
    {
        var html = Normalize(await RenderAsync(TabCard, Model()));
        Assert.Equal(
            "<div class=\"card mb-3 wcn-tabcard\"><div class=\"card-body p-3 d-flex align-items-center gap-3 flex-wrap\">"
            + "<ul class=\"nav nav-pills gap-2 flex-wrap mb-0 wcn-tabs\" role=\"tablist\" data-diten-tabs aria-label=\"Evidence flags\">"
            + "<li class=\"nav-item\" role=\"presentation\"><button type=\"button\" id=\"tc-visits\" class=\"nav-link border shadow-none wc-tab-compact d-inline-flex align-items-center active\" data-tab-key=\"visits\" role=\"tab\" aria-selected=\"true\" aria-controls=\"pane-visits\" tabindex=\"0\" aria-label=\"Visits\" title=\"Visits\">"
            + "<i class=\"bx bx-list-ul wc-tab-icon me-md-1\" aria-hidden=\"true\"></i><span class=\"d-none d-md-inline\">Visits</span></button></li>"
            + "<li class=\"nav-item\" role=\"presentation\"><button type=\"button\" id=\"tc-reps\" class=\"nav-link border shadow-none wc-tab-compact d-inline-flex align-items-center\" data-tab-key=\"reps\" role=\"tab\" aria-selected=\"false\" aria-controls=\"pane-reps\" tabindex=\"-1\" aria-label=\"Rep summary\" title=\"Rep summary\">"
            + "<i class=\"bx bx-group wc-tab-icon me-md-1\" aria-hidden=\"true\"></i><span class=\"d-none d-md-inline\">Rep summary</span>"
            + "<span class=\"badge rounded-pill bg-danger wcn-tab-count position-absolute top-0 start-100 translate-middle\">3</span></button></li></ul>"
            + "<div class=\"d-flex align-items-center gap-2 ms-auto\" id=\"tc-tools\"></div></div></div>",
            html);

        // the SAME classes the Work Center draws (WorkCenterNext/app.js buildTabs) — read from the production file
        var app = File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "js", "WorkCenterNext", "app.js"));
        var at = app.IndexOf("return `<div class=\"card mb-3 wcn-tabcard\">", StringComparison.Ordinal);
        Assert.True(at > 0, "the Work Center tab card moved");
        var builder = app[(at - 2500)..(at + 600)];
        foreach (var wc in new[]
                 {
                     "card mb-3 wcn-tabcard", "card-body p-3 d-flex align-items-center gap-3 flex-wrap", "nav nav-pills gap-2 flex-wrap mb-0 wcn-tabs\" role=\"tablist\"",
                     "<li class=\"nav-item\" role=\"presentation\">", "nav-link border shadow-none wc-tab-compact d-inline-flex align-items-center",
                     "wc-tab-icon me-md-1\"></i><span class=\"d-none d-md-inline\">", "wcn-tab-count position-absolute top-0 start-100 translate-middle",
                     "d-flex align-items-center gap-2 ms-auto"
                 })
        {
            Assert.Contains(wc, builder);                                                                   // the Work Center has it …
            Assert.Contains(wc.Replace("\"></i>", "\" aria-hidden=\"true\"></i>"), html);                   // … and so does the card
        }

        // no badge for a zero / missing count; no tools area without a tools id
        var plain = Normalize(await RenderAsync(TabCard, new TabCardViewModel { Tabs = [new TabCardItem { Key = "a", Id = "a", Icon = "bx-x", Text = "A", Count = 0, Controls = "p" }] }));
        Assert.DoesNotContain("wcn-tab-count", plain);
        Assert.DoesNotContain("ms-auto", plain);
        Assert.Contains("aria-selected=\"true\"", plain);                                                    // nothing active → the first one is
    }

    [Theory]
    [InlineData("ltr")]
    [InlineData("rtl")]
    public void T2_the_tabs_script_clicks_arrows_home_end_swap_in_rtl_and_tell_the_page(string dir)
    {
        var node = RunNode("process.argv[2] = '" + Script("diten-tabs.js") + "'; process.argv[3] = '" + dir + "'; require('" + Smoke("diten-tabs-smoke.js") + "');");
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement;
        string Active(string step) => r.GetProperty(step).GetProperty("active")[0].GetString()!;
        Assert.Equal(1, r.GetProperty("bound").GetInt32());
        Assert.Equal("tab-b", Active("click"));
        Assert.Equal("false,true,false", r.GetProperty("click").GetProperty("selected").GetString());
        Assert.Equal("-1,0,-1", r.GetProperty("click").GetProperty("tabindex").GetString());             // roving tabindex
        Assert.Equal(["pane-b"], r.GetProperty("click").GetProperty("panels").EnumerateArray().Select(x => x.GetString()));
        var rtl = dir == "rtl";
        Assert.Equal(rtl ? "tab-a" : "tab-c", Active("right"));                                           // → is "previous" in RTL
        Assert.Equal(rtl ? "tab-b" : "tab-a", Active("rightWraps"));
        Assert.Equal(rtl ? "tab-b" : "tab-c", Active("left"));
        Assert.Equal("tab-a", Active("home"));                                                              // Home / End do not swap
        Assert.Equal("tab-c", Active("end"));
        Assert.Equal("tab-c", r.GetProperty("end").GetProperty("focused").GetString());                    // the keyboard moves the focus
        Assert.False(r.GetProperty("otherKeyPrevented").GetBoolean());
        var first = r.GetProperty("events")[0];
        Assert.Equal(("diten:tab-change", "b", "pane-b", true), (first.GetProperty("type").GetString(), first.GetProperty("detail").GetProperty("key").GetString(),
            first.GetProperty("detail").GetProperty("controls").GetString(), first.GetProperty("bubbles").GetBoolean()));
        Assert.Equal(0, r.GetProperty("sameTabEvents").GetInt32());                                         // re-selecting the active tab says nothing
    }

    [Fact]
    public async Task T3_the_evidence_flags_page_uses_the_card_its_rep_table_is_the_client_v2_shell_and_its_dates_follow_the_culture()
    {
        var view = File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "VisitEvidenceFlags", "Index.cshtml"));
        Assert.Contains("<partial name=\"~/Views/Shared/Components/Tabs/_TabCard.cshtml\" model=\"tabCard\" />", view);
        Assert.Contains("Key = \"visits\", Id = \"vf-tab-visits\", Icon = \"bx-list-ul\", Text = Localizer[\"TabVisits\"].Value, Active = true, Controls = \"vf-pane-visits\"", view);
        Assert.Contains("Key = \"reps\", Id = \"vf-tab-reps\", Icon = \"bx-group\", Text = Localizer[\"TabReps\"].Value, Controls = \"vf-pane-reps\"", view);
        Assert.DoesNotContain("nav-tabs", view);
        Assert.DoesNotContain("data-bs-toggle=\"tab\"", view);
        Assert.Contains("~/assets/js/diten-tabs.js", view);

        var reps = Normalize(await RenderAsync("/Views/CRM/VisitEvidenceFlags/_RepSummaryTable.cshtml", null, "tr"));
        var table = Regex.Match(reps, "<table [^>]*>").Value;
        Assert.Equal("<table id=\"dt-vf-reps\" data-dt-standard=\"v2\" data-dt-data-mode=\"client\" class=\"datatables-vf-reps table border-top\">", table);
        Assert.Contains("data-table-skeleton", reps);                                                        // the shaped placeholder
        Assert.DoesNotContain("dt-checkboxes-select-all", reps);                                             // read only

        foreach (var culture in new[] { "tr", "ar", "en" })
        {
            var l10n = await RenderAsync("/Views/CRM/VisitEvidenceFlags/_IndexL10n.cshtml", null, culture);
            var json = Regex.Match(l10n, "<script id=\"visitevidenceflags-l10n\" type=\"application/json\">([\\s\\S]*?)</script>").Groups[1].Value;
            var payload = JsonDocument.Parse(json).RootElement;
            var format = payload.TryGetProperty("DateFormat", out var f) ? f.GetString() : payload.GetProperty("dateFormat").GetString();
            Assert.Equal(DateFieldFormat.Flatpickr(CultureInfo.GetCultureInfo(culture)), format);
        }

        Assert.Matches(@"^[dj]\.m\.Y$", DateFieldFormat.Flatpickr(CultureInfo.GetCultureInfo("tr")));         // 15.10.2026 (the platform's tr pattern: d.MM.yyyy or dd.MM.yyyy)
        Assert.Equal("d/m/Y", DateFieldFormat.FromDotNet("dd/MM/yyyy"));
        Assert.Equal("j.n.y", DateFieldFormat.FromDotNet("d.M.yy"));
        Assert.DoesNotContain("y", DateFieldFormat.Flatpickr(CultureInfo.GetCultureInfo("ar")).Replace("Y", ""));   // a 4-digit year, the culture's order
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────

    private static string Normalize(string html) => Regex.Replace(Regex.Replace(html, "\\s+", " "), ">\\s+<", "><").Trim();

    private async Task<string> RenderAsync(string viewPath, object? model, string culture = "en")
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var engine = services.GetRequiredService<IRazorViewEngine>();
        var found = engine.GetView(executingFilePath: null, viewPath: viewPath, isMainPage: false);
        if (!found.Success) throw new InvalidOperationException($"Could not load {viewPath}.");
        var httpContext = new DefaultHttpContext { RequestServices = services };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
        var metadata = services.GetRequiredService<IModelMetadataProvider>();
        ViewDataDictionary viewData;
        if (model is null)
        {
            viewData = new ViewDataDictionary(metadata, actionContext.ModelState);
        }
        else
        {
            viewData = (ViewDataDictionary)Activator.CreateInstance(typeof(ViewDataDictionary<>).MakeGenericType(model.GetType()), metadata, actionContext.ModelState)!;
            viewData.Model = model;
        }

        var tempData = new TempDataDictionary(httpContext, services.GetRequiredService<ITempDataProvider>());
        using var writer = new StringWriter();
        await found.View.RenderAsync(new ViewContext(actionContext, found.View, viewData, tempData, writer, new HtmlHelperOptions()));
        return writer.ToString();
    }

    private static string Script(string file) => Path.Combine(WebRoot(), "wwwroot", "assets", "js", file).Replace("\\", "/");

    private static string Smoke(string file) => Path.Combine(Path.GetDirectoryName(WebRoot())!, "Diten.Web.Tests", "Js", file).Replace("\\", "/");

    private static string? RunNode(string code)
    {
        var temp = Path.Combine(Path.GetTempPath(), "tc-" + Guid.NewGuid().ToString("N") + ".js");
        File.WriteAllText(temp, code, new UTF8Encoding(false));
        try
        {
            using var process = Process.Start(new ProcessStartInfo("node", "\"" + temp + "\"")
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, StandardOutputEncoding = Encoding.UTF8
            });
            if (process is null) return null;
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(20000);
            Assert.True(process.ExitCode == 0, error);
            return output;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
        finally
        {
            File.Delete(temp);
        }
    }

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }
}
