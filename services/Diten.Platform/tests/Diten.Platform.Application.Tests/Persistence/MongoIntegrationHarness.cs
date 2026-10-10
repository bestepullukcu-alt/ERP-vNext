using Diten.Platform.Common.Tenancy;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Diten.Platform.Application.Tests.Persistence;

// Talks to a REAL MongoDB supplied by DITEN_PLATFORM_TEST_MONGO_URI, not a fake. Repository-level defects such as the
// "cannot sort with keys that are parallel arrays" failure that killed the MOD-0023 transition gate are
// invisible to fake repositories — the Mongo query never runs, so the whole suite stays green while the
// feature is dead in production. Tests built on this harness deliberately have no skip-if-unavailable
// escape hatch: a missing Mongo is a broken dev environment, and silently skipping is what let the bug ship.
//
// ⚠ WHAT CHANGED, AND WHY (2026-08-26). This used to open a database named with a fresh Guid for every test
// class. One run therefore created a database per class; combined with the old EnsureIndexesAsync, which
// built ALL 82 collections and 218 indexes whatever the test touched, a single run walked past the macOS
// 10,240 open-files-per-process limit and mongod killed itself with fassert — and once it died, the
// disposal that drops those databases never ran, so the wreckage was still there for the next run.
//
// Two things fix it, and both are visible in this file:
//   • the harness asks for the SCHEMA PROFILES the test actually uses, not the whole platform;
//   • isolation comes from a fresh TenantId inside ONE shared database, not from a new database name.
//
// ⚠ AND ONE HONEST EXCEPTION, see CreateIsolatedAsync: a test whose SUBJECT is database-global state — a
// platform-default row, an idempotent seed that must produce exactly one row in the whole database — cannot
// be isolated by tenant, because the thing under test is not tenant-scoped. Those get a database of their
// own, under a FIXED name that is dropped and reused rather than a new one per run. Fixed is the property
// that matters: it cannot accumulate.
public sealed class MongoIntegrationHarness : IAsyncDisposable
{
    public const string ConnectionStringEnvironmentVariable = PlatformMongoTestConnection.EnvironmentVariableName;

    /// <summary>The one database every tenant-isolated test shares.</summary>
    public const string SharedDatabaseName = "diten_platform_itest";

    private static readonly SemaphoreSlim SchemaGate = new(1, 1);
    private static readonly HashSet<string> AppliedSchemas = new(StringComparer.Ordinal);

    /*
     * ⚠ RESIDUE CLEANUP, AND WHY IT IS NOT ENOUGH TO DROP ON THE WAY OUT. A harness that tidies up when it
     * finishes only tidies up when it FINISHES — and the failure this whole body of work is about is mongod
     * dying mid-run, which is precisely the case where nothing finishes. Measured 2026-08-26: 6 test
     * databases left on this machine, 3 of them named under a scheme the harness stopped using two stages
     * ago. So there is a second line: at the start of a run, sweep what an earlier run abandoned.
     *
     * See MongoResidueSweeper for the four conditions. The short version: a name match alone never deletes
     * anything — the database must carry a marker THIS harness wrote.
     */
    private static readonly Guid RunId = Guid.NewGuid();
    private static readonly RunOnceGate SweepGate = new();
    private static SweepReport? _sweepReport;

    /// <summary>What the start-of-run sweep did. Null until it has run.</summary>
    public static SweepReport? LastSweep => _sweepReport;

    /// <summary>The run id this process stamps into every database it opens (read by the healer's live tests).</summary>
    internal static Guid CurrentRunId => RunId;

    private readonly IMongoClient _client;
    private readonly string? _databaseToDropOnDispose;

    private MongoIntegrationHarness(
        IMongoClient client,
        IMongoDatabase database,
        string databaseName,
        string? databaseToDropOnDispose)
    {
        _client = client;
        Database = database;
        DatabaseName = databaseName;
        _databaseToDropOnDispose = databaseToDropOnDispose;
        TenantContext = new TenantContext();
        TenantContext.SetTenant(TenantId);
        DbContext = new PlatformDbContext(client, database);
    }

    public IMongoDatabase Database { get; }
    public string DatabaseName { get; }
    public Guid TenantId { get; } = IssueTenant();
    public TenantContext TenantContext { get; }
    public IPlatformDbContext DbContext { get; }

    /*
     * ⚠ BL-482: EVERY TENANT A HARNESS OF THIS PROCESS HANDS OUT IS REMEMBERED FOR THE LIFE OF THE PROCESS. Those are
     * LIVE tests' tenants — some class may be writing under one right now, through handlers, into a collection whose
     * profile it never asked for — so the duplicate-key heal never touches a group that belongs to one of them.
     * Only tenants of processes that have exited are residue.
     */
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, byte> IssuedTenants = new();

    private static Guid IssueTenant()
    {
        var tenant = Guid.NewGuid();
        IssuedTenants.TryAdd(tenant, 0);
        return tenant;
    }

    /// <summary>True when a harness of THIS process handed <paramref name="tenantId"/> out.</summary>
    internal static bool IsLiveTenant(Guid tenantId) => IssuedTenants.ContainsKey(tenantId);

    /// <summary>
    /// The default: ONE shared database, a fresh <see cref="TenantId"/> per harness, and only the schema
    /// profiles this test needs. Nothing is dropped — isolation is the tenant, so there is nothing to drop.
    /// </summary>
    public static Task<MongoIntegrationHarness> CreateAsync(params SchemaProfile[] profiles)
        => CreateCoreAsync(SharedDatabaseName, profiles, emptyFirst: false, dropOnDispose: false);

    /// <summary>
    /// For a test whose subject is DATABASE-GLOBAL rather than tenant-scoped — a platform-default row, or a
    /// seed that must be idempotent across the whole database. <paramref name="scope"/> is a fixed suffix,
    /// never a Guid, so it cannot pile up run after run.
    ///
    /// ⚠ IT EMPTIES THE COLLECTIONS; IT DOES NOT DROP THE DATABASE. Both give the test the blank slate it
    /// needs, and they are not remotely the same cost. xUnit runs IAsyncLifetime per TEST, so dropping meant
    /// rebuilding this profile's collections and indexes once per test method — measured at 2,227 files for
    /// one pass over the Persistence tests, which is the same file-count problem this round is fixing, just
    /// moved. Deleting the documents leaves the collections and indexes in place: measured at a handful.
    /// </summary>
    public static Task<MongoIntegrationHarness> CreateIsolatedAsync(string scope, params SchemaProfile[] profiles)
        => CreateCoreAsync(IsolatedDatabaseName(scope), profiles, emptyFirst: true, dropOnDispose: false);

    /// <summary>
    /// The scoped database's name — refused unless it is inside the owned grammar. BL-482 (L6): a scope with a hyphen
    /// (`eventing-outbox-idempotency`) produced a database the sweeper can never sweep and the heal can never heal,
    /// because neither will act outside the grammar; it would simply pile up. Refused here, before Mongo is touched.
    /// </summary>
    internal static string IsolatedDatabaseName(string scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            throw new ArgumentException("An isolated harness needs a stable scope name.", nameof(scope));
        }

        var name = $"{SharedDatabaseName}_{scope}";
        if (!MongoResidueSweeper.IsOwnedName(name))
        {
            throw new ArgumentException(
                $"Scope '{scope}' gives '{name}', which is outside the owned grammar ({MongoResidueSweeper.OwnedPrefix} "
                + "followed by _lowercase_tokens): the sweep and the heal could never act on it. Use lowercase letters, "
                + "digits and underscores.",
                nameof(scope));
        }

        return name;
    }

    private static async Task<MongoIntegrationHarness> CreateCoreAsync(
        string databaseName,
        SchemaProfile[] profiles,
        bool emptyFirst,
        bool dropOnDispose)
    {
        // BL-395: before this process touches the shared mongod at all — the ping, the residue sweep, the marker,
        // the schema, the emptying below — it holds the machine-wide lock. A second test process on this machine
        // waits here instead of emptying a scoped database this one is asserting against. See PlatformMongoTestLock.
        await PlatformMongoTestLock.EnsureHeldAsync();

        RegisterProductionSerializers();

        var settings = MongoClientSettings.FromConnectionString(PlatformMongoTestConnection.RequireConnectionString());
        settings.GuidRepresentation = GuidRepresentation.Standard;
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        var client = new MongoClient(settings);

        // Fail fast and loudly when Mongo is not reachable, instead of skipping the test.
        await client.GetDatabase(databaseName).RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));

        await SweepResidueOnceAsync(client);

        var database = client.GetDatabase(databaseName);

        // Stamp BEFORE anything else: from here on this database is visibly owned and visibly in use, so a
        // sweep running in another process cannot mistake it for residue.
        await MongoResidueSweeper.TouchAsync(database, RunId, DateTime.UtcNow);

        await EnsureSchemaAsync(database, databaseName, profiles);

        if (emptyFirst)
        {
            await EmptyAsync(database);
        }

        return new MongoIntegrationHarness(
            client,
            database,
            databaseName,
            dropOnDispose ? databaseName : null);
    }

    /*
     * Builds each requested profile at most once per database per process. Test classes run in parallel, so
     * the gate is not decoration — two classes asking for the same profile at the same moment would issue
     * overlapping createIndexes calls.
     *
     * ⚠ PlatformSchemaManifest.For REJECTS an empty request, and that rejection is wanted here: a harness
     * built with no profile would hand the test a database with no indexes, and the test would fail later as
     * a puzzling query result rather than here as "name the profiles you need".
     */
    private static async Task EnsureSchemaAsync(
        IMongoDatabase database,
        string databaseName,
        SchemaProfile[] profiles)
    {
        await SchemaGate.WaitAsync();
        try
        {
            foreach (var profile in profiles.Distinct())
            {
                var key = $"{databaseName}:{profile}";
                if (AppliedSchemas.Contains(key))
                {
                    continue;
                }

                await ApplyProfileHealingResidueAsync(database, profile);

                /*
                 * ⚠ BL-482: REMEMBERED ONLY AFTER IT SUCCEEDED. This used to be recorded BEFORE the build, so one
                 * failed build (a duplicate-key residue, a database dropped underneath it) marked the profile as
                 * applied and every later class in the run silently got collections with NO unique indexes —
                 * which is exactly how "the real unique index refuses a duplicate" tests came to insert
                 * duplicates that were accepted and stayed. A failed build now fails the next class that asks
                 * too, loudly, instead of handing it a database without its guarantees.
                 */
                AppliedSchemas.Add(key);
            }
        }
        finally
        {
            SchemaGate.Release();
        }
    }

    /// <summary>
    /// Builds one profile into a harness-owned database. If the build fails on DUPLICATE KEYS, the ONE index the
    /// failure names is healed under the rules in <see cref="MongoSchemaResidueHealer"/> (tenant-keyed index only,
    /// no live tenant of this process, real lock held, our marker), the removal is logged at once, and the build is
    /// retried. A retry that fails on ANOTHER named index heals that one too; the same index is never healed twice,
    /// so the loop ends. Returns the heal report, or null when the first build succeeded. A refusal, a heal that
    /// throws, or any other failure is thrown loudly — naming what was already removed (BL-482, L3).
    ///
    /// Internal so the heal can be proved directly (MongoSchemaResidueHealerMongoTests); the harness calls it
    /// under <see cref="SchemaGate"/>. <paramref name="log"/> defaults to stderr; tests pass their own.
    /// </summary>
    internal static async Task<SchemaHealReport?> ApplyProfileHealingResidueAsync(
        IMongoDatabase database,
        SchemaProfile profile,
        TextWriter? log = null)
    {
        log ??= Console.Error;
        var databaseName = database.DatabaseNamespace.DatabaseName;
        var healed = new List<HealedIndex>();

        while (true)
        {
            try
            {
                await PlatformSchemaManifest.ApplyAsync(database, new[] { profile });
                if (healed.Count == 0)
                {
                    return null;
                }

                var report = new SchemaHealReport(databaseName, profile, healed, null);
                log.WriteLine(
                    $"[MongoIntegrationHarness] BL-482 healed '{databaseName}' ({profile}): {report.RowsRemoved} "
                    + $"residue row(s) removed from {healed.Count} index(es); the schema build then succeeded.");
                return report;
            }
            catch (Exception failure) when (MongoSchemaResidueHealer.IsDuplicateKeyFailure(failure))
            {
                var target = MongoSchemaResidueHealer.ParseDuplicateKeyTarget(failure);
                SchemaHealReport step;
                if (target is null)
                {
                    step = new SchemaHealReport(databaseName, profile, Array.Empty<HealedIndex>(),
                        "the failure does not name the index it failed on");
                }
                else if (healed.Any(h => h.Collection == target.Collection && h.Index == target.Index))
                {
                    step = new SchemaHealReport(databaseName, profile, Array.Empty<HealedIndex>(),
                        $"'{target.Collection}.{target.Index}' was already healed once in this build and still fails");
                }
                else
                {
                    try
                    {
                        step = await MongoSchemaResidueHealer.HealAsync(
                            database, profile, target, RunId, PlatformMongoTestLock.HoldsRealLock, IsLiveTenant);
                    }
                    catch (Exception healFailure)
                    {
                        throw new InvalidOperationException(
                            $"[MongoIntegrationHarness] BL-482: healing '{target.Collection}.{target.Index}' in "
                            + $"'{databaseName}' threw{AlreadyRemoved(healed)}: {healFailure.Message}",
                            healFailure);
                    }
                }

                if (step.Refusal is not null)
                {
                    throw new InvalidOperationException(
                        $"{failure.Message} [BL-482: not healed — {step.Refusal}]{AlreadyRemoved(healed)}", failure);
                }

                foreach (var index in step.Healed)
                {
                    healed.Add(index);
                    // Logged the moment it happens, not after the whole heal: if a later step fails, the log already
                    // says what this one removed.
                    log.WriteLine(
                        $"[MongoIntegrationHarness] BL-482 removed {index.RowsRemoved} residue row(s) "
                        + $"({index.DuplicateKeys} duplicated key(s)) blocking unique index '{index.Index}' on "
                        + $"'{databaseName}.{index.Collection}'; sample keys: {string.Join(" | ", index.SampleKeys)}");
                }
            }
            catch (Exception failure) when (healed.Count > 0)
            {
                throw new InvalidOperationException(
                    $"[MongoIntegrationHarness] BL-482: the {profile} schema build of '{databaseName}' failed"
                    + $"{AlreadyRemoved(healed)}: {failure.Message}",
                    failure);
            }
        }
    }

    private static string AlreadyRemoved(IReadOnlyCollection<HealedIndex> healed)
        => healed.Count == 0
            ? string.Empty
            : $" (BL-482 had already removed {string.Join("; ", healed.Select(h => h.Describe()))})";

    /*
     * Runs at most once per process, and never throws into a test.
     *
     * ⚠ BL-482: EVERY CALLER WAITS FOR IT TO FINISH. The sweep can drop the SHARED database (when its marker is
     * older than the residue window, i.e. the first run after an idle hour). A caller that went on to stamp and
     * build that database while the sweep was still running had its indexes dropped underneath it — see
     * RunOnceGate for the measured failure.
     *
     * ⚠ A CLEANUP FAILURE MUST NOT BE REPORTED AS A TEST FAILURE. If this threw, the first test to construct
     * a harness would go red for a reason that has nothing to do with it, and the real defect underneath
     * would be attributed to housekeeping and waved away. Problems are collected and written to stderr, and
     * MongoResidueSweeperTests asserts that a failing drop is reported rather than swallowed AND rather than
     * propagated.
     */
    private static Task SweepResidueOnceAsync(IMongoClient client)
        => SweepGate.RunAsync(() => SweepResidueAndReportAsync(client));

    /// <summary>What the sweep reports, and writes to stderr, when this process does not hold the real lock.</summary>
    internal const string SweepSkippedWithoutRealLock =
        "residue sweep skipped: this process does not hold the real machine-wide test lock (lock-proof child)";

    private static async Task SweepResidueAndReportAsync(IMongoClient client)
    {
        /*
         * ⚠ BL-482 (M1): NO SWEEP WITHOUT THE REAL LOCK. A lock-proof child holds a PROOF lock while its parent's live
         * run holds the real one; a sweep there would decide on and drop databases under a run it does not exclude.
         */
        if (!PlatformMongoTestLock.HoldsRealLock)
        {
            _sweepReport = new SweepReport(Array.Empty<string>(), new[] { SweepSkippedWithoutRealLock });
            Console.Error.WriteLine($"[MongoIntegrationHarness] {SweepSkippedWithoutRealLock}");
            return;
        }

        SweepReport report;
        try
        {
            report = await MongoResidueSweeper.SweepAsync(client, RunId, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            // SweepAsync already returns its problems instead of throwing; this is the belt to that brace, because
            // a faulted sweep task would now be awaited by EVERY harness in the run.
            report = new SweepReport(Array.Empty<string>(), new[] { $"sweep threw: {ex.Message}" });
        }

        _sweepReport = report;

        if (report.Dropped.Count > 0)
        {
            Console.Error.WriteLine(
                "[MongoIntegrationHarness] swept abandoned test databases: "
                + string.Join(", ", report.Dropped));
        }

        foreach (var problem in report.Problems)
        {
            Console.Error.WriteLine($"[MongoIntegrationHarness] residue sweep problem (not a test failure): {problem}");
        }
    }

    /// <summary>
    /// Removes every document from this profile's collections, leaving the collections and their indexes
    /// intact. This is the blank slate — not a dropped database.
    /// </summary>
    private static async Task EmptyAsync(IMongoDatabase database)
    {
        /*
         * ⚠ EVERY COLLECTION IN THE DATABASE, NOT JUST THE PROFILE'S. Repositories built on the generic
         * convention-based base class create collections the manifest never names — task_comments is one —
         * so clearing only the manifest's list would leave rows behind, and a test that seeds a fixed _id
         * would collide with the previous run instead of starting blank.
         */
        foreach (var name in await database.ListCollectionNames().ToListAsync())
        {
            // Everything except the ownership marker — clearing that would make this database look like
            // somebody else's the moment a sweep looked at it.
            if (string.Equals(name, MongoResidueSweeper.MarkerCollection, StringComparison.Ordinal))
            {
                continue;
            }

            await database.GetCollection<BsonDocument>(name)
                .DeleteManyAsync(Builders<BsonDocument>.Filter.Empty);
        }
    }

    /*
     * ⚠ THIS IS NOW A NO-OP IN PRACTICE, AND ON PURPOSE. PlatformTestSerializers registers the production
     * serializers from a [ModuleInitializer], so they are in place before the first test case in this
     * assembly — whichever class that turns out to be. Calling it again here is idempotent and keeps the
     * registration visible to anyone reading the harness, but the harness is NO LONGER the thing that
     * establishes it. That distinction is the whole fix: while this call site was the only one, a class that
     * built its own MongoClient got the production Guid encoding only if a harness-using class had already
     * run, so tests passed alone and failed in the suite (measured 2026-08-27, 11 failures).
     */
    private static void RegisterProductionSerializers() => PlatformTestSerializers.Register();

    public async ValueTask DisposeAsync()
    {
        if (_databaseToDropOnDispose is not null)
        {
            await _client.DropDatabaseAsync(_databaseToDropOnDispose);
        }
    }
}
