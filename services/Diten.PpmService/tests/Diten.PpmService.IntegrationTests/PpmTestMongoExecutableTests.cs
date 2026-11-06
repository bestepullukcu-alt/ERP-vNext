using Xunit;

namespace Diten.PpmService.IntegrationTests;

public sealed class PpmTestMongoExecutableTests
{
    [Fact]
    public void ExplicitLinuxExecutableTakesPrecedenceOverMacFallbacks()
    {
        var checkedPaths = new List<string>();

        var executable = PpmTestMongoExecutable.Resolve("/usr/bin/mongod", path =>
        {
            checkedPaths.Add(path);
            return true;
        });

        Assert.Equal("/usr/bin/mongod", executable);
        Assert.Equal(new[] { "/usr/bin/mongod" }, checkedPaths);
    }

    [Fact]
    public void InvalidExplicitExecutableFailsWithoutTryingFallbacks()
    {
        var checkedPaths = new List<string>();

        var exception = Assert.Throws<FileNotFoundException>(() =>
            PpmTestMongoExecutable.Resolve("/missing/mongod", path =>
            {
                checkedPaths.Add(path);
                return path != "/missing/mongod";
            }));

        Assert.Equal("/missing/mongod", exception.FileName);
        Assert.Contains(PpmTestMongoExecutable.EnvironmentVariable, exception.Message);
        Assert.Equal(new[] { "/missing/mongod" }, checkedPaths);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void UnconfiguredExecutableUsesExistingMacFallbacksInOrder(string? configuredPath)
    {
        var checkedPaths = new List<string>();

        var executable = PpmTestMongoExecutable.Resolve(configuredPath, path =>
        {
            checkedPaths.Add(path);
            return path == "/opt/local/bin/mongod";
        });

        Assert.Equal("/opt/local/bin/mongod", executable);
        Assert.Equal(new[] { "/opt/homebrew/bin/mongod", "/usr/local/bin/mongod", "/opt/local/bin/mongod" }, checkedPaths);
    }

    [Fact]
    public void MissingExecutableExplainsOfflineConfigurationRequirement()
    {
        var exception = Assert.Throws<FileNotFoundException>(() =>
            PpmTestMongoExecutable.Resolve(null, _ => false));

        Assert.Contains(PpmTestMongoExecutable.EnvironmentVariable, exception.Message);
        Assert.Contains("never download", exception.Message);
    }
}
