using System.Diagnostics;
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
    // off. FIX1: down to two. S2 lane merge (S2 and BL-598 both in): MdmDocumentClassMapsTests moved to
    // MdmTestMongo.SharedPort; one left, it only REFUSES the port (no connection to it).
    private static readonly Dictionary<string, string> NotYetMoved = new(StringComparer.Ordinal)
    {
        [TestsRoot + "/Diten.MdmService.Api.Tests/MdmHttpHost.cs"] =
            "names the port only to refuse it (its own EphemeralMongo); Api.Tests has no reference to this helper"
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
    // FIX2: the port also with a digit separator or a suffix (27_017, 27017L); localhost and a portless loopback IP
    // anywhere in a host list (not only first).
    internal static readonly Regex SharedMongoPattern = new(
        $@"\b{MdmTestMongo.SharedPortText[..2]}_?{MdmTestMongo.SharedPortText[2..]}[lLuU]*\b"
        + @"|mongodb:/{2}[^""\s/]*?\blocalhost\b"
        + @"|mongodb:/{2}[^""\s/]*?(?:127\.0\.0\.1|\[::1\])(?![:\d])"
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

        var (offenders, stale) = Judge(sources, path => File.ReadAllText(Path.Combine(root, path)), NotYetMoved.Keys, Exempt);
        Assert.True(offenders.Length == 0,
            "an MDM test names the shared Mongo — connect through MdmTestMongo.ConnectionString and refuse the port with "
            + "MdmTestMongo.SharedPort:\n" + string.Join("\n", offenders));
        Assert.True(stale.Length == 0, "no longer names the shared Mongo — take it off NotYetMoved:\n" + string.Join("\n", stale));
    }

    /// <summary>
    /// FIX2 (CT N4): the guard's decision alone — a source that names the shared Mongo and is neither listed nor exempt
    /// is an OFFENDER; a listed file that no longer names it is STALE. Tested with fake input, because on the real tree
    /// both listed files still name the port and the stale branch never fires.
    /// </summary>
    internal static (string[] Offenders, string[] Stale) Judge(IEnumerable<string> sources, Func<string, string> read,
        IEnumerable<string> notYetMoved, IEnumerable<string> exempt)
    {
        var listed = notYetMoved.ToArray();
        var naming = sources.Where(path => SharedMongoPattern.IsMatch(read(path))).ToHashSet(StringComparer.Ordinal);
        var offenders = naming.Except(listed).Except(exempt).Order(StringComparer.Ordinal).ToArray();
        var stale = listed.Where(path => !naming.Contains(path)).Order(StringComparer.Ordinal).ToArray();
        return (offenders, stale);
    }

    // FIX2 (CT N4): offenders and stale entries, on fake sources.
    [Fact]
    public void BL595_The_guard_names_an_offender_and_a_stale_entry()
    {
        var files = new Dictionary<string, string>
        {
            ["a/Moved.cs"] = "var c = MdmTestMongo.ConnectionString;",
            ["a/StillListed.cs"] = "Assert.NotEqual(" + MdmTestMongo.SharedPortText + ", port);",
            ["a/CleanNowButListed.cs"] = "Assert.NotEqual(MdmTestMongo.SharedPort, port);",
            ["a/NewOffender.cs"] = "new MongoClient(\"mongodb://localhost\")",
            ["a/Helper.cs"] = "public const int SharedPort = " + MdmTestMongo.SharedPortText + ";"
        };

        var (offenders, stale) = Judge(files.Keys, path => files[path],
            ["a/StillListed.cs", "a/CleanNowButListed.cs"], ["a/Helper.cs"]);

        Assert.Equal(new[] { "a/NewOffender.cs" }, offenders);
        Assert.Equal(new[] { "a/CleanNowButListed.cs" }, stale);
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
    // FIX2
    [InlineData("var port = 27_017;", true)]
    [InlineData("const long port = " + MdmTestMongo.SharedPortText + "L;", true)]
    [InlineData("mongodb://127.0.0.1:41234,localhost:41235", true)]
    [InlineData("mongodb://127.0.0.1:41234,127.0.0.1", true)]
    [InlineData("mongodb://127.0.0.1:41234,127.0.0.1:41235/?replicaSet=x", false)]
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

    // ------------------------------------------------------------------------------------------- the sweep (FIX1/FIX2)
    // One rule: in doubt, Keep. Shapes: a record (or none) and the processes the probe reports.

    private const string MongodPath = "/opt/test/bin/mongod";
    private static readonly DateTime T0 = new(2026, 10, 11, 8, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Young = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Old = MdmTestMongo.RecordlessGrace + TimeSpan.FromMinutes(1);

    [Theory]
    // a live run, by the loose host tolerance (a clock step within it is the same host — FIX2 (5))
    [InlineData("host-alive", MdmTestMongo.SweepAction.Keep)]
    [InlineData("host-start-9s-later", MdmTestMongo.SweepAction.Keep)]
    [InlineData("host-start-9s-earlier", MdmTestMongo.SweepAction.Keep)]
    [InlineData("host-start-11s-earlier", MdmTestMongo.SweepAction.StopMongodAndDeleteDirectory)]
    // a dead run
    [InlineData("host-dead-mongod-alive", MdmTestMongo.SweepAction.StopMongodAndDeleteDirectory)]
    [InlineData("host-dead-mongod-gone", MdmTestMongo.SweepAction.DeleteDirectory)]
    [InlineData("host-dead-pid-reused", MdmTestMongo.SweepAction.DeleteDirectory)]
    [InlineData("host-dead-no-mongod-yet", MdmTestMongo.SweepAction.DeleteDirectory)]
    // the stop is strict (FIX2 (4)/(5)): start within 1 s AND the executable path
    [InlineData("mongod-start-0.9s", MdmTestMongo.SweepAction.StopMongodAndDeleteDirectory)]
    [InlineData("mongod-start-1.5s", MdmTestMongo.SweepAction.Keep)]
    [InlineData("mongod-start-minus-1.5s", MdmTestMongo.SweepAction.Keep)]
    [InlineData("mongod-path-differs", MdmTestMongo.SweepAction.Keep)]
    [InlineData("mongod-path-unreadable", MdmTestMongo.SweepAction.Keep)]
    [InlineData("record-without-path", MdmTestMongo.SweepAction.Keep)]
    // UNKNOWN is doubt (FIX2 (1))
    [InlineData("host-unknown", MdmTestMongo.SweepAction.Keep)]
    [InlineData("mongod-unknown", MdmTestMongo.SweepAction.Keep)]
    // an empty or impossible record is doubt
    [InlineData("host-pid-zero", MdmTestMongo.SweepAction.Keep)]
    [InlineData("host-start-default", MdmTestMongo.SweepAction.Keep)]
    [InlineData("mongod-pid-one", MdmTestMongo.SweepAction.Keep)]
    // no mongod recorded yet and a dead host: the lock file decides
    [InlineData("no-mongod-yet-lock-held", MdmTestMongo.SweepAction.Keep)]
    [InlineData("no-mongod-yet-lock-unreadable", MdmTestMongo.SweepAction.Keep)]
    // FIX3: a recorded mongod that is gone, or one started well apart, still yields to a held / unreadable / unknown lock
    [InlineData("mongod-gone-lock-held", MdmTestMongo.SweepAction.Keep)]
    [InlineData("mongod-gone-lock-unreadable", MdmTestMongo.SweepAction.Keep)]
    [InlineData("mongod-gone-lock-unknown", MdmTestMongo.SweepAction.Keep)]
    [InlineData("mongod-far-lock-held", MdmTestMongo.SweepAction.Keep)]
    [InlineData("mongod-far-lock-unreadable", MdmTestMongo.SweepAction.Keep)]
    // recordless
    [InlineData("no-record", MdmTestMongo.SweepAction.Keep)]
    [InlineData("no-record-old", MdmTestMongo.SweepAction.DeleteDirectory)]
    [InlineData("no-record-age-unknown", MdmTestMongo.SweepAction.Keep)]
    [InlineData("no-record-old-lock-held", MdmTestMongo.SweepAction.Keep)]
    [InlineData("no-record-old-lock-dead", MdmTestMongo.SweepAction.DeleteDirectory)]
    [InlineData("no-record-old-lock-unknown", MdmTestMongo.SweepAction.Keep)]
    [InlineData("no-record-old-lock-unreadable", MdmTestMongo.SweepAction.Keep)]
    [InlineData("no-record-old-lock-pid-one", MdmTestMongo.SweepAction.Keep)]
    public void BL595_The_sweep_rule(string shape, MdmTestMongo.SweepAction expected)
    {
        var (record, processes, age, lockFile) = SweepShape(shape);
        Assert.Equal(expected, MdmTestMongo.Decide(record, ProbeOf(processes), age, lockFile));
    }

    // FIX2 (5): the two tolerances, at their edges and in both directions.
    [Theory]
    [InlineData(0, 1, true)]
    [InlineData(999, 1, true)]
    [InlineData(1000, 1, true)]
    [InlineData(1001, 1, false)]
    [InlineData(-1001, 1, false)]
    [InlineData(-999, 1, true)]
    [InlineData(10000, 10, true)]
    [InlineData(-10001, 10, false)]
    public void BL595_The_tolerances_are_symmetric_and_bounded(int milliseconds, int seconds, bool within)
    {
        Assert.Equal(within, MdmTestMongo.Within(T0.AddMilliseconds(milliseconds), T0, TimeSpan.FromSeconds(seconds)));
        Assert.Equal(TimeSpan.FromSeconds(1), MdmTestMongo.StopTolerance);
        Assert.Equal(TimeSpan.FromSeconds(10), MdmTestMongo.HostTolerance);
    }

    private static Func<int, MdmTestMongo.ProcessProbe> ProbeOf(Dictionary<int, MdmTestMongo.ProcessProbe> processes) =>
        pid => processes.TryGetValue(pid, out var probe) ? probe : MdmTestMongo.ProcessProbe.Gone;

    private static MdmTestMongo.ProcessProbe Alive(DateTime started, string? path = MongodPath) =>
        new(MdmTestMongo.ProcessState.Alive, started, path);

    // A record (or none), the processes by PID, the directory's age and its mongod.lock — for one shape.
    private static (MdmTestMongo.OwnerRecord? Record, Dictionary<int, MdmTestMongo.ProcessProbe> Processes, TimeSpan? Age,
        MdmTestMongo.LockRead Lock) SweepShape(string shape, int offset = 0)
    {
        int host = 1001 + offset, mongod = 2001 + offset, holder = 3001 + offset;
        var processes = new Dictionary<int, MdmTestMongo.ProcessProbe>();
        MdmTestMongo.OwnerRecord? record = new(mongod, T0.AddSeconds(5), host, T0, MongodPath);
        TimeSpan? age = Young;
        var lockFile = MdmTestMongo.LockRead.None;
        switch (shape)
        {
            case "host-alive": processes[host] = Alive(T0, "/usr/bin/dotnet"); processes[mongod] = Alive(T0.AddSeconds(5)); break;
            case "host-start-9s-later": processes[host] = Alive(T0.AddSeconds(9)); processes[mongod] = Alive(T0.AddSeconds(5)); break;
            case "host-start-9s-earlier": processes[host] = Alive(T0.AddSeconds(-9)); processes[mongod] = Alive(T0.AddSeconds(5)); break;
            case "host-start-11s-earlier": processes[host] = Alive(T0.AddSeconds(-11)); processes[mongod] = Alive(T0.AddSeconds(5)); break;
            case "host-dead-mongod-alive": processes[mongod] = Alive(T0.AddSeconds(5)); break;
            case "host-dead-mongod-gone": break;
            case "host-dead-pid-reused": processes[mongod] = Alive(T0.AddHours(3), "/usr/bin/other"); break;
            case "host-dead-no-mongod-yet": record = record with { MongodPid = null, MongodStartedUtc = null, MongodPath = null }; break;
            case "mongod-start-0.9s": processes[mongod] = Alive(T0.AddSeconds(5).AddMilliseconds(900)); break;
            case "mongod-start-1.5s": processes[mongod] = Alive(T0.AddSeconds(5).AddMilliseconds(1500)); break;
            case "mongod-start-minus-1.5s": processes[mongod] = Alive(T0.AddSeconds(5).AddMilliseconds(-1500)); break;
            case "mongod-path-differs": processes[mongod] = Alive(T0.AddSeconds(5), "/usr/bin/sleep"); break;
            case "mongod-path-unreadable": processes[mongod] = Alive(T0.AddSeconds(5), null); break;
            case "record-without-path": record = record with { MongodPath = null }; processes[mongod] = Alive(T0.AddSeconds(5)); break;
            case "host-unknown": processes[host] = MdmTestMongo.ProcessProbe.Unknown; break;
            case "mongod-unknown": processes[mongod] = MdmTestMongo.ProcessProbe.Unknown; break;
            case "host-pid-zero": record = record with { HostPid = 0 }; break;
            case "host-start-default": record = record with { HostStartedUtc = default }; break;
            case "mongod-pid-one": record = record with { MongodPid = 1 }; break;
            case "no-mongod-yet-lock-held":
                record = record with { MongodPid = null, MongodStartedUtc = null, MongodPath = null };
                lockFile = new(false, holder); processes[holder] = Alive(T0); break;
            case "no-mongod-yet-lock-unreadable":
                record = record with { MongodPid = null, MongodStartedUtc = null, MongodPath = null };
                lockFile = MdmTestMongo.LockRead.CannotRead; break;
            case "mongod-gone-lock-held": lockFile = new(false, holder); processes[holder] = Alive(T0); break;
            case "mongod-gone-lock-unreadable": lockFile = MdmTestMongo.LockRead.CannotRead; break;
            case "mongod-gone-lock-unknown": lockFile = new(false, holder); processes[holder] = MdmTestMongo.ProcessProbe.Unknown; break;
            case "mongod-far-lock-held":
                processes[mongod] = Alive(T0.AddHours(3), "/usr/bin/other"); lockFile = new(false, holder); processes[holder] = Alive(T0);
                break;
            case "mongod-far-lock-unreadable":
                processes[mongod] = Alive(T0.AddHours(3), "/usr/bin/other"); lockFile = MdmTestMongo.LockRead.CannotRead; break;
            case "no-record": record = null; break;
            case "no-record-old": record = null; age = Old; break;
            case "no-record-age-unknown": record = null; age = null; break;
            case "no-record-old-lock-held": record = null; age = Old; lockFile = new(false, holder); processes[holder] = Alive(T0); break;
            case "no-record-old-lock-dead": record = null; age = Old; lockFile = new(false, holder); break;
            case "no-record-old-lock-unknown":
                record = null; age = Old; lockFile = new(false, holder); processes[holder] = MdmTestMongo.ProcessProbe.Unknown; break;
            case "no-record-old-lock-unreadable": record = null; age = Old; lockFile = MdmTestMongo.LockRead.CannotRead; break;
            case "no-record-old-lock-pid-one": record = null; age = Old; lockFile = new(false, 1); break;
            default: throw new ArgumentOutOfRangeException(nameof(shape), shape, null);
        }
        return (record, processes, age, lockFile);
    }

    [Theory]
    [InlineData("4242\n", 4242)]
    [InlineData("", null)]
    [InlineData("not-a-pid", null)]
    [InlineData(null, null)]
    public void BL595_The_lock_holder_is_read_from_mongod_lock(string? content, int? expected)
    {
        var directory = NewScratch("locktest");
        try
        {
            if (content is not null) File.WriteAllText(Path.Combine(directory, "mongod.lock"), content);
            var read = MdmTestMongo.LockPid(directory);
            Assert.False(read.Unreadable);
            Assert.Equal(expected, read.Pid);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // FIX2 (2): a lock file that exists but cannot be READ (a Windows mongod holds it exclusively; here: no permission)
    // is "unreadable" — never "none" — and a recordless old directory holding one is kept.
    [SkippableFact]
    public void BL595_An_unreadable_mongod_lock_keeps_the_directory()
    {
        Skip.If(OperatingSystem.IsWindows(), "permission bits are the Unix simulation of a held lock");
        var root = NewScratch("sweeptest");
        var directory = Path.Combine(root, MdmTestMongo.DirectoryPrefix + "unreadable");
        Directory.CreateDirectory(directory);
        var lockPath = Path.Combine(directory, "mongod.lock");
        File.WriteAllText(lockPath, "4242");
        File.SetUnixFileMode(lockPath, UnixFileMode.None);
        try
        {
            // FIX3: root reads a mode-000 file — then this simulation does not hold; skipped by name, not passed.
            Skip.If(CanRead(lockPath), "running as root: mode 000 does not make the lock unreadable");
            Assert.True(MdmTestMongo.LockPid(directory).Unreadable);

            var action = Assert.Single(MdmTestMongo.Sweep(root, _ => MdmTestMongo.ProcessProbe.Gone, _ => true, _ => Old)).Action;

            Assert.Equal(MdmTestMongo.SweepAction.Keep, action);
            Assert.True(File.Exists(lockPath));
        }
        finally
        {
            File.SetUnixFileMode(lockPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            Directory.Delete(root, recursive: true);
        }
    }

    // The sweep on a real directory tree: a live run's directory stays, a dead run's goes, a process whose start time
    // does not match is never stopped, an old recordless directory goes unless a live process holds its lock, a young one
    // stays, nothing outside the prefix is touched.
    [Fact]
    public void BL595_The_sweep_removes_only_dead_runs_and_stops_only_a_verified_mongod()
    {
        var root = NewScratch("sweeptest");
        try
        {
            var directories = new Dictionary<string, string>();
            var processes = new Dictionary<int, MdmTestMongo.ProcessProbe>();
            var shapes = new[] { "host-alive", "host-dead-mongod-alive", "host-dead-mongod-gone", "host-dead-pid-reused", "no-record" };
            foreach (var shape in shapes)
            {
                var (record, alive, _, _) = SweepShape(shape, offset: directories.Count * 100);
                foreach (var pair in alive) processes[pair.Key] = pair.Value;
                var directory = Path.Combine(root, MdmTestMongo.DirectoryPrefix + shape);
                Directory.CreateDirectory(directory);
                if (record is not null) MdmTestMongo.WriteOwner(directory, record);
                directories[shape] = directory;
            }
            // Recordless and old: one whose lock names a live process (this test's own), one whose lock is stale.
            var heldOld = Path.Combine(root, MdmTestMongo.DirectoryPrefix + "old-lock-held");
            var staleOld = Path.Combine(root, MdmTestMongo.DirectoryPrefix + "old-lock-stale");
            foreach (var (directory, holder) in new[] { (heldOld, Environment.ProcessId), (staleOld, 9_999_001) })
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "mongod.lock"), holder.ToString());
            }
            processes[Environment.ProcessId] = Alive(DateTime.UtcNow);
            var stranger = Path.Combine(root, "someone_else_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stranger);
            var stopped = new List<MdmTestMongo.OwnerRecord>();

            // FIX3: the age is injected (a file system's creation time cannot be set everywhere — ubuntu CI).
            var actions = MdmTestMongo.Sweep(root, ProbeOf(processes), record => { stopped.Add(record); return true; },
                entry => entry.Name.Contains("old-lock", StringComparison.Ordinal) ? Old : Young);

            Assert.True(Directory.Exists(directories["host-alive"]));
            Assert.True(Directory.Exists(directories["no-record"]));
            Assert.True(Directory.Exists(heldOld));
            Assert.False(Directory.Exists(staleOld));
            Assert.False(Directory.Exists(directories["host-dead-mongod-alive"]));
            Assert.False(Directory.Exists(directories["host-dead-mongod-gone"]));
            Assert.False(Directory.Exists(directories["host-dead-pid-reused"]));
            Assert.True(Directory.Exists(stranger));
            // Only the dead run's live mongod (directory 1: PIDs offset by 100), by its recorded PID, start and path.
            var only = Assert.Single(stopped);
            Assert.Equal((2101, T0.AddSeconds(5), MongodPath), (only.MongodPid!.Value, only.MongodStartedUtc!.Value, only.MongodPath));
            Assert.Equal(7, actions.Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // FIX2 (3): a link is never followed — neither a prefixed link at the top (its target's content stays) nor a link
    // inside an owned directory (the link goes, its target stays).
    [Fact]
    public void BL595_The_sweep_never_follows_a_link()
    {
        var root = NewScratch("sweeptest");
        var outside = NewScratch("linktarget");
        try
        {
            var (dead, _, _, _) = SweepShape("host-dead-mongod-gone");
            MdmTestMongo.WriteOwner(outside, dead!);
            var precious = Path.Combine(outside, "precious.txt");
            File.WriteAllText(precious, "keep me");
            var topLink = Path.Combine(root, MdmTestMongo.DirectoryPrefix + "link");
            Directory.CreateSymbolicLink(topLink, outside);

            var owned = Path.Combine(root, MdmTestMongo.DirectoryPrefix + "with-inner");
            Directory.CreateDirectory(owned);
            MdmTestMongo.WriteOwner(owned, dead!);
            Directory.CreateSymbolicLink(Path.Combine(owned, "inner"), outside);

            var actions = MdmTestMongo.Sweep(root, _ => MdmTestMongo.ProcessProbe.Gone, _ => true);

            Assert.True(File.Exists(precious));
            Assert.True(File.Exists(Path.Combine(outside, MdmTestMongo.OwnerFile)));
            Assert.Equal(MdmTestMongo.SweepAction.Keep, actions.Single(x => x.Directory == topLink).Action);
            Assert.False(Directory.Exists(owned));

            // And a direct cleanup call on the link refuses it.
            MdmTestMongo.DeleteOwnDirectory(root, topLink);
            Assert.True(File.Exists(precious));
        }
        finally
        {
            foreach (var entry in new DirectoryInfo(root).EnumerateFileSystemInfos())
                if (MdmTestMongo.IsLink(entry)) entry.Delete();
            Directory.Delete(root, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }

    // The cleanup's scope: only a prefixed child of the root — never a non-prefixed one, never one outside the root.
    [Fact]
    public void BL595_A_cleanup_touches_only_a_prefixed_child_of_its_root()
    {
        var root = NewScratch("sweeptest");
        var outsideRoot = NewScratch("outside");
        try
        {
            var unprefixed = Path.Combine(root, "not_ours");
            var deeper = Path.Combine(root, "nested", MdmTestMongo.DirectoryPrefix + "deep");
            var foreign = Path.Combine(outsideRoot, MdmTestMongo.DirectoryPrefix + "foreign");
            foreach (var directory in new[] { unprefixed, deeper, foreign }) Directory.CreateDirectory(directory);

            MdmTestMongo.DeleteOwnDirectory(root, unprefixed);
            MdmTestMongo.DeleteOwnDirectory(root, deeper);
            MdmTestMongo.DeleteOwnDirectory(root, foreign);

            Assert.True(Directory.Exists(unprefixed));
            Assert.True(Directory.Exists(deeper));
            Assert.True(Directory.Exists(foreign));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Directory.Delete(outsideRoot, recursive: true);
        }
    }

    // FIX1 (measured leak: an exit-time cleanup cut short left data files and no record): the owner record is removed
    // LAST, so an interrupted cleanup still carries it and the next start sweeps the rest at once.
    [Fact]
    public void BL595_A_cleanup_removes_the_owner_record_last()
    {
        var root = NewScratch("sweeptest");
        var directory = Path.Combine(root, MdmTestMongo.DirectoryPrefix + "order");
        Directory.CreateDirectory(Path.Combine(directory, "journal"));
        try
        {
            var (record, _, _, _) = SweepShape("host-dead-mongod-gone");
            MdmTestMongo.WriteOwner(directory, record!);
            foreach (var name in new[] { "WiredTiger.wt", "zz-collection.wt", "mongod.lock" })
                File.WriteAllText(Path.Combine(directory, name), "x");

            var order = MdmTestMongo.DeletionOrder(directory);

            Assert.Equal(5, order.Count);
            Assert.Equal(Path.Combine(directory, MdmTestMongo.OwnerFile), order[^1]);

            MdmTestMongo.Sweep(root, _ => MdmTestMongo.ProcessProbe.Gone, _ => true);
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
        var root = NewScratch("sweeptest");
        var directory = Path.Combine(root, MdmTestMongo.DirectoryPrefix + "unverified");
        Directory.CreateDirectory(directory);
        try
        {
            var (record, processes, _, _) = SweepShape("host-dead-mongod-alive");
            MdmTestMongo.WriteOwner(directory, record!);

            var actions = MdmTestMongo.Sweep(root, ProbeOf(processes), _ => false);

            Assert.True(Directory.Exists(directory));
            Assert.Equal(MdmTestMongo.SweepAction.Keep, Assert.Single(actions).Action);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // FIX2: the sweep at start is best effort — a failure inside it never fails the tests' start.
    [Fact]
    public void BL595_A_failing_sweep_never_fails_the_start()
    {
        var root = NewScratch("sweeptest");
        Directory.CreateDirectory(Path.Combine(root, MdmTestMongo.DirectoryPrefix + "boom"));
        var (record, _, _, _) = SweepShape("host-alive");
        MdmTestMongo.WriteOwner(Path.Combine(root, MdmTestMongo.DirectoryPrefix + "boom"), record!);
        try
        {
            MdmTestMongo.SweepBestEffort(root, _ => throw new InvalidOperationException("probe failed"), _ => true);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // FIX2 (4): the ONE stop path against a REAL process (a child this test starts, not a mongod): stopped only when the
    // PID, the start time AND the executable path all match the record.
    [SkippableTheory]
    [InlineData("start-differs", false)]
    [InlineData("path-differs", false)]
    [InlineData("no-path", false)]
    [InlineData("all-match", true)]
    public void BL595_The_stop_needs_pid_start_and_executable_path(string shape, bool stops)
    {
        Skip.If(OperatingSystem.IsWindows(), "the child is /bin/sleep");
        using var child = Process.Start(new ProcessStartInfo("/bin/sleep", "60") { UseShellExecute = false })!;
        try
        {
            // TEST-HIZ-01: Process.Start returns after the fork, before the child has exec'd /bin/sleep; probed in that
            // window the path is null (CI: Assert.NotNull failed) or still the test host's. Wait until the child IS sleep.
            var probe = MdmTestMongo.Probe(child.Id);
            var execDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (DateTime.UtcNow < execDeadline && probe.State == MdmTestMongo.ProcessState.Alive
                   && !string.Equals(System.IO.Path.GetFileName(probe.Path), "sleep", StringComparison.Ordinal))
            {
                Thread.Sleep(20);
                probe = MdmTestMongo.Probe(child.Id);
            }

            Assert.Equal(MdmTestMongo.ProcessState.Alive, probe.State);
            Assert.NotNull(probe.Path);
            var record = new MdmTestMongo.OwnerRecord(child.Id, probe.StartedUtc, Environment.ProcessId, DateTime.UtcNow,
                probe.Path);
            record = shape switch
            {
                "start-differs" => record with { MongodStartedUtc = probe.StartedUtc.AddSeconds(5) },
                "path-differs" => record with { MongodPath = "/usr/bin/true" },
                "no-path" => record with { MongodPath = null },
                _ => record
            };

            var stopped = MdmTestMongo.StopVerified(record);

            Assert.Equal(stops, stopped);
            Assert.Equal(stops, child.WaitForExit(stops ? 15000 : 300));
        }
        finally
        {
            // Only this test's own child; a race with its own exit is harmless.
            try
            {
                if (!child.HasExited) child.Kill();
                child.WaitForExit(5000);
            }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        }
    }

    // FIX2 (1): a PID no process has is GONE (not unknown); this process is alive with its own start and path.
    [Fact]
    public void BL595_The_probe_says_gone_and_alive()
    {
        Assert.Equal(MdmTestMongo.ProcessState.Gone, MdmTestMongo.Probe(9_999_001).State);
        var self = MdmTestMongo.Probe(Environment.ProcessId);
        Assert.Equal(MdmTestMongo.ProcessState.Alive, self.State);
        Assert.True(MdmTestMongo.Within(self.StartedUtc, Process.GetCurrentProcess().StartTime.ToUniversalTime(),
            MdmTestMongo.StopTolerance));
    }

    // FIX2: an age that cannot be read is unknown.
    [Fact]
    public void BL595_An_unreadable_age_is_unknown()
    {
        Assert.Null(MdmTestMongo.AgeOf(new DirectoryInfo(Path.Combine(Path.GetTempPath(), "diten_mdm_missing_" + Guid.NewGuid().ToString("N")))));
    }

    // FIX3: an owner record that EXISTS but cannot be read or parsed is doubt (Keep) — never "recordless" (which an old
    // directory would lose).
    [SkippableTheory]
    [InlineData("corrupt")]
    [InlineData("unreadable")]
    public void BL595_An_owner_record_that_cannot_be_read_keeps_the_directory(string shape)
    {
        var root = NewScratch("sweeptest");
        var directory = Path.Combine(root, MdmTestMongo.DirectoryPrefix + shape);
        Directory.CreateDirectory(directory);
        var owner = Path.Combine(directory, MdmTestMongo.OwnerFile);
        File.WriteAllText(owner, shape == "corrupt" ? "{ not json" : "{}");
        try
        {
            if (shape == "unreadable")
            {
                Skip.If(OperatingSystem.IsWindows(), "permission bits are the Unix simulation");
                File.SetUnixFileMode(owner, UnixFileMode.None);
                Skip.If(CanRead(owner), "running as root: mode 000 does not make the record unreadable");
            }

            var action = Assert.Single(MdmTestMongo.Sweep(root, _ => MdmTestMongo.ProcessProbe.Gone, _ => true, _ => Old)).Action;

            Assert.Equal(MdmTestMongo.SweepAction.Keep, action);
            Assert.True(Directory.Exists(directory));
        }
        finally
        {
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(owner, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            Directory.Delete(root, recursive: true);
        }
    }

    // FIX3 (2): the record a REAL start writes — PID, start time and the executable path as Probe reads it (non-empty);
    // without the path no leftover of it would ever be stopped.
    [Fact]
    public void BL595_A_real_start_records_pid_start_and_path()
    {
        var (directory, pid) = MdmTestMongo.OwnedIdentity;
        AssertRecordOf(directory, pid);
    }

    internal static void AssertRecordOf(string directory, int pid)
    {
        var record = MdmTestMongo.ReadOwner(directory);
        var probe = MdmTestMongo.Probe(pid);

        Assert.NotNull(record);
        Assert.Equal(MdmTestMongo.ProcessState.Alive, probe.State);
        Assert.Equal(pid, record!.MongodPid);
        Assert.Equal(Environment.ProcessId, record.HostPid);
        Assert.True(MdmTestMongo.Within(record.MongodStartedUtc!.Value, probe.StartedUtc, MdmTestMongo.StopTolerance));
        Assert.False(string.IsNullOrEmpty(record.MongodPath));
        Assert.Equal(probe.Path, record.MongodPath);
    }

    private static bool CanRead(string path)
    {
        try { File.ReadAllText(path); return true; }
        catch (UnauthorizedAccessException) { return false; }
        catch (IOException) { return false; }
    }

    private static string NewScratch(string kind)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"diten_mdm_{kind}_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
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
/// FIX2 (6): the redirect is set in-process by a fixture (<see cref="MdmTestMongo.UseOwnedRedirect"/>) and NEVER read
/// from the environment — a <c>MDM_TEST_MONGO</c> left in a shell does not move a test's writes. In the serial
/// collection (the redirecting fixtures run there too); everything set is restored.
/// </summary>
[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class MdmTestMongoRedirectTests
{
    [Theory]
    [InlineData("mongodb://127.0.0.1:27018")]
    [InlineData("mongodb://127.0.0.1:41234/?replicaSet=x&directConnection=true")]
    [InlineData("mongodb://db.example.com:41234")]
    public void BL595_An_environment_value_never_moves_the_connection(string value)
    {
        var previous = Environment.GetEnvironmentVariable(MdmTestMongo.RedirectVariable);
        Environment.SetEnvironmentVariable(MdmTestMongo.RedirectVariable, value);
        try
        {
            Assert.Equal(MdmTestMongo.OwnedConnectionString, MdmTestMongo.ConnectionString);
        }
        finally
        {
            Environment.SetEnvironmentVariable(MdmTestMongo.RedirectVariable, previous);
        }
    }

    [Fact]
    public void BL595_A_fixture_redirect_is_followed_and_restored()
    {
        const string first = "mongodb://127.0.0.1:41234/?replicaSet=x&directConnection=true";
        const string second = "mongodb://127.0.0.1:41235/?replicaSet=y&directConnection=true";

        using (MdmTestMongo.UseOwnedRedirect(first))
        {
            Assert.Equal(first, MdmTestMongo.ConnectionString);
            using (MdmTestMongo.UseOwnedRedirect(second)) Assert.Equal(second, MdmTestMongo.ConnectionString);
            Assert.Equal(first, MdmTestMongo.ConnectionString);
        }

        Assert.Equal(MdmTestMongo.OwnedConnectionString, MdmTestMongo.ConnectionString);
    }

    [Theory]
    [InlineData("mongodb://127.0.0.1:" + MdmTestMongo.SharedPortText)]
    [InlineData("mongodb://db.example.com:41234")]
    [InlineData("not a uri")]
    public void BL595_A_fixture_redirect_to_the_shared_port_or_a_remote_host_is_refused(string value)
    {
        Assert.StartsWith(MdmTestMongo.RedirectInvalid + ":",
            Assert.Throws<InvalidOperationException>(() => MdmTestMongo.UseOwnedRedirect(value)).Message);
        Assert.Equal(MdmTestMongo.OwnedConnectionString, MdmTestMongo.ConnectionString);
    }

    // FIX3: an empty redirect is refused by its own name.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void BL595_An_empty_fixture_redirect_is_refused_by_name(string? value)
    {
        var error = Assert.Throws<ArgumentException>(() => MdmTestMongo.UseOwnedRedirect(value!));
        Assert.StartsWith(MdmTestMongo.RedirectEmpty + ":", error.Message);
    }
}

/// <summary>BL-595 FIX3 (2): the record the temporal fixture's real start writes, as the helper's.</summary>
[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class MdmTemporalFixtureRecordTests(Audit.AuditIntentTemporalMongoFixture fixture)
    : IClassFixture<Audit.AuditIntentTemporalMongoFixture>
{
    [Fact]
    public void BL595_The_temporal_fixture_records_pid_start_and_path()
    {
        var (directory, pid) = fixture.ReplicaIdentity;
        Assert.StartsWith(MdmTestMongo.DirectoryPrefix, Path.GetFileName(directory));
        MdmTestMongoTests.AssertRecordOf(directory, pid);
    }
}
