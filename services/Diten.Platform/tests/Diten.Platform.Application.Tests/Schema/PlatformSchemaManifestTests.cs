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

    [Fact]
    public void TheDeclaredBudgetsAreTheNumbersTheOwnersApproved()
    {
        /*
         * ⚠ THE TEST ABOVE CANNOT CATCH THE FAILURE THIS ROUND WAS ABOUT. DeclaredBudgetsAreRespected asks
         * "is the manifest inside the ceiling" — so the way to make it green is to raise the ceiling, which
         * is precisely the move SchemaProfileBudget's own header warns against ("teaches the reader to raise
         * the number instead of looking at it"). Raising MaxCollections from 8 to 9 does not turn that test
         * red at all; it just widens the gate and nothing says so.
         *
         * So the ceiling itself is pinned here, to the numbers an owner actually signed off:
         *
         *   MaxCollections    8   — GSKU owners, 2026-08-26. NOT raised by BL-298.
         *   MaxLogicalIndexes 19  — GSKU owners, 2026-08-28 (BL-298), raised from 18 by exactly one, on the
         *                           BL-279 measurement for business_reference_data_validation_results.
         *
         * MUTATION GUARD: change either number and this goes red pointing at the owner decision, which is
         * the conversation that has to happen before a ceiling moves. Editing this test to match a new
         * number is not a workaround — it is the edit that says an owner approved it, and it shows up in
         * review as exactly that.
         */
        var budget = SchemaProfileBudget.BusinessReferenceData;

        Assert.Equal(8, budget.MaxCollections);
        Assert.Equal(19, budget.MaxLogicalIndexes);

        // And the profile really is at the ceiling, so the next index cannot slip in unnoticed either.
        var collections = PlatformSchemaManifest.For(SchemaProfile.BusinessReferenceData);
        Assert.Equal(8, collections.Count);
        Assert.Equal(19, collections.Sum(c => c.LogicalIndexCount));
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

        var offenders = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var (file, name) in CollectionNameLiterals(src))
        {
            if (otherDatabase.Contains(name) || declared.Contains(name)) continue;
            offenders.Add($"{name}  ({file})");
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
        var offenders = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var (file, name) in CollectionNameLiterals(src))
        {
            if (!declared.Contains(name)) continue;
            offenders.Add($"{name}  ({file})");
        }

        Assert.True(offenders.Count == 0,
            "a collection name is typed out again outside the manifest — use the PlatformCollections "
            + "constant so a rename cannot half-land:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void NoCollectionNameShapedLiteralSurvivesOutsideItsDeclaration()
    {
        /*
         * ⚠ THE SHAPE-INDEPENDENT BACKSTOP. The two checks above still hunt for CALL SHAPES, and a third
         * spelling would slip past them the way `: base(…)` slipped past the first one. This one does not
         * care how the name travels: in the persistence layer, a string literal that LOOKS like a collection
         * name must not exist at all outside the one place that declares it.
         *
         * The exceptions are the six names whose single declaration predates PlatformCollections and lives
         * next to its owner — AuditCollectionNames, SeedMarkerStore, PersonReferenceRepository. Those are
         * still ONE declaration each, which is the property that matters; the manifest references the
         * constant rather than re-typing the string.
         *
         * MUTATION GUARD: write "task_comments" anywhere in Persistence/ and this goes red with the file.
         */
        var persistence = Path.Combine(
            RepoRoot(), "services", "Diten.Platform", "src",
            "Diten.Platform.Infrastructure", "Persistence");

        // snake_case with at least one underscore — the grammar every collection in this database follows.
        var grammar = new Regex(@"^[a-z][a-z0-9]*(?:_[a-z0-9]+)+$", RegexOptions.Compiled);
        var literal = new Regex(@"""([a-z][a-z0-9_]{3,})""", RegexOptions.Compiled);

        // Each of these files DECLARES one name, once, and the manifest points at that declaration.
        var declarationSites = new HashSet<string>(StringComparer.Ordinal)
        {
            "AuditCollectionNames.cs", "SeedMarkerStore.cs", "PersonReferenceRepository.cs"
        };

        // A database name, not a collection: PositionAssignmentSeed reaches into the AuthService database.
        var notACollection = new HashSet<string>(StringComparer.Ordinal) { "diten_auth_v3" };

        var declared = PlatformSchemaManifest.All.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
        var offenders = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(persistence, "*.cs", SearchOption.AllDirectories))
        {
            var normalized = file.Replace('\\', '/');
            if (normalized.Contains("/obj/") || normalized.Contains("/bin/")) continue;
            if (normalized.Contains("/Persistence/Schema/")) continue;

            var name = Path.GetFileName(file);
            if (declarationSites.Contains(name)) continue;

            foreach (Match match in literal.Matches(File.ReadAllText(file)))
            {
                var value = match.Groups[1].Value;
                if (!grammar.IsMatch(value) || notACollection.Contains(value)) continue;
                if (!declared.Contains(value) && !LooksLikeACollection(value)) continue;
                offenders.Add($"{value}  ({name})");
            }
        }

        Assert.True(offenders.Count == 0,
            "a collection name is written out as a literal in the persistence layer — use the "
            + "PlatformCollections constant, so the manifest stays the only place the name exists:\n"
            + string.Join("\n", offenders));
    }

    /*
     * A literal the manifest does not know, in the persistence layer, matching the collection grammar, is
     * reported rather than ignored: it is either a collection nobody declared (the BL-279 defect) or a field
     * name that reads like one. Both are worth a human look; neither should be silently dropped.
     */
    private static bool LooksLikeACollection(string value)
        => value.StartsWith("task_", StringComparison.Ordinal)
           || value.StartsWith("platform_", StringComparison.Ordinal)
           || value.StartsWith("tenant_", StringComparison.Ordinal)
           || value.StartsWith("workflow_", StringComparison.Ordinal)
           || value.StartsWith("document_management_", StringComparison.Ordinal)
           || value.StartsWith("business_reference_data_", StringComparison.Ordinal)
           || value.StartsWith("notification_", StringComparison.Ordinal);

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

    /*
     * ── HOW A COLLECTION NAME REACHES MONGO, ALL OF IT ────────────────────────────────────────────────────
     *
     * ⚠ THE FIRST VERSION OF THIS SCAN SAW ONE SHAPE AND MISSED SIX COLLECTIONS. It matched
     * `GetCollection<T>("…")` only — but most repositories here never write that call. They derive from the
     * generic base and hand the name to it as a CONSTRUCTOR ARGUMENT:
     *
     *     public TaskCommentRepository(...) : base(dbContext.Database, tenantContext, "task_comments")
     *
     * Seventy call sites take that form. task_comments, task_types, task_transitions,
     * document_reference_list_versions, document_management_collection_deviations and
     * document_management_collection_provisioning_evidence were all read by production code, indexed by
     * nothing, and invisible to the check that existed to find exactly that (BL-279). A guard that covers
     * one of two spellings is worse than none, because it is believed.
     */
    private static readonly Regex[] NamePassedToMongo =
    {
        new(@"GetCollection<[^>]+>\(\s*""([a-z_0-9]+)""\s*\)", RegexOptions.Compiled),
        new(@":\s*base\([^)]*?,\s*""([a-z_0-9]+)""", RegexOptions.Compiled)
    };

    /// <summary>Every collection-name literal in production source, whichever way it is handed to Mongo.</summary>
    private static IEnumerable<(string File, string Name)> CollectionNameLiterals(string src)
    {
        foreach (var file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            var normalized = file.Replace('\\', '/');
            if (normalized.Contains("/obj/") || normalized.Contains("/bin/")) continue;
            if (normalized.Contains("/Persistence/Schema/")) continue; // the manifest is where the name lives

            var text = File.ReadAllText(file);
            var relative = Path.GetRelativePath(src, file).Replace('\\', '/');

            foreach (var pattern in NamePassedToMongo)
            {
                foreach (Match match in pattern.Matches(text))
                {
                    yield return (relative, match.Groups[1].Value);
                }
            }
        }
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
