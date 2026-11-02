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

    // WP-0230-DL10-R1: a key that is there must be exactly "InMemory" or exactly "Mongo" (ordinal). The rows are the
    // other names, the other letter cases of the two names, an empty value and a blank value; the empty value goes through
    // the host (PvgServiceApiHost), as the service gets it from the environment.
    [Theory]
    [InlineData("Postgres")]
    [InlineData(" ")]
    [InlineData("")]
    [InlineData("mongo-db")]
    [InlineData("mongo")]
    [InlineData("MONGO")]
    [InlineData("inmemory")]
    [InlineData("INMEMORY")]
    [InlineData("Mongo ")]
    public void Unknown_store_value_refuses_start(string value)
    {
        var configuration = PvgMongoApiHost.Configuration(value, FakeConnectionString, "diten_fake");
        var builder = PvgServiceApiHost.CreatePvgServiceWebHostBuilder("Development", configuration, "http://127.0.0.1:0");

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Equal("PVG_PERSISTENCE_STORE_UNKNOWN", exception.Message);
        Assert.DoesNotContain(FakeConnectionValue, exception.ToString(), StringComparison.Ordinal);
    }

    // A production-like host refuses a store value of the wrong letter case too, before any other check and without
    // any connection attempt: the refusal comes out of Build, and nothing was started.
    [Theory]
    [InlineData("mongo")]
    [InlineData("MONGO")]
    [InlineData("")]
    public void Wrong_case_or_empty_store_value_is_refused_on_a_production_like_host(string value)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [PvgServiceApiHost.PersistenceStoreKey] = value,
                ["Mongo:ConnectionString"] = FakeConnectionString,
                ["Mongo:DatabaseName"] = "diten_fake",
                ["Pvg:Runtime:ProductionLike"] = "true",
                ["Pvg:Runtime:OperationalRuntimeAuthorized"] = "true",
                ["Pvg:Runtime:ApprovalReference"] = "RTA-MOD0230-001"
            })
            .Build();
        var builder = PvgServiceApiHost.CreatePvgServiceWebHostBuilder("Development", configuration, "http://127.0.0.1:0");

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Equal("PVG_PERSISTENCE_STORE_UNKNOWN", exception.Message);
        Assert.DoesNotContain(FakeConnectionValue, exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("127.0.0.1", exception.ToString(), StringComparison.Ordinal);
    }
}
