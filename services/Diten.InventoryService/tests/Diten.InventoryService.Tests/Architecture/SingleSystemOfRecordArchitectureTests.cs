using System.Reflection;
using System.Text.RegularExpressions;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Persistence;
using Xunit;

namespace Diten.InventoryService.Tests.Architecture;

/// <summary>
/// AC14 / pack §2 — MOD-0173 is the single stock system of record: no second stock collection or shadow balance,
/// no Model B (<c>Product</c> / <c>mdm_products</c>), no MDM database, no reference to another service's code.
/// </summary>
public sealed class SingleSystemOfRecordArchitectureTests
{
    private static readonly Regex CollectionName = new(@"""(inventory_[a-z_]+|[a-z]+_[a-z_]+)""", RegexOptions.Compiled);

    [Fact]
    public void The_service_owns_exactly_the_declared_collections()
    {
        // S2 adds inventory_reservations (holds) and inventory_sku_positions (projection, ASSUMPTION-0173-13).
        // MOD-0173-FU02 adds inventory_locations (place master, no quantity) and inventory_location_history (K-11).
        Assert.Equal(
            ["inventory_audit", "inventory_balances", "inventory_location_history", "inventory_locations", "inventory_outbox",
             "inventory_reservations", "inventory_sequences", "inventory_sku_positions", "inventory_transactions"],
            InventoryCollections.All.OrderBy(x => x).ToList());

        // MOD-0174 (WP-0174-P1) — the lot / serial SoR in the same database, prefix trace_ (pack ASSUMPTION-0174-02).
        // None carries a quantity (asserted below by Only_the_ledger_and_its_projection_carry_quantities).
        Assert.Equal(
            ["trace_genealogy_links", "trace_lot_history", "trace_lot_migrations", "trace_lots", "trace_movement_lot_links", "trace_serials"],
            Diten.InventoryService.Persistence.Traceability.TraceCollections.All.OrderBy(x => x).ToList());

        // WP-0174-P2 (MOD-0174 pack §8 K-A): the per-lot stock projection, written only by the ledger posting code in the
        // posting transaction — a ledger projection like inventory_sku_positions, never a second stock truth.
        Assert.Equal(["inventory_lot_positions"], Diten.InventoryService.Persistence.Traceability.TraceCollections.LedgerProjections);

        // MOD-0175 (WP-0175-Q1) — quarantine records in the same database, prefix quarantine_ (pack §3). None is stock: a hold /
        // blocked-stock Quantity is the quantity the record is about (Only_the_ledger_and_its_projection_carry_quantities).
        Assert.Equal(
            ["quarantine_block_reasons", "quarantine_blocked_stock", "quarantine_decision_events", "quarantine_decisions", "quarantine_event_inbox",
             "quarantine_history", "quarantine_holds", "quarantine_lot_release_claims", "quarantine_sequences", "quarantine_settings"],
            Diten.InventoryService.Persistence.Quarantine.QuarantineCollections.All.OrderBy(x => x).ToList());

        // MOD-0176 (WP-0176-F1) — expiry policy / exception records in the same database, prefix expiry_ (pack §3). None is stock: an
        // exception Quantity is the quantity the case is about.
        Assert.Equal(
            ["expiry_counters", "expiry_date_overrides", "expiry_exception_events", "expiry_exceptions", "expiry_fefo_issue_snapshots", "expiry_policies",
             "expiry_policy_history"],
            Diten.InventoryService.Persistence.Expiry.ExpiryCollections.All.OrderBy(x => x).ToList());
    }

    [Fact]
    public void No_production_source_names_a_collection_outside_the_declared_set()
    {
        var persistence = SourceScan.Production("Diten.InventoryService.Persistence");
        var declared = InventoryCollections.All.Concat(Diten.InventoryService.Persistence.Traceability.TraceCollections.All)
            .Concat(Diten.InventoryService.Persistence.Traceability.TraceCollections.LedgerProjections)
            .Concat(Diten.InventoryService.Persistence.Quarantine.QuarantineCollections.All)
            .Concat(Diten.InventoryService.Persistence.Expiry.ExpiryCollections.All).ToList();
        var named = persistence
            .SelectMany(f => CollectionName.Matches(f.Body).Select(m => m.Groups[1].Value))
            .Where(n => n.StartsWith("inventory_", StringComparison.Ordinal) || n.StartsWith("trace_", StringComparison.Ordinal)
                        || n.StartsWith("quarantine_", StringComparison.Ordinal) || n.StartsWith("expiry_", StringComparison.Ordinal) || n.Contains("stock"))
            .Distinct()
            .ToList();

        Assert.All(named, n => Assert.Contains(n, declared));

        var getCollectionCalls = persistence
            .SelectMany(f => Regex.Matches(f.Body, @"GetCollection<[^>]+>\(([^)]*)\)").Select(m => m.Groups[1].Value.Trim()))
            .Distinct()
            .ToList();
        Assert.All(getCollectionCalls, arg => Assert.True(
            arg.StartsWith("InventoryCollections.", StringComparison.Ordinal) || arg.StartsWith("TraceCollections.", StringComparison.Ordinal)
            || arg.StartsWith("QuarantineCollections.", StringComparison.Ordinal) || arg.StartsWith("ExpiryCollections.", StringComparison.Ordinal),
            $"GetCollection({arg}) does not name a declared collection constant."));
    }

    [Fact]
    public void Only_the_ledger_and_its_projection_carry_quantities()
    {
        var quantityCarriers = typeof(EntityBase).Assembly.GetTypes()
            .Where(t => typeof(EntityBase).IsAssignableFrom(t) && !t.IsAbstract)
            .Where(t => t.GetProperties().Any(p => p.Name is "OnHand" or "Available" or "Reserved"))
            .Select(t => t.Name)
            .ToList();

        // S2: the per-SKU position carries Reserved / AvailableOnHand — it is a projection of the ledger + reservations
        // (pack ASSUMPTION-0173-13, rebuild-proven by the AC-R9 invariant test), not a second stock truth.
        // WP-0174-P2 (MOD-0174 K-A): the per-lot position carries OnHand — a projection of the same ledger transactions,
        // written in the posting transaction and equal to Σ balance OnHand of the lot (property-tested), not a second truth.
        Assert.Equal(
            [nameof(InventoryBalance), nameof(InventorySkuPosition), nameof(Diten.InventoryService.Domain.Traceability.Entities.LotPosition)],
            quantityCarriers.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("mdm_products")]
    [InlineData("Diten.MdmService")]
    [InlineData("Diten.SupplyChainService")]
    [InlineData("Diten.ProcurementService")]
    [InlineData("diten_mdm")]
    public void No_production_source_references_another_services_data_or_code(string forbidden)
    {
        var offenders = SourceScan.Production()
            .Where(f => f.Body.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
            .Select(f => f.RelativePath)
            .ToList();

        Assert.True(offenders.Count == 0, $"'{forbidden}' referenced by: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void No_Model_B_Product_type_is_used()
    {
        var offenders = SourceScan.Production()
            .Where(f => Regex.IsMatch(f.Body, @"\bProduct\b(?!Ref|Reference|Master|RefQuery|RefValidation)"))
            .Select(f => f.RelativePath)
            .ToList();

        Assert.True(offenders.Count == 0, "Model B 'Product' referenced by: " + string.Join(", ", offenders));
    }

    [Fact]
    public void No_service_project_references_another_service()
    {
        var projects = Directory.EnumerateFiles(Path.Combine(TestInfrastructure.RepoPaths.ServiceRoot, "src"), "*.csproj", SearchOption.AllDirectories);
        foreach (var project in projects)
        {
            var references = Regex.Matches(File.ReadAllText(project), @"ProjectReference\s+Include=""([^""]+)""").Select(m => m.Groups[1].Value);
            Assert.All(references, r => Assert.True(
                r.Contains("Diten.InventoryService.", StringComparison.Ordinal) || r.Contains("Diten.BuildingBlocks.", StringComparison.Ordinal),
                $"{Path.GetFileName(project)} references {r}"));
        }
    }

    [Fact]
    public void The_domain_has_no_Mongo_or_framework_dependency()
    {
        var referenced = typeof(EntityBase).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        Assert.DoesNotContain(referenced, n => n.StartsWith("MongoDB", StringComparison.Ordinal));
        Assert.DoesNotContain(referenced, n => n.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        Assert.DoesNotContain(referenced, n => n.StartsWith("MediatR", StringComparison.Ordinal));
    }

    [Fact]
    public void The_application_layer_does_not_touch_Mongo()
    {
        var referenced = Assembly.Load("Diten.InventoryService.Application").GetReferencedAssemblies().Select(a => a.Name!);

        Assert.DoesNotContain(referenced, n => n.StartsWith("MongoDB", StringComparison.Ordinal));
    }

    [Fact]
    public void Five_layers_exist()
    {
        var layers = Directory.GetDirectories(Path.Combine(TestInfrastructure.RepoPaths.ServiceRoot, "src")).Select(Path.GetFileName).OrderBy(x => x).ToList();

        Assert.Equal(
            [
                "Diten.InventoryService.Api", "Diten.InventoryService.Application", "Diten.InventoryService.Domain",
                "Diten.InventoryService.Infrastructure", "Diten.InventoryService.Persistence"
            ],
            layers);
    }
}
