using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Diten.Web.Models.DataTable;
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
using Xunit.Abstractions;

namespace Diten.Web.Tests.Lists;

/// <summary>
/// The list shell renders EXACTLY what the golden references drew by hand (BL-440 package 1, 2026-09-23).
///
/// <para><b>The measured problem.</b> 138 list screens copy the table card: the card, the placeholder, the
/// <c>&lt;table data-dt-standard="v2"&gt;</c>, the selection column and the actions column are hand-written on
/// every screen. 26 carry a selection column, 6 the shared placeholder; the Users screen binds bulk selection in
/// JS into a column its markup never draws, so its bulk bar can never appear. The cure is a component:
/// <c>_ListShell.cshtml</c> fed by <see cref="DataTableListShellViewModel"/>, the same pattern as the bulk bar.</para>
///
/// <para><b>What this test is.</b> It is the claim "same HTML" itself. BEFORE the component existed, the two
/// golden <c>_DataTable.cshtml</c> partials were rendered for real through the Razor engine and their normalized
/// output was frozen into <see cref="SlimBefore"/> and <see cref="CompactBefore"/> (captured 2026-09-23 at
/// integration/2026-09-21-test = 164551ac3, culture "en"). AFTER, the same two partials — now describing a
/// model and calling the shell — must produce that exact string. Nothing is compared against a copy of the
/// shell; the production views and the production component are what is rendered.</para>
///
/// <para>Setting <c>HasSelection = false</c> on a golden partial, or reordering a column in the shell, turns
/// <see cref="The_golden_partial_renders_the_same_HTML_through_the_shell_as_it_did_by_hand"/> red.</para>
/// </summary>
public sealed class ListShellRenderEqualityTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string SlimPartial = "/Views/DevEnablement/GoldenReferenceSlim/_DataTable.cshtml";
    private const string CompactPartial = "/Views/DevEnablement/GoldenReferenceCompact/_DataTable.cshtml";
    private const string Shell = "/Views/Shared/Components/DataTable/_ListShell.cshtml";

    /// <summary>GoldenReferenceSlim/_DataTable.cshtml rendered by hand-written markup, before the component.</summary>
    private const string SlimBefore =
        """
        <div class="card"><div id="skeleton-loader" class="dt-skeleton p-4" data-table-skeleton aria-hidden="true"><div class="dt-skeleton-toolbar"><span class="shimmer skeleton-row dt-skeleton-search"></span><span class="shimmer skeleton-row dt-skeleton-button"></span><span class="shimmer skeleton-row dt-skeleton-button"></span></div><div class="shimmer skeleton-row dt-skeleton-head"></div><div class="dt-skeleton-row"><span class="shimmer skeleton-row dt-skeleton-cell dt-skeleton-cell--sm"></span><span class="shimmer skeleton-row dt-skeleton-cell dt-skeleton-cell--lg"></span><span class="shimmer skeleton-row dt-skeleton-cell"></span><span class="shimmer skeleton-row dt-skeleton-cell dt-skeleton-cell--sm"></span></div><div class="dt-skeleton-row"><span class="shimmer skeleton-row dt-skeleton-cell dt-skeleton-cell--sm"></span><span class="shimmer skeleton-row dt-skeleton-cell dt-skeleton-cell--lg"></span><span class="shimmer skeleton-row dt-skeleton-cell"></span><span class="shimmer skeleton-row dt-skeleton-cell dt-skeleton-cell--sm"></span></div><div class="dt-skeleton-row"><span class="shimmer skeleton-row dt-skeleton-cell dt-skeleton-cell--sm"></span><span class="shimmer skeleton-row dt-skeleton-cell dt-skeleton-cell--lg"></span><span class="shimmer skeleton-row dt-skeleton-cell"></span><span class="shimmer skeleton-row dt-skeleton-cell dt-skeleton-cell--sm"></span></div><div class="dt-skeleton-row"><span class="shimmer skeleton-row dt-skeleton-cell dt-skeleton-cell--sm"></span><span class="shimmer skeleton-row dt-skeleton-cell dt-skeleton-cell--lg"></span><span class="shimmer skeleton-row dt-skeleton-cell"></span><span class="shimmer skeleton-row dt-skeleton-cell dt-skeleton-cell--sm"></span></div><div class="dt-skeleton-row"><span class="shimmer skeleton-row dt-skeleton-cell dt-skeleton-cell--sm"></span><span class="shimmer skeleton-row dt-skeleton-cell dt-skeleton-cell--lg"></span><span class="shimmer skeleton-row dt-skeleton-cell"></span><span class="shimmer skeleton-row dt-skeleton-cell dt-skeleton-cell--sm"></span></div><div class="dt-skeleton-row"><span class="shimmer skeleton-row dt-skeleton-cell dt-skeleton-cell--sm"></span><span class="shimmer skeleton-row dt-skeleton-cell dt-skeleton-cell--lg"></span><span class="shimmer skeleton-row dt-skeleton-cell"></span><span class="shimmer skeleton-row dt-skeleton-cell dt-skeleton-cell--sm"></span></div><div class="dt-skeleton-footer"><span class="shimmer skeleton-row dt-skeleton-info"></span><span class="shimmer skeleton-row dt-skeleton-pager"></span></div></div><div class="card-datatable table-responsive"><table id="dt-goldenreferenceslim" data-dt-standard="v2" data-dt-data-mode="client" class="datatables-goldenreferenceslim table border-top"><thead><tr><th></th><th class="cell-fit"><input type="checkbox" class="dt-checkboxes-select-all form-check-input"></th><th>Code</th><th class="all">Name</th><th>Reference Type</th><th>Priority</th><th>Status</th><th class="cell-fit text-end pe-3 all">Actions</th></tr></thead></table></div></div>
        """;

    /// <summary>GoldenReferenceCompact/_DataTable.cshtml rendered by hand-written markup, before the component.</summary>
    private const string CompactBefore =
        """
        <div class="card"><div id="skeleton-loader" class="dt-skeleton p-4" data-table-skeleton aria-hidden="true"><div class="dt-skeleton-toolbar"><span class="shimmer skeleton-row dt-skeleton-search"></span><span class="shimmer skeleton-row dt-skeleton-button"></span><span class="shimmer skeleton-row dt-skeleton-button"></span></div><div class="shimmer skeleton-row dt-skeleton-head"></div><div class="dt-skeleton-row"><span class="shimmer skeleton-row dt-skeleton-cell dt-skeleton-cell--sm"></span><span class="shimmer skeleton-row dt-skeleton-cell dt-skeleton-cell--lg"></span><span class="shimmer skeleton-row dt-skeleton-cell"></span><span class="shimmer skeleton-row dt-skeleton-cell dt-skeleton-cell--sm"></span></div><div class="dt-skeleton-row"><span class="shimmer skeleton-row dt-skeleton-cell dt-skeleton-cell--sm"></span><span class="shimmer skeleton-row dt-skeleton-cell dt-skeleton-cell--lg"></span><span class="shimmer skeleton-row dt-skeleton-cell"></span><span class="shimmer skeleton-row dt-skeleton-cell dt-skeleton-cell--sm"></span></div><div class="dt-skeleton-row"><span class="shimmer skeleton-row dt-skeleton-cell dt-skeleton-cell--sm"></span><span class="shimmer skeleton-row dt-skeleton-cell dt-skeleton-cell--lg"></span><span class="shimmer skeleton-row dt-skeleton-cell"></span><span class="shimmer skeleton-row dt-skeleton-cell dt-skeleton-cell--sm"></span></div><div class="dt-skeleton-row"><span class="shimmer skeleton-row dt-skeleton-cell dt-skeleton-cell--sm"></span><span class="shimmer skeleton-row dt-skeleton-cell dt-skeleton-cell--lg"></span><span class="shimmer skeleton-row dt-skeleton-cell"></span><span class="shimmer skeleton-row dt-skeleton-cell dt-skeleton-cell--sm"></span></div><div class="dt-skeleton-row"><span class="shimmer skeleton-row dt-skeleton-cell dt-skeleton-cell--sm"></span><span class="shimmer skeleton-row dt-skeleton-cell dt-skeleton-cell--lg"></span><span class="shimmer skeleton-row dt-skeleton-cell"></span><span class="shimmer skeleton-row dt-skeleton-cell dt-skeleton-cell--sm"></span></div><div class="dt-skeleton-row"><span class="shimmer skeleton-row dt-skeleton-cell dt-skeleton-cell--sm"></span><span class="shimmer skeleton-row dt-skeleton-cell dt-skeleton-cell--lg"></span><span class="shimmer skeleton-row dt-skeleton-cell"></span><span class="shimmer skeleton-row dt-skeleton-cell dt-skeleton-cell--sm"></span></div><div class="dt-skeleton-footer"><span class="shimmer skeleton-row dt-skeleton-info"></span><span class="shimmer skeleton-row dt-skeleton-pager"></span></div></div><div class="card-datatable table-responsive"><table id="dt-goldenreferencecompact" data-dt-standard="v2" data-dt-data-mode="client" class="datatables-goldenreferencecompact table border-top"><thead><tr><th></th><th class="cell-fit"><input type="checkbox" class="dt-checkboxes-select-all form-check-input"></th><th>Code</th><th class="all">Name</th><th>Reference Type</th><th>Category</th><th>Owner</th><th>Version</th><th>Priority</th><th>Status</th><th class="cell-fit text-end pe-3 all">Actions</th></tr></thead></table></div></div>
        """;

    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    public ListShellRenderEqualityTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    // ── the claim ────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(SlimPartial, SlimBefore)]
    [InlineData(CompactPartial, CompactBefore)]
    public async Task The_golden_partial_renders_the_same_HTML_through_the_shell_as_it_did_by_hand(string viewPath, string before)
    {
        var after = Normalize(await RenderAsync(viewPath));

        _output.WriteLine($"{viewPath} — {after.Length} chars after normalisation");

        Assert.Equal(Normalize(before), after);
    }

    // ── non-vacuity: the frozen reference really is the golden shape, not an empty string ──────────────

    [Theory]
    [InlineData(SlimBefore, "dt-goldenreferenceslim", "datatables-goldenreferenceslim")]
    [InlineData(CompactBefore, "dt-goldenreferencecompact", "datatables-goldenreferencecompact")]
    public void The_frozen_reference_carries_the_four_list_markers(string before, string tableId, string slugClass)
    {
        Assert.Contains($"<table id=\"{tableId}\" data-dt-standard=\"v2\" data-dt-data-mode=\"client\" class=\"{slugClass} table border-top\">", before);
        Assert.Contains("id=\"skeleton-loader\"", before);
        Assert.Contains("data-table-skeleton", before);
        Assert.Contains("dt-checkboxes-select-all", before);
        Assert.Contains("<th class=\"cell-fit text-end pe-3 all\">", before);
    }

    // ── the two members a list can never leave out ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(nameof(DataTableListShellViewModel.TableId))]
    [InlineData(nameof(DataTableListShellViewModel.DataMode))]
    public void TableId_and_DataMode_are_REQUIRED_members(string member)
    {
        /*
         * `required` is a compile-time rule, so it cannot be probed by constructing the model without it. The
         * compiler stamps RequiredMemberAttribute on the property; that is what is measured. Dropping the keyword
         * from either property turns this red.
         */
        var property = typeof(DataTableListShellViewModel).GetProperty(member);

        Assert.NotNull(property);
        Assert.True(
            property!.IsDefined(typeof(RequiredMemberAttribute), inherit: false),
            $"{member} is no longer `required` — a list could be declared without it and the shell would render a table with no {member}.");
    }

    // ── data mode is fail-closed ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("clint")]
    [InlineData("Server")]
    [InlineData("")]
    public async Task An_invalid_DataMode_makes_the_shell_throw_at_render(string dataMode)
    {
        var model = new DataTableListShellViewModel { TableId = "dt-probe", DataMode = dataMode };

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => RenderAsync(Shell, model));

        Assert.Contains(dataMode, thrown.Message);
        Assert.Contains("server | client", thrown.Message);
    }

    [Theory]
    [InlineData("server")]
    [InlineData("client")]
    public async Task A_valid_DataMode_renders_and_is_written_to_the_table(string dataMode)
    {
        var model = new DataTableListShellViewModel { TableId = "dt-probe", DataMode = dataMode };

        var html = Normalize(await RenderAsync(Shell, model));

        Assert.Contains($"<table id=\"dt-probe\" data-dt-standard=\"v2\" data-dt-data-mode=\"{dataMode}\" class=\"table border-top\">", html);
    }

    // ── the optional columns really are optional ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Without_selection_and_without_actions_the_shell_draws_only_the_control_column_and_the_page_columns()
    {
        var model = new DataTableListShellViewModel
        {
            TableId = "dt-probe",
            DataMode = "server",
            Columns = [new DataTableColumnViewModel { Header = "Only" }]
        };

        var html = Normalize(await RenderAsync(Shell, model));

        Assert.Contains("<thead><tr><th></th><th>Only</th></tr></thead>", html);
        Assert.DoesNotContain("dt-checkboxes-select-all", html);
        Assert.DoesNotContain("text-end pe-3 all", html);
    }

    // ── rendering the view for real ──────────────────────────────────────────────────────────────────────

    private static string Normalize(string html)
    {
        var collapsed = Regex.Replace(html, "\\s+", " ");
        collapsed = Regex.Replace(collapsed, ">\\s+<", "><");
        return collapsed.Trim();
    }

    private async Task<string> RenderAsync(string viewPath, object? model = null)
    {
        // The frozen references were captured in "en"; the same culture makes the headers comparable.
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");

        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        var engine = services.GetRequiredService<IRazorViewEngine>();
        var found = engine.GetView(executingFilePath: null, viewPath: viewPath, isMainPage: false);
        if (!found.Success)
        {
            throw new InvalidOperationException(
                $"Could not load {viewPath}. Searched: {string.Join(", ", found.SearchedLocations ?? [])}");
        }

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
            var viewDataType = typeof(ViewDataDictionary<>).MakeGenericType(model.GetType());
            viewData = (ViewDataDictionary)Activator.CreateInstance(viewDataType, metadata, actionContext.ModelState)!;
            viewData.Model = model;
        }

        var tempData = new TempDataDictionary(httpContext, services.GetRequiredService<ITempDataProvider>());

        using var writer = new StringWriter();
        var viewContext = new ViewContext(actionContext, found.View, viewData, tempData, writer, new HtmlHelperOptions());
        await found.View.RenderAsync(viewContext);
        return writer.ToString();
    }
}
