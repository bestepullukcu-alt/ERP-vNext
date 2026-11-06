namespace Diten.PpmService.IntegrationTests;

internal static class PpmTestMongoExecutable
{
    internal const string EnvironmentVariable = "DITEN_TEST_MONGOD";

    public static string Resolve() => Resolve(
        Environment.GetEnvironmentVariable(EnvironmentVariable), File.Exists);

    internal static string Resolve(string? configuredPath, Func<string, bool> fileExists)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            if (fileExists(configuredPath)) return configuredPath;

            // An explicit override is authoritative; never hide a broken CI configuration.
            throw new FileNotFoundException(
                $"{EnvironmentVariable} must point to an existing local mongod executable; tests never download MongoDB.",
                configuredPath);
        }

        foreach (var path in new[] { "/opt/homebrew/bin/mongod", "/usr/local/bin/mongod", "/opt/local/bin/mongod" })
            if (fileExists(path)) return path;

        throw new FileNotFoundException(
            $"A local mongod executable is required. Set {EnvironmentVariable} to its path; tests never download MongoDB.");
    }
}
