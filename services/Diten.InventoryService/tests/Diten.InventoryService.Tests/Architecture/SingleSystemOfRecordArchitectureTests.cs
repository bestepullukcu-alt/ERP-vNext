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
        Assert.Equal(
            ["inventory_audit", "inventory_balances", "inventory_outbox", "inventory_reservations", "inventory_sequences",
             "inventory_sku_positions", "inventory_transactions"],
            InventoryCollections.All.OrderBy(x => x).ToList());
    }

    [Fact]
    public void No_production_source_names_a_collection_outside_the_declared_set()
    {
        var persistence = SourceScan.Production("Diten.InventoryService.Persistence");
        var named = persistence
            .SelectMany(f => CollectionName.Matches(f.Body).Select(m => m.Groups[1].Value))
            .Where(n => n.StartsWith("inventory_", StringComparison.Ordinal) || n.Contains("stock"))
            .Distinct()
            .ToList();

        Assert.All(named, n => Assert.Contains(n, InventoryCollections.All));

        var getCollectionCalls = persistence
            .SelectMany(f => Regex.Matches(f.Body, @"GetCollection<[^>]+>\(([^)]*)\)").Select(m => m.Groups[1].Value.Trim()))
            .Distinct()
            .ToList();
        Assert.All(getCollectionCalls, arg => Assert.StartsWith("InventoryCollections.", arg));
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
        Assert.Equal([nameof(InventoryBalance), nameof(InventorySkuPosition)], quantityCarriers.OrderBy(x => x, StringComparer.Ordinal));
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
