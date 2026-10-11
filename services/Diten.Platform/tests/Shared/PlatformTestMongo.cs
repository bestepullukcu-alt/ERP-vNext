using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Tests.Shared;

/*
 * THE ONE SOURCE OF THE PLATFORM TESTS' MONGO (WP-CT-LANE-MAIN-SYNC-01, CT decision C; BL-595 + main's Q131a).
 *
 * Linked (not copied) into Diten.Platform.Application.Tests, .BackgroundJobs.Tests and .Eventing.Tests — they do not
 * reference each other, and one rule in three copies drifts. In order:
 *
 *   1. DITEN_PLATFORM_TEST_MONGO_URI given → that URI, through main's fail-closed check UNCHANGED (Q131a, PR #134):
 *      plain mongodb://, no credentials, every host loopback, no port in 27017–27021 (an omitted port counts as 27017),
 *      and an empty value is "not set" — red. The MVP-6 lane scripts (scripts/test-env/mvp6-test-mongo-env.sh) keep
 *      working exactly as before.
 *   2. Otherwise DITEN_TEST_MONGOD (Auth's rule, BL-570): an absolute path to an existing mongod → a mongod THIS test
 *      process starts — a single-node replica set (transactions work) on a free loopback port outside 27017–27021, in a
 *      temporary directory, stopped at process exit. Given but empty / relative / missing → OWNED_TEST_MONGO_INVALID, red.
 *      CI sets this variable on every service-tests row, so no workflow mongod is needed for the Platform rows.
 *   3. Neither → the Windows default install on Windows only; else OWNED_TEST_MONGO_UNAVAILABLE, red (the Platform
 *      harness never skipped: "a missing Mongo is a broken dev environment").
 *   It NEVER falls back to 27017 or to any address it was not given or did not start.
 *
 * CHILD PROCESSES. Once a process owns a mongod it publishes the address in DITEN_PLATFORM_TEST_MONGO_URI itself, so a
 * child test process (the lock proofs start and KILL some) inherits it through rule 1 and never starts — and orphans —
 * a mongod of its own.
 */
internal static class PlatformTestMongo
{
    public const string UriVariable = "DITEN_PLATFORM_TEST_MONGO_URI";
    public const string MongodVariable = "DITEN_TEST_MONGOD";
    public const string WindowsDefaultMongod = "C:/Program Files/MongoDB/Server/7.0/bin/mongod.exe";
    public const string InvalidVariable = "OWNED_TEST_MONGO_INVALID";
    public const string Unavailable = "OWNED_TEST_MONGO_UNAVAILABLE";

    // The operational band (main's Q131a). Neither a given URI nor an owned mongod may use it.
    private static readonly HashSet<int> ProtectedPorts = [27017, 27018, 27019, 27020, 27021];

    public sealed record Choice(string? Uri, string? Executable, string? Failure);

    /// <summary>
    /// Main's Q131a check, verbatim (moved here from PlatformMongoTestConnection so all three projects share it). Pure:
    /// its tests never change the process environment. Messages never repeat the URI.
    /// </summary>
    internal static string ValidateUri(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"{UriVariable} is not set. Platform Mongo tests fail closed: point it at a lane-owned "
                + "loopback MongoDB (scripts/test-env/mvp6-test-mongo-env.sh). There is no fallback port.");
        }

        if (connectionString.StartsWith("mongodb+srv://", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{UriVariable} must be a plain mongodb:// loopback URI.");
        }

        MongoUrl url;
        try
        {
            url = new MongoUrl(connectionString);
        }
        catch (Exception ex) when (ex is MongoConfigurationException or FormatException or ArgumentException)
        {
            throw new InvalidOperationException($"{UriVariable} is not a valid MongoDB URI.", ex);
        }

        if (url.Username is not null)
        {
            throw new InvalidOperationException($"{UriVariable} must not carry credentials.");
        }

        var servers = url.Servers?.ToArray() ?? [];
        if (servers.Length == 0)
        {
            throw new InvalidOperationException($"{UriVariable} must name a loopback host and the lane's port.");
        }

        foreach (var server in servers)
        {
            if (server.Host is not ("127.0.0.1" or "localhost" or "::1" or "[::1]"))
            {
                throw new InvalidOperationException($"{UriVariable} must point at a loopback host.");
            }

            if (ProtectedPorts.Contains(server.Port))
            {
                throw new InvalidOperationException(
                    $"{UriVariable} points at protected MongoDB port {server.Port}. Use the lane's own port.");
            }
        }

        return connectionString;
    }

    /// <summary>The decision alone (tested): a given URI, else an installed mongod to start, else a failure.</summary>
    internal static Choice Choose(string? uri, string? mongod, bool windows, Func<string, bool> exists)
    {
        // A GIVEN URI is the answer — valid or red; an empty one is given too (main: "is not set", red).
        if (uri is not null)
        {
            try
            {
                return new Choice(ValidateUri(uri), null, null);
            }
            catch (InvalidOperationException invalid)
            {
                return new Choice(null, null, invalid.Message);
            }
        }

        // An EMPTY DITEN_TEST_MONGOD is "given" (a CI variable that resolved to nothing): invalid, red.
        if (mongod is not null)
        {
            return Path.IsPathFullyQualified(mongod) && exists(mongod)
                ? new Choice(null, mongod, null)
                : new Choice(null, null, $"{InvalidVariable}: {MongodVariable} names no installed mongod (an absolute path to an existing file).");
        }

        if (windows && exists(WindowsDefaultMongod))
        {
            return new Choice(null, WindowsDefaultMongod, null);
        }

        return new Choice(null, null,
            $"{Unavailable}: neither {UriVariable} nor {MongodVariable} is set — the Platform Mongo tests use a lane URI or a "
            + "mongod they start themselves, never the shared instance on 27017. Set "
            + $"{MongodVariable} to an installed mongod (e.g. /opt/homebrew/bin/mongod).");
    }

    /// <summary>A choice turned into an address: a failure is thrown (red, by name), a URI is used, a mongod is started.</summary>
    internal static async Task<string> AddressAsync(Choice choice, Func<string, Task<string>> start)
    {
        if (choice.Failure is { } failure)
        {
            throw new InvalidOperationException(failure);
        }

        return choice.Uri ?? await start(choice.Executable!).ConfigureAwait(false);
    }

    /// <summary>The choice for THIS process's environment.</summary>
    public static Choice ChooseFromEnvironment() => Choose(
        Environment.GetEnvironmentVariable(UriVariable),
        Environment.GetEnvironmentVariable(MongodVariable),
        OperatingSystem.IsWindows(),
        File.Exists);

    private static readonly object Gate = new();
    private static Task<string>? _processWide;

    /// <summary>
    /// One address for the whole test process: the given URI, or ONE owned mongod started on first use and stopped at
    /// exit (its address then published in <see cref="UriVariable"/> for child processes).
    /// </summary>
    public static Task<string> ConnectionStringAsync()
    {
        lock (Gate)
        {
            return _processWide ??= AddressAsync(ChooseFromEnvironment(), async executable =>
            {
                var owned = await OwnedMongod.StartAsync(executable).ConfigureAwait(false);
                AppDomain.CurrentDomain.ProcessExit += (_, _) => owned.Dispose();
                Environment.SetEnvironmentVariable(UriVariable, owned.ConnectionString);
                Console.WriteLine($"PLATFORM_OWNED_MONGO_STARTED port={owned.Port} pid={owned.ProcessId}");
                return owned.ConnectionString;
            });
        }
    }

    /// <summary>A mongod this process started: a single-node replica set on a free loopback port, in a temporary directory.</summary>
    public sealed class OwnedMongod : IDisposable, IAsyncDisposable
    {
        private readonly Process _process;
        private readonly string _root;

        private OwnedMongod(Process process, string root, int port)
        {
            _process = process;
            _root = root;
            Port = port;
            ConnectionString = $"mongodb://127.0.0.1:{port}/?replicaSet=rs0&directConnection=true";
        }

        public int Port { get; }
        public int ProcessId => _process.Id;
        public string ConnectionString { get; }

        public static async Task<OwnedMongod> StartAsync(string executable)
        {
            var port = FreePort();
            var root = Path.Combine(Path.GetTempPath(), "diten-platform-owned-mongo-" + Guid.NewGuid().ToString("N"));
            var data = Path.Combine(root, "data");
            Directory.CreateDirectory(data);
            var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
            foreach (var argument in new[]
                     {
                         "--dbpath", data, "--port", port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                         "--bind_ip", "127.0.0.1", "--replSet", "rs0", "--logpath", Path.Combine(root, "mongod.log")
                     })
            {
                start.ArgumentList.Add(argument);
            }

            if (!OperatingSystem.IsWindows())
            {
                start.ArgumentList.Add("--nounixsocket");
            }

            var process = Process.Start(start) ?? throw new InvalidOperationException("The test-owned mongod did not start.");
            var owned = new OwnedMongod(process, root, port);
            try
            {
                var direct = Client($"mongodb://127.0.0.1:{port}/?directConnection=true");
                await WaitAsync(process, async () =>
                {
                    await direct.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1)).ConfigureAwait(false);
                    return true;
                }).ConfigureAwait(false);
                await direct.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("replSetInitiate", new BsonDocument
                {
                    { "_id", "rs0" },
                    { "members", new BsonArray { new BsonDocument { { "_id", 0 }, { "host", $"127.0.0.1:{port}" } } } }
                })).ConfigureAwait(false);
                var replica = Client(owned.ConnectionString);
                await WaitAsync(process, async () =>
                    (await replica.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1)).ConfigureAwait(false))
                    .GetValue("isWritablePrimary", false).ToBoolean()).ConfigureAwait(false);
                return owned;
            }
            catch
            {
                owned.Dispose();
                throw;
            }
        }

        private static IMongoClient Client(string connectionString)
        {
            var settings = MongoClientSettings.FromConnectionString(connectionString);
            settings.ServerSelectionTimeout = TimeSpan.FromMilliseconds(500);
            return new MongoClient(settings);
        }

        private static async Task WaitAsync(Process process, Func<Task<bool>> ready)
        {
            for (var attempt = 0; attempt < 300; attempt++)
            {
                if (process.HasExited)
                {
                    throw new InvalidOperationException($"The test-owned mongod exited with code {process.ExitCode}.");
                }

                try
                {
                    if (await ready().ConfigureAwait(false)) return;
                }
                catch (Exception exception) when (exception is MongoException or TimeoutException)
                {
                }

                await Task.Delay(100).ConfigureAwait(false);
            }

            throw new TimeoutException("The test-owned mongod did not become a writable primary.");
        }

        private static int FreePort()
        {
            for (var attempt = 0; attempt < 50; attempt++)
            {
                using var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                if (port >= 27022 && !ProtectedPorts.Contains(port)) return port;
            }

            throw new InvalidOperationException("No free loopback port outside the protected MongoDB band.");
        }

        public void Dispose()
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                    _process.WaitForExit(5000);
                }
            }
            catch (InvalidOperationException)
            {
            }
            finally
            {
                _process.Dispose();
                try
                {
                    if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
                }
                catch (IOException)
                {
                }
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
