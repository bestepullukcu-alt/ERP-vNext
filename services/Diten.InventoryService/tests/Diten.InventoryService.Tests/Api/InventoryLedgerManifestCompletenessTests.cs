using System.Reflection;
using System.Text.RegularExpressions;
using Diten.InventoryService.Api.ModuleRegistration;
using Diten.InventoryService.Infrastructure.Authorization;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// MOD-0173-FU01 + FU02 manifest completeness (module-self-registration-standard §3) — BOTH directions, measured against
/// the shipping code rather than a list typed here:
/// <list type="number">
/// <item>every page / action permission is a real <see cref="InventoryPermissions"/> constant;</item>
/// <item>every frontend view route (the FU01 / FU02 MVC controllers' GET actions that return a View) is a manifest page,
/// and every manifest page is such a route;</item>
/// <item>every row / toolbar / detail button the screens render (JS action objects, Razor <c>data-location-action</c>
/// buttons, the list toolbar's "New") is a manifest action on the page that shows it, and every manifest action is
/// such a button;</item>
/// <item>codes and routes are unique; the nav-visible top pages are exactly Stock Ledger and Locations.</item>
/// </list>
/// Not covered here: the Platform reconcile-state (push A → push B with a removal → DB == B). That is Platform's
/// reconcile, exercised live on restart (WP-0173-UI1 / WP-0173-LOC2 D6).
/// </summary>
public sealed class InventoryLedgerManifestCompletenessTests
{
    private static readonly string WebRoot = Path.Combine(RepoPaths.Root, "frontend", "Diten.Web");

    // The controllers, the JS folder and the Razor folder of each page (FU01 pack §5/§11, FU02 pack §5/§11).
    private static readonly (string Controller, string JsFolder, string ViewFolder, string PageCode)[] Screens =
    [
        ("StockLedgerController.cs", "StockLedger", "StockLedger", "STOCKLEDGER"),
        ("StockMovementsController.cs", "StockMovements", "StockMovements", "STOCKMOVEMENTS"),
        ("LocationTreeController.cs", "LocationTree", "LocationTree", "LOCATIONTREE"),
        ("LocationsController.cs", "Locations", "Locations", "LOCATIONS")
    ];

    // JS / Razor action key → manifest ActionCode (FU01 / FU02 pack §14 naming). The three status buttons are ONE
    // manifest action (CHANGELOCATIONSTATUS — one permission, one dangerous lifecycle operation).
    private static readonly Dictionary<string, string> ActionCodeOfKey = new(StringComparer.Ordinal)
    {
        ["history"] = "ROWHISTORY",
        ["reversal"] = "REVERSAL",
        ["newLocation"] = "NEWLOCATION",
        ["edit"] = "EDITLOCATION",
        ["addChild"] = "ADDCHILDLOCATION",
        ["activate"] = "CHANGELOCATIONSTATUS",
        ["deactivate"] = "CHANGELOCATIONSTATUS",
        ["archive"] = "CHANGELOCATIONSTATUS",
        ["bulkBins"] = "BULKBINS",
        ["locationHistory"] = "LOCATIONHISTORY",
        ["viewStock"] = "VIEWLOCATIONSTOCK"
    };

    private const string ToolbarNew = "NEWLOCATION";

    private static readonly ModuleManifestDocumentView Manifest = new(new InventoryLedgerManifestProvider().GetManifest());

    [Fact]
    public void Every_page_and_action_permission_is_a_real_InventoryPermissions_constant()
    {
        var real = typeof(InventoryPermissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(Manifest.Pages, p => Assert.Contains(p.RequiredPermission, real));
        Assert.All(Manifest.Pages.SelectMany(p => p.Actions), a => Assert.Contains(a.PermissionKey, real));
    }

    [Fact]
    public void Every_frontend_view_route_is_a_manifest_page_and_nothing_else_is()
    {
        var routes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (controller, _, _, _) in Screens)
        {
            var source = File.ReadAllText(Path.Combine(WebRoot, "Controllers", controller));
            var prefix = Regex.Match(source, @"\[Route\(""(?<prefix>[^""]+)""\)\]").Groups["prefix"].Value;
            Assert.False(string.IsNullOrEmpty(prefix), $"{controller}: no [Route] prefix");

            var viewActions = Regex.Matches(source,
                @"\[HttpGet\(""(?<segment>[^""]*)""\)\]\s*public\s+IActionResult\s+\w+\([^)]*\)\s*=>\s*View\(");
            // Every View( in the controller must be one of those one-line GET view actions, or a route would hide.
            Assert.Equal(Regex.Matches(source, @"\bView\(").Count, viewActions.Count);

            foreach (Match action in viewActions)
            {
                var segment = action.Groups["segment"].Value;
                routes.Add("/" + prefix + (segment.Length == 0 ? string.Empty : "/" + segment));
            }
        }

        Assert.Equal(routes.OrderBy(r => r), Manifest.Pages.Select(p => p.RoutePath).OrderBy(r => r));
    }

    [Fact]
    public void Every_rendered_button_is_a_manifest_action_on_its_page_and_nothing_else_is()
    {
        foreach (var (_, jsFolder, viewFolder, pageCode) in Screens)
        {
            var script = File.ReadAllText(Path.Combine(WebRoot, "wwwroot", "assets", "js", "Inventory", jsFolder, "index.js"));
            // An action object opens with its key (`{ key: 'history', … }`); a filter field opens with its id.
            var keys = Regex.Matches(script, @"\{\s*key:\s*'(?<key>\w+)'").Select(m => m.Groups["key"].Value).ToList();
            // Razor buttons the page draws itself (L-1 toolbar "New site", empty-state "Add the first site").
            keys.AddRange(Directory.EnumerateFiles(Path.Combine(WebRoot, "Views", "Inventory", viewFolder), "*.cshtml")
                .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"data-location-action=""(?<key>\w+)""").Select(m => m.Groups["key"].Value)));
            Assert.All(keys, k => Assert.True(ActionCodeOfKey.ContainsKey(k), $"{jsFolder} renders action '{k}' that has no manifest mapping"));

            var expected = keys.Select(k => ActionCodeOfKey[k]).ToHashSet(StringComparer.Ordinal);

            // The list toolbar's "New" comes from the factory: an empty add-new text means no button.
            var addNew = Regex.Match(script, @"addNewText:\s*(?<value>[^,\n]+)").Groups["value"].Value.Trim();
            var hasToolbarNew = addNew.Length > 0 && addNew != "''";
            if (hasToolbarNew) expected.Add(ToolbarNew);

            var page = Manifest.Pages.Single(p => p.PageCode == pageCode);
            Assert.Equal(expected.OrderBy(c => c), page.Actions.Select(a => a.ActionCode).OrderBy(c => c));
            Assert.All(page.Actions, a =>
            {
                var toolbar = a.ActionCode == ToolbarNew;
                Assert.True(a.IsToolbarAction == toolbar && a.IsRowAction == !toolbar, $"{pageCode}.{a.ActionCode}: wrong placement");
                Assert.Equal(toolbar ? "Toolbar" : "RowAction", a.ActionType);
            });
        }
    }

    [Fact]
    public void Codes_and_routes_are_unique_and_the_nav_visible_pages_are_stock_ledger_and_locations()
    {
        var manifest = new InventoryLedgerManifestProvider().GetManifest();
        Assert.Equal("INVENTORY-LEDGER", manifest.ModuleCode);
        Assert.Equal("SUPPLYCHAINEXECUTION", manifest.Domain);
        Assert.True(manifest.IsTenantAssignable);

        Assert.Equal(manifest.Pages.Count, manifest.Pages.Select(p => p.PageCode).Distinct().Count());
        Assert.Equal(manifest.Pages.Count, manifest.Pages.Select(p => p.RoutePath).Distinct().Count());
        // Action codes are unique PER PAGE — Platform's catalog key is (tenant, page, action code)
        // (ux_platform_module_page_actions_tenant_page_action); FU02 pack §14 puts NEWLOCATION, EDITLOCATION … on both
        // location pages because both render those buttons.
        Assert.All(manifest.Pages, p => Assert.Equal(p.Actions.Count, p.Actions.Select(a => a.ActionCode).Distinct().Count()));

        // UI-1 asserted exactly ONE nav-visible page (Stock Ledger). FU02 (pack §14) adds the location master to the
        // same module with its own nav entry, so the measured set is now two top pages, each with its own children.
        var top = manifest.Pages.Where(p => p.IsNavigationVisible).ToList();
        Assert.Equal(["LOCATIONTREE", "STOCKLEDGER"], top.Select(p => p.PageCode).OrderBy(c => c));
        Assert.All(top, p => Assert.Null(p.ParentPageCode));
        Assert.Equal("STOCKLEDGER", manifest.Pages.Single(p => p.PageCode == "STOCKMOVEMENTS").ParentPageCode);
        Assert.Equal("LOCATIONTREE", manifest.Pages.Single(p => p.PageCode == "LOCATIONS").ParentPageCode);
        Assert.False(manifest.Pages.Single(p => p.PageCode == "LOCATIONS").IsNavigationVisible);

        // Tenant routes, never /Platform/… (the permission scope is derived from the route — standard §2c).
        Assert.All(manifest.Pages, p => Assert.False(p.RoutePath.StartsWith("/Platform", StringComparison.OrdinalIgnoreCase)));

        var actions = manifest.Pages.SelectMany(p => p.Actions).ToList();
        Assert.Single(actions, a => a.ActionCode == "REVERSAL");
        Assert.True(actions.Single(a => a.ActionCode == "REVERSAL").IsDangerous);
        // Archive lives in the status change: dangerous on every page that offers it (FU02 pack §14).
        Assert.All(actions.Where(a => a.ActionCode == "CHANGELOCATIONSTATUS"), a => Assert.True(a.IsDangerous));
        Assert.All(actions.Where(a => a.ActionCode != "REVERSAL" && a.ActionCode != "CHANGELOCATIONSTATUS"), a => Assert.False(a.IsDangerous));

        // The location pages gate on the location permissions; "View in stock list" on the stock list's own gate.
        Assert.All(manifest.Pages.Where(p => p.PageCode.StartsWith("LOCATION", StringComparison.Ordinal)),
            p => Assert.Equal(InventoryPermissions.LocationsRead, p.RequiredPermission));
        Assert.All(actions.Where(a => a.ActionCode is "NEWLOCATION" or "EDITLOCATION" or "ADDCHILDLOCATION" or "CHANGELOCATIONSTATUS" or "BULKBINS"),
            a => Assert.Equal(InventoryPermissions.LocationsManage, a.PermissionKey));
        Assert.All(actions.Where(a => a.ActionCode == "LOCATIONHISTORY"), a => Assert.Equal(InventoryPermissions.LocationsRead, a.PermissionKey));
        Assert.All(actions.Where(a => a.ActionCode == "VIEWLOCATIONSTOCK"), a => Assert.Equal(InventoryPermissions.Read, a.PermissionKey));
    }

    private sealed record ModuleManifestDocumentView(Diten.BuildingBlocks.ModuleRegistration.Abstractions.ModuleManifestDocument Document)
    {
        public IReadOnlyList<Diten.BuildingBlocks.ModuleRegistration.Abstractions.ModuleManifestPage> Pages => Document.Pages;
    }
}
