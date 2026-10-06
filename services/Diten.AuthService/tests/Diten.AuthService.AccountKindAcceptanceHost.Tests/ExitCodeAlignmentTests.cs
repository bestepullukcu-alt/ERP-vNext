// WP-INFRA-AUTH-ACCEPTANCE-HOST-01 — F7 (Aşama E): a real, out-of-process, wire-level proof that a genuine
// framing/parsing violation reaches exit code 1 (protocol-violation, per the P3 table), even when it occurs
// somewhere OTHER than the hello-ack wait — specifically during the `shutdown` wait, whose own try/catch (line
// ~397-408 in Program.cs) only ever catches IOException/TimeoutException, so before this fix ANY
// ProtocolFramingViolationException thrown there fell through to the generic top-level catch and reported exit 5
// instead. This is the exact CT-found inconsistency between the ":412-413" branch (a syntactically well-formed
// but wrong-type `shutdown` reply, correctly exit 1) and the ":418-422" generic catch (previously exit 5 for a
// framing violation at that same wait point).

using System.Net.Sockets;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Diten.AuthService.AccountKindAcceptanceHost.Tests;

[Collection("SevenStateSupervisor")]
public sealed class ExitCodeAlignmentTests
{
    private readonly ITestOutputHelper _output;

    public ExitCodeAlignmentTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task FramingViolationDuringShutdownWait_ReportsProtocolViolationExit1_NotExit5()
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
            await h.SendAsync(stream, runId, "hello-ack", includeProtocolVersion: true);

            var ready = await h.ReceiveRaw(reader, TimeSpan.FromSeconds(60));
            Assert.NotNull(ready);
            Assert.Equal("ready", ready!.Value.GetProperty("type").GetString());
            var seedReady = await h.ReceiveRaw(reader, TimeSpan.FromSeconds(60));
            Assert.NotNull(seedReady);

            // Now the host is in its "wait for shutdown" ReceiveAsync — send a genuine C7 framing violation (a
            // raw CR inside the line) instead of a well-formed shutdown. This is NOT a wrong-type message (that
            // path already correctly returns 1) and NOT an IOException/timeout (already correctly handled) — it
            // is a framing violation that, before this fix, fell through this wait's narrow catch straight to
            // the generic top-level catch (exit 5).
            var malformed = Encoding.UTF8.GetBytes($"{{\"type\":\"shutdown\",\r\"runId\":\"{runId}\"}}\n");
            await stream.WriteAsync(malformed);
            await stream.FlushAsync();

            var exitCode = await h.WaitForExitAsync(hostPid, TimeSpan.FromSeconds(15));
            Assert.Equal(1, exitCode); // ExitCodes.ProtocolViolation — the F7 fix
        }
        finally
        {
            h.CleanRoot(root, dev, ino);
        }
    }

    // CT (2026-09-16) — the F7 fix classified every framing violation EXCEPT a syntactically broken line:
    // JsonDocument.Parse threw a raw JsonException, which is not the framing type, so it fell to the generic
    // catch and reported exit 5 while CR, BOM, bad UTF-8, duplicate keys and the rest reported 1. Same class of
    // failure, same exit code — this pins it.
    [Fact]
    public async Task MalformedJsonDuringShutdownWait_ReportsProtocolViolationExit1_NotExit5()
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
            await h.SendAsync(stream, runId, "hello-ack", includeProtocolVersion: true);

            var ready = await h.ReceiveRaw(reader, TimeSpan.FromSeconds(60));
            Assert.NotNull(ready);
            Assert.Equal("ready", ready!.Value.GetProperty("type").GetString());
            var seedReady = await h.ReceiveRaw(reader, TimeSpan.FromSeconds(60));
            Assert.NotNull(seedReady);

            // A well-framed line (single LF, valid UTF-8, no BOM, no CR) whose JSON is simply broken: the object
            // is never closed. Nothing before the parse can catch this.
            var malformed = Encoding.UTF8.GetBytes($"{{\"type\":\"shutdown\",\"runId\":\"{runId}\"\n");
            await stream.WriteAsync(malformed);
            await stream.FlushAsync();

            var exitCode = await h.WaitForExitAsync(hostPid, TimeSpan.FromSeconds(15));
            Assert.Equal(1, exitCode); // ExitCodes.ProtocolViolation, not Unexpected
        }
        finally
        {
            h.CleanRoot(root, dev, ino);
        }
    }
}
