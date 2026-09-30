using System.Net;
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
        => MongoSchemaResidueHealer.MayHeal(name, marker, CurrentRun);

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
