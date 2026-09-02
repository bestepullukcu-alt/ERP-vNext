using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Diten.ManagementGovernanceService.Persistence.Modules.ProcessModeling.Catalog;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.ManagementGovernanceService.IntegrationTests.Modules.ProcessModeling.Catalog;

[CollectionDefinition(Name)]
public sealed class CatalogMongoCollection : ICollectionFixture<DisposableCatalogMongo>
{
    public const string Name = "MOD-0355 Catalog disposable Mongo";
}

public sealed class DisposableCatalogMongo : IAsyncLifetime
{
    public const string DatabaseName = "diten_mg_process_modeling_catalog_itest";
    private const string Mongod = "/opt/homebrew/bin/mongod";
    private Process? _process;
    private string? _directory;

    public MongoClient Client { get; private set; } = null!;
    public CatalogMongoContext Context { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        if (!File.Exists(Mongod)) throw new InvalidOperationException("mongod is unavailable");
        var port = AllocatePort();
        var replicaSet = "pmcatalog" + Guid.NewGuid().ToString("N")[..8];
        _directory = Path.Combine(Path.GetTempPath(), "diten-mod0355-catalog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _process = Start(port, _directory, replicaSet);

        var direct = new MongoClient($"mongodb://127.0.0.1:{port}/?directConnection=true&serverSelectionTimeoutMS=1000");
        await WaitForPingAsync(direct);
        await direct.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument
        {
            ["replSetInitiate"] = new BsonDocument
            {
                ["_id"] = replicaSet,
                ["members"] = new BsonArray { new BsonDocument { ["_id"] = 0, ["host"] = $"127.0.0.1:{port}" } }
            }
        });
        Client = new MongoClient($"mongodb://127.0.0.1:{port}/?replicaSet={replicaSet}&directConnection=true&serverSelectionTimeoutMS=1000");
        await WaitForPrimaryAsync(Client);
        Context = new CatalogMongoContext(Client, DatabaseName);
        await new CatalogMongoIndexInitializer(Context).InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        try { if (Client is not null) await Client.DropDatabaseAsync(DatabaseName); } catch { }
        if (_process is not null)
        {
            if (!_process.HasExited) { _process.Kill(entireProcessTree: true); _process.WaitForExit(5000); }
            _process.Dispose();
        }
        if (_directory is not null && Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private static Process Start(int port, string directory, string replicaSet)
    {
        var info = new ProcessStartInfo(Mongod) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var argument in new[] { "--bind_ip", "127.0.0.1", "--port", port.ToString(), "--dbpath", directory, "--logpath", Path.Combine(directory, "mongod.log"), "--quiet", "--replSet", replicaSet })
            info.ArgumentList.Add(argument);
        return Process.Start(info) ?? throw new InvalidOperationException("mongod did not start");
    }

    private static int AllocatePort()
    {
        while (true)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            if (port >= 27022 && port is not 27017 and not 27018 and not 27019) return port;
        }
    }

    private static async Task WaitForPingAsync(MongoClient client)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            try { await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1)); return; }
            catch when (attempt < 99) { await Task.Delay(50); }
        }
    }

    private static async Task WaitForPrimaryAsync(MongoClient client)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            try
            {
                var hello = await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1));
                if (hello.GetValue("isWritablePrimary", false).ToBoolean()) return;
            }
            catch when (attempt < 99) { }
            await Task.Delay(50);
        }
        throw new InvalidOperationException("replica-set primary unavailable");
    }
}
