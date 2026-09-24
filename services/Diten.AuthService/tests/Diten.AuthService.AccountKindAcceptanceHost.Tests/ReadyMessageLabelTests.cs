// WP-INFRA-AUTH-ACCEPTANCE-HOST-01 (T12) — the `ready` message's machine-readable trust-boundary labels
// (loginSettings.source / provesPlatformIntegration, authLoginProof) are asserted directly against a REAL run
// (host + real mongod + real out-of-process API), not just documented in a comment.

using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Diten.AuthService.AccountKindAcceptanceHost.Tests;

[Collection("SevenStateSupervisor")]
public sealed class ReadyMessageLabelTests
{
    [Fact]
    public async Task Ready_carries_the_stub_loginSettings_label_and_the_real_authLoginProof_label()
    {
        var h = new SupervisorTestHarness(new NullOutput());
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

            // The schema MUST be present — this is the negative-test half: absence is a red, wrong value is a red.
            Assert.True(ready.Value.TryGetProperty("loginSettings", out var loginSettings), "ready is missing the loginSettings label entirely");
            Assert.True(loginSettings.TryGetProperty("source", out var source), "loginSettings.source is missing");
            Assert.Equal("stub", source.GetString());
            Assert.True(loginSettings.TryGetProperty("provesPlatformIntegration", out var provesIntegration));
            Assert.False(provesIntegration.GetBoolean(), "the stub must never claim to prove Platform integration");
            Assert.True(loginSettings.TryGetProperty("boundaryEndpoint", out _), "loginSettings.boundaryEndpoint is missing");

            Assert.True(ready.Value.TryGetProperty("authLoginProof", out var authLoginProof), "ready is missing the authLoginProof label");
            Assert.Equal("real", authLoginProof.GetString());

            // drain the rest of the cycle so the host exits cleanly
            await h.ReceiveRaw(reader, TimeSpan.FromSeconds(60)); // seed-ready
            await h.SendAsync(stream, runId, "shutdown");
            await h.ReceiveRaw(reader, TimeSpan.FromSeconds(15)); // bye
        }
        finally
        {
            await Task.Delay(1000);
            h.CleanRoot(root, dev, ino);
        }
    }

    // ── negative — a schema check simulated against a hand-crafted (wrong) ready payload ────────────────────
    [Theory]
    [InlineData("{\"type\":\"ready\",\"runId\":\"x\"}")] // missing loginSettings entirely
    [InlineData("{\"type\":\"ready\",\"runId\":\"x\",\"loginSettings\":{\"source\":\"real\",\"provesPlatformIntegration\":false}}")] // wrong source value
    public void Malformed_ready_payloads_fail_the_loginSettings_schema_check(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var hasValidLoginSettingsStub =
            root.TryGetProperty("loginSettings", out var loginSettings)
            && loginSettings.TryGetProperty("source", out var source)
            && source.GetString() == "stub";

        Assert.False(hasValidLoginSettingsStub);
    }

    private sealed class NullOutput : Xunit.Abstractions.ITestOutputHelper
    {
        public void WriteLine(string message) { }
        public void WriteLine(string format, params object[] args) { }
    }
}
