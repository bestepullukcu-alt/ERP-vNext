using System.Net;
using System.Reflection;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Servers;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// ABB FIX3 — a deterministic interleaving point. The first call that reaches it stops: <see cref="Reached"/> completes
/// and the call waits for <see cref="Release"/>. Between the two, the test runs a competing request to the end.
/// </summary>
internal sealed class MongoBarrier
{
    private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Reached => _reached.Task.WaitAsync(TimeSpan.FromSeconds(30));

    public void Release() => _release.TrySetResult();

    public async Task ArriveAsync()
    {
        _reached.TrySetResult();
        await _release.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }
}

/// <summary>
/// ABB FIX3 — taps on the real driver objects a repository is built over: a database whose named collection holds (and
/// optionally fails) its first call of one method inside a transaction, or whose client hands out sessions that fail to
/// open or lose their commit answer. Everything else goes straight to the real driver.
/// </summary>
internal static class MongoTaps
{
    private static readonly ConnectionId Connection =
        new(new ServerId(new ClusterId(), new DnsEndPoint("localhost", 27017)));

    /// <summary>A server error with the driver's labels, as the driver would raise it.</summary>
    public static MongoCommandException Labelled(int code, string codeName, string label)
    {
        var exception = new MongoCommandException(Connection, codeName, new BsonDocument("update", 1),
            new BsonDocument { ["ok"] = 0, ["code"] = code, ["codeName"] = codeName });
        exception.AddErrorLabel(label);
        return exception;
    }

    /// <summary>
    /// The first call of <paramref name="method"/> on <paramref name="collection"/> made inside a session waits at
    /// <paramref name="barrier"/> (when given), then either goes on or throws <paramref name="thenThrow"/>'s exception.
    /// </summary>
    public static IMongoDatabase HoldFirst(
        IMongoDatabase inner,
        string collection,
        string method,
        MongoBarrier? barrier,
        Func<Exception>? thenThrow = null)
    {
        var armed = 1;
        return DatabaseProxy.Create(inner, (name, targetMethod, args, proceed) =>
        {
            if (name != collection || targetMethod.Name != method || args is not { Length: > 0 }
                || args[0] is not IClientSessionHandle || Interlocked.Exchange(ref armed, 0) == 0)
            {
                return proceed();
            }

            return Held(targetMethod.ReturnType, async () =>
            {
                if (barrier is not null)
                {
                    await barrier.ArriveAsync();
                }

                if (thenThrow is not null)
                {
                    throw thenThrow();
                }

                return proceed();
            });
        });
    }

    /// <summary>Every session commits for real, then reports the commit as failed with <paramref name="failure"/>.</summary>
    public static IMongoDatabase CommitThenThrow(IMongoDatabase inner, Func<Exception> failure)
        => DatabaseProxy.Create(inner, (_, _, _, proceed) => proceed(), ClientProxy.Create(inner.Client, session =>
            SessionProxy.Create(session, async () =>
            {
                await session.CommitTransactionAsync();
                throw failure();
            })));

    /// <summary>No session can be opened.</summary>
    public static IMongoDatabase SessionFails(IMongoDatabase inner, Func<Exception> failure)
        => DatabaseProxy.Create(inner, (_, _, _, proceed) => proceed(), ClientProxy.Create(inner.Client, _ => throw failure()));

    /// <summary>A Task (or Task&lt;T&gt;) of the method's own return type that runs <paramref name="body"/> first.</summary>
    private static object Held(Type returnType, Func<Task<object?>> body)
    {
        if (returnType == typeof(Task))
        {
            return Run(body);
        }

        var result = returnType.GetGenericArguments()[0];
        return typeof(MongoTaps).GetMethod(nameof(RunTyped), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(result).Invoke(null, [body])!;
    }

    private static async Task Run(Func<Task<object?>> body) => await (Task)(await body())!;

    private static async Task<T> RunTyped<T>(Func<Task<object?>> body) => await (Task<T>)(await body())!;

    public delegate object? CollectionHook(string collection, MethodInfo method, object?[]? args, Func<object?> proceed);

    public class DatabaseProxy : DispatchProxy
    {
        private IMongoDatabase _inner = null!;
        private CollectionHook _hook = null!;
        private IMongoClient? _client;

        public static IMongoDatabase Create(IMongoDatabase inner, CollectionHook hook, IMongoClient? client = null)
        {
            var proxy = Create<IMongoDatabase, DatabaseProxy>();
            var state = (DatabaseProxy)(object)proxy;
            state._inner = inner;
            state._hook = hook;
            state._client = client;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod!.Name == "get_Client" && _client is not null)
            {
                return _client;
            }

            var result = Forward(targetMethod, _inner, args);
            if (targetMethod.Name != nameof(IMongoDatabase.GetCollection))
            {
                return result;
            }

            var name = (string)args![0]!;
            var document = targetMethod.GetGenericArguments()[0];
            return typeof(CollectionProxy<>).MakeGenericType(document)
                .GetMethod(nameof(CollectionProxy<object>.Wrap), BindingFlags.Public | BindingFlags.Static)!
                .Invoke(null, [result, name, _hook]);
        }
    }

    public class CollectionProxy<TDocument> : DispatchProxy
    {
        private IMongoCollection<TDocument> _inner = null!;
        private string _name = null!;
        private CollectionHook _hook = null!;

        public static IMongoCollection<TDocument> Wrap(IMongoCollection<TDocument> inner, string name, CollectionHook hook)
        {
            var proxy = Create<IMongoCollection<TDocument>, CollectionProxy<TDocument>>();
            var state = (CollectionProxy<TDocument>)(object)proxy;
            state._inner = inner;
            state._name = name;
            state._hook = hook;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => _hook(_name, targetMethod!, args, () => Forward(targetMethod!, _inner, args));
    }

    public class ClientProxy : DispatchProxy
    {
        private IMongoClient _inner = null!;
        private Func<IClientSessionHandle, IClientSessionHandle> _wrap = null!;

        public static IMongoClient Create(IMongoClient inner, Func<IClientSessionHandle, IClientSessionHandle> wrap)
        {
            var proxy = Create<IMongoClient, ClientProxy>();
            var state = (ClientProxy)(object)proxy;
            state._inner = inner;
            state._wrap = wrap;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod!.Name == nameof(IMongoClient.StartSessionAsync))
            {
                return Open((Task<IClientSessionHandle>)Forward(targetMethod, _inner, args)!);
            }

            return Forward(targetMethod, _inner, args);
        }

        private async Task<IClientSessionHandle> Open(Task<IClientSessionHandle> opening) => _wrap(await opening);
    }

    public class SessionProxy : DispatchProxy
    {
        private IClientSessionHandle _inner = null!;
        private Func<Task> _commit = null!;

        public static IClientSessionHandle Create(IClientSessionHandle inner, Func<Task> commit)
        {
            var proxy = Create<IClientSessionHandle, SessionProxy>();
            var state = (SessionProxy)(object)proxy;
            state._inner = inner;
            state._commit = commit;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => targetMethod!.Name == nameof(IClientSessionHandle.CommitTransactionAsync)
                ? _commit()
                : Forward(targetMethod, _inner, args);
    }

    private static object? Forward(MethodInfo method, object target, object?[]? args)
    {
        try
        {
            return method.Invoke(target, args);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }
}
