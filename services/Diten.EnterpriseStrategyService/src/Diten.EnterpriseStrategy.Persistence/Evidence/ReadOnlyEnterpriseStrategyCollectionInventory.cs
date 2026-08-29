using Diten.Application.Common.Tenancy;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;
using static Diten.Application.Common.Tenancy.EnterpriseStrategyCollectionInventoryContracts;

namespace Diten.Persistence.Evidence;

public sealed class ReadOnlyEnterpriseStrategyCollectionInventory
{
    public static readonly IReadOnlySet<string> InventoryCommandNames =
        new HashSet<string>(["aggregate", "count", "listCollections", "listIndexes"], StringComparer.Ordinal);

    public static readonly IReadOnlySet<string> DriverSupportCommandNames =
        new HashSet<string>(
            ["authenticate", "buildInfo", "endSessions", "getMore", "getParameter", "getnonce", "hello", "isMaster", "killCursors", "saslContinue", "saslStart"],
            StringComparer.Ordinal);

    private readonly IMongoDatabase _database;
    private readonly InventoryOptions _options;

    public ReadOnlyEnterpriseStrategyCollectionInventory(InventoryOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        var settings = MongoClientSettings.FromConnectionString(options.MongoUri);
        settings.ClusterConfigurator = builder => builder.Subscribe<CommandStartedEvent>(RejectForbiddenCommand);
        _database = new MongoClient(settings).GetDatabase(options.Database);
    }

    public async Task<InventoryReport> ReadAsync(TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        var names = await ReadCollectionNamesAsync(cancellationToken);
        var collections = new List<CollectionEvidence>(names.Count);
        foreach (var name in names)
            collections.Add(await ReadCollectionAsync(name, cancellationToken));

        var ordered = collections.OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();
        return new InventoryReport(
            _options.TargetCopyId,
            _options.RunId,
            timeProvider.GetUtcNow(),
            ordered,
            ComputeMetadataDigest(ordered));
    }

    private async Task<IReadOnlyList<string>> ReadCollectionNamesAsync(CancellationToken cancellationToken)
    {
        using var cursor = await _database.ListCollectionNamesAsync(cancellationToken: cancellationToken);
        return (await cursor.ToListAsync(cancellationToken)).Order(StringComparer.Ordinal).ToArray();
    }

    private async Task<CollectionEvidence> ReadCollectionAsync(string name, CancellationToken cancellationToken)
    {
        var collection = _database.GetCollection<BsonDocument>(name);
        var count = await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: cancellationToken);
        var indexes = await ReadIndexesAsync(collection, cancellationToken);
        var fieldTypes = await ReadFieldTypesAsync(collection, cancellationToken);
        return new CollectionEvidence(name, count, "unreviewed", indexes, fieldTypes);
    }

    private static async Task<IReadOnlyList<IndexEvidence>> ReadIndexesAsync(
        IMongoCollection<BsonDocument> collection,
        CancellationToken cancellationToken)
    {
        using var cursor = await collection.Indexes.ListAsync(cancellationToken);
        var documents = await cursor.ToListAsync(cancellationToken);
        return documents.Select(MapIndex).OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();
    }

    private static IndexEvidence MapIndex(BsonDocument document)
    {
        var keys = document["key"].AsBsonDocument.Elements
            .Select(element => new IndexKeyEvidence(element.Name, RenderDirection(element.Value)))
            .ToArray();
        var expire = document.TryGetValue("expireAfterSeconds", out var ttl) && ttl.IsNumeric
            ? ttl.ToInt64()
            : (long?)null;
        return new IndexEvidence(
            document.GetValue("name", "unnamed").AsString,
            document.GetValue("unique", false).ToBoolean(),
            document.GetValue("sparse", false).ToBoolean(),
            expire,
            keys);
    }

    private static string RenderDirection(BsonValue value) => value.IsNumeric
        ? value.ToInt64().ToString(System.Globalization.CultureInfo.InvariantCulture)
        : value.IsString ? value.AsString : "unsupported";

    private static async Task<IReadOnlyList<FieldTypeEvidence>> ReadFieldTypesAsync(
        IMongoCollection<BsonDocument> collection,
        CancellationToken cancellationToken)
    {
        var pipeline = new[]
        {
            new BsonDocument("$project", new BsonDocument("pairs", new BsonDocument("$objectToArray", "$$ROOT"))),
            new BsonDocument("$unwind", "$pairs"),
            new BsonDocument("$group", new BsonDocument
            {
                ["_id"] = new BsonDocument { ["field"] = "$pairs.k", ["type"] = new BsonDocument("$type", "$pairs.v") },
                ["count"] = new BsonDocument("$sum", 1)
            }),
            new BsonDocument("$project", new BsonDocument
            {
                ["_id"] = 0, ["field"] = "$_id.field", ["type"] = "$_id.type", ["count"] = 1
            })
        };
        var documents = await collection.Aggregate<BsonDocument>(pipeline).ToListAsync(cancellationToken);
        return documents.Select(document => new FieldTypeEvidence(
                document["field"].AsString,
                document["type"].AsString,
                document["count"].ToInt64()))
            .OrderBy(item => item.Field, StringComparer.Ordinal)
            .ThenBy(item => item.BsonType, StringComparer.Ordinal)
            .ToArray();
    }

    private static void RejectForbiddenCommand(CommandStartedEvent command)
    {
        if (!InventoryCommandNames.Contains(command.CommandName) &&
            !DriverSupportCommandNames.Contains(command.CommandName))
            throw new InvalidOperationException("Read-only inventory rejected a MongoDB command.");
    }
}
