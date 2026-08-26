using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

/// <summary>
/// BL-042 — stamps <c>AcceptedByUserId</c> on tasks that were already accepted under the OLD rule.
///
/// <para><b>Why this is not optional.</b> Acceptance used to be inferred: a person-assigned task counted as
/// accepted once its lifecycle left Open/Planned. That inference is gone, and without this backfill every task
/// anybody has ever accepted reverts to <c>pendingAcceptance</c> the moment this ships — the whole tenant's
/// "My Work" empties back into the Inbox in one deploy. The code change and this migration are one change; either
/// alone is a defect.</para>
///
/// <para><b>The predicate is the old rule, verbatim.</b> <c>AssignmentTarget = Person</c> AND
/// <c>AssigneeUserId != null</c> AND lifecycle NOT IN (Open, Planned) — which is exactly what
/// <c>ITaskAssignmentResolver.IsAccepted</c> used to compute. Copying the old definition rather than inventing a
/// new one is what makes "behaviour is preserved" a fact instead of a hope: every task the old code called
/// accepted, the new code still calls accepted.</para>
///
/// <para><b>AcceptedByUserId = AssigneeUserId, not the actor.</b> Nobody recorded who pressed accept on this
/// historical data, and the assignee is the only person the old rule could have meant — accept is assignee-only
/// (the handler refuses anyone else). Reading the TaskAssignment history instead would be more precise for the
/// rows that have one, and silently wrong for the rows that do not.</para>
///
/// <para><b>Idempotent.</b> Only rows with no mark are touched, so a re-run is a no-op and a task accepted after
/// the deploy is never overwritten.</para>
/// </summary>
public static class TaskAcceptanceBackfillMigration
{
    public static async Task MigrateAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        var collection = database.GetCollection<TaskItem>("task_items");

        var filter = Builders<TaskItem>.Filter.And(
            Builders<TaskItem>.Filter.Eq(x => x.IsDeleted, false),
            // Not yet stamped — this is what makes a re-run a no-op.
            Builders<TaskItem>.Filter.Eq(x => x.AcceptedByUserId, null),
            Builders<TaskItem>.Filter.Eq(x => x.AssignmentTarget, TaskAssignmentTarget.Person),
            Builders<TaskItem>.Filter.Ne(x => x.AssigneeUserId, null),
            // The old IsAccepted, verbatim: anything past the pre-acceptance lifecycles counted as accepted.
            Builders<TaskItem>.Filter.Nin(x => x.Lifecycle, [TaskLifecycle.Open, TaskLifecycle.Planned]));

        // No $set-from-another-field in a plain UpdateMany, and the value differs per row, so read the ids and
        // stamp each. The collection is small enough that a pipeline update would be optimising the wrong thing.
        var pending = await collection.Find(filter).ToListAsync(ct);
        foreach (var task in pending)
        {
            await collection.UpdateOneAsync(
                Builders<TaskItem>.Filter.Eq(x => x.Id, task.Id),
                Builders<TaskItem>.Update.Set(x => x.AcceptedByUserId, task.AssigneeUserId),
                cancellationToken: ct);
        }
    }
}
