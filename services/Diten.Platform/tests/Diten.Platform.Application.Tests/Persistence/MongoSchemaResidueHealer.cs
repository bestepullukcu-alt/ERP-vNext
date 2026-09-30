using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Application.Tests.Persistence;

/// <summary>Whether the healer may delete rows in a database, and why.</summary>
public sealed record HealDecision(bool Allowed, string Reason)
{
    public static HealDecision Refuse(string reason) => new(false, reason);
    public static readonly HealDecision Allow = new(true, "owned name, our marker, stamped by this run");
}

/// <summary>The rows removed from one unique index so that it could be built.</summary>
public sealed record HealedIndex(string Collection, string Index, int DuplicateKeys, int RowsRemoved, IReadOnlyList<string> SampleKeys);

/// <summary>What one heal did. <see cref="Refusal"/> is set, and nothing was touched, when the database is not ours.</summary>
public sealed record SchemaHealReport(
    string DatabaseName,
    SchemaProfile Profile,
    IReadOnlyList<HealedIndex> Healed,
    string? Refusal)
{
    public int RowsRemoved => Healed.Sum(h => h.RowsRemoved);
}

/*
 * ⚠ BL-482 — THIS IS THE SECOND DELETE PATH IN THIS HARNESS. Read MongoResidueSweeper's header first; the same
 * rules apply here, and the ownership check below reuses its grammar and its marker rather than a copy of them.
 *
 * THE DEFECT (measured 2026-09-30 and reproduced 2026-10-01). A test that proves "the real unique index refuses a
 * duplicate" inserts the duplicate. If the index is missing at that moment, the duplicate is ACCEPTED, and in the
 * shared database — where tenant-isolated rows are never cleaned — it stays. From then on every build of that
 * collection's indexes fails with E11000, so every test class that asks for the profile goes red, one per run,
 * rotating, forever. The measured way the index went missing: the start-of-run sweep dropped the shared database
 * while another class was building its schema ("database is in the process of being dropped"), the profile was
 * memoised as applied, and the rest of the run wrote into collections with no unique index at all.
 *
 * WHY DELETING IS SAFE HERE, AND ONLY HERE. The rows that break a unique-index build in a harness-owned test
 * database are test residue by construction: that database holds nothing but test tenants, every one of them a
 * fresh Guid of a test process that has since exited. So the offending rows are removed — ALL of them, keeping
 * none of the duplicated keys, because there is no "right" copy of a dead test's data — and the build is retried
 * once. What is removed is written to stderr, so the heal is visible rather than silent.
 *
 * WHY IT IS NARROW. Three conditions, all required, all checked by THIS class before it deletes anything (the
 * caller's own checks are not trusted):
 *   1. the database name is inside the harness's owned grammar (MongoResidueSweeper.IsOwnedName);
 *   2. it carries OUR marker (harness name + a run id);
 *   3. that marker was stamped by THIS run — the harness stamps it immediately before it builds the schema, so
 *      "stamped by this run" means "this process opened it seconds ago".
 * A development database, a colleague's scratch database, the BRD harness's databases: none carries a marker
 * stamped by this run, so none can be reached. The database name is read from the database object itself, never
 * accepted as a parameter.
 *
 * ⚠ PRODUCTION SCHEMA CODE IS NOT CHANGED. The index definitions are read from PlatformSchemaManifest — the same
 * models production builds — so the healer can never disagree with the manifest about what "a duplicate" is.
 *
 * KNOWN LIMITS, STATED: the duplicate finder groups on the index's key fields (missing == null, as Mongo's unique
 * index treats them) inside its partial filter. It does not model collations, sparse unique indexes or unique
 * indexes over array fields; the manifest declares none of those today. If one appears and the finder misses its
 * duplicates, the retry fails and the harness reports it loudly — it never silently proceeds.
 */
public static class MongoSchemaResidueHealer
{
    /// <summary>
    /// The whole ownership decision as a pure function, so every refusal can be proved without a live server.
    /// </summary>
    public static HealDecision MayHeal(string databaseName, HarnessMarker? marker, Guid currentRunId)
    {
        if (!MongoResidueSweeper.IsOwnedName(databaseName))
        {
            return HealDecision.Refuse("name is outside the owned prefix grammar");
        }

        if (marker is null)
        {
            return HealDecision.Refuse("no harness marker — this database was not created by us");
        }

        if (!string.Equals(marker.Harness, MongoResidueSweeper.MarkerHarness, StringComparison.Ordinal))
        {
            return HealDecision.Refuse($"marker belongs to '{marker.Harness}', not us");
        }

        if (marker.RunId == Guid.Empty)
        {
            return HealDecision.Refuse("marker carries no run id");
        }

        if (marker.RunId != currentRunId)
        {
            return HealDecision.Refuse("marker was stamped by another run — this run did not just open it");
        }

        return HealDecision.Allow;
    }

    /// <summary>
    /// True only for a unique-index build that failed on duplicate keys. Anything else — a conflicting index
    /// definition, a dropped connection, a database being dropped — is not residue and must not be "healed".
    /// </summary>
    public static bool IsDuplicateKeyFailure(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            switch (current)
            {
                case MongoCommandException command when command.Code is 11000 or 11001:
                    return true;
                case MongoWriteException write when write.WriteError?.Category == ServerErrorCategory.DuplicateKey:
                    return true;
                case MongoException mongo when mongo.Message.Contains("E11000 duplicate key", StringComparison.Ordinal):
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Removes every row that collides on a unique index of <paramref name="profile"/>, in a database this run
    /// owns. Returns a refusal report — having touched nothing — when the database is not provably ours.
    /// </summary>
    public static async Task<SchemaHealReport> RemoveDuplicateKeyResidueAsync(
        IMongoDatabase database,
        SchemaProfile profile,
        Guid currentRunId,
        CancellationToken cancellationToken = default)
    {
        var databaseName = database.DatabaseNamespace.DatabaseName;

        // Cheap check first: never even READ from a database outside the grammar.
        if (!MongoResidueSweeper.IsOwnedName(databaseName))
        {
            return new SchemaHealReport(databaseName, profile, Array.Empty<HealedIndex>(),
                MayHeal(databaseName, null, currentRunId).Reason);
        }

        var marker = await MongoResidueSweeper.ReadMarkerAsync(database, cancellationToken);
        var decision = MayHeal(databaseName, marker, currentRunId);
        if (!decision.Allowed)
        {
            return new SchemaHealReport(databaseName, profile, Array.Empty<HealedIndex>(), decision.Reason);
        }

        var healed = new List<HealedIndex>();
        foreach (var collection in PlatformSchemaManifest.For(profile))
        {
            foreach (var index in collection.Indexes.Where(i => i.Unique))
            {
                var result = await RemoveDuplicatesAsync(database, collection.Name, index, cancellationToken);
                if (result is not null)
                {
                    healed.Add(result);
                }
            }
        }

        return new SchemaHealReport(databaseName, profile, healed, null);
    }

    private static async Task<HealedIndex?> RemoveDuplicatesAsync(
        IMongoDatabase database,
        string collectionName,
        SchemaIndex index,
        CancellationToken cancellationToken)
    {
        var keyNames = index.Key.Names.ToArray();
        if (keyNames.Length == 0)
        {
            return null;
        }

        // A unique index treats a missing field and an explicit null as the SAME key; $group does not, so
        // both are folded to null before grouping. Generated names (k0, k1…) because a dotted index path is not
        // a legal field name inside $group's _id.
        var groupKey = new BsonDocument();
        for (var i = 0; i < keyNames.Length; i++)
        {
            groupKey.Add($"k{i}", new BsonDocument("$ifNull", new BsonArray { "$" + keyNames[i], BsonNull.Value }));
        }

        var pipeline = new List<BsonDocument>();
        if (index.PartialFilterExpression is { ElementCount: > 0 } partialFilter)
        {
            pipeline.Add(new BsonDocument("$match", partialFilter));
        }

        pipeline.Add(new BsonDocument("$group", new BsonDocument
        {
            { "_id", groupKey },
            { "ids", new BsonDocument("$push", "$_id") },
            { "count", new BsonDocument("$sum", 1) }
        }));
        pipeline.Add(new BsonDocument("$match", new BsonDocument("count", new BsonDocument("$gt", 1))));

        var collection = database.GetCollection<BsonDocument>(collectionName);
        var groups = await (await collection.AggregateAsync(
                PipelineDefinition<BsonDocument, BsonDocument>.Create(pipeline),
                cancellationToken: cancellationToken))
            .ToListAsync(cancellationToken);

        if (groups.Count == 0)
        {
            return null;
        }

        var ids = groups.SelectMany(g => g["ids"].AsBsonArray).ToList();
        var deleted = await collection.DeleteManyAsync(
            Builders<BsonDocument>.Filter.In("_id", ids),
            cancellationToken);

        var samples = groups
            .Take(5)
            .Select(g => string.Join(", ", keyNames.Select((name, i) => $"{name}: {g["_id"][$"k{i}"]}")))
            .ToArray();

        return new HealedIndex(collectionName, index.Name, groups.Count, (int)deleted.DeletedCount, samples);
    }
}
