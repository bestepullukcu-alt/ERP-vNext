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
/// <para>The one redirect: test classes that own a replica set of their own (<c>AuditIntentTemporalMongoFixture</c>)
/// point the shared helpers at it for their lifetime through <see cref="UseOwnedRedirect"/> — in-process, by the
/// fixture's own API (their collection runs alone). FIX2: the process ENVIRONMENT is never read for a connection — a
/// <c>MDM_TEST_MONGO</c> left in a shell (or CI's job-local value) does not move a test, its writes or its
/// <c>DropDatabase</c> anywhere. A redirect must name loopback hosts only and not the shared port
/// (<see cref="RedirectInvalid"/>).</para>
/// <para>FIX1/FIX2 cleanup: every owned directory carries an owner record; a start sweeps what an earlier, killed run
/// left behind. The one rule is "in doubt, Keep": a process whose state cannot be read, a lock file that cannot be
/// read, a directory whose age cannot be read, a link, an empty record — all kept. A leftover mongod is stopped only
/// when its PID, start time (within <see cref="StopTolerance"/>) AND executable path all match its record.</para>
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
    /// <summary>The environment variable a test process IGNORES (FIX2) — named so the test can set it and prove it.</summary>
    public const string RedirectVariable = "MDM_TEST_MONGO";
    public const string RedirectInvalid = "OWNED_TEST_MONGO_REDIRECT_INVALID";
    public const string DirectoryPrefix = "diten_mdm_owned_";
    public const string OwnerFile = "owner.json";
    public const int StartAttempts = 3;
    public static readonly TimeSpan RecordlessGrace = TimeSpan.FromHours(1);
    /// <summary>FIX2 (5): a host counts as the recorded one within this (loose: a clock step must not make it "gone").</summary>
    public static readonly TimeSpan HostTolerance = TimeSpan.FromSeconds(10);
    /// <summary>FIX2 (5): a mongod is stopped only within this (strict) — plus the PID and the executable path.</summary>
    public static readonly TimeSpan StopTolerance = TimeSpan.FromSeconds(1);
    private const string ReplicaSetName = "mdm-owned";

    public sealed record Resolution(string? Executable, string? Failure);

    private static readonly Lazy<OwnedMongod> Owned = new(Start, LazyThreadSafetyMode.ExecutionAndPublication);
    private static string? _redirect;

    /// <summary>
    /// The connection every MDM Mongo test uses: an in-process redirect set by a fixture (<see cref="UseOwnedRedirect"/>),
    /// else this process's own replica set (started on first use; a failure is red by name).
    /// </summary>
    public static string ConnectionString => _redirect ?? OwnedConnectionString;

    /// <summary>This process's own replica set — never a redirect.</summary>
    public static string OwnedConnectionString => Owned.Value.ConnectionString;

    /// <summary>Whether the owned mongod's stop is registered for process exit (set where the handler is added).</summary>
    public static bool StopRegisteredForExit { get; private set; }

    /// <summary>
    /// FIX2 (6): a fixture that owns a replica set points the shared helpers at it until the returned scope is disposed
    /// (the previous redirect is restored). Only from a serial collection. The URI must be a test-owned loopback mongod
    /// that is not the shared port — anything else is <see cref="RedirectInvalid"/>.
    /// </summary>
    public static IDisposable UseOwnedRedirect(string connectionString)
    {
        var valid = RedirectFor(connectionString)
                    ?? throw new InvalidOperationException($"{RedirectInvalid}: a redirect never names the shared port.");
        var previous = _redirect;
        _redirect = valid;
        return new Restore(() => _redirect = previous);
    }

    private sealed class Restore(Action restore) : IDisposable
    {
        private int _done;
        public void Dispose() { if (Interlocked.Exchange(ref _done, 1) == 0) restore(); }
    }

    // The redirect rule alone (tested): absent → none; every host loopback, else the named failure (a default port too);
    // then a list naming the shared port → none; else the value.
    internal static string? RedirectFor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        MongoUrl url;
        try { url = new MongoUrl(value); }
        catch (MongoConfigurationException) { throw new InvalidOperationException($"{RedirectInvalid}: not a mongodb URI."); }
        var servers = url.Servers.ToArray();
        if (servers.Length == 0 || !servers.All(server => server.Host is "127.0.0.1" or "localhost"))
            throw new InvalidOperationException(
                $"{RedirectInvalid}: a redirect may only name a test-owned loopback mongod (never a remote one).");
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
            SweepBestEffort(Path.GetTempPath());
            owned = WaitOffContext(() => OwnedMongod.StartWithRetryAsync(executable, PickPort, StartAttempts));
            return owned.ConnectionString;
        });
        AppDomain.CurrentDomain.ProcessExit += (_, _) => owned!.Stop();
        StopRegisteredForExit = true;
        return owned!;
    }

    // FIX2: the sweep is best effort — an unexpected failure is written out and never fails the tests' start.
    internal static void SweepBestEffort(string root, Func<int, ProcessProbe>? probe = null, Func<OwnerRecord, bool>? stop = null)
    {
        try { Sweep(root, probe ?? Probe, stop ?? StopVerified); }
        catch (Exception exception)
        {
            Console.WriteLine($"OWNED_MDM_MONGO_SWEEP_SKIPPED {exception.GetType().Name}: {exception.Message}");
        }
    }

    private static int PickPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    // ------------------------------------------------------------------------------------------------- the sweep

    /// <summary>What a start writes into its directory: its mongod (PID, start time, executable) and its test host.</summary>
    public sealed record OwnerRecord(int? MongodPid, DateTime? MongodStartedUtc, int HostPid, DateTime HostStartedUtc,
        string? MongodPath = null);

    public enum SweepAction { Keep, DeleteDirectory, StopMongodAndDeleteDirectory }

    /// <summary>FIX2 (1): a process is alive (with its start and path), gone, or UNKNOWN (its state could not be read).</summary>
    public enum ProcessState { Gone, Alive, Unknown }

    public readonly record struct ProcessProbe(ProcessState State, DateTime StartedUtc = default, string? Path = null)
    {
        public static readonly ProcessProbe Gone = new(ProcessState.Gone);
        public static readonly ProcessProbe Unknown = new(ProcessState.Unknown);
    }

    /// <summary>FIX2 (2): a lock file is absent / empty, names a PID, or exists but cannot be READ.</summary>
    public readonly record struct LockRead(bool Unreadable, int? Pid)
    {
        public static readonly LockRead None = new(false, null);
        public static readonly LockRead CannotRead = new(true, null);
    }

    /// <summary>
    /// The sweep rule alone (tested). "In doubt, Keep": an unreadable process state, an unreadable lock, an unknown
    /// age, an empty record, a start time between the strict and the loose tolerance, an unverifiable executable path.
    /// </summary>
    internal static SweepAction Decide(OwnerRecord? record, Func<int, ProcessProbe> probe, TimeSpan? age, LockRead lockFile)
    {
        if (record is null)
        {
            // A start writes its record the moment it creates the directory: a recordless directory is from the older
            // helper (c3a9f00c7) or a failed write. Its process, if any, is never touched; the directory goes only when
            // its age is known and past the grace, and no live — or unreadable — process holds its mongod.lock.
            if (age is not { } known || known <= RecordlessGrace) return SweepAction.Keep;
            return LockFree(lockFile, probe) ? SweepAction.DeleteDirectory : SweepAction.Keep;
        }
        // An empty or impossible record is doubt.
        if (record.HostPid <= 1 || record.HostStartedUtc == default) return SweepAction.Keep;

        var host = probe(record.HostPid);
        if (host.State == ProcessState.Unknown) return SweepAction.Keep;
        // Loose: a clock step must not make a live host look gone.
        if (host.State == ProcessState.Alive && Within(host.StartedUtc, record.HostStartedUtc, HostTolerance))
            return SweepAction.Keep;

        // The host is gone (or its PID belongs to a process started well apart from it).
        if (record is not { MongodPid: { } pid, MongodStartedUtc: { } started })
            // No mongod recorded yet: the lock file says whether one is running in it.
            return LockFree(lockFile, probe) ? SweepAction.DeleteDirectory : SweepAction.Keep;
        if (pid <= 1) return SweepAction.Keep;

        var mongod = probe(pid);
        switch (mongod.State)
        {
            case ProcessState.Unknown:
                return SweepAction.Keep;
            case ProcessState.Gone:
                return LockFree(lockFile, probe) ? SweepAction.DeleteDirectory : SweepAction.Keep;
        }
        // Alive. Strict for a stop: PID, start time within StopTolerance AND the executable path.
        if (Within(mongod.StartedUtc, started, StopTolerance))
            return record.MongodPath is { } path && mongod.Path is { } actual && SamePath(actual, path)
                ? SweepAction.StopMongodAndDeleteDirectory
                : SweepAction.Keep;
        // Started well apart: that PID is another process now (never touched) — this run's mongod is gone.
        return Within(mongod.StartedUtc, started, HostTolerance) ? SweepAction.Keep : SweepAction.DeleteDirectory;
    }

    private static bool LockFree(LockRead lockFile, Func<int, ProcessProbe> probe) =>
        !lockFile.Unreadable
        && (lockFile.Pid is not { } holder || (holder > 1 && probe(holder).State == ProcessState.Gone));

    internal static bool Within(DateTime actual, DateTime recorded, TimeSpan tolerance) =>
        (actual - recorded).Duration() <= tolerance;

    private static bool SamePath(string actual, string recorded) =>
        string.Equals(Path.GetFullPath(actual), Path.GetFullPath(recorded),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    // FIX2 (3): a link (symbolic link, junction, any reparse point) is never followed, read or deleted.
    internal static bool IsLink(FileSystemInfo entry) =>
        entry.LinkTarget is not null || entry.Attributes.HasFlag(FileAttributes.ReparsePoint);

    /// <summary>
    /// Sweeps <paramref name="root"/>: only real (non-link) directories named with <see cref="DirectoryPrefix"/>, each by
    /// its owner record and <see cref="Decide"/>. <paramref name="stop"/> stops a process only after verifying PID,
    /// start time and executable path again. Returns what it did, for the test.
    /// </summary>
    internal static IReadOnlyList<(string Directory, SweepAction Action)> Sweep(
        string root, Func<int, ProcessProbe> probe, Func<OwnerRecord, bool> stop)
    {
        var done = new List<(string, SweepAction)>();
        if (!Directory.Exists(root)) return done;
        foreach (var entry in new DirectoryInfo(root).EnumerateDirectories(DirectoryPrefix + "*"))
        {
            if (IsLink(entry)) { done.Add((entry.FullName, SweepAction.Keep)); continue; }
            var record = ReadOwner(entry.FullName);
            var action = Decide(record, probe, AgeOf(entry), LockPid(entry.FullName));
            if (action == SweepAction.StopMongodAndDeleteDirectory && !stop(record!))
                action = SweepAction.Keep; // it could not be verified or stopped: leave it
            if (action != SweepAction.Keep) DeleteOwnDirectory(root, entry.FullName);
            done.Add((entry.FullName, action));
        }
        return done;
    }

    // FIX2: an age that cannot be read (a stat failure answers the 1601 epoch) is unknown → Keep.
    internal static TimeSpan? AgeOf(FileSystemInfo entry)
    {
        try
        {
            entry.Refresh();
            var created = entry.CreationTimeUtc;
            return created.Year <= 1601 ? null : DateTime.UtcNow - created;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return null; }
    }

    // The PID a running mongod wrote into its dbpath's mongod.lock (empty after a clean stop); a lock that exists but
    // cannot be read is UNREADABLE (FIX2: a Windows mongod holds it exclusively) — never "none".
    internal static LockRead LockPid(string directory)
    {
        var path = Path.Combine(directory, "mongod.lock");
        if (!File.Exists(path)) return LockRead.None;
        try
        {
            return int.TryParse(File.ReadAllText(path).Trim(), out var pid) && pid > 0 ? new LockRead(false, pid) : LockRead.None;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return LockRead.CannotRead; }
    }

    internal static OwnerRecord? ReadOwner(string directory)
    {
        try { return JsonSerializer.Deserialize<OwnerRecord>(File.ReadAllText(Path.Combine(directory, OwnerFile))); }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException) { return null; }
    }

    internal static void WriteOwner(string directory, OwnerRecord record) =>
        File.WriteAllText(Path.Combine(directory, OwnerFile), JsonSerializer.Serialize(record));

    /// <summary>FIX2 (1): a process's state — alive (start, executable path), gone, or unknown (could not be read).</summary>
    internal static ProcessProbe Probe(int pid)
    {
        Process process;
        try { process = Process.GetProcessById(pid); }
        catch (ArgumentException) { return ProcessProbe.Gone; } // no process has that PID
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception
                                              or UnauthorizedAccessException) { return ProcessProbe.Unknown; }
        using (process)
        {
            try
            {
                if (process.HasExited) return ProcessProbe.Gone;
                var started = process.StartTime.ToUniversalTime();
                string? path;
                try { path = process.MainModule?.FileName; }
                catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception
                                                      or NotSupportedException) { path = null; }
                return new ProcessProbe(ProcessState.Alive, started, path);
            }
            catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception
                                                  or UnauthorizedAccessException) { return ProcessProbe.Unknown; }
        }
    }

    /// <summary>
    /// FIX2 (4): the ONE stop path. A leftover mongod is stopped only when its PID, its start time (within
    /// <see cref="StopTolerance"/>) and its executable path all match the record — read again right before the stop.
    /// Never by name.
    /// </summary>
    internal static bool StopVerified(OwnerRecord record)
    {
        if (record is not { MongodPid: { } pid, MongodStartedUtc: { } started, MongodPath: { } path } || pid <= 1) return false;
        try
        {
            using var process = Process.GetProcessById(pid);
            if (process.HasExited || !Within(process.StartTime.ToUniversalTime(), started, StopTolerance)) return false;
            if (process.MainModule?.FileName is not { } actual || !SamePath(actual, path)) return false;
            process.Kill();
            return process.WaitForExit(15000);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException
                                              or System.ComponentModel.Win32Exception or NotSupportedException) { return false; }
    }

    // Removes a directory only when it is a real (non-link), prefixed child of the given root.
    internal static void DeleteOwnDirectory(string root, string directory)
    {
        var target = Path.GetFullPath(directory);
        var scope = Path.GetFullPath(root);
        if (!scope.EndsWith(Path.DirectorySeparatorChar)) scope += Path.DirectorySeparatorChar;
        if (!target.StartsWith(scope, StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(target).StartsWith(DirectoryPrefix, StringComparison.Ordinal)
            || Path.GetDirectoryName(target.TrimEnd(Path.DirectorySeparatorChar)) + Path.DirectorySeparatorChar != scope
            || !Directory.Exists(target) || IsLink(new DirectoryInfo(target))) return;
        for (var attempt = 0; attempt < 20 && Directory.Exists(target); attempt++)
        {
            try
            {
                foreach (var path in DeletionOrder(target))
                {
                    var entry = new FileInfo(path);
                    if (IsLink(entry)) { File.Delete(path); continue; } // the link itself, never its target
                    if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
                    else File.Delete(path);
                }
                Directory.Delete(target, recursive: false);
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
            var start = new ProcessStartInfo(Path.GetFullPath(executable))
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (var argument in MongodArguments(port, owned._directory)) start.ArgumentList.Add(argument);
            try
            {
                Directory.CreateDirectory(owned._directory);
                using var host = Process.GetCurrentProcess();
                var hostStarted = host.StartTime.ToUniversalTime();
                WriteOwner(owned._directory, new OwnerRecord(null, null, Environment.ProcessId, hostStarted));
                owned._process = new Process { StartInfo = start, EnableRaisingEvents = true };
                owned._process.OutputDataReceived += (_, line) => owned.Capture(line.Data);
                owned._process.ErrorDataReceived += (_, line) => owned.Capture(line.Data);
                if (!owned._process.Start()) throw new InvalidOperationException("OWNED_TEST_MONGO_START_FAILED");
                owned._started = owned._process.StartTime;
                owned._process.BeginOutputReadLine();
                owned._process.BeginErrorReadLine();
                // The executable as the OS reports it (the same reading the sweep's check makes).
                var path = Probe(owned._process.Id).Path;
                WriteOwner(owned._directory, new OwnerRecord(owned._process.Id, owned._started.ToUniversalTime(),
                    Environment.ProcessId, hostStarted, path));

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
                // FIX2: a server that answered on a lost port may be a stranger's — ours must still be running.
                if (owned._process.HasExited)
                    throw new MongodExitedException($"OWNED_TEST_MONGO_EXITED_AFTER_READY:{owned._process.ExitCode}");
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
