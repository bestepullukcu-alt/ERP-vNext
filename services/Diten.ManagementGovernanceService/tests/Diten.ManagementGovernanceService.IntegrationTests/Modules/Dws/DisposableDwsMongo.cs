using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.ManagementGovernanceService.IntegrationTests.Modules.Dws;

[CollectionDefinition(Name)]
public sealed class DwsMongoCollection : ICollectionFixture<DisposableDwsMongo>
{
    public const string Name = "MOD-0354 disposable Mongo";
}

public sealed class DisposableDwsMongo : IAsyncLifetime
{
    private const string Mongod = "/opt/homebrew/bin/mongod";
    private Process? _process;
    private string? _directory;

    public int Port { get; private set; }
    public string ReplicaSetName { get; } = "dwsrs" + Guid.NewGuid().ToString("N")[..8];
    public MongoClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        if (!File.Exists(Mongod)) throw new InvalidOperationException("mongod is unavailable");
        Port = AllocatePort();
        if (Port < 27022 || Port is 27017 or 27018 or 27019) throw new InvalidOperationException("protected Mongo port");
        _directory = Path.Combine(Path.GetTempPath(), "diten-mod0354-b02-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _process = StartProcess(Port, _directory, ReplicaSetName);

        var direct = new MongoClient($"mongodb://127.0.0.1:{Port}/?directConnection=true&serverSelectionTimeoutMS=1000");
        await WaitForPingAsync(direct);
        var config = new BsonDocument
        {
            ["_id"] = ReplicaSetName,
            ["members"] = new BsonArray { new BsonDocument { ["_id"] = 0, ["host"] = $"127.0.0.1:{Port}" } }
        };
        await direct.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("replSetInitiate", config));
        Client = new MongoClient($"mongodb://127.0.0.1:{Port}/?replicaSet={ReplicaSetName}&directConnection=true&serverSelectionTimeoutMS=1000");
        await WaitForPrimaryAsync(Client);
    }

    public async Task DisposeAsync()
    {
        try
        {
            if (Client is not null) await Client.DropDatabaseAsync("mod0354_b02");
        }
        catch { }
        Stop();
        if (_directory is not null && Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    public static async Task<StandaloneDwsMongo> StartStandaloneAsync()
    {
        if (!File.Exists(Mongod)) throw new InvalidOperationException("mongod is unavailable");
        var port = AllocatePort();
        var directory = Path.Combine(Path.GetTempPath(), "diten-mod0354-standalone-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var process = StartProcess(port, directory, null);
        var client = new MongoClient($"mongodb://127.0.0.1:{port}/?directConnection=true&serverSelectionTimeoutMS=1000");
        await WaitForPingAsync(client);
        return new StandaloneDwsMongo(port, directory, process, client);
    }

    private static Process StartProcess(int port, string directory, string? replicaSet)
    {
        var info = new ProcessStartInfo(Mongod)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        info.ArgumentList.Add("--bind_ip"); info.ArgumentList.Add("127.0.0.1");
        info.ArgumentList.Add("--port"); info.ArgumentList.Add(port.ToString());
        info.ArgumentList.Add("--dbpath"); info.ArgumentList.Add(directory);
        info.ArgumentList.Add("--logpath"); info.ArgumentList.Add(Path.Combine(directory, "mongod.log"));
        info.ArgumentList.Add("--quiet");
        if (replicaSet is not null) { info.ArgumentList.Add("--replSet"); info.ArgumentList.Add(replicaSet); }
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

    private void Stop()
    {
        if (_process is null) return;
        if (!_process.HasExited) { _process.Kill(entireProcessTree: true); _process.WaitForExit(5000); }
        _process.Dispose();
        _process = null;
    }
}

public sealed class StandaloneDwsMongo(int port, string directory, Process process, MongoClient client) : IAsyncDisposable
{
    public int Port { get; } = port;
    public MongoClient Client { get; } = client;
    public async ValueTask DisposeAsync()
    {
        try { await Client.DropDatabaseAsync("mod0354_b02_standalone"); } catch { }
        if (!process.HasExited) { process.Kill(entireProcessTree: true); process.WaitForExit(5000); }
        process.Dispose();
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
