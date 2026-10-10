using MongoDB.Driver;

namespace Diten.SupplyChainService.Tests.CapacityPlans;

/// <summary>
/// Q131a: the CapacityPlans tests no longer hard-code one MongoDB port and replica-set name. The lane passes
/// its own replica set in <c>MVP6_MOD0192_MONGO_URI</c> (the pattern of <c>MVP6_MOD0190_MONGO_URI</c> in
/// SandopPlans). It is set per slot by <c>scripts/test-env/mvp6-test-mongo-env.sh</c>.
///
/// There is no default. The tests need a loopback single-member replica set: they use transactions, fail points
/// (mongod started with enableTestCommands=1) and a second test process. A missing or unsuitable URI fails closed.
/// </summary>
internal static class CapacityTestMongo
{
    public const string EnvironmentVariableName = "MVP6_MOD0192_MONGO_URI";

    private static readonly HashSet<int> ProtectedPorts = [27017, 27018, 27019, 27020, 27021];

    // What the former literal carried (serverSelectionTimeoutMS=5000); kept unless the lane URI sets its own.
    private static readonly TimeSpan DefaultServerSelectionTimeout = TimeSpan.FromSeconds(5);

    /// <summary>The lane URI. The child worker of CapacityRestartTests inherits the variable from its parent process.</summary>
    public static string Connection => Validate(Environment.GetEnvironmentVariable(EnvironmentVariableName));

    /// <summary>The lane URI with an application name, used by the fail-point tests to target one client.</summary>
    public static string WithApplicationName(string applicationName) =>
        new MongoUrlBuilder(Connection) { ApplicationName = applicationName }.ToString();

    /// <summary>Pure check; messages never repeat the URI.</summary>
    internal static string Validate(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"{EnvironmentVariableName} is not set. CapacityPlans tests fail closed: point it at the lane's loopback "
                + "replica set (scripts/test-env/mvp6-test-mongo-env.sh). There is no fallback port.");
        }

        if (connectionString.StartsWith("mongodb+srv://", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{EnvironmentVariableName} must be a plain mongodb:// loopback URI.");
        }

        MongoUrlBuilder builder;
        try
        {
            builder = new MongoUrlBuilder(connectionString);
        }
        catch (Exception ex) when (ex is MongoConfigurationException or FormatException or ArgumentException)
        {
            throw new InvalidOperationException($"{EnvironmentVariableName} is not a valid MongoDB URI.", ex);
        }

        if (builder.Username is not null)
        {
            throw new InvalidOperationException($"{EnvironmentVariableName} must not carry credentials.");
        }

        var servers = builder.Servers?.ToArray() ?? [];
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

        if (string.IsNullOrWhiteSpace(builder.ReplicaSetName))
        {
            throw new InvalidOperationException(
                $"{EnvironmentVariableName} must name the lane's replica set (replicaSet=...): CapacityPlans tests use transactions.");
        }

        if (!connectionString.Contains("serverSelectionTimeoutMS", StringComparison.OrdinalIgnoreCase))
        {
            builder.ServerSelectionTimeout = DefaultServerSelectionTimeout;
        }

        return builder.ToString();
    }
}
