// WP-INFRA-AUTH-ACCEPTANCE-HOST-01 — T3 (Aşama D): a real, out-of-process, wire-adjacent proof that the API
// child's environment is genuinely allow-list-only (C10), that a poisoned SUPERVISOR-side environment (canary
// ConnectionStrings__/MongoDbSettings__/JwtSettings__/Smtp__/Eventing__/OTEL_*/DOTNET_STARTUP_HOOKS/
// ASPNETCORE_HOSTINGSTARTUPASSEMBLIES values) never reaches the child, that launchSettings.json's own variable
// (ASPNETCORE_ENVIRONMENT) arrives with the HOST's value ("AcceptanceHost") and never "Development", and that the
// real per-developer user-secrets file is never opened.
//
// Mechanism (T3-A/T3-C): DITEN_ACCEPTANCE_DOTNET_PATH (already an existing test-only override seam, used by T6)
// is pointed at a throwaway shell script instead of real `dotnet`. Program.cs execs it EXACTLY as it would exec
// the real dotnet (same FileName, same ArgumentList, same allow-list-only envp built by psi.Environment) — from
// Program.cs's point of view this is indistinguishable from launching the real API, so the captured envp is the
// REAL one the production Auth API would have received, not a simulation of it. The script writes `env` output
// into its own working directory (contentDir, a FIXED path under the supervisor-owned root we already know) and
// then sleeps, so WaitForHealthyAsync legitimately times out and the host legitimately reports api-bind-failed —
// proving this exercises the real health-check/failure code path, not a shortcut.
//
// `env`'s own interpreter (/bin/sh, invoked via the script's shebang) adds PWD/SHLVL/_ itself — verified
// empirically against `env -i PATH=... /bin/sh -c 'env'` on this machine; these three keys are NOT part of the
// envp Program.cs builds and are excluded from the exact-set comparison below for that reason.
//
// Mechanism (T3-B): a REAL full run (real dotnet, real API, real mongod) reaches `ready`; lsof -p <apiPid> is
// used to confirm no open file descriptor path contains "usersecrets" anywhere — the real per-developer
// ~/.microsoft/usersecrets/<id>/secrets.json file is never touched, and neither is the also-empty redirected one
// under the isolated HOME. This test deliberately never reads or stats the real secrets file itself (C3
// boundary) — only whether the running API process holds ANY descriptor whose path mentions "usersecrets".

using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Diten.AuthService.AccountKindAcceptanceHost.Tests;

[Collection("SevenStateSupervisor")]
public sealed class PoisonedEnvironmentTests
{
    private readonly ITestOutputHelper _output;

    public PoisonedEnvironmentTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // The exact allow-list Program.cs builds (C10) — keys only; values are per-run/dynamic for several of these.
    // Kept here as a literal, independent restatement of the contract (not derived from Program.cs), so that any
    // ADDITION to the production allow-list (the T3 sabotage) is caught as an unexpected extra key.
    private static readonly HashSet<string> ExpectedAllowListKeys = new()
    {
        "PATH", "HOME", "TMPDIR", "DOTNET_ROOT", "ASPNETCORE_ENVIRONMENT", "ASPNETCORE_URLS",
        "MongoDbSettings__ConnectionString", "MongoDbSettings__DatabaseName", "JwtSettings__Secret",
        "Eventing__Transport", "Smtp__Enabled", "TenantResolution__DevBypassEnabled",
        "Observability__Metrics__Enabled", "Observability__Seq__Enabled", "OTEL_SDK_DISABLED",
        "InternalEventAuth__ApiKey", "PlatformService__InternalApiKey", "PlatformService__BaseUrl",
    };

    // Keys /bin/sh itself adds when it runs the capture script — artifacts of the INTERPRETER, not of the envp
    // Program.cs constructed. Measured: `env -i PATH=/usr/bin:/bin HOME=/tmp/x FOO=y /bin/sh -c 'env'` on this
    // machine prints PATH, HOME, FOO (unchanged) plus PWD, SHLVL, _ (added by sh).
    private static readonly HashSet<string> ShellInjectedKeys = new() { "PWD", "SHLVL", "_" };

    [Fact]
    public async Task ApiChildEnvironment_IsExactlyTheAllowList_CanariesAndLaunchSettingsNeverReachTheChild()
    {
        var h = new SupervisorTestHarness(_output);
        var (root, dev, ino) = h.CreateRunRoot();
        h.PrepareFixedSubdirs(root);

        var contentDir = Path.Combine(root, "content");
        var observedEnvPath = Path.Combine(contentDir, "observed-api-env.txt");
        var fakeDotnetPath = Path.Combine(root, "fake-dotnet.sh");
        File.WriteAllText(fakeDotnetPath, "#!/bin/sh\nenv > ./observed-api-env.txt\nsleep 60\n");
        File.SetUnixFileMode(fakeDotnetPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        using var listener = h.Listen(root, out var runId);

        // T3 — a poisoned SUPERVISOR-side environment: every canary family named in the WP prompt, plus the two
        // startup-hook vectors. These land on the HOST process's own env (posix_spawn's envp is exactly what
        // SpawnHost builds — nothing is inherited from the test process — see SpawnHost/SpawnWithNewProcessGroup),
        // simulating a supervisor whose own environment is poisoned. The assertion below proves none of this
        // reaches the API child regardless.
        // MEASURED FINDING: DOTNET_STARTUP_HOOKS and ASPNETCORE_HOSTINGSTARTUPASSEMBLIES cannot be exercised via
        // this injection method. The accept-HOST is itself a `dotnet` process; the .NET runtime processes
        // DOTNET_STARTUP_HOOKS (loading the named assembly) before Program.Main even runs. Injecting a bogus
        // DOTNET_STARTUP_HOOKS into the host's own spawn env crashed the HOST itself on startup (observed:
        // "Unhandled exception. System.ArgumentException: Startup hook assembly '/tmp/evil-hook.dll' failed to
        // load"), before it ever reached the code that builds the child's environment — this is inherent .NET
        // hosting behavior (identical for a REAL poisoned supervisor, which is also necessarily a dotnet-hosted
        // process), not a gap in this WP's code, and not something a canary-and-observe test can exercise without
        // also killing the process doing the observing. The structural guarantee that these two specifically
        // cannot reach the CHILD either way is `psi.Environment.Clear()` (Program.cs) — it discards the entire
        // inherited/ambient environment unconditionally before the explicit allow-list is added, so neither key
        // survives into the child's envp regardless of what the host's own environment contained. This is proven
        // by the exact-key-set comparison below using the six OTHER canary families, which do not crash a dotnet
        // host and fully exercise the same Clear()-then-allow-list code path.
        var canaries = new[]
        {
            "ConnectionStrings__Default=poisoned-canary",
            "MongoDbSettings__CanaryExtra=poisoned-canary",     // same prefix as an allowed key, different key
            "JwtSettings__CanaryOther=poisoned-canary",
            "Smtp__Host=poisoned-canary",
            "Eventing__CanaryQueue=poisoned-canary",
            "OTEL_EXPORTER_OTLP_ENDPOINT=poisoned-canary",
        };
        var hostPid = h.SpawnHost(root, runId, dotnetPathOverrideForApiChild: fakeDotnetPath, extraEnv: canaries);

        try
        {
            var acceptTask = listener.AcceptAsync();
            Assert.Same(acceptTask, await Task.WhenAny(acceptTask, Task.Delay(TimeSpan.FromSeconds(10))));
            using var sock = await acceptTask;
            using var stream = new NetworkStream(sock, ownsSocket: false);
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            await reader.ReadLineAsync(); // hello
            await h.SendAsync(stream, runId, "hello-ack", includeProtocolVersion: true);

            // Poll for the capture file rather than waiting the full health-check timeout — the fake dotnet script
            // writes it almost immediately once Program.cs execs it (after real seeding completes).
            var pollDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(45);
            while (DateTime.UtcNow < pollDeadline && !File.Exists(observedEnvPath))
            {
                await Task.Delay(200);
            }
            Assert.True(File.Exists(observedEnvPath), "fake-dotnet.sh never ran / never wrote the capture file — real seeding may have failed before the API child was even launched");

            var observed = new Dictionary<string, string>();
            foreach (var line in await File.ReadAllLinesAsync(observedEnvPath))
            {
                var idx = line.IndexOf('=');
                if (idx <= 0) continue;
                observed[line[..idx]] = line[(idx + 1)..];
            }

            var observedKeys = observed.Keys.Where(k => !ShellInjectedKeys.Contains(k)).ToHashSet();
            _output.WriteLine("observed keys: " + string.Join(", ", observedKeys.OrderBy(k => k)));

            var missing = ExpectedAllowListKeys.Except(observedKeys).ToList();
            var extra = observedKeys.Except(ExpectedAllowListKeys).ToList();
            Assert.True(missing.Count == 0, "allow-listed keys missing from the child's real environment: " + string.Join(", ", missing));
            Assert.True(extra.Count == 0, "UNEXPECTED keys reached the child's real environment (allow-list breach): " + string.Join(", ", extra));

            // Canary families explicitly, by substring, as a second independent check (defense in depth beyond
            // the exact-set comparison above).
            foreach (var forbidden in new[]
            {
                "ConnectionStrings__", "JwtSettings__CanaryOther", "MongoDbSettings__CanaryExtra", "Smtp__Host",
                "Eventing__CanaryQueue", "OTEL_EXPORTER_OTLP_ENDPOINT",
            })
            {
                Assert.DoesNotContain(observedKeys, k => k.Contains(forbidden, StringComparison.Ordinal));
            }

            // launchSettings.json's only variable (ASPNETCORE_ENVIRONMENT=Development) never reaches the child —
            // the host's own value ("AcceptanceHost", C3/C10) is what actually arrives.
            Assert.Equal("AcceptanceHost", observed["ASPNETCORE_ENVIRONMENT"]);
            Assert.NotEqual("Development", observed["ASPNETCORE_ENVIRONMENT"]);

            // HOME is the isolated, empty <root>/home — never the real developer HOME (empirical proof the
            // real user-secrets path can never even be constructed from this process's own env).
            Assert.Equal(Path.Combine(root, "home"), observed["HOME"]);
            Assert.Equal(Path.Combine(root, "tmp"), observed["TMPDIR"]);
            Assert.Equal("/usr/bin:/bin", observed["PATH"]);

            // Drain the rest of the real protocol (host legitimately times out its health check and reports
            // api-bind-failed) so the process exits cleanly instead of us having to signal it.
            var next = await h.ReceiveRaw(reader, TimeSpan.FromSeconds(35));
            Assert.NotNull(next);
            Assert.Equal("error", next!.Value.GetProperty("type").GetString());
            Assert.Equal("api-bind-failed", next.Value.GetProperty("code").GetString());

            var exitCode = await h.WaitForExitAsync(hostPid, TimeSpan.FromSeconds(15));
            Assert.Equal(3, exitCode); // ExitCodes.ApiStartFailed — proves the REAL health-check failure path ran
        }
        finally
        {
            h.CleanRoot(root, dev, ino);
        }
    }

    [Fact]
    public async Task RealApiChild_NeverOpensAnyFileDescriptorMentioningUserSecrets()
    {
        var h = new SupervisorTestHarness(_output);
        var (root, dev, ino) = h.CreateRunRoot();
        h.PrepareFixedSubdirs(root);
        using var listener = h.Listen(root, out var runId);
        var hostPid = h.SpawnHost(root, runId); // real dotnet, real API — no override

        try
        {
            // Manual protocol driving (NOT the shared DriveToReady helper) — DriveToReady disposes the control
            // socket the moment it returns, which triggers the host's own teardown before this test's own lsof
            // call can run (measured: an earlier version of this test raced its own cleanup and always saw an
            // already-exited process, i.e. a false-positive pass). Keeping the channel open here until AFTER the
            // lsof check, then sending a real `shutdown`, is what makes this a genuine live-process check.
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

            var seedReady = await h.ReceiveRaw(reader, TimeSpan.FromSeconds(60));
            Assert.NotNull(seedReady);

            Assert.True(h.ProcessExists(apiPid), "api process is not alive to inspect");

            var lsof = RunLsof(apiPid);
            _output.WriteLine($"lsof -p {apiPid} ({lsof.Lines.Length} lines):\n" + string.Join('\n', lsof.Lines));
            Assert.True(lsof.ToolAvailable, "lsof is required for this evidence and is not available — RED, not skipped");
            Assert.True(lsof.Lines.Length > 1, "lsof returned no descriptors at all for a process we just confirmed alive — evidence is not trustworthy");

            // Match the SECRETS FILE location (".microsoft/usersecrets/" — the directory PathHelper.
            // GetSecretsPathFromSecretsId builds under HOME on Unix) or the literal file name "secrets.json", not
            // the FRAMEWORK ASSEMBLY Microsoft.Extensions.Configuration.UserSecrets.dll — that DLL is always
            // loaded as part of the shared runtime regardless of whether user-secrets are actually read (measured:
            // an earlier version of this check false-positived on exactly that DLL being an open text/lib
            // descriptor). Loading the assembly is not evidence of reading the file.
            var secretsHits = lsof.Lines
                .Where(l => !l.Contains(".dll", StringComparison.OrdinalIgnoreCase))
                .Where(l => l.Contains(".microsoft/usersecrets", StringComparison.OrdinalIgnoreCase)
                    || l.Contains("secrets.json", StringComparison.OrdinalIgnoreCase))
                .ToList();
            Assert.True(secretsHits.Count == 0, "api process holds a descriptor for the user-secrets FILE (not just the framework assembly): " + string.Join(" | ", secretsHits));

            // Clean shutdown, real protocol, so nothing needs to be signaled.
            await h.SendAsync(stream, runId, "shutdown");
            var bye = await h.ReceiveRaw(reader, TimeSpan.FromSeconds(15));
            Assert.NotNull(bye);
            Assert.Equal("bye", bye!.Value.GetProperty("type").GetString());

            await Task.Delay(1000);
            Assert.False(h.ProcessExists(apiPid), "api process leaked");
            Assert.False(h.ProcessExists(hostPid), "host process leaked");
        }
        finally
        {
            h.CleanRoot(root, dev, ino);
        }
    }

    // ── G2(b)(ii) (Aşama G) — the HOST's own process environment is reduced to an allow-list at the start of Main ──
    // The in-process prep host's WebApplication.CreateBuilder adds the WHOLE process environment as a configuration
    // source in its eager window (before any fixture callback can clear sources), so the host itself must not carry
    // anything the supervisor did not mean to give it.

    // Independent literal restatement of the contract (not derived from Program.cs): an addition or removal on the
    // production set is caught here.
    private static readonly HashSet<string> ExpectedHostAllowList = new(StringComparer.Ordinal)
    {
        "DITEN_ACCEPTANCE_ROOT", "DITEN_ACCEPTANCE_RUN_ID", "DITEN_ACCEPTANCE_DOTNET_PATH",
        "PATH", "HOME", "TMPDIR", "DITEN_ACCEPTANCE_TESTONLY_CORRUPT_PERMISSION_KEY",
    };

    [Fact]
    public void HostAllowList_IsExactlyTheContract_AndSelectionIsAPureOrdinalFilter()
    {
        Assert.True(ExpectedHostAllowList.SetEquals(Program.HostEnvironmentAllowList), "the host environment allow-list differs from the contract");

        // A dictionary only — this test never mutates the runner's own environment.
        var poisoned = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["DITEN_ACCEPTANCE_ROOT"] = "/r",
            ["DITEN_ACCEPTANCE_RUN_ID"] = "id",
            ["DITEN_ACCEPTANCE_DOTNET_PATH"] = "/d/dotnet",
            ["PATH"] = "/usr/bin:/bin",
            ["HOME"] = "/r/home",
            ["TMPDIR"] = "/r/tmp",
            ["DITEN_ACCEPTANCE_TESTONLY_CORRUPT_PERMISSION_KEY"] = "k",
            ["Mfa__Enabled"] = "true",
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["DOTNET_ENVIRONMENT"] = "Development",
            ["MongoDbSettings__ConnectionString"] = "mongodb://127.0.0.1:27017",
            ["Observability__Seq__ApiKey"] = "poisoned",
            ["path"] = "/lowercase/is/a/different/name",
            ["HOME "] = "/trailing/space/is/a/different/name",
            ["DITEN_ACCEPTANCE_ROOT_EXTRA"] = "prefix-match-is-not-a-match",
        };
        var snapshot = new Dictionary<string, string?>(poisoned, StringComparer.Ordinal);

        var kept = Program.SelectAllowListedEnvironment(poisoned);

        Assert.True(ExpectedHostAllowList.SetEquals(kept.Keys), "the selection kept a key outside the allow-list or dropped an allowed one");
        foreach (var key in ExpectedHostAllowList)
        {
            Assert.Equal(poisoned[key], kept[key]);
        }

        Assert.Equal(snapshot, poisoned); // pure: the input is untouched
    }

    [Fact]
    public async Task RealHost_PoisonedEnvironment_StillReachesReadyAndExitsZero()
    {
        var h = new SupervisorTestHarness(_output);
        var (root, dev, ino) = h.CreateRunRoot();
        h.PrepareFixedSubdirs(root);
        using var listener = h.Listen(root, out var runId);

        // Black-box discriminator (measured): Mfa:Enabled is read EAGERLY by AuthService's
        // Infrastructure/DependencyInjection.cs BuildSecretRequirements and is not one of the fixture's overrides.
        // If it reached the prep host's configuration, Mfa:HashSecret would become a required secret, startup
        // validation would throw, and the host would report mongo-start-failed/exit 2 instead of ready/exit 0.
        // The other entries are the same canary families the API-child test uses, plus environment-name vectors.
        var poisoned = new[]
        {
            "Mfa__Enabled=true",
            "ASPNETCORE_ENVIRONMENT=Development",
            "DOTNET_ENVIRONMENT=Development",
            "JwtSettings__Issuer=poisoned-canary",
            "Observability__Seq__ApiKey=poisoned-canary",
            "ConnectionStrings__Default=poisoned-canary",
            "OTEL_EXPORTER_OTLP_ENDPOINT=poisoned-canary",
        };
        var hostPid = h.SpawnHost(root, runId, extraEnv: poisoned);

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
            // On failure this shows the protocol error CODE only (a fixed P2 code, never exception text).
            Assert.Equal("ready", ready!.Value.GetProperty("type").GetString() is "error"
                ? "error:" + ready.Value.GetProperty("code").GetString()
                : ready.Value.GetProperty("type").GetString());

            var seedReady = await h.ReceiveRaw(reader, TimeSpan.FromSeconds(60));
            Assert.NotNull(seedReady);
            Assert.Equal("seed-ready", seedReady!.Value.GetProperty("type").GetString());

            await h.SendAsync(stream, runId, "shutdown");
            var bye = await h.ReceiveRaw(reader, TimeSpan.FromSeconds(15));
            Assert.NotNull(bye);
            Assert.Equal("bye", bye!.Value.GetProperty("type").GetString());

            var exitCode = await h.WaitForExitAsync(hostPid, TimeSpan.FromSeconds(20));
            Assert.Equal(0, exitCode); // ExitCodes.Success
        }
        finally
        {
            h.CleanRoot(root, dev, ino);
        }
    }

    internal static (bool ToolAvailable, string[] Lines) RunLsof(int pid) => RunLsof2(pid, string.Empty);

    internal static (bool ToolAvailable, string[] Lines) RunLsof2(int pid, string extraArgs)
    {
        try
        {
            var args = string.IsNullOrEmpty(extraArgs) ? $"-p {pid}" : $"-p {pid} {extraArgs}";
            var psi = new ProcessStartInfo("lsof", args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var p = Process.Start(psi)!;
            var stdout = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);
            // lsof exits 1 when the process has open files but some are inaccessible; treat any stdout as success.
            return (true, stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            return (false, Array.Empty<string>());
        }
    }
}
