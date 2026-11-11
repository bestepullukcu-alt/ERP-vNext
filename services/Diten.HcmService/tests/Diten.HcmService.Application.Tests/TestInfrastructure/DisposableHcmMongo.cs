using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.HcmService.Application.Tests.TestInfrastructure;

/// <summary>
/// A mongod of the test's own: a fresh process on a free port at or above 27022, with a temporary dbpath, removed when the
/// test ends (owner 'Evet, düzelt' 2026-11-06). The repository test used to connect to the SHARED developer mongod on
/// 27017. CI has none there, so the project never ran in CI; locally it opened a database on the shared server. The
/// binary comes from DITEN_TEST_MONGOD (set by CI's install step), then the usual Mac / Linux paths. Tests never
/// download MongoDB. Copied from Platform's DisposableStandaloneMongo (the same port rule and Windows guard).
/// </summary>
public sealed class DisposableHcmMongo : IAsyncDisposable
{
    private readonly Process _process;
    private readonly string _root;

    private DisposableHcmMongo(Process process, string root, int port)
    {
        _process = process;
        _root = root;
        Port = port;
    }

    public int Port { get; }

    public string ConnectionString => $"mongodb://127.0.0.1:{Port}/?directConnection=true";

    public static async Task<DisposableHcmMongo> StartAsync(CancellationToken ct = default)
    {
        var binary = new[] { Environment.GetEnvironmentVariable("DITEN_TEST_MONGOD"), "/opt/homebrew/bin/mongod", "/usr/local/bin/mongod", "/opt/local/bin/mongod", "/usr/bin/mongod" }
            .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            ?? throw new InvalidOperationException("A local mongod binary is required (DITEN_TEST_MONGOD); tests never download MongoDB.");
        var port = ReservePort();
        var root = Path.Combine(Path.GetTempPath(), "diten-hcm-mongo-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(root, "data");
        Directory.CreateDirectory(data);
        var start = new ProcessStartInfo { FileName = binary, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { "--dbpath", data, "--port", port.ToString(), "--bind_ip", "127.0.0.1" })
            start.ArgumentList.Add(argument);
        // Unix-domain sockets do not exist in mongod's Windows build; the option is rejected there (exit code 2).
        if (!OperatingSystem.IsWindows())
        {
            start.ArgumentList.Add("--nounixsocket");
        }
        var process = Process.Start(start) ?? throw new InvalidOperationException("Failed to start the test's mongod.");
        var settings = MongoClientSettings.FromConnectionString($"mongodb://127.0.0.1:{port}/?directConnection=true");
        settings.ServerSelectionTimeout = TimeSpan.FromMilliseconds(500);
        var client = new MongoClient(settings);
        try
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                if (process.HasExited) throw new InvalidOperationException($"The test's mongod exited with {process.ExitCode}.");
                try
                {
                    await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: ct);
                    return new DisposableHcmMongo(process, root, port);
                }
                catch (Exception exception) when (exception is MongoException or TimeoutException)
                {
                    await Task.Delay(100, ct);
                }
            }
            throw new TimeoutException("The test's mongod did not accept connections.");
        }
        catch
        {
            if (!process.HasExited) process.Kill();
            process.Dispose();
            if (Directory.Exists(root)) Directory.Delete(root, true);
            throw;
        }
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            if (!_process.HasExited) { _process.Kill(); _process.WaitForExit(TimeSpan.FromSeconds(5)); }
        }
        finally
        {
            _process.Dispose();
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
        return ValueTask.CompletedTask;
    }

    private static int ReservePort()
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            if (port >= 27022) return port;
        }
        throw new InvalidOperationException("Could not reserve a Mongo test port >= 27022.");
    }
}
