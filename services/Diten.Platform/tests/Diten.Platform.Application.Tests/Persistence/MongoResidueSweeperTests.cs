using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

/*
 * THE SIX RULES OF A DELETE PATH THAT RUNS UNATTENDED.
 *
 * MongoResidueSweeper drops MongoDB databases on a developer's machine, at the start of a test run, with
 * nobody watching. Every rule that stops it from dropping the wrong thing is pinned here, and each of these
 * tests is a mutation: delete the corresponding condition in Decide() and exactly one of them goes red.
 *
 * The decision is a pure function precisely so these can be proved WITHOUT a live server — a safety rule
 * that can only be checked when mongod is healthy is no use in a codebase whose recurring failure is mongod
 * dying.
 */
public class MongoResidueSweeperTests
{
    private static readonly Guid CurrentRun = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid EarlierRun = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime Now = new(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);

    private static HarnessMarker Stale(Guid? runId = null) =>
        new(nameof(MongoIntegrationHarness), runId ?? EarlierRun, Now - MongoResidueSweeper.ResidueMaxAge - TimeSpan.FromMinutes(1));

    private static SweepDecision Decide(string name, HarnessMarker? marker)
        => MongoResidueSweeper.Decide(name, marker, CurrentRun, Now);

    // ── 1. THE ONE CASE THAT IS SUPPOSED TO BE DELETED ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("diten_platform_itest")]
    [InlineData("diten_platform_itest_task_comment_order")]
    [InlineData("diten_platform_itest_b9a87557f0a74d91b33cf0a07b8a91a1")]  // the old Guid-named scheme
    public void OwnedPrefixWithAStaleMarkerFromAnotherRunIsSwept(string name)
    {
        // The third case is real residue measured on this machine on 2026-08-26 — a database left by a run
        // that died before it could clean up, under a naming scheme the harness abandoned two stages ago.
        Assert.True(Decide(name, Stale()).Drop);
    }

    // ── 2. A NAME OUTSIDE THE PREFIX IS NEVER TOUCHED ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("diten_personalization_dev")]
    [InlineData("diten_mdm_dev")]
    [InlineData("diten_auth")]
    [InlineData("DitenEnterpriseDb")]
    [InlineData("workflow_gate_repository_tests")]
    public void ADatabaseOutsideTheOwnedPrefixIsNeverSwept(string name)
    {
        /*
         * ⚠ NOTE WHAT IS BEING PROVEN: not merely that these are kept, but that they are kept EVEN WITH A
         * PERFECTLY VALID, STALE, FOREIGN-RUN MARKER attached. The name check has to hold on its own. If the
         * marker alone could authorise a drop, anything that ever ran this harness code — a copied helper in
         * another service, say — could licence the deletion of a development database.
         */
        var decision = Decide(name, Stale());
        Assert.False(decision.Drop);
        Assert.Contains("owned prefix grammar", decision.Reason);
    }

    // ── 3. PREFIX-SHAPED BUT NOT ACTUALLY OURS ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("diten_platform_itestX")]          // prefix is not a whole segment
    [InlineData("diten_platform_itest2")]          // ditto
    [InlineData("x_diten_platform_itest")]         // not at the start
    [InlineData("diten_platform_itest_Task")]      // uppercase is not this grammar
    [InlineData("diten_platform_itest-scope")]     // hyphen is not this grammar
    public void ANameThatMerelyResemblesThePrefixIsNeverSwept(string name)
    {
        // Near-misses are where a prefix check quietly becomes a substring check. `diten_platform_itestX` is
        // a different database from `diten_platform_itest`, and somebody's, and not ours.
        Assert.False(Decide(name, Stale()).Drop);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("SomeOtherHarness")]
    public void AnOwnedNameWithoutOurMarkerIsNeverSwept(string? harness)
    {
        /*
         * ⚠ THE LOAD-BEARING RULE. The marker is what makes the name check non-load-bearing: a database this
         * harness did not create cannot be deleted by it, whatever it is called. That is what protects a
         * colleague who happens to name a scratch database the same thing, and it is why the sweeper reads
         * the marker before it drops anything.
         */
        var marker = harness is null ? null : new HarnessMarker(harness, EarlierRun, Stale().TouchedAtUtc);
        var decision = Decide("diten_platform_itest_scratch", marker);

        Assert.False(decision.Drop);
        Assert.Contains(harness is null ? "no harness marker" : "not us", decision.Reason);
    }

    [Fact]
    public void AMarkerWithNoRunIdIsNeverSwept()
    {
        // A malformed marker is an untrusted marker, and an untrusted marker means keep.
        var decision = Decide(
            "diten_platform_itest_scratch",
            new HarnessMarker(nameof(MongoIntegrationHarness), Guid.Empty, Stale().TouchedAtUtc));

        Assert.False(decision.Drop);
        Assert.Contains("no run id", decision.Reason);
    }

    // ── 4. AN ACTIVE DATABASE IS NEVER TOUCHED ─────────────────────────────────────────────────────────────

    [Fact]
    public void ADatabaseThisRunOwnsIsNeverSwept()
    {
        var decision = Decide(
            "diten_platform_itest",
            new HarnessMarker(nameof(MongoIntegrationHarness), CurrentRun, Now.AddDays(-30)));

        // Age is irrelevant when the run id is ours: the shared database is long-lived BY DESIGN.
        Assert.False(decision.Drop);
        Assert.Contains("this run owns it", decision.Reason);
    }

    [Fact]
    public void ADatabaseAnotherRunTouchedRecentlyIsNeverSwept()
    {
        /*
         * ⚠ THE CONCURRENCY RULE, AND THE MOST DAMAGING ONE TO GET WRONG. Two suites can run on one machine
         * at once. The harness re-stamps the marker every time it OPENS a database, so a live run's
         * databases are always inside the window however long the run takes; without this condition, one
         * suite would delete the other's database mid-test and the victim's failures would be unreproducible.
         */
        var decision = Decide(
            "diten_platform_itest_notification_dispatch",
            new HarnessMarker(nameof(MongoIntegrationHarness), EarlierRun, Now - TimeSpan.FromMinutes(1)));

        Assert.False(decision.Drop);
        Assert.Contains("still active", decision.Reason);
    }

    // ── 5. THE PRODUCTION AND DEFAULT NAMES, EXPLICITLY ────────────────────────────────────────────────────

    [Theory]
    [InlineData("admin")]
    [InlineData("config")]
    [InlineData("local")]
    [InlineData("diten_personalization_dev")]
    [InlineData("diten_background_jobs_dev")]
    [InlineData("diten_deven_dev")]
    [InlineData("ERP_DB")]
    [InlineData("DitenERP_Dev")]
    public void TheServersOwnDatabasesAndTheDevelopmentDatabasesAreNeverSwept(string name)
    {
        // These are the databases actually present on this machine on 2026-08-26, named here rather than
        // described, because "the sweeper would never touch production" is a claim and this is a check.
        Assert.False(Decide(name, Stale()).Drop);
        Assert.False(Decide(name, null).Drop);
    }

    // ── 6. CLEANUP TROUBLE IS REPORTED, NOT THROWN, AND NOT SWALLOWED ──────────────────────────────────────

    [Fact]
    public async Task AFailingSweepIsReportedAndDoesNotBecomeATestFailure()
    {
        /*
         * ⚠ WHY THIS MATTERS MORE THAN IT LOOKS. Housekeeping runs at the start of a run, before any
         * assertion. If it threw, the first test to build a harness would go red for a reason that has
         * nothing to do with it — and the next person would read a genuine defect as "the cleanup is flaky"
         * and move on. So the sweep must fail QUIETLY into the test and LOUDLY into the report.
         *
         * The client here cannot connect at all, which is the broadest failure available.
         */
        var settings = MongoClientSettings.FromConnectionString("mongodb://127.0.0.1:1");
        settings.ServerSelectionTimeout = TimeSpan.FromMilliseconds(250);
        settings.ConnectTimeout = TimeSpan.FromMilliseconds(250);

        var report = await MongoResidueSweeper.SweepAsync(new MongoClient(settings), CurrentRun, Now);

        Assert.Empty(report.Dropped);
        Assert.NotEmpty(report.Problems);          // reported...
        Assert.Contains("could not list databases", report.Problems[0]);
        // ...and the absence of an exception above is the other half of the rule.
    }

    [Fact]
    public void TheOwnedPrefixIsAConstantThatNoCallerCanWiden()
    {
        /*
         * A sweeper that took "which prefix should I delete?" from its caller would be one typo away from
         * dropping the development database, and the typo would live in a test file. So the prefix is a
         * const on the sweeper and SweepAsync has no parameter that could replace it.
         */
        Assert.Equal("diten_platform_itest", MongoResidueSweeper.OwnedPrefix);

        var parameters = typeof(MongoResidueSweeper)
            .GetMethod(nameof(MongoResidueSweeper.SweepAsync))!
            .GetParameters()
            .Select(p => p.ParameterType);

        Assert.DoesNotContain(typeof(string), parameters);
    }
}
