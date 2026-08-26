using Xunit;
using System.Text.RegularExpressions;
using Diten.Platform.Infrastructure.Persistence.Schema;

namespace Diten.Platform.Application.Tests.Schema;

/*
 * THE MANIFEST'S OWN CONTRACT — the half that needs no Mongo.
 *
 * These run on a machine where mongod is dead, which matters: the manifest exists because mongod kept dying,
 * and a check that only works when the database is healthy is no use in the state it was built for.
 * The half that DOES need a real database — "the index Mongo actually built matches what the manifest
 * declared" — is in PlatformSchemaContractMongoTests, and it cannot be replaced by anything here.
 */
public class PlatformSchemaManifestTests
{
    // ── FAIL-CLOSED ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AskingForNoProfileIsRejected()
    {
        /*
         * ⚠ THE POINT IS THE TIMING OF THE FAILURE. If an empty request quietly returned an empty schema, the
         * test that made it would not fail here — it would fail much later, as a query returning nothing, and
         * the reader would go looking for a bug in the repository.
         */
        Assert.Throws<ArgumentException>(() => PlatformSchemaManifest.For());
        Assert.Throws<ArgumentException>(() => PlatformSchemaManifest.For(Array.Empty<SchemaProfile>()));
    }

    [Fact]
    public void AskingForAnUnknownProfileIsRejected()
    {
        // A cast integer must not resolve to "no collections". That is the same silent-empty failure as above,
        // reached by a typo instead of an omission.
        Assert.Throws<ArgumentOutOfRangeException>(() => PlatformSchemaManifest.For((SchemaProfile)9999));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlatformSchemaManifest.For((SchemaProfile)0));
    }

    // ── THE PRODUCTION PATH MUST NOT NARROW ────────────────────────────────────────────────────────────────

    [Fact]
    public void ProductionBuildsTheUnionOfEveryProfile()
    {
        /*
         * MUTATION GUARD: drop a profile from the union inside PlatformSchemaManifest.AllCollections and this
         * goes red naming it. Without this test that edit is invisible — production stops building those
         * indexes, and NOTHING fails, because a missing index in Mongo is not an error. The query runs
         * unindexed. It is slower, not broken, so it ships and stays.
         */
        var union = PlatformSchemaManifest.KnownProfiles
            .SelectMany(p => PlatformSchemaManifest.For(p))
            .Select(c => c.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        var all = PlatformSchemaManifest.All
            .Select(c => c.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(union, all);

        // And every declared profile must actually carry something — an empty profile is a profile someone
        // forgot to fill in, and it would make the union test above pass for the wrong reason.
        var empty = PlatformSchemaManifest.KnownProfiles
            .Where(p => PlatformSchemaManifest.For(p).Count == 0)
            .ToArray();
        Assert.True(empty.Length == 0, $"profiles with no collections: {string.Join(", ", empty)}");
    }

    [Fact]
    public void EveryCollectionIsDeclaredExactlyOnceAndBelongsToOneProfile()
    {
        var duplicates = PlatformSchemaManifest.All
            .GroupBy(c => c.Name, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} -> {string.Join(", ", g.Select(c => c.Profile))}")
            .ToArray();

        Assert.True(duplicates.Length == 0,
            "a collection is declared in more than one place — the profiles would build it twice and could "
            + "disagree about its indexes:\n" + string.Join("\n", duplicates));
    }

    // ── BUDGET ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DeclaredBudgetsAreRespected()
    {
        /*
         * ⚠ LOGICAL BUDGET, NOT A FILE COUNT. The failure being prevented is file-descriptor exhaustion, so
         * the obvious acceptance criterion is "files created per run" — but that number moves with the Mongo
         * version, the storage engine and the OS, so a test pinned to it would be red on one machine and
         * green on another for reasons unrelated to the schema. Collections and indexes are what the manifest
         * controls and are the same number everywhere.
         *
         * MUTATION GUARD: add a collection to the BusinessReferenceData profile and this goes red.
         */
        foreach (var budget in SchemaProfileBudget.Declared)
        {
            var collections = PlatformSchemaManifest.For(budget.Profile);
            var indexes = collections.Sum(c => c.LogicalIndexCount);

            Assert.True(collections.Count <= budget.MaxCollections,
                $"{budget.Profile} carries {collections.Count} collections against a budget of "
                + $"{budget.MaxCollections}: {string.Join(", ", collections.Select(c => c.Name))}");

            Assert.True(indexes <= budget.MaxLogicalIndexes,
                $"{budget.Profile} carries {indexes} indexes (including the implicit _id on each collection) "
                + $"against a budget of {budget.MaxLogicalIndexes}. Per collection: "
                + string.Join(", ", collections.Select(c => $"{c.Name}={c.LogicalIndexCount}")));
        }
    }

    // ── THE REPOSITORIES AND THE MANIFEST NAME THE SAME COLLECTIONS ────────────────────────────────────────

    [Fact]
    public void EveryCollectionTheProductionCodeTouchesIsInTheManifest()
    {
        /*
         * Contract item 1. A repository that reads a collection the manifest never declares is the quiet
         * failure this whole file is about: Mongo creates the collection on first write and answers the query
         * without an index. Nothing throws. It is measurably slower and completely invisible.
         *
         * ⚠ THIS FOUND THREE ON THE DAY IT WAS WRITTEN — business_reference_data_validation_results,
         * document_reference_entries and notification_event_definitions were read by repositories and named
         * nowhere in the index configuration. They are in the manifest now, carrying no index, which is a
         * finding recorded rather than a gap hidden.
         */
        var root = RepoRoot();
        var src = Path.Combine(root, "services", "Diten.Platform", "src");
        var declared = PlatformSchemaManifest.All.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);

        // Lives in the AuthService database, reached through database.Client — not this schema's to own.
        var otherDatabase = new HashSet<string>(StringComparer.Ordinal) { "users" };

        var pattern = new Regex(@"GetCollection<[^>]+>\(""([a-z_0-9]+)""\)", RegexOptions.Compiled);
        var offenders = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            var normalized = file.Replace('\\', '/');
            if (normalized.Contains("/obj/") || normalized.Contains("/bin/")) continue;

            foreach (Match match in pattern.Matches(File.ReadAllText(file)))
            {
                var name = match.Groups[1].Value;
                if (otherDatabase.Contains(name) || declared.Contains(name)) continue;
                offenders.Add($"{name}  ({Path.GetRelativePath(src, file).Replace('\\', '/')})");
            }
        }

        Assert.True(offenders.Count == 0,
            "these collections are used by production code but are not declared in PlatformSchemaManifest — "
            + "every query against them runs unindexed and nothing reports it:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void TheManifestNamesEachCollectionOnceInSourceToo()
    {
        /*
         * The manifest owning the name is only worth something if nobody re-types it. A literal that matches
         * a PlatformCollections constant is a second copy of the name, and a rename would land on one of them.
         */
        var root = RepoRoot();
        var src = Path.Combine(root, "services", "Diten.Platform", "src");
        var declared = PlatformSchemaManifest.All.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
        var pattern = new Regex(@"GetCollection<[^>]+>\(""([a-z_0-9]+)""\)", RegexOptions.Compiled);
        var offenders = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            var normalized = file.Replace('\\', '/');
            if (normalized.Contains("/obj/") || normalized.Contains("/bin/")) continue;
            if (normalized.Contains("/Persistence/Schema/")) continue; // the manifest is where the name lives

            foreach (Match match in pattern.Matches(File.ReadAllText(file)))
            {
                if (!declared.Contains(match.Groups[1].Value)) continue;
                offenders.Add($"{match.Groups[1].Value}  ({Path.GetRelativePath(src, file).Replace('\\', '/')})");
            }
        }

        Assert.True(offenders.Count == 0,
            "a collection name is typed out again outside the manifest — use the PlatformCollections "
            + "constant so a rename cannot half-land:\n" + string.Join("\n", offenders));
    }

    // ── MEASURED, NOT PINNED ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheManifestIsNotEmptyAndCarriesRealIndexes()
    {
        /*
         * ⚠ REPORTED WITH A FLOOR, NOT A HARD COUNT. Sizes move whenever a module adds an index and that is
         * fine. What must never happen is the manifest collapsing to nothing and every check above passing
         * vacuously — which is exactly how a guard stays green while seeing a fraction of its surface.
         */
        Assert.True(PlatformSchemaManifest.All.Count > 70,
            $"the manifest collapsed to {PlatformSchemaManifest.All.Count} collections");

        var indexes = PlatformSchemaManifest.All.Sum(c => c.Indexes.Count);
        Assert.True(indexes > 200, $"the manifest declares only {indexes} indexes");

        // Every declared index must have a name — an unnamed one cannot be compared against listIndexes.
        var unnamed = PlatformSchemaManifest.All
            .SelectMany(c => c.Indexes.Select(i => (c.Name, i)))
            .Where(x => string.IsNullOrWhiteSpace(x.i.Name))
            .Select(x => x.Name)
            .ToArray();
        Assert.True(unnamed.Length == 0, $"indexes with no resolvable name on: {string.Join(", ", unnamed)}");
    }

    private static string RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md"))) return current.FullName;
            current = current.Parent;
        }

        throw new InvalidOperationException("Repo root not found.");
    }
}
