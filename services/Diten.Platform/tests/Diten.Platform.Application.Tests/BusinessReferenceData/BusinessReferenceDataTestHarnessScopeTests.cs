using System.Text.RegularExpressions;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

/*
 * BL-482 (BRD half) — THE THREE PROPERTIES THE BRD TEST HARNESS NOW RESTS ON, EACH PROVED ON ITS OWN.
 *
 * The 53 red BRD tests were not 53 defects. They were two harness defects (see BusinessReferenceDataTestHarness),
 * and every one of them was invisible in the test that failed: the message named a ping, or a database belonging
 * to another class. These tests name the rule that was broken instead.
 */
public sealed class BusinessReferenceDataTestHarnessScopeTests
{
    private const string Scope = "harness_scope_proof";

    /*
     * SABOTAGE: remove the OpenScopes.TryAdd refusal in BusinessReferenceDataTestHarness.CreateAsync → the second
     * CreateAsync succeeds, empties the database under the first owner, and this is red.
     */
    [Fact]
    public async Task A_scope_that_is_already_open_is_refused_and_opens_again_once_released()
    {
        var first = await BusinessReferenceDataTestHarness.CreateAsync(Scope);
        try
        {
            var sets = first.Database.GetCollection<BsonDocument>(PlatformCollections.BusinessReferenceDataSets);
            await sets.InsertOneAsync(Row(first.ReferenceTenantId, "owner-row"));

            var refused = await Assert.ThrowsAsync<InvalidOperationException>(
                () => BusinessReferenceDataTestHarness.CreateAsync(Scope));

            Assert.Contains(Scope, refused.Message);
            Assert.Contains("already open", refused.Message);
            // The refusal came BEFORE the emptying: the first owner's row is still there.
            Assert.Equal(1, await sets.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        }
        finally
        {
            await first.DisposeAsync();
        }

        await using var second = await BusinessReferenceDataTestHarness.CreateAsync(Scope);
        Assert.Equal(
            BusinessReferenceDataTestHarness.DatabaseNameFor(Scope),
            second.Database.DatabaseNamespace.DatabaseName);
    }

    /*
     * CT acceptance (2026-10-01). The scope set is CLOSED. A per-run name is inside the grammar (lowercase letters and
     * digits), so the grammar could not refuse it; the registry does, and before Mongo is touched — the database is
     * not there afterwards.
     *
     * SABOTAGE: remove the RegisteredScopes check in CreateAsync → the per-run scope opens, its database appears, and
     * this is red.
     */
    [Fact]
    public async Task A_scope_that_is_not_registered_is_refused_before_a_database_exists()
    {
        var perRun = $"run_{Guid.NewGuid():N}";
        var databaseName = BusinessReferenceDataTestHarness.DatabaseNameFor(perRun); // inside the grammar

        var refused = await Assert.ThrowsAsync<ArgumentException>(
            () => BusinessReferenceDataTestHarness.CreateAsync(perRun));

        Assert.Contains("not registered", refused.Message);
        var client = new MongoClient(PlatformMongoTestConnection.RequireConnectionString());
        var names = await (await client.ListDatabaseNamesAsync()).ToListAsync();
        Assert.DoesNotContain(databaseName, names);
    }

    // Every registered scope is a permanent database: the list is the budget, and it is pinned so it cannot grow unseen.
    [Fact]
    public void The_registered_scopes_are_exactly_the_eight_this_folder_owns()
    {
        Assert.Equal(8, BusinessReferenceDataTestHarness.RegisteredScopes.Count);
        Assert.All(BusinessReferenceDataTestHarness.RegisteredScopes,
            scope => Assert.Matches("^[a-z][a-z0-9_]*$", scope));
        Assert.Contains(Scope, BusinessReferenceDataTestHarness.RegisteredScopes);
    }

    /*
     * The blank slate is DELETED DOCUMENTS, not a dropped database: releasing a scope leaves the database, its
     * rows and its indexes where they are, and the next owner empties it on the way in.
     *
     * SABOTAGE: open the scope with MongoIntegrationHarness.CreateAsync-style `emptyFirst: false` (or skip the
     * EmptyAsync call) → the second owner inherits the first owner's row and this is red.
     */
    [Fact]
    public async Task Releasing_a_scope_drops_nothing_and_the_next_owner_starts_blank_with_the_indexes_in_place()
    {
        const string scope = "harness_blank_proof";
        var databaseName = BusinessReferenceDataTestHarness.DatabaseNameFor(scope);
        var client = new MongoClient(PlatformMongoTestConnection.RequireConnectionString());

        await using (var first = await BusinessReferenceDataTestHarness.CreateAsync(scope))
        {
            await first.Database.GetCollection<BsonDocument>(PlatformCollections.BusinessReferenceDataSets)
                .InsertOneAsync(Row(first.ReferenceTenantId, "left-behind"));
        }

        var released = client.GetDatabase(databaseName)
            .GetCollection<BsonDocument>(PlatformCollections.BusinessReferenceDataSets);
        Assert.Equal(1, await released.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));

        await using var second = await BusinessReferenceDataTestHarness.CreateAsync(scope);
        var sets = second.Database.GetCollection<BsonDocument>(PlatformCollections.BusinessReferenceDataSets);

        Assert.Equal(0, await sets.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        var indexes = await (await sets.Indexes.ListAsync()).ToListAsync();
        Assert.Contains(indexes, index => index["name"] == "ux_business_reference_data_sets_tenant_code");
    }

    /*
     * THE TWO SHAPES THAT MADE THE BASELINE RED, HELD OUT OF THE SOURCE.
     *
     *   • `RunCommandAsync<object>`: a replica set answers every command with $clusterTime / operationTime, which are
     *     BSON Timestamps, and ObjectSerializer cannot materialise one. It passes on a standalone mongod and fails
     *     on the developer's replica set. Read a command reply as BsonDocument.
     *   • a database drop in this folder: the shared mongod's databases are emptied, never dropped. The residue
     *     sweeper and its tests are the one exception, and they must stay off the shared mongod.
     *
     * SABOTAGE: put `await _database.RunCommandAsync<object>("{ ping: 1 }");` back into
     * BusinessReferenceDataTenantAssignmentMongoTests, or `DropDatabaseAsync` back into the harness's DisposeAsync
     * → this names the file.
     * Weakness, stated: a text match on code lines. Comment lines are skipped; a block comment's middle lines are
     * only skipped when they start with `*`.
     */
    [Fact]
    public void No_test_reads_a_command_reply_as_object_and_no_BRD_test_drops_a_shared_database()
    {
        var project = Path.Combine(RepoPaths.Services(), "Diten.Platform", "tests", "Diten.Platform.Application.Tests");
        var separator = Path.DirectorySeparatorChar;
        var files = Directory.EnumerateFiles(project, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{separator}obj{separator}") && !path.Contains($"{separator}bin{separator}"))
            .Select(path => (Path: Path.GetRelativePath(project, path).Replace('\\', '/'), Code: CodeLines(path)))
            .Where(file => file.Path != "BusinessReferenceData/BusinessReferenceDataTestHarnessScopeTests.cs")
            .ToArray();

        // A scan that silently finds nothing is green forever.
        Assert.Contains(files, file => file.Path == "BusinessReferenceData/BusinessReferenceDataGskuCatalogLoadMongoTests.cs");

        // `object`, `object?`, `Object`, `System.Object` and `dynamic` all take the same serializer path.
        var objectReply = new Regex(@"RunCommand(?:Async)?\s*<\s*(?:(?:System\s*\.\s*)?[Oo]bject\??|dynamic)\s*>");
        var readingAsObject = files.Where(file => objectReply.IsMatch(file.Code)).Select(file => file.Path).ToArray();
        Assert.True(readingAsObject.Length == 0,
            "these files read a Mongo command reply as `object`; on a replica set the reply carries BSON Timestamps "
            + "and ObjectSerializer throws. Use RunCommandAsync<BsonDocument>:\n" + string.Join("\n", readingAsObject));

        var sweeperPair = new[]
        {
            "BusinessReferenceData/BusinessReferenceDataMongoResidueSweeper.cs",
            "BusinessReferenceData/BusinessReferenceDataMongoResidueSweeperTests.cs"
        };
        var drop = new Regex(@"\bDrop(?:Database|Collection)(?:Async)?\s*\(");
        var brd = files.Where(file => file.Path.StartsWith("BusinessReferenceData/", StringComparison.Ordinal)).ToArray();
        var dropping = brd
            .Where(file => drop.IsMatch(file.Code) && !sweeperPair.Contains(file.Path))
            .Select(file => file.Path)
            .ToArray();
        Assert.True(dropping.Length == 0,
            "these BRD test files drop a database or collection; on the shared mongod a scope is emptied, never "
            + "dropped (BusinessReferenceDataTestHarness):\n" + string.Join("\n", dropping));

        // …by its address, by the shared harness's connection string, or by a client built with no address at all.
        var sharedMongod = new Regex(
            @"mongodb://(?:localhost|127\.0\.0\.1):27017|MongoIntegrationHarness\s*\.\s*ConnectionString|new\s+MongoClient\s*\(\s*\)");
        var sweeperOnShared = files
            .Where(file => sweeperPair.Contains(file.Path) && sharedMongod.IsMatch(file.Code))
            .Select(file => file.Path)
            .ToArray();
        Assert.True(sweeperOnShared.Length == 0,
            "the BRD residue sweeper drops databases, so it and its tests must not address the shared mongod:\n"
            + string.Join("\n", sweeperOnShared));

        // CreateDatabaseAsync makes a Guid-named database; SweepAsync is the method that actually drops.
        var createsPerRunDatabase = new Regex(
            @"BusinessReferenceDataMongoResidueSweeper\s*\.\s*(?:CreateDatabaseAsync|SweepAsync)\s*\(");
        var perRun = files
            .Where(file => createsPerRunDatabase.IsMatch(file.Code) && !sweeperPair.Contains(file.Path))
            .Select(file => file.Path)
            .ToArray();
        Assert.True(perRun.Length == 0,
            "these files create a Guid-named BRD database per run, or call the sweeper that drops; use "
            + "BusinessReferenceDataTestHarness.CreateAsync with a fixed scope, or CreateSharedAsync with a fresh tenant:\n"
            + string.Join("\n", perRun));

        /*
         * CT acceptance (2026-10-01). ONE SCOPE, ONE FILE — statically. The harness refuses a scope that is already
         * open, but only when the two owners overlap in time, which is exactly the intermittent red this folder had.
         * Every scope literal handed to CreateAsync must sit in one file only, and must be a registered scope.
         */
        var scopeLiteral = new Regex(@"BusinessReferenceDataTestHarness\s*\.\s*CreateAsync\s*\(\s*""(?<scope>[^""]*)""");
        var owners = files
            .SelectMany(file => scopeLiteral.Matches(file.Code).Select(match => (Scope: match.Groups["scope"].Value, file.Path)))
            .GroupBy(x => x.Scope, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(x => x.Path).Distinct().ToArray(), StringComparer.Ordinal);
        Assert.True(owners.Count >= 6, "the scope scan found fewer CreateAsync(\"scope\") calls than this folder is known to make");
        Assert.Empty(owners.Where(owner => owner.Value.Length > 1).Select(owner => $"{owner.Key}: {string.Join(", ", owner.Value)}"));
        Assert.Empty(owners.Keys.Where(scope => !BusinessReferenceDataTestHarness.RegisteredScopes.Contains(scope)));
    }

    private static string CodeLines(string path)
        => string.Join(
            "\n",
            File.ReadLines(path).Where(line =>
            {
                var trimmed = line.TrimStart();
                return !trimmed.StartsWith("//", StringComparison.Ordinal)
                       && !trimmed.StartsWith("*", StringComparison.Ordinal)
                       && !trimmed.StartsWith("/*", StringComparison.Ordinal);
            }));

    private static BsonDocument Row(Guid tenantId, string setCode)
        => new()
        {
            ["_id"] = Guid.NewGuid().ToString("N"),
            [nameof(BusinessReferenceDataSet.TenantId)] = tenantId.ToString(),
            [nameof(BusinessReferenceDataSet.SetCode)] = setCode,
            [nameof(BusinessReferenceDataSet.IsDeleted)] = false
        };
}
