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

    // The controllers, the JS folder and the Razor folder of each LIST page (FU01 pack §5/§11, FU02 pack §5/§11), with
    // the manifest code of that list's toolbar "New" button.
    private static readonly (string Controller, string JsFolder, string ViewFolder, string PageCode, string ToolbarNew)[] Screens =
    [
        ("StockLedgerController.cs", "StockLedger", "StockLedger", "STOCKLEDGER", "NEWMOVEMENT"),
        ("StockMovementsController.cs", "StockMovements", "StockMovements", "STOCKMOVEMENTS", "NEWMOVEMENT"),
        ("LocationTreeController.cs", "LocationTree", "LocationTree", "LOCATIONTREE", "NEWLOCATION"),
        ("LocationsController.cs", "Locations", "Locations", "LOCATIONS", "NEWLOCATION")
    ];

    // UI-2: the form / receipt pages (pack §14) — reached from a list's button, they render no manifest action of their own.
    private static readonly string[] FormPages = ["STOCKMOVEMENTCREATE", "STOCKMOVEMENTEDIT", "STOCKMOVEMENTDETAILS", "STOCKLEDGERCREATE"];

    // JS / Razor action key → manifest ActionCode (FU01 / FU02 pack §14 naming). The three status buttons are ONE
    // manifest action (CHANGELOCATIONSTATUS — one permission, one dangerous lifecycle operation).
    private static readonly Dictionary<string, string> ActionCodeOfKey = new(StringComparer.Ordinal)
    {
        ["history"] = "ROWHISTORY",
        ["reversal"] = "REVERSAL",
        ["movement"] = "ROWMOVEMENT",
        ["correct"] = "CORRECTION",
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
        foreach (var (controller, _, _, _, _) in Screens)
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
        foreach (var (_, jsFolder, viewFolder, pageCode, toolbarNew) in Screens)
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
            if (hasToolbarNew) expected.Add(toolbarNew);

            var page = Manifest.Pages.Single(p => p.PageCode == pageCode);
            Assert.Equal(expected.OrderBy(c => c), page.Actions.Select(a => a.ActionCode).OrderBy(c => c));
            Assert.All(page.Actions, a =>
            {
                var toolbar = a.ActionCode == toolbarNew;
                Assert.True(a.IsToolbarAction == toolbar && a.IsRowAction == !toolbar, $"{pageCode}.{a.ActionCode}: wrong placement");
                Assert.Equal(toolbar ? "Toolbar" : "RowAction", a.ActionType);
            });
        }

        // The form / receipt pages declare no action, and their views render no action key either (the other direction).
        Assert.All(FormPages, code => Assert.Empty(Manifest.Pages.Single(p => p.PageCode == code).Actions));
        var formViews = new[] { "Create.cshtml", "Edit.cshtml", "Details.cshtml", "_Form.cshtml" }
            .Select(f => Path.Combine(WebRoot, "Views", "Inventory", "StockMovements", f))
            .Append(Path.Combine(WebRoot, "Views", "Inventory", "StockLedger", "Create.cshtml"));
        Assert.All(formViews, f => Assert.DoesNotMatch(@"data-(location-)?action=""", File.ReadAllText(f)));
        var formScript = File.ReadAllText(Path.Combine(WebRoot, "wwwroot", "assets", "js", "Inventory", "StockMovements", "form.js"));
        Assert.DoesNotMatch(@"\{\s*key:\s*'\w+'", formScript);
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
        // UI-2 KD-1: "Correct" posts a REVERSAL first — as dangerous as Reversal itself.
        Assert.True(actions.Single(a => a.ActionCode == "CORRECTION").IsDangerous);
        string[] dangerous = ["REVERSAL", "CHANGELOCATIONSTATUS", "CORRECTION"];
        Assert.All(actions.Where(a => !dangerous.Contains(a.ActionCode)), a => Assert.False(a.IsDangerous));

        // UI-2 (FU01 pack §14): the movement pages and buttons gate on movements.create; the form pages hang under the
        // list they are reached from.
        Assert.All(actions.Where(a => a.ActionCode is "NEWMOVEMENT" or "ROWMOVEMENT" or "CORRECTION" or "REVERSAL"),
            a => Assert.Equal(InventoryPermissions.CreateMovements, a.PermissionKey));
        Assert.Equal(["STOCKLEDGER", "STOCKMOVEMENTS"], manifest.Pages.Where(p => p.Actions.Any(a => a.ActionCode == "NEWMOVEMENT")).Select(p => p.PageCode).OrderBy(c => c));
        foreach (var (code, parent) in new[] { ("STOCKMOVEMENTCREATE", "STOCKMOVEMENTS"), ("STOCKMOVEMENTEDIT", "STOCKMOVEMENTS"), ("STOCKMOVEMENTDETAILS", "STOCKMOVEMENTS"), ("STOCKLEDGERCREATE", "STOCKLEDGER") })
        {
            var page = manifest.Pages.Single(p => p.PageCode == code);
            Assert.Equal(parent, page.ParentPageCode);
            Assert.False(page.IsNavigationVisible);
            Assert.Equal(InventoryPermissions.CreateMovements, page.RequiredPermission);
        }

        // The location pages gate on the location permissions; "View in stock list" on the stock list's own gate.
        Assert.All(manifest.Pages.Where(p => p.PageCode.StartsWith("LOCATION", StringComparison.Ordinal)),
            p => Assert.Equal(InventoryPermissions.LocationsRead, p.RequiredPermission));
        Assert.All(actions.Where(a => a.ActionCode is "NEWLOCATION" or "EDITLOCATION" or "ADDCHILDLOCATION" or "CHANGELOCATIONSTATUS" or "BULKBINS"),
            a => Assert.Equal(InventoryPermissions.LocationsManage, a.PermissionKey));
        Assert.All(actions.Where(a => a.ActionCode == "LOCATIONHISTORY"), a => Assert.Equal(InventoryPermissions.LocationsRead, a.PermissionKey));
        Assert.All(actions.Where(a => a.ActionCode == "VIEWLOCATIONSTOCK"), a => Assert.Equal(InventoryPermissions.Read, a.PermissionKey));
    }

    /// <summary>
    /// FU-RSV-01 (WP-0173-RSV1) — every key an Inventory controller enforces with <c>[HasPermission]</c> must reach the
    /// permission catalog, i.e. be declared by the manifest as a page, an action OR an API-only permission (standard
    /// §2d). Measured by reflection over the shipping API assembly: a key with no UI (reserve / release) that is dropped
    /// from <c>ApiPermissions</c> makes this red — the G1 live-run failure (403 for every role) caught at build time.
    /// </summary>
    [Fact]
    public void Every_enforced_permission_is_reachable_from_the_manifest()
    {
        var enforced = typeof(InventoryLedgerManifestProvider).Assembly.GetTypes()
            .Where(t => typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetCustomAttributes<HasPermissionAttribute>(inherit: true)
                .Concat(t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .SelectMany(m => m.GetCustomAttributes<HasPermissionAttribute>(inherit: true))))
            .Select(a => a.Policy!["Permission:".Length..])
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains(InventoryPermissions.ManageReservations, enforced); // the measured gap, so the scan is not vacuous

        var manifest = new InventoryLedgerManifestProvider().GetManifest();
        var reachable = manifest.Pages.Select(p => p.RequiredPermission)
            .Concat(manifest.Pages.SelectMany(p => p.Actions).Select(a => a.PermissionKey))
            .Concat((manifest.ApiPermissions ?? []).Select(p => p.PermissionKey))
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(enforced, key => Assert.True(reachable.Contains(key), $"[HasPermission({key})] is enforced but the manifest never declares it"));
    }

    [Fact]
    public void Api_permissions_are_real_tenant_keys_without_a_ui()
    {
        var manifest = new InventoryLedgerManifestProvider().GetManifest();
        var api = Assert.Single(manifest.ApiPermissions ?? []);
        Assert.Equal(InventoryPermissions.ManageReservations, api.PermissionKey);
        Assert.Equal("Tenant", api.Scope);
        Assert.False(string.IsNullOrWhiteSpace(api.DisplayName));
        // API-only means no UI: the key is neither a page gate nor a button (standard §2d — a UI key stays a page/action).
        Assert.DoesNotContain(manifest.Pages, p => p.RequiredPermission == api.PermissionKey);
        Assert.DoesNotContain(manifest.Pages.SelectMany(p => p.Actions), a => a.PermissionKey == api.PermissionKey);
    }

    private sealed record ModuleManifestDocumentView(Diten.BuildingBlocks.ModuleRegistration.Abstractions.ModuleManifestDocument Document)
    {
        public IReadOnlyList<Diten.BuildingBlocks.ModuleRegistration.Abstractions.ModuleManifestPage> Pages => Document.Pages;
    }
}
