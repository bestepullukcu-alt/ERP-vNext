using Microsoft.Extensions.Configuration;
using Xunit;

namespace Diten.PvgService.Api.Tests.Persistence;

// WP-0230-DL10, AC-13 (c): the start refusals of the persistence selection. Every refusal is decided before any
// connection attempt, so none of these tests needs a mongod. The refusal message is the safe code and nothing else:
// the fake connection value below must not appear in it.
public sealed class PvgMongoStartRefusalTests
{
    private const string FakeConnectionValue = "fake-secret-pw-for-refusal-tests";

    private static string FakeConnectionString =>
        $"mongodb://fake-user:{FakeConnectionValue}@127.0.0.1:9/diten_fake";

    [Theory]
    [InlineData("Production")]
    [InlineData("DevelopmentProductionLike")]
    public void Mongo_store_is_refused_on_a_production_like_host(string row)
    {
        var settings = new Dictionary<string, string?>
        {
            [PvgServiceApiHost.PersistenceStoreKey] = "Mongo",
            ["Mongo:ConnectionString"] = FakeConnectionString,
            ["Mongo:DatabaseName"] = "diten_fake"
        };
        string environment;
        if (row == "Production")
        {
            // Environment Production and no other runtime flag.
            environment = "Production";
        }
        else
        {
            // Development, made production-like by the flag, with every other runtime flag in order: the persistence
            // check is the first check, so it refuses before any of them is looked at.
            environment = "Development";
            settings["Pvg:Runtime:ProductionLike"] = "true";
            settings["Pvg:Runtime:OperationalRuntimeAuthorized"] = "true";
            settings["Pvg:Runtime:ApprovalReference"] = "RTA-MOD0230-001";
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        var builder = PvgServiceApiHost.CreatePvgServiceWebHostBuilder(environment, configuration, "http://127.0.0.1:0");

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Equal("PVG_PERSISTENCE_BUILD_TEST_ONLY", exception.Message);
        Assert.DoesNotContain(FakeConnectionValue, exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("127.0.0.1", exception.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Postgres")]
    [InlineData(" ")]
    [InlineData("mongo-db")]
    // An empty value reads as "not set" through the host's configuration and so selects the default store; a value that
    // is present but is not one of the two names refuses the start.
    public void Unknown_store_value_refuses_start(string value)
    {
        var configuration = PvgMongoApiHost.Configuration(value, FakeConnectionString, "diten_fake");
        var builder = PvgServiceApiHost.CreatePvgServiceWebHostBuilder("Development", configuration, "http://127.0.0.1:0");

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Equal("PVG_PERSISTENCE_STORE_UNKNOWN", exception.Message);
        Assert.DoesNotContain(FakeConnectionValue, exception.ToString(), StringComparison.Ordinal);
    }
}
