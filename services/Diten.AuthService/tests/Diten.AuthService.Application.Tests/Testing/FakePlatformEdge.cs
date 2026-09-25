using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Tests.Testing;

/// <summary>
/// BL-456/459 — a fake of Platform's internal S2S surface that AuthService's user writes call: it RECORDS every request
/// (method, path, the internal-key header, the JSON body) and answers like Platform does. It replaces only the primary
/// handler of the production typed clients — the clients, their base address, headers and JSON are the real code.
/// <para><see cref="Down"/> = Platform unreachable (the handler throws exactly as a refused connection does).</para>
/// </summary>
public sealed class FakePlatformEdge
{
    public sealed record Call(string Method, string Path, bool HasInternalKey, JsonElement Body);

    private readonly ConcurrentQueue<Call> _calls = new();

    /// <summary>When true every call fails with a connection error, like a stopped Platform.</summary>
    public bool Down { get; set; }

    /// <summary>
    /// Answers for a path (after recording). Unset paths answer 202 with an empty envelope. A test sets it to shape
    /// Platform's reply (e.g. the quota 409) and resets it to null after.
    /// </summary>
    public Func<Call, HttpResponseMessage?>? Responder { get; set; }

    public IReadOnlyList<Call> Calls => _calls.ToArray();

    public IReadOnlyList<Call> CallsTo(string path) => _calls.Where(c => c.Path == path).ToArray();

    public HttpMessageHandler NewHandler() => new Handler(this);

    /// <summary>What Platform answers when all is well: audit queued, a quota seat taken / given back.</summary>
    public static HttpResponseMessage DefaultAnswer(Call call) => call.Path switch
    {
        "/api/internal/quotas/consume" or "/api/internal/quotas/release" =>
            Json(HttpStatusCode.OK, new { data = new { applied = true }, statusCode = 200, isSuccessful = true, errors = Array.Empty<string>() }),
        _ => Json(HttpStatusCode.Accepted, new { status = "Queued" })
    };

    public static HttpResponseMessage Json(HttpStatusCode status, object body) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
    };

    private sealed class Handler(FakePlatformEdge edge) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (edge.Down)
            {
                throw new HttpRequestException("Connection refused (FakePlatformEdge.Down)");
            }

            var raw = request.Content is null ? "{}" : await request.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw);
            var call = new Call(
                request.Method.Method,
                request.RequestUri?.AbsolutePath ?? string.Empty,
                request.Headers.TryGetValues("X-Internal-Api-Key", out var keys) && keys.Any(k => !string.IsNullOrWhiteSpace(k)),
                doc.RootElement.Clone());
            edge._calls.Enqueue(call);

            return edge.Responder?.Invoke(call) ?? DefaultAnswer(call);
        }
    }
}

/// <summary>
/// The acceptance host with its OUTBOUND edges replaced: Platform (the audit forwarder's and the quota client's typed
/// clients keep their production registration; only the network under them is the <see cref="FakePlatformEdge"/>), the
/// invitation e-mail (recorded, never sent) — and a log sink on the quota client, so a test can read its warnings.
/// </summary>
public sealed class PlatformEdgeTestHost : AccountKindAcceptance.AuthTestHost
{
    public FakePlatformEdge Platform { get; } = new();
    public RecordingInvitationEmails Emails { get; } = new();
    public CapturingLoggerProvider Logs { get; } = new();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddHttpClient<IPlatformAuditForwarder, PlatformAuditForwarder>()
            .ConfigurePrimaryHttpMessageHandler(() => Platform.NewHandler());
        services.AddHttpClient<IUserQuotaClient, PlatformUserQuotaClient>()
            .ConfigurePrimaryHttpMessageHandler(() => Platform.NewHandler());
        services.AddScoped<ITenantUserInvitationEmailService>(_ => Emails);
        // The Api logs through Serilog (UseSerilog replaces the provider list), so the sink is the quota client's own
        // ILogger<T> — the closed registration wins over the open-generic one.
        services.AddSingleton<ILogger<PlatformUserQuotaClient>>(Logs.CreateLogger<PlatformUserQuotaClient>());
    }
}

public sealed class RecordingInvitationEmails : ITenantUserInvitationEmailService
{
    private readonly ConcurrentQueue<string> _sent = new();

    public IReadOnlyList<string> SentTo => _sent.ToArray();

    public string BuildTenantSetPasswordUrl(string email, string setupToken) => "http://localhost/set-password?test";

    public Task SendTenantUserInvitationAsync(string email, string setupToken, CancellationToken ct)
    {
        _sent.Enqueue(email);
        return Task.CompletedTask;
    }
}

public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<(LogLevel Level, string Category, string Message)> _entries = new();

    public IReadOnlyList<(LogLevel Level, string Category, string Message)> Entries => _entries.ToArray();

    public ILogger CreateLogger(string categoryName) => new Sink(this, categoryName);

    public ILogger<T> CreateLogger<T>() => new TypedSink<T>(new Sink(this, typeof(T).FullName ?? typeof(T).Name));

    private sealed class TypedSink<T>(ILogger inner) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => inner.BeginScope(state);
        public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => inner.Log(logLevel, eventId, state, exception, formatter);
    }

    public void Dispose()
    {
    }

    private sealed class Sink(CapturingLoggerProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel)) owner._entries.Enqueue((logLevel, category, formatter(state, exception)));
        }
    }
}
