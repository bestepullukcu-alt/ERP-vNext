using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.PpmService.IntegrationTests.GateI.DecisionTrace;


public sealed class GateIDisposableMongoReplicaSet : IAsyncLifetime
{
    private const string MongoExecutable = "/opt/homebrew/bin/mongod";
    private const string ReplicaSetName = "ppm-gate-i-rs";
    private static readonly HashSet<int> ForbiddenPorts = [27017, 27018, 27019, 27021];
    private Process? _process;
    private string? _temporaryRoot;
    private int _port;

    public string ConnectionString { get; private set; } = string.Empty;
    public string DatabaseName { get; } = $"diten_ppm_gate_i_r4_{Environment.ProcessId}";

    public async Task InitializeAsync()
    {
        if (!File.Exists(MongoExecutable))
            throw new FileNotFoundException("The test-owned mongod executable is unavailable.", MongoExecutable);

        try
        {
            _port = SelectAllowedPort();
            _temporaryRoot = Path.Combine(Path.GetTempPath(), $"diten-mod0117-gate-i-{_port}");
            var dataPath = Path.Combine(_temporaryRoot, "data");
            Directory.CreateDirectory(dataPath);

            var start = new ProcessStartInfo(MongoExecutable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in new[]
            {
                "--bind_ip", "127.0.0.1",
                "--port", _port.ToString(CultureInfo.InvariantCulture),
                "--dbpath", dataPath,
                "--logpath", Path.Combine(_temporaryRoot, "mongod.log"),
                "--pidfilepath", Path.Combine(_temporaryRoot, "mongod.pid"),
                "--nounixsocket", "--replSet", ReplicaSetName
            }) start.ArgumentList.Add(argument);

            _process = Process.Start(start)
                ?? throw new InvalidOperationException("Disposable Gate I mongod did not start.");
            _ = _process.StandardOutput.ReadToEndAsync();
            _ = _process.StandardError.ReadToEndAsync();

            var direct = new MongoClient(
                $"mongodb://127.0.0.1:{_port}/?directConnection=true&serverSelectionTimeoutMS=1000");
            await WaitForPingAsync(direct);
            await direct.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument
            {
                ["replSetInitiate"] = new BsonDocument
                {
                    ["_id"] = ReplicaSetName,
                    ["members"] = new BsonArray
                    {
                        new BsonDocument { ["_id"] = 0, ["host"] = $"127.0.0.1:{_port}" }
                    }
                }
            });

            ConnectionString =
                $"mongodb://127.0.0.1:{_port}/?replicaSet={ReplicaSetName}&serverSelectionTimeoutMS=5000";
            await WaitForPrimaryAsync(new MongoClient(ConnectionString));
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async Task ResetAsync()
    {
        var database = new MongoClient(ConnectionString).GetDatabase(DatabaseName);
        using var cursor = await database.ListCollectionNamesAsync();
        foreach (var collectionName in await cursor.ToListAsync())
            await database.GetCollection<BsonDocument>(collectionName)
                .DeleteManyAsync(FilterDefinition<BsonDocument>.Empty);
    }

    public async Task DisposeAsync()
    {
        if (!string.IsNullOrWhiteSpace(ConnectionString))
        {
            try
            {
                await new MongoClient(ConnectionString).DropDatabaseAsync(DatabaseName);
            }
            catch (MongoException)
            {
                // Process and filesystem cleanup remains authoritative if Mongo is unavailable.
            }
        }

        if (_process is not null)
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }
            _process.Dispose();
            _process = null;
        }

        if (_temporaryRoot is not null && Directory.Exists(_temporaryRoot))
            Directory.Delete(_temporaryRoot, recursive: true);
        if (_temporaryRoot is not null && Directory.Exists(_temporaryRoot))
            throw new InvalidOperationException("Disposable Gate I Mongo temporary residue remains.");
        if (_port != 0 && !CanBind(_port))
            throw new InvalidOperationException("Disposable Gate I Mongo listener residue remains.");
    }

    private static int SelectAllowedPort()
    {
        for (var attempt = 0; attempt < 32; attempt++)
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            if (port >= 27022 && !ForbiddenPorts.Contains(port)) return port;
        }
        throw new InvalidOperationException("The operating system did not provide an allowed Mongo test port.");
    }

    private static async Task WaitForPingAsync(IMongoClient client)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        Exception? last = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await client.GetDatabase("admin")
                    .RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
                return;
            }
            catch (Exception exception) when (exception is MongoException or TimeoutException)
            {
                last = exception;
                await Task.Delay(100);
            }
        }
        throw new InvalidOperationException("Disposable Gate I Mongo did not accept connections.", last);
    }

    private static async Task WaitForPrimaryAsync(IMongoClient client)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var hello = await client.GetDatabase("admin")
                    .RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1));
                if (hello.TryGetValue("isWritablePrimary", out var writable) && writable.ToBoolean()) return;
            }
            catch (MongoException)
            {
                // Retry until the bounded deadline.
            }
            await Task.Delay(100);
        }
        throw new InvalidOperationException("Disposable Gate I replica set did not become primary.");
    }

    private static bool CanBind(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
