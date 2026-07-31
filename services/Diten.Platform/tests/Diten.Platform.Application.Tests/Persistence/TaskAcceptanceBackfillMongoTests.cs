using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Infrastructure.Persistence.Configurations;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

/// <summary>
/// BL-042 — the backfill, against a REAL MongoDB.
///
/// <para><b>What this protects.</b> Acceptance stopped being inferred from the lifecycle and became a stored
/// field. Without the backfill, every task anybody has ever accepted reverts to <c>pendingAcceptance</c> on the
/// first deploy and the whole tenant's "My Work" empties back into the Inbox — a worse outage than the defect
/// being fixed. The code change and the migration are ONE change, and this test is what makes that true rather
/// than remembered.</para>
///
/// <para><b>Why real Mongo.</b> The migration is a query — a typed filter over <c>Nin</c> and a null check. A fake
/// repository would run C# predicates that the driver may translate differently or refuse outright; this repo has
/// already shipped one query that every unit test accepted and the server rejected (BL-030).</para>
/// </summary>
public sealed class TaskAcceptanceBackfillMongoTests : IAsyncLifetime
{
    private static readonly Guid Assignee = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid Somebody = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    private MongoIntegrationHarness _harness = null!;

    public async Task InitializeAsync() => _harness = await MongoIntegrationHarness.CreateAsync();

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    private IMongoCollection<TaskItem> Tasks => _harness.Database.GetCollection<TaskItem>("task_items");

    [Theory]
    [InlineData(TaskLifecycle.InProgress)]
    [InlineData(TaskLifecycle.Done)]
    [InlineData(TaskLifecycle.Cancelled)]
    public async Task A_task_accepted_under_the_OLD_rule_does_not_fall_back_into_the_Inbox(TaskLifecycle lifecycle)
    {
        /*
         * THE regression. The old rule was "Person + assignee + lifecycle not (Open, Planned) = accepted", so
         * each of these WAS accepted before this change. If the backfill misses them, the resolver now says
         * pendingAcceptance and the user finds their work back in the Inbox.
         */
        var task = await SeedAsync(lifecycle, TaskAssignmentTarget.Person, Assignee);

        await TaskAcceptanceBackfillMigration.MigrateAsync(_harness.Database);

        var stamped = await ReadAsync(task.Id);
        Assert.Equal(Assignee, stamped.AcceptedByUserId);
        Assert.Equal("admitted", Resolve(stamped).AdmissionState);
    }

    [Theory]
    [InlineData(TaskLifecycle.Open)]
    [InlineData(TaskLifecycle.Planned)]
    public async Task A_task_that_was_NOT_accepted_is_left_alone(TaskLifecycle lifecycle)
    {
        /*
         * The other half, and the one that would hide a too-wide filter: Open and Planned are exactly the two
         * lifecycles the old rule called NOT accepted. Stamping them would mark unaccepted work as accepted and
         * quietly empty the Inbox in the opposite direction — the same size of mistake, harder to notice.
         *
         * Planned is the case BL-042 is actually about: it must still be pendingAcceptance after the backfill, so
         * that accepting it does something.
         */
        var task = await SeedAsync(lifecycle, TaskAssignmentTarget.Person, Assignee);

        await TaskAcceptanceBackfillMigration.MigrateAsync(_harness.Database);

        var untouched = await ReadAsync(task.Id);
        Assert.Null(untouched.AcceptedByUserId);
        Assert.Equal("pendingAcceptance", Resolve(untouched).AdmissionState);
    }

    [Fact]
    public async Task Pooled_and_unassigned_work_is_never_stamped()
    {
        // Acceptance is a person-assignment concept; a queue item is claimed, not accepted. Stamping one would
        // invent an acceptance nobody made.
        var pooled = await SeedAsync(TaskLifecycle.InProgress, TaskAssignmentTarget.PositionPool, assignee: null);
        var personNoAssignee = await SeedAsync(TaskLifecycle.InProgress, TaskAssignmentTarget.Person, assignee: null);

        await TaskAcceptanceBackfillMigration.MigrateAsync(_harness.Database);

        Assert.Null((await ReadAsync(pooled.Id)).AcceptedByUserId);
        Assert.Null((await ReadAsync(personNoAssignee.Id)).AcceptedByUserId);
    }

    [Fact]
    public async Task Running_it_twice_changes_nothing_and_never_overwrites_a_real_acceptance()
    {
        /*
         * It runs on EVERY startup, so a second pass must be a no-op — and in particular must not rewrite a task
         * accepted after the deploy by somebody other than the assignee's own row.
         */
        var reassignedAfterAccept = await SeedAsync(TaskLifecycle.InProgress, TaskAssignmentTarget.Person, Assignee);
        await Tasks.UpdateOneAsync(
            Builders<TaskItem>.Filter.Eq(x => x.Id, reassignedAfterAccept.Id),
            Builders<TaskItem>.Update.Set(x => x.AcceptedByUserId, Somebody));

        await TaskAcceptanceBackfillMigration.MigrateAsync(_harness.Database);
        await TaskAcceptanceBackfillMigration.MigrateAsync(_harness.Database);

        Assert.Equal(Somebody, (await ReadAsync(reassignedAfterAccept.Id)).AcceptedByUserId);
    }

    [Fact]
    public async Task The_backfill_reproduces_the_OLD_rule_exactly_across_every_lifecycle()
    {
        /*
         * Non-vacuity for the whole file, and the claim the migration's doc comment makes: "every task the old
         * code called accepted, the new code still calls accepted". Asserted over the entire enum rather than the
         * handful sampled above, so a lifecycle added later cannot slip onto the wrong side unnoticed.
         */
        var seeded = new Dictionary<TaskLifecycle, Guid>();
        foreach (var lifecycle in Enum.GetValues<TaskLifecycle>())
        {
            seeded[lifecycle] = (await SeedAsync(lifecycle, TaskAssignmentTarget.Person, Assignee)).Id;
        }

        await TaskAcceptanceBackfillMigration.MigrateAsync(_harness.Database);

        foreach (var (lifecycle, id) in seeded)
        {
            // The old IsAccepted, written out here so the comparison is against the RULE, not against the code.
            var wasAcceptedUnderOldRule = lifecycle is not (TaskLifecycle.Open or TaskLifecycle.Planned);
            var isAcceptedNow = (await ReadAsync(id)).AcceptedByUserId is not null;

            Assert.True(
                wasAcceptedUnderOldRule == isAcceptedNow,
                $"{lifecycle}: old rule said accepted={wasAcceptedUnderOldRule}, backfill produced {isAcceptedNow}.");
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static TaskAssignmentProjection Resolve(TaskItem task) => new TaskAssignmentResolver().Resolve(task);

    private async Task<TaskItem> ReadAsync(Guid id) =>
        await Tasks.Find(Builders<TaskItem>.Filter.Eq(x => x.Id, id)).SingleAsync();

    private async Task<TaskItem> SeedAsync(TaskLifecycle lifecycle, TaskAssignmentTarget target, Guid? assignee)
    {
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            TenantId = _harness.TenantId,
            Title = $"{target}/{lifecycle}",
            Lifecycle = lifecycle,
            AssignmentTarget = target,
            AssigneeUserId = assignee,
            PoolPositionId = target == TaskAssignmentTarget.PositionPool ? Guid.NewGuid() : null,
            OrganizationUnitId = Guid.NewGuid(),
            Version = 1
        };

        await Tasks.InsertOneAsync(task);
        return task;
    }
}
