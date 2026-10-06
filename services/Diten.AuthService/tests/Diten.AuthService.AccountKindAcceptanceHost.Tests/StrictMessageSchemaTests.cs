// WP-INFRA-AUTH-ACCEPTANCE-HOST-01 — F13 (Aşama F): closes a genuine regression CT found — C7's finalized rule
// ("unknown field, missing required field, wrong JSON type, null -> protocol-violation") was never actually
// enforced for the three message types the host receives FROM the supervisor: hello-ack, shutdown, error.
// ControlChannel.ParseAndValidate only ever checked `type`/`runId`/`protocolVersion`'s OWN placement — any other
// field, of any shape, was silently accepted. Most cases here reuse the same fast, in-process, real-socket-pair
// technique as ControlChannelC7NegativeTests (ControlChannel.ForTestingFromConnectedSocket); one case drives a
// REAL out-of-process host end to end and asserts the real exit code, per the WP's own requirement that at least
// one case go through the wire against the real process.

using System.Net.Sockets;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Diten.AuthService.AccountKindAcceptanceHost.Tests;

[Collection("SevenStateSupervisor")]
public sealed class StrictMessageSchemaTests
{
    private const string RunId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private readonly ITestOutputHelper _output;

    public StrictMessageSchemaTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static async Task<(ControlChannel Host, Socket Raw, string Dir)> MakePairAsync(string runId = RunId)
    {
        var dir = Directory.CreateTempSubdirectory("dak-schema-").FullName;
        var path = Path.Combine(dir, "s.sock");
        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(path));
        listener.Listen(1);

        var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        var connectTask = client.ConnectAsync(new UnixDomainSocketEndPoint(path));
        var acceptedSocket = await listener.AcceptAsync();
        await connectTask;

        var host = ControlChannel.ForTestingFromConnectedSocket(acceptedSocket, runId);
        return (host, client, dir);
    }

    private static async Task SendRaw(Socket client, byte[] bytes)
    {
        using var stream = new NetworkStream(client, ownsSocket: false);
        await stream.WriteAsync(bytes);
        await stream.FlushAsync();
    }

    private static void Cleanup(string dir)
    {
        try { Directory.Delete(dir, recursive: true); } catch { /* best effort, own temp dir */ }
    }

    // ── hello-ack — unknown top-level field ─────────────────────────────────────────────────────────────────
    [Fact]
    public async Task HelloAck_UnknownField_IsRejected()
    {
        var (host, client, dir) = await MakePairAsync();
        try
        {
            var json = Encoding.UTF8.GetBytes(
                $"{{\"type\":\"hello-ack\",\"runId\":\"{RunId}\",\"protocolVersion\":\"1.2\",\"bogus\":\"x\"}}\n");
            await SendRaw(client, json);
            var ex = await Assert.ThrowsAsync<ProtocolFramingViolationException>(() => host.ReceiveAsync(TimeSpan.FromSeconds(3), "hello-ack"));
            Assert.Contains("unknown field", ex.Message);
        }
        finally { client.Dispose(); Cleanup(dir); }
    }

    // ── shutdown — unknown top-level field ──────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Shutdown_UnknownField_IsRejected()
    {
        var (host, client, dir) = await MakePairAsync();
        try
        {
            var json = Encoding.UTF8.GetBytes($"{{\"type\":\"shutdown\",\"runId\":\"{RunId}\",\"reason\":\"user\"}}\n");
            await SendRaw(client, json);
            var ex = await Assert.ThrowsAsync<ProtocolFramingViolationException>(() => host.ReceiveAsync(TimeSpan.FromSeconds(3), "shutdown"));
            Assert.Contains("unknown field", ex.Message);
        }
        finally { client.Dispose(); Cleanup(dir); }
    }

    // ── error — unknown top-level field ─────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Error_UnknownField_IsRejected()
    {
        var (host, client, dir) = await MakePairAsync();
        try
        {
            var json = Encoding.UTF8.GetBytes(
                $"{{\"type\":\"error\",\"runId\":\"{RunId}\",\"code\":\"x\",\"message\":\"y\",\"extra\":1}}\n");
            await SendRaw(client, json);
            var ex = await Assert.ThrowsAsync<ProtocolFramingViolationException>(() => host.ReceiveAsync(TimeSpan.FromSeconds(3)));
            Assert.Contains("unknown field", ex.Message);
        }
        finally { client.Dispose(); Cleanup(dir); }
    }

    // ── error — missing required field "code" ───────────────────────────────────────────────────────────────
    [Fact]
    public async Task Error_MissingCode_IsRejected()
    {
        var (host, client, dir) = await MakePairAsync();
        try
        {
            var json = Encoding.UTF8.GetBytes($"{{\"type\":\"error\",\"runId\":\"{RunId}\",\"message\":\"y\"}}\n");
            await SendRaw(client, json);
            var ex = await Assert.ThrowsAsync<ProtocolFramingViolationException>(() => host.ReceiveAsync(TimeSpan.FromSeconds(3)));
            Assert.Contains("missing required field", ex.Message);
        }
        finally { client.Dispose(); Cleanup(dir); }
    }

    // ── error — missing required field "message" ────────────────────────────────────────────────────────────
    [Fact]
    public async Task Error_MissingMessage_IsRejected()
    {
        var (host, client, dir) = await MakePairAsync();
        try
        {
            var json = Encoding.UTF8.GetBytes($"{{\"type\":\"error\",\"runId\":\"{RunId}\",\"code\":\"x\"}}\n");
            await SendRaw(client, json);
            var ex = await Assert.ThrowsAsync<ProtocolFramingViolationException>(() => host.ReceiveAsync(TimeSpan.FromSeconds(3)));
            Assert.Contains("missing required field", ex.Message);
        }
        finally { client.Dispose(); Cleanup(dir); }
    }

    // ── error — wrong JSON type for "code" (number, not string) ────────────────────────────────────────────
    [Fact]
    public async Task Error_CodeWrongType_IsRejected()
    {
        var (host, client, dir) = await MakePairAsync();
        try
        {
            var json = Encoding.UTF8.GetBytes($"{{\"type\":\"error\",\"runId\":\"{RunId}\",\"code\":42,\"message\":\"y\"}}\n");
            await SendRaw(client, json);
            var ex = await Assert.ThrowsAsync<ProtocolFramingViolationException>(() => host.ReceiveAsync(TimeSpan.FromSeconds(3)));
            Assert.Contains("'code'", ex.Message);
        }
        finally { client.Dispose(); Cleanup(dir); }
    }

    // ── error — null for "message" ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Error_MessageNull_IsRejected()
    {
        var (host, client, dir) = await MakePairAsync();
        try
        {
            var json = Encoding.UTF8.GetBytes($"{{\"type\":\"error\",\"runId\":\"{RunId}\",\"code\":\"x\",\"message\":null}}\n");
            await SendRaw(client, json);
            var ex = await Assert.ThrowsAsync<ProtocolFramingViolationException>(() => host.ReceiveAsync(TimeSpan.FromSeconds(3)));
            Assert.Contains("'message'", ex.Message);
        }
        finally { client.Dispose(); Cleanup(dir); }
    }

    // ── G4 (Aşama G) — framing exception texts are FIXED: no sender-controlled name/value is ever interpolated ──
    // Stage F built three messages from wire input (unknown field NAME, unexpected type VALUE, duplicated KEY).
    // Main only ever prints the exception TYPE, but the fixed-text promise is the exception's own; each canary below
    // is the sender-controlled part, and none may appear in Message or ToString(). Assert.False with a fixed text,
    // never Assert.DoesNotContain — a failure must not print the text that carries the canary.
    [Fact]
    public async Task UnknownFieldWhoseNameIsACanary_CanaryNeverInExceptionText()
    {
        var canary = "CANARYFIELD" + Guid.NewGuid().ToString("N");
        var (host, client, dir) = await MakePairAsync();
        try
        {
            var json = Encoding.UTF8.GetBytes(
                $"{{\"type\":\"hello-ack\",\"runId\":\"{RunId}\",\"protocolVersion\":\"1.2\",\"{canary}\":\"x\"}}\n");
            await SendRaw(client, json);
            var ex = await Assert.ThrowsAsync<ProtocolFramingViolationException>(() => host.ReceiveAsync(TimeSpan.FromSeconds(3), "hello-ack"));
            Assert.Contains("unknown field", ex.Message);
            Assert.False(ex.Message.Contains(canary, StringComparison.Ordinal), "unknown-field exception message carries the sender-controlled field name");
            Assert.False(ex.ToString().Contains(canary, StringComparison.Ordinal), "unknown-field exception text carries the sender-controlled field name");
        }
        finally { client.Dispose(); Cleanup(dir); }
    }

    [Fact]
    public async Task UnexpectedTypeWhoseValueIsACanary_CanaryNeverInExceptionText()
    {
        var canary = "CANARYTYPE" + Guid.NewGuid().ToString("N");
        var (host, client, dir) = await MakePairAsync();
        try
        {
            var json = Encoding.UTF8.GetBytes($"{{\"type\":\"{canary}\",\"runId\":\"{RunId}\"}}\n");
            await SendRaw(client, json);
            var ex = await Assert.ThrowsAsync<ProtocolFramingViolationException>(() => host.ReceiveAsync(TimeSpan.FromSeconds(3), "hello-ack"));
            Assert.Contains("unexpected message type", ex.Message);
            Assert.False(ex.Message.Contains(canary, StringComparison.Ordinal), "unexpected-type exception message carries the sender-controlled type value");
            Assert.False(ex.ToString().Contains(canary, StringComparison.Ordinal), "unexpected-type exception text carries the sender-controlled type value");
        }
        finally { client.Dispose(); Cleanup(dir); }
    }

    [Fact]
    public async Task DuplicatedKeyThatIsACanary_CanaryNeverInExceptionText()
    {
        var canary = "CANARYKEY" + Guid.NewGuid().ToString("N");
        var (host, client, dir) = await MakePairAsync();
        try
        {
            var json = Encoding.UTF8.GetBytes(
                $"{{\"type\":\"hello-ack\",\"runId\":\"{RunId}\",\"protocolVersion\":\"1.2\",\"{canary}\":1,\"{canary}\":2}}\n");
            await SendRaw(client, json);
            var ex = await Assert.ThrowsAsync<ProtocolFramingViolationException>(() => host.ReceiveAsync(TimeSpan.FromSeconds(3), "hello-ack"));
            Assert.Contains("duplicate", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(ex.Message.Contains(canary, StringComparison.Ordinal), "duplicate-key exception message carries the sender-controlled key");
            Assert.False(ex.ToString().Contains(canary, StringComparison.Ordinal), "duplicate-key exception text carries the sender-controlled key");
        }
        finally { client.Dispose(); Cleanup(dir); }
    }

    // ── real host, real wire, real exit code — an unknown field on `shutdown` must map to exit 1 ─────────────
    [Fact]
    public async Task RealHost_ShutdownWithUnknownField_ReportsProtocolViolationExit1()
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

            var malformedShutdown = Encoding.UTF8.GetBytes($"{{\"type\":\"shutdown\",\"runId\":\"{runId}\",\"force\":true}}\n");
            await stream.WriteAsync(malformedShutdown);
            await stream.FlushAsync();

            var exitCode = await h.WaitForExitAsync(hostPid, TimeSpan.FromSeconds(15));
            Assert.Equal(1, exitCode); // ExitCodes.ProtocolViolation
        }
        finally
        {
            h.CleanRoot(root, dev, ino);
        }
    }
}
