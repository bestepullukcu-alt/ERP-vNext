using System.Net;
using System.Reflection;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Servers;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

/*
 * BL-482 — THE RULES OF THE HARNESS'S SECOND DELETE PATH, AND OF THE PRECONDITION THAT KEEPS RESIDUE OUT.
 *
 * MongoSchemaResidueHealer deletes rows, unattended, when a schema build fails on duplicate keys. Every rule that
 * stops it from deleting in the wrong database is pinned here as a pure function, provable without a live server —
 * the same reasoning as MongoResidueSweeperTests: a safety rule that can only be checked while mongod is healthy is
 * no use in a codebase whose recurring failure is mongod's state.
 *
 * The live proofs (the heal really removes the residue and builds the index; an unstamped database is refused and
 * keeps its rows; a failed build is retried by the next harness) are in MongoSchemaResidueHealerMongoTests.
 */
public class MongoSchemaResidueHealerTests
{
    private static readonly Guid CurrentRun = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherRun = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static HarnessMarker Ours(Guid? runId = null) =>
        new(MongoResidueSweeper.MarkerHarness, runId ?? CurrentRun, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));

    private static HealDecision MayHeal(string name, HarnessMarker? marker)
        => MongoSchemaResidueHealer.MayHeal(name, marker, CurrentRun, holdsRealLock: true);

    // ── 1. THE ONE CASE THAT IS SUPPOSED TO BE HEALED ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("diten_platform_itest")]
    [InlineData("diten_platform_itest_working_calendar_code")]
    [InlineData("diten_platform_itest_bl482_schema_heal")]
    public void An_owned_database_stamped_by_this_run_may_be_healed(string name)
    {
        Assert.True(MayHeal(name, Ours()).Allowed);
    }

    // ── 2. NOTHING OUTSIDE THE OWNED GRAMMAR, WHATEVER MARKER IT CARRIES ───────────────────────────────────────

    [Theory]
    [InlineData("diten_personalization_dev")]
    [InlineData("diten_background_jobs_dev")]
    [InlineData("diten_auth_v3")]
    [InlineData("diten_platform")]
    [InlineData("diten_platform_dev")]
    [InlineData("diten_platform_brd_itest_gsku_0123456789abcdef0123456789abcdef")] // the BRD harness's, not ours
    [InlineData("diten_platform_standalone_time_entry")]
    [InlineData("diten_platform_itestX")]
    [InlineData("x_diten_platform_itest")]
    [InlineData("diten_platform_itest_Task")]
    [InlineData("admin")]
    [InlineData("config")]
    [InlineData("local")]
    public void A_database_outside_the_owned_grammar_is_never_healed_even_with_a_perfect_marker(string name)
    {
        // ⚠ Kept even with a marker stamped by THIS run: the name check must hold on its own, so that no marker —
        // copied into a development database by a helper somewhere — can ever licence deleting rows there.
        var decision = MayHeal(name, Ours());

        Assert.False(decision.Allowed);
        Assert.Contains("owned prefix grammar", decision.Reason);
    }

    // ── 3. AN OWNED NAME IS NOT ENOUGH: THE MARKER MUST BE OURS, AND THIS RUN'S ────────────────────────────────

    [Fact]
    public void An_owned_name_without_a_marker_is_never_healed()
    {
        var decision = MayHeal("diten_platform_itest_scratch", null);

        Assert.False(decision.Allowed);
        Assert.Contains("no harness marker", decision.Reason);
    }

    [Fact]
    public void An_owned_name_with_another_harnesss_marker_is_never_healed()
    {
        var decision = MayHeal("diten_platform_itest", new HarnessMarker("BusinessReferenceDataTestHarness", CurrentRun, DateTime.UtcNow));

        Assert.False(decision.Allowed);
        Assert.Contains("not us", decision.Reason);
    }

    [Fact]
    public void A_marker_without_a_run_id_is_never_healed()
    {
        var decision = MayHeal("diten_platform_itest", Ours(Guid.Empty));

        Assert.False(decision.Allowed);
        Assert.Contains("no run id", decision.Reason);
    }

    [Fact]
    public void A_marker_stamped_by_another_run_is_never_healed()
    {
        // The harness stamps the marker immediately before it builds the schema, so "this run's marker" means "this
        // process opened the database seconds ago". Anything else is a database this run did not just open.
        var decision = MayHeal("diten_platform_itest", Ours(OtherRun));

        Assert.False(decision.Allowed);
        Assert.Contains("another run", decision.Reason);
    }

    // ── 3b. ROUND 2 (M1): NOTHING WITHOUT THE REAL MACHINE-WIDE LOCK ───────────────────────────────────────────

    [Fact]
    public void A_process_without_the_real_lock_never_heals_even_an_owned_database_it_stamped()
    {
        // A lock-proof child holds a PROOF lock while its parent's live run holds the real one. Everything else here
        // is perfect — owned name, our marker, this run — and it must still refuse.
        var decision = MongoSchemaResidueHealer.MayHeal("diten_platform_itest", Ours(), CurrentRun, holdsRealLock: false);

        Assert.False(decision.Allowed);
        Assert.Contains("does not hold the real machine-wide test lock", decision.Reason);
    }

    // ── 4. ONLY A DUPLICATE-KEY FAILURE IS RESIDUE ─────────────────────────────────────────────────────────────

    [Fact]
    public void A_duplicate_key_index_build_failure_is_recognised_through_the_manifest_wrapper()
    {
        var failure = new InvalidOperationException(
            "[PlatformSchemaManifest] failed to build indexes on 'meeting_series'",
            Command(11000, "Index build failed: E11000 duplicate key error collection: diten_platform_itest.meeting_series"));

        Assert.True(MongoSchemaResidueHealer.IsDuplicateKeyFailure(failure));
    }

    [Theory]
    [InlineData(86, "An existing index has the same name as the requested index")] // IndexKeySpecsConflict
    [InlineData(215, "database is in the process of being dropped")]              // DatabaseDropPending
    public void A_conflicting_definition_or_a_dropped_database_is_not_residue(int code, string message)
    {
        // Neither is fixed by deleting rows, so neither may trigger a delete.
        var failure = new InvalidOperationException("[PlatformSchemaManifest] failed", Command(code, message));

        Assert.False(MongoSchemaResidueHealer.IsDuplicateKeyFailure(failure));
        Assert.False(MongoSchemaResidueHealer.IsDuplicateKeyFailure(new InvalidOperationException("no Mongo cause at all")));
    }

    // ── 4b. ROUND 2 (M2): ONLY THE INDEX THE FAILURE NAMES ─────────────────────────────────────────────────────

    [Fact]
    public void The_failing_index_is_read_from_the_E11000_message_the_index_build_throws()
    {
        // The text measured on 2026-10-01 (MeetingTwoUserFlowMongoTests, the sticky run), wrapped as the manifest wraps it.
        var failure = new InvalidOperationException(
            "[PlatformSchemaManifest] failed to build indexes on 'meeting_minutes_versions'",
            Command(11000,
                "Index build failed: fafa374f: Collection diten_platform_itest.meeting_minutes_versions ( be504036 ) :: caused by :: "
                + "E11000 duplicate key error collection: diten_platform_itest.meeting_minutes_versions index: "
                + "ux_meeting_minutes_versions_tenant_meeting_version dup key: { TenantId: UUID(\"ceb0c84e-25f9-4cab-87b4-e98a2cc68559\"), "
                + "MeetingId: UUID(\"e178283d-a541-4382-9b56-59c723ca7fce\"), VersionNumber: 1, IsDeleted: false }"));

        var target = MongoSchemaResidueHealer.ParseDuplicateKeyTarget(failure);

        Assert.Equal(new DuplicateKeyTarget("meeting_minutes_versions", "ux_meeting_minutes_versions_tenant_meeting_version"), target);
        Assert.Null(MongoSchemaResidueHealer.ParseDuplicateKeyTarget(new InvalidOperationException("E11000 without a namespace")));
    }

    [Fact]
    public void Only_the_named_index_is_a_heal_candidate_never_every_unique_index_of_the_profile()
    {
        var candidates = MongoSchemaResidueHealer.IndexesNamedBy(
            SchemaProfile.Meetings, new DuplicateKeyTarget("meeting_series", "ux_meeting_series_tenant_name"));

        var only = Assert.Single(candidates);
        Assert.Equal("meeting_series", only.Collection);
        Assert.Equal("ux_meeting_series_tenant_name", only.Index.Name);

        // The profile has more unique indexes than that one — the first version would have scanned them all.
        Assert.True(PlatformSchemaManifest.For(SchemaProfile.Meetings).Sum(c => c.Indexes.Count(i => i.Unique)) > 1);
    }

    // ── 4c. ROUND 2 (L2): THE MANIFEST HAS NO UNIQUE INDEX THE FINDER CANNOT MODEL ─────────────────────────────

    [Fact]
    public void No_unique_index_in_the_manifest_is_sparse_or_collated()
    {
        /*
         * The finder groups on key values inside the partial filter. A SPARSE unique index would make every document
         * missing the field look like one duplicate group — and the heal would delete them all; a COLLATED one groups
         * differently from the index. Read from the manifest's own CreateIndexModel options (the same models
         * production builds), by reflection because SchemaIndex does not carry them and src/ is not changed for tests.
         */
        var declared = DeclaredIndexOptions().ToArray();

        Assert.True(declared.Count(d => d.Options?.Unique == true) > 100,
            $"the manifest scan collapsed — it saw {declared.Count(d => d.Options?.Unique == true)} unique indexes");
        Assert.Empty(MongoSchemaResidueHealer.UnmodelledUniqueIndexes(declared));
    }

    [Fact]
    public void A_unique_sparse_or_collated_index_is_reported_and_a_sparse_non_unique_one_is_not()
    {
        var offenders = MongoSchemaResidueHealer.UnmodelledUniqueIndexes(new (string, string, CreateIndexOptions?)[]
        {
            ("c", "ux_sparse", new CreateIndexOptions { Unique = true, Sparse = true }),
            ("c", "ux_collated", new CreateIndexOptions { Unique = true, Collation = new Collation("tr", strength: CollationStrength.Secondary) }),
            ("c", "ix_sparse", new CreateIndexOptions { Sparse = true }),
            ("c", "ux_plain", new CreateIndexOptions { Unique = true }),
            ("c", "ix_no_options", null)
        });

        Assert.Equal(new[] { "c.ux_sparse", "c.ux_collated" }, offenders);
    }

    // ── 4d. ROUND 2 (L6): A SCOPED DATABASE IS ALWAYS INSIDE THE OWNED GRAMMAR ─────────────────────────────────

    [Theory]
    [InlineData("eventing-outbox-idempotency")]   // the scope that was in use until round 2
    [InlineData("Upper_Case")]
    [InlineData("dotted.scope")]
    public void A_scope_outside_the_owned_grammar_is_refused_before_Mongo_is_touched(string scope)
    {
        var refused = Assert.Throws<ArgumentException>(() => MongoIntegrationHarness.IsolatedDatabaseName(scope));

        Assert.Contains("outside the owned grammar", refused.Message);
    }

    [Fact]
    public void An_underscored_scope_gives_an_owned_database_name()
    {
        var name = MongoIntegrationHarness.IsolatedDatabaseName("eventing_outbox_idempotency");

        Assert.Equal("diten_platform_itest_eventing_outbox_idempotency", name);
        Assert.True(MongoResidueSweeper.IsOwnedName(name));
    }

    // ── 5. THE PRECONDITION: NO DUPLICATE IS WRITTEN WITHOUT A UNIQUE INDEX TO REFUSE IT ───────────────────────

    [Fact]
    public void The_precondition_fails_when_the_unique_index_is_missing()
    {
        var failure = UniqueIndexPrecondition.Check(
            new[] { Index("_id_", unique: false) }, "diten_platform_itest.meeting_series", "ux_meeting_series_tenant_name");

        Assert.NotNull(failure);
        Assert.Contains("ux_meeting_series_tenant_name", failure);
        Assert.Contains("MISSING", failure);
        Assert.Contains("Nothing was written", failure);
    }

    [Fact]
    public void The_precondition_fails_when_the_index_exists_but_is_not_unique()
    {
        var failure = UniqueIndexPrecondition.Check(
            new[] { Index("_id_", unique: false), Index("ux_meeting_series_tenant_name", unique: false) },
            "diten_platform_itest.meeting_series",
            "ux_meeting_series_tenant_name");

        Assert.NotNull(failure);
        Assert.Contains("NOT unique", failure);
    }

    [Fact]
    public void The_precondition_passes_when_the_index_exists_and_is_unique()
    {
        Assert.Null(UniqueIndexPrecondition.Check(
            new[] { Index("_id_", unique: false), Index("ux_meeting_series_tenant_name", unique: true) },
            "diten_platform_itest.meeting_series",
            "ux_meeting_series_tenant_name"));
    }

    // ── 6. THE SWEEP GATE: NOBODY TOUCHES A DATABASE UNTIL THE SWEEP HAS FINISHED ──────────────────────────────

    [Fact]
    public async Task Every_caller_of_the_run_once_gate_waits_for_the_work_to_finish()
    {
        /*
         * The measured failure: the second caller returned at once and built the shared database's schema while the
         * first caller's sweep was still dropping that database. The second caller's task must not complete before
         * the work does — and the work must run exactly once.
         */
        var gate = new RunOnceGate();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runs = 0;

        async Task Work()
        {
            Interlocked.Increment(ref runs);
            await release.Task;
        }

        var first = gate.RunAsync(Work);
        var second = gate.RunAsync(Work);

        Assert.False(second.IsCompleted, "the second caller went on before the first caller's sweep had finished");

        release.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(1, runs);
    }

    private static IEnumerable<(string Collection, string Index, CreateIndexOptions? Options)> DeclaredIndexOptions()
    {
        foreach (var collection in PlatformSchemaManifest.All)
        {
            var field = collection.GetType().GetField("_models", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.True(field is not null,
                $"{collection.GetType().Name} no longer has a '_models' field — update this scan, do not delete it");

            var models = (Array)((Delegate)field!.GetValue(collection)!).DynamicInvoke()!;
            var names = collection.Indexes;
            Assert.Equal(names.Count, models.Length);

            for (var i = 0; i < models.Length; i++)
            {
                var model = models.GetValue(i)!;
                var options = (CreateIndexOptions?)model.GetType().GetProperty("Options")!.GetValue(model);
                yield return (collection.Name, names[i].Name, options);
            }
        }
    }

    private static BsonDocument Index(string name, bool unique)
    {
        var index = new BsonDocument { { "v", 2 }, { "key", new BsonDocument("_id", 1) }, { "name", name } };
        if (unique)
        {
            index.Add("unique", true);
        }

        return index;
    }

    private static MongoCommandException Command(int code, string message)
    {
        var connection = new ConnectionId(new ServerId(new ClusterId(), new DnsEndPoint("localhost", 27017)));
        return new MongoCommandException(
            connection,
            $"Command createIndexes failed: {message}.",
            new BsonDocument("createIndexes", "meeting_series"),
            new BsonDocument { { "ok", 0 }, { "errmsg", message }, { "code", code } });
    }
}
