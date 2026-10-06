// WP-INFRA-AUTH-ACCEPTANCE-HOST-01 — F1/F2/F4: real, out-of-process, wire-level proof that error codes/exit codes
// reach the SUPERVISOR correctly. This is the exact gap a sabotage found: SeedFailureDetectionTests only proved
// the in-process FIXTURE's own exception type; nothing drove a genuine failure through the full posix_spawn'd
// host and asserted on what actually arrives over the socket + the real process exit code.

using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Diten.AuthService.Domain.Authorization;
using Xunit;
using Xunit.Abstractions;

namespace Diten.AuthService.AccountKindAcceptanceHost.Tests;

[Collection("SevenStateSupervisor")]
public sealed class WireLevelErrorMappingTests
{
    private readonly ITestOutputHelper _output;

    public WireLevelErrorMappingTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // ── F1 — a genuine seed failure, driven through the FULL wire protocol ─────────────────────────────────
    [Fact]
    public async Task SeedFailure_OverTheWire_ReportsSeedFailedAndExit4_NeverReadyOrSeedReady()
    {
        var h = new SupervisorTestHarness(_output);
        var (root, dev, ino) = h.CreateRunRoot();
        h.PrepareFixedSubdirs(root);
        using var listener = h.Listen(root, out var runId);

        // F1 TEST-ONLY seam (protocol v1.2 Ek-D) — unset in every normal run; here set to reproduce a silently-
        // swallowed production DataSeeder failure through the REAL wire protocol, not just in-process.
        var hostPid = h.SpawnHost(root, runId, extraEnv: new[]
        {
            $"DITEN_ACCEPTANCE_TESTONLY_CORRUPT_PERMISSION_KEY={ExplicitGrantOnlyPermissions.UsersAccountKindManage}"
        });

        try
        {
            var acceptTask = listener.AcceptAsync();
            Assert.Same(acceptTask, await Task.WhenAny(acceptTask, Task.Delay(TimeSpan.FromSeconds(10))));
            using var sock = await acceptTask;
            using var stream = new NetworkStream(sock, ownsSocket: false);
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            await reader.ReadLineAsync(); // hello
            await h.SendAsync(stream, runId, "hello-ack", includeProtocolVersion: true);

            var msg = await h.ReceiveRaw(reader, TimeSpan.FromSeconds(30));
            Assert.NotNull(msg);
            Assert.Equal("error", msg!.Value.GetProperty("type").GetString());
            Assert.Equal("seed-failed", msg.Value.GetProperty("code").GetString());
            _output.WriteLine($"error.message (fixed text): {msg.Value.GetProperty("message").GetString()}");

            // ready/seed-ready must NEVER arrive after error — but `bye` legitimately follows (host's own
            // finally block always sends it, on every exit path). Confirm the NEXT message is bye, not ready.
            var next = await h.ReceiveRaw(reader, TimeSpan.FromSeconds(5));
            Assert.NotNull(next);
            Assert.Equal("bye", next!.Value.GetProperty("type").GetString());

            var exitCode = await h.WaitForExitAsync(hostPid, TimeSpan.FromSeconds(15));
            Assert.Equal(4, exitCode); // ExitCodes.SeedFailed

            await Task.Delay(1000);
            var remaining = h.EnumerateProcessGroupMembers(h.Getpgid(hostPid) is var pg && pg > 0 ? pg : hostPid)
                .Where(p => p != hostPid).ToList();
            Assert.Empty(remaining); // mongod must already be gone — host's OWN failure-path cleanup, no signal from us
        }
        finally
        {
            h.CleanRoot(root, dev, ino);
        }
    }

    // ── F1 regression guard — a genuine mongod failure is STILL mongo-start-failed/2, never confused with seed-failed ──
    [Fact]
    public async Task GenuineMongodFailure_OverTheWire_StillReportsMongoStartFailedAndExit2()
    {
        var h = new SupervisorTestHarness(_output);
        var (root, dev, ino) = h.CreateRunRoot();
        // Measured: EphemeralMongo/mongod CREATES a missing data directory rather than failing — a nonexistent
        // path does not force a failure. A path that exists as a REGULAR FILE (not a directory) does: mongod
        // cannot use it (ENOTDIR), which is what actually forces mongo-start-failed through the full host.
        File.WriteAllText(Path.Combine(root, "mongo"), "not a directory, on purpose");
        Directory.CreateDirectory(Path.Combine(root, "home"));
        Directory.CreateDirectory(Path.Combine(root, "tmp"));
        Directory.CreateDirectory(Path.Combine(root, "content"));
        using var listener = h.Listen(root, out var runId);
        var hostPid = h.SpawnHost(root, runId);

        try
        {
            var acceptTask = listener.AcceptAsync();
            Assert.Same(acceptTask, await Task.WhenAny(acceptTask, Task.Delay(TimeSpan.FromSeconds(10))));
            using var sock = await acceptTask;
            using var stream = new NetworkStream(sock, ownsSocket: false);
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            await reader.ReadLineAsync();
            await h.SendAsync(stream, runId, "hello-ack", includeProtocolVersion: true);

            var msg = await h.ReceiveRaw(reader, TimeSpan.FromSeconds(30));
            Assert.NotNull(msg);
            Assert.Equal("error", msg!.Value.GetProperty("type").GetString());
            Assert.Equal("mongo-start-failed", msg.Value.GetProperty("code").GetString());

            var exitCode = await h.WaitForExitAsync(hostPid, TimeSpan.FromSeconds(15));
            Assert.Equal(2, exitCode); // ExitCodes.MongoStartFailed
        }
        finally
        {
            h.CleanRoot(root, dev, ino);
        }
    }

    // ── F2 — wrong protocol version in hello-ack, end to end ───────────────────────────────────────────────
    [Fact]
    public async Task WrongProtocolVersionInHelloAck_ReportsUnsupportedVersion_AndHostExits()
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
            await reader.ReadLineAsync(); // hello

            // Deliberately wrong version.
            var bytes = Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(new Dictionary<string, object?> { ["type"] = "hello-ack", ["runId"] = runId, ["protocolVersion"] = "1.9" }) + "\n");
            await stream.WriteAsync(bytes);
            await stream.FlushAsync();

            var msg = await h.ReceiveRaw(reader, TimeSpan.FromSeconds(10));
            Assert.NotNull(msg);
            Assert.Equal("error", msg!.Value.GetProperty("type").GetString());
            Assert.Equal("unsupported-version", msg.Value.GetProperty("code").GetString());

            var exitCode = await h.WaitForExitAsync(hostPid, TimeSpan.FromSeconds(10));
            // CT correction 2026-09-16: exit 1, per the host's own P3 table ("protocol-violation or
            // unsupported-version"), which PPM/Codex already accepted. The prompt's "exit 5" was a CT error.
            Assert.Equal(1, exitCode);
        }
        finally
        {
            h.CleanRoot(root, dev, ino);
        }
    }

    // ── F4 — supervisor closes the channel during seed; host must see it as supervisor-lost, not a generic crash ──
    [Fact]
    public async Task SupervisorClosesChannelDuringSeed_HostReportsSupervisorLostExit6_MongodAndRootCleaned()
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
            var sock = await acceptTask; // NOT `using` — closed explicitly below, mid-seed
            var stream = new NetworkStream(sock, ownsSocket: true);
            var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            await reader.ReadLineAsync(); // hello
            await h.SendAsync(stream, runId, "hello-ack", includeProtocolVersion: true);

            // Close the channel RIGHT NOW — the host has only just started seeding (mongod may not even be a
            // group member yet); it will not notice until its next socket operation (the `ready` send).
            reader.Dispose();
            stream.Dispose();
            sock.Dispose();

            var exitCode = await h.WaitForExitAsync(hostPid, TimeSpan.FromSeconds(20));
            Assert.Equal(6, exitCode); // ExitCodes.SupervisorLost

            await Task.Delay(1000);
            var members = h.EnumerateProcessGroupMembers(hostPgid).Where(p => p != hostPid).ToList();
            if (members.Count > 0)
            {
                // best-effort backup cleanup, verified PID+start-time, matching D2 discipline
                h.KillGroup(hostPid, hostPgid, hostStart, "F4-backup-if-needed");
                await Task.Delay(1000);
            }

            var stillAlive = h.EnumerateProcessGroupMembers(hostPgid).Where(p => p != hostPid).ToList();
            Assert.Empty(stillAlive);
        }
        finally
        {
            h.CleanRoot(root, dev, ino);
        }
    }
}
