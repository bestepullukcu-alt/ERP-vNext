// WP-INFRA-AUTH-ACCEPTANCE-HOST-01 — T5 (Aşama D): file-descriptor evidence and EOF timing. Two real,
// out-of-process proofs:
//   (a) neither the real API process nor the real mongod process holds `control.sock` open as a descriptor
//       (black-box, via `lsof -p`) — Program.cs's own header comment (P4) explicitly flags
//       POSIX_SPAWN_CLOEXEC_DEFAULT as NOT set for these children and defers evidence to "the real 7-state run";
//       this is that measurement.
//   (b) after a host SIGKILL (with API and mongod both still alive — no signal sent to them), the supervisor
//       (this test, holding the still-open control-channel socket) sees EOF within <=1s, with the actual
//       measured time reported regardless of pass/fail.

using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Diten.AuthService.AccountKindAcceptanceHost.Tests;

[Collection("SevenStateSupervisor")]
public sealed class DescriptorAndEofTimingTests
{
    private readonly ITestOutputHelper _output;

    public DescriptorAndEofTimingTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ApiAndMongod_NeverHoldControlSocketOpen_AndSupervisorSeesEofWithin1sOfHostSigkill()
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
            // Manual driving, connection kept OPEN (not `using` a helper that disposes it) — the whole point of
            // (b) is to measure THIS test's own read on THIS still-open socket after the host dies.
            var acceptTask = listener.AcceptAsync();
            Assert.Same(acceptTask, await Task.WhenAny(acceptTask, Task.Delay(TimeSpan.FromSeconds(10))));
            using var sock = await acceptTask;
            using var stream = new NetworkStream(sock, ownsSocket: false);
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            await reader.ReadLineAsync(); // hello
            await h.SendAsync(stream, runId, "hello-ack", includeProtocolVersion: true);

            var ready = await h.ReceiveRaw(reader, TimeSpan.FromSeconds(60));
            Assert.NotNull(ready);
            Assert.Equal("ready", ready!.Value.GetProperty("type").GetString());
            var apiPid = ready.Value.GetProperty("apiPid").GetInt32();
            var mongodPid = ready.Value.GetProperty("mongodPid").GetInt32();

            var seedReady = await h.ReceiveRaw(reader, TimeSpan.FromSeconds(60));
            Assert.NotNull(seedReady);

            Assert.True(h.ProcessExists(apiPid), "api not alive to inspect");
            Assert.True(h.ProcessExists(mongodPid), "mongod not alive to inspect");

            // ── (a) descriptor evidence ──────────────────────────────────────────────────────────────────
            var apiLsof = PoisonedEnvironmentTests.RunLsof(apiPid);
            Assert.True(apiLsof.ToolAvailable, "lsof required for T5 evidence, not available — RED, not skipped");
            var mongodLsof = PoisonedEnvironmentTests.RunLsof(mongodPid);
            Assert.True(mongodLsof.ToolAvailable, "lsof required for T5 evidence, not available — RED, not skipped");

            _output.WriteLine($"api ({apiPid}) descriptor count: {apiLsof.Lines.Length}");
            _output.WriteLine($"mongod ({mongodPid}) descriptor count: {mongodLsof.Lines.Length}");

            var apiControlSockHits = apiLsof.Lines.Where(l => l.Contains("control.sock", StringComparison.Ordinal)).ToList();
            var mongodControlSockHits = mongodLsof.Lines.Where(l => l.Contains("control.sock", StringComparison.Ordinal)).ToList();
            Assert.True(apiControlSockHits.Count == 0, "api process holds control.sock open: " + string.Join(" | ", apiControlSockHits));
            Assert.True(mongodControlSockHits.Count == 0, "mongod process holds control.sock open: " + string.Join(" | ", mongodControlSockHits));

            // ── (b) EOF timing after a host SIGKILL, API/mongod left alive (no signal to them) ─────────────
            var sw = Stopwatch.StartNew();
            Assert.True(h.KillSingle(hostPid, hostStart, 9, "T5-eof-timing"), "SIGKILL verification refused");

            string? eofLine = null;
            Exception? eofException = null;
            try
            {
                eofLine = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                eofException = ex;
            }
            sw.Stop();

            _output.WriteLine($"measured EOF-observation time after host SIGKILL: {sw.ElapsedMilliseconds}ms " +
                $"(result: {(eofLine is null ? "EOF (null)" : "unexpected data: " + eofLine)}" +
                $"{(eofException is null ? "" : ", exception: " + eofException.GetType().Name)})");

            var sawEof = eofLine is null; // ReadLineAsync returns null on graceful/abrupt close alike here
            Assert.True(sawEof, "did not observe EOF on the control channel after host SIGKILL within 5s");
            Assert.True(sw.ElapsedMilliseconds <= 1000,
                $"EOF was observed but took {sw.ElapsedMilliseconds}ms, not <=1s as required — mechanism needs fixing (close inherited descriptors on API/mongod launch), not the test");

            // API and mongod must still be alive — nobody signaled them (this scenario's whole point).
            Assert.True(h.ProcessExists(apiPid), "api unexpectedly died from the host's own SIGKILL (should be independent processes)");
            Assert.True(h.ProcessExists(mongodPid), "mongod unexpectedly died from the host's own SIGKILL");

            // Backup cleanup — real cleanup responsibility now falls to the supervisor since the host is dead.
            var groupResult = h.KillGroup(hostPid, hostPgid, hostStart, "T5-backup-cleanup");
            _output.WriteLine(groupResult);
            await Task.Delay(1000);
            Assert.False(h.ProcessExists(apiPid), $"api leaked after backup cleanup ({groupResult})");
            Assert.False(h.ProcessExists(mongodPid), $"mongod leaked after backup cleanup ({groupResult})");
        }
        finally
        {
            h.CleanRoot(root, dev, ino);
        }
    }
}
