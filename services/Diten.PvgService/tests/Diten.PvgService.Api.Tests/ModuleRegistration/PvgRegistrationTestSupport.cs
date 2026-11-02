using System.Collections.Concurrent;
using System.Net;
using Diten.PvgService.Api.ModuleRegistration;
using Diten.PvgService.Api.Tests.Persistence;
using Diten.PvgService.Application.RegPvBase;
using Diten.PvgService.Infrastructure.RegPvBase;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.PvgService.Api.Tests.ModuleRegistration;

// WP-0230-DL11, AC-37: the test doubles of the closed catalog registration. Nothing here opens a network connection
// (the one loopback server lives in the redirect test), writes the process environment or holds a real key or address.

internal sealed record CapturedRequest(string Method, string Uri, string? ApiKeyHeader, IReadOnlyList<string> HeaderNames, string Body);

// A stub primary handler that counts and records every call, then lets the test decide the answer. The call number
// starts at 1.
internal sealed class StubRegistrationHandler : HttpMessageHandler
{
    private readonly Func<int, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;
    private readonly List<CapturedRequest> _requests = [];
    private readonly object _gate = new();

    public StubRegistrationHandler(Func<int, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        _responder = responder;
    }

    public static StubRegistrationHandler Always(HttpStatusCode status) =>
        new((_, _, _) => Task.FromResult(new HttpResponseMessage(status)));

    public int CallCount
    {
        get
        {
            lock (_gate)
            {
                return _requests.Count;
            }
        }
    }

    public IReadOnlyList<CapturedRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return _requests.ToArray();
            }
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(CancellationToken.None);
        var key = request.Headers.TryGetValues("X-Internal-Api-Key", out var values) ? string.Join(",", values) : null;
        int number;
        lock (_gate)
        {
            _requests.Add(new CapturedRequest(
                request.Method.Method,
                request.RequestUri?.ToString() ?? string.Empty,
                key,
                request.Headers.Select(header => header.Key).ToArray(),
                body));
            number = _requests.Count;
        }

        return await _responder(number, request, cancellationToken);
    }
}

internal sealed class FakeRegistrationEnvironment : IPvgModuleRegistrationEnvironment
{
    private readonly string? _key;
    private readonly string? _baseUrl;
    private int _reads;

    public FakeRegistrationEnvironment(string? key, string? baseUrl)
    {
        _key = key;
        _baseUrl = baseUrl;
    }

    public int Reads => Volatile.Read(ref _reads);

    public string? InternalApiKey
    {
        get
        {
            Interlocked.Increment(ref _reads);
            return _key;
        }
    }

    public string? BaseUrl
    {
        get
        {
            Interlocked.Increment(ref _reads);
            return _baseUrl;
        }
    }
}

// The injected wait. It records the requested waits and returns at once, or - when asked to - blocks until cancelled.
internal sealed class RecordingDelay
{
    private readonly List<TimeSpan> _waits = [];
    private readonly object _gate = new();
    private readonly TaskCompletionSource _waiting = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool BlockUntilCancelled { get; init; }

    public Task Waiting => _waiting.Task;

    public IReadOnlyList<TimeSpan> Waits
    {
        get
        {
            lock (_gate)
            {
                return _waits.ToArray();
            }
        }
    }

    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _waits.Add(delay);
        }

        _waiting.TrySetResult();
        if (BlockUntilCancelled)
        {
            return BlockAsync(cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    // Blocks until cancelled, then takes a moment to unwind: a StopAsync that does not wait for the push returns before
    // the push has logged its stop code, so the test can tell the two apart.
    private static async Task BlockAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await Task.Delay(300, CancellationToken.None);
            throw;
        }
    }
}

internal sealed record CapturedLog(string Category, LogLevel Level, string Message, string? Exception, string State)
{
    public string AllText => string.Join("\n", Category, Message, Exception ?? string.Empty, State);
}

// A capturing logger provider for every category. It is given the lowest level through the general minimum level; no
// provider-specific rule is ever written, because a provider-specific rule beats the general category filter and would
// make the log test measure the wrong thing (F-12).
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<CapturedLog> _entries = new();

    public IReadOnlyList<CapturedLog> Entries => _entries.ToArray();

    public string AllText => string.Join("\n", Entries.Select(entry => entry.AllText));

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _entries);

    public void Dispose()
    {
    }

    public IEnumerable<string> CodesOf(string codePrefix) =>
        Entries
            .Where(entry => entry.Category.EndsWith(nameof(PvgModuleRegistrationHostedService), StringComparison.Ordinal))
            .Select(entry => entry.Message.Split(' ')[0])
            .Where(code => code.StartsWith(codePrefix, StringComparison.Ordinal));

    public int CountOf(string code) => CodesOf(code).Count(found => found == code);

    // Waits for one of the codes (bounded), polling; returns the code found or null on timeout.
    public async Task<string?> WaitForAnyAsync(TimeSpan timeout, params string[] codes)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var found = CodesOf("PVG_MODULE_REGISTRATION_").FirstOrDefault(codes.Contains);
            if (found is not null)
            {
                return found;
            }

            await Task.Delay(10);
        }

        return null;
    }

    private sealed class CapturingLogger(string category, ConcurrentQueue<CapturedLog> entries) : ILogger
    {
        // A scope is captured like an entry (level None), so a scope that carries the key or the address is found too.
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            entries.Enqueue(new CapturedLog(category, LogLevel.None, "scope: " + state, null, Describe(state)));
            return null;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        private static string Describe<TState>(TState state) =>
            state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? string.Join(";", pairs.Select(pair => pair.Key + "=" + pair.Value))
                : state?.ToString() ?? string.Empty;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var values = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? string.Join(";", pairs.Select(pair => pair.Key + "=" + pair.Value))
                : state?.ToString() ?? string.Empty;
            entries.Enqueue(new CapturedLog(category, logLevel, formatter(state, exception), exception?.ToString(), values));
        }
    }
}

internal static class RegistrationValues
{
    public const string RegisterPath = "/api/internal/module-catalog/register-manifest";

    // Values are produced per run; nothing here is a real key or a real address (".invalid" never resolves).
    public static string NewKey() => "fake-key-" + Guid.NewGuid().ToString("N");

    public static string NewToken() => Guid.NewGuid().ToString("N");

    public static string NewBaseUrl(string token) => "http://pvg-test-" + token + ".invalid";

    public static async Task<bool> UntilAsync(Func<bool> condition, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(10);
        }

        return condition();
    }

    // Bounded await: true when the task completed within the limit.
    public static async Task<bool> CompletesWithinAsync(Task task, TimeSpan limit)
    {
        var finished = await Task.WhenAny(task, Task.Delay(limit));
        return ReferenceEquals(finished, task);
    }
}

// The hosted service built from the host's own registration (AddPvgModuleRegistration), with the reader, the wait, the
// attempt timeout and the primary handler replaced, and a capturing provider at the lowest level.
internal sealed class RegistrationRig : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    private RegistrationRig(
        ServiceProvider provider,
        StubRegistrationHandler handler,
        CapturingLoggerProvider logs,
        FakeRegistrationEnvironment environment,
        RecordingDelay delay,
        IHostedService service)
    {
        _provider = provider;
        Handler = handler;
        Logs = logs;
        Environment = environment;
        Delay = delay;
        Service = service;
    }

    public StubRegistrationHandler Handler { get; }

    public CapturingLoggerProvider Logs { get; }

    public FakeRegistrationEnvironment Environment { get; }

    public RecordingDelay Delay { get; }

    public IHostedService Service { get; }

    public IServiceProvider Services => _provider;

    public static RegistrationRig Create(
        StubRegistrationHandler handler,
        string? key,
        string? baseUrl,
        bool productionLike = false,
        TimeSpan? attemptTimeout = null,
        RecordingDelay? delay = null,
        bool replacePrimaryHandler = true)
    {
        var logs = new CapturingLoggerProvider();
        var environment = new FakeRegistrationEnvironment(key, baseUrl);
        delay ??= new RecordingDelay();

        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(logs);
        });
        services.AddSingleton(Options.Create(new PvgServiceApiRuntimeOptions(productionLike, false, !productionLike)));
        services.AddPvgModuleRegistration();
        services.Replace(ServiceDescriptor.Singleton<IPvgModuleRegistrationEnvironment>(environment));
        services.Replace(ServiceDescriptor.Singleton(
            new PvgModuleRegistrationTiming(delay.DelayAsync, attemptTimeout ?? TimeSpan.FromSeconds(5))));
        if (replacePrimaryHandler)
        {
            services
                .AddHttpClient(PvgModuleRegistrationServiceCollectionExtensions.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => handler);
        }

        var provider = services.BuildServiceProvider();
        var service = provider.GetServices<IHostedService>().Single(hosted => hosted is PvgModuleRegistrationHostedService);
        return new RegistrationRig(provider, handler, logs, environment, delay, service);
    }

    // The host's own client, primary handler included: the redirect test measures AllowAutoRedirect, which lives there.
    public static RegistrationRig CreateWithRealHandler(string key, string baseUrl) =>
        Create(StubRegistrationHandler.Always(HttpStatusCode.OK), key, baseUrl, replacePrimaryHandler: false);

    public Task StartAsync() => Service.StartAsync(CancellationToken.None);

    public Task StopAsync() => Service.StopAsync(CancellationToken.None);

    public async ValueTask DisposeAsync()
    {
        try
        {
            await RegistrationValues.CompletesWithinAsync(Service.StopAsync(CancellationToken.None), TimeSpan.FromSeconds(5));
        }
        catch (Exception)
        {
            // The rig is going away.
        }

        await _provider.DisposeAsync();
    }
}

// The real PVG host (loopback, random port, never 5011) with the registration doubles in place.
internal sealed class RegistrationHost : IAsyncDisposable
{
    private RegistrationHost(
        PvgMongoApiHost host,
        StubRegistrationHandler handler,
        CapturingLoggerProvider logs,
        FakeRegistrationEnvironment environment)
    {
        Host = host;
        Handler = handler;
        Logs = logs;
        Environment = environment;
    }

    public PvgMongoApiHost Host { get; }

    public StubRegistrationHandler Handler { get; }

    public CapturingLoggerProvider Logs { get; }

    public FakeRegistrationEnvironment Environment { get; }

    public static async Task<RegistrationHost> StartAsync(
        StubRegistrationHandler handler,
        string? key,
        string? baseUrl,
        bool productionLike = false,
        IEnumerable<KeyValuePair<string, string?>>? extraConfiguration = null,
        TimeSpan? attemptTimeout = null,
        RecordingDelay? delay = null)
    {
        var logs = new CapturingLoggerProvider();
        var environment = new FakeRegistrationEnvironment(key, baseUrl);
        delay ??= new RecordingDelay();

        var settings = new Dictionary<string, string?>();
        if (productionLike)
        {
            settings["Pvg:Runtime:ProductionLike"] = "true";
            settings["Pvg:Runtime:OperationalRuntimeAuthorized"] = "true";
            settings["Pvg:Runtime:ApprovalReference"] = "RTA-MOD0230-001";
        }

        foreach (var pair in extraConfiguration ?? [])
        {
            settings[pair.Key] = pair.Value;
        }

        var configuration = new ConfigurationBuilder()
            .AddConfiguration(PvgMongoApiHost.Configuration("InMemory", null, null))
            .AddInMemoryCollection(settings)
            .Build();

        var host = await PvgMongoApiHost.StartAsync(configuration, services =>
        {
            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddProvider(logs);
            });
            if (productionLike)
            {
                // A production-like host registers no store and no deny adapter itself; the four allow stubs come from
                // PvgMongoApiHost.Build.
                services.AddSingleton<IPvgIntakeDraftStore, InMemoryPvgIntakeDraftRepository>();
            }

            services.Replace(ServiceDescriptor.Singleton<IPvgModuleRegistrationEnvironment>(environment));
            services.Replace(ServiceDescriptor.Singleton(
                new PvgModuleRegistrationTiming(delay.DelayAsync, attemptTimeout ?? TimeSpan.FromSeconds(5))));
            services
                .AddHttpClient(PvgModuleRegistrationServiceCollectionExtensions.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => handler);
        });

        return new RegistrationHost(host, handler, logs, environment);
    }

    public async ValueTask DisposeAsync() => await Host.DisposeAsync();
}
