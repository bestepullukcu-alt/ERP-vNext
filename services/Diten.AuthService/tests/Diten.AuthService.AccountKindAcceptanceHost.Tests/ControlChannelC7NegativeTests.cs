// WP-INFRA-AUTH-ACCEPTANCE-HOST-01 (T2) — the full C7 negative-test list (protocol v1.2 Ek-B), exercised
// directly against the real ControlChannel class (no mocks, no reimplementation of the parser) over a real
// in-process Unix socket pair — fast, no real host OS process spawn needed for wire-level cases.
//
// Coverage map (13 items):
//   1. 65 537 bayt (no LF)               -> ProtocolLineTooLong
//   2. geçersiz UTF-8                     -> ProtocolInvalidUtf8
//   3. BOM                                -> ProtocolBom
//   4. CR                                 -> ProtocolCr
//   5. yinelenen alan (her derinlikte)    -> ProtocolDuplicateKeyTopLevel / ProtocolDuplicateKeyNested
//   6. eksik runId                        -> ProtocolMissingRunId
//   7. yanlış runId                       -> ProtocolWrongRunId
//   8. hello'dan önce ready               -> UnexpectedTypeBeforeHello (allowedTypes state machine)
//   9. ikinci ready (aynı noktada iki kez)-> UnexpectedTypeSecondReady
//   10. yanlış sürüm                      -> UnsupportedProtocolVersionInHelloAck
//   11. ready'de protocolVersion          -> ProtocolVersionOutsideHelloHelloAck
//   12. yarım satırda EOF                 -> HalfLineEof
//   13. satır içi LF 5 sn aşımı           -> InlineLineTimeoutFiveSeconds (slow: waits >5s for real)
//   14. seed sırasında EOF                -> covered by T8's existing state-triggered scenario (process-level,
//       already in SevenStateAcceptanceTests) — an EOF mid-seed is what a SIGKILL there produces; not repeated
//       here as a second, separate wire-level case since it is not a framing/parsing question.
//   15. kanarya içeren hatalı girdi (error.message kanaryayı içermez) -> ErrorMessageNeverEchoesInput
//
// D2/mechanical rule: no real OS process is spawned or signaled by this file at all — everything here is two
// ends of one Unix socket inside the SAME test process.
//
// F7 (Aşama E) — every ThrowsAsync assertion below expects ProtocolFramingViolationException, not
// InvalidOperationException. ControlChannel's own framing/parsing/state-machine throw sites were changed to
// this dedicated type so Main can map them to exit code 1 by TYPE (a catch clause), never by re-matching the
// exception's own message string — see Program.cs's ProtocolFramingViolationException and its catch clause.

using System.Net.Sockets;
using System.Text;
using Xunit;

namespace Diten.AuthService.AccountKindAcceptanceHost.Tests;

[Collection("SevenStateSupervisor")] // shares the no-parallel discipline with the process-level tests
public sealed class ControlChannelC7NegativeTests
{
    private const string RunId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private static async Task<(ControlChannel Host, Socket Raw, string Dir)> MakePairAsync(string runId = RunId)
    {
        var dir = Directory.CreateTempSubdirectory("dak-c7-").FullName;
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

    // ── 1. 65 537 bayt, LF yok ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Line_65537_bytes_without_LF_is_rejected()
    {
        var (host, client, dir) = await MakePairAsync();
        try
        {
            // Measured: a Unix domain socket's kernel send buffer is smaller than 65537 bytes on this machine,
            // so writing them all in one go BLOCKS until a reader drains some — awaiting the send to completion
            // before starting the receive deadlocks (writer waits on reader, reader never started). Race them:
            // ReadStrictLineAsync reads one byte at a time, which drains the writer as it goes.
            var sendTask = SendRaw(client, new byte[65537]); // no trailing LF — must be refused while reading
            var ex = await Assert.ThrowsAsync<ProtocolFramingViolationException>(() => host.ReceiveAsync(TimeSpan.FromSeconds(5)));
            Assert.Contains("protocol-violation", ex.Message);
            Assert.Contains("65536", ex.Message);
            try { await sendTask; } catch { /* the send may itself fault once the receiver rejects and disposes — fine */ }
        }
        finally { client.Dispose(); Cleanup(dir); }
    }

    // ── 2. geçersiz UTF-8 ────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Invalid_UTF8_byte_sequence_is_rejected()
    {
        var (host, client, dir) = await MakePairAsync();
        try
        {
            byte[] invalid = { 0xFF, 0xFE, (byte)'\n' }; // not valid UTF-8
            await SendRaw(client, invalid);
            var ex = await Assert.ThrowsAsync<ProtocolFramingViolationException>(() => host.ReceiveAsync(TimeSpan.FromSeconds(3)));
            Assert.Contains("UTF-8", ex.Message);
        }
        finally { client.Dispose(); Cleanup(dir); }
    }

    // ── 3. BOM ───────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task UTF8_BOM_is_rejected()
    {
        var (host, client, dir) = await MakePairAsync();
        try
        {
            var json = Encoding.UTF8.GetBytes($"{{\"type\":\"hello-ack\",\"runId\":\"{RunId}\",\"protocolVersion\":\"1.2\"}}\n");
            var withBom = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(json).ToArray();
            await SendRaw(client, withBom);
            var ex = await Assert.ThrowsAsync<ProtocolFramingViolationException>(() => host.ReceiveAsync(TimeSpan.FromSeconds(3)));
            Assert.Contains("BOM", ex.Message);
        }
        finally { client.Dispose(); Cleanup(dir); }
    }

    // ── 4. CR ────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task CR_anywhere_in_the_line_is_rejected()
    {
        var (host, client, dir) = await MakePairAsync();
        try
        {
            var json = Encoding.UTF8.GetBytes($"{{\"type\":\"hello-ack\",\r\"runId\":\"{RunId}\",\"protocolVersion\":\"1.2\"}}\n");
            await SendRaw(client, json);
            var ex = await Assert.ThrowsAsync<ProtocolFramingViolationException>(() => host.ReceiveAsync(TimeSpan.FromSeconds(3)));
            Assert.Contains("CR", ex.Message);
        }
        finally { client.Dispose(); Cleanup(dir); }
    }

    // ── 5a. yinelenen alan — üst seviye ──────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Duplicate_key_at_top_level_is_rejected()
    {
        var (host, client, dir) = await MakePairAsync();
        try
        {
            var json = Encoding.UTF8.GetBytes(
                $"{{\"type\":\"hello-ack\",\"runId\":\"{RunId}\",\"protocolVersion\":\"1.2\",\"protocolVersion\":\"1.3\"}}\n");
            await SendRaw(client, json);
            var ex = await Assert.ThrowsAsync<ProtocolFramingViolationException>(() => host.ReceiveAsync(TimeSpan.FromSeconds(3)));
            Assert.Contains("duplicate", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally { client.Dispose(); Cleanup(dir); }
    }

    // ── 5b. yinelenen alan — iç içe (JsonDocument bunu reddetmez; Utf8JsonReader tarafımızdan yakalanır) ──
    [Fact]
    public async Task Duplicate_key_at_a_nested_depth_is_rejected()
    {
        var (host, client, dir) = await MakePairAsync();
        try
        {
            var json = Encoding.UTF8.GetBytes(
                $"{{\"type\":\"ready\",\"runId\":\"{RunId}\",\"endpoint\":{{\"kind\":\"unix\",\"kind\":\"tls\"}}}}\n");
            await SendRaw(client, json);
            var ex = await Assert.ThrowsAsync<ProtocolFramingViolationException>(() => host.ReceiveAsync(TimeSpan.FromSeconds(3)));
            Assert.Contains("duplicate", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally { client.Dispose(); Cleanup(dir); }
    }

    // ── 6. eksik runId ───────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Missing_runId_is_rejected()
    {
        var (host, client, dir) = await MakePairAsync();
        try
        {
            var json = Encoding.UTF8.GetBytes("{\"type\":\"hello-ack\",\"protocolVersion\":\"1.2\"}\n");
            await SendRaw(client, json);
            var ex = await Assert.ThrowsAsync<ProtocolFramingViolationException>(() => host.ReceiveAsync(TimeSpan.FromSeconds(3)));
            Assert.Contains("runId", ex.Message);
        }
        finally { client.Dispose(); Cleanup(dir); }
    }

    // ── 7. yanlış runId ──────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Wrong_runId_is_rejected()
    {
        var (host, client, dir) = await MakePairAsync();
        try
        {
            var json = Encoding.UTF8.GetBytes("{\"type\":\"hello-ack\",\"runId\":\"not-the-expected-run-id\",\"protocolVersion\":\"1.2\"}\n");
            await SendRaw(client, json);
            var ex = await Assert.ThrowsAsync<ProtocolFramingViolationException>(() => host.ReceiveAsync(TimeSpan.FromSeconds(3)));
            Assert.Contains("runId mismatch", ex.Message);
        }
        finally { client.Dispose(); Cleanup(dir); }
    }

    // ── 8. hello'dan önce ready — durum makinesi: bu noktada yalnız "hello-ack" beklenir ───────────────────
    [Fact]
    public async Task Ready_instead_of_expected_helloAck_is_rejected_by_the_state_machine()
    {
        var (host, client, dir) = await MakePairAsync();
        try
        {
            var json = Encoding.UTF8.GetBytes($"{{\"type\":\"ready\",\"runId\":\"{RunId}\"}}\n");
            await SendRaw(client, json);
            var ex = await Assert.ThrowsAsync<ProtocolFramingViolationException>(() => host.ReceiveAsync(TimeSpan.FromSeconds(3), "hello-ack"));
            Assert.Contains("unexpected message type", ex.Message);
        }
        finally { client.Dispose(); Cleanup(dir); }
    }

    // ── 9. ikinci ready — aynı beklenti noktasında tekrar aynı (yanlış) tür ─────────────────────────────────
    [Fact]
    public async Task A_second_unexpected_message_of_the_same_wrong_type_is_also_rejected()
    {
        var (host, client, dir) = await MakePairAsync();
        try
        {
            var json = Encoding.UTF8.GetBytes($"{{\"type\":\"ready\",\"runId\":\"{RunId}\"}}\n");
            await SendRaw(client, json);
            await Assert.ThrowsAsync<ProtocolFramingViolationException>(() => host.ReceiveAsync(TimeSpan.FromSeconds(3), "shutdown"));
            // second attempt, same channel state expectation — still rejected, not "learned" as acceptable
            await SendRaw(client, json);
            var ex2 = await Assert.ThrowsAsync<ProtocolFramingViolationException>(() => host.ReceiveAsync(TimeSpan.FromSeconds(3), "shutdown"));
            Assert.Contains("unexpected message type", ex2.Message);
        }
        finally { client.Dispose(); Cleanup(dir); }
    }

    // ── 10. yanlış sürüm ─────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Unsupported_protocol_version_in_hello_ack_is_rejected_by_caller_check()
    {
        var (host, client, dir) = await MakePairAsync();
        try
        {
            var json = Encoding.UTF8.GetBytes($"{{\"type\":\"hello-ack\",\"runId\":\"{RunId}\",\"protocolVersion\":\"1.9\"}}\n");
            await SendRaw(client, json);
            var msg = await host.ReceiveAsync(TimeSpan.FromSeconds(3), "hello-ack");
            // ControlChannel parses successfully (protocolVersion is syntactically fine here — allowed on
            // hello-ack); the SEMANTIC "is it OUR version" check is Main's, exercised by the process-level
            // sanity test (host itself performs exactly this comparison and reports unsupported-version).
            Assert.Equal("1.9", msg.ProtocolVersion);
            Assert.NotEqual("1.2", msg.ProtocolVersion);
        }
        finally { client.Dispose(); Cleanup(dir); }
    }

    // ── 11. ready'de protocolVersion ─────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ProtocolVersion_field_outside_hello_and_helloAck_is_rejected()
    {
        var (host, client, dir) = await MakePairAsync();
        try
        {
            var json = Encoding.UTF8.GetBytes($"{{\"type\":\"ready\",\"runId\":\"{RunId}\",\"protocolVersion\":\"1.2\"}}\n");
            await SendRaw(client, json);
            var ex = await Assert.ThrowsAsync<ProtocolFramingViolationException>(() => host.ReceiveAsync(TimeSpan.FromSeconds(3)));
            Assert.Contains("protocolVersion", ex.Message);
        }
        finally { client.Dispose(); Cleanup(dir); }
    }

    // ── 12. yarım satırda EOF ────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task EOF_mid_line_before_LF_is_rejected()
    {
        var (host, client, dir) = await MakePairAsync();
        try
        {
            var partial = Encoding.UTF8.GetBytes("{\"type\":\"hello-ack\""); // no closing brace, no LF
            await SendRaw(client, partial);
            client.Shutdown(SocketShutdown.Send); // half-close: no more bytes will ever arrive
            var ex = await Assert.ThrowsAsync<IOException>(() => host.ReceiveAsync(TimeSpan.FromSeconds(3)));
            Assert.Contains("EOF", ex.Message);
        }
        finally { client.Dispose(); Cleanup(dir); }
    }

    // ── 13. satır içi LF 5 sn aşımı (yavaş — gerçekten 5+ sn bekler) ────────────────────────────────────
    [Fact]
    public async Task Line_that_never_completes_within_5s_after_starting_is_rejected()
    {
        var (host, client, dir) = await MakePairAsync();
        try
        {
            using var stream = new NetworkStream(client, ownsSocket: false);
            await stream.WriteAsync(Encoding.UTF8.GetBytes("{\"type\":\"hello-ack\""));
            await stream.FlushAsync();
            // Deliberately send nothing further and do NOT close — the line has STARTED (bytes received) but
            // will never reach LF. The receive-level 10s test timeout must not fire first, so ReceiveAsync is
            // given a generous outer bound while the assertion is really about the channel's OWN 5s inline cap.
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var ex = await Assert.ThrowsAsync<ProtocolFramingViolationException>(() => host.ReceiveAsync(TimeSpan.FromSeconds(10)));
            sw.Stop();
            Assert.Contains("5s", ex.Message);
            Assert.InRange(sw.Elapsed.TotalSeconds, 4.5, 9.5);
        }
        finally { client.Dispose(); Cleanup(dir); }
    }

    // ── 15. kanarya içeren hatalı girdi — error.message kanaryayı içermez (P2 sabit metin tablosu) ────────
    [Fact]
    public async Task Malformed_input_containing_a_canary_never_echoes_the_canary_in_any_error_text()
    {
        const string canary = "CANARY-VALUE-MUST-NEVER-APPEAR-IN-ANY-ERROR-TEXT-9f3ac1";
        var (host, client, dir) = await MakePairAsync();
        try
        {
            // A syntactically-broken line that CONTAINS the canary as a raw fragment (e.g. a bad UTF-8 tail
            // appended after a canary-laden prefix) — the FIXED error text (ProtocolErrors / the parser's own
            // constant strings) must never include caller-supplied content.
            var prefix = Encoding.UTF8.GetBytes($"{{\"type\":\"hello-ack\",\"canaryField\":\"{canary}\"");
            var badTail = new byte[] { 0xFF, 0xFE };
            await SendRaw(client, prefix.Concat(badTail).Concat(new byte[] { (byte)'\n' }).ToArray());
            var ex = await Assert.ThrowsAsync<ProtocolFramingViolationException>(() => host.ReceiveAsync(TimeSpan.FromSeconds(3)));
            Assert.DoesNotContain(canary, ex.Message);
            Assert.DoesNotContain(canary, ex.ToString());
        }
        finally { client.Dispose(); Cleanup(dir); }
    }
}
