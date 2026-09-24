// WP-INFRA-AUTH-ACCEPTANCE-HOST-01 — F14 (Aşama F) + G3/G7 (Aşama G): nothing about an exception (message, inner
// exception, stack) and no run detail (connection string, binary directory, paths) may reach the host's own
// stdout/stderr — not from Program.cs, not from the in-process prep host (AuthService's Serilog console sink,
// Persistence's Console.WriteLine(ex.Message)), and not from the reused fixture.
//
//   F14 — two real host failures carry a canary into a raw exception message (a bad DITEN_ACCEPTANCE_DOTNET_PATH
//         echoed by Process.Start's Win32Exception; a root path echoed by EphemeralMongo's IOException); the canary
//         must not reach the wire, stdout or stderr.
//   G3(1) — a happy-path real run: the host's stdout is EXACTLY 0 bytes at exit (Console.SetOut(TextWriter.Null)
//         at the start of Main; this host never needs stdout).
//   G3(2) — the fixture's own stderr sites on the acceptance path write a fixed code + the exception TYPE only.
//         Driven in-process through the existing test-only seams: BeforeSeedHookForTesting fails the seed with a
//         canary AND installs DisposeFactoryHookForTesting, which disposes the real factory and then fails with a
//         second canary — reaching both the "factory disposal failed" and the "cleanup after a seed failure" sites
//         in one real start. Neither canary may appear in what the process wrote to stdout or stderr.
//
// G7 — spawned hosts get HOME=<root>/home, never the developer's HOME; every canary assertion on captured text is
// Assert.False(text.Contains(canary), "<fixed text>") so a failure never prints the captured text; a spawned host
// that outlives its test is signalled only through the harness's PID + start-time verified KillSingle.

using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using Diten.AuthService.Application.Tests.Testing;
using Xunit;
using Xunit.Abstractions;

namespace Diten.AuthService.AccountKindAcceptanceHost.Tests;

[Collection("SevenStateSupervisor")]
public sealed class ExceptionTextLeakTests
{
    private readonly ITestOutputHelper _output;

    public ExceptionTextLeakTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // ── G3(1) — happy path, real host, real API, real mongod: host stdout is 0 bytes ───────────────────────────
    [Fact]
    public async Task HappyPathRealRun_HostStdoutIsExactlyZeroBytesAtExit()
    {
        var h = new SupervisorTestHarness(_output);
        var (root, dev, ino) = h.CreateRunRoot();
        h.PrepareFixedSubdirs(root);
        using var listener = h.Listen(root, out var runId);

        var spawned = SpawnHostWithCapturedOutput(root, runId, dotnetPathOverrideForApiChild: null);
        try
        {
            var cycle = await h.DriveFullCycle(listener, runId);
            Assert.True(cycle.Ok, "did not reach ready/seed-ready");
            Assert.True(cycle.ByeReceived, "bye was not received");

            Assert.True(spawned.Process.WaitForExit((int)TimeSpan.FromSeconds(20).TotalMilliseconds), "host did not exit in time");
            Assert.Equal(0, spawned.Process.ExitCode);

            var stdout = await spawned.Stdout.WaitAsync(TimeSpan.FromSeconds(10));
            var stderr = await spawned.Stderr.WaitAsync(TimeSpan.FromSeconds(10));
            _output.WriteLine($"host stdout: {stdout.Length} bytes; host stderr: {stderr.Length} bytes");

            Assert.True(stdout.Length == 0, "the host wrote to stdout during a normal run");
        }
        finally
        {
            StopIfStillRunning(h, spawned, "G3-happy-path-cleanup");
            h.CleanRoot(root, dev, ino);
        }
    }

    // ── G3(2) — the fixture's acceptance-path stderr sites, reached for real, carry no canary ─────────────────
    [Fact]
    public async Task AcceptancePathFixtureDiagnostics_SeedAndDisposalFailuresWithCanaries_NoCanaryOnStdoutOrStderr()
    {
        var seedCanary = "CANARY-SEED-" + Guid.NewGuid().ToString("N");
        var disposalCanary = "CANARY-DISPOSAL-" + Guid.NewGuid().ToString("N");

        var root = Directory.CreateTempSubdirectory("dak-g3-").FullName;
        var mongoDir = Directory.CreateDirectory(Path.Combine(root, "mongo")).FullName;
        var contentDir = Directory.CreateDirectory(Path.Combine(root, "content")).FullName;
        var homeDir = Directory.CreateDirectory(Path.Combine(root, "home")).FullName;

        var previousHome = Environment.GetEnvironmentVariable("HOME");
        var previousOut = Console.Out;
        var previousError = Console.Error;
        var capturedOut = new StringWriter();
        var capturedError = new StringWriter();
        Exception? ex;
        try
        {
            Environment.SetEnvironmentVariable("HOME", homeDir);
            Console.SetOut(capturedOut);
            Console.SetError(capturedError);

            ex = await Record.ExceptionAsync(() => AccountKindAcceptance.AuthTestHost.StartWithMongoDataDirectoryAsync(
                mongoDir,
                contentDir,
                beforeSeedHookForTesting: host =>
                {
                    // Runs after the real host was built, before the fixture's SeedAsync. The disposal hook disposes
                    // the REAL factory, then fails — the cleanup that follows the seed failure reaches both sites.
                    host.DisposeFactoryHookForTesting = async factory =>
                    {
                        await factory.DisposeAsync();
                        throw new InvalidOperationException(disposalCanary);
                    };
                    throw new InvalidOperationException(seedCanary);
                }));
        }
        finally
        {
            Console.SetOut(previousOut);
            Console.SetError(previousError);
            Environment.SetEnvironmentVariable("HOME", previousHome);
        }

        try
        {
            Assert.IsType<AccountKindSeedFailedException>(ex);

            var outText = capturedOut.ToString();
            var errorText = capturedError.ToString();
            _output.WriteLine($"captured stdout: {outText.Length} chars; captured stderr: {errorText.Length} chars");

            Assert.False(outText.Contains(seedCanary, StringComparison.Ordinal), "the seed-failure canary reached stdout");
            Assert.False(outText.Contains(disposalCanary, StringComparison.Ordinal), "the disposal-failure canary reached stdout");
            Assert.False(errorText.Contains(seedCanary, StringComparison.Ordinal), "the seed-failure canary reached stderr");
            Assert.False(errorText.Contains(disposalCanary, StringComparison.Ordinal), "the disposal-failure canary reached stderr");

            // Proof the sites were actually reached (not a vacuous pass): each wrote its fixed code + type name.
            Assert.True(errorText.Contains("[AccountKindAcceptance] factory-disposal-failed (InvalidOperationException)", StringComparison.Ordinal),
                "the factory-disposal diagnostic site was not reached or did not write its fixed code");
            Assert.True(errorText.Contains("[AccountKindAcceptance] seed-cleanup-failed (InvalidOperationException)", StringComparison.Ordinal),
                "the seed-cleanup diagnostic site was not reached or did not write its fixed code");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* best effort, own temp dir */ }
        }
    }

    // ── F14 — bad DITEN_ACCEPTANCE_DOTNET_PATH carrying a canary ─────────────────────────────────────────────
    [Fact]
    public async Task BadDotnetPathCanary_NeverLeaksIntoHostOutputOrWireOrTestFailureText()
    {
        const string canary = "CANARY-DOTNET-PATH-7c1e9a";
        var badDotnetPath = $"/nonexistent-{canary}/dotnet";

        var h = new SupervisorTestHarness(_output);
        var (root, dev, ino) = h.CreateRunRoot();
        h.PrepareFixedSubdirs(root);
        using var listener = h.Listen(root, out var runId);

        var spawned = SpawnHostWithCapturedOutput(root, runId, dotnetPathOverrideForApiChild: badDotnetPath);

        try
        {
            var acceptTask = listener.AcceptAsync();
            Assert.Same(acceptTask, await Task.WhenAny(acceptTask, Task.Delay(TimeSpan.FromSeconds(10))));
            using var sock = await acceptTask;
            using var stream = new NetworkStream(sock, ownsSocket: false);
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            await reader.ReadLineAsync(); // hello
            await h.SendAsync(stream, runId, "hello-ack", includeProtocolVersion: true);

            var errorMsg = await h.ReceiveRaw(reader, TimeSpan.FromSeconds(30));
            Assert.NotNull(errorMsg);
            Assert.Equal("error", errorMsg!.Value.GetProperty("type").GetString());
            Assert.Equal("api-start-failed", errorMsg.Value.GetProperty("code").GetString());
            var wireMessage = errorMsg.Value.GetProperty("message").GetString() ?? "";
            Assert.False(wireMessage.Contains(canary, StringComparison.Ordinal), "the wire error message carries the canary");

            Assert.True(spawned.Process.WaitForExit((int)TimeSpan.FromSeconds(15).TotalMilliseconds), "host did not exit in time");

            var stdoutText = Encoding.UTF8.GetString(await spawned.Stdout.WaitAsync(TimeSpan.FromSeconds(10)));
            var stderrText = Encoding.UTF8.GetString(await spawned.Stderr.WaitAsync(TimeSpan.FromSeconds(10)));
            _output.WriteLine($"host stdout captured: {stdoutText.Length} chars; host stderr captured: {stderrText.Length} chars");

            Assert.False(stdoutText.Contains(canary, StringComparison.Ordinal), "the canary reached host stdout");
            Assert.False(stderrText.Contains(canary, StringComparison.Ordinal), "the canary reached host stderr");
        }
        finally
        {
            StopIfStillRunning(h, spawned, "F14-bad-dotnet-path-cleanup");
            h.CleanRoot(root, dev, ino);
        }
    }

    // ── F14 — a root path carrying a canary, forced mongod failure ───────────────────────────────────────────
    [Fact]
    public async Task RootPathCanary_ForcedMongodFailure_NeverLeaksIntoHostOutputOrWireOrTestFailureText()
    {
        const string canary = "CANARY-ROOT-PATH-d4f01b";

        // A test-owned root whose OWN path embeds the canary, instead of the harness's mkdtemp'd "dak-XXXXXX"
        // name — this is what lets a path-echoing exception (EphemeralMongo's own IOException, measured to
        // include the full path) carry a caller-chosen canary at all.
        var root = Directory.CreateTempSubdirectory($"dak-{canary}-").FullName;
        var dev = 0L;
        var ino = 0L;
        try { (dev, ino) = LstatDevIno(root); } catch { /* best effort for the CleanRoot dev/ino re-check below */ }

        var h = new SupervisorTestHarness(_output);
        h.PrepareFixedSubdirs(root);

        // Same forced-mongod-failure technique as WireLevelErrorMappingTests.GenuineMongodFailure_OverTheWire_...
        // — EphemeralMongo auto-creates a missing directory, but refuses when the path exists as a plain FILE.
        Directory.Delete(Path.Combine(root, "mongo"));
        File.WriteAllText(Path.Combine(root, "mongo"), "not a directory, on purpose (F14 forced mongo-start-failed)");

        using var listener = h.Listen(root, out var runId);
        var spawned = SpawnHostWithCapturedOutput(root, runId, dotnetPathOverrideForApiChild: null);

        try
        {
            var acceptTask = listener.AcceptAsync();
            Assert.Same(acceptTask, await Task.WhenAny(acceptTask, Task.Delay(TimeSpan.FromSeconds(10))));
            using var sock = await acceptTask;
            using var stream = new NetworkStream(sock, ownsSocket: false);
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            await reader.ReadLineAsync(); // hello
            await h.SendAsync(stream, runId, "hello-ack", includeProtocolVersion: true);

            var errorMsg = await h.ReceiveRaw(reader, TimeSpan.FromSeconds(30));
            Assert.NotNull(errorMsg);
            Assert.Equal("error", errorMsg!.Value.GetProperty("type").GetString());
            Assert.Equal("mongo-start-failed", errorMsg.Value.GetProperty("code").GetString());
            var wireMessage = errorMsg.Value.GetProperty("message").GetString() ?? "";
            Assert.False(wireMessage.Contains(canary, StringComparison.Ordinal), "the wire error message carries the canary");

            Assert.True(spawned.Process.WaitForExit((int)TimeSpan.FromSeconds(15).TotalMilliseconds), "host did not exit in time");

            var stdoutText = Encoding.UTF8.GetString(await spawned.Stdout.WaitAsync(TimeSpan.FromSeconds(10)));
            var stderrText = Encoding.UTF8.GetString(await spawned.Stderr.WaitAsync(TimeSpan.FromSeconds(10)));
            _output.WriteLine($"host stdout captured: {stdoutText.Length} chars; host stderr captured: {stderrText.Length} chars");

            Assert.False(stdoutText.Contains(canary, StringComparison.Ordinal), "the canary reached host stdout");
            Assert.False(stderrText.Contains(canary, StringComparison.Ordinal), "the canary reached host stderr");
        }
        finally
        {
            StopIfStillRunning(h, spawned, "F14-root-path-cleanup");
            h.CleanRoot(root, dev, ino);
        }
    }

    private sealed record SpawnedHost(Process Process, long StartTime, Task<byte[]> Stdout, Task<byte[]> Stderr);

    /// <summary>
    /// Spawns the real host via the managed Process API (not the harness's posix_spawn) SOLELY so this test can
    /// capture the host's own stdout/stderr BYTES directly — the harness's SpawnHost inherits the test runner's
    /// console instead of redirecting it. The environment is the same explicit set the harness passes, with the
    /// run-owned <root>/home as HOME (G7).
    /// </summary>
    private static SpawnedHost SpawnHostWithCapturedOutput(string root, string runId, string? dotnetPathOverrideForApiChild)
    {
        var dotnetPath = "/usr/local/share/dotnet/dotnet";
        var hostDll = Path.Combine(AppContext.BaseDirectory, "Diten.AuthService.AccountKindAcceptanceHost.dll");
        var psi = new ProcessStartInfo(dotnetPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add(hostDll);
        psi.Environment.Clear();
        psi.Environment["DITEN_ACCEPTANCE_ROOT"] = root;
        psi.Environment["DITEN_ACCEPTANCE_RUN_ID"] = runId;
        psi.Environment["DITEN_ACCEPTANCE_DOTNET_PATH"] = dotnetPathOverrideForApiChild ?? dotnetPath;
        psi.Environment["PATH"] = Environment.GetEnvironmentVariable("PATH");
        psi.Environment["HOME"] = Path.Combine(root, "home");

        var process = Process.Start(psi) ?? throw new InvalidOperationException("could not start the acceptance host");
        var startTime = SupervisorTestHarness.GetStartTimeUnixMs(process.Id);
        return new SpawnedHost(
            process,
            startTime,
            ReadAllBytesAsync(process.StandardOutput.BaseStream),
            ReadAllBytesAsync(process.StandardError.BaseStream));

        static async Task<byte[]> ReadAllBytesAsync(Stream stream)
        {
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            return buffer.ToArray();
        }
    }

    /// <summary>Only this test's own child, verified by PID + start time immediately before the signal (D2).</summary>
    private static void StopIfStillRunning(SupervisorTestHarness h, SpawnedHost spawned, string purpose)
    {
        if (!spawned.Process.HasExited)
        {
            h.KillSingle(spawned.Process.Id, spawned.StartTime, 15, purpose);
            spawned.Process.WaitForExit(10000);
        }

        spawned.Process.Dispose();
    }

    private static unsafe (long dev, long ino) LstatDevIno(string path)
    {
        var buf = System.Runtime.InteropServices.Marshal.AllocHGlobal(200);
        try
        {
            if (lstat(path, buf) != 0) throw new InvalidOperationException("lstat failed for " + path);
            return (System.Runtime.InteropServices.Marshal.ReadInt32(buf, 0), System.Runtime.InteropServices.Marshal.ReadInt64(buf, 8));
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(buf); }

        [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "lstat", SetLastError = true)]
        static extern int lstat(string path, IntPtr buf);
    }
}
