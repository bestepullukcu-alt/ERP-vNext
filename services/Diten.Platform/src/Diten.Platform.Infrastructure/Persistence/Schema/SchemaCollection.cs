using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Schema;

/// <summary>
/// One index as the manifest declares it, rendered down to the four properties Mongo actually stores.
/// The contract test compares THIS against <c>listIndexes</c> — see the note on <see cref="Name"/> for why
/// comparing anything less than this is a test that passes for the wrong reason.
/// </summary>
public sealed record SchemaIndex(
    string Name,
    BsonDocument Key,
    bool Unique,
    BsonDocument? PartialFilterExpression);

/// <summary>
/// One collection in the manifest: its name, its document type, the profile it belongs to, and every index
/// it must carry.
/// </summary>
public abstract class SchemaCollection
{
    protected SchemaCollection(string name, SchemaProfile profile, string? failureHint)
    {
        Name = name;
        Profile = profile;
        FailureHint = failureHint;
    }

    public string Name { get; }

    public SchemaProfile Profile { get; }

    /// <summary>Extra operator-facing context when this collection's indexes fail to build.</summary>
    public string? FailureHint { get; }

    public abstract Type DocumentType { get; }

    /// <summary>The declared indexes, rendered. Does NOT include the implicit <c>_id</c> index.</summary>
    public abstract IReadOnlyList<SchemaIndex> Indexes { get; }

    /// <summary>Declared indexes plus the one Mongo creates on its own — the number a budget counts.</summary>
    public int LogicalIndexCount => Indexes.Count + 1;

    public abstract Task ApplyAsync(IMongoDatabase database, CancellationToken cancellationToken);
}

/// <inheritdoc />
public sealed class SchemaCollection<TDocument> : SchemaCollection
{
    private readonly Func<CreateIndexModel<TDocument>[]> _models;
    private IReadOnlyList<SchemaIndex>? _rendered;

    public SchemaCollection(
        string name,
        SchemaProfile profile,
        Func<CreateIndexModel<TDocument>[]> models,
        string? failureHint = null)
        : base(name, profile, failureHint)
    {
        _models = models;
    }

    public override Type DocumentType => typeof(TDocument);

    public override IReadOnlyList<SchemaIndex> Indexes => _rendered ??= Render();

    public override async Task ApplyAsync(IMongoDatabase database, CancellationToken cancellationToken)
    {
        var models = _models();
        if (models.Length == 0)
        {
            return;
        }

        try
        {
            await database.GetCollection<TDocument>(Name).Indexes
                .CreateManyAsync(models, cancellationToken);
        }
        catch (MongoException ex)
        {
            // Loud, not swallowed. A half-built unique index leaves the collection with NO uniqueness
            // protection, and the drop of the old index has already happened by the time we get here.
            var message =
                $"[PlatformSchemaManifest] failed to build indexes on '{Name}' "
                + $"({string.Join(", ", Indexes.Select(i => i.Name))}): {ex.Message}";
            if (FailureHint is not null)
            {
                message += " " + FailureHint;
            }

            Console.Error.WriteLine("ERROR: " + message);
            throw new InvalidOperationException(message, ex);
        }
    }

    private IReadOnlyList<SchemaIndex> Render()
    {
        var serializer = BsonSerializer.SerializerRegistry.GetSerializer<TDocument>();
        var registry = BsonSerializer.SerializerRegistry;

        return _models().Select(model =>
        {
            var key = model.Keys.Render(serializer, registry);
            var options = model.Options;
            var typed = options as CreateIndexOptions<TDocument>;

            return new SchemaIndex(
                options?.Name ?? DefaultIndexName(key),
                key,
                options?.Unique == true,
                typed?.PartialFilterExpression?.Render(serializer, registry));
        }).ToArray();
    }

    /*
     * ⚠ NOT COSMETIC. Several models are declared with keys and no options, so the manifest never names them
     * — Mongo does, by concatenating "field_direction". If the contract test compared only the indexes the
     * manifest happens to name, every unnamed index would be invisible to it: absent from Mongo, absent from
     * the comparison, green. Reproducing Mongo's own rule here is what puts them under the check.
     */
    private static string DefaultIndexName(BsonDocument key)
        => string.Join("_", key.Elements.Select(e => $"{e.Name}_{e.Value}"));
}
