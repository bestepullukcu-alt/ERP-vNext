// WP-INFRA-AUTH-ACCEPTANCE-HOST-01 — the out-of-process acceptance host (W2). Protocol v1.2 (v1.1 + C7-C10 +
// D1-D8 + P1-P8, CT-approved 2026-09-15/16).
//
// This process is the "host" side. A supervisor (PPM's test process, out of scope — see the WP boundary) starts
// it in a NEW process group (D1: posix_spawn + POSIX_SPAWN_SETPGROUP) with three environment variables:
//   DITEN_ACCEPTANCE_ROOT         — the supervisor-owned, mkdtemp'd, lstat-verified 0700 run root (C9). This
//                                    host reads ONLY the fixed relative names below from it; no path is ever
//                                    read from a message, and `ready` carries no path field.
//   DITEN_ACCEPTANCE_RUN_ID       — every message on the control channel must echo this back (C7 §0/§6).
//   DITEN_ACCEPTANCE_DOTNET_PATH  — the supervisor's own absolute resolution of `dotnet`, so the API child never
//                                    needs PATH to find it (C10).
// Fixed root layout (supervisor creates all of these 0700 before spawn — C9 §1):
//   <root>/control.sock  — this host connects to it as a client (supervisor listens/accepts).
//   <root>/api.sock       — Kestrel listens here for the real Auth API (R2 Tercih A).
//   <root>/mongo/         — EphemeralMongo's data directory (D5 — PID is read from mongod.lock here, not lsof).
//   <root>/home/          — HOME for the API child (C10 — the developer's real HOME never reaches it).
//   <root>/tmp/           — TMPDIR for the API child.
//   <root>/content/       — API child's --contentRoot AND working directory; left EMPTY (C10 — appsettings.json,
//                            appsettings.Development.json and their bin-output copies are never read this way).
//                            G1 (Aşama G): also the in-process prep host's content root; the fixture refuses it
//                            if it has any entry (mongo-start-failed / exit 2).
//
// Sequence: hello -> hello-ack -> [seed via AuthTestHost, reused per R6, NOT copied] -> launch real Api process
// (dotnet <dll>, no `dotnet run`, no intermediate process) -> health-poll -> ONE real login round trip BEFORE
// declaring ready (P7 — proves the whole Mongo+DB path end to end, not just Kestrel liveness) -> ready ->
// seed-ready -> wait shutdown -> graceful kill (D3: SIGTERM, 5s, SIGKILL) -> dispose mongod -> bye -> exit.
//
// Honest accounting of what this pass does NOT fully cover (see the delivery report): C7's full 13-item negative
// test list is exercised only partially by the supervisor-side driver (a throwaway scratchpad script, not this
// file); POSIX_SPAWN_CLOEXEC_DEFAULT is not set for the API/mongod children (P4 — measured mechanism documented
// in the protocol text instead, evidence deferred to the real 7-state run).

using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.AuthService.Application.Tests.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// WP-INFRA-AUTH-ACCEPTANCE-HOST-01 (T2) — lets the committable test project unit-test ControlChannel's strict
// C7 parsing directly (both ends of a real Unix socket, in-process, no real host process spawn needed for the
// 13 wire-protocol negative tests) via the test-only seam below. Never widens any OTHER internal member's
// visibility beyond what these tests exercise.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Diten.AuthService.AccountKindAcceptanceHost.Tests")]

namespace Diten.AuthService.AccountKindAcceptanceHost;

// P3 — fixed host exit codes.
internal static class ExitCodes
{
    public const int Success = 0;                  // normal: shutdown -> bye
    public const int ProtocolViolation = 1;         // protocol-violation or unsupported-version
    public const int MongoStartFailed = 2;
    public const int ApiStartFailed = 3;            // api-start-failed or api-bind-failed
    public const int SeedFailed = 4;
    public const int Unexpected = 5;
    public const int SupervisorLost = 6;             // EOF before shutdown
    public const int Timeout = 7;                    // host-side timeout waiting for a message
}

// P2 — fixed, English, code-keyed error text. NEVER exception text, input fragments, paths, PIDs, or secrets.
internal static class ProtocolErrors
{
    public static readonly IReadOnlyDictionary<string, string> Text = new Dictionary<string, string>
    {
        ["unsupported-version"] = "Protocol version not supported.",
        ["peer-verification-failed"] = "Peer verification failed.",
        ["mongo-start-failed"] = "Disposable MongoDB could not be started.",
        ["api-start-failed"] = "Auth API process could not be started.",
        ["api-bind-failed"] = "Auth API did not bind its socket.",
        ["seed-failed"] = "Seeding the disposable tenant failed.",
        ["timeout"] = "A protocol step timed out.",
        ["protocol-violation"] = "Protocol violation.",
    };
}

internal static class Program
{
    private const string ProtocolVersion = "1.2";

    /// <summary>
    /// G2(b)(ii) (Aşama G) — the ONLY process environment variables this host keeps. Everything else is removed at
    /// the very start of Main, before anything reads configuration: the in-process prep host's
    /// <c>WebApplication.CreateBuilder</c> adds the WHOLE process environment as a configuration source in its eager
    /// window, before any fixture callback can clear sources. Reason per entry:
    ///   DITEN_ACCEPTANCE_ROOT / _RUN_ID / _DOTNET_PATH — the supervisor contract (read below in Main).
    ///   PATH   — ResolveLocalMongoBinaryDirectory (fixture) searches it for mongod; the ps probe (ReadProcessComm)
    ///            is started by name.
    ///   HOME   — the supervisor-owned <root>/home (every spawned test passes it). Removing it would not remove a
    ///            home directory: .NET's special-folder lookups fall back to the passwd entry, i.e. the developer's
    ///            real home.
    ///   TMPDIR — keeps Path.GetTempPath() on the supervisor's choice when it sets one (unset -> /tmp).
    ///   DITEN_ACCEPTANCE_TESTONLY_CORRUPT_PERMISSION_KEY — the Ek-D test-only seam, read in Main AFTER this
    ///            reduction (the seed step below).
    /// Nothing else was needed: every spawned-host test reaches its expected state with exactly this set.
    /// Limit (known .NET-on-Unix behaviour, not separately measured here): Environment.SetEnvironmentVariable edits
    /// the managed environment block that configuration sources, Environment.GetEnvironmentVariable and Process.Start
    /// read; native getenv() callers and runtime switches consumed before Main are not affected.
    /// </summary>
    internal static readonly IReadOnlySet<string> HostEnvironmentAllowList = new HashSet<string>(StringComparer.Ordinal)
    {
        "DITEN_ACCEPTANCE_ROOT",
        "DITEN_ACCEPTANCE_RUN_ID",
        "DITEN_ACCEPTANCE_DOTNET_PATH",
        "PATH",
        "HOME",
        "TMPDIR",
        "DITEN_ACCEPTANCE_TESTONLY_CORRUPT_PERMISSION_KEY",
    };

    /// <summary>G2(b)(ii) — pure: the subset of <paramref name="environment"/> whose key is on the allow-list
    /// (ordinal, case-sensitive, as Unix environment names are). Never mutates anything.</summary>
    internal static Dictionary<string, string?> SelectAllowListedEnvironment(IReadOnlyDictionary<string, string?> environment) =>
        environment
            .Where(pair => HostEnvironmentAllowList.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    /// <summary>G2(b)(ii) — applies <see cref="SelectAllowListedEnvironment"/> to this process's own environment and
    /// re-reads it; returns false (fail closed) if anything outside the allow-list is still visible.</summary>
    private static bool ReduceProcessEnvironmentToAllowList()
    {
        var current = ReadProcessEnvironment();
        var kept = SelectAllowListedEnvironment(current);
        foreach (var key in current.Keys.Where(key => !kept.ContainsKey(key)))
        {
            Environment.SetEnvironmentVariable(key, null);
        }

        return ReadProcessEnvironment().Keys.All(HostEnvironmentAllowList.Contains);

        static Dictionary<string, string?> ReadProcessEnvironment()
        {
            var snapshot = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                snapshot[(string)entry.Key] = entry.Value as string;
            }

            return snapshot;
        }
    }

    private static async Task<int> Main(string[] args)
    {
        // G3(i) (Aşama G) — measured: this host never writes to stdout itself (every DIAG line is Console.Error,
        // the protocol is the control socket, the API/mongod children's output is redirected into pipes this
        // process owns). Stdout is therefore closed off FIRST, so the in-process prep host's Serilog console sink,
        // Persistence's Console.WriteLine(ex.Message) and the fixture's own Console.WriteLine lines (connection
        // string, mongod binary directory) have nowhere to go.
        Console.SetOut(TextWriter.Null);

        // G2(b)(ii) (Aşama G) — before anything reads configuration or the environment.
        if (!ReduceProcessEnvironmentToAllowList())
        {
            await Console.Error.WriteLineAsync("[host] DIAG environment-allow-list-failed");
            return ExitCodes.Unexpected;
        }

        // Measured (Stage 2 / C4 report): WebApplicationFactory<Program>'s content-root guess (inside
        // AccountKindAcceptance.AuthTestHost, reused per R6) is sensitive to Directory.GetCurrentDirectory() at
        // the moment the in-process seed host is built. A supervisor can launch this process from ANY working
        // directory; AppContext.BaseDirectory (this assembly's own bin output, deterministic regardless of
        // caller) is pinned as CWD before anything else touches WebApplicationFactory.
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);

        var root = Environment.GetEnvironmentVariable("DITEN_ACCEPTANCE_ROOT");
        var runId = Environment.GetEnvironmentVariable("DITEN_ACCEPTANCE_RUN_ID");
        var dotnetPath = Environment.GetEnvironmentVariable("DITEN_ACCEPTANCE_DOTNET_PATH");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(runId) || string.IsNullOrWhiteSpace(dotnetPath))
        {
            await Console.Error.WriteLineAsync(
                "DITEN_ACCEPTANCE_ROOT, DITEN_ACCEPTANCE_RUN_ID and DITEN_ACCEPTANCE_DOTNET_PATH are required (protocol v1.2).");
            return ExitCodes.Unexpected;
        }

        var controlSockPath = Path.Combine(root, "control.sock");
        var apiSockPath = Path.Combine(root, "api.sock");
        var mongoDataDir = Path.Combine(root, "mongo");
        var homeDir = Path.Combine(root, "home");
        var tmpDir = Path.Combine(root, "tmp");
        var contentDir = Path.Combine(root, "content");

        await using var channel = await ControlChannel.ConnectAsync(controlSockPath, runId);
        AccountKindAcceptance.AuthTestHost? seedHost = null;
        Process? apiProcess = null;
        PlatformLoginSettingsStub? platformStub = null;

        try
        {
            await channel.SendAsync("hello", new { protocolVersion = ProtocolVersion, hostPid = Environment.ProcessId });
            ControlMessage ack;
            try
            {
                await Console.Error.WriteLineAsync($"[host] DIAG {DateTime.UtcNow:O} waiting for hello-ack (10s)...");
                ack = await channel.ReceiveAsync(TimeSpan.FromSeconds(10), "hello-ack");
            }
            catch (TimeoutException)
            {
                await Console.Error.WriteLineAsync($"[host] DIAG {DateTime.UtcNow:O} hello-ack TIMED OUT, returning exit 7");
                return ExitCodes.Timeout;
            }

            if (ack.Type == "error")
            {
                return ExitCodes.ProtocolViolation; // supervisor already refused (e.g. unsupported-version)
            }

            if (ack.Type != "hello-ack" || ack.ProtocolVersion != ProtocolVersion)
            {
                // F2 — CT correction 2026-09-16: the prompt asked for exit 5, which contradicts this host's own
                // P3 table ("1 — protocol-violation or unsupported-version") that PPM/Codex already accepted. A
                // delivered protocol is not re-numbered by a later instruction, so the table wins and the CT
                // prompt was wrong. The deviation the previous round reported is resolved here, not shipped.
                await channel.SendErrorAsync("unsupported-version");
                return ExitCodes.ProtocolViolation;
            }

            // ── seed (R6, reused not copied) — mongod's data directory is the supervisor-owned <root>/mongo (D5) ──
            // T11(a) — mongo-start-failed (mongod/pre-flight/host-build) and seed-failed (fixture SeedAsync,
            // including its read-back detection of a silently-swallowed production DataSeeder error) are now
            // DISTINCT: AccountKindSeedFailedException is the only path that reports seed-failed here; anything
            // else at this stage (mongod never came up, wrong target, host failed to build) is mongo-start-failed.
            //
            // F1 — TEST-ONLY seam (protocol v1.2 Ek-D): DITEN_ACCEPTANCE_TESTONLY_CORRUPT_PERMISSION_KEY. When
            // this env var is UNSET (every normal/production run, always), corruptPermissionKey is null and
            // BeforeSeedHookForTesting below is null — IDENTICAL to no seam existing at all. When a test sets it
            // to a permission key, the fixture's own repository deletes that key right after the production
            // DataSeeder ran but BEFORE the fixture's read-back check — reproducing exactly what a silently-
            // swallowed production seeding error would leave behind, WITHOUT touching production code or the
            // filesystem. This is what closes the gap a sabotage test found: no existing test drove a genuine
            // seed-failure through the FULL wire protocol (only the in-process fixture was tested directly).
            var corruptPermissionKey = Environment.GetEnvironmentVariable("DITEN_ACCEPTANCE_TESTONLY_CORRUPT_PERMISSION_KEY");
            try
            {
                // G1 (Aşama G) — contentDir is the supervisor-created, still-empty <root>/content (the API child's
                // content root too, launched later); the fixture refuses it if it has any entry (-> mongo-start-failed).
                seedHost = await AccountKindAcceptance.AuthTestHost.StartWithMongoDataDirectoryAsync(
                    mongoDataDir,
                    contentDir,
                    beforeSeedHookForTesting: string.IsNullOrEmpty(corruptPermissionKey)
                        ? null
                        : async host =>
                        {
                            using var scope = host.Factory.Services.CreateScope();
                            var permissions = scope.ServiceProvider.GetRequiredService<Diten.AuthService.Application.Common.Interfaces.IPermissionRepository>();
                            var permission = await permissions.GetByKeyAsync(corruptPermissionKey, CancellationToken.None);
                            if (permission is not null)
                            {
                                await permissions.DeleteAsync(permission.Id, CancellationToken.None);
                            }
                        });
            }
            catch (AccountKindSeedFailedException seedDiagEx)
            {
                await Console.Error.WriteLineAsync(DiagLine("seed-failed", seedDiagEx));
                await channel.SendErrorAsync("seed-failed");
                return ExitCodes.SeedFailed;
            }
            catch (Exception diagEx)
            {
                await Console.Error.WriteLineAsync(DiagLine("mongo-start-failed", diagEx));
                await channel.SendErrorAsync("mongo-start-failed");
                return ExitCodes.MongoStartFailed;
            }

            // C8's subjects list needs 4 more display-label subjects, seeded via the SAME in-process factory —
            // must happen BEFORE the factory is disposed below. T11(a) — its failure is ALSO seed-failed, not the
            // generic top-level protocol-violation it used to fall through to.
            AccountKindAcceptance.DisplayLabelSubjects displayLabelSubjects;
            try
            {
                displayLabelSubjects = await AccountKindAcceptance.SeedDisplayLabelSubjectsAsync(seedHost);
            }
            catch (Exception displayLabelEx)
            {
                await Console.Error.WriteLineAsync(DiagLine("seed-failed-display-label", displayLabelEx));
                await channel.SendErrorAsync("seed-failed");
                return ExitCodes.SeedFailed;
            }

            await seedHost.DisposeFactoryOnlyAsync();

            // D5 — mongod PID from mongod.lock (measured: plain-text PID + newline, verified against a real
            // mongod on this machine), NOT a port/lsof lookup. Parent must be THIS process; executable name must
            // be "mongod".
            int mongodPid;
            try
            {
                mongodPid = ReadMongodPidFromLockFile(mongoDataDir);
                var mongodPpid = ProcessTiming.GetParentPid(mongodPid);
                var mongodComm = ReadProcessComm(mongodPid);
                if (mongodPpid != Environment.ProcessId || mongodComm != "mongod")
                {
                    throw new InvalidOperationException(
                        $"mongod.lock PID failed parent/name verification: pid={mongodPid} ppid={mongodPpid} (expected {Environment.ProcessId}) comm='{mongodComm}'");
                }
            }
            catch (Exception diagEx)
            {
                await Console.Error.WriteLineAsync(DiagLine("mongod-pid-verification-failed", diagEx));
                await channel.SendErrorAsync("mongo-start-failed");
                return ExitCodes.MongoStartFailed;
            }

            var mongodStartTime = ProcessTiming.GetStartTimeUnixMs(mongodPid);

            // F8 — generated here (not after StartAsync, as before) so the stub can be given the SAME key it
            // must require the real client to present — the header check below is only meaningful if the stub
            // knows the expected value before it starts accepting requests.
            var apiDllPath = ResolveApiDllPath();
            var internalEventApiKey = GenerateDisposableKey();
            var platformInternalApiKey = GenerateDisposableKey();

            // P1 — the login-settings stub replaces the old bind/close/rebind race: a real in-process Kestrel
            // bound directly to 127.0.0.1:0, with the actually-bound address read AFTER start.
            platformStub = await PlatformLoginSettingsStub.StartAsync(platformInternalApiKey);

            var psi = new ProcessStartInfo
            {
                FileName = dotnetPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = contentDir // C10 — empty; never the API's own bin directory
            };
            psi.ArgumentList.Add(apiDllPath);
            psi.ArgumentList.Add("--contentRoot");
            psi.ArgumentList.Add(contentDir);

            // C1/C10 — CLEANED environment: Clear() then an explicit allow-list only.
            psi.Environment.Clear();
            psi.Environment["PATH"] = "/usr/bin:/bin";
            psi.Environment["HOME"] = homeDir;
            psi.Environment["TMPDIR"] = tmpDir;
            psi.Environment["DOTNET_ROOT"] = Path.GetDirectoryName(dotnetPath)!;
            psi.Environment["ASPNETCORE_ENVIRONMENT"] = "AcceptanceHost"; // C3/C10 — not "Development"
            psi.Environment["ASPNETCORE_URLS"] = $"http://unix:{apiSockPath}";
            psi.Environment["MongoDbSettings__ConnectionString"] = seedHost.ConnectionString;
            psi.Environment["MongoDbSettings__DatabaseName"] = AccountKindAcceptance.DatabaseName;
            psi.Environment["JwtSettings__Secret"] = seedHost.GeneratedJwtSecretForLeakGuardOnly
                ?? throw new InvalidOperationException("seed host did not generate a JWT secret");
            // The isolated API must mint and validate the same run-owned JWT profile.
            psi.Environment["JwtSettings__Issuer"] = $"urn:diten:acceptance:issuer:{runId}";
            psi.Environment["JwtSettings__Audience"] = $"urn:diten:acceptance:audience:{runId}";
            psi.Environment["Eventing__Transport"] = "InMemory";
            psi.Environment["Smtp__Enabled"] = "false";
            psi.Environment["TenantResolution__DevBypassEnabled"] = "false";
            psi.Environment["Observability__Metrics__Enabled"] = "false";
            psi.Environment["Observability__Seq__Enabled"] = "false";
            psi.Environment["OTEL_SDK_DISABLED"] = "true";
            psi.Environment["InternalEventAuth__ApiKey"] = internalEventApiKey;
            psi.Environment["PlatformService__InternalApiKey"] = platformInternalApiKey;
            psi.Environment["PlatformService__BaseUrl"] = platformStub.BaseUrl;

            apiProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var stdout = new List<string>();
            var stderr = new List<string>();
            apiProcess.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.Add(e.Data); };
            apiProcess.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.Add(e.Data); };

            // T6 — Process.Start THROWS (Win32Exception) when FileName does not resolve to an existing
            // executable; it does not just return false. A bad DITEN_ACCEPTANCE_DOTNET_PATH must map to
            // api-start-failed/exit 3, not fall through to the generic top-level catch (protocol-violation/5).
            bool started;
            try
            {
                started = apiProcess.Start();
            }
            catch (Exception startEx)
            {
                await Console.Error.WriteLineAsync(DiagLine("api-start-threw", startEx));
                apiProcess = null; // nothing to kill in finally — Start() never produced a live process
                await channel.SendErrorAsync("api-start-failed");
                return ExitCodes.ApiStartFailed;
            }

            if (!started)
            {
                apiProcess = null;
                await channel.SendErrorAsync("api-start-failed");
                return ExitCodes.ApiStartFailed;
            }

            apiProcess.BeginOutputReadLine();
            apiProcess.BeginErrorReadLine();

            var apiPid = apiProcess.Id;
            var apiStartTime = ProcessTiming.GetStartTimeUnixMs(apiPid);

            var healthy = await WaitForHealthyAsync(apiSockPath, apiProcess, TimeSpan.FromSeconds(30));
            if (!healthy)
            {
                await channel.SendErrorAsync("api-bind-failed");
                return ExitCodes.ApiStartFailed;
            }

            var seed = seedHost.Seeded;

            // P7 — ONE real login round trip through api.sock BEFORE declaring ready: proves the FULL path
            // (Kestrel -> TenantResolution -> LoginCommandHandler -> ITenantLoginSettingsClient -> the stub ->
            // Mongo user lookup -> password verify -> JWT mint), not just /health/live's liveness. noPermission
            // is used deliberately: it has no grants, so a genuine 401/403 on a LATER permission check would be
            // expected, but LOGIN ITSELF must still succeed (login needs no permission) — a real, measurable
            // pass/fail signal that Mongo and the login-settings stub are actually reachable from THIS process.
            var preReadyLoginOk = await TryLoginAsync(apiSockPath, apiPid, apiStartTime, seed.TenantId, seed.NoPermission.Email, seedHost.ActorPassword);
            if (!preReadyLoginOk)
            {
                await channel.SendErrorAsync("api-bind-failed");
                return ExitCodes.ApiStartFailed;
            }

            // T12 — machine-readable labels so nothing about trust boundaries is left to a code comment only:
            //   loginSettings: this run's ITenantLoginSettingsClient target is the in-process stub (D6), not
            //     Platform — "provesPlatformIntegration": false is the honest claim; a consumer must not read
            //     `ready` and assume Platform's own login-settings contract was exercised.
            //   authLoginProof: "real" — P7's login DID go through the genuine Auth pipeline (Kestrel ->
            //     TenantResolution -> LoginCommandHandler -> Mongo -> JWT mint) over api.sock; this is the
            //     part that IS proven, kept in its own field so it is never confused with the stub label above.
            // F4 — a supervisor that closes the channel DURING seed/launch (before this point) is only ever
            // observed by the host at its NEXT socket operation — this send. A broken pipe here (NetworkStream
            // wraps the underlying SocketException as IOException) is supervisor-lost/exit 6, exactly as if the
            // same EOF had been seen on a read — NOT the generic top-level catch (which would report exit 5).
            try
            {
                await channel.SendAsync("ready", new
                {
                    apiPid,
                    apiStartTime,
                    mongodPid,
                    mongodStartTime,
                    endpoint = new { kind = "unix", socket = "api.sock" },
                    loginSettings = new
                    {
                        source = "stub",
                        boundaryEndpoint = "/api/internal/tenants/{tenantId}/login-settings",
                        provesPlatformIntegration = false
                    },
                    authLoginProof = "real"
                });

                // ── C8 seed-ready shape: actors (4, with password) + subjects (10, userId only) + foreignTenantId ──
                await channel.SendAsync("seed-ready", new
                {
                    tenantId = seed.TenantId,
                    foreignTenantId = seed.ForeignTenantId,
                    actors = new Dictionary<string, object>
                    {
                        ["kindAdmin"] = new { userId = seed.KindAdmin.Id, email = seed.KindAdmin.Email, password = seedHost.ActorPassword },
                        ["pmo"] = new { userId = seed.Pmo.Id, email = seed.Pmo.Email, password = seedHost.ActorPassword },
                        ["creator"] = new { userId = seed.Creator.Id, email = seed.Creator.Email, password = seedHost.ActorPassword },
                        ["noPermission"] = new { userId = seed.NoPermission.Id, email = seed.NoPermission.Email, password = seedHost.ActorPassword },
                    },
                    subjects = new Dictionary<string, object>
                    {
                        ["human"] = new { userId = seed.Human.Id },
                        ["unknown"] = new { userId = seed.Unknown.Id },
                        ["service"] = new { userId = seed.Service.Id },
                        ["passive"] = new { userId = seed.Passive.Id },
                        ["foreign"] = new { userId = seed.Foreign.Id },
                        ["mutable"] = new { userId = seed.Mutable.Id },
                        ["unnamed"] = new { userId = displayLabelSubjects.Unnamed.Id },
                        ["whitespace"] = new { userId = displayLabelSubjects.Whitespace.Id },
                        ["emailUserName"] = new { userId = displayLabelSubjects.EmailUserName.Id },
                        ["longName"] = new { userId = displayLabelSubjects.LongName.Id },
                    }
                });
            }
            catch (IOException)
            {
                return ExitCodes.SupervisorLost;
            }

            // ── wait for shutdown; EOF before it => supervisor-lost (P3, exit 6); a timeout => exit 7 ────────
            ControlMessage shutdown;
            try
            {
                shutdown = await channel.ReceiveAsync(TimeSpan.FromMinutes(5), "shutdown");
            }
            catch (IOException)
            {
                return ExitCodes.SupervisorLost;
            }
            catch (TimeoutException)
            {
                return ExitCodes.Timeout;
            }

            if (shutdown.Type != "shutdown")
            {
                await channel.SendErrorAsync("protocol-violation");
                return ExitCodes.ProtocolViolation;
            }

            return ExitCodes.Success;
        }
        // F7 — a framing/parsing/state-machine violation caught HERE (i.e. not already handled by a closer,
        // more specific catch further up, such as the hello-ack/shutdown waits' own TimeoutException/IOException
        // handling) must still map to exit 1, not fall through to the generic catch below and report 5. This is
        // the SAME distinction the type ProtocolFramingViolationException exists to make: caught by its own
        // type, never by re-parsing the exception's message string.
        catch (ProtocolFramingViolationException framingEx)
        {
            await Console.Error.WriteLineAsync(DiagLine("protocol-violation-framing", framingEx));
            try { await channel.SendErrorAsync("protocol-violation"); } catch { /* channel already gone */ }
            return ExitCodes.ProtocolViolation;
        }
        catch (Exception diagEx)
        {
            await Console.Error.WriteLineAsync(DiagLine("top-level-unexpected", diagEx));
            try { await channel.SendErrorAsync("protocol-violation"); } catch { /* channel already gone */ }
            return ExitCodes.Unexpected;
        }
        finally
        {
            await Console.Error.WriteLineAsync($"[host] DIAG {DateTime.UtcNow:O} entering finally block");
            // ── cleanup — every path (D3: graceful SIGTERM -> 5s -> SIGKILL as last resort) ─────────────────
            if (apiProcess is not null)
            {
                await GracefulKillAsync(apiProcess);
            }

            platformStub?.Dispose();

            if (seedHost is not null)
            {
                await seedHost.DisposeAsync(); // factory already null — this stops mongod + drops the DB
            }

            await Console.Error.WriteLineAsync($"[host] DIAG {DateTime.UtcNow:O} about to send bye");
            try
            {
                await channel.SendAsync("bye", new { });
                await Console.Error.WriteLineAsync($"[host] DIAG {DateTime.UtcNow:O} bye sent");
            }
            catch
            {
                // best effort
            }
        }
    }

    // D3 — libc kill(pid, SIGTERM), 5s grace, then SIGKILL as the LAST resort (never Process.Kill first).
    private static async Task GracefulKillAsync(Process process)
    {
        if (process.HasExited) return;

        var pid = process.Id;
        try
        {
            Kill(pid, 15); // SIGTERM
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (DateTime.UtcNow < deadline && !process.HasExited)
            {
                await Task.Delay(100);
            }

            if (!process.HasExited)
            {
                Kill(pid, 9); // SIGKILL — last resort
                await process.WaitForExitAsync(new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
            }
        }
        catch
        {
            // best effort
        }

        [DllImport("libc", SetLastError = true)]
        static extern int kill(int pid, int sig);
        static void Kill(int pid, int sig) => kill(pid, sig);
    }

    private static int ReadMongodPidFromLockFile(string mongoDataDir)
    {
        // Measured: mongod holds mongod.lock with an exclusive advisory flock() while running (verified against
        // a real 7.0.28 mongod on this machine — file contains the PID as plain decimal text + trailing
        // newline). .NET's FileStream on Unix ALSO takes its own advisory lock to emulate Windows FileShare
        // semantics (even FileAccess.Read + FileShare.ReadWrite still trips on mongod's exclusive flock) — a
        // plain POSIX open()/read()/close() (no flock involved) reads it without contention.
        var lockPath = Path.Combine(mongoDataDir, "mongod.lock");
        const int O_RDONLY = 0;
        var fd = open(lockPath, O_RDONLY);
        if (fd < 0) throw new IOException($"open({lockPath}) failed, errno={Marshal.GetLastWin32Error()}");
        try
        {
            var buffer = new byte[64];
            var n = read(fd, buffer, (nint)buffer.Length);
            if (n < 0) throw new IOException($"read({lockPath}) failed, errno={Marshal.GetLastWin32Error()}");
            return int.Parse(Encoding.ASCII.GetString(buffer, 0, (int)n).Trim());
        }
        finally
        {
            close(fd);
        }

        [DllImport("libc", SetLastError = true, EntryPoint = "open")]
        static extern int open(string path, int flags);
        [DllImport("libc", SetLastError = true)]
        static extern nint read(int fd, byte[] buf, nint count);
        [DllImport("libc", SetLastError = true)]
        static extern int close(int fd);
    }

    private static string ReadProcessComm(int pid)
    {
        var psi = new ProcessStartInfo("ps", $"-o comm= -p {pid}") { RedirectStandardOutput = true, UseShellExecute = false };
        using var p = Process.Start(psi)!;
        var output = p.StandardOutput.ReadToEnd().Trim();
        p.WaitForExit(3000);
        return Path.GetFileName(output);
    }

    /// <summary>
    /// F14 — the ONLY thing a DIAG line may carry about an exception: a fixed diagnostic code (never
    /// caller-supplied, never exception-derived) plus the exception's own TYPE name. Never `.Message`, never
    /// `.ToString()`, never an inner exception — any of those could carry a canary from test-only injected
    /// failures (or, in a real run, a connection string, a path, or other operational detail) straight into
    /// stderr/stdout, which is exactly the leak this closes.
    /// </summary>
    private static string DiagLine(string diagnosticCode, Exception ex) =>
        $"[host] DIAG {diagnosticCode} ({ex.GetType().Name})";

    private static string GenerateDisposableKey()
    {
        Span<byte> bytes = stackalloc byte[24];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes);
    }

    private static string ResolveApiDllPath()
    {
        var candidate = Path.Combine(AppContext.BaseDirectory, "Diten.AuthService.Api.dll");
        if (File.Exists(candidate)) return candidate;
        throw new FileNotFoundException("Diten.AuthService.Api.dll was not found next to this host's own build output.", candidate);
    }

    private static async Task<bool> WaitForHealthyAsync(string socketPath, Process apiProcess, TimeSpan timeout)
    {
        using var handler = UnixSocketHandler(socketPath);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/"), Timeout = TimeSpan.FromSeconds(3) };

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (apiProcess.HasExited) return false;
            if (File.Exists(socketPath))
            {
                try
                {
                    var response = await client.GetAsync("health/live");
                    if (response.IsSuccessStatusCode) return true;
                }
                catch { /* not ready yet */ }
            }

            await Task.Delay(250);
        }

        return false;
    }

    // F12 — `internal` (not `private`) and taking the expected peer identity explicitly so
    // LoginPeerVerificationTests can drive this exact code path against a listener it controls, with
    // deliberately-wrong expected values, and observe that the listener receives ZERO bytes (not just no body —
    // ConnectCallback throwing means the HTTP layer never even writes the request line).
    internal static async Task<bool> TryLoginAsync(
        string apiSockPath, int expectedPeerPid, long expectedPeerStartTime, Guid tenantId, string email, string password)
    {
        using var handler = UnixSocketHandlerWithPeerVerification(apiSockPath, expectedPeerPid, expectedPeerStartTime);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/"), Timeout = TimeSpan.FromSeconds(10) };

        var body = JsonSerializer.Serialize(new { email, password, rememberMe = false });
        using var req = new HttpRequestMessage(HttpMethod.Post, "api/tenant-auth/login")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString("D"));

        try
        {
            using var resp = await client.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return false;
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            if (!root.TryGetProperty("isSuccessful", out var isSuccessful) || !isSuccessful.GetBoolean()) return false;
            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object) return false;
            return data.TryGetProperty("accessToken", out var token) && token.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(token.GetString());
        }
        catch
        {
            return false;
        }
    }

    private static SocketsHttpHandler UnixSocketHandler(string socketPath) => new()
    {
        ConnectCallback = async (_, ct) =>
        {
            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), ct);
            return new NetworkStream(socket, ownsSocket: true);
        }
    };

    // F12 — used ONLY by TryLoginAsync (the call that carries the actor's password). AllowAutoRedirect=false so a
    // 3xx response can never cause the credential-bearing request to be re-sent to a redirect target; the
    // ConnectCallback verifies the peer BEFORE returning the stream the HTTP layer will write the request onto,
    // so a mismatch means literally zero bytes (not even the request line) ever reach the wrong peer.
    private static SocketsHttpHandler UnixSocketHandlerWithPeerVerification(string socketPath, int expectedPid, long expectedStartTime) => new()
    {
        AllowAutoRedirect = false,
        ConnectCallback = async (_, ct) =>
        {
            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            try
            {
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), ct);
                VerifyPeerIsExpectedApiProcess(socket, expectedPid, expectedStartTime);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                // G6(a) (Aşama G) — a failed connect or a failed peer check closes the socket here, at once: the
                // peer sees EOF with zero bytes, and no descriptor is left for the GC to find.
                socket.Dispose();
                throw;
            }
        }
    };

    /// <summary>
    /// F12 — kernel-verified peer identity on the connected Unix domain socket (macOS LOCAL_PEERPID/LOCAL_PEERCRED,
    /// SOL_LOCAL=0 — measured against this machine's own sys/un.h and sys/ucred.h): the socket's OTHER end really
    /// is the process this host itself spawned (PID match), that process has not since exited and been replaced by
    /// a same-PID impostor (start-time match, same technique as D5/D2 elsewhere in this file), and it runs as this
    /// same OS user (UID match). Throws (never returns a bool) so the caller's ConnectCallback never hands the
    /// HTTP layer a stream to a peer that failed verification.
    /// </summary>
    internal static void VerifyPeerIsExpectedApiProcess(Socket socket, int expectedPid, long expectedStartTime)
    {
        // G6(b) (Aşama G) — the option numbers, struct layout and kinfo_proc offsets below are macOS-only. Any other
        // OS is refused explicitly (fail closed), not by whatever getsockopt happens to return there.
        if (!OperatingSystem.IsMacOS())
        {
            throw new IOException("peer verification failed: unsupported operating system.");
        }

        var fd = (int)socket.SafeHandle.DangerousGetHandle();

        var peerPid = GetLocalPeerPid(fd);
        if (peerPid != expectedPid)
        {
            throw new IOException($"peer verification failed: connected peer pid {peerPid} does not match the expected api process pid {expectedPid}.");
        }

        var peerStartTime = ProcessTiming.GetStartTimeUnixMs(peerPid);
        if (peerStartTime != expectedStartTime)
        {
            throw new IOException($"peer verification failed: pid {peerPid} start time does not match — a same-PID process, not the expected one.");
        }

        var peerUid = GetLocalPeerUid(fd);
        var ownUid = GetOwnUid();
        if (peerUid != ownUid)
        {
            throw new IOException("peer verification failed: connected peer does not run as this process's own user.");
        }

        // Measured against this machine's SDK (/Library/Developer/CommandLineTools/SDKs/MacOSX.sdk):
        // sys/un.h SOL_LOCAL 0, LOCAL_PEERCRED 0x001, LOCAL_PEERPID 0x002; sys/ucred.h struct xucred
        // { u_int cr_version; uid_t cr_uid; short cr_ngroups; gid_t cr_groups[NGROUPS]; }, XUCRED_VERSION 0.
        const int SOL_LOCAL = 0;
        const int LOCAL_PEERPID = 0x002;
        const int LOCAL_PEERCRED = 0x001;
        const uint XUCRED_VERSION = 0;
        const int CrUidOffset = 4;

        static int GetLocalPeerPid(int fd)
        {
            var buf = new byte[4];
            var len = (uint)buf.Length;
            if (getsockopt(fd, SOL_LOCAL, LOCAL_PEERPID, buf, ref len) != 0)
            {
                throw new IOException("peer verification failed: getsockopt(LOCAL_PEERPID) failed.");
            }

            // G6(c) — pid_t is exactly 4 bytes; any other returned length means the value is not a pid.
            if (len != sizeof(int))
            {
                throw new IOException("peer verification failed: getsockopt(LOCAL_PEERPID) returned an unexpected length.");
            }

            return BitConverter.ToInt32(buf, 0);
        }

        static uint GetLocalPeerUid(int fd)
        {
            var buf = new byte[128]; // struct xucred: cr_version(4) + cr_uid(4) + cr_ngroups(2, padded) + groups
            var len = (uint)buf.Length;
            if (getsockopt(fd, SOL_LOCAL, LOCAL_PEERCRED, buf, ref len) != 0)
            {
                throw new IOException("peer verification failed: getsockopt(LOCAL_PEERCRED) failed.");
            }

            // G6(c) — the kernel must have filled at least cr_version and cr_uid, in the layout this code reads.
            if (len < CrUidOffset + sizeof(uint))
            {
                throw new IOException("peer verification failed: getsockopt(LOCAL_PEERCRED) returned too few bytes.");
            }

            if (BitConverter.ToUInt32(buf, 0) != XUCRED_VERSION)
            {
                throw new IOException("peer verification failed: unexpected xucred layout version.");
            }

            return BitConverter.ToUInt32(buf, CrUidOffset);
        }

        static uint GetOwnUid() => getuid();

        [DllImport("libc", SetLastError = true)]
        static extern int getsockopt(int socket, int level, int option_name, byte[] option_value, ref uint option_len);
        [DllImport("libc", SetLastError = true)]
        static extern uint getuid();
    }
}

/// <summary>C7 — a message parsed off the control channel. Fields are validated per-type by the caller.</summary>
internal sealed record ControlMessage(string Type, string RunId, string? ProtocolVersion, JsonElement Root);

/// <summary>
/// F7 — thrown ONLY for a genuine C7 wire-level framing/parsing/state-machine violation: the 65536-byte cap, the
/// inline 5s line timeout, BOM, CR, invalid UTF-8, a duplicate JSON key at any depth, a missing/empty field,
/// runId mismatch, protocolVersion misplaced, or an unexpected message type per the `allowedTypes` state machine.
/// This is the type/message-string distinction Main uses to map these to exit code 1 (protocol-violation, per the
/// P3 table), keeping them separate from any other, genuinely unexpected failure (exit 5) — never a string match
/// on the exception's own message.
/// </summary>
internal sealed class ProtocolFramingViolationException : Exception
{
    public ProtocolFramingViolationException(string message) : base(message)
    {
    }
}

/// <summary>
/// C7 — the strict wire protocol: UTF-8 bytes + single LF, no BOM/CR, 65536-byte cap enforced WHILE reading
/// (never after an unbounded ReadLine), a 5s cap on completing a line once it starts, strict UTF-8 decoding, and
/// duplicate-JSON-key rejection at every depth (JsonDocument does not reject these — Utf8JsonReader is used to
/// scan for them explicitly before any value is trusted).
/// </summary>
internal sealed class ControlChannel : IAsyncDisposable
{
    private const int MaxLineBytes = 65536;
    private static readonly TimeSpan LineTimeout = TimeSpan.FromSeconds(5);

    private readonly Socket _socket;
    private readonly NetworkStream _stream;
    private readonly string _runId;

    private ControlChannel(Socket socket, string runId)
    {
        _socket = socket;
        _stream = new NetworkStream(socket, ownsSocket: false);
        _runId = runId;
    }

    public static async Task<ControlChannel> ConnectAsync(string channelPath, string runId)
    {
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(channelPath));
        return new ControlChannel(socket, runId);
    }

    /// <summary>
    /// WP-INFRA-AUTH-ACCEPTANCE-HOST-01 (T2) — TEST-ONLY seam. Wraps an ALREADY-CONNECTED socket (the "host"
    /// end of an in-process Unix socket pair a test sets up itself) so C7's strict parsing/framing can be
    /// unit-tested directly — a test writes malformed bytes on the OTHER end and asserts on what ReceiveAsync
    /// does — without spawning a real host OS process for every one of the 13 wire-protocol negative cases.
    /// Not part of the production entry point's own call graph (Main never calls this).
    /// </summary>
    internal static ControlChannel ForTestingFromConnectedSocket(Socket socket, string runId) => new(socket, runId);

    public async Task SendAsync(string type, object payload)
    {
        var merged = new Dictionary<string, object?> { ["type"] = type, ["runId"] = _runId };
        if (type is "hello") merged["protocolVersion"] = "1.2";
        foreach (var prop in payload.GetType().GetProperties()) merged[prop.Name] = prop.GetValue(payload);

        var json = JsonSerializer.Serialize(merged);
        var bytes = Encoding.UTF8.GetBytes(json + "\n");
        await _stream.WriteAsync(bytes);
        await _stream.FlushAsync();
    }

    public Task SendErrorAsync(string code) =>
        SendAsync("error", new { code, message = ProtocolErrors.Text[code] });

    public async Task<ControlMessage> ReceiveAsync(TimeSpan timeout) => await ReceiveAsync(timeout, allowedTypes: null);

    /// <summary>
    /// C7 durum makinesi — <paramref name="allowedTypes"/> verildiğinde, gelen mesajın <c>type</c>'ı bu kümede
    /// değilse "sıra dışı mesaj" protocol-violation olarak reddedilir (ör. hello-ack beklenirken bir başka tür
    /// gelmesi, shutdown beklenirken ikinci bir ready gelmesi). <c>error</c> her zaman örtük olarak izinlidir —
    /// R7/C7: "error her yönde her durumda gönderilebilir ve terminaldir".
    /// </summary>
    public async Task<ControlMessage> ReceiveAsync(TimeSpan timeout, params string[]? allowedTypes)
    {
        var readTask = ReadStrictLineAsync();
        var completed = await Task.WhenAny(readTask, Task.Delay(timeout));
        if (completed != readTask)
        {
            throw new TimeoutException($"No message received within {timeout}.");
        }

        var lineBytes = await readTask;
        var message = ParseAndValidate(lineBytes);

        if (allowedTypes is { Length: > 0 } && message.Type != "error" && !allowedTypes.Contains(message.Type))
        {
            // G4 (Aşama G) — fixed text: the sender-controlled type value is never part of the message.
            throw new ProtocolFramingViolationException(
                "protocol-violation: unexpected message type for the current protocol state.");
        }

        return message;
    }

    /// <summary>Byte-level strict reader: LF-delimited, size-capped WHILE reading, per-line timeout.</summary>
    private async Task<byte[]> ReadStrictLineAsync()
    {
        var buffer = new List<byte>(256);
        var single = new byte[1];
        DateTime? firstByteAt = null;

        while (true)
        {
            if (firstByteAt is { } started && DateTime.UtcNow - started > LineTimeout)
            {
                throw new ProtocolFramingViolationException("protocol-violation: line did not complete within 5s.");
            }

            var readTask = _stream.ReadAsync(single.AsMemory(0, 1));
            var timeLeft = firstByteAt is null ? Timeout.InfiniteTimeSpan : LineTimeout - (DateTime.UtcNow - firstByteAt.Value);
            var n = timeLeft == Timeout.InfiniteTimeSpan
                ? await readTask
                : await WithTimeout(readTask.AsTask(), timeLeft);

            if (n == 0)
            {
                throw new IOException("control channel closed (EOF) mid-line or before a message arrived");
            }

            firstByteAt ??= DateTime.UtcNow;

            if (single[0] == (byte)'\n')
            {
                return buffer.ToArray();
            }

            buffer.Add(single[0]);
            if (buffer.Count > MaxLineBytes)
            {
                throw new ProtocolFramingViolationException("protocol-violation: line exceeded 65536 bytes without LF.");
            }
        }

        static async Task<int> WithTimeout(Task<int> task, TimeSpan timeout)
        {
            if (timeout <= TimeSpan.Zero) throw new ProtocolFramingViolationException("protocol-violation: line did not complete within 5s.");
            var completed = await Task.WhenAny(task, Task.Delay(timeout));
            if (completed != task) throw new ProtocolFramingViolationException("protocol-violation: line did not complete within 5s.");
            return await task;
        }
    }

    private ControlMessage ParseAndValidate(byte[] lineBytes)
    {
        if (lineBytes.Length == 0)
        {
            throw new ProtocolFramingViolationException("protocol-violation: empty line.");
        }

        // No BOM: EF BB BF at the start is refused outright.
        if (lineBytes.Length >= 3 && lineBytes[0] == 0xEF && lineBytes[1] == 0xBB && lineBytes[2] == 0xBF)
        {
            throw new ProtocolFramingViolationException("protocol-violation: UTF-8 BOM present.");
        }

        // No CR anywhere (a CRLF pair would otherwise silently leave a trailing \r in the "line").
        if (Array.IndexOf(lineBytes, (byte)'\r') >= 0)
        {
            throw new ProtocolFramingViolationException("protocol-violation: CR present.");
        }

        // Strict UTF-8: throws on any invalid byte sequence instead of substituting U+FFFD.
        var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        string text;
        try
        {
            text = strictUtf8.GetString(lineBytes);
        }
        catch (DecoderFallbackException)
        {
            throw new ProtocolFramingViolationException("protocol-violation: invalid UTF-8 sequence.");
        }

        // CT (2026-09-16) — a syntactically broken line threw a raw JsonException and fell to the generic catch,
        // reporting exit 5 while every OTHER C7 violation reported 1. Both readers of the raw bytes are inside the
        // try: the duplicate-key walk reaches the malformed bytes FIRST (measured — classifying only the parse
        // below left the test red at exit 5), and its own duplicate-key refusal already throws the framing type,
        // so it passes through this catch untouched.
        JsonElement root;
        try
        {
            EnsureNoDuplicateKeys(lineBytes);
            using var doc = JsonDocument.Parse(text);
            root = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            throw new ProtocolFramingViolationException("protocol-violation: malformed JSON.");
        }
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new ProtocolFramingViolationException("protocol-violation: message is not a JSON object.");
        }

        var type = RequireString(root, "type");
        var runId = RequireString(root, "runId");
        if (!string.Equals(runId, _runId, StringComparison.Ordinal))
        {
            throw new ProtocolFramingViolationException("protocol-violation: runId mismatch.");
        }

        string? protocolVersion = null;
        if (root.TryGetProperty("protocolVersion", out var pv))
        {
            if (type != "hello" && type != "hello-ack")
            {
                throw new ProtocolFramingViolationException("protocol-violation: protocolVersion outside hello/hello-ack.");
            }

            protocolVersion = pv.ValueKind == JsonValueKind.String ? pv.GetString() : throw new ProtocolFramingViolationException("protocol-violation: protocolVersion not a string.");
        }
        else if (type is "hello" or "hello-ack")
        {
            throw new ProtocolFramingViolationException("protocol-violation: protocolVersion missing on hello/hello-ack.");
        }

        // F13 — the C7 rule this closes a regression in: for every message TYPE the host actually receives FROM
        // the supervisor (hello-ack, shutdown, error — `hello`/`ready`/`seed-ready`/`bye` are HOST-to-supervisor
        // only and never parsed here), an unknown top-level field, a missing required field, a wrong JSON type,
        // or a null where a non-null string is required is protocol-violation — never silently accepted by a
        // lenient parser. None of these three types currently has a nested object field, so there is no "unknown
        // inner field" case to enforce today; the check below still walks every level the schema DOES define.
        if (MessageSchemas.TryGetValue(type, out var schema))
        {
            var seenFields = new HashSet<string>(StringComparer.Ordinal);
            foreach (var prop in root.EnumerateObject())
            {
                seenFields.Add(prop.Name);
                if (!schema.Allowed.Contains(prop.Name))
                {
                    // G4 (Aşama G) — fixed text: the sender-controlled field NAME is never part of the message.
                    throw new ProtocolFramingViolationException(
                        "protocol-violation: unknown field for this message type.");
                }
            }

            foreach (var required in schema.Required)
            {
                if (!seenFields.Contains(required))
                {
                    // `required` comes from this file's own schema table, never from the wire.
                    throw new ProtocolFramingViolationException(
                        $"protocol-violation: missing required field '{required}'.");
                }
            }

            // error{code,message} — both required, non-null, non-empty strings (P2's own fixed-text contract).
            // RequireString already throws ProtocolFramingViolationException for missing/null/wrong-type/empty.
            if (type == "error")
            {
                RequireString(root, "code");
                RequireString(root, "message");
            }
        }

        return new ControlMessage(type, runId, protocolVersion, root);
    }

    /// <summary>
    /// F13 — the strict schema for every message type the host actually receives FROM the supervisor. `type` and
    /// `runId` are already validated unconditionally above (every message needs them); listed again here only so
    /// the "unknown field" walk below has the complete allowed-set per type.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, (HashSet<string> Required, HashSet<string> Allowed)> MessageSchemas =
        new Dictionary<string, (HashSet<string> Required, HashSet<string> Allowed)>
        {
            ["hello-ack"] = (
                new HashSet<string>(StringComparer.Ordinal) { "type", "runId", "protocolVersion" },
                new HashSet<string>(StringComparer.Ordinal) { "type", "runId", "protocolVersion" }),
            ["shutdown"] = (
                new HashSet<string>(StringComparer.Ordinal) { "type", "runId" },
                new HashSet<string>(StringComparer.Ordinal) { "type", "runId" }),
            ["error"] = (
                new HashSet<string>(StringComparer.Ordinal) { "type", "runId", "code", "message" },
                new HashSet<string>(StringComparer.Ordinal) { "type", "runId", "code", "message" }),
        };

    private static string RequireString(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(value.GetString()))
        {
            throw new ProtocolFramingViolationException($"protocol-violation: '{name}' missing, null, or not a non-empty string.");
        }

        return value.GetString()!;
    }

    /// <summary>C7 — Utf8JsonReader walk maintaining a per-object-depth name set; a repeat at ANY depth throws.
    /// JsonDocument/JsonSerializer silently accept the LAST duplicate value and are never used for this check.</summary>
    private static void EnsureNoDuplicateKeys(ReadOnlySpan<byte> json)
    {
        var reader = new Utf8JsonReader(json, isFinalBlock: true, state: default);
        var stack = new Stack<HashSet<string>>();

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    stack.Push(new HashSet<string>(StringComparer.Ordinal));
                    break;
                case JsonTokenType.EndObject:
                    if (stack.Count > 0) stack.Pop();
                    break;
                case JsonTokenType.PropertyName:
                    if (stack.Count > 0)
                    {
                        var name = reader.GetString()!;
                        if (!stack.Peek().Add(name))
                        {
                            // G4 (Aşama G) — fixed text: the duplicated key is never part of the message.
                            throw new ProtocolFramingViolationException("protocol-violation: duplicate JSON key.");
                        }
                    }
                    break;
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        _stream.Dispose();
        _socket.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// P1 — real in-process Kestrel bound to 127.0.0.1:0; the actually-bound address is read from
/// IServerAddressesFeature AFTER start, closing the old find-free-port/close/rebind race. Serves ONLY
/// `GET /api/internal/tenants/{id}/login-settings` (D6) — the header name it requires (X-Internal-Api-Key) is
/// the one measured from PlatformTenantLoginSettingsClient.cs, and a missing or wrong value is a real 401 (F8 —
/// this was previously a comment claim with no corresponding code; the header is now genuinely checked against
/// the same disposable key this run's real API child was given via PlatformService__InternalApiKey, so a
/// successful P7 login is also proof the real client actually presented the right key, not just that SOME
/// request reached this endpoint). Every other path is 404. Never Platform's own code, data, or a second real
/// service — and this endpoint's acceptance is NOT PPM's real entitlement provider (D6).
/// </summary>
internal sealed class PlatformLoginSettingsStub : IDisposable
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key"; // measured from PlatformTenantLoginSettingsClient.cs

    private readonly WebApplication _app;
    public string BaseUrl { get; }

    private PlatformLoginSettingsStub(WebApplication app, string baseUrl)
    {
        _app = app;
        BaseUrl = baseUrl;
    }

    public static async Task<PlatformLoginSettingsStub> StartAsync(string expectedInternalApiKey)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var app = builder.Build();

        app.MapGet("/api/internal/tenants/{tenantId:guid}/login-settings", IResult (Guid tenantId, HttpRequest request) =>
        {
            // F8 — genuinely checked, not just documented: a missing or wrong X-Internal-Api-Key is a real 401.
            if (!request.Headers.TryGetValue(InternalApiKeyHeader, out var presented) ||
                !string.Equals(presented.ToString(), expectedInternalApiKey, StringComparison.Ordinal))
            {
                return Results.Unauthorized();
            }

            var snapshot = new
            {
                tenantId,
                twoFactorEnabled = false,
                mfaRequired = false,
                emailLoginEnabled = true,
                phoneLoginEnabled = false,
                passwordMinLength = 8,
                passwordRequireUppercase = false,
                passwordRequireLowercase = false,
                passwordRequireDigit = false,
                passwordRequireSpecialChar = false,
                passwordExpirationDays = (int?)null,
                sessionTimeoutMinutes = 60,
                refreshTokenLifetimeDays = 7,
                maxFailedLoginAttempts = 5,
                lockoutDurationMinutes = 15
            };
            return Results.Json(new { succeeded = true, message = (string?)null, data = snapshot });
        });

        await app.StartAsync();

        var addressesFeature = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
        var boundAddress = addressesFeature?.Addresses.FirstOrDefault()
            ?? throw new InvalidOperationException("login-settings stub did not report a bound address.");

        return new PlatformLoginSettingsStub(app, boundAddress.TrimEnd('/') + "/");
    }

    public void Dispose()
    {
        _app.StopAsync(new CancellationTokenSource(TimeSpan.FromSeconds(3)).Token).GetAwaiter().GetResult();
    }
}

/// <summary>C6/D5 — OS process start time and parent PID, macOS branch (measured against known references — see
/// the Stage 2 report: offset 0 for start time, offset 560 for parent PID, neither a textbook struct offset).</summary>
internal static class ProcessTiming
{
    public static long GetStartTimeUnixMs(int pid) => ReadKinfoInt64(pid, offset: 0) is { } sec
        ? sec * 1000 + (ReadKinfoInt64(pid, offset: 8) ?? 0) / 1000
        : throw new InvalidOperationException($"pid {pid} not found.");

    public static int GetParentPid(int pid) => (int)(ReadKinfoInt64(pid, offset: 560, readAsInt32: true)
        ?? throw new InvalidOperationException($"pid {pid} not found."));

    private static long? ReadKinfoInt64(int pid, int offset, bool readAsInt32 = false)
    {
        const int CTL_KERN = 1, KERN_PROC = 14, KERN_PROC_PID = 1;
        int[] mib = { CTL_KERN, KERN_PROC, KERN_PROC_PID, pid };
        nuint size = 0;
        sysctl(mib, 4, IntPtr.Zero, ref size, IntPtr.Zero, 0);
        if (size == 0) return null;

        var buf = Marshal.AllocHGlobal((int)size);
        try
        {
            sysctl(mib, 4, buf, ref size, IntPtr.Zero, 0);
            return readAsInt32 ? Marshal.ReadInt32(buf, offset) : Marshal.ReadInt64(buf, offset);
        }
        finally
        {
            Marshal.FreeHGlobal(buf);
        }

        [DllImport("libc", SetLastError = true)]
        static extern int sysctl(int[] name, uint namelen, IntPtr oldp, ref nuint oldlenp, IntPtr newp, nuint newlen);
    }
}
