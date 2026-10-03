using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

// Pure tests of the fail-closed rule: they call Validate(...) and never set DITEN_PLATFORM_TEST_MONGO_URI, because
// the process environment is shared with the Mongo test classes that run in parallel.
public sealed class PlatformMongoTestConnectionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingUri_FailsClosed(string? value)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => PlatformMongoTestConnection.Validate(value));

        Assert.Contains(PlatformMongoTestConnection.EnvironmentVariableName, ex.Message);
        Assert.Contains("fail closed", ex.Message);
    }

    [Theory]
    [InlineData("mongodb://localhost:27017")]
    [InlineData("mongodb://127.0.0.1:27017/?directConnection=true")]
    [InlineData("mongodb://127.0.0.1:27018/?directConnection=true")]
    [InlineData("mongodb://127.0.0.1")]
    public void OperationalPorts_FailClosed(string value)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => PlatformMongoTestConnection.Validate(value));

        Assert.Contains("protected MongoDB port", ex.Message);
    }

    [Theory]
    [InlineData("mongodb://db.example.test:31994/?replicaSet=rsq131")]
    [InlineData("mongodb+srv://cluster.example.test/")]
    public void NonLoopbackUri_FailsClosed(string value)
    {
        Assert.Throws<InvalidOperationException>(() => PlatformMongoTestConnection.Validate(value));
    }

    [Theory]
    [InlineData("mongodb://127.0.0.1:31994/?replicaSet=rsq131")]
    [InlineData("mongodb://localhost:37994/?directConnection=true")]
    public void LaneOwnedLoopbackUri_IsReturnedUnchanged(string value)
    {
        Assert.Equal(value, PlatformMongoTestConnection.Validate(value));
    }
}
