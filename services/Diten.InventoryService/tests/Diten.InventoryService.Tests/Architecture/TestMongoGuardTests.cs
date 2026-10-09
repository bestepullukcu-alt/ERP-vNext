using System.Text.RegularExpressions;
using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Architecture;

/// <summary>
/// FU-0173-TEST-01 (WP-0173-PM1): the Inventory suite never runs against the developer's live mongod on 27017. Before PM1,
/// <c>TestSettings</c> fell back to <c>mongodb://127.0.0.1:27017</c> when <c>INVENTORY_TEST_MONGO</c> was unset (and the
/// WP-0173-PM2 baseline measured it doing exactly that). Now: unset → the suite's OWN replica set; set to 27017 → refused.
/// </summary>
public sealed class TestMongoGuardTests
{
    [Fact]
    public void Without_INVENTORY_TEST_MONGO_the_suite_starts_its_own_replica_set()
    {
        var started = 0;
        var connection = TestMongo.Resolve(null, () => { started++; return "mongodb://127.0.0.1:48123/?replicaSet=rs0"; });
        Assert.Equal(1, started);
        Assert.Equal("mongodb://127.0.0.1:48123/?replicaSet=rs0", connection);
        Assert.Equal(1, TestMongo.Resolve("  ", () => { started++; return "mongodb://127.0.0.1:48124/"; }) == "mongodb://127.0.0.1:48124/" ? 1 : 0);
    }

    [Theory]
    [InlineData("mongodb://127.0.0.1:27017")]
    [InlineData("mongodb://localhost:27017/?replicaSet=rs0")]
    [InlineData("mongodb://127.0.0.1:27217,127.0.0.1:27017/?replicaSet=rs0")]
    public void A_configured_address_on_27017_is_refused(string configured)
        => Assert.Throws<InvalidOperationException>(() => TestMongo.Resolve(configured, () => throw new Xunit.Sdk.XunitException("must not start")));

    [Fact]
    public void A_configured_own_replica_set_is_used_as_is()
        => Assert.Equal("mongodb://127.0.0.1:27217/?replicaSet=rs0",
            TestMongo.Resolve("mongodb://127.0.0.1:27217/?replicaSet=rs0", () => throw new Xunit.Sdk.XunitException("must not start")));

    [Fact]
    public void The_suite_in_this_run_is_not_on_27017()
        => Assert.DoesNotContain(MongoUrl.Create(TestSettings.MongoConnectionString).Servers, s => s.Port == TestMongo.ForbiddenPort);

    [Fact]
    public void No_test_source_names_a_27017_connection_string()
    {
        var root = Path.Combine(RepoPaths.ServiceRoot, "tests", "Diten.InventoryService.Tests");
        var offenders = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(f => !f.EndsWith(nameof(TestMongoGuardTests) + ".cs", StringComparison.Ordinal))
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"mongodb://[^""\s]*:27017"))
            .Select(Path.GetFileName)
            .ToList();
        Assert.Empty(offenders);
    }
}
