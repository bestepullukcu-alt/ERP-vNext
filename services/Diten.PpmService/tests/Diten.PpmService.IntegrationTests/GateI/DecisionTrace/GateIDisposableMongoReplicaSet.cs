using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.PpmService.IntegrationTests.GateI.DecisionTrace;

[CollectionDefinition(CollectionName, DisableParallelization = true)]
public sealed class GateIDisposableMongoReplicaSetCollection
    : ICollectionFixture<GateIDisposableMongoReplicaSet>
{
    public const string CollectionName = GateIDisposableMongoReplicaSet.CollectionName;
}

public sealed class GateIDisposableMongoReplicaSet : IAsyncLifetime
{
    public const string CollectionName = "MOD-0117-Gate-I-disposable-Mongo";
    private const string MongoExecutable = "/opt/homebrew/bin/mongod";
    private static readonly HashSet<int> ForbiddenPorts = [27017, 27018, 27019, 27021];
    private Process? _process;
    private string? _temporaryRoot;
    private int _port;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        if (!File.Exists(MongoExecutable))
            throw new FileNotFoundException("The authorized test-owned mongod executable is unavailable.", MongoExecutable);

        _port = SelectAllowedOperatingSystemPort();
        _temporaryRoot = Path.Combine(Path.GetTempPath(), $"diten-mod0117-gate-i-{Guid.NewGuid():N}");
        var dataPath = Path.Combine(_temporaryRoot, "data");
        var logPath = Path.Combine(_temporaryRoot, "mongod.log");
        var pidPath = Path.Combine(_temporaryRoot, "mongod.pid");
        Directory.CreateDirectory(dataPath);

        var startInfo = new ProcessStartInfo(MongoExecutable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[]
        {
            "--replSet", "ppm-gate-i-rs",
            "--bind_ip", "127.0.0.1",
            "--port", _port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--dbpath", dataPath,
            "--logpath", logPath,
            "--pidfilepath", pidPath,
            "--nounixsocket"
        }) startInfo.ArgumentList.Add(argument);

        _process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The disposable Gate I mongod process did not start.");
        _ = _process.StandardOutput.ReadToEndAsync();
        _ = _process.StandardError.ReadToEndAsync();

        try
        {
            var direct = new MongoClient(
                $"mongodb://127.0.0.1:{_port}/?directConnection=true&serverSelectionTimeoutMS=1000");
            await WaitForPingAsync(direct);
            var member = new BsonDocument
            {
                ["_id"] = 0,
                ["host"] = $"127.0.0.1:{_port}"
            };
            var configuration = new BsonDocument
            {
                ["_id"] = "ppm-gate-i-rs",
                ["members"] = new BsonArray { member }
            };
            await direct.GetDatabase("admin").RunCommandAsync<BsonDocument>(
                new BsonDocument("replSetInitiate", configuration));

            ConnectionString =
                $"mongodb://127.0.0.1:{_port}/?replicaSet=ppm-gate-i-rs&serverSelectionTimeoutMS=5000";
            await WaitForPrimaryAsync(new MongoClient(ConnectionString));
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        if (_process is { HasExited: false })
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        }
        _process?.Dispose();
        _process = null;

        if (_temporaryRoot is not null && Directory.Exists(_temporaryRoot))
            Directory.Delete(_temporaryRoot, recursive: true);

        if (_temporaryRoot is not null && Directory.Exists(_temporaryRoot))
            throw new InvalidOperationException("Disposable Gate I Mongo temporary residue remains.");
        if (_port != 0 && !CanBind(_port))
            throw new InvalidOperationException("Disposable Gate I Mongo listener residue remains.");
    }

    private static int SelectAllowedOperatingSystemPort()
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
                await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
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
        Exception? last = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var hello = await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(
                    new BsonDocument("hello", 1));
                if (hello.TryGetValue("isWritablePrimary", out var writable) && writable.ToBoolean()) return;
            }
            catch (Exception exception) when (exception is MongoException or TimeoutException)
            {
                last = exception;
            }
            await Task.Delay(100);
        }
        throw new InvalidOperationException("Disposable Gate I Mongo did not become primary.", last);
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
