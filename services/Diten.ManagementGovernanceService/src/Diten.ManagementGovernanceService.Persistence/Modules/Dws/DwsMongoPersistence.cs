using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.ManagementGovernanceService.Persistence.Modules.Dws;

internal static class DwsMongoGuid
{
    public static BsonBinaryData Standard(Guid value) => new(value, GuidRepresentation.Standard);
}

public sealed class DwsMongoContext
{
    private static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["definitions"] = "mg_dws_structure_definitions",
        ["revisions"] = "mg_dws_structure_revisions",
        ["nodes"] = "mg_dws_structure_nodes",
        ["dependencies"] = "mg_dws_structural_dependencies",
        ["baselines"] = "mg_dws_structure_baselines",
        ["receipts"] = "mg_dws_idempotency_receipts",
        ["audit-intents"] = "mg_dws_audit_intents",
        ["outbox"] = "mg_dws_outbox_messages"
    };

    public DwsMongoContext(IMongoClient client, string databaseName)
    {
        Client = client ?? throw new ArgumentNullException(nameof(client));
        if (string.IsNullOrWhiteSpace(databaseName)) throw new ArgumentException("Database name is required.", nameof(databaseName));
        Database = client.GetDatabase(databaseName);
    }

    public IMongoClient Client { get; }
    public IMongoDatabase Database { get; }
    public static IReadOnlyDictionary<string, string> CollectionAliases => Aliases;

    public IMongoCollection<BsonDocument> Collection(string alias)
    {
        if (!Aliases.TryGetValue(alias, out var name)) throw new InvalidOperationException("dws_transaction_unavailable");
        return Database.GetCollection<BsonDocument>(name);
    }
}

public sealed class DwsMongoIndexInitializer
{
    private readonly DwsMongoContext _context;
    public DwsMongoIndexInitializer(DwsMongoContext context) => _context = context;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        foreach (var collection in DwsPersistenceOwnershipManifest.Collections)
        {
            if (!DwsMongoContext.CollectionAliases.Values.Contains(collection.Name, StringComparer.Ordinal))
                throw new InvalidOperationException("dws_transaction_unavailable");
            await EnsureCollectionAsync(collection.Name, cancellationToken);
        }

        foreach (var group in DwsPersistenceOwnershipManifest.Indexes.GroupBy(index => index.Collection, StringComparer.Ordinal))
        {
            var collection = _context.Collection(NormalizeAlias(group.Key));
            var models = group.Select(ToModel).ToArray();
            await collection.Indexes.CreateManyAsync(models, cancellationToken);
        }
    }

    private async Task EnsureCollectionAsync(string name, CancellationToken cancellationToken)
    {
        using var cursor = await _context.Database.ListCollectionNamesAsync(new ListCollectionNamesOptions
        {
            Filter = new BsonDocument("name", name)
        }, cancellationToken);
        if (!await cursor.AnyAsync(cancellationToken)) await _context.Database.CreateCollectionAsync(name, cancellationToken: cancellationToken);
    }

    private static string NormalizeAlias(string value) => value switch
    {
        "audit intents" => "audit-intents",
        _ => value
    };

    private static CreateIndexModel<BsonDocument> ToModel(DwsIndex index)
    {
        var keys = new BsonDocument(index.Keys.Select(key => new BsonElement(key, 1)));
        var options = new CreateIndexOptions<BsonDocument> { Name = index.Name, Unique = index.Unique };
        if (index.PartialFilter is not null) options.PartialFilterExpression = ParsePartialFilter(index.PartialFilter);
        return new CreateIndexModel<BsonDocument>(keys, options);
    }

    private static BsonDocument ParsePartialFilter(string filter)
    {
        var document = new BsonDocument();
        foreach (var clause in filter.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = clause.Split('=', 2, StringSplitOptions.TrimEntries);
            if (pair.Length != 2 || !bool.TryParse(pair[1], out var value)) throw new InvalidOperationException("dws_transaction_unavailable");
            document[pair[0]] = value;
        }
        return document;
    }
}

public sealed record DwsMongoParticipant(string CollectionAlias, Guid Id, int ExpectedVersion, BsonDocument Values);
public sealed record DwsMongoMutation(Guid TenantId, string TransactionFamily, string ReceiptKey, string PayloadHash, IReadOnlyList<DwsMongoParticipant> Participants);
public sealed record DwsMongoReceiptReconciliation(bool Matches, bool Conflicts);

public interface IDwsMongoCommitter
{
    Task CommitAsync(IClientSessionHandle session, CancellationToken cancellationToken);
}

public interface IDwsMongoReceiptReconciler
{
    Task<DwsMongoReceiptReconciliation> ReconcileAsync(DwsMongoMutation mutation, CancellationToken cancellationToken);
}

public interface IDwsMongoFaultProbe
{
    Task AfterParticipantAsync(int participantNumber, CancellationToken cancellationToken);
}

public sealed class DwsMongoCommitter : IDwsMongoCommitter
{
    public Task CommitAsync(IClientSessionHandle session, CancellationToken cancellationToken) => session.CommitTransactionAsync(cancellationToken);
}

public sealed class DwsMongoReceiptReconciler(DwsMongoContext context) : IDwsMongoReceiptReconciler
{
    public async Task<DwsMongoReceiptReconciliation> ReconcileAsync(DwsMongoMutation mutation, CancellationToken cancellationToken)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("TenantId", DwsMongoGuid.Standard(mutation.TenantId))
            & Builders<BsonDocument>.Filter.Eq("CommandFamily", mutation.TransactionFamily)
            & Builders<BsonDocument>.Filter.Eq("IdempotencyKey", mutation.ReceiptKey);
        var receipt = await context.Collection("receipts").WithReadConcern(ReadConcern.Majority).Find(filter).SingleOrDefaultAsync(cancellationToken);
        return receipt is null
            ? new(false, false)
            : receipt.GetValue("RequestPayloadHash", "").AsString == mutation.PayloadHash ? new(true, false) : new(false, true);
    }
}

public sealed class DwsUnknownCommitResultException : Exception;

public sealed class DwsMongoAtomicWriter
{
    private readonly DwsMongoContext _context;
    private readonly IDwsMongoCommitter _committer;
    private readonly IDwsMongoReceiptReconciler _reconciler;

    public DwsMongoAtomicWriter(DwsMongoContext context, IDwsMongoCommitter? committer = null, IDwsMongoReceiptReconciler? reconciler = null)
    {
        _context = context;
        _committer = committer ?? new DwsMongoCommitter();
        _reconciler = reconciler ?? new DwsMongoReceiptReconciler(context);
    }

    public async Task ExecuteAsync(DwsMongoMutation mutation, IDwsMongoFaultProbe? faultProbe = null, CancellationToken cancellationToken = default)
    {
        Validate(mutation);
        using var session = await _context.Client.StartSessionAsync(cancellationToken: cancellationToken);
        try
        {
            session.StartTransaction(new TransactionOptions(ReadConcern.Snapshot, ReadPreference.Primary, WriteConcern.WMajority));
        }
        catch (NotSupportedException error)
        {
            throw new InvalidOperationException("dws_transaction_unavailable", error);
        }
        try
        {
            for (var index = 0; index < mutation.Participants.Count; index++)
            {
                await WriteAsync(session, mutation.TenantId, mutation.Participants[index], cancellationToken);
                if (faultProbe is not null) await faultProbe.AfterParticipantAsync(index + 1, cancellationToken);
            }

            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    await _committer.CommitAsync(session, cancellationToken);
                    return;
                }
                catch (Exception error) when (IsUnknownCommit(error) && attempt < 3) { }
                catch (Exception error) when (IsUnknownCommit(error)) { break; }
            }

            var reconciliation = await _reconciler.ReconcileAsync(mutation, cancellationToken);
            if (reconciliation.Matches) return;
            if (reconciliation.Conflicts) throw new InvalidOperationException("dws_idempotency_conflict");
            throw new InvalidOperationException("dws_commit_indeterminate");
        }
        catch
        {
            if (session.IsInTransaction) await session.AbortTransactionAsync(CancellationToken.None);
            throw;
        }
    }

    private static bool IsUnknownCommit(Exception error) => error is DwsUnknownCommitResultException
        || error is MongoException mongo && mongo.HasErrorLabel("UnknownTransactionCommitResult");

    private async Task WriteAsync(IClientSessionHandle session, Guid tenantId, DwsMongoParticipant participant, CancellationToken cancellationToken)
    {
        var collection = _context.Collection(participant.CollectionAlias);
        if (participant.ExpectedVersion == 0)
        {
            var document = new BsonDocument
            {
                ["_id"] = DwsMongoGuid.Standard(participant.Id),
                ["TenantId"] = DwsMongoGuid.Standard(tenantId),
                ["Version"] = 1,
                ["IsDeleted"] = false
            };
            document.AddRange(participant.Values);
            await collection.InsertOneAsync(session, document, cancellationToken: cancellationToken);
            return;
        }

        var filter = Builders<BsonDocument>.Filter.Eq("_id", DwsMongoGuid.Standard(participant.Id))
            & Builders<BsonDocument>.Filter.Eq("TenantId", DwsMongoGuid.Standard(tenantId))
            & Builders<BsonDocument>.Filter.Eq("Version", participant.ExpectedVersion)
            & Builders<BsonDocument>.Filter.Eq("IsDeleted", false);
        var update = Builders<BsonDocument>.Update.Set("Value", participant.Values.GetValue("Value", BsonNull.Value)).Inc("Version", 1);
        var result = await collection.UpdateOneAsync(session, filter, update, cancellationToken: cancellationToken);
        if (result.MatchedCount != 1) throw new InvalidOperationException("dws_concurrency_conflict");
    }

    private static void Validate(DwsMongoMutation mutation)
    {
        if (mutation.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(mutation.ReceiptKey) || mutation.PayloadHash.Length != 64)
            throw new InvalidOperationException("dws_transaction_unavailable");
        var family = DwsPersistenceOwnershipManifest.Transactions.SingleOrDefault(value => value.Name == mutation.TransactionFamily)
            ?? throw new InvalidOperationException("dws_transaction_unavailable");
        var expected = family.BusinessCollections.Concat(DwsTransactionFamily.TechnicalParticipants).ToArray();
        if (!expected.SequenceEqual(mutation.Participants.Select(value => value.CollectionAlias), StringComparer.Ordinal))
            throw new InvalidOperationException("dws_transaction_unavailable");
        if (mutation.Participants.Any(value => value.Id == Guid.Empty || value.ExpectedVersion < 0))
            throw new InvalidOperationException("dws_transaction_unavailable");
    }
}
