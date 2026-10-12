using EphemeralMongo;
using MongoDB.Driver;

namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>
/// FU-0173-TEST-01 (WP-0173-PM1) — where the Inventory suite's Mongo is, decided ONCE per test process:
/// <list type="number">
/// <item><c>INVENTORY_TEST_MONGO</c> set → that replica set; refused (exception, the run stops) when any of its hosts is port
/// <see cref="ForbiddenPort"/>, the developer's live mongod.</item>
/// <item>not set → the suite STARTS ITS OWN single-node replica set (EphemeralMongo, random port, temporary data directory)
/// and disposes it when the process exits. The mongod binary is taken from the directory of <c>DITEN_TEST_MONGOD</c> (the
/// repo's other suites use the same variable) or the default install directory; no binary → an explicit error naming both
/// variables. It never falls back to an address.</item>
/// </list>
/// </summary>
public static class TestMongo
{
    /// <summary>The developer's live fleet mongod — never a test target.</summary>
    public const int ForbiddenPort = 27017;

    public const string DefaultBinaryDirectory = @"C:\Program Files\MongoDB\Server\9.0\bin";

    private static readonly Lazy<string> Resolved = new(
        () => Resolve(Environment.GetEnvironmentVariable("INVENTORY_TEST_MONGO"), StartOwn),
        LazyThreadSafetyMode.ExecutionAndPublication);

    private static IMongoRunner? _own;

    public static string ConnectionString => Resolved.Value;

    /// <summary>The decision itself (pure, so it is tested without starting anything).</summary>
    public static string Resolve(string? configured, Func<string> startOwn)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            var own = startOwn();
            Refuse27017(own, "the suite's own replica set");
            return own;
        }

        Refuse27017(configured, "INVENTORY_TEST_MONGO");
        return configured;
    }

    private static void Refuse27017(string connectionString, string source)
    {
        if (MongoUrl.Create(connectionString).Servers.Any(s => s.Port == ForbiddenPort))
        {
            throw new InvalidOperationException(
                $"{source} points at port {ForbiddenPort} (the developer's live mongod). Inventory tests run only on their own replica set: " +
                "unset INVENTORY_TEST_MONGO to let the suite start one, or point it at your own mongod (--replSet) on another port.");
        }
    }

    private static string StartOwn()
    {
        var configured = Environment.GetEnvironmentVariable("DITEN_TEST_MONGOD");
        var directory = !string.IsNullOrWhiteSpace(configured) ? Path.GetDirectoryName(configured) : DefaultBinaryDirectory;
        if (directory is null || !File.Exists(Path.Combine(directory, OperatingSystem.IsWindows() ? "mongod.exe" : "mongod")))
        {
            throw new InvalidOperationException(
                "No mongod binary for the Inventory suite's own replica set: set INVENTORY_TEST_MONGO (own replica set, not port 27017) " +
                $"or DITEN_TEST_MONGOD (path to mongod), or install MongoDB in {DefaultBinaryDirectory}.");
        }

        _own = MongoRunner.Run(new MongoRunnerOptions
        {
            BinaryDirectory = directory,
            UseSingleNodeReplicaSet = true,
            StandardOuputLogger = _ => { },
            StandardErrorLogger = _ => { }
        });
        AppDomain.CurrentDomain.ProcessExit += (_, _) => _own?.Dispose();
        return _own.ConnectionString;
    }
}
