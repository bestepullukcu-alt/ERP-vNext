using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.RegularExpressions;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// BL-595 (WP-MDM-OWNED-MONGO-01) — the guard: no MDM test source (and no <c>appsettings*.json</c> under the tests)
/// names the shared developer Mongo. Every Mongo test connects through <see cref="MdmTestMongo"/>; the shared port is
/// named once, in that helper, so tests can refuse it. It reads text (comments included) and needs no Mongo.
/// <para>FIX1: what the pattern catches is pinned by its own theory (<see cref="SharedMongoPattern"/>); what it cannot
/// catch is named there too.</para>
/// </summary>
public sealed class MdmOwnedMongoGuardTests
{
    private const string TestsRoot = "services/Diten.MdmService/tests";

    // Files that still name the shared Mongo, each with its reason; a listed file that no longer does is red — take it
    // off. FIX1: down to two. Both only REFUSE the port (no connection to it) and both change on open branches
    // (S2 item-activation, BL-598 bl598-lazy-index), so they move after those merge.
    private static readonly Dictionary<string, string> NotYetMoved = new(StringComparer.Ordinal)
    {
        [TestsRoot + "/Diten.MdmService.Api.Tests/MdmHttpHost.cs"] =
            "names the port only to refuse it (its own EphemeralMongo); Api.Tests has no reference to this helper; changes on S2 / BL-598",
        [TestsRoot + "/Diten.MdmService.Application.Tests/MdmDocumentClassMapsTests.cs"] =
            "only a NotEqual(port) assertion; changes on S2 / BL-598"
    };

    // The helper names the port (to refuse it); this file holds the pattern's own examples. Nothing else.
    private static readonly string[] Exempt =
    [
        TestsRoot + "/Diten.MdmService.Application.Tests/MdmTestMongo.cs",
        TestsRoot + "/Diten.MdmService.Application.Tests/MdmOwnedMongoGuardTests.cs"
    ];

    // Built from the helper's constant. The arms: the port; any localhost URI (a test never needs it — owned servers are
    // 127.0.0.1 with a port); a loopback IP without a port (the driver's default is the shared port); a bare scheme
    // literal (a URI assembled by concatenation); a client or settings built with no arguments (the default server).
    internal static readonly Regex SharedMongoPattern = new(
        $@"\b{MdmTestMongo.SharedPort}\b"
        + @"|mongodb:/{2}(?:[^""\s/@]*@)?localhost"
        + @"|mongodb:/{2}(?:[^""\s/@]*@)?(?:127\.0\.0\.1|\[::1\])(?![:\d])"
        + @"|""mongodb:/{2}"""
        + @"|new\s+MongoClient(?:Settings)?\s*\(\s*\)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    [Fact]
    public void BL595_No_mdm_test_names_the_shared_mongo()
    {
        var root = RepoRoot();
        var sources = Directory.EnumerateFiles(Path.Combine(root, TestsRoot), "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal)
                           || Regex.IsMatch(Path.GetFileName(path), @"^appsettings.*\.json$", RegexOptions.IgnoreCase))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Where(path => !path.Contains("/bin/", StringComparison.Ordinal) && !path.Contains("/obj/", StringComparison.Ordinal))
            .ToArray();
        Assert.True(sources.Length > 100, $"only {sources.Length} MDM test sources read — the guard is blind");
        Assert.All(Exempt, path => Assert.Contains(path, sources));

        var naming = sources.Where(path => SharedMongoPattern.IsMatch(File.ReadAllText(Path.Combine(root, path)))).ToHashSet();
        var offenders = naming.Except(NotYetMoved.Keys).Except(Exempt).Order(StringComparer.Ordinal).ToArray();
        Assert.True(offenders.Length == 0,
            "an MDM test names the shared Mongo — connect through MdmTestMongo.ConnectionString and refuse the port with "
            + "MdmTestMongo.SharedPort:\n" + string.Join("\n", offenders));

        var stale = NotYetMoved.Keys.Where(path => !naming.Contains(path)).ToArray();
        Assert.True(stale.Length == 0, "no longer names the shared Mongo — take it off NotYetMoved:\n" + string.Join("\n", stale));
    }

    // FIX1: the pattern's own examples. Red = the guard catches it.
    // NOT caught, named rather than hidden: a host or port held in a variable ($"mongodb://{host}:{port}" with
    // host = "localhost", or "mongodb://127.0.0.1:" + port with port = the shared one at run time); a MongoServerAddress
    // built from parts; a URI read from a file outside the tests. The guard reads text, not values.
    [Theory]
    [InlineData("mongodb://localhost", true)]
    [InlineData("mongodb://127.0.0.1/", true)]
    [InlineData("mongodb://127.0.0.1", true)]
    [InlineData("mongodb://user:pw@localhost", true)]
    [InlineData("mongodb://[::1]", true)]
    [InlineData("$\"mongodb://localhost:{port}\"", true)]
    [InlineData("new MongoClient()", true)]
    [InlineData("new MongoClientSettings()", true)]
    [InlineData("\"mongodb://\" + \"localhost\"", true)]
    [InlineData("var port = " + MdmTestMongo.SharedPortText + ";", true)]
    [InlineData("mongodb://127.0.0.1:" + MdmTestMongo.SharedPortText, true)]
    [InlineData("$\"mongodb://127.0.0.1:{Port}/?directConnection=true\"", false)]
    [InlineData("mongodb://127.0.0.1:1/?serverSelectionTimeoutMS=25", false)]
    [InlineData("new MongoClient(settings)", false)]
    [InlineData("Port = 270170", false)]
    public void BL595_The_guard_pattern_catches_every_shared_mongo_form_it_claims(string text, bool caught)
    {
        Assert.Equal(caught, SharedMongoPattern.IsMatch(text));
    }

    private static string RepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) return directory.FullName;
        throw new InvalidOperationException("Repository root (AGENTS.md) not found.");
    }
}

/// <summary>BL-595 — the helper's own rules (Auth's resolution, the redirect, the sweep) and the server it gives.</summary>
public sealed class MdmTestMongoTests
{
    private const string Mongod = "/opt/test/bin/mongod";

    [Theory]
    [InlineData("absent", false, false, null, MdmTestMongo.Unavailable)]
    [InlineData("absent", true, false, null, MdmTestMongo.RequiredButUnavailable)]
    [InlineData("", false, false, null, MdmTestMongo.InvalidVariable)]
    [InlineData("", true, false, null, MdmTestMongo.InvalidVariable)]
    [InlineData("mongod", false, false, null, MdmTestMongo.InvalidVariable)]
    // FIX1: a relative path that EXISTS is still invalid (only an absolute path counts).
    [InlineData("bin/mongod", false, false, null, MdmTestMongo.InvalidVariable)]
    [InlineData("/opt/missing/mongod", false, false, null, MdmTestMongo.InvalidVariable)]
    [InlineData(Mongod, false, false, Mongod, null)]
    [InlineData("absent", false, true, MdmTestMongo.WindowsDefaultMongod, null)]
    public void BL595_The_mongod_is_resolved_by_auths_rule(string variable, bool required, bool windows, string? executable,
        string? failure)
    {
        var resolution = MdmTestMongo.Resolve(variable == "absent" ? null : variable, required, windows,
            path => path is Mongod or MdmTestMongo.WindowsDefaultMongod or "bin/mongod");

        Assert.Equal(executable, resolution.Executable);
        if (failure is null) Assert.Null(resolution.Failure);
        else Assert.StartsWith(failure + ":", resolution.Failure);
    }

    // FIX1: Windows without the default install is "nothing found" (Auth has this row).
    [Fact]
    public void BL595_Windows_without_the_default_install_is_nothing_found()
    {
        var resolution = MdmTestMongo.Resolve(null, required: false, windows: true, _ => false);
        Assert.Null(resolution.Executable);
        Assert.StartsWith(MdmTestMongo.Unavailable + ":", resolution.Failure);
    }

    // FIX1: the environment plumbing — REQUIRE is "1" exactly.
    [Theory]
    [InlineData("1", MdmTestMongo.RequiredButUnavailable)]
    [InlineData("true", MdmTestMongo.Unavailable)]
    [InlineData(null, MdmTestMongo.Unavailable)]
    public void BL595_The_require_flag_is_read_from_the_environment_as_1_exactly(string? require, string failure)
    {
        var environment = new Dictionary<string, string?> { [MdmTestMongo.RequireVariable] = require };
        var resolution = MdmTestMongo.ResolveFrom(name => environment.GetValueOrDefault(name), windows: false, _ => false);
        Assert.StartsWith(failure + ":", resolution.Failure);
    }

    // Nothing resolved → the named failure; never a connection, never the shared server.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BL595_Without_an_owned_mongod_there_is_no_connection_only_the_named_failure(bool required)
    {
        var started = false;
        var resolution = MdmTestMongo.Resolve(null, required, windows: false, _ => false);

        var error = Assert.Throws<InvalidOperationException>(() =>
            MdmTestMongo.ConnectionFor(resolution, _ => { started = true; return "unreachable"; }));

        Assert.False(started);
        Assert.Equal(resolution.Failure, error.Message);
    }

    // The in-process redirect of the classes that own a replica set: only another test-owned loopback mongod. FIX1: the
    // host rule first (a remote host is red even on the default port), then a list naming the shared port is ignored.
    [Theory]
    [InlineData(null, false, null)]
    [InlineData("", false, null)]
    [InlineData("mongodb://127.0.0.1:" + MdmTestMongo.SharedPortText, false, null)]
    [InlineData("mongodb://localhost:" + MdmTestMongo.SharedPortText + "/?directConnection=true", false, null)]
    [InlineData("mongodb://127.0.0.1:" + MdmTestMongo.SharedPortText + ",127.0.0.1:41234", false, null)]
    [InlineData("mongodb://127.0.0.1:41234/?replicaSet=x&directConnection=true", false, "mongodb://127.0.0.1:41234/?replicaSet=x&directConnection=true")]
    [InlineData("mongodb://127.0.0.1:41234,localhost:41235/?replicaSet=x", false, "mongodb://127.0.0.1:41234,localhost:41235/?replicaSet=x")]
    [InlineData("mongodb://db.example.com:41234", true, null)]
    [InlineData("mongodb://db.example.com", true, null)]
    [InlineData("mongodb://127.0.0.1:41234,db.example.com:41235", true, null)]
    [InlineData("mongodb://db.example.com:" + MdmTestMongo.SharedPortText + ",127.0.0.1:41234", true, null)]
    [InlineData("not a uri", true, null)]
    public void BL595_A_redirect_is_only_another_test_owned_loopback_mongod_never_the_shared_one(string? value, bool red,
        string? expected)
    {
        if (red)
            Assert.StartsWith(MdmTestMongo.RedirectInvalid + ":",
                Assert.Throws<InvalidOperationException>(() => MdmTestMongo.RedirectFor(value)).Message);
        else
            Assert.Equal(expected, MdmTestMongo.RedirectFor(value));
    }

    // FIX1: the mongod listens on loopback only — it runs with no authentication and with test commands.
    [Fact]
    public void BL595_The_owned_mongod_listens_on_loopback_only()
    {
        var arguments = MdmTestMongo.MongodArguments(41234, "/tmp/x").ToArray();
        var bind = Array.IndexOf(arguments, "--bind_ip");
        Assert.True(bind >= 0 && bind + 1 < arguments.Length);
        Assert.Equal("127.0.0.1", arguments[bind + 1]);
        Assert.DoesNotContain("--bind_ip_all", arguments);
        Assert.Equal("41234", arguments[Array.IndexOf(arguments, "--port") + 1]);
    }

    // FIX1 (2): a start awaited from a thread whose synchronization context is busy (as xunit's worker is, blocked on the
    // Lazy) completes — its continuations never need that thread.
    [Fact]
    public void BL595_The_start_is_awaited_off_the_callers_synchronization_context()
    {
        var finished = false;
        var thread = new Thread(() =>
        {
            var context = new BlockedContext();
            SynchronizationContext.SetSynchronizationContext(context);
            finished = MdmTestMongo.WaitOffContext(async () =>
            {
                await Task.Delay(20);
                return true;
            });
        }) { IsBackground = true };

        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "the start waited for the blocked context — a deadlock");
        Assert.True(finished);
    }

    // A context whose posts are only run by its own thread — which is the one waiting (a busy xunit worker).
    private sealed class BlockedContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state) { }
        public override void Send(SendOrPostCallback callback, object? state) { }
    }

    // FIX1 (3): the sweep rule.
    [Theory]
    [InlineData("host-alive", MdmTestMongo.SweepAction.Keep)]
    [InlineData("host-dead-mongod-alive", MdmTestMongo.SweepAction.StopMongodAndDeleteDirectory)]
    [InlineData("host-dead-mongod-gone", MdmTestMongo.SweepAction.DeleteDirectory)]
    [InlineData("host-dead-pid-reused", MdmTestMongo.SweepAction.DeleteDirectory)]
    [InlineData("host-dead-no-mongod-yet", MdmTestMongo.SweepAction.DeleteDirectory)]
    [InlineData("host-pid-reused", MdmTestMongo.SweepAction.StopMongodAndDeleteDirectory)]
    [InlineData("no-record", MdmTestMongo.SweepAction.Keep)]
    [InlineData("no-record-old", MdmTestMongo.SweepAction.DeleteDirectory)]
    // CT note: an older-code run (no record) still going after the grace — its mongod holds mongod.lock → kept.
    [InlineData("no-record-old-lock-held", MdmTestMongo.SweepAction.Keep)]
    [InlineData("no-record-old-lock-dead", MdmTestMongo.SweepAction.DeleteDirectory)]
    public void BL595_The_sweep_rule(string shape, MdmTestMongo.SweepAction expected)
    {
        var (record, live) = SweepShape(shape);
        var age = shape.StartsWith("no-record-old", StringComparison.Ordinal)
            ? MdmTestMongo.RecordlessGrace + TimeSpan.FromMinutes(1)
            : TimeSpan.FromMinutes(1);
        int? lockPid = shape.StartsWith("no-record-old-lock", StringComparison.Ordinal) ? 3001 : null;
        if (shape == "no-record-old-lock-held") live[3001] = DateTime.UtcNow.AddHours(-5);
        Assert.Equal(expected, MdmTestMongo.Decide(record, pid => live.TryGetValue(pid, out var at) ? at : null, age, lockPid));
    }

    [Theory]
    [InlineData("4242\n", 4242)]
    [InlineData("", null)]
    [InlineData("not-a-pid", null)]
    [InlineData(null, null)]
    public void BL595_The_lock_holder_is_read_from_mongod_lock(string? content, int? expected)
    {
        var directory = Path.Combine(Path.GetTempPath(), "diten_mdm_locktest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            if (content is not null) File.WriteAllText(Path.Combine(directory, "mongod.lock"), content.Replace("\\n", "\n"));
            Assert.Equal(expected, MdmTestMongo.LockPid(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // FIX1 (3): the sweep on a real directory tree — a live run's directory stays, a dead run's goes, a process whose
    // start time does not match is never stopped, nothing outside the prefix is touched.
    [Fact]
    public void BL595_The_sweep_removes_only_dead_runs_and_stops_only_a_verified_mongod()
    {
        var root = Path.Combine(Path.GetTempPath(), "diten_mdm_sweeptest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var directories = new Dictionary<string, string>();
            var live = new Dictionary<int, DateTime>();
            foreach (var shape in new[] { "host-alive", "host-dead-mongod-alive", "host-dead-mongod-gone", "host-dead-pid-reused", "no-record" })
            {
                var (record, alive) = SweepShape(shape, offset: directories.Count * 100);
                foreach (var pair in alive) live[pair.Key] = pair.Value;
                var directory = Path.Combine(root, MdmTestMongo.DirectoryPrefix + shape);
                Directory.CreateDirectory(directory);
                if (record is not null) MdmTestMongo.WriteOwner(directory, record);
                directories[shape] = directory;
            }
            var stranger = Path.Combine(root, "someone_else_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stranger);
            var stopped = new List<(int Pid, DateTime Started)>();

            var actions = MdmTestMongo.Sweep(root, pid => live.TryGetValue(pid, out var at) ? at : null,
                (pid, started) => { stopped.Add((pid, started)); return true; });

            Assert.True(Directory.Exists(directories["host-alive"]));
            Assert.True(Directory.Exists(directories["no-record"]));
            Assert.False(Directory.Exists(directories["host-dead-mongod-alive"]));
            Assert.False(Directory.Exists(directories["host-dead-mongod-gone"]));
            Assert.False(Directory.Exists(directories["host-dead-pid-reused"]));
            Assert.True(Directory.Exists(stranger));
            var only = Assert.Single(stopped);
            // Only the dead run's live mongod (directory 1: PIDs offset by 100), by its recorded PID and start time.
            Assert.Equal((2101, new DateTime(2026, 10, 11, 8, 0, 5, DateTimeKind.Utc)), only);
            Assert.Equal(5, actions.Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // FIX1 (measured leak: an exit-time cleanup cut short left data files and no record): the owner record is removed
    // LAST, so an interrupted cleanup still carries it and the next start sweeps the rest at once.
    [Fact]
    public void BL595_A_cleanup_removes_the_owner_record_last()
    {
        var root = Path.Combine(Path.GetTempPath(), "diten_mdm_sweeptest_" + Guid.NewGuid().ToString("N"));
        var directory = Path.Combine(root, MdmTestMongo.DirectoryPrefix + "order");
        Directory.CreateDirectory(Path.Combine(directory, "journal"));
        try
        {
            var (record, _) = SweepShape("host-dead-mongod-gone");
            MdmTestMongo.WriteOwner(directory, record!);
            foreach (var name in new[] { "WiredTiger.wt", "zz-collection.wt", "mongod.lock" })
                File.WriteAllText(Path.Combine(directory, name), "x");

            var order = MdmTestMongo.DeletionOrder(directory);

            Assert.Equal(5, order.Count);
            Assert.Equal(Path.Combine(directory, MdmTestMongo.OwnerFile), order[^1]);

            MdmTestMongo.Sweep(root, _ => null, (_, _) => true);
            Assert.False(Directory.Exists(directory));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    // A mongod that cannot be verified (stop answers false) is left with its directory.
    [Fact]
    public void BL595_The_sweep_keeps_what_it_could_not_verify()
    {
        var root = Path.Combine(Path.GetTempPath(), "diten_mdm_sweeptest_" + Guid.NewGuid().ToString("N"));
        var directory = Path.Combine(root, MdmTestMongo.DirectoryPrefix + "unverified");
        Directory.CreateDirectory(directory);
        try
        {
            var (record, live) = SweepShape("host-dead-mongod-alive");
            MdmTestMongo.WriteOwner(directory, record!);

            var actions = MdmTestMongo.Sweep(root, pid => live.TryGetValue(pid, out var at) ? at : null, (_, _) => false);

            Assert.True(Directory.Exists(directory));
            Assert.Equal(MdmTestMongo.SweepAction.Keep, Assert.Single(actions).Action);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // Records and the live processes (PID → start) for one sweep shape. PIDs are offset per directory.
    private static (MdmTestMongo.OwnerRecord? Record, Dictionary<int, DateTime> Live) SweepShape(string shape, int offset = 0)
    {
        var t = new DateTime(2026, 10, 11, 8, 0, 0, DateTimeKind.Utc);
        int host = 1001 + offset, mongod = 2001 + offset;
        var live = new Dictionary<int, DateTime>();
        MdmTestMongo.OwnerRecord? record = new(mongod, t.AddSeconds(5), host, t);
        switch (shape)
        {
            case "host-alive": live[host] = t; live[mongod] = t.AddSeconds(5); break;
            case "host-dead-mongod-alive": live[mongod] = t.AddSeconds(5); break;
            case "host-dead-mongod-gone": break;
            case "host-dead-pid-reused": live[mongod] = t.AddHours(3); break; // another process now has that PID
            case "host-dead-no-mongod-yet": record = record with { MongodPid = null, MongodStartedUtc = null }; break;
            case "host-pid-reused": live[host] = t.AddHours(3); live[mongod] = t.AddSeconds(5); break;
            default: record = null; break;
        }
        return (record, live);
    }

    // FIX1 (4): a port taken between its pick and mongod's bind is retried on a fresh one.
    [Fact]
    public async Task BL595_A_port_lost_before_the_bind_is_retried_on_a_fresh_one()
    {
        var executable = MdmTestMongo.ResolveOwned().Executable
                         ?? throw new InvalidOperationException(MdmTestMongo.ResolveOwned().Failure);
        using var taken = new TcpListener(IPAddress.Loopback, 0);
        taken.Start();
        var ports = new Queue<int>([((IPEndPoint)taken.LocalEndpoint).Port, FreePort()]);

        var owned = await MdmTestMongo.OwnedMongod.StartWithRetryAsync(executable, ports.Dequeue, MdmTestMongo.StartAttempts);
        try
        {
            Assert.Equal(2, owned.Attempts);
            Assert.NotEqual(((IPEndPoint)taken.LocalEndpoint).Port, owned.Port);
        }
        finally
        {
            owned.Stop();
        }
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    // The helper's OWN server (not a redirect): this process's replica set on loopback, never the shared port; its stop
    // is registered for process exit (FIX1: a removed registration leaks the mongod and its directory).
    [Fact]
    public async Task BL595_The_owned_mongo_is_a_test_owned_replica_set_never_the_shared_port()
    {
        var settings = MongoClientSettings.FromConnectionString(MdmTestMongo.OwnedConnectionString);
        var server = Assert.Single(settings.Servers);

        Assert.Equal("127.0.0.1", server.Host);
        Assert.NotEqual(MdmTestMongo.SharedPort, server.Port);
        Assert.Equal("mdm-owned", settings.ReplicaSetName);
        using var session = await new MongoClient(settings).StartSessionAsync();
        session.StartTransaction();
        await session.AbortTransactionAsync();

        Assert.True(MdmTestMongo.StopRegisteredForExit);
        var exit = typeof(AppContext).GetField("ProcessExit", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as EventHandler;
        Assert.NotNull(exit);
        Assert.Contains(exit!.GetInvocationList(), handler =>
            handler.Method.DeclaringType?.FullName?.StartsWith(typeof(MdmTestMongo).FullName!, StringComparison.Ordinal) == true);
    }
}

/// <summary>
/// FIX1: <see cref="MdmTestMongo.ConnectionString"/> reads the redirect from the process — in the serial collection
/// (the redirecting classes run there too), set and restored here.
/// </summary>
[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class MdmTestMongoRedirectTests
{
    [Theory]
    [InlineData("mongodb://127.0.0.1:41234/?replicaSet=x&directConnection=true", true)]
    [InlineData("mongodb://127.0.0.1:" + MdmTestMongo.SharedPortText, false)]
    public void BL595_The_connection_follows_the_in_process_redirect(string value, bool followed)
    {
        var previous = Environment.GetEnvironmentVariable(MdmTestMongo.RedirectVariable);
        Environment.SetEnvironmentVariable(MdmTestMongo.RedirectVariable, value);
        try
        {
            if (followed) Assert.Equal(value, MdmTestMongo.ConnectionString);
            else Assert.Equal(MdmTestMongo.OwnedConnectionString, MdmTestMongo.ConnectionString);
        }
        finally
        {
            Environment.SetEnvironmentVariable(MdmTestMongo.RedirectVariable, previous);
        }
    }
}
