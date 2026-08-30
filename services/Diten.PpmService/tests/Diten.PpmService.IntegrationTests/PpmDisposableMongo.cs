using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.PpmService.IntegrationTests;


public sealed class PpmDisposableMongo : IAsyncLifetime
{
    public const string DatabaseName = "diten_ppm_integration_tests";
    private const string ReplicaSetName = "ppm-test-rs";
    private const string MongoExecutable = "/opt/homebrew/bin/mongod";
    private static readonly HashSet<int> ForbiddenPorts = [27017, 27018, 27019, 27020, 27021];
    private readonly List<Process> _processes = [];
    private readonly List<string> _temporaryRoots = [];
    private readonly List<int> _ports = [];

    public string ReplicaSetConnectionString { get; private set; } = string.Empty;
    public string StandaloneConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        if (!File.Exists(MongoExecutable))
            throw new FileNotFoundException("The test-owned mongod executable is unavailable.", MongoExecutable);

        try
        {
            var replicaPort = SelectAllowedPort();
            var standalonePort = SelectAllowedPort();
            var replica = StartMongo(replicaPort, ReplicaSetName);
            _ = StartMongo(standalonePort, null);

            var direct = new MongoClient(
                $"mongodb://127.0.0.1:{replicaPort}/?directConnection=true&serverSelectionTimeoutMS=1000");
            await WaitForPingAsync(direct);
            await direct.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument
            {
                ["replSetInitiate"] = new BsonDocument
                {
                    ["_id"] = ReplicaSetName,
                    ["members"] = new BsonArray
                    {
                        new BsonDocument { ["_id"] = 0, ["host"] = $"127.0.0.1:{replicaPort}" }
                    }
                }
            });

            ReplicaSetConnectionString =
                $"mongodb://127.0.0.1:{replicaPort}/?replicaSet={ReplicaSetName}&serverSelectionTimeoutMS=5000";
            StandaloneConnectionString =
                $"mongodb://127.0.0.1:{standalonePort}/?directConnection=true&serverSelectionTimeoutMS=5000";
            await WaitForPrimaryAsync(new MongoClient(ReplicaSetConnectionString));
            await WaitForPingAsync(new MongoClient(StandaloneConnectionString));

            _ = replica;
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        if (!string.IsNullOrWhiteSpace(ReplicaSetConnectionString))
        {
            try
            {
                await PpmMongoTestDatabase.ResetAsync(
                    PpmMongoTestDatabase.Open(ReplicaSetConnectionString));
            }
            catch (MongoException)
            {
                // Process cleanup below remains authoritative when Mongo is already unavailable.
            }
        }

        foreach (var process in _processes)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }
            process.Dispose();
        }
        _processes.Clear();

        foreach (var root in _temporaryRoots)
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);

        if (_temporaryRoots.Any(Directory.Exists))
            throw new InvalidOperationException("Disposable PPM Mongo temporary residue remains.");
        if (_ports.Any(port => !CanBind(port)))
            throw new InvalidOperationException("Disposable PPM Mongo listener residue remains.");
    }

    private Process StartMongo(int port, string? replicaSet)
    {
        var root = Path.Combine(Path.GetTempPath(), $"diten-mod0117-mongo-{port}");
        var data = Path.Combine(root, "data");
        Directory.CreateDirectory(data);
        _temporaryRoots.Add(root);
        _ports.Add(port);

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
            "--port", port.ToString(CultureInfo.InvariantCulture),
            "--dbpath", data,
            "--logpath", Path.Combine(root, "mongod.log"),
            "--pidfilepath", Path.Combine(root, "mongod.pid"),
            "--nounixsocket"
        }) start.ArgumentList.Add(argument);
        if (replicaSet is not null)
        {
            start.ArgumentList.Add("--replSet");
            start.ArgumentList.Add(replicaSet);
        }

        var process = Process.Start(start)
            ?? throw new InvalidOperationException("Disposable PPM mongod did not start.");
        _ = process.StandardOutput.ReadToEndAsync();
        _ = process.StandardError.ReadToEndAsync();
        _processes.Add(process);
        return process;
    }

    private int SelectAllowedPort()
    {
        for (var attempt = 0; attempt < 32; attempt++)
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            if (port >= 27022 && !ForbiddenPorts.Contains(port) && !_ports.Contains(port)) return port;
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
                await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
                return;
            }
            catch (Exception exception) when (exception is MongoException or TimeoutException)
            {
                last = exception;
                await Task.Delay(100);
            }
        }
        throw new InvalidOperationException("Disposable PPM Mongo did not accept connections.", last);
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
        throw new InvalidOperationException("Disposable PPM replica set did not become primary.");
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
