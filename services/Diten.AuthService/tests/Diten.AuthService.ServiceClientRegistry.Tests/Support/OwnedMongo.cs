using Diten.AuthService.Persistence.Serialization;
using EphemeralMongo;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.AuthService.ServiceClientRegistry.Tests.Support;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class OwnedMongoCollection : ICollectionFixture<OwnedMongo>
{
    public const string Name = "Owned service-client registry mongod";
}

/// <summary>
/// A test-owned, throwaway <c>mongod</c> (its own process, random port, temp data directory) — never the shared
/// localhost:27017 and never a development database. One fixed-name database (DB-010); every test isolates itself
/// with fresh client codes and tenant ids, the way production isolates.
/// </summary>
public sealed class OwnedMongo : IDisposable
{
    public const string DatabaseName = "diten_auth_service_client_registry_itest";
    private readonly IMongoRunner _runner;

    public OwnedMongo()
    {
        GuidSerializerRegistration.EnsureStandard();
        _runner = MongoRunner.Run(new MongoRunnerOptions { BinaryDirectory = ResolveMongodDirectory() });
        var url = new MongoUrl(_runner.ConnectionString);
        if (url.Server.Port == 27017)
        {
            _runner.Dispose();
            throw new InvalidOperationException("The owned mongod bound the shared port; refusing to run.");
        }

        // The same client settings production builds in AddPersistence: without the Standard Guid representation on
        // the client, a Guid in a FILTER is encoded differently from the Guid in the stored document and matches nothing.
        var settings = MongoClientSettings.FromConnectionString(_runner.ConnectionString);
#pragma warning disable CS0618
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        Database = new MongoClient(settings).GetDatabase(DatabaseName);
        Database.RunCommand<BsonDocument>(new BsonDocument("ping", 1));
    }

    public IMongoDatabase Database { get; }

    public void Dispose() => _runner.Dispose();

    private static string ResolveMongodDirectory()
    {
        var executable = OperatingSystem.IsWindows() ? "mongod.exe" : "mongod";
        var named = Environment.GetEnvironmentVariable("DITEN_TEST_MONGOD");
        if (!string.IsNullOrWhiteSpace(named) && File.Exists(named)) return Path.GetDirectoryName(Path.GetFullPath(named))!;

        var directory = Environment.GetEnvironmentVariable("DITEN_ITEST_MONGOD_BIN_DIR");
        if (!string.IsNullOrWhiteSpace(directory) && File.Exists(Path.Combine(directory, executable))) return directory;

        var candidates = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Concat(["/opt/homebrew/bin", "/usr/local/bin", "/usr/bin"]);
        return candidates.FirstOrDefault(candidate => File.Exists(Path.Combine(candidate, executable)))
            ?? throw new InvalidOperationException(
                "These tests start their OWN mongod. Set DITEN_TEST_MONGOD to the mongod binary (or install MongoDB).");
    }
}
