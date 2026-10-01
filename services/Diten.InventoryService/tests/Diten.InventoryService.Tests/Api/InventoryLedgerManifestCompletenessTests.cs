using System.Reflection;
using System.Text.RegularExpressions;
using Diten.InventoryService.Api.ModuleRegistration;
using Diten.InventoryService.Infrastructure.Authorization;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// MOD-0173-FU01 manifest completeness (module-self-registration-standard §3) — BOTH directions, measured against the
/// shipping code rather than a list typed here:
/// <list type="number">
/// <item>every page / action permission is a real <see cref="InventoryPermissions"/> constant;</item>
/// <item>every frontend view route (the FU01 MVC controllers' GET actions that return a View) is a manifest page, and
/// every manifest page is such a route;</item>
/// <item>every row / toolbar button the screens' JS renders is a manifest action on the page that shows it, and every
/// manifest action is such a button;</item>
/// <item>codes and routes are unique; exactly one nav-visible top page.</item>
/// </list>
/// Not covered here: the Platform reconcile-state (push A → push B with a removal → DB == B). That is Platform's
/// reconcile, exercised live on restart (WP-0173-UI1 D6).
/// </summary>
public sealed class InventoryLedgerManifestCompletenessTests
{
    private static readonly string WebRoot = Path.Combine(RepoPaths.Root, "frontend", "Diten.Web");

    // The FU01 controllers and the JS folder of each page (pack §5/§11).
    private static readonly (string Controller, string JsFolder, string PageCode)[] Screens =
    [
        ("StockLedgerController.cs", "StockLedger", "STOCKLEDGER"),
        ("StockMovementsController.cs", "StockMovements", "STOCKMOVEMENTS")
    ];

    // JS action key → manifest ActionCode (pack §14 naming).
    private static readonly Dictionary<string, string> ActionCodeOfKey = new(StringComparer.Ordinal)
    {
        ["history"] = "ROWHISTORY",
        ["reversal"] = "REVERSAL"
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
        foreach (var (controller, _, _) in Screens)
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
        foreach (var (_, jsFolder, pageCode) in Screens)
        {
            var script = File.ReadAllText(Path.Combine(WebRoot, "wwwroot", "assets", "js", "Inventory", jsFolder, "index.js"));
            // An action object opens with its key (`{ key: 'history', … }`); a filter field opens with its id.
            var keys = Regex.Matches(script, @"\{\s*key:\s*'(?<key>\w+)'").Select(m => m.Groups["key"].Value).Distinct().ToList();
            Assert.All(keys, k => Assert.True(ActionCodeOfKey.ContainsKey(k), $"{jsFolder}/index.js renders action '{k}' that has no manifest mapping"));

            // UI-1 has no toolbar "New movement" (its page arrives with UI-2): the list's add-new text is empty.
            Assert.Matches(@"addNewText:\s*''", script);

            var expected = keys.Select(k => ActionCodeOfKey[k]).OrderBy(c => c).ToList();
            var page = Manifest.Pages.Single(p => p.PageCode == pageCode);
            Assert.Equal(expected, page.Actions.Select(a => a.ActionCode).OrderBy(c => c).ToList());
            Assert.All(page.Actions, a => Assert.True(a.IsRowAction && !a.IsToolbarAction, $"{a.ActionCode} must be a row action"));
        }
    }

    [Fact]
    public void Codes_and_routes_are_unique_and_the_module_has_one_nav_visible_top_page()
    {
        var manifest = new InventoryLedgerManifestProvider().GetManifest();
        Assert.Equal("INVENTORY-LEDGER", manifest.ModuleCode);
        Assert.Equal("SUPPLYCHAINEXECUTION", manifest.Domain);
        Assert.True(manifest.IsTenantAssignable);

        Assert.Equal(manifest.Pages.Count, manifest.Pages.Select(p => p.PageCode).Distinct().Count());
        Assert.Equal(manifest.Pages.Count, manifest.Pages.Select(p => p.RoutePath).Distinct().Count());
        var actions = manifest.Pages.SelectMany(p => p.Actions).Select(a => a.ActionCode).ToList();
        Assert.Equal(actions.Count, actions.Distinct().Count());

        var top = Assert.Single(manifest.Pages, p => p.IsNavigationVisible);
        Assert.Equal("STOCKLEDGER", top.PageCode);
        Assert.Null(top.ParentPageCode);
        Assert.All(manifest.Pages.Where(p => !p.IsNavigationVisible), p => Assert.Equal("STOCKLEDGER", p.ParentPageCode));
        // Tenant routes, never /Platform/… (the permission scope is derived from the route — standard §2c).
        Assert.All(manifest.Pages, p => Assert.False(p.RoutePath.StartsWith("/Platform", StringComparison.OrdinalIgnoreCase)));
        Assert.Single(actions, a => a == "REVERSAL");
        Assert.True(manifest.Pages.SelectMany(p => p.Actions).Single(a => a.ActionCode == "REVERSAL").IsDangerous);
    }

    private sealed record ModuleManifestDocumentView(Diten.BuildingBlocks.ModuleRegistration.Abstractions.ModuleManifestDocument Document)
    {
        public IReadOnlyList<Diten.BuildingBlocks.ModuleRegistration.Abstractions.ModuleManifestPage> Pages => Document.Pages;
    }
}
