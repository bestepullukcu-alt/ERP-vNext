using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// BL-595 (WP-MDM-OWNED-MONGO-01) — the ONE connection source of MDM's Mongo tests: a test-owned mongod (random port,
/// temporary data directory, single-node replica set for the transactions, <c>enableTestCommands</c>, bound to
/// 127.0.0.1 only), started once per test process on first use and stopped when the process ends. Never the shared
/// <c>localhost:27017</c>: before BL-595 every local run wrote its <c>*_itest</c> / <c>DitenERP_*_Test_*</c> databases
/// there.
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
/// not the shared port — another test-owned mongod. Every host must be loopback (else RED by name,
/// <see cref="RedirectInvalid"/>, a default port included); then a list naming the shared port is ignored (CI's
/// job-local value <c>127.0.0.1:27017</c>: the owned mongod is used).</para>
/// <para>FIX1: the start runs off the test's synchronization context (no xunit worker is held waiting for itself);
/// a port lost between its pick and mongod's bind is retried; every owned directory carries an owner record, and a
/// start sweeps what an earlier, killed run left behind (only its own prefix, only a mongod whose PID AND start time
/// match a record whose test host is gone — never by name).</para>
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
    public const string DirectoryPrefix = "diten_mdm_owned_";
    public const string OwnerFile = "owner.json";
    public const int StartAttempts = 3;
    public static readonly TimeSpan RecordlessGrace = TimeSpan.FromHours(1);
    private const string ReplicaSetName = "mdm-owned";

    public sealed record Resolution(string? Executable, string? Failure);

    private static readonly Lazy<OwnedMongod> Owned = new(Start, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// The connection every MDM Mongo test uses: a test-owned redirect (see the class remarks), else this process's own
    /// replica set (started on first use; a failure is red by name).
    /// </summary>
    public static string ConnectionString =>
        RedirectFor(Environment.GetEnvironmentVariable(RedirectVariable)) ?? OwnedConnectionString;

    /// <summary>This process's own replica set — never a redirect.</summary>
    public static string OwnedConnectionString => Owned.Value.ConnectionString;

    /// <summary>Whether the owned mongod's stop is registered for process exit (set where the handler is added).</summary>
    public static bool StopRegisteredForExit { get; private set; }

    // The redirect rule alone (tested): absent → none; every host loopback, else the named failure (a default port too);
    // then a list naming the shared port → none; else the value.
    internal static string? RedirectFor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        MongoUrl url;
        try { url = new MongoUrl(value); }
        catch (MongoConfigurationException) { throw new InvalidOperationException($"{RedirectInvalid}: {RedirectVariable} is not a mongodb URI."); }
        var servers = url.Servers.ToArray();
        if (servers.Length == 0 || !servers.All(server => server.Host is "127.0.0.1" or "localhost"))
            throw new InvalidOperationException(
                $"{RedirectInvalid}: {RedirectVariable} may only name a test-owned loopback mongod (never a remote one).");
        return servers.Any(server => server.Port == SharedPort) ? null : value;
    }

    public static Resolution ResolveOwned() =>
        ResolveFrom(Environment.GetEnvironmentVariable, OperatingSystem.IsWindows(), File.Exists);

    // The plumbing from the process environment to the rule (tested: REQUIRE is "1" exactly).
    internal static Resolution ResolveFrom(Func<string, string?> environment, bool windows, Func<string, bool> exists) =>
        Resolve(environment(Variable), environment(RequireVariable) == "1", windows, exists);

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

    /// <summary>
    /// FIX1 (2): waits for an async start from a synchronous caller WITHOUT the caller's synchronization context. The
    /// Lazy runs on an xunit worker whose context (MaxConcurrencySyncContext) would receive the start's continuations;
    /// with every worker blocked on the same Lazy (2 vCPU CI runner) nothing would run them. On the thread pool the
    /// continuations never need a worker.
    /// </summary>
    internal static T WaitOffContext<T>(Func<Task<T>> start) => Task.Run(start).GetAwaiter().GetResult();

    /// <summary>The mongod command line — loopback only (tested): an unauthenticated mongod with test commands.</summary>
    internal static IReadOnlyList<string> MongodArguments(int port, string dataDirectory) =>
    [
        "--bind_ip", "127.0.0.1", "--port", port.ToString(System.Globalization.CultureInfo.InvariantCulture),
        "--dbpath", dataDirectory, "--replSet", ReplicaSetName, "--setParameter", "enableTestCommands=1", "--quiet"
    ];

    private static OwnedMongod Start()
    {
        OwnedMongod? owned = null;
        ConnectionFor(ResolveOwned(), executable =>
        {
            Sweep(Path.GetTempPath(), ProcessStartOf, StopVerified);
            owned = WaitOffContext(() => OwnedMongod.StartWithRetryAsync(executable, PickPort, StartAttempts));
            return owned.ConnectionString;
        });
        AppDomain.CurrentDomain.ProcessExit += (_, _) => owned!.Stop();
        StopRegisteredForExit = true;
        return owned!;
    }

    private static int PickPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    // ------------------------------------------------------------------------------------------------- FIX1 (3) sweep

    /// <summary>What a start writes into its directory: its mongod and its test host, each by PID and start time.</summary>
    public sealed record OwnerRecord(int? MongodPid, DateTime? MongodStartedUtc, int HostPid, DateTime HostStartedUtc);

    public enum SweepAction { Keep, DeleteDirectory, StopMongodAndDeleteDirectory }

    /// <summary>
    /// The sweep rule alone (tested). <paramref name="startOf"/> answers a live process's start time (UTC), or null when
    /// no process has that PID. A process is "the recorded one" only when its PID AND start time match.
    /// </summary>
    internal static SweepAction Decide(OwnerRecord? record, Func<int, DateTime?> startOf, TimeSpan age, int? lockPid = null)
    {
        // No record: a start writes it the moment it creates the directory, so a recordless directory is one from before
        // the record existed (the older helper, c3a9f00c7) or a write that failed. Its process, if any, is never touched:
        // nothing verifies it. The directory goes only when it is older than the grace AND no live process holds it —
        // a running mongod keeps its PID in <dbpath>/mongod.lock (emptied on a clean stop), so an older-code run that is
        // still going, however long, keeps its directory.
        if (record is null)
            return age > RecordlessGrace && !(lockPid is { } holder && startOf(holder) is not null)
                ? SweepAction.DeleteDirectory
                : SweepAction.Keep;
        var hostAlive = Same(startOf(record.HostPid), record.HostStartedUtc);
        var mongodAlive = record is { MongodPid: { } pid, MongodStartedUtc: { } started } && Same(startOf(pid), started);
        if (hostAlive) return SweepAction.Keep; // another live run's server
        return mongodAlive ? SweepAction.StopMongodAndDeleteDirectory : SweepAction.DeleteDirectory;
    }

    private static bool Same(DateTime? actual, DateTime recorded) =>
        actual is { } value && Math.Abs((value - recorded).TotalSeconds) < 1;

    /// <summary>
    /// Sweeps <paramref name="root"/>: only directories named with <see cref="DirectoryPrefix"/>, each by its owner
    /// record and <see cref="Decide"/>. <paramref name="stop"/> stops a process only after verifying PID + start time.
    /// Returns what it did, for the test.
    /// </summary>
    internal static IReadOnlyList<(string Directory, SweepAction Action)> Sweep(
        string root, Func<int, DateTime?> startOf, Func<int, DateTime, bool> stop)
    {
        var done = new List<(string, SweepAction)>();
        if (!Directory.Exists(root)) return done;
        foreach (var directory in Directory.EnumerateDirectories(root, DirectoryPrefix + "*"))
        {
            var record = ReadOwner(directory);
            var action = Decide(record, startOf, DateTime.UtcNow - Directory.GetCreationTimeUtc(directory), LockPid(directory));
            if (action == SweepAction.StopMongodAndDeleteDirectory
                && !stop(record!.MongodPid!.Value, record.MongodStartedUtc!.Value))
                action = SweepAction.Keep; // it could not be verified or stopped: leave it
            if (action != SweepAction.Keep) DeleteOwnDirectory(root, directory);
            done.Add((directory, action));
        }
        return done;
    }

    // The PID a running mongod wrote into its dbpath's mongod.lock (empty after a clean stop), or null.
    internal static int? LockPid(string directory)
    {
        try
        {
            return int.TryParse(File.ReadAllText(Path.Combine(directory, "mongod.lock")).Trim(), out var pid) && pid > 0
                ? pid
                : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return null; }
    }

    internal static OwnerRecord? ReadOwner(string directory)
    {
        try { return JsonSerializer.Deserialize<OwnerRecord>(File.ReadAllText(Path.Combine(directory, OwnerFile))); }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException) { return null; }
    }

    internal static void WriteOwner(string directory, OwnerRecord record) =>
        File.WriteAllText(Path.Combine(directory, OwnerFile), JsonSerializer.Serialize(record));

    private static DateTime? ProcessStartOf(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.HasExited ? null : process.StartTime.ToUniversalTime();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException
                                              or System.ComponentModel.Win32Exception) { return null; }
    }

    // Stops a leftover mongod by PID only after its start time matches the record (never by name).
    private static bool StopVerified(int pid, DateTime startedUtc)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            if (process.HasExited || !Same(process.StartTime.ToUniversalTime(), startedUtc)) return false;
            process.Kill();
            return process.WaitForExit(15000);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException
                                              or System.ComponentModel.Win32Exception) { return false; }
    }

    // Removes a directory only when it is a prefixed child of the given root.
    private static void DeleteOwnDirectory(string root, string directory)
    {
        var target = Path.GetFullPath(directory);
        var scope = Path.GetFullPath(root);
        if (!scope.EndsWith(Path.DirectorySeparatorChar)) scope += Path.DirectorySeparatorChar;
        if (!target.StartsWith(scope, StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(target).StartsWith(DirectoryPrefix, StringComparison.Ordinal)) return;
        // FIX1 (measured: an exit-time cleanup cut short left data files and no record): the owner record goes LAST,
        // so an interrupted cleanup still carries it and the next start's sweep removes the rest at once.
        for (var attempt = 0; attempt < 20 && Directory.Exists(target); attempt++)
        {
            try
            {
                foreach (var path in DeletionOrder(target))
                    if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
                    else File.Delete(path);
                Directory.Delete(target, recursive: true);
            }
            catch (IOException) { Thread.Sleep(100); }
            catch (UnauthorizedAccessException) { Thread.Sleep(100); }
        }
    }

    // The order a cleanup removes a directory's entries in (tested): sub-directories, then files, the owner record last.
    internal static IReadOnlyList<string> DeletionOrder(string directory)
    {
        var owner = Path.Combine(directory, OwnerFile);
        return
        [
            .. Directory.EnumerateDirectories(directory),
            .. Directory.EnumerateFiles(directory).Where(file => file != owner),
            .. File.Exists(owner) ? new[] { owner } : []
        ];
    }

    // ------------------------------------------------------------------------------------------------- the mongod

    internal sealed class OwnedMongod
    {
        private readonly ConcurrentQueue<string> _log = new();
        private Process? _process;
        private DateTime _started;
        private string? _directory;
        public int Port { get; private set; }
        public string ConnectionString { get; private set; } = string.Empty;
        public int Attempts { get; private set; }

        /// <summary>
        /// FIX1 (4): the port is picked by a listener that is then released, so another process can take it before
        /// mongod binds. A start whose mongod exits (a bind failure among others) is retried on a fresh port, up to
        /// <paramref name="attempts"/> times.
        /// </summary>
        public static async Task<OwnedMongod> StartWithRetryAsync(string executable, Func<int> pickPort, int attempts)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    var owned = await StartAsync(executable, pickPort()).ConfigureAwait(false);
                    owned.Attempts = attempt;
                    return owned;
                }
                catch (MongodExitedException) when (attempt < attempts) { }
            }
        }

        private static async Task<OwnedMongod> StartAsync(string executable, int port)
        {
            if (port == SharedPort) throw new MongodExitedException("The owned mongod drew the SHARED port.");
            var owned = new OwnedMongod { Port = port };
            owned._directory = Path.Combine(Path.GetTempPath(), DirectoryPrefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(owned._directory);
            using var host = Process.GetCurrentProcess();
            var hostStarted = host.StartTime.ToUniversalTime();
            WriteOwner(owned._directory, new OwnerRecord(null, null, Environment.ProcessId, hostStarted));
            var start = new ProcessStartInfo(Path.GetFullPath(executable))
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (var argument in MongodArguments(port, owned._directory)) start.ArgumentList.Add(argument);
            try
            {
                owned._process = new Process { StartInfo = start, EnableRaisingEvents = true };
                owned._process.OutputDataReceived += (_, line) => owned.Capture(line.Data);
                owned._process.ErrorDataReceived += (_, line) => owned.Capture(line.Data);
                if (!owned._process.Start()) throw new InvalidOperationException("OWNED_TEST_MONGO_START_FAILED");
                owned._started = owned._process.StartTime;
                owned._process.BeginOutputReadLine();
                owned._process.BeginErrorReadLine();
                WriteOwner(owned._directory,
                    new OwnerRecord(owned._process.Id, owned._started.ToUniversalTime(), Environment.ProcessId, hostStarted));

                var direct = $"mongodb://127.0.0.1:{port}/?directConnection=true";
                await owned.WaitAsync(direct, async admin =>
                {
                    await admin.RunCommandAsync<BsonDocument>(new BsonDocument("replSetInitiate", new BsonDocument
                    {
                        ["_id"] = ReplicaSetName,
                        ["members"] = new BsonArray { new BsonDocument { ["_id"] = 0, ["host"] = $"127.0.0.1:{port}" } }
                    })).ConfigureAwait(false);
                    return true;
                }).ConfigureAwait(false);
                owned.ConnectionString = $"mongodb://127.0.0.1:{port}/?replicaSet={ReplicaSetName}&directConnection=true";
                await owned.WaitAsync(owned.ConnectionString, async admin =>
                    (await admin.RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1)).ConfigureAwait(false))
                    .GetValue("isWritablePrimary", false).ToBoolean()).ConfigureAwait(false);
                Console.WriteLine($"OWNED_MDM_MONGO_STARTED pid={owned._process.Id} port={port}");
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
                if (_process!.HasExited)
                    throw new MongodExitedException(
                        $"OWNED_TEST_MONGO_EXITED:{_process.ExitCode}{Environment.NewLine}{string.Join(Environment.NewLine, _log)}");
                try { if (await ready(admin).ConfigureAwait(false)) return; }
                catch (Exception exception) when (exception is MongoException or TimeoutException) { }
                await Task.Delay(150).ConfigureAwait(false);
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
            if (_directory is not null) DeleteOwnDirectory(Path.GetTempPath(), _directory);
        }

        private void Capture(string? line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            _log.Enqueue(line);
            while (_log.Count > 100) _log.TryDequeue(out _);
        }
    }

    /// <summary>The mongod ended before it was ready (a lost port among others) — the start may be retried.</summary>
    internal sealed class MongodExitedException(string message) : InvalidOperationException(message);
}
