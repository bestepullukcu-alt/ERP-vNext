using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// BL-595 (WP-MDM-OWNED-MONGO-01) — the ONE connection source of MDM's Mongo tests: a test-owned mongod (random port,
/// temporary data directory, single-node replica set for the transactions, <c>enableTestCommands</c>), started once
/// per test process on first use and stopped when the process ends. Never the shared <c>localhost:27017</c>: before
/// BL-595 every local run wrote its <c>*_itest</c> / <c>DitenERP_*_Test_*</c> databases there.
/// <para>The mongod is resolved by Auth's rule (BL-570 FIX3/FIX4, <c>DisposableAuthMongoReplicaSet.Resolve</c>):
/// <c>DITEN_TEST_MONGOD</c> given → it must be an absolute path to an existing file, else RED
/// (<see cref="InvalidVariable"/>; an EMPTY variable is "given" too); not given → the Windows default install on
/// Windows only, else nothing. One difference, by design: Auth SKIPS by name when nothing is found (its tests carry
/// <c>[OwnedMongoFact]</c>); MDM's tests are plain <c>[Fact]</c>s, so "nothing found" is RED by name
/// (<see cref="Unavailable"/>) — never a silent pass and never the shared server, the same answer MDM's HTTP host
/// already gives. <c>DITEN_REQUIRE_OWNED_MONGO=1</c> is honoured (<see cref="RequiredButUnavailable"/>).</para>
/// <para>The one redirect (<see cref="RedirectVariable"/>): six test classes own a replica set of their own
/// (<c>AuditIntentTemporalMongoFixture</c>) and, for their lifetime, point the shared helpers at it by setting
/// <c>MDM_TEST_MONGO</c> in-process (their collection runs alone). That is honoured ONLY for a loopback server that is
/// not the shared port — another test-owned mongod. The shared port is never a redirect (CI's job-local value
/// <c>127.0.0.1:27017</c> is ignored, the owned mongod is used); any other address is RED by name
/// (<see cref="RedirectInvalid"/>).</para>
/// </summary>
public static class MdmTestMongo
{
    public const string Variable = "DITEN_TEST_MONGOD";
    public const string RequireVariable = "DITEN_REQUIRE_OWNED_MONGO";
    /// <summary>The shared developer Mongo's port — named here only so tests can refuse it.</summary>
    public const int SharedPort = 27017;
    public const string SharedPortText = "27017";
    public const string WindowsDefaultMongod = "C:/Program Files/MongoDB/Server/7.0/bin/mongod.exe";
    public const string InvalidVariable = "OWNED_TEST_MONGO_INVALID";
    public const string Unavailable = "OWNED_TEST_MONGO_UNAVAILABLE";
    public const string RequiredButUnavailable = "OWNED_TEST_MONGO_REQUIRED";
    public const string RedirectVariable = "MDM_TEST_MONGO";
    public const string RedirectInvalid = "OWNED_TEST_MONGO_REDIRECT_INVALID";
    private const string DirectoryPrefix = "diten_mdm_owned_";
    private const string ReplicaSetName = "mdm-owned";

    public sealed record Resolution(string? Executable, string? Failure);

    private static readonly Lazy<OwnedMongod> Owned = new(Start, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// The connection every MDM Mongo test uses: a test-owned redirect (see the class remarks), else this process's own
    /// replica set (started on first use; a failure is red by name).
    /// </summary>
    public static string ConnectionString =>
        RedirectFor(Environment.GetEnvironmentVariable(RedirectVariable)) ?? Owned.Value.ConnectionString;

    // The redirect rule alone (tested): absent or the shared port → none; a loopback server on another port → it;
    // anything else (a remote host, an unparsable value) → the named failure.
    internal static string? RedirectFor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        MongoUrl url;
        try { url = new MongoUrl(value); }
        catch (MongoConfigurationException) { throw new InvalidOperationException($"{RedirectInvalid}: {RedirectVariable} is not a mongodb URI."); }
        var servers = url.Servers.ToArray();
        if (servers.Any(server => server.Port == SharedPort)) return null;
        return servers.Length > 0 && servers.All(server => server.Host is "127.0.0.1" or "localhost")
            ? value
            : throw new InvalidOperationException(
                $"{RedirectInvalid}: {RedirectVariable} may only name a test-owned loopback mongod (never a remote one).");
    }

    public static Resolution ResolveOwned() => Resolve(
        Environment.GetEnvironmentVariable(Variable),
        Environment.GetEnvironmentVariable(RequireVariable) == "1", OperatingSystem.IsWindows(), File.Exists);

    // The rule alone (tested): the variable first; the Windows default ONLY on Windows; a named path that is relative or
    // missing is a failure; nothing found is a failure too — never the shared server.
    internal static Resolution Resolve(string? variable, bool required, bool windows, Func<string, bool> exists)
    {
        // An EMPTY variable is "given" (a CI variable that resolved to nothing) — invalid; only an absent one is not.
        if (variable is not null)
            return Path.IsPathFullyQualified(variable) && exists(variable)
                ? new(variable, null)
                : new(null, $"{InvalidVariable}: {Variable} names no installed mongod (an absolute path to an existing file).");
        if (windows && exists(WindowsDefaultMongod)) return new(WindowsDefaultMongod, null);
        return required
            ? new(null, $"{RequiredButUnavailable}: {RequireVariable}=1 and no installed mongod was found.")
            : new(null, $"{Unavailable}: {Variable} is not set — MDM's Mongo tests start their own mongod and never use "
                        + $"the shared localhost:{SharedPort}. Set {Variable} to an installed mongod.");
    }

    // The only way from a resolution to a connection: an owned mongod, or the named failure.
    internal static string ConnectionFor(Resolution resolution, Func<string, string> start) =>
        resolution.Executable is { } executable ? start(executable) : throw new InvalidOperationException(resolution.Failure);

    private static OwnedMongod Start()
    {
        OwnedMongod? owned = null;
        ConnectionFor(ResolveOwned(), executable =>
        {
            owned = OwnedMongod.StartAsync(executable).GetAwaiter().GetResult();
            return owned.ConnectionString;
        });
        AppDomain.CurrentDomain.ProcessExit += (_, _) => owned!.Stop();
        return owned!;
    }

    private sealed class OwnedMongod
    {
        private readonly ConcurrentQueue<string> _log = new();
        private Process _process = null!;
        private DateTime _started;
        private string _directory = null!;
        public int Port { get; private set; }
        public string ConnectionString { get; private set; } = string.Empty;

        public static async Task<OwnedMongod> StartAsync(string executable)
        {
            var owned = new OwnedMongod();
            using (var listener = new TcpListener(IPAddress.Loopback, 0))
            {
                listener.Start();
                owned.Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            if (owned.Port == SharedPort) throw new InvalidOperationException("The owned mongod drew the SHARED port 27017.");
            owned._directory = Path.Combine(Path.GetTempPath(), DirectoryPrefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(owned._directory);
            var start = new ProcessStartInfo(Path.GetFullPath(executable))
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (var argument in new[] { "--bind_ip", "127.0.0.1", "--port", owned.Port.ToString(), "--dbpath",
                         owned._directory, "--replSet", ReplicaSetName, "--setParameter", "enableTestCommands=1", "--quiet" })
                start.ArgumentList.Add(argument);
            try
            {
                owned._process = new Process { StartInfo = start, EnableRaisingEvents = true };
                owned._process.OutputDataReceived += (_, line) => owned.Capture(line.Data);
                owned._process.ErrorDataReceived += (_, line) => owned.Capture(line.Data);
                if (!owned._process.Start()) throw new InvalidOperationException("OWNED_TEST_MONGO_START_FAILED");
                owned._started = owned._process.StartTime;
                owned._process.BeginOutputReadLine();
                owned._process.BeginErrorReadLine();

                var direct = $"mongodb://127.0.0.1:{owned.Port}/?directConnection=true";
                await owned.WaitAsync(direct, async admin =>
                {
                    await admin.RunCommandAsync<BsonDocument>(new BsonDocument("replSetInitiate", new BsonDocument
                    {
                        ["_id"] = ReplicaSetName,
                        ["members"] = new BsonArray { new BsonDocument { ["_id"] = 0, ["host"] = $"127.0.0.1:{owned.Port}" } }
                    }));
                    return true;
                });
                owned.ConnectionString = $"mongodb://127.0.0.1:{owned.Port}/?replicaSet={ReplicaSetName}&directConnection=true";
                await owned.WaitAsync(owned.ConnectionString, async admin =>
                    (await admin.RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1)))
                    .GetValue("isWritablePrimary", false).ToBoolean());
                Console.WriteLine($"OWNED_MDM_MONGO_STARTED pid={owned._process.Id} port={owned.Port}");
                return owned;
            }
            catch
            {
                owned.Stop();
                throw;
            }
        }

        private async Task WaitAsync(string connection, Func<IMongoDatabase, Task<bool>> ready)
        {
            var settings = MongoClientSettings.FromConnectionString(connection);
            settings.ServerSelectionTimeout = TimeSpan.FromMilliseconds(750);
            var admin = new MongoClient(settings).GetDatabase("admin");
            var deadline = DateTime.UtcNow.AddSeconds(35);
            while (DateTime.UtcNow < deadline)
            {
                if (_process.HasExited)
                    throw new InvalidOperationException(
                        $"OWNED_TEST_MONGO_EXITED:{_process.ExitCode}{Environment.NewLine}{string.Join(Environment.NewLine, _log)}");
                try { if (await ready(admin)) return; }
                catch (Exception exception) when (exception is MongoException or TimeoutException) { }
                await Task.Delay(150);
            }
            throw new TimeoutException($"OWNED_TEST_MONGO_READY_TIMEOUT{Environment.NewLine}{string.Join(Environment.NewLine, _log)}");
        }

        // Stops exactly the process this helper started (its handle and start time) and removes only its own directory.
        public void Stop()
        {
            try
            {
                if (_process is not null && _started != default && !_process.HasExited && _process.StartTime == _started)
                {
                    _process.Kill(entireProcessTree: true);
                    _process.WaitForExit(15000);
                }
            }
            catch (InvalidOperationException) { }
            if (_directory is null) return;
            var target = Path.GetFullPath(_directory);
            var root = Path.GetFullPath(Path.GetTempPath());
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(target).StartsWith(DirectoryPrefix, StringComparison.Ordinal)) return;
            for (var attempt = 0; attempt < 20 && Directory.Exists(target); attempt++)
            {
                try { Directory.Delete(target, recursive: true); }
                catch (IOException) { Thread.Sleep(100); }
                catch (UnauthorizedAccessException) { Thread.Sleep(100); }
            }
        }

        private void Capture(string? line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            _log.Enqueue(line);
            while (_log.Count > 100) _log.TryDequeue(out _);
        }
    }
}
