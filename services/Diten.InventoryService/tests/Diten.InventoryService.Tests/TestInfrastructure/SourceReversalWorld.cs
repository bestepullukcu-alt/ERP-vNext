using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Inventory.Ports;
using Diten.InventoryService.Application.Features.Quarantine;
using Diten.InventoryService.Application.Features.Quarantine.Commands;
using Diten.InventoryService.Application.Features.Quarantine.Ports;
using Diten.InventoryService.Application.Features.Quarantine.Services;
using Diten.InventoryService.Application.Features.Traceability.Handlers.CommandHandlers;
using Diten.InventoryService.Application.Features.Traceability.Ports;
using Diten.InventoryService.Domain.Quarantine.Repositories;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Traceability.Repositories;
using Diten.InventoryService.Persistence.Quarantine;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>A decorated call the hooks see (service, method, arguments; the result after the call).</summary>
public sealed record HookCall(Type Service, string Method, object?[] Args, object? Result = null)
{
    public T Arg<T>(int index) => (T)Args[index]!;
}

/// <summary>
/// MOD-0175-FU01 test decorators (pack §17: "üretim koduna test kancası eklenmez — dekoratör InventoryApiFactory DI'ında"). The
/// production services are wrapped in DI by <see cref="HookedQuarantineApiFactory"/>; a test sets <see cref="Before"/> /
/// <see cref="After"/> to stage a race window (block, run the close, count, fail) and clears them in <c>finally</c>. Unset = pass-through.
/// </summary>
public sealed class ReversalHooks
{
    public Func<HookCall, Task>? Before { get; set; }

    public Func<HookCall, Task>? After { get; set; }

    /// <summary>A staged answer in place of the real call (e.g. a start failure of the approval port); null = call the real service.</summary>
    public Func<HookCall, object?>? Instead { get; set; }

    public void Clear()
    {
        Before = null;
        After = null;
        Instead = null;
    }

    /// <summary>Runs <paramref name="work"/> on a clean execution context (no ambient central-audit recorder / system-actor frame of the paused caller).</summary>
    public static Task Detached(Func<Task> work)
    {
        using (ExecutionContext.SuppressFlow())
        {
            return Task.Run(work);
        }
    }
}

/// <summary>A pass-through proxy that runs the <see cref="ReversalHooks"/> around every async call (and wraps the quarantine write session).</summary>
public class HookProxy<T> : DispatchProxy where T : class
{
    private static readonly MethodInfo CallGenericMethod = typeof(HookProxy<T>).GetMethod(nameof(CallGeneric), BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly MethodInfo WrapWorkMethod = typeof(HookProxy<T>).GetMethod(nameof(WrapWork), BindingFlags.NonPublic | BindingFlags.Static)!;

    private T _target = null!;
    private ReversalHooks _hooks = null!;

    public static T Wrap(T target, ReversalHooks hooks)
    {
        var proxy = Create<T, HookProxy<T>>();
        var self = (HookProxy<T>)(object)proxy;
        self._target = target;
        self._hooks = hooks;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        args ??= [];
        if (typeof(T) == typeof(IQuarantineRepository) && method!.Name == nameof(IQuarantineRepository.InTransactionAsync))
        {
            args[0] = WrapWorkMethod.MakeGenericMethod(method.GetGenericArguments()[0]).Invoke(null, [args[0], _hooks]);
        }

        if (method!.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            return CallGenericMethod.MakeGenericMethod(method.ReturnType.GetGenericArguments()[0]).Invoke(this, [method, args]);
        }

        if (method.ReturnType == typeof(Task))
        {
            return CallVoid(method, args);
        }

        return Unwrap(() => method.Invoke(_target, args));
    }

    private async Task<TResult> CallGeneric<TResult>(MethodInfo method, object?[] args)
    {
        if (_hooks.Before is { } before) await before(new HookCall(typeof(T), method.Name, args));
        if (_hooks.Instead?.Invoke(new HookCall(typeof(T), method.Name, args)) is TResult staged) return staged;
        var result = await (Task<TResult>)Unwrap(() => method.Invoke(_target, args))!;
        if (_hooks.After is { } after) await after(new HookCall(typeof(T), method.Name, args, result));
        return result;
    }

    private async Task CallVoid(MethodInfo method, object?[] args)
    {
        if (_hooks.Before is { } before) await before(new HookCall(typeof(T), method.Name, args));
        await (Task)Unwrap(() => method.Invoke(_target, args))!;
        if (_hooks.After is { } after) await after(new HookCall(typeof(T), method.Name, args));
    }

    private static Func<IQuarantineWriteSession, CancellationToken, Task<TX>> WrapWork<TX>(Func<IQuarantineWriteSession, CancellationToken, Task<TX>> work, ReversalHooks hooks)
        => (session, ct) => work(HookProxy<IQuarantineWriteSession>.Wrap(session, hooks), ct);

    private static object? Unwrap(Func<object?> call)
    {
        try
        {
            return call();
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }
}

/// <summary>Counts / faults the dispatches of the two source-reversal commands (MediatR behavior, test assembly only).</summary>
public sealed class SourceReversalDispatches
{
    public ConcurrentQueue<string> Sent { get; } = new();
}

public sealed class SourceReversalCountingBehavior<TRequest, TResponse>(SourceReversalDispatches dispatches) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is IQuarantineSystemActorCommand command)
        {
            dispatches.Sent.Enqueue($"{typeof(TRequest).Name}:{command.ReversalTransactionId}");
        }

        return next();
    }
}

/// <summary>
/// The MOD-0175 host of <see cref="QuarantineApiFactory"/> with the FU01 test decorators in DI (pass-through until a test sets a hook).
/// <see cref="SourceReversalObserver"/> = false is the pack §16 "gözlemcisiz varyant" (kurgu i): the production
/// <c>QuarantineSourceReversalObserver</c> is simply not registered — rule, handler, sweeper are the production ones.
/// </summary>
public class HookedQuarantineApiFactory : QuarantineApiFactory
{
    public ReversalHooks Hooks { get; } = new();

    public SourceReversalDispatches Dispatches { get; } = new();

    /// <summary>The log lines of the FU01 names (<c>quarantine.source_reversal.*</c>, <c>quarantine.intake.*</c>).</summary>
    public ConcurrentQueue<string> Logs { get; } = new();

    protected virtual bool SourceReversalObserver => true;

    /// <summary>AC-14: the intake observer off (a "late" intake: only the intake sweep creates records).</summary>
    protected virtual bool IntakeObserver => true;

    /// <summary>The Workflow approval port with MOD-0023 faked at the HTTP edge (<see cref="FakeWorkflowGateway"/>), instead of the test port.</summary>
    protected virtual bool WorkflowPort => false;

    protected override bool TestApprovalPort => !WorkflowPort;

    public FakeWorkflowGateway Workflow { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            if (WorkflowPort)
            {
                services.AddHttpClient(nameof(IQuarantineWorkflowClient)).ConfigurePrimaryHttpMessageHandler(() => new FakeWorkflowHandler(Workflow));
            }

            foreach (var d in services.Where(d => d.ServiceType == typeof(IPostedMovementObserver)
                                                  && ((!SourceReversalObserver && d.ImplementationType == typeof(QuarantineSourceReversalObserver))
                                                      || (!IntakeObserver && d.ImplementationType == typeof(QuarantineIntakeObserver)))).ToList())
            {
                services.Remove(d);
            }

            services.AddSingleton(Hooks);
            services.AddSingleton(Dispatches);
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(SourceReversalCountingBehavior<,>));
            Decorate<IQuarantineRepository>(services);
            Decorate<IQuarantineDecisionRepository>(services);
            Decorate<IInventoryLedgerRepository>(services);
            Decorate<IApprovedLotChanges>(services);
            Decorate<IQuarantineWorkflowClient>(services);
            Decorate<ILotRepository>(services);
            Decorate<IQuarantineApprovalPort>(services);
            Decorate<ICentralAuditOutboxStore>(services);
            services.AddSingleton<ILoggerProvider>(new CapturingLoggerProvider(Logs));
        });
    }

    private void Decorate<T>(IServiceCollection services) where T : class
    {
        var descriptor = services.Last(d => d.ServiceType == typeof(T));
        services.Remove(descriptor);
        services.Add(new ServiceDescriptor(typeof(T), sp =>
        {
            var inner = descriptor.ImplementationFactory is { } factory
                ? (T)factory(sp)
                : descriptor.ImplementationInstance is T instance
                    ? instance
                    : (T)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType!);
            return HookProxy<T>.Wrap(inner, Hooks);
        }, descriptor.Lifetime));
    }

    private sealed class CapturingLoggerProvider(ConcurrentQueue<string> logs) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new Capturing(logs);

        public void Dispose()
        {
        }

        private sealed class Capturing(ConcurrentQueue<string> logs) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var text = formatter(state, exception);
                if (text.StartsWith("quarantine.source_reversal.", StringComparison.Ordinal) || text.StartsWith("quarantine.intake.", StringComparison.Ordinal)
                    || (logLevel >= LogLevel.Error && exception is not null))
                {
                    logs.Enqueue($"{logLevel}: {text}" + (exception is null ? "" : " | " + exception));
                }
            }
        }
    }
}

/// <summary>Pack §16 kurgu (i): the observer-less host — a REVERSAL commits while the record is still closable, the record moves on after it.</summary>
public sealed class ObserverlessQuarantineApiFactory : HookedQuarantineApiFactory
{
    protected override bool SourceReversalObserver => false;
}

/// <summary>Kurgu (i) on the production Workflow port (refresh, read-time reconciliation, the completion consumer, withdraw).</summary>
public sealed class ObserverlessWorkflowQuarantineApiFactory : HookedQuarantineApiFactory
{
    protected override bool SourceReversalObserver => false;

    protected override bool WorkflowPort => true;
}

/// <summary>AC-14: neither post-commit observer — the intake sweep is the only intake path.</summary>
public sealed class NoObserverQuarantineApiFactory : HookedQuarantineApiFactory
{
    protected override bool SourceReversalObserver => false;

    protected override bool IntakeObserver => false;
}

/// <summary>
/// A one-shot pause for a staged race: the hook that <see cref="TryFire"/>s first holds its caller in <see cref="HoldAsync"/> until the
/// test <see cref="Release"/>s it; every later call passes.
/// </summary>
public sealed class Pause
{
    private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _fired;

    public bool Fired => Volatile.Read(ref _fired) == 1;

    public bool TryFire() => Interlocked.Exchange(ref _fired, 1) == 0;

    public async Task HoldAsync()
    {
        _reached.TrySetResult();
        await _release.Task.WaitAsync(TimeSpan.FromSeconds(90));
    }

    public Task Reached => _reached.Task.WaitAsync(TimeSpan.FromSeconds(90));

    public void Release() => _release.TrySetResult();
}

/// <summary>
/// One MOD-0175-FU01 scenario: a <see cref="QuarantineScenario"/> plus the reversal steps (REVERSAL through the API, or written directly —
/// pack §16 kurgu (ii), a REVERSAL committed before DL-3), the sweep / observer / command entry points and the persisted reads.
/// </summary>
public sealed class ReversalWorld
{
    public ReversalWorld(HookedQuarantineApiFactory factory, bool lotControlled = true)
    {
        Factory = factory;
        S = new QuarantineScenario(factory, lotControlled);
    }

    public HookedQuarantineApiFactory Factory { get; }

    public QuarantineScenario S { get; }

    public sealed record Posted(string Tx, Dictionary<string, object?> Body);

    public static string Id(JsonElement e, string name) => e.GetProperty(name).GetString()!;

    /// <summary>An inbound movement (receipt) into <paramref name="status"/> at bin A (or <paramref name="location"/>).</summary>
    public async Task<Posted> ReceiveAsync(string quantity, string? lot, string status, string movementType = "GOODS_RECEIPT_PO", string? location = null)
    {
        var body = S.Lots.Receipt(quantity, lot, location, movementType, status);
        return new Posted(await S.Lots.PostOkAsync(body), body);
    }

    /// <summary>The REVERSAL of <paramref name="original"/> through <c>postMovement</c> (201 expected); returns its transaction id.</summary>
    public Task<string> ReverseAsync(Posted original) => S.Lots.PostOkAsync(S.Lots.Reversal(original.Body, original.Tx));

    public Task<JsonElement> ReverseExpectingAsync(Posted original, int status) => S.Lots.PostAsync(S.Lots.Reversal(original.Body, original.Tx), status);

    /// <summary>
    /// Kurgu (ii): a REVERSAL document written straight into <c>inventory_transactions</c> (balances untouched — only <c>LedgerPosting</c>
    /// writes those, G-38), shaped like the ledger's own: the original reversed, same tenant / company / item / lot.
    /// </summary>
    public string DirectReversal(string originalTx, TimeSpan? age = null)
    {
        var transactions = Factory.Collection("inventory_transactions");
        var original = transactions.Find(new BsonDocument("_id", originalTx)).Single();
        var reversal = original.DeepClone().AsBsonDocument;
        var id = Guid.NewGuid().ToString("D");
        var at = DateTime.UtcNow - (age ?? TimeSpan.Zero);
        reversal["_id"] = id;
        reversal["MovementType"] = "Reversal";
        reversal["ReversesTransactionId"] = originalTx;
        reversal["TransactionNumber"] = original["TransactionNumber"].AsString + "-R";
        reversal["IdempotencyKey"] = "direct-reversal-" + id;
        reversal["RequestFingerprint"] = "direct-reversal-" + id;
        reversal["SourceModule"] = "MOD-0173";
        reversal["SourceType"] = "REVERSAL";
        reversal["SourceDocumentId"] = originalTx;
        reversal["CorrelationId"] = Guid.NewGuid().ToString("D");
        reversal["CreatedBy"] = S.Ledger.User.ToString("D");
        (reversal["FromWarehouseId"], reversal["ToWarehouseId"]) = (original.GetValue("ToWarehouseId", BsonNull.Value), original.GetValue("FromWarehouseId", BsonNull.Value));
        (reversal["FromLocationId"], reversal["ToLocationId"]) = (original.GetValue("ToLocationId", BsonNull.Value), original.GetValue("FromLocationId", BsonNull.Value));
        (reversal["FromStockStatus"], reversal["ToStockStatus"]) = (original.GetValue("ToStockStatus", BsonNull.Value), original.GetValue("FromStockStatus", BsonNull.Value));
        reversal["CreatedAt"] = at;
        reversal["PostingDate"] = at;
        reversal["TransactionDate"] = at;
        transactions.InsertOne(reversal);
        return id;
    }

    /// <summary>Moves a document's <c>CreatedAt</c> / <c>PostingDate</c> into the past (old data, AC-15).</summary>
    public void Age(string collection, string id, TimeSpan age)
    {
        var at = DateTime.UtcNow - age;
        Factory.Collection(collection).UpdateOne(new BsonDocument("_id", id),
            new BsonDocument("$set", new BsonDocument { ["CreatedAt"] = at, ["PostingDate"] = at, ["TransactionDate"] = at }));
    }

    // ── entry points ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A DI scope with the scenario's tenant, company and user (what the scope middleware sets).</summary>
    public IServiceScope Scope(Guid? user = null)
    {
        var scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetScope(S.Tenant, S.Ledger.LegalEntity, user ?? S.Ledger.User);
        scope.ServiceProvider.GetRequiredService<ICorrelationContext>().Set(Guid.NewGuid());
        return scope;
    }

    /// <summary>One sweep round limited to this tenant (the hosted sweep is off in tests).</summary>
    public Task<QuarantineSourceReversalSweeper.SweepResult> SweepAsync(QuarantineSourceReversalSweeper? sweeper = null)
        => (sweeper ?? Factory.Services.GetRequiredService<QuarantineSourceReversalSweeper>()).SweepAsync(DateTimeOffset.UtcNow.AddMinutes(5), CancellationToken.None, S.Tenant);

    /// <summary>A sweeper of its own with other options (page size, …) — its cursors are its own.</summary>
    public QuarantineSourceReversalSweeper Sweeper(Action<QuarantineSweeperOptions> configure)
    {
        var options = new QuarantineSweeperOptions();
        configure(options);
        return new QuarantineSourceReversalSweeper(Factory.Services.GetRequiredService<IServiceScopeFactory>(), Options.Create(options),
            Factory.Services.GetRequiredService<ILogger<QuarantineSourceReversalSweeper>>());
    }

    /// <summary>The production observer, called by the test with the REVERSAL (the poster's scope).</summary>
    public async Task<IReadOnlyList<Response<SourceReversalResultDto>>> ObserveAsync(string reversalTx)
    {
        using var scope = Scope();
        var sp = scope.ServiceProvider;
        var transaction = (await sp.GetRequiredService<IInventoryLedgerRepository>().FindTransactionAsync(Guid.Parse(reversalTx)))!;
        return await new QuarantineSourceReversalObserver(sp.GetRequiredService<ISender>()).ApplyAsync(transaction);
    }

    public async Task<Response<SourceReversalResultDto>> SendHoldAsync(string reversalTx)
    {
        using var scope = Scope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ApplySourceReversalToHoldCommand(reversalTx));
    }

    // ── persisted state ──────────────────────────────────────────────────────────────────────────────────

    public BsonDocument HoldDoc(string holdId) => S.Holds().Single(h => h["_id"].AsString == holdId);

    public BsonDocument BlockedDoc(string blockedId) => S.Blocked().Single(b => b["_id"].AsString == blockedId);

    public BsonDocument HoldOfSource(string tx) => S.Holds().Single(h => h.Contains("SourceMovementId") && h["SourceMovementId"].IsString && h["SourceMovementId"].AsString == tx);

    public BsonDocument BlockedOfSource(string tx) => S.Blocked().Single(h => h.Contains("SourceMovementId") && h["SourceMovementId"].IsString && h["SourceMovementId"].AsString == tx);

    public List<BsonDocument> History(string subjectId) => S.Ledger.Persisted("quarantine_history").Where(h => h["SubjectId"].AsString == subjectId).ToList();

    public List<string> Actions(string subjectId) => History(subjectId).OrderBy(h => h["AtUtc"].ToUniversalTime()).Select(h => h["Action"].AsString).ToList();

    public List<string> EventKinds(string decisionId)
        => S.Events().Where(e => e["DecisionId"].AsString == decisionId).OrderBy(e => e["Sequence"].ToInt64()).Select(e => e["Kind"].AsString).ToList();

    public List<CentralAuditOutboxRecord> Audit(Guid? entityId = null, string? requestType = null)
    {
        _ = Factory.Services;
        return Factory.Database.GetCollection<CentralAuditOutboxRecord>(CentralAuditOutboxRecord.CollectionName)
            .Find(x => x.TenantId == S.Tenant).ToList()
            .Where(r => r.Status != CentralAuditOutboxStatus.Abandoned)
            .Where(r => entityId is null || r.EntityId == entityId)
            .Where(r => requestType is null || r.RequestType == requestType).ToList();
    }

    public List<CentralAuditOutboxRecord> ReversalAudit(string recordId)
        => Audit(Guid.Parse(recordId)).Where(r => r.RequestType.StartsWith("ApplySourceReversalTo", StringComparison.Ordinal)).ToList();

    public int LedgerCount() => S.Ledger.Persisted("inventory_transactions").Count;

    public List<string> Logs(string name) => Factory.Logs.Where(l => l.Contains(name, StringComparison.Ordinal)).ToList();
}

/// <summary>A barrier the hooks share between real tasks (AC-12: both callers read before either writes).</summary>
public sealed class CountdownGate(int parties)
{
    private int _arrived;
    private readonly TaskCompletionSource _all = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task ArriveAsync()
    {
        if (Interlocked.Increment(ref _arrived) >= parties)
        {
            _all.TrySetResult();
        }

        await _all.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }
}
