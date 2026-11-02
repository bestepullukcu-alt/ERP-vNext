using System.Net;
using System.Text.Json;
using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.PvgService.Api.ModuleRegistration;
using Diten.PvgService.Api.Tests.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.PvgService.Api.Tests.ModuleRegistration;

// WP-0230-DL11, AC-37: the closed catalog registration is inert and harmless. The tests count calls on a stub handler and
// make no network call outside the loopback address of the redirect test. Every wait is bounded, so a sabotage turns a
// test red and never hangs it. Values are produced per run; there is no real key or address anywhere.
public sealed class PvgModuleRegistrationTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private const string Succeeded = "PVG_MODULE_REGISTRATION_SUCCEEDED";
    private const string Refused = "PVG_MODULE_REGISTRATION_REFUSED";
    private const string Retrying = "PVG_MODULE_REGISTRATION_RETRYING";
    private const string GaveUp = "PVG_MODULE_REGISTRATION_GAVE_UP";
    private const string Stopped = "PVG_MODULE_REGISTRATION_STOPPED";
    private const string SkippedNotConfigured = "PVG_MODULE_REGISTRATION_SKIPPED_NOT_CONFIGURED";
    private const string SkippedProductionLike = "PVG_MODULE_REGISTRATION_SKIPPED_PRODUCTION_LIKE";

    // (a)
    [Fact]
    public async Task Case_intake_registration_posts_the_manifest_once_to_the_internal_endpoint()
    {
        var expectedBody = JsonSerializer.Serialize(new PvgCaseIntakeTriageManifestProvider().GetManifest(), Web);

        foreach (var suffix in new[] { string.Empty, "/" })
        {
            var key = RegistrationValues.NewKey();
            var baseUrl = RegistrationValues.NewBaseUrl(RegistrationValues.NewToken());
            await using var rig = RegistrationRig.Create(StubRegistrationHandler.Always(HttpStatusCode.OK), key, baseUrl + suffix);

            await rig.StartAsync();
            Assert.Equal(Succeeded, await rig.Logs.WaitForAnyAsync(Bound, Succeeded));

            var request = Assert.Single(rig.Handler.Requests);
            Assert.Equal("POST", request.Method);
            Assert.Equal(baseUrl + RegistrationValues.RegisterPath, request.Uri);
            Assert.Equal(key, request.ApiKeyHeader);
            Assert.DoesNotContain("Authorization", request.HeaderNames);
            Assert.DoesNotContain("X-Tenant-Id", request.HeaderNames);

            // The body is the provider's manifest, compared after a round trip through the shared record.
            var sent = JsonSerializer.Deserialize<ModuleManifestDocument>(request.Body, Web);
            Assert.NotNull(sent);
            Assert.Equal(expectedBody, JsonSerializer.Serialize(sent, Web));
        }
    }

    // (b)
    [Fact]
    public async Task Case_intake_registration_makes_no_call_without_key_or_url()
    {
        var goodKey = RegistrationValues.NewKey();
        var goodUrl = RegistrationValues.NewBaseUrl(RegistrationValues.NewToken());

        (string Case, string? Key, string? Url)[] cases =
        [
            ("key missing", null, goodUrl),
            ("key empty", string.Empty, goodUrl),
            ("key whitespace", " \t ", goodUrl),
            ("url missing", goodKey, null),
            ("url empty", goodKey, string.Empty),
            ("url whitespace", goodKey, "   "),
            ("url relative", goodKey, "relative/path/" + RegistrationValues.NewToken()),
            ("url rooted path", goodKey, "/rooted/path/" + RegistrationValues.NewToken()),
            ("url ftp", goodKey, "ftp://pvg-test-" + RegistrationValues.NewToken() + ".invalid")
        ];

        // Positive control: with both values valid the same harness does see the call.
        await using (var control = await RegistrationHost.StartAsync(StubRegistrationHandler.Always(HttpStatusCode.OK), goodKey, goodUrl))
        {
            Assert.Equal(Succeeded, await control.Logs.WaitForAnyAsync(Bound, Succeeded));
            Assert.Equal(1, control.Handler.CallCount);
        }

        foreach (var (name, key, url) in cases)
        {
            await using var host = await RegistrationHost.StartAsync(StubRegistrationHandler.Always(HttpStatusCode.OK), key, url);

            var code = await host.Logs.WaitForAnyAsync(Bound, SkippedNotConfigured, Succeeded, GaveUp, Refused);
            Assert.True(code == SkippedNotConfigured, $"{name}: expected {SkippedNotConfigured} but saw {code ?? "nothing"}");
            Assert.True(host.Handler.CallCount == 0, $"{name}: the stub saw {host.Handler.CallCount} call(s)");

            // The host started: the process answers.
            Assert.Equal(200, (await host.Host.GetReadyAsync()).StatusCode);
        }
    }

    [Fact]
    public async Task Case_intake_registration_makes_no_call_on_a_production_like_host()
    {
        var key = RegistrationValues.NewKey();
        var url = RegistrationValues.NewBaseUrl(RegistrationValues.NewToken());

        await using var host = await RegistrationHost.StartAsync(
            StubRegistrationHandler.Always(HttpStatusCode.OK), key, url, productionLike: true);

        var code = await host.Logs.WaitForAnyAsync(Bound, SkippedProductionLike, SkippedNotConfigured, Succeeded, GaveUp, Refused);
        Assert.Equal(SkippedProductionLike, code);
        Assert.Equal(0, host.Handler.CallCount);
        // On a production-like host the values are not read at all.
        Assert.Equal(0, host.Environment.Reads);
    }

    // (c)
    [Fact]
    public async Task Case_intake_registration_values_come_only_from_the_environment()
    {
        var key = RegistrationValues.NewKey();
        var url = RegistrationValues.NewBaseUrl(RegistrationValues.NewToken());
        KeyValuePair<string, string?>[] configured =
        [
            new("PlatformRegistration:InternalApiKey", key),
            new("PlatformRegistration:BaseUrl", url),
            new("PlatformRegistration__InternalApiKey", key),
            new("PlatformRegistration__BaseUrl", url)
        ];

        // The reader answers nothing; the configuration holds both values.
        await using var host = await RegistrationHost.StartAsync(
            StubRegistrationHandler.Always(HttpStatusCode.OK), null, null, extraConfiguration: configured);

        var code = await host.Logs.WaitForAnyAsync(Bound, SkippedNotConfigured, Succeeded, GaveUp, Refused);
        Assert.Equal(SkippedNotConfigured, code);
        Assert.Equal(0, host.Handler.CallCount);
    }

    [Fact]
    public void No_committed_configuration_carries_the_registration_values()
    {
        var root = PvgRepositoryRoot.Find();
        var files = PvgCommittedConfiguration.Collect(root)
            .Where(file =>
                file.Path.StartsWith("services/Diten.PvgService/", StringComparison.Ordinal) ||
                file.Path == ".claude/launch.json" ||
                file.Path.StartsWith(".github/workflows/", StringComparison.Ordinal))
            .ToArray();

        // Positive control: the walk reaches the launch configuration and the workflows it is about.
        Assert.Contains(files, file => file.Path == ".claude/launch.json");
        Assert.Contains(files, file => file.Path == ".github/workflows/service-tests.yml");
        Assert.Empty(PvgRegistrationValueScan.Find(files));

        // The check itself, once per spelling: each fixture yields exactly one finding.
        var section = PvgRegistrationValueScan.Find(
            [("fixture/appsettings.json", """{ "PlatformRegistration": { "BaseUrl": "fixture-value", "InternalApiKey": "fixture-value" } }""")]);
        Assert.Equal(new[] { ("fixture/appsettings.json", "section") }, section.ToArray());

        var variable = PvgRegistrationValueScan.Find(
            [("fixture/workflow.yml", "env:\n  PlatformRegistration__BaseUrl: fixture-value\n  PlatformRegistration__InternalApiKey: fixture-value\n")]);
        Assert.Equal(new[] { ("fixture/workflow.yml", "underscore") }, variable.ToArray());
    }

    // (d)
    [Fact]
    public async Task Case_intake_registration_start_returns_before_the_push_finishes()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new StubRegistrationHandler(async (_, _, ct) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        await using var rig = RegistrationRig.Create(
            handler, RegistrationValues.NewKey(), RegistrationValues.NewBaseUrl(RegistrationValues.NewToken()));
        Task? start = null;
        try
        {
            start = rig.StartAsync();
            var returned = await RegistrationValues.CompletesWithinAsync(start, TimeSpan.FromSeconds(5));
            var callOpen = await RegistrationValues.CompletesWithinAsync(entered.Task, TimeSpan.FromSeconds(5));

            Assert.True(returned, "StartAsync did not return within 5 seconds while the call was open");
            Assert.True(callOpen, "the push never reached the handler");
            Assert.Equal(1, handler.CallCount);
            Assert.Equal(0, rig.Logs.CountOf(Succeeded));
        }
        finally
        {
            release.TrySetResult();
            if (start is not null)
            {
                await RegistrationValues.CompletesWithinAsync(start, TimeSpan.FromSeconds(5));
            }
        }
    }

    [Fact]
    public async Task Case_intake_registration_stop_cancels_the_pending_push()
    {
        // During an open call.
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var open = new StubRegistrationHandler(async (_, _, ct) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        await using (var rig = RegistrationRig.Create(
                         open, RegistrationValues.NewKey(), RegistrationValues.NewBaseUrl(RegistrationValues.NewToken())))
        {
            Task? stop = null;
            try
            {
                await rig.StartAsync();
                Assert.True(await RegistrationValues.CompletesWithinAsync(entered.Task, TimeSpan.FromSeconds(5)));

                stop = rig.StopAsync();
                var stopped = await RegistrationValues.CompletesWithinAsync(stop, TimeSpan.FromSeconds(5));
                Assert.True(stopped, "StopAsync did not end the push within 5 seconds (open call)");
                Assert.Equal(1, open.CallCount);
                Assert.Equal(Stopped, await rig.Logs.WaitForAnyAsync(Bound, Stopped));
            }
            finally
            {
                release.TrySetResult();
                if (stop is not null)
                {
                    await RegistrationValues.CompletesWithinAsync(stop, TimeSpan.FromSeconds(5));
                }
            }
        }

        // During a wait between attempts: the wait blocks until it is cancelled.
        var blocked = new RecordingDelay { BlockUntilCancelled = true };
        var failing = StubRegistrationHandler.Always(HttpStatusCode.ServiceUnavailable);
        await using (var rig = RegistrationRig.Create(
                         failing, RegistrationValues.NewKey(), RegistrationValues.NewBaseUrl(RegistrationValues.NewToken()), delay: blocked))
        {
            await rig.StartAsync();
            Assert.True(await RegistrationValues.CompletesWithinAsync(blocked.Waiting, TimeSpan.FromSeconds(5)));

            var stop = rig.StopAsync();
            var stopped = await RegistrationValues.CompletesWithinAsync(stop, TimeSpan.FromSeconds(5));
            Assert.True(stopped, "StopAsync did not end the push within 5 seconds (wait)");
            Assert.Equal(1, failing.CallCount);
            Assert.Equal(Stopped, await rig.Logs.WaitForAnyAsync(Bound, Stopped));
        }
    }

    // (e)
    [Fact]
    public async Task Case_intake_registration_stops_after_the_bounded_attempts()
    {
        // Five answers in order: connection failure, 500, timeout, 503, connection failure. A sixth call would be a 200.
        var handler = new StubRegistrationHandler(async (number, _, ct) =>
        {
            switch (number)
            {
                case 1:
                case 5:
                    throw new HttpRequestException("connection failure");
                case 2:
                    return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                case 3:
                    await Task.Delay(Timeout.Infinite, ct); // the attempt's own cancellation ends it
                    return new HttpResponseMessage(HttpStatusCode.OK);
                case 4:
                    return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                default:
                    return new HttpResponseMessage(HttpStatusCode.OK);
            }
        });
        var delay = new RecordingDelay();

        await using var rig = RegistrationRig.Create(
            handler,
            RegistrationValues.NewKey(),
            RegistrationValues.NewBaseUrl(RegistrationValues.NewToken()),
            attemptTimeout: TimeSpan.FromMilliseconds(200),
            delay: delay);
        await rig.StartAsync();

        Assert.Equal(GaveUp, await rig.Logs.WaitForAnyAsync(Bound, GaveUp, Succeeded));

        // No further call after the give-up.
        await Task.Delay(300);
        Assert.Equal(5, handler.CallCount);
        Assert.Equal(new[] { 2, 4, 8, 16 }, delay.Waits.Select(wait => (int)wait.TotalSeconds).ToArray());
        Assert.Equal(5, rig.Logs.CountOf(Retrying));
        Assert.Equal(1, rig.Logs.CountOf(GaveUp));
    }

    [Fact]
    public void Case_intake_registration_attempt_timeout_defaults_to_five_seconds()
    {
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton(Options.Create(new PvgServiceApiRuntimeOptions(false, false, true)))
            .AddPvgModuleRegistration()
            .BuildServiceProvider();

        var timing = provider.GetRequiredService<PvgModuleRegistrationTiming>();
        Assert.Equal(TimeSpan.FromSeconds(5), timing.AttemptTimeout);
        Assert.Equal(TimeSpan.FromSeconds(5), PvgModuleRegistrationTiming.DefaultAttemptTimeout);
        Assert.NotNull(timing.Delay);
    }

    [Fact]
    public async Task Case_intake_registration_stops_after_success_or_a_refusal()
    {
        (HttpStatusCode Status, string Code)[] answers =
        [
            (HttpStatusCode.OK, Succeeded),
            (HttpStatusCode.Unauthorized, Refused),
            (HttpStatusCode.NotFound, Refused),
            (HttpStatusCode.Found, Refused)
        ];

        foreach (var (status, code) in answers)
        {
            var handler = StubRegistrationHandler.Always(status);
            await using var rig = RegistrationRig.Create(
                handler, RegistrationValues.NewKey(), RegistrationValues.NewBaseUrl(RegistrationValues.NewToken()));
            await rig.StartAsync();

            Assert.Equal(code, await rig.Logs.WaitForAnyAsync(Bound, Succeeded, Refused, Retrying, GaveUp));
            await Task.Delay(300); // a push that went on would have made its next calls by now
            Assert.True(handler.CallCount == 1, $"{(int)status}: {handler.CallCount} call(s)");
            Assert.Equal(0, rig.Logs.CountOf(Retrying));
            Assert.Equal(0, rig.Logs.CountOf(GaveUp));
        }
    }

    // (f)
    [Fact]
    public async Task Case_intake_registration_failure_changes_neither_start_nor_readiness()
    {
        // The reference: both values unset.
        PvgMongoApiHost.ApiResponse unset;
        await using (var host = await RegistrationHost.StartAsync(StubRegistrationHandler.Always(HttpStatusCode.OK), null, null))
        {
            Assert.Equal(SkippedNotConfigured, await host.Logs.WaitForAnyAsync(Bound, SkippedNotConfigured));
            unset = await host.Host.GetReadyAsync();
        }

        Assert.Equal(200, unset.StatusCode);

        // A handler that always fails with a connection error, and one that fails with an exception no table row names.
        var failures = new Func<int, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>[]
        {
            (_, _, _) => throw new HttpRequestException("connection failure"),
            (_, _, _) => throw new InvalidOperationException("unexpected failure")
        };

        foreach (var failure in failures)
        {
            var key = RegistrationValues.NewKey();
            var url = RegistrationValues.NewBaseUrl(RegistrationValues.NewToken());
            await using var host = await RegistrationHost.StartAsync(new StubRegistrationHandler(failure), key, url);

            Assert.Equal(GaveUp, await host.Logs.WaitForAnyAsync(Bound, GaveUp, Succeeded));
            var ready = await host.Host.GetReadyAsync();
            Assert.Equal(unset.StatusCode, ready.StatusCode);
            Assert.Equal(unset.Raw, ready.Raw);
            Assert.Equal(200, (await host.Host.GetReadyAsync()).StatusCode);
        }
    }

    // (g)
    [Fact]
    public async Task Case_intake_registration_logs_neither_key_nor_url()
    {
        var rows = new List<(string Name, RegistrationRig Rig, string Key, string Token, Func<RegistrationRig, Task> Run)>();

        RegistrationRig Build(
            StubRegistrationHandler handler,
            out string key,
            out string token,
            bool productionLike = false,
            string? urlOverride = null)
        {
            key = RegistrationValues.NewKey();
            token = RegistrationValues.NewToken();
            return RegistrationRig.Create(
                handler, key, urlOverride?.Replace("{token}", token) ?? RegistrationValues.NewBaseUrl(token), productionLike);
        }

        // The rows of the state table.
        var invalid = Build(StubRegistrationHandler.Always(HttpStatusCode.OK), out var k1, out var t1, urlOverride: "relative/{token}");
        rows.Add(("invalid url", invalid, k1, t1, rig => WaitAsync(rig, SkippedNotConfigured)));

        var production = Build(StubRegistrationHandler.Always(HttpStatusCode.OK), out var k2, out var t2, productionLike: true);
        rows.Add(("production-like host", production, k2, t2, rig => WaitAsync(rig, SkippedProductionLike)));

        var success = Build(StubRegistrationHandler.Always(HttpStatusCode.OK), out var k3, out var t3);
        rows.Add(("success", success, k3, t3, rig => WaitAsync(rig, Succeeded)));

        var refusal = Build(StubRegistrationHandler.Always(HttpStatusCode.Unauthorized), out var k4, out var t4);
        rows.Add(("refusal", refusal, k4, t4, rig => WaitAsync(rig, Refused)));

        // Give-up with a handler exception whose message holds the address (and the key).
        var gaveUpToken = RegistrationValues.NewToken();
        var gaveUpKey = RegistrationValues.NewKey();
        var gaveUpUrl = RegistrationValues.NewBaseUrl(gaveUpToken);
        var gaveUp = RegistrationRig.Create(
            new StubRegistrationHandler((_, request, _) => throw new HttpRequestException(
                $"Name or service not known ({request.RequestUri}) key {gaveUpKey}")),
            gaveUpKey,
            gaveUpUrl);
        rows.Add(("give-up", gaveUp, gaveUpKey, gaveUpToken, rig => WaitAsync(rig, GaveUp)));

        var unexpectedToken = RegistrationValues.NewToken();
        var unexpectedKey = RegistrationValues.NewKey();
        var unexpected = RegistrationRig.Create(
            new StubRegistrationHandler((_, request, _) => throw new InvalidOperationException(
                $"unexpected ({request.RequestUri}) key {unexpectedKey}")),
            unexpectedKey,
            RegistrationValues.NewBaseUrl(unexpectedToken));
        rows.Add(("give-up (unexpected exception)", unexpected, unexpectedKey, unexpectedToken, rig => WaitAsync(rig, GaveUp)));

        // A stop during the push.
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopToken = RegistrationValues.NewToken();
        var stopKey = RegistrationValues.NewKey();
        var stopRig = RegistrationRig.Create(
            new StubRegistrationHandler(async (_, _, ct) =>
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(ct);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }),
            stopKey,
            RegistrationValues.NewBaseUrl(stopToken));
        rows.Add(("stop during the push", stopRig, stopKey, stopToken, async rig =>
        {
            await rig.StartAsync();
            Assert.True(await RegistrationValues.CompletesWithinAsync(entered.Task, TimeSpan.FromSeconds(5)));
            await RegistrationValues.CompletesWithinAsync(rig.StopAsync(), TimeSpan.FromSeconds(5));
            Assert.Equal(Stopped, await rig.Logs.WaitForAnyAsync(Bound, Stopped));
        }));

        try
        {
            foreach (var (name, rig, key, token, run) in rows)
            {
                await run(rig);

                var text = rig.Logs.AllText;
                Assert.True(rig.Logs.Entries.Count > 0, $"{name}: nothing was captured");
                Assert.False(text.Contains(key, StringComparison.Ordinal), $"{name}: the key reached a log");
                Assert.False(text.Contains(token, StringComparison.Ordinal), $"{name}: the address reached a log");
                Assert.False(rig.Logs.Entries.Any(entry => entry.Exception is not null), $"{name}: an exception reached a log");
            }
        }
        finally
        {
            release.TrySetResult();
            foreach (var row in rows)
            {
                await row.Rig.DisposeAsync();
            }
        }

        static async Task WaitAsync(RegistrationRig rig, string code)
        {
            await rig.StartAsync();
            Assert.Equal(code, await rig.Logs.WaitForAnyAsync(Bound, code));
        }
    }

    [Fact]
    public async Task Case_intake_registration_logs_the_safe_code_of_each_state()
    {
        // Each row: handler, values, expected codes in order.
        async Task<IReadOnlyList<string>> RunAsync(
            StubRegistrationHandler handler,
            string? key,
            string? url,
            bool productionLike,
            string untilCode)
        {
            await using var rig = RegistrationRig.Create(handler, key, url, productionLike);
            await rig.StartAsync();
            Assert.Equal(untilCode, await rig.Logs.WaitForAnyAsync(Bound, untilCode));
            await Task.Delay(100);
            return rig.Logs.CodesOf("PVG_MODULE_REGISTRATION_").ToArray();
        }

        var key = RegistrationValues.NewKey();
        var url = RegistrationValues.NewBaseUrl(RegistrationValues.NewToken());

        Assert.Equal(
            new[] { SkippedNotConfigured },
            await RunAsync(StubRegistrationHandler.Always(HttpStatusCode.OK), key, "relative/path", false, SkippedNotConfigured));
        Assert.Equal(
            new[] { SkippedProductionLike },
            await RunAsync(StubRegistrationHandler.Always(HttpStatusCode.OK), key, url, true, SkippedProductionLike));
        Assert.Equal(
            new[] { Succeeded },
            await RunAsync(StubRegistrationHandler.Always(HttpStatusCode.OK), key, url, false, Succeeded));
        Assert.Equal(
            new[] { Refused },
            await RunAsync(StubRegistrationHandler.Always(HttpStatusCode.Forbidden), key, url, false, Refused));
        Assert.Equal(
            new[] { Retrying, Retrying, Retrying, Retrying, Retrying, GaveUp },
            await RunAsync(StubRegistrationHandler.Always(HttpStatusCode.BadGateway), key, url, false, GaveUp));

        // Stop during the push.
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var stopRig = RegistrationRig.Create(
            new StubRegistrationHandler(async (_, _, ct) =>
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(ct);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }),
            key,
            url);
        try
        {
            await stopRig.StartAsync();
            Assert.True(await RegistrationValues.CompletesWithinAsync(entered.Task, TimeSpan.FromSeconds(5)));
            await RegistrationValues.CompletesWithinAsync(stopRig.StopAsync(), TimeSpan.FromSeconds(5));
            Assert.Equal(Stopped, await stopRig.Logs.WaitForAnyAsync(Bound, Stopped));
            Assert.Equal(new[] { Stopped }, stopRig.Logs.CodesOf("PVG_MODULE_REGISTRATION_").ToArray());
        }
        finally
        {
            release.TrySetResult();
        }
    }

    // (i)
    [Fact]
    public async Task Case_intake_registration_does_not_follow_a_redirect()
    {
        var requestedPaths = new List<string>();
        var gate = new object();
        using var server = new WebHostBuilder()
            .UseKestrel()
            .UseUrls("http://127.0.0.1:0")
            .Configure(app => app.Run(async context =>
            {
                lock (gate)
                {
                    requestedPaths.Add(context.Request.Path.Value ?? string.Empty);
                }

                if (context.Request.Path.Value == RegistrationValues.RegisterPath)
                {
                    context.Response.StatusCode = StatusCodes.Status307TemporaryRedirect;
                    context.Response.Headers.Location = "/second";
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status200OK;
                await context.Response.WriteAsync("second");
            }))
            .Build();
        await server.StartAsync();

        try
        {
            var address = server.ServerFeatures.Get<IServerAddressesFeature>()!.Addresses.Single();
            Assert.StartsWith("http://127.0.0.1:", address, StringComparison.Ordinal);

            // The client is the host's own: the primary handler is NOT replaced, because AllowAutoRedirect lives on it.
            await using var rig = RegistrationRig.CreateWithRealHandler(RegistrationValues.NewKey(), address);
            await rig.StartAsync();

            Assert.Equal(Refused, await rig.Logs.WaitForAnyAsync(Bound, Refused, Succeeded, GaveUp));
            await Task.Delay(200);

            string[] seen;
            lock (gate)
            {
                seen = requestedPaths.ToArray();
            }

            Assert.Equal(new[] { RegistrationValues.RegisterPath }, seen);
        }
        finally
        {
            await server.StopAsync();
        }
    }
}
