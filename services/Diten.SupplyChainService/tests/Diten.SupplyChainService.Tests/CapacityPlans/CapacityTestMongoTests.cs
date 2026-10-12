using Xunit;

namespace Diten.SupplyChainService.Tests.CapacityPlans;

// Pure tests of the fail-closed rule (Q131a). They never set MVP6_MOD0192_MONGO_URI: the process environment is
// shared with the CapacityPlans Mongo tests.
public sealed class CapacityTestMongoTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingUri_FailsClosed(string? value)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => CapacityTestMongo.Validate(value));

        Assert.Contains(CapacityTestMongo.EnvironmentVariableName, ex.Message);
        Assert.Contains("fail closed", ex.Message);
    }

    [Theory]
    [InlineData("mongodb://127.0.0.1:27017/?replicaSet=rs0")]
    [InlineData("mongodb://localhost/?replicaSet=rs0")]
    public void OperationalPorts_FailClosed(string value)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => CapacityTestMongo.Validate(value));

        Assert.Contains("protected MongoDB port", ex.Message);
    }

    [Theory]
    [InlineData("mongodb://db.example.test:31994/?replicaSet=rsq131")]
    [InlineData("mongodb://127.0.0.1:31994/")]
    public void RemoteHostOrMissingReplicaSet_FailsClosed(string value)
    {
        Assert.Throws<InvalidOperationException>(() => CapacityTestMongo.Validate(value));
    }

    [Fact]
    public void LaneReplicaSet_IsAccepted_AndKeepsTheFiveSecondSelectionTimeout()
    {
        var url = new MongoDB.Driver.MongoUrl(CapacityTestMongo.Validate("mongodb://127.0.0.1:31994/?replicaSet=rsq131"));

        Assert.Equal("rsq131", url.ReplicaSetName);
        Assert.Equal(31994, url.Server.Port);
        Assert.Equal(TimeSpan.FromSeconds(5), url.ServerSelectionTimeout);
    }

    [Fact]
    public void ExplicitSelectionTimeout_IsKept()
    {
        var url = new MongoDB.Driver.MongoUrl(
            CapacityTestMongo.Validate("mongodb://127.0.0.1:31994/?replicaSet=rsq131&serverSelectionTimeoutMS=2000"));

        Assert.Equal(TimeSpan.FromSeconds(2), url.ServerSelectionTimeout);
    }
}
