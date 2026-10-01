using System.Text.RegularExpressions;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Application.Tests.Persistence;

/// <summary>Whether the healer may delete rows in a database, and why.</summary>
public sealed record HealDecision(bool Allowed, string Reason)
{
    public static HealDecision Refuse(string reason) => new(false, reason);
    public static readonly HealDecision Allow = new(true, "owned name, our marker, stamped by this run, real lock held");
}

/// <summary>The one index an E11000 index-build failure names.</summary>
public sealed record DuplicateKeyTarget(string Collection, string Index);

/// <summary>The rows removed from one unique index so that it could be built.</summary>
public sealed record HealedIndex(string Collection, string Index, int DuplicateKeys, int RowsRemoved, IReadOnlyList<string> SampleKeys)
{
    public string Describe() => $"{RowsRemoved} row(s) from '{Collection}.{Index}'";
}

/// <summary>What one heal did. <see cref="Refusal"/> is set, and nothing was touched by that step, when it refused.</summary>
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
 * WHY DELETING IS SAFE HERE, AND ONLY HERE. The rows that break a TENANT-KEYED unique index in a harness-owned
 * test database belong to test tenants — each a fresh Guid of a test process. When that tenant is not one THIS
 * process handed out, its process has exited and the rows are residue: they are removed — all of them, keeping
 * none of the duplicated keys, because there is no "right" copy of a dead test's data.
 *
 * WHAT IT HEALS (round 2, independent review):
 *   • ONLY the index the E11000 failure names — never "every unique index of the profile";
 *   • ONLY an index whose key holds TenantId. A global key (tenant code, catalog code, event id, seed marker…)
 *     groups rows ACROSS tenants: nothing proves such a group is residue, so a human decides — the harness throws;
 *   • NEVER a group whose tenant a harness of THIS process handed out (a live test's rows, possibly written by a
 *     class that is running right now through handlers), and never a group with no tenant at all.
 *
 * WHERE IT MAY DELETE. Four conditions, all required, all checked by THIS class before it deletes anything:
 *   1. this process holds the REAL machine-wide test lock (not a lock-proof child, not a redirected lock);
 *   2. the database name is inside the harness's owned grammar (MongoResidueSweeper.IsOwnedName);
 *   3. it carries OUR marker (harness name + a run id);
 *   4. that marker was stamped by THIS run — the harness stamps it immediately before it builds the schema.
 * The database name is read from the database object itself, never accepted as a parameter.
 *
 * ⚠ PRODUCTION SCHEMA CODE IS NOT CHANGED. The index definitions are read from PlatformSchemaManifest — the same
 * models production builds — so the healer can never disagree with the manifest about what "a duplicate" is.
 *
 * KNOWN LIMITS, STATED: the duplicate finder groups on the index's key fields (missing == null, as Mongo's unique
 * index treats them) inside its partial filter. It does not model collations, sparse unique indexes or unique
 * indexes over array fields — MongoSchemaResidueHealerTests pins that the manifest declares no unique sparse or
 * collated index. If the finder finds no duplicate for the failing index, it refuses rather than guesses.
 */
public static class MongoSchemaResidueHealer
{
    private const string TenantKey = "TenantId";

    private static readonly Regex DuplicateKeyNamespace = new(
        @"E11000 duplicate key error collection: (?<ns>\S+) index: (?<index>\S+)",
        RegexOptions.Compiled);

    /// <summary>
    /// The whole ownership decision as a pure function, so every refusal can be proved without a live server.
    /// </summary>
    public static HealDecision MayHeal(string databaseName, HarnessMarker? marker, Guid currentRunId, bool holdsRealLock)
    {
        if (!holdsRealLock)
        {
            return HealDecision.Refuse(
                "this process does not hold the real machine-wide test lock (a lock-proof child or a redirected lock)");
        }

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
    /// The collection and index an E11000 failure names — the ONLY index the healer may touch for it. Null when the
    /// failure does not name one, in which case nothing is healed.
    /// </summary>
    public static DuplicateKeyTarget? ParseDuplicateKeyTarget(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            var match = DuplicateKeyNamespace.Match(current.Message);
            if (!match.Success)
            {
                continue;
            }

            var ns = match.Groups["ns"].Value;
            var dot = ns.IndexOf('.');
            if (dot <= 0 || dot == ns.Length - 1)
            {
                return null;
            }

            return new DuplicateKeyTarget(ns[(dot + 1)..], match.Groups["index"].Value);
        }

        return null;
    }

    /// <summary>
    /// The unique indexes the finder cannot model: a SPARSE unique index would make it treat every document missing
    /// the field as one duplicate group (and delete them all); a COLLATED one groups differently from the index.
    /// Pure, so the rule is provable on synthetic input; the manifest scan lives in MongoSchemaResidueHealerTests.
    /// </summary>
    public static IReadOnlyList<string> UnmodelledUniqueIndexes(
        IEnumerable<(string Collection, string Index, CreateIndexOptions? Options)> indexes)
        => indexes
            .Where(i => i.Options?.Unique == true && (i.Options.Sparse == true || i.Options.Collation is not null))
            .Select(i => $"{i.Collection}.{i.Index}")
            .ToArray();

    /// <summary>
    /// Removes the rows that collide on the ONE unique index <paramref name="target"/> names, in a database this run
    /// owns, under the rules in the header. Returns a report whose <see cref="SchemaHealReport.Refusal"/> is set —
    /// having deleted nothing — when any rule does not hold.
    /// </summary>
    public static async Task<SchemaHealReport> HealAsync(
        IMongoDatabase database,
        SchemaProfile profile,
        DuplicateKeyTarget target,
        Guid currentRunId,
        bool holdsRealLock,
        Func<Guid, bool> isLiveTenant,
        CancellationToken cancellationToken = default)
    {
        var databaseName = database.DatabaseNamespace.DatabaseName;
        SchemaHealReport Refused(string reason) => new(databaseName, profile, Array.Empty<HealedIndex>(), reason);

        // Cheap checks first: never even READ from a database this process may not heal.
        if (!holdsRealLock || !MongoResidueSweeper.IsOwnedName(databaseName))
        {
            return Refused(MayHeal(databaseName, null, currentRunId, holdsRealLock).Reason);
        }

        var decision = MayHeal(
            databaseName,
            await MongoResidueSweeper.ReadMarkerAsync(database, cancellationToken),
            currentRunId,
            holdsRealLock);
        if (!decision.Allowed)
        {
            return Refused(decision.Reason);
        }

        var candidates = IndexesNamedBy(profile, target);
        if (candidates.Count == 0)
        {
            return Refused($"'{target.Collection}.{target.Index}' is not a unique index the {profile} profile declares");
        }

        // Every index and every group is judged BEFORE anything is deleted: one global key, one live tenant or one
        // unattributable group refuses the whole heal.
        var plan = new List<(string Collection, SchemaIndex Index, string[] KeyNames, List<BsonDocument> Groups)>();
        foreach (var (collectionName, index) in candidates)
        {
            var keyNames = index.Key.Names.ToArray();
            var tenantPosition = Array.IndexOf(keyNames, TenantKey);
            if (tenantPosition < 0)
            {
                return Refused(
                    $"'{collectionName}.{index.Name}' has no {TenantKey} in its key ({string.Join(", ", keyNames)}), so a "
                    + "duplicate group can span tenants and nothing proves it is test residue — a human decides");
            }

            var groups = await FindDuplicateGroupsAsync(database, collectionName, index, keyNames, cancellationToken);
            foreach (var group in groups)
            {
                var tenant = tenantPosition >= 0 ? AsGuid(group["_id"][$"k{tenantPosition}"]) : null;
                if (tenant is null)
                {
                    return Refused(
                        $"a duplicate group on '{collectionName}.{index.Name}' has no tenant id, so it is not "
                        + "attributable to a finished test — a human decides");
                }

                if (isLiveTenant(tenant.Value))
                {
                    return Refused(
                        $"a duplicate group on '{collectionName}.{index.Name}' belongs to tenant {tenant.Value}, which a "
                        + "harness of THIS process handed out — a live test's rows are never residue");
                }
            }

            if (groups.Count > 0)
            {
                plan.Add((collectionName, index, keyNames, groups));
            }
        }

        if (plan.Count == 0)
        {
            return Refused(
                $"no duplicate rows found for '{target.Collection}.{target.Index}' — the finder cannot model this failure");
        }

        var healed = new List<HealedIndex>();
        foreach (var (collectionName, index, keyNames, groups) in plan)
        {
            var ids = groups.SelectMany(g => g["ids"].AsBsonArray).ToList();
            var deleted = await database.GetCollection<BsonDocument>(collectionName)
                .DeleteManyAsync(Builders<BsonDocument>.Filter.In("_id", ids), cancellationToken);

            var samples = groups
                .Take(5)
                .Select(g => string.Join(", ", keyNames.Select((name, i) => $"{name}: {g["_id"][$"k{i}"]}")))
                .ToArray();
            healed.Add(new HealedIndex(collectionName, index.Name, groups.Count, (int)deleted.DeletedCount, samples));
        }

        return new SchemaHealReport(databaseName, profile, healed, null);
    }

    /// <summary>
    /// The unique indexes of <paramref name="profile"/> the healer may touch for <paramref name="target"/>: exactly the
    /// one the failure names (round 2 — the first version healed EVERY unique index of the profile).
    /// </summary>
    public static IReadOnlyList<(string Collection, SchemaIndex Index)> IndexesNamedBy(SchemaProfile profile, DuplicateKeyTarget target)
        => PlatformSchemaManifest.For(profile)
            .SelectMany(c => c.Indexes.Where(i => i.Unique).Select(i => (Collection: c.Name, Index: i)))
            .Where(x => x.Collection == target.Collection && x.Index.Name == target.Index)
            .ToArray();

    private static async Task<List<BsonDocument>> FindDuplicateGroupsAsync(
        IMongoDatabase database,
        string collectionName,
        SchemaIndex index,
        string[] keyNames,
        CancellationToken cancellationToken)
    {
        // A unique index treats a missing field and an explicit null as the SAME key; $group does not, so both are
        // folded to null before grouping. Generated names (k0, k1…) because a dotted index path is not a legal field
        // name inside $group's _id.
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

        return await (await database.GetCollection<BsonDocument>(collectionName).AggregateAsync(
                PipelineDefinition<BsonDocument, BsonDocument>.Create(pipeline),
                cancellationToken: cancellationToken))
            .ToListAsync(cancellationToken);
    }

    private static Guid? AsGuid(BsonValue value) => value switch
    {
        BsonBinaryData { SubType: BsonBinarySubType.UuidStandard } binary => binary.ToGuid(),
        BsonBinaryData { SubType: BsonBinarySubType.UuidLegacy } binary => binary.ToGuid(GuidRepresentation.CSharpLegacy),
        BsonString text when Guid.TryParse(text.Value, out var parsed) => parsed,
        _ => null
    };
}
