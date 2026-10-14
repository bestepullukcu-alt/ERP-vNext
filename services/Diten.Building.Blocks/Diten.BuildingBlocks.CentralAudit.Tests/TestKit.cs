using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Diten.BuildingBlocks.CentralAudit.Tests;

/// <summary>
/// The production registration (<see cref="CentralAuditServiceCollectionExtensions.AddCentralAudit{TScope}"/>) with the
/// real MediatR pipeline; only the two edges are replaced: the Mongo outbox by an in-memory one that keeps rows exactly
/// as the store contract says (the Mongo store itself runs in the Inventory suite over a replica set), and Platform by a
/// scripted HTTP handler.
/// </summary>
internal sealed class Harness : IDisposable
{
    public static readonly Guid Tenant = Guid.Parse("50150150-0000-4000-8000-000000000001");
    public static readonly Guid OtherTenant = Guid.Parse("50150150-0000-4000-8000-000000000002");
    public static readonly Guid Company = Guid.Parse("50150150-0000-4000-8000-0000000000c1");
    public static readonly Guid User = Guid.Parse("50150150-0000-4000-8000-0000000000a1");
    public static readonly Guid Correlation = Guid.Parse("50150150-0000-4000-8000-0000000000f1");
    public const string Key = "central-audit-unit-test-key";

    private readonly ServiceProvider _provider;

    public Harness(bool configured = true, bool resolvedScope = true, Action<IServiceCollection>? configure = null, bool requireEnlistment = false)
    {
        var settings = new Dictionary<string, string?>
        {
            ["CentralAudit:DispatcherEnabled"] = "false",
            ["CentralAudit:BaseBackoff"] = "00:00:05"
        };
        if (configured)
        {
            settings["PlatformRegistration:BaseUrl"] = "http://platform.test";
            settings["PlatformRegistration:InternalApiKey"] = Key;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(Logs).SetMinimumLevel(LogLevel.Trace));
        services.AddSingleton<IConfiguration>(configuration);
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Harness).Assembly));
        services.AddSingleton(new TestScopeState { IsResolved = resolvedScope });

        services.AddCentralAudit<TestScope>(configuration, "Diten.TestService", options => options.RequireEnlistment = requireEnlistment);

        services.AddSingleton(Store);
        services.Replace(ServiceDescriptor.Scoped<ICentralAuditOutboxStore>(sp => sp.GetRequiredService<InMemoryOutboxStore>()));
        services.Replace(ServiceDescriptor.Singleton<TimeProvider>(Clock));
        services.AddHttpClient(PlatformCentralAuditClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => Platform);
        configure?.Invoke(services);
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public InMemoryOutboxStore Store { get; } = new();
    public ScriptedPlatform Platform { get; } = new();
    public ManualClock Clock { get; } = new(new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero));
    public CapturingLogs Logs { get; } = new();

    public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
    {
        using var scope = _provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    public Task<int> DispatchAsync() => _provider.GetRequiredService<CentralAuditDispatcher>().RunOnceAsync(CancellationToken.None);

    public T Get<T>() where T : notnull => _provider.GetRequiredService<T>();

    public CentralAuditHealthCheck Health() => ActivatorUtilities.CreateInstance<CentralAuditHealthCheck>(_provider);

    public void Dispose() => _provider.Dispose();
}

internal sealed class TestScopeState
{
    public bool IsResolved { get; set; } = true;
}

/// <summary>What a service's adapter does: read the caller's RESOLVED scope — here a fixed caller.</summary>
internal sealed class TestScope(TestScopeState state) : ICentralAuditScope
{
    public bool IsResolved => state.IsResolved;
    public Guid TenantId => Harness.Tenant;
    public Guid? CompanyId => Harness.Company;
    public Guid? ActorId => Harness.User;
    public string? ActorDisplayName => "Ayşe Denetçi";
    public string ActorType => CentralAuditActorTypes.TenantUser;
    public Guid CorrelationId => Harness.Correlation;
}

/// <summary>
/// The store contract in memory. "In a transaction" is modelled the way Mongo behaves: an enlisted row becomes visible
/// only when the test commits the fake session; an aborted session leaves nothing.
/// </summary>
internal sealed class InMemoryOutboxStore : ICentralAuditOutboxStore
{
    private readonly ConcurrentDictionary<Guid, CentralAuditOutboxRecord> _rows = new();
    private readonly ConcurrentDictionary<FakeSession, List<CentralAuditOutboxRecord>> _uncommitted = new();

    public IReadOnlyList<CentralAuditOutboxRecord> Rows => _rows.Values.OrderBy(r => r.CreatedAtUtc).ToList();

    public bool FailInserts { get; set; }

    public Task InsertInTransactionAsync(IClientSessionHandle session, CentralAuditOutboxRecord record, CancellationToken ct)
    {
        var fake = FakeSession.Of(session);
        if (_rows.ContainsKey(record.Id) || _uncommitted.GetOrAdd(fake, _ => []).Any(r => r.Id == record.Id))
        {
            return Task.CompletedTask;
        }

        _uncommitted[fake].Add(record.Copy());
        fake.OnEnd = commit =>
        {
            if (commit && _uncommitted.TryRemove(fake, out var rows))
            {
                rows.ForEach(r => _rows.TryAdd(r.Id, r));
            }
            else
            {
                _uncommitted.TryRemove(fake, out _);
            }
        };
        return Task.CompletedTask;
    }

    public Task InsertAsync(CentralAuditOutboxRecord record, CancellationToken ct)
    {
        if (FailInserts)
        {
            throw new InvalidOperationException("store down (test)");
        }

        _rows.TryAdd(record.Id, record.Copy());
        return Task.CompletedTask;
    }

    public Task SetEntityIdIfMissingAsync(Guid id, Guid entityId, CancellationToken ct)
    {
        if (_rows.TryGetValue(id, out var row) && row.EntityId is null)
        {
            row.EntityId = entityId;
        }

        return Task.CompletedTask;
    }

    public Task ConfirmAsync(Guid id, CancellationToken ct) => Move(id, CentralAuditOutboxStatus.Pending);

    public Task AbandonAsync(Guid id, CancellationToken ct) => Move(id, CentralAuditOutboxStatus.Abandoned);

    private Task Move(Guid id, CentralAuditOutboxStatus to)
    {
        if (_rows.TryGetValue(id, out var row) && row.Status == CentralAuditOutboxStatus.AwaitingCommand)
        {
            row.Status = to;
        }

        return Task.CompletedTask;
    }

    public Task<long> ReleaseOrphansAsync(DateTime createdBeforeUtc, CancellationToken ct)
    {
        var orphans = _rows.Values.Where(r => r.Status == CentralAuditOutboxStatus.AwaitingCommand && r.CreatedAtUtc < createdBeforeUtc).ToList();
        orphans.ForEach(r => r.Status = CentralAuditOutboxStatus.Pending);
        return Task.FromResult((long)orphans.Count);
    }

    public Task<IReadOnlyList<CentralAuditOutboxRecord>> GetDueAsync(DateTime nowUtc, int batchSize, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<CentralAuditOutboxRecord>>(_rows.Values
            .Where(r => r.Status == CentralAuditOutboxStatus.Pending && r.NextAttemptAtUtc <= nowUtc)
            .OrderBy(r => r.NextAttemptAtUtc).Take(batchSize).Select(r => r.Copy()).ToList());

    public Task MarkDeliveredAsync(Guid id, DateTime nowUtc, CancellationToken ct)
    {
        var row = _rows[id];
        row.Status = CentralAuditOutboxStatus.Delivered;
        row.DeliveredAtUtc = nowUtc;
        row.Attempts++;
        return Task.CompletedTask;
    }

    public Task MarkRetryAsync(Guid id, int attempts, DateTime nextAttemptAtUtc, string error, CancellationToken ct)
    {
        var row = _rows[id];
        row.Attempts = attempts;
        row.NextAttemptAtUtc = nextAttemptAtUtc;
        row.LastError = error;
        return Task.CompletedTask;
    }

    public Task MarkRejectedAsync(Guid id, int attempts, string error, CancellationToken ct)
    {
        var row = _rows[id];
        row.Status = CentralAuditOutboxStatus.Rejected;
        row.Attempts = attempts;
        row.LastError = error;
        return Task.CompletedTask;
    }

    public Task<CentralAuditBacklog> GetBacklogAsync(CancellationToken ct)
    {
        var pending = _rows.Values.Where(r => r.Status == CentralAuditOutboxStatus.Pending).ToList();
        return Task.FromResult(new CentralAuditBacklog(
            pending.Count,
            pending.Count == 0 ? null : pending.Min(r => r.CreatedAtUtc),
            _rows.Values.Count(r => r.Status == CentralAuditOutboxStatus.Rejected)));
    }

    public Task CreateIndexesAsync(CancellationToken ct) => Task.CompletedTask;
}

/// <summary>
/// A session handle the in-memory store recognises; <see cref="Commit"/> / <see cref="Abort"/> decide whether the rows
/// enlisted in it exist. A <see cref="DispatchProxy"/> over the driver interface: the store only needs its identity.
/// </summary>
internal sealed class FakeSession
{
    private static readonly ConditionalWeakTable<IClientSessionHandle, FakeSession> ByHandle = new();

    private FakeSession()
    {
        Handle = DispatchProxy.Create<IClientSessionHandle, NotSupportedProxy>();
        ByHandle.Add(Handle, this);
    }

    public IClientSessionHandle Handle { get; }

    public Action<bool>? OnEnd { get; set; }

    public static FakeSession Start() => new();

    public static FakeSession Of(IClientSessionHandle handle) => ByHandle.TryGetValue(handle, out var fake)
        ? fake
        : throw new InvalidOperationException("not a test session");

    public void Commit() => OnEnd?.Invoke(true);

    public void Abort() => OnEnd?.Invoke(false);
}

public class NotSupportedProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        targetMethod?.Name is "GetHashCode" ? 0 : throw new NotSupportedException(targetMethod?.Name);
}

internal sealed class ScriptedPlatform : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _script = new();

    public List<(HttpRequestMessage Request, string Body)> Calls { get; } = [];

    public void Answer(HttpStatusCode status, string body = "{}") => _script.Enqueue(() => new HttpResponseMessage(status) { Content = new StringContent(body) });

    public void Throw() => _script.Enqueue(() => throw new HttpRequestException("connection refused (test)"));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls.Add((request, request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));
        return _script.Count == 0 ? new HttpResponseMessage(HttpStatusCode.Accepted) : _script.Dequeue()();
    }
}

internal sealed class ManualClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

internal sealed class CapturingLogs : ILoggerProvider
{
    public ConcurrentQueue<(LogLevel Level, string Message)> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new Logger(this);

    public void Dispose() { }

    private sealed class Logger(CapturingLogs owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            owner.Entries.Enqueue((logLevel, formatter(state, exception)));
    }
}
