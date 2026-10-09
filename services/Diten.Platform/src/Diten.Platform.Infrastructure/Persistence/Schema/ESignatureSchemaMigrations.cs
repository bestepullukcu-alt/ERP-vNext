using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Schema;

/// <summary>What the startup step found under the retired index name.</summary>
public enum LegacyIndexRetirement
{
    /// <summary>No index of that name: a fresh database, or the step already ran. Nothing is done.</summary>
    Absent,

    /// <summary>The exact E2–E5W1 definition was found and dropped; the manifest builds the lookup index next.</summary>
    Dropped,

    /// <summary>An index of that name with ANOTHER definition: not ours to judge, left as it is and logged.</summary>
    LeftUntouched
}

/// <summary>
/// MOD-0022 E5B (FU-0022-E4-01, CT-D16) — retires the per-task UNIQUE index of <c>esign_records</c>.
///
/// E2 declared <c>ux_esign_records_tenant_workflow_task</c> unique, so a task whose signature was invalidated or carried the
/// wrong meaning could never be signed again (409 at signing). The manifest now declares the same keys as the lookup index
/// <c>ix_esign_records_tenant_workflow_task</c>; "one signature binds one approval" is enforced in ONE place, the E4 gate
/// (context = this task, single use on the transition log), and a task is approved once.
///
/// Mongo cannot turn a unique index into a non-unique one in place, and the same keys under a new name next to the old one
/// is IndexOptionsConflict. So the old one is dropped first (<see cref="PlatformSchemaMigrations.RunAsync"/>, before the
/// manifest), and ONLY when its name AND every stored option equal what E2 built — anything else under that name is left
/// alone and reported. Idempotent: once dropped, every later startup finds it <see cref="LegacyIndexRetirement.Absent"/>.
/// No document is read or written.
/// </summary>
public static class ESignatureSchemaMigrations
{
    public const string LegacyWorkflowTaskIndex = "ux_esign_records_tenant_workflow_task";
    public const string WorkflowTaskLookupIndex = "ix_esign_records_tenant_workflow_task";

    private const string Log = "[PlatformSchemaMigrations] MOD-0022 E5B";

    // Exactly what E2 built (PlatformSchemaManifest.ESignature.cs at 562afeaa5), as listIndexes returns it.
    private static readonly string[] LegacyKeyFields = { "TenantId", "Context.workflowTaskId" };
    private static readonly BsonDocument LegacyPartialFilter =
        new("Context.workflowTaskId", new BsonDocument("$exists", true));
    private static readonly HashSet<string> LegacyOptionNames =
        new(StringComparer.Ordinal) { "v", "key", "name", "ns", "unique", "partialFilterExpression", "collation" };

    // Measured on mongod 9.0: listIndexes reports an index built WITHOUT a collation as { locale: "simple" } (the binary
    // default). That one value equals "no collation"; any other collation is not E2's index.
    private static readonly BsonDocument SimpleCollation = new("locale", "simple");

    public static async Task<LegacyIndexRetirement> RetireWorkflowTaskUniqueIndexAsync(
        IMongoDatabase database,
        Action<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        log ??= Console.WriteLine;
        var indexes = database.GetCollection<BsonDocument>(PlatformCollections.ESignatureRecords).Indexes;

        List<BsonDocument> existing;
        try
        {
            using var cursor = await indexes.ListAsync(cancellationToken);
            existing = await cursor.ToListAsync(cancellationToken);
        }
        catch (MongoCommandException ex) when (ex.CodeName == "NamespaceNotFound")
        {
            existing = new List<BsonDocument>();
        }

        var legacy = existing.FirstOrDefault(i => i.TryGetValue("name", out var name) && name == LegacyWorkflowTaskIndex);
        if (legacy is null)
        {
            log($"{Log}: {LegacyWorkflowTaskIndex} absent on {PlatformCollections.ESignatureRecords} — nothing to do.");
            return LegacyIndexRetirement.Absent;
        }

        if (!IsTheLegacyDefinition(legacy))
        {
            log($"{Log}: WARNING {LegacyWorkflowTaskIndex} on {PlatformCollections.ESignatureRecords} does not match the E2 "
                + $"definition and was left untouched: {legacy.ToJson()}");
            return LegacyIndexRetirement.LeftUntouched;
        }

        await indexes.DropOneAsync(LegacyWorkflowTaskIndex, cancellationToken);
        log($"{Log}: dropped the unique {LegacyWorkflowTaskIndex} on {PlatformCollections.ESignatureRecords} (exact E2 "
            + $"definition); the manifest builds the lookup index {WorkflowTaskLookupIndex} next.");
        return LegacyIndexRetirement.Dropped;
    }

    /// <summary>Name already matched; every stored option must be the E2 one, and nothing else may be set.</summary>
    internal static bool IsTheLegacyDefinition(BsonDocument index)
    {
        if (index.Names.Any(n => !LegacyOptionNames.Contains(n)))
        {
            return false;
        }

        if (index.TryGetValue("collation", out var collation)
            && !(collation.IsBsonDocument && collation.AsBsonDocument.Equals(SimpleCollation)))
        {
            return false;
        }

        if (!index.TryGetValue("unique", out var unique) || !unique.IsBoolean || !unique.AsBoolean)
        {
            return false;
        }

        if (!index.TryGetValue("key", out var key) || !key.IsBsonDocument)
        {
            return false;
        }

        var fields = key.AsBsonDocument.Elements.ToArray();
        if (fields.Length != LegacyKeyFields.Length
            || fields.Where((f, i) => f.Name != LegacyKeyFields[i] || !f.Value.IsNumeric || f.Value.ToDouble() != 1d).Any())
        {
            return false;
        }

        return index.TryGetValue("partialFilterExpression", out var partial)
               && partial.IsBsonDocument
               && partial.AsBsonDocument.Equals(LegacyPartialFilter);
    }
}
