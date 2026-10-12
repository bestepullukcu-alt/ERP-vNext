using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

/// <summary>
/// WP-MDM-BL598-01 — a collection's indexes are prepared ONCE per process, lazily, never in a repository's constructor.
///
/// <para><b>Why.</b> Repositories are scoped: an index build in the constructor sent every request's DI resolution to
/// Mongo, and with Mongo down the request died there (a TimeoutException → an uncoded 500) before any handler could give
/// its coded 503.</para>
///
/// <para><b>The rule.</b></para>
/// <list type="bullet">
/// <item>The constructor does not go to Mongo.</item>
/// <item>A WRITE waits for its collection's preparation (<see cref="EnsureForWriteAsync"/>): a UNIQUE index is what keeps two
/// writes from creating two rows of one key, so no write is made without it. A preparation that fails, or does not end
/// within the budget, refuses the write with <see cref="NotReady"/> (fail closed → the caller's coded 503).</item>
/// <item>A READ never waits (<see cref="Start"/> only starts it): an index is for speed there, not for correctness; the
/// read's own Mongo failure is mapped by its caller.</item>
/// <item>A failed or cancelled preparation is kept only for <see cref="FailureCooldown"/> (30 s: the writes meanwhile are
/// refused at once, without a new createIndexes or log line); then the next request starts it again (Mongo coming back
/// heals the process without a restart). It is logged once; a refusal for good (credential / authorization) is its own
/// named failure, not "getting ready".</item>
/// <item>A finished one is re-asserted by the first write after <see cref="Revalidate"/> (an index dropped under a
/// running process comes back within that window).</item>
/// </list>
/// The key is the database + collection + the index set's own name, so a test database and the live one never share one.
/// </summary>
public static class MongoIndexReadiness
{
    public const string NotReady = "MDM_STORE_INDEXES_NOT_READY";

    /// <summary>How long a write waits for a preparation in progress before it is refused (the Mongo client's own server
    /// selection timeout bounds the build itself).</summary>
    public static readonly TimeSpan DefaultWriteBudget = TimeSpan.FromSeconds(10);

    /// <summary>
    /// A finished preparation is re-asserted by the first write after this long (createIndexes on existing indexes is
    /// an idempotent, cheap round trip): a collection or index dropped under a running process is rebuilt within this
    /// window instead of staying missing until a restart (writes would otherwise run without their UNIQUE index).
    /// </summary>
    public static TimeSpan Revalidate { get; internal set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// BL-598 FIX2 (minor): after a FAILED preparation the same key is not tried again for this long — the failure answers
    /// the writes meanwhile (refused, nothing written), without a new createIndexes round trip and without a new log line
    /// for each of them. Then the next write tries again (a failure is still never kept beyond this).
    /// </summary>
    public static TimeSpan FailureCooldown { get; internal set; } = TimeSpan.FromSeconds(30);

    /// <summary>A preparation the server REFUSED for good (authentication / authorization): not "getting ready".</summary>
    public const string PreparationRefused = "MDM_STORE_INDEXES_PREPARATION_REFUSED";

    // BL-598 FIX2 item 4: which preparations were made during a call is told by a sequence number, never by the clock (a
    // clock tick is ~15.6 ms on Windows: an entry finished earlier in the same tick passed for "made by this call").
    private static long _generation;

    private sealed class Entry(long generation, long createdAtMs)
    {
        private int _logged;
        private long _failedAtMs;
        public Lazy<Task> Work { get; set; } = null!;
        public long Generation { get; } = generation;
        public long CreatedAtMs { get; } = createdAtMs;
        public long FailedAtMs => Volatile.Read(ref _failedAtMs);
        public void MarkFailed() => Volatile.Write(ref _failedAtMs, Environment.TickCount64);

        // A failed preparation is said ONCE, by whoever sees it first (the preparation itself, a write, or a later caller).
        public bool TryMarkLogged() => Interlocked.Exchange(ref _logged, 1) == 0;
    }

    private static readonly ConcurrentDictionary<string, Entry> Preparations = new(StringComparer.Ordinal);

    /// <summary>Tests only: how many times a preparation actually started (per key).</summary>
    internal static readonly ConcurrentDictionary<string, int> StartedForTests = new(StringComparer.Ordinal);

    public static string KeyOf<T>(IMongoCollection<T> collection, string indexSet) =>
        $"{collection.Database.DatabaseNamespace.DatabaseName}/{collection.CollectionNamespace.CollectionName}/{indexSet}";

    /// <summary>
    /// Starts the preparation if none is running or done (a read's call: it does not wait). BL-598 FIX2 item 2: it takes
    /// the logger too — a failure of a preparation a read started is said by the preparation itself.
    /// </summary>
    public static void Start(string key, Func<CancellationToken, Task> prepare, ILogger? logger = null) =>
        _ = Current(key, prepare, false, Revalidate, logger);

    /// <summary>
    /// The write's gate: waits for the preparation (started now if needed, or re-asserted when it is older than
    /// <paramref name="maxAge"/>, default <see cref="Revalidate"/>) within the budget. Throws
    /// <see cref="MongoIndexNotReadyException"/> when it failed or did not end in time (tried again after
    /// <see cref="FailureCooldown"/>), <see cref="MongoIndexPreparationRefusedException"/> when the server refused it for
    /// good. <paramref name="refresh"/> forces a new preparation (tests whose database is dropped between them).
    /// </summary>
    public static async Task EnsureForWriteAsync(string key, Func<CancellationToken, Task> prepare, TimeSpan budget,
        CancellationToken cancellationToken, bool refresh = false, TimeSpan? maxAge = null, ILogger? logger = null)
    {
        var entry = Current(key, prepare, refresh, maxAge ?? Revalidate, logger);
        var work = entry.Work.Value;
        try
        {
            await work.WaitAsync(budget, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException exception) when (!work.IsCompleted)
        {
            // Still running past the budget: refused now; the build goes on (and says its own failure, if it fails).
            throw new MongoIndexNotReadyException(key, exception);
        }
        catch (Exception exception)
        {
            LogFailure(entry, key, exception, logger);
            var failure = Unwrap(exception);
            if (IsPermanent(failure)) throw new MongoIndexPreparationRefusedException(key, failure);
            throw new MongoIndexNotReadyException(key, exception);
        }
    }

    /// <summary>
    /// BL-598 FIX1 / FIX2 item 2: a preparation that FAILED (a duplicate key, an index option conflict, an unreachable
    /// server, a refused credential) would otherwise only show as every write's 503 — it is logged once per failure: the
    /// collection and index set, the failure's type and server code. No tenant datum (the key holds the database /
    /// collection).
    /// </summary>
    private static void LogFailure(Entry entry, string key, Exception exception, ILogger? logger)
    {
        if (logger is null || !entry.TryMarkLogged()) return;
        var failure = Unwrap(exception);
        logger.LogError("{Code}: index preparation of {IndexKey} failed ({ExceptionType}, server code {ServerCode}); writes to it are refused until it succeeds.",
            IsPermanent(failure) ? PreparationRefused : NotReady, key, failure.GetType().Name,
            (failure as MongoCommandException)?.CodeName is { Length: > 0 } name ? name : "none");
    }

    private static Exception Unwrap(Exception exception) =>
        exception is AggregateException { InnerException: { } inner } ? inner : exception;

    /// <summary>
    /// BL-598 FIX2 (minor): the server refused the preparation for good — a wrong credential (the driver's authentication
    /// failure) or a user not allowed to create indexes (Unauthorized 13 / AuthenticationFailed 18). Waiting never ends it:
    /// it is a configuration defect (a 500 by its own name), not "getting ready". A server-selection timeout or a
    /// connection / TLS failure stays transient (the driver reports TLS trouble as a connection failure; it may heal).
    /// </summary>
    private static bool IsPermanent(Exception failure) =>
        failure is MongoAuthenticationException
        || failure is MongoCommandException { Code: 13 or 18 };

    private static Entry NewEntry(string key, Func<CancellationToken, Task> prepare, ILogger? logger)
    {
        var entry = new Entry(Interlocked.Increment(ref _generation), Environment.TickCount64);
        entry.Work = new Lazy<Task>(() =>
        {
            StartedForTests.AddOrUpdate(key, 1, (_, count) => count + 1);
            return Task.Run(async () =>
            {
                try
                {
                    await prepare(CancellationToken.None);
                }
                catch (Exception exception)
                {
                    // BL-598 FIX2 item 2: the preparation says its own failure (a read that started it waits for nothing).
                    entry.MarkFailed();
                    LogFailure(entry, key, exception, logger);
                    throw;
                }
            });
        }, LazyThreadSafetyMode.ExecutionAndPublication);
        return entry;
    }

    private static Entry Current(string key, Func<CancellationToken, Task> prepare, bool refresh, TimeSpan maxAge,
        ILogger? logger)
    {
        // Entries made during THIS call (a later sequence number) are never stale for it.
        var calledAt = Interlocked.Read(ref _generation);
        while (true)
        {
            var entry = Preparations.GetOrAdd(key, _ => NewEntry(key, prepare, logger));
            var task = entry.Work.Value;
            var madeDuringThisCall = entry.Generation > calledAt;
            if (task.IsFaulted || task.IsCanceled)
            {
                if (task.Exception is { } failed) LogFailure(entry, key, failed, logger);
                // Cooling down: the failure answers again — no new createIndexes, no new log line.
                if (!refresh && (madeDuringThisCall
                    || Environment.TickCount64 - entry.FailedAtMs < (long)FailureCooldown.TotalMilliseconds))
                    return entry;
            }
            else
            {
                var stale = !madeDuringThisCall && (refresh && task.IsCompleted || task.IsCompletedSuccessfully
                    && Environment.TickCount64 - entry.CreatedAtMs >= (long)maxAge.TotalMilliseconds);
                if (!stale) return entry;
            }
            // A failure past its cool-down, or a finished one due to be re-asserted: dropped, and started again.
            refresh = false;
            Preparations.TryRemove(new KeyValuePair<string, Entry>(key, entry));
        }
    }

    /// <summary>Tests only: forget every preparation (a test that stops and starts Mongo).</summary>
    internal static void ResetForTests()
    {
        Preparations.Clear();
        StartedForTests.Clear();
    }
}

/// <summary>
/// BL-598 FIX2 (minor): the server refused the index preparation for good (credential / authorization) — not caught as
/// "not ready": the write fails as the configuration defect it is (500, logged by its own code).
/// </summary>
public sealed class MongoIndexPreparationRefusedException(string key, Exception inner)
    : InvalidOperationException(MongoIndexReadiness.PreparationRefused, inner)
{
    public string Key { get; } = key;
}

/// <summary>A write refused because its collection's indexes are not (yet) in place — map to a coded 503.</summary>
public sealed class MongoIndexNotReadyException(string key, Exception inner)
    : InvalidOperationException(MongoIndexReadiness.NotReady, inner)
{
    public string Key { get; } = key;
}
