using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.ProcurementService.Api.Tests.TestInfrastructure;

[CollectionDefinition(Name)]
public sealed class ProcurementMongoCollection : ICollectionFixture<ProcurementMongoReplicaSet>
{
    public const string Name = "ProcurementMongoReplicaSet";
}

/// <summary>
/// Procurement'ın kendi tek üyeli replika kümesi (MOD-0140 §17: "gerçek replica set, 27017–27021 ve ≥49152 dışında geçici
/// Mongo"). mongod <c>DITEN_TEST_MONGOD</c>'dan alınır (CI bunu her satıra verir); yoksa test kırmızıdır — atlanmaz, bir
/// adrese düşmez. Port <see cref="PortPolicy"/> ile seçilir. Tek, sabit adlı bir veritabanı vardır; koleksiyondaki testler
/// sırayla koştuğu için her test onu silip baştan alır (test başına Guid adlı veritabanı yok — MongoTestDatabaseGuardTests).
/// Süreç sonunda mongod durdurulur ve geçici klasör silinir. Desen: Platform <c>DisposableMongoReplicaSet</c>.
/// </summary>
public sealed class ProcurementMongoReplicaSet : IAsyncLifetime
{
    public const string DatabaseName = "procurement_supplier_migration_tests";

    private Process? _process;
    private string? _root;

    public IMongoClient Client { get; private set; } = null!;
    public int Port { get; private set; }

    /// <summary>Veritabanını siler ve boş olarak verir. Göç işi bütün kiracıları okuduğu için her test boş bir veritabanıyla başlar.</summary>
    public async Task<IMongoDatabase> FreshDatabaseAsync()
    {
        await Client.DropDatabaseAsync(DatabaseName);
        return Client.GetDatabase(DatabaseName);
    }

    public async Task InitializeAsync()
    {
        var binary = Environment.GetEnvironmentVariable("DITEN_TEST_MONGOD");
        if (string.IsNullOrWhiteSpace(binary) || !File.Exists(binary))
        {
            throw new InvalidOperationException(
                "Procurement Mongo tests need DITEN_TEST_MONGOD (path to a mongod binary); they start their own replica set " +
                "and never use an existing server.");
        }

        Port = PortPolicy.Select(FreeCandidate, IsInUse);
        _root = Path.Combine(Path.GetTempPath(), "diten-procurement-mongo-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(_root, "data");
        Directory.CreateDirectory(data);

        var start = new ProcessStartInfo { FileName = binary, UseShellExecute = false, CreateNoWindow = true };
        foreach (var arg in new[] { "--dbpath", data, "--port", Port.ToString(CultureInfo.InvariantCulture), "--bind_ip", "127.0.0.1",
                     "--replSet", "rs0", "--logpath", Path.Combine(_root, "mongod.log") })
        {
            start.ArgumentList.Add(arg);
        }

        if (!OperatingSystem.IsWindows())
        {
            start.ArgumentList.Add("--nounixsocket");
        }

        _process = Process.Start(start) ?? throw new InvalidOperationException("mongod did not start.");

        var direct = new MongoClient(Settings($"mongodb://127.0.0.1:{Port}/?directConnection=true", TimeSpan.FromMilliseconds(500)));
        await RetryAsync(() => direct.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1)), 100);
        await direct.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("replSetInitiate", new BsonDocument
        {
            { "_id", "rs0" },
            { "members", new BsonArray { new BsonDocument { { "_id", 0 }, { "host", $"127.0.0.1:{Port}" } } } }
        }));

        Client = new MongoClient(Settings($"mongodb://127.0.0.1:{Port}/?replicaSet=rs0&directConnection=true", TimeSpan.FromSeconds(2)));
        await RetryAsync(async () =>
        {
            var hello = await Client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1));
            if (!hello.GetValue("isWritablePrimary", false).ToBoolean())
            {
                throw new TimeoutException("no primary yet");
            }

            return hello;
        }, 150);
    }

    public async Task DisposeAsync()
    {
        try
        {
            if (Client is not null)
            {
                await Client.DropDatabaseAsync(DatabaseName);
            }
        }
        finally
        {
            if (_process is { HasExited: false })
            {
                _process.Kill();
                _process.WaitForExit(TimeSpan.FromSeconds(5));
            }

            _process?.Dispose();
            try
            {
                if (_root is not null && Directory.Exists(_root))
                {
                    Directory.Delete(_root, recursive: true);
                }
            }
            catch (IOException)
            {
                // mongod gittikten sonra en iyi çaba; kalan klasör geçici dizinde görünür.
            }
        }
    }

    private static MongoClientSettings Settings(string connectionString, TimeSpan serverSelection)
    {
        var settings = MongoClientSettings.FromConnectionString(connectionString);
        settings.ServerSelectionTimeout = serverSelection;
        return settings;
    }

    private async Task RetryAsync<T>(Func<Task<T>> action, int attempts)
    {
        for (var attempt = 1; ; attempt++)
        {
            if (_process is { HasExited: true })
            {
                throw new InvalidOperationException($"mongod exited with code {_process.ExitCode}.");
            }

            try
            {
                await action();
                return;
            }
            catch (Exception e) when (attempt < attempts && e is MongoException or TimeoutException)
            {
                await Task.Delay(100);
            }
        }
    }

    private static int FreeCandidate()
    {
        var candidate = Random.Shared.Next(PortPolicy.Minimum, PortPolicy.Maximum + 1);
        return candidate;
    }

    private static bool IsInUse(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return false;
        }
        catch (SocketException)
        {
            return true;
        }
    }

    /// <summary>Port kuralı: 27017–27021 (geliştiricinin ve canlı yığının Mongo'su) ve ≥49152 (işletim sisteminin geçici aralığı) dışında.</summary>
    public static class PortPolicy
    {
        public const int Minimum = 27022;
        public const int Maximum = 49151;

        public static bool IsAllowed(int port) => port is >= Minimum and <= Maximum;

        public static int Select(Func<int> candidate, Func<int, bool> inUse)
        {
            for (var attempt = 0; attempt < 50; attempt++)
            {
                var port = candidate();
                if (IsAllowed(port) && !inUse(port))
                {
                    return port;
                }
            }

            throw new InvalidOperationException($"No free port in {Minimum}–{Maximum} for the Procurement test replica set.");
        }
    }
}
