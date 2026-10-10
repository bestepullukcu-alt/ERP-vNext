using System.Collections.Concurrent;
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
/// <item>A failed or cancelled preparation is NOT kept: the next request starts it again (Mongo coming back heals the
/// process without a restart).</item>
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

    private sealed class Entry(Lazy<Task> work, long createdAtMs)
    {
        public Lazy<Task> Work { get; } = work;
        public long CreatedAtMs { get; } = createdAtMs;
    }

    private static readonly ConcurrentDictionary<string, Entry> Preparations = new(StringComparer.Ordinal);

    /// <summary>Tests only: how many times a preparation actually started (per key).</summary>
    internal static readonly ConcurrentDictionary<string, int> StartedForTests = new(StringComparer.Ordinal);

    public static string KeyOf<T>(IMongoCollection<T> collection, string indexSet) =>
        $"{collection.Database.DatabaseNamespace.DatabaseName}/{collection.CollectionNamespace.CollectionName}/{indexSet}";

    /// <summary>Starts the preparation if none is running or done (a read's call: it does not wait).</summary>
    public static void Start(string key, Func<CancellationToken, Task> prepare) => _ = Current(key, prepare, false);

    /// <summary>
    /// The write's gate: waits for the preparation (started now if needed, or re-asserted when it is older than
    /// <see cref="Revalidate"/>) within the budget. Throws <see cref="MongoIndexNotReadyException"/> when it failed or did
    /// not end in time — the failed one is dropped so the next call starts it again. <paramref name="refresh"/> forces a
    /// new preparation (tests whose database is dropped between them).
    /// </summary>
    public static async Task EnsureForWriteAsync(string key, Func<CancellationToken, Task> prepare, TimeSpan budget,
        CancellationToken cancellationToken, bool refresh = false)
    {
        var entry = Current(key, prepare, refresh);
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
            // Still running past the budget: refused now; the build goes on and a later write finds it done (or failed
            // and dropped).
            throw new MongoIndexNotReadyException(key, exception);
        }
        catch (Exception exception)
        {
            Preparations.TryRemove(new KeyValuePair<string, Entry>(key, entry));
            throw new MongoIndexNotReadyException(key, exception);
        }
    }

    private static Entry Current(string key, Func<CancellationToken, Task> prepare, bool refresh)
    {
        while (true)
        {
            var entry = Preparations.GetOrAdd(key, _ => new Entry(new Lazy<Task>(() =>
            {
                StartedForTests.AddOrUpdate(key, 1, (_, count) => count + 1);
                return Task.Run(() => prepare(CancellationToken.None));
            }, LazyThreadSafetyMode.ExecutionAndPublication), Environment.TickCount64));
            var task = entry.Work.Value;
            var stale = refresh && task.IsCompleted || task.IsCompletedSuccessfully
                && Environment.TickCount64 - entry.CreatedAtMs >= (long)Revalidate.TotalMilliseconds;
            if (!task.IsFaulted && !task.IsCanceled && !stale) return entry;
            // A failure already known, or a finished one due to be re-asserted: dropped, and started again for this caller.
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

/// <summary>A write refused because its collection's indexes are not (yet) in place — map to a coded 503.</summary>
public sealed class MongoIndexNotReadyException(string key, Exception inner)
    : InvalidOperationException(MongoIndexReadiness.NotReady, inner)
{
    public string Key { get; } = key;
}
