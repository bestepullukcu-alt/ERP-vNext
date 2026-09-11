using System.Net;
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

        var originalError = Console.Error;

        var errorCapture = new StringWriter();

        Console.SetError(errorCapture);

        var host = new AccountKindAcceptance.AuthTestHost();
        InvalidOperationException thrown;
        try
        {
            thrown = await Assert.ThrowsAsync<InvalidOperationException>(host.InitializeAsync_ForTestingInjectedFailure);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
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
        var originalError = Console.Error;
        var errorCapture = new StringWriter();
        Console.SetError(errorCapture);

        var host = new AccountKindAcceptance.AuthTestHost();
        try
        {
            await host.InitializeAsync();
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }

        try
        {
            var secret = host.GeneratedJwtSecretForLeakGuardOnly;
            Assert.False(string.IsNullOrWhiteSpace(secret));

            var consoleText = capture.ToString();
            Assert.False(errorCapture.ToString().Contains(secret!, StringComparison.Ordinal), "the fixture's captured Console.Error output contains the generated test JWT secret.");
            Assert.False(thrown.ToString().Contains(secret!, StringComparison.Ordinal), "the startup-failure exception (inner exceptions and stack included) contains the generated test JWT secret.");
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

    // ── C3 — the override's lifetime is lock-scoped and short, never tied to Dispose ───────────────────────
    //
    // PPM CT finding (accepted): the environment used to stay overridden until Dispose, while the LOCK released
    // as soon as the host's own config was captured. A second host starting in that window captured the FIRST
    // host's still-live overrides as ITS OWN "previous" — so whichever host disposed LAST restored the OTHER
    // host's dead connection string / discarded JWT secret into the process, permanently. T1/T2 pin BOTH dispose
    // orders; T3 pins that a RUNNING host is unaffected by its own environment already being restored; T4/T5 pin
    // the failure-path guarantees (env unchanged, no process, lock free — even when cleanup itself throws); T6
    // pins that none of this leaks the generated JWT secret into an exception message or a log line.

    private static Dictionary<string, string?> CaptureEnvironment() =>
        OverrideEnvironmentKeys.ToDictionary(k => k, Environment.GetEnvironmentVariable);

    private static void AssertEnvironmentEquals(Dictionary<string, string?> expected)
    {
        foreach (var key in OverrideEnvironmentKeys)
        {
            // Assert.True over Assert.Equal on purpose: Assert.Equal prints expected/actual on failure, and for
            // MongoDbSettings__ConnectionString or JwtSettings__Secret that would put a connection string or the
            // generated secret into xunit's own output. Only the variable NAME may appear in the message.
            var same = string.Equals(expected[key], Environment.GetEnvironmentVariable(key), StringComparison.Ordinal);
            Assert.True(same, $"Environment variable '{key}' did not return to its initial value (values withheld on purpose).");
        }
    }

    // T1 — A then B start; A disposed, then B disposed.
    [Fact]
    public async Task T1_A_then_B_start_dispose_A_then_B_always_restores_the_true_baseline()
    {
        var before = CaptureEnvironment();

        var hostA = new AccountKindAcceptance.AuthTestHost();
        await hostA.InitializeAsync();
        AssertEnvironmentEquals(before); // the core of the fix: A's own overrides are already gone by the time Start returns

        var hostB = new AccountKindAcceptance.AuthTestHost();
        await hostB.InitializeAsync();
        AssertEnvironmentEquals(before); // B captured the TRUE baseline as "previous" — not A's still-live overrides

        await hostA.DisposeAsync();
        AssertEnvironmentEquals(before);

        await hostB.DisposeAsync();
        AssertEnvironmentEquals(before);
    }

    // T2 — the same start order, the OPPOSITE dispose order. Under the bug this fixes, THIS was the order that
    // corrupted the environment: B (disposed last) used to restore whatever it captured as "previous", and before
    // the fix that was A's own override — not the baseline.
    [Fact]
    public async Task T2_A_then_B_start_dispose_B_then_A_still_restores_the_true_baseline()
    {
        var before = CaptureEnvironment();

        var hostA = new AccountKindAcceptance.AuthTestHost();
        await hostA.InitializeAsync();
        var hostB = new AccountKindAcceptance.AuthTestHost();
        await hostB.InitializeAsync();
        AssertEnvironmentEquals(before);

        await hostB.DisposeAsync();
        AssertEnvironmentEquals(before);

        await hostA.DisposeAsync();
        AssertEnvironmentEquals(before);
    }

    // T3 — a RUNNING host (not yet disposed) is unaffected by its own environment already being restored: its
    // MongoDbSettings was captured into its DI container before the restore ran, so it keeps reading/writing its
    // isolated database over real HTTP.
    [Fact]
    public async Task T3_a_running_host_keeps_serving_its_isolated_database_after_its_own_environment_is_already_restored()
    {
        var before = CaptureEnvironment();
        var host = new AccountKindAcceptance.AuthTestHost();
        await host.InitializeAsync();
        try
        {
            AssertEnvironmentEquals(before); // env is back to baseline WHILE the host is still running, unrelated to Dispose

            using var client = host.Client(host.Seeded.PmoToken, host.Seeded.TenantId);
            var response = await client.GetAsync($"api/users/{host.Seeded.Human.Id}/account-assertion");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    // T4 — a deliberately failed start (the injected-failure seam) leaves the environment untouched, kills the
    // mongod process, and — critically — frees the lock immediately, so the VERY NEXT Start() does not wait on
    // anything the failed attempt left behind.
    [Fact]
    public async Task T4_a_failed_start_leaves_the_environment_unchanged_no_process_and_the_lock_free_for_the_next_Start()
    {
        var before = CaptureEnvironment();

        var failing = new AccountKindAcceptance.AuthTestHost();
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(failing.InitializeAsync_ForTestingInjectedFailure);
        Assert.Contains("TEST-INJECTED-FAILURE", failure.Message);
        AssertEnvironmentEquals(before);

        // The lock must be free RIGHT NOW — bounded wait, so a regression (the lock never released) fails this
        // test with a clear timeout instead of hanging the whole run.
        var succeeding = new AccountKindAcceptance.AuthTestHost();
        var startTask = succeeding.InitializeAsync();
        var winner = await Task.WhenAny(startTask, Task.Delay(TimeSpan.FromSeconds(30)));
        Assert.True(ReferenceEquals(winner, startTask), "the next Start() did not complete within 30s — the lock the failed attempt held was not released.");
        await startTask; // propagate any genuine failure with its real message

        try
        {
            AssertEnvironmentEquals(before); // the succeeding host also restored the environment within its own Start
        }
        finally
        {
            await succeeding.DisposeAsync();
        }
    }

    // T5 — even when factory disposal itself throws, Dispose still stops the mongod process, still frees the
    // lock, and still lets the exception propagate (not an AggregateException, not silently lost).
    [Fact]
    public async Task T5_dispose_still_stops_the_runner_and_frees_the_lock_when_factory_disposal_throws()
    {
        var host = new AccountKindAcceptance.AuthTestHost();
        await host.InitializeAsync();

        var originalOut = Console.Out;
        var capture = new StringWriter();
        Console.SetOut(capture);

        var originalError = Console.Error;

        var errorCapture = new StringWriter();

        Console.SetError(errorCapture);

        // The hook disposes the REAL factory for real (no resource actually leaks), then injects the failure this
        // test exists to observe — proving the SURROUNDING cleanup code's resilience, not faking away the dispose.
        host.DisposeFactoryHookForTesting = async factory =>
        {
            await factory.DisposeAsync();
            throw new InvalidOperationException("TEST-INJECTED-DISPOSE-FAILURE: simulated factory disposal failure.");
        };

        Exception? thrown;
        try
        {
            thrown = await Record.ExceptionAsync(host.DisposeAsync);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }

        Assert.NotNull(thrown);
        Assert.Contains("TEST-INJECTED-DISPOSE-FAILURE", thrown!.Message);
        Assert.Contains("ephemeral mongod STOPPED", capture.ToString()); // the runner still stopped despite the factory exception

        // The lock is free — a fresh Start right after must not hang on it either.
        var succeeding = new AccountKindAcceptance.AuthTestHost();
        var startTask = succeeding.InitializeAsync();
        var winner = await Task.WhenAny(startTask, Task.Delay(TimeSpan.FromSeconds(30)));
        Assert.True(ReferenceEquals(winner, startTask), "the next Start() did not complete within 30s — Dispose did not free the lock after its own failure.");
        await startTask;
        await succeeding.DisposeAsync();
    }

    // T6 — a startup failure's exception message and every captured log line name the offending variable, never
    // the generated JWT secret's VALUE.
    [Fact]
    public async Task T6_a_startup_failures_message_and_logs_never_contain_the_generated_JWT_secret_value()
    {
        var originalOut = Console.Out;
        var capture = new StringWriter();
        Console.SetOut(capture);
        var originalError = Console.Error;
        var errorCapture = new StringWriter();
        Console.SetError(errorCapture);

        var host = new AccountKindAcceptance.AuthTestHost();
        InvalidOperationException thrown;
        try
        {
            thrown = await Assert.ThrowsAsync<InvalidOperationException>(host.InitializeAsync_ForTestingInjectedFailure);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }

        var secret = host.GeneratedJwtSecretForLeakGuardOnly;
        Assert.False(string.IsNullOrWhiteSpace(secret));

        // Assert.False, not Assert.Contains/DoesNotContain — a failed assertion here must not embed the secret
        // into xunit's own failure message, which would itself be the leak this test exists to catch.
        Assert.False(thrown.Message.Contains(secret!, StringComparison.Ordinal), "the startup-failure exception message contains the generated test JWT secret.");
        Assert.False(capture.ToString().Contains(secret!, StringComparison.Ordinal), "the fixture's captured Console output contains the generated test JWT secret.");
        Assert.False(errorCapture.ToString().Contains(secret!, StringComparison.Ordinal), "the fixture's captured Console.Error output contains the generated test JWT secret.");
        Assert.False(thrown.ToString().Contains(secret!, StringComparison.Ordinal), "the startup-failure exception (inner exceptions and stack included) contains the generated test JWT secret.");
        var mongoLogText = string.Join('\n', host.MongoLog);
        Assert.False(mongoLogText.Contains(secret!, StringComparison.Ordinal), "mongod's own captured log contains the generated test JWT secret.");
    }
}
