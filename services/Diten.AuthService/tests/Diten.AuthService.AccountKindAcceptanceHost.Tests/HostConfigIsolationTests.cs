// WP-INFRA-AUTH-ACCEPTANCE-HOST-01 — F10 (Aşama F) + G1/G2(a) (Aşama G): the acceptance-host PREP path
// (StartWithMongoDataDirectoryAsync — the fixture code that runs INSIDE the out-of-process host, W2, before the real
// API child is even launched) must build its configuration EXCLUSIVELY from test-owned values, in BOTH windows:
//
//   EAGER window (G1) — the API's own Program.cs reads configuration BEFORE builder.Build() (observability options,
//     secret validation, JwtBearer issuer/audience, Mongo settings, the DataSeeder). ConfigureAppConfiguration's
//     Sources.Clear() runs only at Build, so it cannot protect that window; the prep host's content root must be an
//     EMPTY run-owned directory so appsettings.json is never found. Proof: IHostEnvironment.ContentRootPath is that
//     directory, and JwtBearer's ValidIssuer — bound at REGISTRATION from the eager configuration — is not the
//     JwtSettings:Issuer value checked in to the API's appsettings.json.
//   LATE window (G2(a)) — the built host's configuration has no file provider and no environment-variable provider
//     at all (asserted by TYPE), and a canary environment variable set in this process during the call never
//     appears in it.
//
// Calls AuthTestHost.StartWithMongoDataDirectoryAsync directly, in-process (the same entry point Program.cs itself
// uses), with an isolated HOME for the duration of the call. One case spawns the real host to prove a non-empty
// content root maps to mongo-start-failed/exit 2 over the wire.

using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Diten.AuthService.Application.Tests.Testing;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace Diten.AuthService.AccountKindAcceptanceHost.Tests;

[Collection("SevenStateSupervisor")]
public sealed class HostConfigIsolationTests
{
    private const string CanaryEnvironmentKey = "Observability__Seq__ApiKey";
    private const string CanaryConfigurationKey = "Observability:Seq:ApiKey";

    private readonly ITestOutputHelper _output;

    public HostConfigIsolationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task AcceptanceHostPrepPath_EagerAndLateWindows_SeeOnlyTestOwnedConfiguration()
    {
        // Measured first, not assumed: the checked-in value the eager window would have bound if appsettings.json
        // were still readable. Never printed.
        var checkedInIssuer = ReadCheckedInApiJwtIssuer();
        Assert.False(string.IsNullOrEmpty(checkedInIssuer), "the checked-in Api appsettings.json has no non-empty JwtSettings:Issuer to compare against");

        var root = Directory.CreateTempSubdirectory("dak-g1-").FullName;
        var mongoDataDir = Path.Combine(root, "mongo");
        var contentDir = Path.Combine(root, "content");
        var isolatedHome = Path.Combine(root, "home");
        Directory.CreateDirectory(mongoDataDir);
        Directory.CreateDirectory(contentDir);
        Directory.CreateDirectory(isolatedHome);

        var canary = "canary-" + Guid.NewGuid().ToString("N");
        var previousHome = Environment.GetEnvironmentVariable("HOME");
        var previousCanaryKeyValue = Environment.GetEnvironmentVariable(CanaryEnvironmentKey);

        AccountKindAcceptance.AuthTestHost? host = null;
        try
        {
            Environment.SetEnvironmentVariable("HOME", isolatedHome);
            Environment.SetEnvironmentVariable(CanaryEnvironmentKey, canary);

            host = await AccountKindAcceptance.AuthTestHost.StartWithMongoDataDirectoryAsync(mongoDataDir, contentDir);

            // ── G1 (a) — a value bound BEFORE Build did not come from the checked-in appsettings.json ──────────
            var jwtBearer = host.Factory.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
                .Get(JwtBearerDefaults.AuthenticationScheme);
            var eagerIssuer = jwtBearer.TokenValidationParameters.ValidIssuer;
            Assert.False(
                string.Equals(eagerIssuer, checkedInIssuer, StringComparison.Ordinal),
                "JwtBearer ValidIssuer (bound before Build) equals the checked-in appsettings.json JwtSettings:Issuer — the eager window read the API content root");

            // ── G1 (b) — the content root IS the given empty directory ─────────────────────────────────────────
            var env = host.Factory.Services.GetRequiredService<IHostEnvironment>();
            Assert.NotEqual("Development", env.EnvironmentName);
            Assert.Equal(
                Path.TrimEndingDirectorySeparator(contentDir),
                Path.TrimEndingDirectorySeparator(env.ContentRootPath));
            Assert.Empty(Directory.EnumerateFileSystemEntries(contentDir));

            // ── G2(a) — the final provider list, by TYPE ────────────────────────────────────────────────────────
            var config = (IConfigurationRoot)host.Factory.Services.GetRequiredService<IConfiguration>();
            var providerTypes = config.Providers.Select(p => p.GetType().FullName).ToList();
            _output.WriteLine("final configuration provider types: " + string.Join(", ", providerTypes));

            Assert.DoesNotContain(config.Providers, p => p is FileConfigurationProvider);
            Assert.DoesNotContain(config.Providers, p => p is EnvironmentVariablesConfigurationProvider);
            // Measured on this machine: exactly one provider, the fixture's in-memory override source.
            Assert.Equal(new[] { typeof(MemoryConfigurationProvider).FullName }, providerTypes);

            // The canary environment variable was live in this process for the whole call; it never reached the
            // built host's configuration (fixed-text assertions only — the canary is never printed).
            Assert.True(config[CanaryConfigurationKey] is null, "the canary environment variable reached the built host's configuration");
            Assert.False(
                config.AsEnumerable().Any(pair => string.Equals(pair.Value, canary, StringComparison.Ordinal)),
                "the canary value appears somewhere in the built host's configuration");

            // Positive check: the test-owned overrides ARE what the host resolved.
            Assert.Equal(host.ConnectionString, config["MongoDbSettings:ConnectionString"]);
            Assert.Equal(AccountKindAcceptance.DatabaseName, config["MongoDbSettings:DatabaseName"]);
            Assert.Equal("false", config["Observability:Seq:Enabled"]);
            Assert.Equal("true", config["OTEL_SDK_DISABLED"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(CanaryEnvironmentKey, previousCanaryKeyValue);
            Environment.SetEnvironmentVariable("HOME", previousHome);
            if (host is not null)
            {
                await host.DisposeAsync();
            }

            try { Directory.Delete(root, recursive: true); } catch { /* best effort, own temp dir */ }
        }
    }

    [Fact]
    public async Task AcceptanceHostPrepPath_ContentRootWithAnyEntry_IsRefusedBeforeMongodStarts()
    {
        var root = Directory.CreateTempSubdirectory("dak-g1-refuse-").FullName;
        var mongoDataDir = Path.Combine(root, "mongo");
        var contentDir = Path.Combine(root, "content");
        Directory.CreateDirectory(mongoDataDir);
        Directory.CreateDirectory(contentDir);
        File.WriteAllText(Path.Combine(contentDir, "appsettings.json"), "{}");

        try
        {
            var hookCalled = false;
            var ex = await Record.ExceptionAsync(() => AccountKindAcceptance.AuthTestHost.StartWithMongoDataDirectoryAsync(
                mongoDataDir, contentDir, beforeSeedHookForTesting: _ => { hookCalled = true; return Task.CompletedTask; }));

            Assert.IsType<InvalidOperationException>(ex);
            Assert.Contains("content root", ex!.Message);
            Assert.False(hookCalled, "the host must never be built on a non-empty content root");
            Assert.True(!Directory.EnumerateFileSystemEntries(mongoDataDir).Any(), "mongod wrote into its data directory — it was started before the refusal");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* best effort, own temp dir */ }
        }
    }

    [Fact]
    public async Task RealHost_NonEmptyContentRoot_ReportsMongoStartFailedExit2()
    {
        var h = new SupervisorTestHarness(_output);
        var (root, dev, ino) = h.CreateRunRoot();
        h.PrepareFixedSubdirs(root);
        File.WriteAllText(Path.Combine(root, "content", "stray.txt"), "a content root must be empty");
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

            var msg = await h.ReceiveRaw(reader, TimeSpan.FromSeconds(30));
            Assert.NotNull(msg);
            Assert.Equal("error", msg!.Value.GetProperty("type").GetString());
            Assert.Equal("mongo-start-failed", msg.Value.GetProperty("code").GetString());

            var exitCode = await h.WaitForExitAsync(hostPid, TimeSpan.FromSeconds(15));
            Assert.Equal(2, exitCode); // ExitCodes.MongoStartFailed
            Assert.DoesNotContain(h.EnumerateProcessGroupMembers(hostPid), p => p != hostPid);
        }
        finally
        {
            h.CleanRoot(root, dev, ino);
        }
    }

    /// <summary>The checked-in services/Diten.AuthService/src/Diten.AuthService.Api/appsettings.json, found by walking
    /// up from this test's own output directory.</summary>
    private static string? ReadCheckedInApiJwtIssuer()
    {
        var probe = new DirectoryInfo(AppContext.BaseDirectory);
        while (probe is not null)
        {
            var candidate = Path.Combine(probe.FullName, "services", "Diten.AuthService", "src", "Diten.AuthService.Api", "appsettings.json");
            if (File.Exists(candidate))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(candidate));
                return doc.RootElement.TryGetProperty("JwtSettings", out var jwt)
                       && jwt.TryGetProperty("Issuer", out var issuer)
                       && issuer.ValueKind == JsonValueKind.String
                    ? issuer.GetString()
                    : null;
            }

            probe = probe.Parent;
        }

        throw new DirectoryNotFoundException("the checked-in Diten.AuthService.Api/appsettings.json was not found above the test output directory.");
    }
}
