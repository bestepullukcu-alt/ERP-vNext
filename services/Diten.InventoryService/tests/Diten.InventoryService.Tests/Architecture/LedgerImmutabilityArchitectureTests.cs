using System.Reflection;
using System.Text.RegularExpressions;
using Diten.InventoryService.Api.Controllers;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Persistence.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Diten.InventoryService.Tests.Architecture;

/// <summary>
/// AC17 / pack §8, §13.10 — no HTTP route and no repository member can update or delete a transaction.
/// Checked on the compiled types (reflection) AND on the source text, so neither a new method nor a raw driver call
/// slips in unnoticed.
/// </summary>
public sealed class LedgerImmutabilityArchitectureTests
{
    private static readonly Regex MutatingName = new(@"^(Update|Replace|Delete|Remove|Edit|Patch|Upsert|Modify|Set|Change|Purge|Drop)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Any driver call that could change or remove an existing transaction document.</summary>
    private static readonly Regex TransactionMutation = new(
        @"_transactions\s*\.\s*(UpdateOne|UpdateMany|ReplaceOne|DeleteOne|DeleteMany|FindOneAndUpdate|FindOneAndReplace|FindOneAndDelete|BulkWrite)\w*\s*\(",
        RegexOptions.Compiled);

    [Fact]
    public void The_ledger_repository_contract_has_no_mutating_member()
    {
        var members = typeof(IInventoryLedgerRepository).GetMethods().Select(m => m.Name).ToList();

        Assert.DoesNotContain(members, n => MutatingName.IsMatch(n));
        Assert.Equal("PostAsync", Assert.Single(members, n => !n.StartsWith("Find", StringComparison.Ordinal)
                                                              && !n.StartsWith("Query", StringComparison.Ordinal)
                                                              && !n.StartsWith("List", StringComparison.Ordinal)
                                                              && !n.StartsWith("Is", StringComparison.Ordinal)));
    }

    [Fact]
    public void The_ledger_repository_implementation_exposes_no_mutating_public_member()
    {
        var publicMembers = typeof(InventoryLedgerRepository)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToList();

        Assert.DoesNotContain(publicMembers, n => MutatingName.IsMatch(n));
    }

    [Fact]
    public void No_production_source_updates_replaces_or_deletes_a_transaction_document()
    {
        var offenders = SourceScan.Production()
            .Where(f => TransactionMutation.IsMatch(f.Body))
            .Select(f => f.RelativePath)
            .ToList();

        Assert.True(offenders.Count == 0, "transaction mutation found in: " + string.Join(", ", offenders));
    }

    [Fact]
    public void The_transaction_collection_is_only_ever_read_or_inserted()
    {
        var repository = SourceScan.Production("Diten.InventoryService.Persistence")
            .Single(f => f.RelativePath.EndsWith("InventoryLedgerRepository.cs", StringComparison.Ordinal));

        var calls = Regex.Matches(repository.Body, @"_transactions\s*\.\s*(\w+)")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        Assert.NotEmpty(calls);
        Assert.All(calls, call => Assert.Contains(call, new[] { "Find", "InsertOneAsync", "CountDocumentsAsync" }));
    }

    /// <summary>
    /// The LEDGER controller (every /api/inventory route) is GET / POST only. MOD-0173-FU02 adds LocationsController in
    /// the same assembly, whose frozen-by-pack PATCH is master-data editing, not ledger mutation; it has its own, equally
    /// exact guard (<see cref="LocationRouteArchitectureTests"/>), and no controller may expose PUT or DELETE
    /// (<see cref="No_controller_in_the_service_exposes_put_or_delete"/>).
    /// </summary>
    [Fact]
    public void No_controller_route_can_put_patch_or_delete()
    {
        var verbs = LedgerControllerActions()
            .SelectMany(a => a.GetCustomAttributes<HttpMethodAttribute>())
            .SelectMany(a => a.HttpMethods)
            .Distinct()
            .ToList();

        Assert.NotEmpty(verbs);
        Assert.All(verbs, v => Assert.Contains(v, new[] { "GET", "POST" }));
    }

    [Fact]
    public void The_routes_are_exactly_the_six_contract_operations()
    {
        var routes = LedgerControllerActions()
            .SelectMany(a => a.GetCustomAttributes<HttpMethodAttribute>().Select(h => $"{h.HttpMethods.Single()} api/inventory/{h.Template}"))
            .OrderBy(r => r, StringComparer.Ordinal)
            .ToList();

        // S1's four + S2's createReservation / releaseReservation — the frozen contract has no other operation (no
        // GET reservation, no reservation list), so neither does the service.
        Assert.Equal(
            [
                "GET api/inventory/availability",
                "GET api/inventory/balance",
                "GET api/inventory/transactions",
                "POST api/inventory/movements",
                "POST api/inventory/reservations",
                "POST api/inventory/reservations/{id}/release"
            ],
            routes);
    }

    [Fact]
    public void Every_api_inventory_route_is_served_by_the_ledger_controller_only()
    {
        var owners = typeof(InventoryController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .Where(t => t.GetCustomAttribute<RouteAttribute>()?.Template?.StartsWith("api/inventory", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        Assert.Equal([typeof(InventoryController)], owners);
    }

    /// <summary>
    /// No DELETE anywhere. PUT exists only where a frozen contract names a whole-record replacement of MASTER DATA, never a
    /// stock record: QUARANTINE-BUNDLE 1.0.0 saveSettings (<c>PUT /settings</c>) and updateBlockReason
    /// (<c>PUT /block-reasons/{code}</c>) — exactly these two, by controller + template (WP-0175-Q1) — and FEFO-BUNDLE 1.0.0
    /// updatePolicy (<c>PUT /api/expiry/policy</c>, the company's expiry policy, WP-0176-F1).
    /// </summary>
    [Fact]
    public void No_controller_in_the_service_exposes_put_or_delete()
    {
        var routes = typeof(InventoryController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>())
                .SelectMany(a => a.HttpMethods.Select(v => (Verb: v, Route: $"{t.Name}:{a.Template}"))))
            .ToList();

        Assert.NotEmpty(routes);
        Assert.DoesNotContain(routes, r => r.Verb == "DELETE");
        // RECALL-BUNDLE 1.0.0 updateSettings (PUT /api/recall/settings — the company's recall settings, master data, WP-0177-R1): exactly
        // this one recall PUT; it is taken out here so the list below stays as frozen (any other recall PUT still turns it red).
        Assert.Single(routes, r => r.Verb == "PUT" && r.Route == "RecallController:settings");
        routes.RemoveAll(r => r.Verb == "PUT" && r.Route == "RecallController:settings");
        Assert.Equal(
            ["ExpiryController:policy", "QuarantineController:block-reasons/{code}", "QuarantineController:settings"],
            routes.Where(r => r.Verb == "PUT").Select(r => r.Route).OrderBy(x => x, StringComparer.Ordinal).ToList());
    }

    [Fact]
    public void Every_controller_derives_from_CustomBaseController()
    {
        var controllers = typeof(InventoryController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        Assert.NotEmpty(controllers);
        Assert.All(controllers, c => Assert.True(typeof(CustomBaseController).IsAssignableFrom(c), c.Name));
    }

    private static IEnumerable<MethodInfo> LedgerControllerActions()
        => typeof(InventoryController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

    private static IEnumerable<MethodInfo> ControllerActions()
        => typeof(InventoryController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
}
