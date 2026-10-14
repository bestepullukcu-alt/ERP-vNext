using System.Text.RegularExpressions;
using Diten.Web.Models.Traceability;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Diten.Web.Tests.Traceability;

/// <summary>
/// MOD-0174 P3 (pack §9, §11.1, §16 P3) on the REAL Razor views:
/// <list type="bullet">
/// <item>UAS-001 on every page: without the page key only <c>_AccessDenied</c> is drawn — no heading, filter, table,
/// skeleton or button, never "403" / "Forbidden" text, no redirect;</item>
/// <item>the shell: every page sets <c>Layout = "_LayoutTenantShell"</c> explicitly (grep), the lists are server-mode
/// DataTables without a selection column;</item>
/// <item>WP-0174-P4: the 8th page — Elle Girilmiş Lotları Aktarma (/LotMigration, trace.lots.migrate) — is drawn: "grep 8".</item>
/// </list>
/// </summary>
public sealed class TracePagesSurfaceTests : IClassFixture<WebApplicationFactory<Program>>
{
    public sealed record Page(string View, string Key, string[] Skeleton, Func<object?> Model);

    public static readonly Page[] Pages =
    [
        new("/Views/Traceability/Lots/Index.cshtml", "trace.lots.read", ["dt-lots", "inlineFilterHost", "<h5 class=\"mb-0\">", "skeleton-loader", "lots-antiforgery"], () => null),
        new("/Views/Traceability/Lots/Details.cshtml", "trace.lots.read", ["lot-detail", "lot-history", "data-trace-action", "<h5 class=\"mb-0"], () => LotPageViewModel.From(Guid.NewGuid().ToString())),
        new("/Views/Traceability/Lots/Create.cshtml", "trace.lots.create", ["formLot", "btnLotSubmit", "lotIdempotencyKey", "<h5 class=\"mb-0\">"], () => LotFormPageViewModel.ForCreate()),
        new("/Views/Traceability/Lots/Edit.cshtml", "trace.lots.update", ["formLot", "btnLotSubmit", "lotIfMatch", "<h5 class=\"mb-0\">"], () => LotFormPageViewModel.ForEdit(Guid.NewGuid().ToString())),
        new("/Views/Traceability/Serials/Index.cshtml", "trace.serials.read", ["dt-serials", "inlineFilterHost", "<h5 class=\"mb-0\">", "skeleton-loader"], () => LotPageViewModel.From(null)),
        new("/Views/Traceability/LotMovements/Index.cshtml", "trace.lots.read", ["dt-lotmovements", "lotMovementsLot", "<h5 class=\"mb-0\">", "modalLotReceipt"], () => LotPageViewModel.From(null)),
        new("/Views/Traceability/LotTrace/Index.cshtml", "trace.lots.read", ["lottrace", "lotTraceLot", "<h5 class=\"mb-0\">"], () => LotPageViewModel.From(null)),
        new("/Views/Traceability/LotMigration/Index.cshtml", "trace.lots.migrate", ["dt-lotmigration", "migration-kpi", "btnConfirmMigration", "<h5 class=\"mb-0\">", "migration-antiforgery"], () => null)
    ];

    public static TheoryData<int> PageIndexes => new() { 0, 1, 2, 3, 4, 5, 6, 7 };

    private readonly WebApplicationFactory<Program> _factory;

    public TracePagesSurfaceTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Theory]
    [MemberData(nameof(PageIndexes))]
    public async Task UAS_001_without_the_page_key_only_the_access_explanation_is_drawn(int index)
    {
        var page = Pages[index];
        // Every OTHER trace / inventory key, so it is the page key that is missing — not everything.
        var others = TraceActions.KeyOf.Values.Append("inventory.stock.read").Append("inventory.movements.create").Distinct()
            .Where(k => k != page.Key && !(page.Key == "trace.lots.update" && k == "trace.lots.update-released")).ToArray();
        var html = await TraceViewRenderer.RenderAsync(_factory, page.View, page.Model(), others);

        Assert.Contains("diten-access-denied", html, StringComparison.Ordinal);
        Assert.All(page.Skeleton, s => Assert.DoesNotContain(s, html, StringComparison.Ordinal));
        Assert.DoesNotContain("Forbidden", html, StringComparison.Ordinal);
        Assert.DoesNotMatch("(?<![0-9a-fA-F-])403(?![0-9a-fA-F])", html); // a status code as text, not a digit run inside an id
    }

    [Theory]
    [MemberData(nameof(PageIndexes))]
    public async Task With_the_page_key_the_page_is_drawn(int index)
    {
        var page = Pages[index];
        var html = await TraceViewRenderer.RenderAsync(_factory, page.View, page.Model(), page.Key, "inventory.stock.read");

        Assert.DoesNotContain("diten-access-denied", html, StringComparison.Ordinal);
        Assert.Contains(page.Skeleton[0], html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_edit_page_also_admits_update_released_ASSUMPTION_0174_08()
    {
        var html = await TraceViewRenderer.RenderAsync(_factory, Pages[3].View, Pages[3].Model(), "trace.lots.update-released");
        Assert.DoesNotContain("diten-access-denied", html, StringComparison.Ordinal);
        Assert.Contains("formLot", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, "dt-lots")]
    [InlineData(4, "dt-serials")]
    [InlineData(5, "dt-lotmovements")]
    public async Task The_lists_are_server_mode_without_selection_bulk_or_export(int index, string table)
    {
        var html = await TraceViewRenderer.RenderAsync(_factory, Pages[index].View, Pages[index].Model(), Pages[index].Key, "inventory.stock.read");
        Assert.Contains($"id=\"{table}\" data-dt-standard=\"v2\" data-dt-data-mode=\"server\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("dt-checkboxes-select-all", html, StringComparison.Ordinal);
        Assert.DoesNotContain("bulkActionBar", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_migration_list_is_client_mode_without_selection_bulk_or_export()
    {
        // Decisions are page state until the one commit (ASSUMPTION-0174-11): every group is loaded once, the table is client data.
        var html = await TraceViewRenderer.RenderAsync(_factory, Pages[7].View, null, "trace.lots.migrate");
        Assert.Contains("id=\"dt-lotmigration\" data-dt-standard=\"v2\" data-dt-data-mode=\"client\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("dt-checkboxes-select-all", html, StringComparison.Ordinal);
        Assert.DoesNotContain("bulkActionBar", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_page_sets_the_tenant_shell_explicitly_grep_8()
    {
        var root = Path.Combine(TraceViewRenderer.WebRoot, "Views", "Traceability");
        var layouts = Directory.EnumerateFiles(root, "*.cshtml", SearchOption.AllDirectories)
            .Where(f => File.ReadAllText(f).Contains("Layout = \"_LayoutTenantShell\";", StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(
            ["LotMigration/Index.cshtml", "LotMovements/Index.cshtml", "LotTrace/Index.cshtml", "Lots/Create.cshtml", "Lots/Details.cshtml", "Lots/Edit.cshtml",
             "Lots/Index.cshtml", "Serials/Index.cshtml"],
            layouts);
        // Partials set no layout (pack §9).
        Assert.All(Directory.EnumerateFiles(root, "_*.cshtml", SearchOption.AllDirectories),
            f => Assert.DoesNotContain("Layout =", File.ReadAllText(f), StringComparison.Ordinal));
    }

    [Fact]
    public void The_eight_drawn_routes_match_the_LOT_SERIAL_TRACKING_manifest_pages()
    {
        var manifest = File.ReadAllText(Path.Combine(TraceViewRenderer.RepoRoot(), "services", "Diten.InventoryService", "src",
            "Diten.InventoryService.Api", "ModuleRegistration", "LotSerialTrackingManifestProvider.cs"));
        var routes = Regex.Matches(manifest, @"RoutePath: ""(?<r>/[^""]+)""").Select(m => m.Groups["r"].Value).ToList();
        Assert.Equal(8, routes.Count);

        var drawn = new Dictionary<string, string>
        {
            ["/Lots"] = "Lots/Index.cshtml", ["/Lots/Details"] = "Lots/Details.cshtml", ["/Lots/Create"] = "Lots/Create.cshtml",
            ["/Lots/Edit"] = "Lots/Edit.cshtml", ["/LotMovements"] = "LotMovements/Index.cshtml", ["/Serials"] = "Serials/Index.cshtml",
            ["/LotTrace"] = "LotTrace/Index.cshtml", ["/LotMigration"] = "LotMigration/Index.cshtml"
        };
        Assert.Equal(routes.OrderBy(r => r), drawn.Keys.OrderBy(r => r));
        Assert.All(drawn.Values, v => Assert.True(File.Exists(Path.Combine(TraceViewRenderer.WebRoot, "Views", "Traceability", v)), v));
        // Each route's controller exists with its [Route] (the page answers at the manifest's address).
        foreach (var (controller, route) in new[] { ("LotsController", "Lots"), ("SerialsController", "Serials"), ("LotMovementsController", "LotMovements"),
                     ("LotTraceController", "LotTrace"), ("LotMigrationController", "LotMigration") })
        {
            var source = File.ReadAllText(Path.Combine(TraceViewRenderer.WebRoot, "Controllers", controller + ".cs"));
            Assert.Contains($"[Route(\"{route}\")]", source, StringComparison.Ordinal);
        }
    }
}
