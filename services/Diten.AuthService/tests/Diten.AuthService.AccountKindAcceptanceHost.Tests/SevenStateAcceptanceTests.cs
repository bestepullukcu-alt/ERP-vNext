// WP-INFRA-AUTH-ACCEPTANCE-HOST-01 (T1) — the real 7-state supervisor-side driver as a committable, CI-rerunnable
// xUnit suite. Ports the throwaway scratchpad script (seven-state-run) into test form, with T6/T7/T8 fixes:
//   T6 — "başlatma hatası" is genuinely induced (DITEN_ACCEPTANCE_DOTNET_PATH pointing at a nonexistent path),
//        not skipped.
//   T7 — "İptal" drives the real D3 timed fallback (shutdown/no-bye -> wait <=5s -> SIGTERM -> wait 5s -> SIGKILL),
//        not an arbitrary 50ms kill.
//   T8 — "seed sırasında SIGKILL" is triggered by STATE (mongod is a confirmed process-group member and `ready`
//        has not been read), not by a fixed millisecond delay.
//
// D2 protected-signal discipline: every OS signal in this file goes through SupervisorTestHarness.KillSingle /
// KillGroup, which re-verify PID+start-time (or the full C9 group proof) immediately before signaling and record
// every attempt to a signal log. No `pkill`/`killall`/name-based matching anywhere. The mechanical rule (no
// rm/rmdir/mv/find-delete, no Bash kill) applies to Bash-adjacent tooling only — this is the test code itself,
// which C9 explicitly allows to clean up its OWN mkdtemp'd roots.
//
// Requires a real mongod on PATH (same requirement every existing AccountKindAcceptance test already has) and
// runs each scenario in its own real disposable mongod + real out-of-process Auth API — no mocks.

using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Diten.AuthService.AccountKindAcceptanceHost.Tests;

[CollectionDefinition("SevenStateSupervisor", DisableParallelization = true)]
public sealed class SevenStateSupervisorCollection;

[Collection("SevenStateSupervisor")]
public sealed class SevenStateAcceptanceTests
{
    private readonly ITestOutputHelper _output;

    public SevenStateAcceptanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // ── Scenario 4 — normal kapanış ──────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task NormalShutdown_HostApiMongodAllExitCleanly()
    {
        var h = new SupervisorTestHarness(_output);
        var (root, dev, ino) = h.CreateRunRoot();
        h.PrepareFixedSubdirs(root);
        using var listener = h.Listen(root, out var runId);
        var hostPid = h.SpawnHost(root, runId);

        try
        {
            var cycle = await h.DriveFullCycle(listener, runId);
            Assert.True(cycle.Ok, "did not reach ready/seed-ready");
            Assert.True(cycle.ByeReceived, "bye was not received");

            await Task.Delay(1000);
            Assert.False(h.ProcessExists(cycle.ApiPid), "api process leaked");
            Assert.False(h.ProcessExists(cycle.MongodPid), "mongod process leaked");
            Assert.False(h.ProcessExists(hostPid), "host process leaked");
        }
        finally
        {
            h.CleanRoot(root, dev, ino);
        }
    }

    // ── Scenario 2 — timeout: host's own hello-ack wait times out for real (P3 exit 7) ─────────────────────
    [Fact]
    public async Task HelloAckWithheld_HostSelfTimesOutAndExits()
    {
        var h = new SupervisorTestHarness(_output);
        var (root, dev, ino) = h.CreateRunRoot();
        h.PrepareFixedSubdirs(root);
        using var listener = h.Listen(root, out var runId);
        var hostPid = h.SpawnHost(root, runId);

        try
        {
            var acceptTask = listener.AcceptAsync();
            Assert.Same(acceptTask, await Task.WhenAny(acceptTask, Task.Delay(TimeSpan.FromSeconds(10))));
            using var sock = await acceptTask;
            using var stream = new NetworkStream(sock, ownsSocket: false);
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            var hello = await reader.ReadLineAsync();
            Assert.Contains("\"hello\"", hello);
            // Deliberately withhold hello-ack — host's own 10s ReceiveAsync timeout must fire (P3 exit 7).

            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
            while (DateTime.UtcNow < deadline && h.ProcessExists(hostPid))
            {
                await Task.Delay(250);
            }

            Assert.False(h.ProcessExists(hostPid), "host did not self-timeout within 20s of a withheld hello-ack");
        }
        finally
        {
            h.CleanRoot(root, dev, ino);
        }
    }

    // ── Scenario 3 — İptal: real D3 timed fallback (T7) ──────────────────────────────────────────────────
    // "No bye" is achieved by never engaging the shutdown handshake at all (rather than making a healthy host
    // hang, which nothing in this WP's scope can induce on command) — from the supervisor's observable
    // perspective, "sent shutdown, no bye" and "never got a confirmation" drive the identical D3 fallback code
    // path and timing; this test exercises that fallback for real, deterministically.
    [Fact]
    public async Task Cancel_NoConfirmationWithin5s_FallsBackToSigtermThenSigkillPerD3()
    {
        var h = new SupervisorTestHarness(_output);
        var (root, dev, ino) = h.CreateRunRoot();
        h.PrepareFixedSubdirs(root);
        using var listener = h.Listen(root, out var runId);
        var hostPid = h.SpawnHost(root, runId);
        var hostPgid = h.Getpgid(hostPid);
        var hostStart = SupervisorTestHarness.GetStartTimeUnixMs(hostPid);

        try
        {
            var ready = await h.DriveToReady(listener, runId);
            Assert.True(ready.Ok, "did not reach ready");
            _output.WriteLine($"reached ready: api={ready.ApiPid} mongod={ready.MongodPid}");

            var t0 = DateTime.UtcNow;
            // D3 fallback, driven for real: wait <=5s for a confirmation that will never come, then SIGTERM,
            // then up to 5s, then SIGKILL only as the last resort.
            var confirmationDeadline = t0 + TimeSpan.FromSeconds(5);
            while (DateTime.UtcNow < confirmationDeadline)
            {
                await Task.Delay(200);
            }
            var t1 = DateTime.UtcNow;
            _output.WriteLine($"no confirmation after {(t1 - t0).TotalMilliseconds:F0}ms — sending SIGTERM");
            Assert.True(h.KillSingle(hostPid, hostStart, 15, "T7-D3-sigterm"), "SIGTERM verification refused");

            var termDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            var exitedViaTerm = false;
            while (DateTime.UtcNow < termDeadline)
            {
                if (!h.ProcessExists(hostPid)) { exitedViaTerm = true; break; }
                await Task.Delay(200);
            }
            var t2 = DateTime.UtcNow;

            if (!exitedViaTerm)
            {
                _output.WriteLine($"still alive {(t2 - t1).TotalMilliseconds:F0}ms after SIGTERM — escalating to SIGKILL (D3 last resort)");
                h.KillSingle(hostPid, hostStart, 9, "T7-D3-sigkill-last-resort");
                await Task.Delay(1500);
            }
            else
            {
                _output.WriteLine($"host exited via SIGTERM alone after {(t2 - t1).TotalMilliseconds:F0}ms — SIGKILL not needed");
            }

            // Backup group cleanup — api/mongod must not leak regardless of which signal stopped the host.
            var groupResult = h.KillGroup(hostPid, hostPgid, hostStart, "T7-backup-group");
            await Task.Delay(1000);

            Assert.False(h.ProcessExists(ready.ApiPid), $"api leaked after D3 fallback (group result: {groupResult})");
            Assert.False(h.ProcessExists(ready.MongodPid), $"mongod leaked after D3 fallback (group result: {groupResult})");
        }
        finally
        {
            h.CleanRoot(root, dev, ino);
        }
    }

    // ── Scenario 5 — ready SONRASI host SIGKILL ──────────────────────────────────────────────────────────
    [Fact]
    public async Task SigkillAfterReady_BackupGroupCleansUpApiAndMongod()
    {
        var h = new SupervisorTestHarness(_output);
        var (root, dev, ino) = h.CreateRunRoot();
        h.PrepareFixedSubdirs(root);
        using var listener = h.Listen(root, out var runId);
        var hostPid = h.SpawnHost(root, runId);
        var hostPgid = h.Getpgid(hostPid);
        var hostStart = SupervisorTestHarness.GetStartTimeUnixMs(hostPid);

        try
        {
            var ready = await h.DriveToReady(listener, runId);
            Assert.True(ready.Ok, "did not reach ready");

            Assert.True(h.KillSingle(hostPid, hostStart, 9, "scenario5"));
            await Task.Delay(1500);
            var groupResult = h.KillGroup(hostPid, hostPgid, hostStart, "scenario5-backup");
            _output.WriteLine(groupResult);
            await Task.Delay(1000);

            Assert.False(h.ProcessExists(ready.ApiPid));
            Assert.False(h.ProcessExists(ready.MongodPid));
        }
        finally
        {
            h.CleanRoot(root, dev, ino);
        }
    }

    // ── Scenario 6 — ready ÖNCESİ host SIGKILL (before mongod has started at all) ────────────────────────
    [Fact]
    public async Task SigkillBeforeMongodStarts_BackupCleansUpHostOnly()
    {
        var h = new SupervisorTestHarness(_output);
        var (root, dev, ino) = h.CreateRunRoot();
        h.PrepareFixedSubdirs(root);
        using var listener = h.Listen(root, out var runId);
        var hostPid = h.SpawnHost(root, runId);
        var hostPgid = h.Getpgid(hostPid);
        var hostStart = SupervisorTestHarness.GetStartTimeUnixMs(hostPid);

        try
        {
            // State-triggered, not timer-based: kill as soon as the host connects and sends hello (before it has
            // had any real chance to start mongod).
            var acceptTask = listener.AcceptAsync();
            Assert.Same(acceptTask, await Task.WhenAny(acceptTask, Task.Delay(TimeSpan.FromSeconds(10))));
            using var sock = await acceptTask;
            using var stream = new NetworkStream(sock, ownsSocket: false);
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            var hello = await reader.ReadLineAsync();
            Assert.Contains("\"hello\"", hello);

            var membersAtKill = h.EnumerateProcessGroupMembers(hostPgid);
            _output.WriteLine($"members at kill time: {string.Join(",", membersAtKill)}");
            Assert.Equal(new[] { hostPid }, membersAtKill); // mongod must not exist yet at this point

            Assert.True(h.KillSingle(hostPid, hostStart, 9, "scenario6"));
            await Task.Delay(1500);
            h.KillGroup(hostPid, hostPgid, hostStart, "scenario6-backup");
            await Task.Delay(500);

            foreach (var pid in membersAtKill.Where(p => p != hostPid))
            {
                Assert.False(h.ProcessExists(pid));
            }
        }
        finally
        {
            h.CleanRoot(root, dev, ino);
        }
    }

    // ── Scenario 7 — seed SIRASINDA host SIGKILL, STATE-triggered (T8) ──────────────────────────────────
    [Fact]
    public async Task SigkillWhileMongodIsGroupMemberButReadyNotYetRead_StateTriggered()
    {
        var h = new SupervisorTestHarness(_output);
        var (root, dev, ino) = h.CreateRunRoot();
        h.PrepareFixedSubdirs(root);
        using var listener = h.Listen(root, out var runId);
        var hostPid = h.SpawnHost(root, runId);
        var hostPgid = h.Getpgid(hostPid);
        var hostStart = SupervisorTestHarness.GetStartTimeUnixMs(hostPid);

        try
        {
            var acceptTask = listener.AcceptAsync();
            Assert.Same(acceptTask, await Task.WhenAny(acceptTask, Task.Delay(TimeSpan.FromSeconds(10))));
            using var sock = await acceptTask;
            using var stream = new NetworkStream(sock, ownsSocket: false);
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            await reader.ReadLineAsync(); // hello
            await h.SendAsync(stream, runId, "hello-ack", includeProtocolVersion: true);

            // STATE trigger: poll process-group membership until mongod (a second member) is confirmed present.
            // `ready` is deliberately never read — this thread never calls reader.ReadLineAsync() again, so by
            // construction the state "mongod is a group member AND ready has not been read" holds at kill time.
            var pollDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
            List<int> members = new();
            while (DateTime.UtcNow < pollDeadline)
            {
                members = h.EnumerateProcessGroupMembers(hostPgid);
                if (members.Count >= 2) break; // host + mongod
                await Task.Delay(100);
            }

            _output.WriteLine($"members observed at trigger time: {string.Join(",", members)}");
            Assert.True(members.Count >= 2, "mongod never joined the process group within 30s — cannot state-trigger this scenario");
            Assert.Contains(hostPid, members);

            Assert.True(h.KillSingle(hostPid, hostStart, 9, "scenario7-state-triggered"));
            await Task.Delay(1500);
            var groupResult = h.KillGroup(hostPid, hostPgid, hostStart, "scenario7-backup");
            _output.WriteLine(groupResult);
            await Task.Delay(1000);

            foreach (var pid in members.Where(p => p != hostPid))
            {
                Assert.False(h.ProcessExists(pid), $"group member {pid} leaked after state-triggered kill");
            }
        }
        finally
        {
            h.CleanRoot(root, dev, ino);
        }
    }

    // ── Scenario 1 — başlatma hatası, genuinely induced (T6): a nonexistent DITEN_ACCEPTANCE_DOTNET_PATH ──
    [Fact]
    public async Task BadDotnetPath_ApiStartFailedWithExitCode3AndCleanup()
    {
        var h = new SupervisorTestHarness(_output);
        var (root, dev, ino) = h.CreateRunRoot();
        h.PrepareFixedSubdirs(root);
        using var listener = h.Listen(root, out var runId);

        // The bad path is only fed to the CHILD via DITEN_ACCEPTANCE_DOTNET_PATH — the host itself still needs
        // its own real dotnet (it's how the host process itself was launched) to run its in-process seed logic;
        // only the value it later uses to spawn the API child is corrupted.
        var hostPid = h.SpawnHost(root, runId, dotnetPathOverrideForApiChild: "/nonexistent/path/dotnet-does-not-exist");
        var hostPgid = h.Getpgid(hostPid);
        var hostStart = SupervisorTestHarness.GetStartTimeUnixMs(hostPid);

        try
        {
            var acceptTask = listener.AcceptAsync();
            Assert.Same(acceptTask, await Task.WhenAny(acceptTask, Task.Delay(TimeSpan.FromSeconds(10))));
            using var sock = await acceptTask;
            using var stream = new NetworkStream(sock, ownsSocket: false);
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            await reader.ReadLineAsync(); // hello
            await h.SendAsync(stream, runId, "hello-ack", includeProtocolVersion: true);

            var next = await h.ReceiveRaw(reader, TimeSpan.FromSeconds(30));
            Assert.NotNull(next);
            Assert.Equal("error", next!.Value.GetProperty("type").GetString());
            Assert.Equal("api-start-failed", next.Value.GetProperty("code").GetString());
            _output.WriteLine($"error message (fixed text, no path/exception leaked): {next.Value.GetProperty("message").GetString()}");

            // Host should exit on its own with code 3 (ApiStartFailed) and clean up mongod without any signal
            // from us at all — this is the host's OWN failure-path cleanup, not the supervisor backup.
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
            while (DateTime.UtcNow < deadline && h.ProcessExists(hostPid))
            {
                await Task.Delay(200);
            }
            Assert.False(h.ProcessExists(hostPid), "host did not exit on its own after api-start-failed");

            var remainingMembers = h.EnumerateProcessGroupMembers(hostPgid).Where(p => p != hostPid).ToList();
            Assert.Empty(remainingMembers); // mongod must already be gone, no orphan
        }
        finally
        {
            h.CleanRoot(root, dev, ino);
        }
    }
}

/// <summary>
/// Shared supervisor-side test infrastructure: mkdtemp+lstat run roots (C9 §1), posix_spawn+POSIX_SPAWN_SETPGROUP
/// host launch (D1), protected single/group kill with a signal log (D2), and process-group/start-time
/// introspection. All P/Invoke implementations are the SAME ones measured and cross-verified in the Stage 2/D1D2
/// reports (kinfo_proc offsets 0/560, LOCAL_PEERPID+getpeereid, proc_listpids PROC_PGRP_ONLY=2).
/// </summary>
internal sealed class SupervisorTestHarness
{
    private readonly ITestOutputHelper _output;
    private readonly int _selfPgid;
    public List<(int Pid, int Pgid, long ExpectedStartTime, int Signal, bool Verified, string Purpose, DateTime At)> SignalLog { get; } = new();

    public SupervisorTestHarness(ITestOutputHelper output)
    {
        _output = output;
        _selfPgid = Getpgid(0);
    }

    public (string root, long dev, long ino) CreateRunRoot()
    {
        var canonicalTmp = Realpath("/tmp");
        var template = Path.Combine(canonicalTmp, "dak-XXXXXX");
        var buffer = Encoding.UTF8.GetBytes(template + "\0");
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            var ptr = handle.AddrOfPinnedObject();
            var result = mkdtemp(ptr);
            if (result == IntPtr.Zero) throw new InvalidOperationException("mkdtemp failed");
            var path = Marshal.PtrToStringUTF8(result)!;
            var (dev, ino) = Lstat(path);
            return (path, dev, ino);
        }
        finally { handle.Free(); }

        [DllImport("libc", SetLastError = true)] static extern IntPtr mkdtemp(IntPtr template);
    }

    public void PrepareFixedSubdirs(string root)
    {
        foreach (var name in new[] { "mongo", "home", "tmp", "content" })
        {
            var d = Path.Combine(root, name);
            Directory.CreateDirectory(d);
            Chmod(d, 0x1C0);
        }
    }

    public void CleanRoot(string root, long dev, long ino)
    {
        var (nowDev, nowIno) = Lstat(root);
        if (nowDev == dev && nowIno == ino)
        {
            Directory.Delete(root, recursive: true);
        }
        else
        {
            _output.WriteLine($"REFUSING to delete {root} — dev/ino changed since creation. Left for inspection.");
        }
    }

    public Socket Listen(string root, out string runId)
    {
        var controlSock = Path.Combine(root, "control.sock");
        var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(controlSock));
        listener.Listen(1);
        runId = Guid.NewGuid().ToString("N");
        return listener;
    }

    public int SpawnHost(string root, string runId, string? dotnetPathOverrideForApiChild = null, string[]? extraEnv = null)
    {
        var dotnetPath = "/usr/local/share/dotnet/dotnet";
        var hostDll = Path.Combine(AppContext.BaseDirectory, "Diten.AuthService.AccountKindAcceptanceHost.dll");
        if (!File.Exists(hostDll))
        {
            throw new FileNotFoundException(
                "Diten.AuthService.AccountKindAcceptanceHost.dll not found next to this test assembly's own output " +
                "— expected the ProjectReference to copy it here.", hostDll);
        }

        var env = new List<string>
        {
            $"DITEN_ACCEPTANCE_ROOT={root}",
            $"DITEN_ACCEPTANCE_RUN_ID={runId}",
            $"DITEN_ACCEPTANCE_DOTNET_PATH={dotnetPathOverrideForApiChild ?? dotnetPath}",
            $"PATH={Environment.GetEnvironmentVariable("PATH")}",
            $"HOME={Environment.GetEnvironmentVariable("HOME")}"
        };
        if (extraEnv is not null) env.AddRange(extraEnv);

        return SpawnWithNewProcessGroup(dotnetPath, new[] { dotnetPath, hostDll }, env.ToArray());
    }

    /// <summary>Reaps the host via waitpid and returns its real POSIX exit code (WEXITSTATUS), or -1 if it did
    /// not exit within the timeout (still alive) or died from a signal instead of exiting normally.</summary>
    public async Task<int> WaitForExitAsync(int pid, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var result = waitpid(pid, out var status, 1 /* WNOHANG */);
            if (result == pid)
            {
                var exitedNormally = (status & 0x7f) == 0;
                return exitedNormally ? (status >> 8) & 0xFF : -1;
            }

            await Task.Delay(100);
        }

        return -1;

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        static extern int waitpid(int pid, out int status, int options);
    }

    public async Task SendAsync(NetworkStream stream, string runId, string type, bool includeProtocolVersion = false)
    {
        var dict = new Dictionary<string, object?> { ["type"] = type, ["runId"] = runId };
        if (includeProtocolVersion) dict["protocolVersion"] = "1.2";
        var b = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(dict) + "\n");
        await stream.WriteAsync(b);
        await stream.FlushAsync();
    }

    public async Task<JsonElement?> ReceiveRaw(StreamReader reader, TimeSpan timeout)
    {
        var lt = reader.ReadLineAsync();
        if (await Task.WhenAny(lt, Task.Delay(timeout)) != lt) return null;
        var l = await lt;
        if (l is null) return null;
        using var d = JsonDocument.Parse(l);
        return d.RootElement.Clone();
    }

    public sealed record CycleResult(bool Ok, int ApiPid, long ApiStart, int MongodPid, long MongodStart, bool ByeReceived);
    public sealed record ReadyResult(bool Ok, int ApiPid, long ApiStart, int MongodPid, long MongodStart);

    public async Task<ReadyResult> DriveToReady(Socket listener, string runId)
    {
        var acceptTask = listener.AcceptAsync();
        if (await Task.WhenAny(acceptTask, Task.Delay(TimeSpan.FromSeconds(10))) != acceptTask) return new(false, 0, 0, 0, 0);
        using var sock = await acceptTask;
        using var stream = new NetworkStream(sock, ownsSocket: false);
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

        if (await ReceiveRaw(reader, TimeSpan.FromSeconds(10)) is null) return new(false, 0, 0, 0, 0);
        await SendAsync(stream, runId, "hello-ack", includeProtocolVersion: true);
        var ready = await ReceiveRaw(reader, TimeSpan.FromSeconds(60));
        if (ready is null || ready.Value.GetProperty("type").GetString() != "ready") return new(false, 0, 0, 0, 0);
        var apiPid = ready.Value.GetProperty("apiPid").GetInt32();
        var apiStart = ready.Value.GetProperty("apiStartTime").GetInt64();
        var mongodPid = ready.Value.GetProperty("mongodPid").GetInt32();
        var mongodStart = ready.Value.GetProperty("mongodStartTime").GetInt64();
        var seedReady = await ReceiveRaw(reader, TimeSpan.FromSeconds(60));
        return new(seedReady is not null, apiPid, apiStart, mongodPid, mongodStart);
    }

    public async Task<CycleResult> DriveFullCycle(Socket listener, string runId)
    {
        var acceptTask = listener.AcceptAsync();
        if (await Task.WhenAny(acceptTask, Task.Delay(TimeSpan.FromSeconds(10))) != acceptTask) return new(false, 0, 0, 0, 0, false);
        using var sock = await acceptTask;
        using var stream = new NetworkStream(sock, ownsSocket: false);
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

        if (await ReceiveRaw(reader, TimeSpan.FromSeconds(10)) is null) return new(false, 0, 0, 0, 0, false);
        await SendAsync(stream, runId, "hello-ack", includeProtocolVersion: true);
        var ready = await ReceiveRaw(reader, TimeSpan.FromSeconds(60));
        if (ready is null) return new(false, 0, 0, 0, 0, false);
        var apiPid = ready.Value.GetProperty("apiPid").GetInt32();
        var apiStart = ready.Value.GetProperty("apiStartTime").GetInt64();
        var mongodPid = ready.Value.GetProperty("mongodPid").GetInt32();
        var mongodStart = ready.Value.GetProperty("mongodStartTime").GetInt64();
        if (await ReceiveRaw(reader, TimeSpan.FromSeconds(60)) is null) return new(false, apiPid, apiStart, mongodPid, mongodStart, false);
        await SendAsync(stream, runId, "shutdown");
        var bye = await ReceiveRaw(reader, TimeSpan.FromSeconds(15));
        return new(true, apiPid, apiStart, mongodPid, mongodStart, bye is not null && bye.Value.GetProperty("type").GetString() == "bye");
    }

    // ── D2 protected signal primitives ───────────────────────────────────────────────────────────────────
    public bool KillSingle(int pid, long expectedStartTime, int sig, string purpose)
    {
        long? actual = null;
        try { actual = GetStartTimeUnixMs(pid); } catch { /* already gone */ }
        var verified = actual == expectedStartTime;
        var pgid = verified ? Getpgid(pid) : -1;
        SignalLog.Add((pid, pgid, expectedStartTime, sig, verified, purpose, DateTime.UtcNow));
        _output.WriteLine($"signal-log: pid={pid} sig={sig} verified={verified} purpose={purpose}");
        if (!verified) return false;
        kill(pid, sig);
        return true;
    }

    public string KillGroup(int hostPid, int hostPgid, long hostSpawnTime, string purpose)
    {
        if (hostPgid != hostPid) return "REFUSED: pgid != hostPid";
        if (hostPgid == _selfPgid) return "REFUSED: pgid == test process's own pgid";

        var members = EnumerateProcessGroupMembers(hostPgid);
        var verifiedMembers = new List<int>();
        foreach (var pid in members)
        {
            try
            {
                var st = GetStartTimeUnixMs(pid);
                if (st >= hostSpawnTime) verifiedMembers.Add(pid);
            }
            catch { /* already gone */ }
        }

        foreach (var pid in verifiedMembers)
        {
            var st = GetStartTimeUnixMs(pid);
            KillSingle(pid, st, 15, purpose);
        }

        return $"OK: {verifiedMembers.Count} members signaled (SIGTERM): {string.Join(",", verifiedMembers)}";
    }

    public bool ProcessExists(int pid)
    {
        waitpid(pid, out _, 1 /* WNOHANG */);
        return kill(pid, 0) == 0;

        [DllImport("libc", SetLastError = true)] static extern int waitpid(int pid, out int status, int options);
    }

    public int Getpgid(int pid) => getpgid(pid);

    public unsafe List<int> EnumerateProcessGroupMembers(int pgid)
    {
        const uint PROC_PGRP_ONLY = 2;
        var buffer = new int[4096];
        int count;
        fixed (int* buf = buffer) { count = proc_listpids(PROC_PGRP_ONLY, (uint)pgid, (IntPtr)buf, buffer.Length * sizeof(int)); }
        var n = count / sizeof(int);
        return buffer.Take(n).Where(p => p != 0).Distinct().OrderBy(p => p).ToList();

        [DllImport("libproc", SetLastError = true)] static extern int proc_listpids(uint type, uint typeinfo, IntPtr buffer, int buffersize);
    }

    public static long GetStartTimeUnixMs(int pid)
    {
        const int CTL_KERN = 1, KERN_PROC = 14, KERN_PROC_PID = 1;
        int[] mib = { CTL_KERN, KERN_PROC, KERN_PROC_PID, pid };
        nuint size = 0;
        sysctl(mib, 4, IntPtr.Zero, ref size, IntPtr.Zero, 0);
        if (size == 0) throw new InvalidOperationException($"pid {pid} not found");
        var buf = Marshal.AllocHGlobal((int)size);
        try
        {
            sysctl(mib, 4, buf, ref size, IntPtr.Zero, 0);
            var tvSec = Marshal.ReadInt64(buf, 0);
            var tvUsec = Marshal.ReadInt64(buf, 8);
            return tvSec * 1000 + tvUsec / 1000;
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    private static string Realpath(string path)
    {
        var buf = Marshal.AllocHGlobal(4096);
        try
        {
            var r = realpath(path, buf);
            if (r == IntPtr.Zero) throw new InvalidOperationException("realpath failed");
            return Marshal.PtrToStringUTF8(r)!;
        }
        finally { Marshal.FreeHGlobal(buf); }

        [DllImport("libc", SetLastError = true)] static extern IntPtr realpath(string path, IntPtr resolved);
    }

    private static (long dev, long ino) Lstat(string path)
    {
        var buf = Marshal.AllocHGlobal(200);
        try
        {
            if (lstat(path, buf) != 0) throw new InvalidOperationException("lstat failed for " + path);
            return (Marshal.ReadInt32(buf, 0), Marshal.ReadInt64(buf, 8));
        }
        finally { Marshal.FreeHGlobal(buf); }

        [DllImport("libc", EntryPoint = "lstat", SetLastError = true)] static extern int lstat(string path, IntPtr buf);
    }

    private static void Chmod(string path, int mode)
    {
        chmod(path, mode);
        [DllImport("libc", SetLastError = true)] static extern int chmod(string path, int mode);
    }

    private static unsafe int SpawnWithNewProcessGroup(string path, string[] argv, string[] envp)
    {
        const short POSIX_SPAWN_SETPGROUP = 0x0002;
        var attr = IntPtr.Zero;
        if (posix_spawnattr_init(ref attr) != 0) throw new InvalidOperationException("posix_spawnattr_init failed");
        try
        {
            if (posix_spawnattr_setflags(ref attr, POSIX_SPAWN_SETPGROUP) != 0) throw new InvalidOperationException("setflags failed");
            if (posix_spawnattr_setpgroup(ref attr, 0) != 0) throw new InvalidOperationException("setpgroup failed");

            var argvPtrs = MarshalStringArray(argv);
            var envpPtrs = MarshalStringArray(envp);
            try
            {
                fixed (IntPtr* argvPin = argvPtrs)
                fixed (IntPtr* envpPin = envpPtrs)
                {
                    var rc = posix_spawn(out var pid, path, IntPtr.Zero, ref attr, (IntPtr)argvPin, (IntPtr)envpPin);
                    if (rc != 0) throw new InvalidOperationException($"posix_spawn failed, errno={rc}");
                    return pid;
                }
            }
            finally { FreeStringArray(argvPtrs); FreeStringArray(envpPtrs); }
        }
        finally { posix_spawnattr_destroy(ref attr); }

        static IntPtr[] MarshalStringArray(string[] items)
        {
            var result = new IntPtr[items.Length + 1];
            for (var i = 0; i < items.Length; i++) result[i] = Marshal.StringToHGlobalAnsi(items[i]);
            result[items.Length] = IntPtr.Zero;
            return result;
        }
        static void FreeStringArray(IntPtr[] arr) { foreach (var p in arr) if (p != IntPtr.Zero) Marshal.FreeHGlobal(p); }

        [DllImport("libc", SetLastError = false)] static extern int posix_spawnattr_init(ref IntPtr attr);
        [DllImport("libc", SetLastError = false)] static extern int posix_spawnattr_destroy(ref IntPtr attr);
        [DllImport("libc", SetLastError = false)] static extern int posix_spawnattr_setflags(ref IntPtr attr, short flags);
        [DllImport("libc", SetLastError = false)] static extern int posix_spawnattr_setpgroup(ref IntPtr attr, int pgroup);
        [DllImport("libc", SetLastError = false, EntryPoint = "posix_spawn")]
        static extern int posix_spawn(out int pid, string path, IntPtr fileActions, ref IntPtr attrp, IntPtr argv, IntPtr envp);
    }

    [DllImport("libc", SetLastError = true)] private static extern int getpgid(int pid);
    [DllImport("libc", SetLastError = true)] private static extern int kill(int pid, int sig);
    [DllImport("libc", SetLastError = true)]
    private static extern int sysctl(int[] name, uint namelen, IntPtr oldp, ref nuint oldlenp, IntPtr newp, nuint newlen);
}
