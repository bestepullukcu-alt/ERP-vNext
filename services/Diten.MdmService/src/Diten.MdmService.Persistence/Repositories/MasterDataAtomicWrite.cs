using System.Collections.Concurrent;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

/// <summary>
/// WP-MDM-BRAND-ACCEPT-01 FIX2 (item 7) — how a brand / product store behaves on a server that cannot run a transaction.
/// Default (every environment but Development): REFUSE the write (<see cref="BrandWriteOutcome.Unavailable"/>, 503) — a
/// record committed without its history would be a change with no audit trail that nobody knows about. Development may
/// keep writing in order (record, then trail) on a standalone mongod, with a warning in the log.
/// </summary>
public sealed class BrandProductStoreOptions
{
    public bool AllowNonTransactionalWrites { get; set; }
}

/// <summary>
/// FIX2 (items 7, 18) — ONE policy for every brand / product write that must land together with its history and its
/// external-reference reservations:
/// <list type="bullet">
/// <item>a transaction with snapshot reads and majority writes (the GSKU stores' options);</item>
/// <item>a transient transaction error re-runs the whole write (short, growing pause); a commit whose result is unknown
/// is committed again; when the attempts run out the answer is <see cref="BrandWriteOutcome.Unavailable"/> (503), never
/// an exception the caller reports as a 500;</item>
/// <item>no transaction support → <see cref="BrandProductStoreOptions"/> decides (refuse, or Development's ordered writes).</item>
/// </list>
/// </summary>
internal static class MasterDataAtomicWrite
{
    internal const int MaxTransactionAttempts = 10;
    internal const int MaxCommitAttempts = 5;

    private static readonly TransactionOptions Options =
        new(ReadConcern.Snapshot, ReadPreference.Primary, WriteConcern.WMajority);

    public static async Task<BrandWriteOutcome> RunAsync(
        IMongoClient client,
        BrandProductStoreOptions storeOptions,
        ILogger? logger,
        Func<IClientSessionHandle?, Task<BrandWriteOutcome>> write,
        CancellationToken cancellationToken,
        // FIX3 (M-2) — the commit, replaceable only by the tests that measure an unknown commit result.
        Func<IClientSessionHandle, CancellationToken, Task>? commit = null)
    {
        commit ??= static (session, token) => session.CommitTransactionAsync(token);
        for (var attempt = 1; ; attempt++)
        {
            using var session = await client.StartSessionAsync(cancellationToken: cancellationToken);
            try
            {
                session.StartTransaction(Options);
            }
            catch (NotSupportedException)
            {
                // A standalone mongod: the driver refuses before the server is asked.
                if (!storeOptions.AllowNonTransactionalWrites)
                {
                    logger?.LogError("Master data write refused: the database cannot run a transaction, so the record and its history could not be written together.");
                    return BrandWriteOutcome.Unavailable;
                }

                logger?.LogWarning("Master data write without a transaction (Development, standalone mongod): record first, then its history.");
                return await write(null);
            }

            try
            {
                var outcome = await write(session);
                if (outcome != BrandWriteOutcome.Written)
                {
                    await session.AbortTransactionAsync(cancellationToken);
                    return outcome;
                }

                return await CommitAsync(session, commit, logger, cancellationToken)
                    ? outcome
                    : BrandWriteOutcome.Ambiguous;
            }
            catch (MongoException exception) when (exception.HasErrorLabel("TransientTransactionError"))
            {
                await AbortQuietlyAsync(session);
                if (attempt >= MaxTransactionAttempts)
                {
                    logger?.LogWarning(exception, "Master data write gave up after {Attempts} transient transaction errors.", attempt);
                    return BrandWriteOutcome.Unavailable;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(15 * attempt), cancellationToken);
            }
            catch
            {
                await AbortQuietlyAsync(session);
                throw;
            }
        }
    }

    /// <summary>
    /// A commit whose result is unknown (a network blip after the server got it) is safe to commit again. FIX3 (M-2) — when it
    /// is STILL unknown after <see cref="MaxCommitAttempts"/>, the answer is false: the write may or may not have landed. The
    /// caller says so with a named, ambiguous outcome (503 write_outcome_unknown, the LSKU FIX4 shape), never a 500 — the
    /// client repeats the same request: a create meets its own code (409 duplicate), an edit meets its own version (409).
    /// </summary>
    private static async Task<bool> CommitAsync(
        IClientSessionHandle session, Func<IClientSessionHandle, CancellationToken, Task> commit, ILogger? logger,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await commit(session, cancellationToken);
                return true;
            }
            catch (MongoException exception) when (exception.HasErrorLabel("UnknownTransactionCommitResult"))
            {
                if (attempt >= MaxCommitAttempts)
                {
                    logger?.LogWarning(exception, "Master data commit result still unknown after {Attempts} attempts.", attempt);
                    return false;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(10 * attempt), cancellationToken);
            }
        }
    }

    private static async Task AbortQuietlyAsync(IClientSessionHandle session)
    {
        if (!session.IsInTransaction)
        {
            return;
        }

        try
        {
            await session.AbortTransactionAsync(CancellationToken.None);
        }
        catch (MongoException)
        {
            // The transaction is already gone server-side; nothing was committed.
        }
    }

    internal static bool IsDuplicateKey(Exception exception) => exception switch
    {
        MongoWriteException write => write.WriteError?.Category == ServerErrorCategory.DuplicateKey,
        MongoCommandException command => command.Code == 11000,
        MongoBulkWriteException bulk => bulk.WriteErrors.Any(x => x.Category == ServerErrorCategory.DuplicateKey),
        _ => false
    };
}

/// <summary>
/// FIX2 (item 6) — the external-reference reservations of brands and products (<see cref="ExternalReferenceReservation"/>),
/// written inside the caller's transaction.
/// </summary>
internal sealed class ExternalReferenceReservations
{
    public const string CollectionName = "mdm_external_reference_reservations";
    public const string UniqueIndexName = "ux_mdm_external_reference_reservations_pair";

    private readonly IMongoCollection<ExternalReferenceReservation> _collection;
    private readonly Guid _tenantId;

    public ExternalReferenceReservations(IMongoDatabase database, Guid tenantId)
    {
        _collection = database.GetCollection<ExternalReferenceReservation>(CollectionName);
        _tenantId = tenantId;
        IndexOnce.Ensure(database, CollectionName, () => _collection.Indexes.CreateOne(new CreateIndexModel<ExternalReferenceReservation>(
            Builders<ExternalReferenceReservation>.IndexKeys
                .Ascending(x => x.TenantId)
                .Ascending(x => x.RecordType)
                .Ascending(x => x.SourceKey)
                .Ascending(x => x.ExternalIdKey),
            new CreateIndexOptions { Unique = true, Name = UniqueIndexName })));
    }

    /// <summary>False when another record of the tenant already holds one of the pairs (nothing is reserved then).</summary>
    public async Task<bool> TryReserveAsync(
        IClientSessionHandle? session, string recordType, Guid recordId, string recordCode,
        IEnumerable<BrandProductExternalReference> references, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var rows = references
            .Select(x => (Source: ExternalReferenceReservation.Key(x.SourceSystem), Id: ExternalReferenceReservation.Key(x.ExternalId)))
            .Distinct()
            .Select(x => new ExternalReferenceReservation
            {
                TenantId = _tenantId,
                RecordType = recordType,
                SourceKey = x.Source,
                ExternalIdKey = x.Id,
                RecordId = recordId,
                RecordCode = recordCode,
                CreatedAt = now,
                UpdatedAt = now
            })
            .ToList();
        if (rows.Count == 0)
        {
            return true;
        }

        try
        {
            if (session is null)
                await _collection.InsertManyAsync(rows, cancellationToken: cancellationToken);
            else
                await _collection.InsertManyAsync(session, rows, cancellationToken: cancellationToken);
            return true;
        }
        catch (Exception exception) when (MasterDataAtomicWrite.IsDuplicateKey(exception))
        {
            return false;
        }
    }

    /// <summary>Releases pairs THIS record held (another record's reservation is never touched).</summary>
    public async Task ReleaseAsync(
        IClientSessionHandle? session, string recordType, Guid recordId,
        IEnumerable<BrandProductExternalReference> references, CancellationToken cancellationToken)
    {
        foreach (var reference in references)
        {
            var filter = Builders<ExternalReferenceReservation>.Filter.Eq(x => x.TenantId, _tenantId)
                         & Builders<ExternalReferenceReservation>.Filter.Eq(x => x.RecordType, recordType)
                         & Builders<ExternalReferenceReservation>.Filter.Eq(x => x.SourceKey, ExternalReferenceReservation.Key(reference.SourceSystem))
                         & Builders<ExternalReferenceReservation>.Filter.Eq(x => x.ExternalIdKey, ExternalReferenceReservation.Key(reference.ExternalId))
                         & Builders<ExternalReferenceReservation>.Filter.Eq(x => x.RecordId, recordId);
            if (session is null)
                await _collection.DeleteOneAsync(filter, cancellationToken);
            else
                await _collection.DeleteOneAsync(session, filter, cancellationToken: cancellationToken);
        }
    }
}

/// <summary>
/// FIX2 (item 19) — index DDL once per process and database, not on every request: the brand / product repositories are
/// scoped (one per request) and used to issue createIndexes from their constructors on every construction.
/// FIX3 (R-2) — only a SUCCESS is remembered. A failed createIndexes (the server not reachable yet at the first request, a
/// conflicting index) is not cached: the next repository tries again. A cached failure would answer every brand / product
/// request with a 500 until the process restarts.
/// </summary>
internal static class IndexOnce
{
    private static readonly ConcurrentDictionary<string, bool> Done = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, object> Gates = new(StringComparer.Ordinal);

    /// <summary>How many times index DDL actually succeeded in this process (measured by the FIX2 / FIX3 tests).</summary>
    internal static int Runs;

    public static void Ensure(IMongoDatabase database, string collectionName, Action create)
    {
        // Every server of the connection (a replica-set string lists several; Settings.Server would throw on it).
        var servers = string.Join(",", database.Client.Settings.Servers.Select(x => x.ToString()).OrderBy(x => x, StringComparer.Ordinal));
        var key = $"{servers}/{database.DatabaseNamespace.DatabaseName}/{collectionName}";
        if (Done.ContainsKey(key))
        {
            return;
        }

        lock (Gates.GetOrAdd(key, _ => new object()))
        {
            if (Done.ContainsKey(key))
            {
                return;
            }

            create(); // throws → nothing is recorded, the next call tries again
            Done[key] = true;
            Interlocked.Increment(ref Runs);
        }
    }
}

/// <summary>
/// WP-MDM-BRAND-ACCEPT-01 FIX3 — conditions on stored brand / product documents that also hold for documents written before
/// a field existed: "not archived" is Ne(true) (a document without IsArchived is not archived) and "at version 0" also
/// matches a document without Version (it reads as 0). Never Eq(false) / Eq(0), which would miss those documents.
/// </summary>
internal static class StoredFilters
{
    public static FilterDefinition<T> NotArchived<T>() => Builders<T>.Filter.Ne("IsArchived", true);

    public static FilterDefinition<T> AtVersion<T>(int expectedVersion) => expectedVersion == 0
        ? Builders<T>.Filter.Or(Builders<T>.Filter.Eq("Version", 0), Builders<T>.Filter.Exists("Version", false))
        : Builders<T>.Filter.Eq("Version", expectedVersion);
}
