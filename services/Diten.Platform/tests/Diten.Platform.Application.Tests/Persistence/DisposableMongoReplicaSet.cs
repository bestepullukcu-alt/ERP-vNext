using System.Diagnostics;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

[CollectionDefinition(Name)]
public sealed class DisposableMongoReplicaSetCollection
{
    public const string Name = "DisposableMongoReplicaSet";
}

/// <summary>
/// Test-owned single-node replica set. It never touches the developer Mongo ports or databases.
/// </summary>
public sealed class DisposableMongoReplicaSet : IAsyncDisposable
{
    internal const int MinimumPort = 27022;
    private readonly Process _process;
    private readonly string _rootDirectory;
    private readonly MongoClient _client;
    private readonly ConcurrentDictionary<string, byte> _ownedDatabases = new(StringComparer.Ordinal);

    private DisposableMongoReplicaSet(
        Process process,
        string rootDirectory,
        int port,
        MongoClient client)
    {
        _process = process;
        _rootDirectory = rootDirectory;
        Port = port;
        _client = client;
        ConnectionString = $"mongodb://127.0.0.1:{port}/?replicaSet=rs0&directConnection=true";
    }

    public int Port { get; }
    internal string RootDirectory => _rootDirectory;
    internal int ProcessId => _process.Id;
    public string ConnectionString { get; }
    public IMongoClient Client => _client;

    public IMongoDatabase CreateDatabase()
    {
        var name = "diten_platform_tx_" + Guid.NewGuid().ToString("N");
        _ownedDatabases.TryAdd(name, 0);
        return _client.GetDatabase(name);
    }

    public static Task<DisposableMongoReplicaSet> StartAsync(CancellationToken cancellationToken = default) =>
        StartAsync(new DynamicReplicaSetPortAllocator(), new SystemMongodProcessStarter(), new TempReplicaSetWorkspaceFactory(), cancellationToken);

    internal static async Task<DisposableMongoReplicaSet> StartAsync(
        IReplicaSetPortAllocator portAllocator,
        IMongodProcessStarter processStarter,
        IReplicaSetWorkspaceFactory workspaceFactory,
        CancellationToken cancellationToken = default)
    {
        var binary = ResolveMongodBinary();
        var port = ReplicaSetPortPolicy.Select(portAllocator);
        var root = workspaceFactory.CreateRoot();
        var data = Path.Combine(root, "data");
        Directory.CreateDirectory(data);

        var startInfo = new ProcessStartInfo
        {
            FileName = binary,
            UseShellExecute = false,
            RedirectStandardError = false,
            RedirectStandardOutput = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("--dbpath");
        startInfo.ArgumentList.Add(data);
        startInfo.ArgumentList.Add("--port");
        startInfo.ArgumentList.Add(port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--bind_ip");
        startInfo.ArgumentList.Add("127.0.0.1");
        startInfo.ArgumentList.Add("--replSet");
        startInfo.ArgumentList.Add("rs0");
        startInfo.ArgumentList.Add("--nounixsocket");
        startInfo.ArgumentList.Add("--logpath");
        startInfo.ArgumentList.Add(Path.Combine(root, "mongod.log"));

        var process = processStarter.Start(startInfo)
                      ?? throw new InvalidOperationException("Failed to start disposable mongod.");
        var directSettings = MongoClientSettings.FromConnectionString(
            $"mongodb://127.0.0.1:{port}/?directConnection=true");
#pragma warning disable CS0618
        directSettings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        directSettings.ServerSelectionTimeout = TimeSpan.FromMilliseconds(500);
        var directClient = new MongoClient(directSettings);

        try
        {
            await WaitForPingAsync(directClient, process, cancellationToken);
            var configuration = new BsonDocument
            {
                { "_id", "rs0" },
                { "members", new BsonArray { new BsonDocument { { "_id", 0 }, { "host", $"127.0.0.1:{port}" } } } }
            };
            await directClient.GetDatabase("admin").RunCommandAsync<BsonDocument>(
                new BsonDocument("replSetInitiate", configuration),
                cancellationToken: cancellationToken);

            var replicaSettings = MongoClientSettings.FromConnectionString(
                $"mongodb://127.0.0.1:{port}/?replicaSet=rs0&directConnection=true");
#pragma warning disable CS0618
            replicaSettings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
            replicaSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(1);
            var replicaClient = new MongoClient(replicaSettings);
            await WaitForPrimaryAsync(replicaClient, process, cancellationToken);
            Console.WriteLine($"DISPOSABLE_MONGO_START port={port} pid={process.Id} root={root}");
            return new DisposableMongoReplicaSet(process, root, port, replicaClient);
        }
        catch
        {
            StopProcess(process);
            TryDelete(root);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            foreach (var name in _ownedDatabases.Keys)
            {
                await _client.DropDatabaseAsync(name);
            }
        }
        finally
        {
            var pid = _process.Id;
            var exitCode = StopProcess(_process);
            var deleted = TryDelete(_rootDirectory);
            Console.WriteLine($"DISPOSABLE_MONGO_CLEANUP port={Port} pid={pid} exit={exitCode} rootDeleted={deleted}");
        }
    }

    private static string ResolveMongodBinary()
    {
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("DITEN_TEST_MONGOD"),
            "/opt/homebrew/bin/mongod",
            "/usr/local/bin/mongod",
            "/opt/local/bin/mongod"
        };
        var binary = candidates.FirstOrDefault(x =>
            !string.IsNullOrWhiteSpace(x) && File.Exists(x));
        return binary ?? throw new InvalidOperationException(
            "A local mongod binary is required; tests never download or install MongoDB.");
    }

    private static async Task WaitForPingAsync(
        IMongoClient client,
        Process process,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureRunning(process);
            try
            {
                await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(
                    new BsonDocument("ping", 1),
                    cancellationToken: cancellationToken);
                return;
            }
            catch (Exception exception) when (exception is MongoException or TimeoutException)
            {
                await Task.Delay(100, cancellationToken);
            }
        }

        throw new TimeoutException("Disposable mongod did not accept connections.");
    }

    private static async Task WaitForPrimaryAsync(
        IMongoClient client,
        Process process,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 150; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureRunning(process);
            try
            {
                var hello = await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(
                    new BsonDocument("hello", 1),
                    cancellationToken: cancellationToken);
                if (hello.TryGetValue("isWritablePrimary", out var primary) && primary.ToBoolean())
                {
                    return;
                }
            }
            catch (Exception exception) when (exception is MongoException or TimeoutException)
            {
                // Election is still in progress.
            }

            await Task.Delay(100, cancellationToken);
        }

        throw new TimeoutException("Disposable MongoDB replica set did not elect a primary.");
    }

    private static void EnsureRunning(Process process)
    {
        if (process.HasExited)
        {
            throw new InvalidOperationException($"Disposable mongod exited with code {process.ExitCode}.");
        }
    }

    private static int StopProcess(Process process)
    {
        if (!process.HasExited)
        {
            // mongod is the exact child we own. Killing an inferred process tree can
            // terminate the vstest host on Unix when process-group ancestry is reused.
            process.Kill();
            process.WaitForExit(TimeSpan.FromSeconds(5));
        }

        var exitCode = process.ExitCode;
        process.Dispose();
        return exitCode;
    }

    private static bool TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }

            return !Directory.Exists(path);
        }
        catch (IOException)
        {
            // Best effort after the process is gone; a failed cleanup remains visible in the temp directory.
            return false;
        }
    }
}

internal interface IReplicaSetPortAllocator
{
    int NextCandidate();
    bool IsInUse(int port);
}

internal interface IMongodProcessStarter
{
    Process? Start(ProcessStartInfo startInfo);
}

internal interface IReplicaSetWorkspaceFactory
{
    string CreateRoot();
}

internal static class ReplicaSetPortPolicy
{
    internal static bool IsAllowed(int port) => port >= DisposableMongoReplicaSet.MinimumPort;

    internal static int Select(IReplicaSetPortAllocator allocator)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var candidate = allocator.NextCandidate();
            if (IsAllowed(candidate) && !allocator.IsInUse(candidate)) return candidate;
        }
        throw new InvalidOperationException("Could not reserve a disposable MongoDB port >= 27022.");
    }
}

internal sealed class DynamicReplicaSetPortAllocator : IReplicaSetPortAllocator
{
    public int NextCandidate()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var candidate = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return candidate;
    }

    public bool IsInUse(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return false;
        }
        catch (SocketException) { return true; }
    }
}

internal sealed class SystemMongodProcessStarter : IMongodProcessStarter
{
    public Process? Start(ProcessStartInfo startInfo) => Process.Start(startInfo);
}

internal sealed class TempReplicaSetWorkspaceFactory : IReplicaSetWorkspaceFactory
{
    public string CreateRoot() => Path.Combine(Path.GetTempPath(), "diten-platform-mongo-rs-" + Guid.NewGuid().ToString("N"));
}
