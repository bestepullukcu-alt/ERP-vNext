using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Application.Tests.Persistence;

public sealed class DisposableStandaloneMongo : IAsyncDisposable
{
    private readonly Process _process;
    private readonly string _root;
    private readonly MongoClient _client;

    private DisposableStandaloneMongo(Process process, string root, int port, MongoClient client)
    {
        _process = process;
        _root = root;
        Port = port;
        _client = client;
    }

    public int Port { get; }
    public IMongoClient Client => _client;
    public IMongoDatabase CreateDatabase() => _client.GetDatabase("diten_platform_standalone_" + Guid.NewGuid().ToString("N"));

    public static async Task<DisposableStandaloneMongo> StartAsync(CancellationToken ct = default)
    {
        var binary = new[] { Environment.GetEnvironmentVariable("DITEN_TEST_MONGOD"), "/opt/homebrew/bin/mongod", "/usr/local/bin/mongod", "/opt/local/bin/mongod" }
            .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            ?? throw new InvalidOperationException("A local mongod binary is required; tests never download MongoDB.");
        var port = ReservePort();
        var root = Path.Combine(Path.GetTempPath(), "diten-platform-mongo-standalone-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(root, "data");
        Directory.CreateDirectory(data);
        var start = new ProcessStartInfo { FileName = binary, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { "--dbpath", data, "--port", port.ToString(), "--bind_ip", "127.0.0.1", "--nounixsocket" })
            start.ArgumentList.Add(argument);
        var process = Process.Start(start) ?? throw new InvalidOperationException("Failed to start standalone mongod.");
        var settings = MongoClientSettings.FromConnectionString($"mongodb://127.0.0.1:{port}/?directConnection=true");
        settings.ServerSelectionTimeout = TimeSpan.FromMilliseconds(500);
        var client = new MongoClient(settings);
        try
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                if (process.HasExited) throw new InvalidOperationException($"Standalone mongod exited with {process.ExitCode}.");
                try
                {
                    await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: ct);
                    return new DisposableStandaloneMongo(process, root, port, client);
                }
                catch (Exception exception) when (exception is MongoException or TimeoutException)
                {
                    await Task.Delay(100, ct);
                }
            }
            throw new TimeoutException("Standalone mongod did not accept connections.");
        }
        catch
        {
            if (!process.HasExited) process.Kill();
            process.Dispose();
            if (Directory.Exists(root)) Directory.Delete(root, true);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            foreach (var name in (await _client.ListDatabaseNames().ToListAsync()).Where(x => x.StartsWith("diten_platform_standalone_", StringComparison.Ordinal)))
                await _client.DropDatabaseAsync(name);
        }
        finally
        {
            if (!_process.HasExited) { _process.Kill(); _process.WaitForExit(TimeSpan.FromSeconds(5)); }
            _process.Dispose();
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
    }

    private static int ReservePort()
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            if (port >= 27022 && port is not 27017 and not 27018) return port;
        }
        throw new InvalidOperationException("Could not reserve standalone Mongo port >= 27022.");
    }
}
