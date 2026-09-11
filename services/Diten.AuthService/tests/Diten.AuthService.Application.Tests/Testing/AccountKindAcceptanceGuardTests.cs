using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace Diten.AuthService.Application.Tests.Testing;

// Shares the SAME xunit collection as AccountKindEndpointTests: env vars are process-global (C1 §3), so no test
// touching AuthTestHost anywhere in this assembly may run concurrently with another one outside its own control.
[CollectionDefinition("AccountKindAcceptance", DisableParallelization = true)]
public sealed class AccountKindAcceptanceCollection;

/// <summary>
/// WP-INFRA-AUTH-ACCOUNT-KIND-01 (C1) — the fixture's own correctness: the pre-flight target checks, the
/// startup-failure cleanup, the Start() serialization, and the test-only JWT secret never leaking.
///
/// <para>Split by cost on purpose. §1's three checks are PURE (<see cref="AccountKindAcceptanceGuard"/>) and are
/// proven here with crafted inputs — no mongod, no host, fast. §2/§3/§4-5 need the REAL fixture (a genuine mongod
/// process, a genuine in-process host) to prove the wiring, not just the decision — those are integration-level and
/// slower; each spins at most one or two real <see cref="AccountKindAcceptance.AuthTestHost"/> instances.</para>
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed class AccountKindAcceptanceGuardTests
{
    // ── §1(a) — the environment actually holds the override (pure) ─────────────────────────────────────────

    [Fact]
    public void EnsureEnvironmentTookTheOverride_accepts_a_variable_that_matches()
    {
        const string key = "DITEN_ACCOUNT_KIND_GUARD_TEST_MATCH";
        Environment.SetEnvironmentVariable(key, "expected-value");
        try
        {
            var ex = Record.Exception(() => AccountKindAcceptanceGuard.EnsureEnvironmentTookTheOverride(key, "expected-value"));
            Assert.Null(ex);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void EnsureEnvironmentTookTheOverride_throws_when_the_variable_does_not_hold_the_expected_value()
    {
        const string key = "DITEN_ACCOUNT_KIND_GUARD_TEST_MISMATCH";
        Environment.SetEnvironmentVariable(key, "something-else");
        try
        {
            Assert.Throws<InvalidOperationException>(
                () => AccountKindAcceptanceGuard.EnsureEnvironmentTookTheOverride(key, "expected-value"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    // ── §1(b) — the runner is not the shared server (pure) ──────────────────────────────────────────────────

    [Theory]
    [InlineData("mongodb://127.0.0.1:54321")]
    [InlineData("mongodb://localhost:54321")]
    public void EnsureRunnerIsNotTheSharedServer_accepts_a_loopback_non_shared_port(string connectionString)
    {
        var ex = Record.Exception(() => AccountKindAcceptanceGuard.EnsureRunnerIsNotTheSharedServer(connectionString));
        Assert.Null(ex);
    }

    [Fact]
    public void EnsureRunnerIsNotTheSharedServer_throws_when_the_runner_bound_the_shared_port_27017()
    {
        Assert.Throws<InvalidOperationException>(
            () => AccountKindAcceptanceGuard.EnsureRunnerIsNotTheSharedServer("mongodb://127.0.0.1:27017"));
    }

    [Fact]
    public void EnsureRunnerIsNotTheSharedServer_throws_for_a_non_loopback_host()
    {
        Assert.Throws<InvalidOperationException>(
            () => AccountKindAcceptanceGuard.EnsureRunnerIsNotTheSharedServer("mongodb://10.0.0.5:54321"));
    }

    // ── §1(c) — the effective configuration resolves to the isolated database (pure) ───────────────────────

    [Fact]
    public void EnsureEffectiveConfigurationTargetsTheIsolatedDatabase_accepts_a_matching_configuration()
    {
        var config = BuildConfig(("MongoDbSettings:ConnectionString", "mongodb://127.0.0.1:54321"), ("MongoDbSettings:DatabaseName", "diten_auth_itest_account_kind"));

        var ex = Record.Exception(() => AccountKindAcceptanceGuard.EnsureEffectiveConfigurationTargetsTheIsolatedDatabase(
            config, "mongodb://127.0.0.1:54321", "diten_auth_itest_account_kind"));

        Assert.Null(ex);
    }

    [Fact]
    public void EnsureEffectiveConfigurationTargetsTheIsolatedDatabase_throws_when_the_database_name_resolves_to_the_shared_one()
    {
        // The prompt's own example: DatabaseName resolves to a FAKE/wrong value — here, literally the real shared
        // database's name, exactly the scenario this check exists to catch before a single write reaches it.
        var config = BuildConfig(("MongoDbSettings:ConnectionString", "mongodb://127.0.0.1:54321"), ("MongoDbSettings:DatabaseName", "diten_auth_v3"));

        var ex = Assert.Throws<InvalidOperationException>(() => AccountKindAcceptanceGuard.EnsureEffectiveConfigurationTargetsTheIsolatedDatabase(
            config, "mongodb://127.0.0.1:54321", "diten_auth_itest_account_kind"));
        Assert.Contains("diten_auth_v3", ex.Message);
    }

    [Fact]
    public void EnsureEffectiveConfigurationTargetsTheIsolatedDatabase_throws_when_the_connection_string_resolves_elsewhere()
    {
        var config = BuildConfig(("MongoDbSettings:ConnectionString", "mongodb://localhost:27017"), ("MongoDbSettings:DatabaseName", "diten_auth_itest_account_kind"));

        Assert.Throws<InvalidOperationException>(() => AccountKindAcceptanceGuard.EnsureEffectiveConfigurationTargetsTheIsolatedDatabase(
            config, "mongodb://127.0.0.1:54321", "diten_auth_itest_account_kind"));
    }

    private static IConfiguration BuildConfig(params (string Key, string Value)[] pairs) =>
        new ConfigurationBuilder().AddInMemoryCollection(pairs.Select(p => new KeyValuePair<string, string?>(p.Key, p.Value))).Build();

    // ── §2 — a startup failure reverts everything and leaves nothing running (integration, real mongod) ────

    private static readonly string[] OverrideEnvironmentKeys =
    {
        "MongoDbSettings__ConnectionString",
        "MongoDbSettings__DatabaseName",
        "Eventing__Transport",
        "Smtp__Enabled",
        "TenantResolution__DevBypassEnabled",
        "Observability__Metrics__Enabled",
        "ASPNETCORE_ENVIRONMENT",
        "JwtSettings__Secret"
    };

    [Fact]
    public async Task A_startup_failure_reverts_the_environment_and_leaves_no_mongod_process_running()
    {
        var before = OverrideEnvironmentKeys.ToDictionary(k => k, Environment.GetEnvironmentVariable);

        var originalOut = Console.Out;
        var capture = new StringWriter();
        Console.SetOut(capture);

        var host = new AccountKindAcceptance.AuthTestHost();
        InvalidOperationException thrown;
        try
        {
            thrown = await Assert.ThrowsAsync<InvalidOperationException>(host.InitializeAsync_ForTestingInjectedFailure);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.Contains("TEST-INJECTED-FAILURE", thrown.Message);

        // (i) environment reverted — every override key reads exactly what it read before Start() was ever called.
        foreach (var key in OverrideEnvironmentKeys)
        {
            Assert.Equal(before[key], Environment.GetEnvironmentVariable(key));
        }

        // (ii) the mongod process is really gone — connect to the port the STARTED line printed and expect refusal.
        var started = Regex.Match(capture.ToString(), @"STARTED at mongodb://127\.0\.0\.1:(\d+)");
        Assert.True(started.Success, "the fixture's STARTED line was not captured — cannot verify the process died on the right port.");
        var port = int.Parse(started.Groups[1].Value);

        Assert.False(await CanConnectAsync("127.0.0.1", port, TimeSpan.FromSeconds(2)),
            $"a TCP connection to 127.0.0.1:{port} succeeded after the failed start's cleanup — the mongod process is still running.");
    }

    private static async Task<bool> CanConnectAsync(string host, int port, TimeSpan timeout)
    {
        using var client = new TcpClient();
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await client.ConnectAsync(host, port, cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ── §4/§5 — the generated JWT secret never reaches Console output or the mongod log (integration, real) ──

    [Fact]
    public async Task Generated_jwt_secret_never_appears_in_console_output_or_the_mongod_log()
    {
        var originalOut = Console.Out;
        var capture = new StringWriter();
        Console.SetOut(capture);

        var host = new AccountKindAcceptance.AuthTestHost();
        try
        {
            await host.InitializeAsync();
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        try
        {
            var secret = host.GeneratedJwtSecretForLeakGuardOnly;
            Assert.False(string.IsNullOrWhiteSpace(secret));

            var consoleText = capture.ToString();
            var mongoLogText = string.Join('\n', host.MongoLog);

            // Assert.False (not Assert.DoesNotContain): a failure here must not embed the secret in xunit's own
            // failure message, which would leak it into the test runner's own output — the exact thing under test.
            Assert.False(consoleText.Contains(secret!, StringComparison.Ordinal), "the fixture's Console output contains the generated test JWT secret.");
            Assert.False(mongoLogText.Contains(secret!, StringComparison.Ordinal), "mongod's own captured log contains the generated test JWT secret.");
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    // ── §3 — two Start()/Dispose() lifecycles ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Two_sequential_start_dispose_cycles_leave_the_environment_clean_each_time()
    {
        var before = OverrideEnvironmentKeys.ToDictionary(k => k, Environment.GetEnvironmentVariable);

        var host1 = new AccountKindAcceptance.AuthTestHost();
        await host1.InitializeAsync();
        await host1.DisposeAsync();
        foreach (var key in OverrideEnvironmentKeys)
        {
            Assert.Equal(before[key], Environment.GetEnvironmentVariable(key));
        }

        var host2 = new AccountKindAcceptance.AuthTestHost();
        await host2.InitializeAsync();
        await host2.DisposeAsync();
        foreach (var key in OverrideEnvironmentKeys)
        {
            Assert.Equal(before[key], Environment.GetEnvironmentVariable(key));
        }
    }

    [Fact]
    public async Task Two_concurrent_Start_calls_never_overlap_their_environment_mutation_window()
    {
        // Safe to reset: this test method is the only thing touching AuthTestHost while it runs — the whole
        // "AccountKindAcceptance" collection is serialized against every OTHER test class that shares it.
        AccountKindAcceptance.AuthTestHost.ResetConcurrencyProbeForTests();

        var host1 = new AccountKindAcceptance.AuthTestHost();
        var host2 = new AccountKindAcceptance.AuthTestHost();
        await Task.WhenAll(host1.InitializeAsync(), host2.InitializeAsync());
        try
        {
            // StartLock (SemaphoreSlim(1,1)) guarantees this by construction; the probe proves it EMPIRICALLY, so a
            // regression that removes the lock later (K1: `await StartLock.WaitAsync()` deleted) turns this red —
            // it would then observe 2 here instead of 1.
            Assert.Equal(1, AccountKindAcceptance.AuthTestHost.MaxObservedConcurrentCriticalSections);
        }
        finally
        {
            await host1.DisposeAsync();
            await host2.DisposeAsync();
        }
    }
}
