using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Diten.PvgService.Application.RegPvBase;
using Diten.PvgService.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.PvgService.Api.Tests.Persistence;

// WP-0230-DL10 (AC-35 (f)): the Mongo test harness. Every Mongo test of this project talks to a mongod that the test run
// starts itself, on a random loopback port with a temporary data directory, and stops again. The development mongod
// (port 27017) is never used: the harness refuses that port, and a missing mongod binary is an error, never a skip.

// All Mongo tests that share the one replica set run one after the other in this collection.
[CollectionDefinition(Name)]
public sealed class PvgSharedMongoCollection : ICollectionFixture<PvgMongoReplicaSetFixture>
{
    public const string Name = "Pvg shared mongo";
}

// Tests that stop a mongod or set a fail point start their own and never touch the shared one; they run one after the
// other, and not beside any other collection.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PvgDedicatedMongoCollection
{
    public const string Name = "Pvg dedicated mongo";
}

// The pure part of the harness: what it accepts, without starting anything.
internal static class PvgMongoTestHarness
{
    public const string BinaryVariable = "DITEN_TEST_MONGOD";
    public const string SharedDatabaseName = "diten_pvg_intake_tests";
    public const string ReplicaSetName = "pvgintake";
    public const int ForbiddenPort = 27017;

    public static string ResolveBinary(string? configuredPath, Func<string, bool> fileExists)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidOperationException(
                BinaryVariable + " is not set: the PVG Mongo tests need an installed mongod and are never skipped.");
        }

        if (!Path.IsPathFullyQualified(configuredPath) || !fileExists(configuredPath))
        {
            throw new InvalidOperationException(
                BinaryVariable + " names no installed mongod (an absolute path to an existing file is required).");
        }

        return configuredPath;
    }

    public static int ResolvePort(int requestedPort)
    {
        if (requestedPort == ForbiddenPort)
        {
            throw new InvalidOperationException("Port 27017 is the development mongod; the PVG Mongo tests never use it.");
        }

        if (requestedPort is < 1024 or > 65535)
        {
            throw new InvalidOperationException("The PVG Mongo tests use an unprivileged loopback port.");
        }

        return requestedPort;
    }

    // "host:port" or a mongodb:// address. An address without a port means the default port, which is refused too.
    public static int ResolveAddressPort(string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);

        var text = address.Contains("://", StringComparison.Ordinal) ? address : "mongodb://" + address;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("The configured Mongo test address cannot be read.");
        }

        return ResolvePort(uri.Port < 0 ? ForbiddenPort : uri.Port);
    }
}

// One mongod process this test run started. It is stopped through its own PID, after checking that the process is still
// the one that was started; nothing is ever stopped by name.
internal sealed class PvgOwnedMongod : IAsyncDisposable
{
    private Process? _process;
    private DateTime _startedAt;
    private string? _root;
    private bool _stopped;

    private PvgOwnedMongod(bool replicaSet)
    {
        ReplicaSet = replicaSet;
    }

    public bool ReplicaSet { get; }

    public int Port { get; private set; }

    public int ProcessId { get; private set; }

    public string ConnectionString =>
        ReplicaSet
            ? $"mongodb://127.0.0.1:{Port}/?replicaSet={PvgMongoTestHarness.ReplicaSetName}"
            : $"mongodb://127.0.0.1:{Port}/?directConnection=true";

    public static async Task<PvgOwnedMongod> StartAsync(bool replicaSet)
    {
        var binary = PvgMongoTestHarness.ResolveBinary(
            Environment.GetEnvironmentVariable(PvgMongoTestHarness.BinaryVariable),
            File.Exists);

        var mongod = new PvgOwnedMongod(replicaSet);
        try
        {
            await mongod.LaunchAsync(binary);
        }
        catch
        {
            await mongod.DisposeAsync();
            throw;
        }

        return mongod;
    }

    // A client of the test's own, direct to this mongod, for raw reads and the fail point command.
    public IMongoClient CreateRawClient()
    {
        var settings = MongoClientSettings.FromConnectionString(ConnectionString);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(10);
        return new MongoClient(settings);
    }

    private async Task LaunchAsync(string binary)
    {
        int port;
        using (var listener = new TcpListener(IPAddress.Loopback, 0))
        {
            listener.Start();
            port = ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        Port = PvgMongoTestHarness.ResolvePort(port);
        _root = Path.Combine(Path.GetTempPath(), "diten-pvg-intake-mongo-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(_root, "data");
        Directory.CreateDirectory(data);

        var start = new ProcessStartInfo(binary)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        var arguments = new List<string>
        {
            "--bind_ip", "127.0.0.1",
            "--port", Port.ToString(),
            "--dbpath", data,
            "--logpath", Path.Combine(_root, "mongod.log"),
            "--nounixsocket",
            "--wiredTigerCacheSizeGB", "0.25",
            "--setParameter", "enableTestCommands=1"
        };
        if (ReplicaSet)
        {
            arguments.AddRange(["--replSet", PvgMongoTestHarness.ReplicaSetName, "--oplogSize", "128"]);
        }

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        _process = Process.Start(start) ?? throw new InvalidOperationException("The test mongod did not start.");
        _startedAt = _process.StartTime;
        ProcessId = _process.Id;
        _ = _process.StandardOutput.ReadToEndAsync();
        _ = _process.StandardError.ReadToEndAsync();
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

        var direct = MongoClientSettings.FromConnectionString($"mongodb://127.0.0.1:{Port}/?directConnection=true");
        direct.ServerSelectionTimeout = TimeSpan.FromSeconds(2);
        var bootstrap = new MongoClient(direct);
        try
        {
            var admin = bootstrap.GetDatabase("admin");
            await WaitAsync(async () =>
            {
                await admin.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
                return true;
            });

            if (ReplicaSet)
            {
                await admin.RunCommandAsync<BsonDocument>(new BsonDocument("replSetInitiate", new BsonDocument
                {
                    { "_id", PvgMongoTestHarness.ReplicaSetName },
                    { "members", new BsonArray { new BsonDocument { { "_id", 0 }, { "host", $"127.0.0.1:{Port}" } } } }
                }));
                await WaitAsync(async () =>
                    (await admin.RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1)))
                    .GetValue("isWritablePrimary", false).ToBoolean());
            }
        }
        finally
        {
            ClusterRegistry.Instance.UnregisterAndDisposeCluster(bootstrap.Cluster);
        }
    }

    private async Task WaitAsync(Func<Task<bool>> ready)
    {
        var deadline = DateTime.UtcNow.AddSeconds(40);
        while (DateTime.UtcNow < deadline)
        {
            if (_process is null || _process.HasExited)
            {
                throw new InvalidOperationException("The test mongod exited while starting.");
            }

            try
            {
                if (await ready())
                {
                    return;
                }
            }
            catch (Exception exception) when (exception is MongoException or TimeoutException)
            {
                // Not up yet.
            }

            await Task.Delay(150);
        }

        throw new TimeoutException("The test mongod did not become ready.");
    }

    // Stops the mongod: a shutdown command first, the process kill (own PID, start time checked) as the fallback.
    public async Task StopAsync()
    {
        if (_stopped || _process is null)
        {
            return;
        }

        if (!_process.HasExited)
        {
            var direct = MongoClientSettings.FromConnectionString($"mongodb://127.0.0.1:{Port}/?directConnection=true");
            direct.ServerSelectionTimeout = TimeSpan.FromSeconds(2);
            var client = new MongoClient(direct);
            try
            {
                await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(
                    new BsonDocument { { "shutdown", 1 }, { "force", true } });
            }
            catch (Exception)
            {
                // The connection is dropped by the shutdown; that is the expected outcome.
            }
            finally
            {
                ClusterRegistry.Instance.UnregisterAndDisposeCluster(client.Cluster);
            }

            if (!_process.WaitForExit(20_000))
            {
                KillOwnProcess();
            }
        }

        _stopped = true;
        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
    }

    private void KillOwnProcess()
    {
        if (_process is { HasExited: false } && _process.StartTime == _startedAt)
        {
            _process.Kill();
            _process.WaitForExit(20_000);
        }
    }

    private void OnProcessExit(object? sender, EventArgs e) => KillOwnProcess();

    public async ValueTask DisposeAsync()
    {
        try
        {
            await StopAsync();
        }
        finally
        {
            KillOwnProcess();
            _process?.Dispose();
            _process = null;
            if (_root is not null && Directory.Exists(_root))
            {
                for (var attempt = 0; attempt < 20; attempt++)
                {
                    try
                    {
                        Directory.Delete(_root, recursive: true);
                        break;
                    }
                    catch (IOException)
                    {
                        await Task.Delay(250);
                    }
                }
            }
        }
    }
}

// The one replica set the shared collection uses. It also runs the schema bootstrap once, like a host start would.
public sealed class PvgMongoReplicaSetFixture : IAsyncLifetime
{
    private PvgOwnedMongod? _mongod;
    private IMongoClient? _rawClient;

    internal string ConnectionString => _mongod!.ConnectionString;

    internal ServiceProvider Services { get; private set; } = null!;

    internal IPvgIntakeDraftStore Store => Services.GetRequiredService<IPvgIntakeDraftStore>();

    internal IMongoDatabase RawDatabase => _rawClient!.GetDatabase(PvgMongoTestHarness.SharedDatabaseName);

    public async Task InitializeAsync()
    {
        _mongod = await PvgOwnedMongod.StartAsync(replicaSet: true);
        _rawClient = _mongod.CreateRawClient();
        Services = PvgMongoTestStores.BuildServices(ConnectionString);
        await PvgMongoTestStores.RunBootstrapAsync(Services);
    }

    public async Task DisposeAsync()
    {
        if (Services is not null)
        {
            await Services.DisposeAsync();
        }

        if (_rawClient is not null)
        {
            ClusterRegistry.Instance.UnregisterAndDisposeCluster(_rawClient.Cluster);
        }

        if (_mongod is not null)
        {
            await _mongod.DisposeAsync();
        }
    }

    internal async Task<BsonDocument?> RawDraftAsync(string tenantId, string intakeDraftId) =>
        await RawDatabase
            .GetCollection<BsonDocument>(Diten.PvgService.Infrastructure.RegPvBase.PvgIntakeDraftIndexCatalog.CollectionName)
            .Find(new BsonDocument { { "TenantId", tenantId }, { "IntakeDraftId", intakeDraftId } })
            .FirstOrDefaultAsync();
}

// Builds the Mongo store the way a host does, through the persistence project's registration extension.
internal static class PvgMongoTestStores
{
    public static ServiceProvider BuildServices(string connectionString, Action<MongoClientSettings>? configureClient = null)
    {
        var settings = new Dictionary<string, string?>
        {
            [PvgPersistenceServiceCollectionExtensions.ConnectionStringKey] = connectionString,
            [PvgPersistenceServiceCollectionExtensions.DatabaseNameKey] = PvgMongoTestHarness.SharedDatabaseName
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var environment = new PvgTestHostEnvironment(Environments.Development);

        var services = new ServiceCollection();
        if (configureClient is null)
        {
            services.AddPvgMongoIntakeStore(configuration, environment);
        }
        else
        {
            services.AddPvgMongoIntakeStore(configuration, environment, configureClient);
        }

        return services.BuildServiceProvider();
    }

    public static PvgMongoSchemaBootstrap Bootstrap(IServiceProvider services) =>
        services.GetServices<IHostedService>().OfType<PvgMongoSchemaBootstrap>().Single();

    public static Task RunBootstrapAsync(IServiceProvider services) =>
        Bootstrap(services).PrepareSchemaAsync(CancellationToken.None);
}

internal sealed class PvgTestHostEnvironment(string environmentName) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = environmentName;

    public string ApplicationName { get; set; } = "Diten.PvgService.Api.Tests";

    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
