// WP-INFRA-AUTH-ACCEPTANCE-HOST-01 — F12 (Aşama F) + G6 (Aşama G): the host's own pre-ready login call
// (TryLoginAsync) must verify, from the kernel (LOCAL_PEERPID/LOCAL_PEERCRED on the connected Unix domain socket),
// that the peer it is about to send an actor's password to really is the API process this host itself spawned —
// BEFORE any byte of the HTTP request is written — and must never follow a redirect (AllowAutoRedirect=false), so
// a 3xx response can never cause the credential-bearing request to be resent to a different target.
//
// G6 — the fake listener now reports "EOF with 0 bytes", "timeout with 0 bytes" or "N bytes" distinctly, and a
// test fails ONLY when N > 0 (Stage F's expected "0 bytes (timeout)" passed only because the refused socket was
// never closed). A new case keeps the PID correct and makes only the START TIME wrong, so that comparison is
// proven on its own. The UID comparison cannot be driven without a second OS user; it is covered by code reading
// only. Every temp directory is removed; the listener exits on its own (accept/recv timeouts) and, if it somehow
// does not, is signalled only through the harness's PID + start-time verified KillSingle.
//
// Scenarios call Program.TryLoginAsync directly (internal for exactly this) against a listener this test itself
// owns; none spawns the real out-of-process host or mongod.

using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Diten.AuthService.AccountKindAcceptanceHost.Tests;

public sealed class LoginPeerVerificationTests
{
    // One accept (15s cap), then read until EOF or a 3s silence. Prints exactly one RESULT line:
    //   RESULT eof <n>      — the client closed the connection after sending n bytes
    //   RESULT timeout <n>  — nothing more arrived for 3s after n bytes
    //   RESULT no-connection 0
    private const string ListenerScript =
        "import os,socket,sys\n" +
        "path=sys.argv[1]\n" +
        "s=socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)\n" +
        "s.bind(path)\n" +
        "s.listen(1)\n" +
        "s.settimeout(15)\n" +
        "print('READY %d' % os.getpid(), flush=True)\n" +
        "try:\n" +
        "    conn,_=s.accept()\n" +
        "except socket.timeout:\n" +
        "    print('RESULT no-connection 0', flush=True)\n" +
        "    sys.exit(0)\n" +
        "conn.settimeout(3)\n" +
        "total=0\n" +
        "try:\n" +
        "    while True:\n" +
        "        chunk=conn.recv(65536)\n" +
        "        if not chunk:\n" +
        "            print('RESULT eof %d' % total, flush=True)\n" +
        "            break\n" +
        "        total+=len(chunk)\n" +
        "except socket.timeout:\n" +
        "    print('RESULT timeout %d' % total, flush=True)\n";

    private readonly ITestOutputHelper _output;

    public LoginPeerVerificationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // ── (a) a genuinely different real OS process is listening at the expected path ────────────────────────
    [Fact]
    public async Task WrongPeerListening_PasswordNeverSent_FakeListenerReceivesZeroBytes()
    {
        await RunAgainstFakeListenerAsync(
            expectedIdentity: _ => (Environment.ProcessId, 0L), // the test process, not the listener: wrong PID
            scenario: "wrong-pid");
    }

    // ── (d) G6 — the PID is the listener's own, only the start time is wrong ───────────────────────────────
    [Fact]
    public async Task CorrectPidWrongStartTime_PasswordNeverSent_FakeListenerReceivesZeroBytes()
    {
        await RunAgainstFakeListenerAsync(
            expectedIdentity: listenerPid => (listenerPid, ProcessTiming.GetStartTimeUnixMs(listenerPid) + 1),
            scenario: "right-pid-wrong-start-time");
    }

    private async Task RunAgainstFakeListenerAsync(Func<int, (int Pid, long StartTime)> expectedIdentity, string scenario)
    {
        var harness = new SupervisorTestHarness(_output);
        var dir = Directory.CreateTempSubdirectory("dak-peer-").FullName;
        var sockPath = Path.Combine(dir, "fake-api.sock");

        var psi = new ProcessStartInfo("python3")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add(ListenerScript);
        psi.ArgumentList.Add(sockPath);

        using var fakeListener = Process.Start(psi) ?? throw new InvalidOperationException("could not start python3 fake listener");
        var spawnedPid = fakeListener.Id;
        var spawnedStart = SupervisorTestHarness.GetStartTimeUnixMs(spawnedPid);
        try
        {
            var readyLine = await fakeListener.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.NotNull(readyLine);
            Assert.StartsWith("READY ", readyLine);
            var listenerPid = int.Parse(readyLine!["READY ".Length..]);
            _output.WriteLine($"[{scenario}] spawned pid {spawnedPid}, listening pid {listenerPid}");

            var (pid, startTime) = expectedIdentity(listenerPid);
            var loginOk = await Program.TryLoginAsync(
                sockPath, pid, startTime, Guid.NewGuid(), "someone@example.invalid", "irrelevant-password");
            Assert.False(loginOk, "login must not report success against an unverified peer");

            var resultLine = await fakeListener.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
            _output.WriteLine($"[{scenario}] fake listener reported: {resultLine}");
            Assert.NotNull(resultLine);
            var parts = resultLine!.Split(' ');
            Assert.Equal(3, parts.Length);
            Assert.Equal("RESULT", parts[0]);
            Assert.NotEqual("no-connection", parts[1]); // the peer check must have actually been exercised
            Assert.True(parts[1] is "eof" or "timeout", "unexpected listener result kind");
            Assert.True(int.Parse(parts[2]) == 0, "the unverified peer received request bytes");

            Assert.True(await fakeListener.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)).ContinueWith(t => t.IsCompletedSuccessfully),
                "fake listener did not exit on its own");
        }
        finally
        {
            if (!fakeListener.HasExited)
            {
                // Only this test's own child, verified by PID + start time immediately before the signal.
                harness.KillSingle(spawnedPid, spawnedStart, 15, $"G6-{scenario}-listener-cleanup");
                fakeListener.WaitForExit(5000);
            }

            try { Directory.Delete(dir, recursive: true); } catch { /* best effort, own temp dir */ }
        }
    }

    // ── (b) the peer IS the expected process, but replies 3xx — credentials must never follow it ────────────
    [Fact]
    public async Task RedirectResponse_CredentialsNeverFollowIt_RedirectTargetNeverHit()
    {
        var dir = Directory.CreateTempSubdirectory("dak-peer-redirect-").FullName;
        var sockPath = Path.Combine(dir, "fake-api.sock");

        var redirectTargetHit = false;
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://unix:{sockPath}");
        builder.Logging.ClearProviders();
        var app = builder.Build();
        app.MapPost("/api/tenant-auth/login", () => Results.Redirect("/somewhere-else", permanent: false));
        app.MapGet("/somewhere-else", () =>
        {
            redirectTargetHit = true;
            return Results.Ok();
        });
        await app.StartAsync();

        try
        {
            // This test process itself IS the real peer here — a genuine, correct identity, so peer verification
            // passes and the redirect-handling behavior is what's actually being isolated and tested.
            var ownPid = Environment.ProcessId;
            var ownStartTime = ProcessTiming.GetStartTimeUnixMs(ownPid);

            var loginOk = await Program.TryLoginAsync(
                sockPath, ownPid, ownStartTime, Guid.NewGuid(), "someone@example.invalid", "irrelevant-password");

            Assert.False(redirectTargetHit, "credentials must never be sent to a 3xx redirect target");
            Assert.False(loginOk, "a 3xx response is not a successful login");
        }
        finally
        {
            await app.StopAsync(new CancellationTokenSource(TimeSpan.FromSeconds(3)).Token);
            await app.DisposeAsync();
            try { Directory.Delete(dir, recursive: true); } catch { /* best effort, own temp dir */ }
        }
    }
}
