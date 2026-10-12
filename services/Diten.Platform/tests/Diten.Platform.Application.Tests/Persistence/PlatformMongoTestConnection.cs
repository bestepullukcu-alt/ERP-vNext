using MongoDB.Driver;

namespace Diten.Platform.Application.Tests.Persistence;

/// <summary>
/// The one place the Platform Mongo tests take their connection string from (Q131a).
///
/// The URI comes ONLY from <c>DITEN_PLATFORM_TEST_MONGO_URI</c>, which the lane sets for its own slot
/// (<c>scripts/test-env/mvp6-test-mongo-env.sh</c>). There is no default. A missing value, a remote host, a URI
/// with credentials or an operational MongoDB port fails the test with a clear message instead of quietly
/// using the developer's MongoDB (Q121b D2: 49 test databases were created there).
/// </summary>
internal static class PlatformMongoTestConnection
{
    public const string EnvironmentVariableName = "DITEN_PLATFORM_TEST_MONGO_URI";

    // The operational band. The URI must name the lane's own port explicitly.
    private static readonly HashSet<int> ProtectedPorts = [27017, 27018, 27019, 27020, 27021];

    public static string RequireConnectionString() =>
        Validate(Environment.GetEnvironmentVariable(EnvironmentVariableName));

    /// <summary>
    /// Pure check, so its tests never change the process environment (which parallel test classes share).
    /// Messages never repeat the URI.
    /// </summary>
    internal static string Validate(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"{EnvironmentVariableName} is not set. Platform Mongo tests fail closed: point it at a lane-owned "
                + "loopback MongoDB (scripts/test-env/mvp6-test-mongo-env.sh). There is no fallback port.");
        }

        if (connectionString.StartsWith("mongodb+srv://", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{EnvironmentVariableName} must be a plain mongodb:// loopback URI.");
        }

        MongoUrl url;
        try
        {
            url = new MongoUrl(connectionString);
        }
        catch (Exception ex) when (ex is MongoConfigurationException or FormatException or ArgumentException)
        {
            throw new InvalidOperationException($"{EnvironmentVariableName} is not a valid MongoDB URI.", ex);
        }

        if (url.Username is not null)
        {
            throw new InvalidOperationException($"{EnvironmentVariableName} must not carry credentials.");
        }

        var servers = url.Servers?.ToArray() ?? [];
        if (servers.Length == 0)
        {
            throw new InvalidOperationException($"{EnvironmentVariableName} must name a loopback host and the lane's port.");
        }

        foreach (var server in servers)
        {
            if (server.Host is not ("127.0.0.1" or "localhost" or "::1" or "[::1]"))
            {
                throw new InvalidOperationException($"{EnvironmentVariableName} must point at a loopback host.");
            }

            if (ProtectedPorts.Contains(server.Port))
            {
                throw new InvalidOperationException(
                    $"{EnvironmentVariableName} points at protected MongoDB port {server.Port}. Use the lane's own port.");
            }
        }

        return connectionString;
    }
}
