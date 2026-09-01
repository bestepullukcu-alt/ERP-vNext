using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

// Guard against the two ways BL-030 bites, both of which compile, pass every fake-repository test, and are
// wrong only against a real database.
//
// No DateTimeOffsetSerializer is registered anywhere, so the Mongo driver stores every DateTimeOffset as the
// BSON array [localTicks, offsetMinutes]. Two consequences, and one test each:
//
//   1. A sort whose keys include TWO such fields is REJECTED at runtime — "cannot sort with keys that are
//      parallel arrays". Loud, but only in production. That is the original guard, below.
//
//   2. An ASCENDING sort on ONE such field is ACCEPTED and returns the wrong order, silently. MongoDB
//      compares an array by its extremum, and ascending takes the SMALLEST element — which is the offset
//      (-300..+180), never the ticks (~6.4e17). Ascending orders BY TIME ZONE. That is the second guard.
//
// ⚠ WHY THE OLD REGEX SAW NONE OF CASE 2 — measured 2026-08-28, both blind spots confirmed against the tree:
//   · it only knew the `.SortBy(...)` fluent form, so all 15 `Builders<T>.Sort.Ascending(...)` sites were
//     invisible — the regex had ZERO matches on that shape;
//   · its `(?<rest>…)+` quantifier made a `.ThenBy` link MANDATORY, so single-key ascending never matched.
//   Between them, 26 of 26 real cases went unseen.
//
// ⚠ DESCENDING IS NOT FORBIDDEN, and deliberately so — but nor is it correct. Descending compares the LARGEST
// element, which IS the ticks, so it beats ascending; the ticks are LOCAL WALL-CLOCK ticks, however, so it
// still ignores the offset. Measured on diten_personalization_dev, a descending DueAt query inverted at row
// 14. Banning descending would be wrong (it is the best the representation allows and every list depends on
// it); calling it correct would also be wrong. See DateTimeOffsetAscendingSortMongoTests, which pins both.
public sealed class DateTimeOffsetSortGuardTests
{
    /*
     * THE ALLOW-LIST. Every entry is an ascending sort on a DateTimeOffset that is CORRECT, and correct for
     * one reason only: the field is stamped exclusively from DateTimeOffset.UtcNow, so its offset element is
     * invariably 0, the array comparison degenerates to a comparison of ticks, and ascending means what it
     * says.
     *
     * ⚠ THAT IS A CLAIM ABOUT WRITES, NOT ABOUT THIS QUERY. It holds while nothing ever assigns the field a
     * value carrying a non-zero offset — a user-supplied date, a parsed client string, DateTimeOffset.Now.
     * Measured 2026-08-28: across 95 collections and 2761 sampled documents in diten_personalization_dev,
     * every non-zero offset (164 of 5930 timestamp fields) sat on a user-chosen business date — DueAt,
     * StartAt, PlannedDate, EffectiveFrom, StartsAt — and none on a machine-stamped one. If that ever stops
     * being true for a field below, its entry is wrong, not merely stale.
     *
     * Adding a line here is a claim you must be able to defend. Removing one must turn this test red.
     */
    private static readonly HashSet<string> MachineStampedAscendingSorts =
    [
        // Outbox drains and retry queues: FIFO is the REQUIREMENT, not a preference — oldest work first.
        // Flipping these to descending would process the newest message first and starve the backlog.
        "Diten.Platform.Common/src/Diten.Platform.Common/Events/Outbox/OutboxRepository.cs|OutboxMessage.CreatedAt",
        "Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/AuditOutboxRepository.cs|AuditOutboxMessage.CreatedAtUtc",

        // NextRetryAt is a SCHEDULE the server computes (UtcNow + backoff), so due-soonest-first is both
        // correct and the point; it is never a date anybody types.
        "Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/NotificationDispatchRepository.cs|NotificationDispatch.NextRetryAt",

        // History and audit trails, read oldest-first because that is the order they happened in. Each of
        // these fields defaults to UtcNow on the entity and is never set from input.
        "Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/ProductAbbreviationHistoryRepository.cs|ProductAbbreviationHistoryEntry.OccurredAtUtc",
        "Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/TaskRepositories.cs|TaskAssignment.OccurredAt",

        // Subtask listings ordered by creation. CreatedAt comes from BaseEntity's UtcNow initialiser.
        "Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/TaskRepositories.cs|TaskItem.CreatedAt",
        "Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/WorkflowRepositories.cs|ApprovalTask.CreatedAt",

        /*
         * ⚠ THE ONE ENTRY THAT IS A JUDGEMENT CALL, NOT AN INVARIANT. ApprovalTask.DueAt IS user-supplied,
         * and diten_personalization_dev holds 5 such rows at offset +180 — so this ascending sort really can
         * misorder. It stays on the server anyway, for a reason the other DueAt sorts did not have: the query
         * is `.Limit(maxItems)` over a set ALREADY filtered to overdue tasks, so the sort chooses which
         * overdue items a bounded escalation batch takes first. Ordering after the fact cannot change a
         * selection the server already made, and over-fetching to reorder would change what "batch" means.
         *
         * What is actually at stake is FAIRNESS INSIDE ONE BATCH, not correctness of the outcome: every
         * overdue task remains in the candidate set and is escalated by a later pass. The residual is a
         * bounded jitter in escalation order, recorded in BL-030 rather than hidden here.
         */
        "Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/WorkflowRepositories.cs|ApprovalTask.DueAt",
    ];

    [Fact]
    public void No_ascending_server_side_sort_on_a_date_time_offset_outside_the_allow_list()
    {
        var root = RepoPaths.Services();
        var allSources = MongoSortSourceScanner.AllSources(root);
        var productionSources = MongoSortSourceScanner.ProductionSources(root);

        Assert.NotEmpty(allSources);
        Assert.NotEmpty(productionSources);

        var propertiesByType = MongoSortSourceScanner.DateTimeOffsetPropertiesByType(allSources);

        // Sanity checks: if type resolution silently found nothing, every assertion below would pass
        // vacuously — exactly the failure mode this slice is about.
        Assert.Contains("CreatedAt", propertiesByType["BaseEntity"]);
        Assert.Contains("DueAt", propertiesByType["TaskItem"]);
        // Inherited, never redeclared on TaskItem — proves the base-class walk runs.
        Assert.Contains("CreatedAt", propertiesByType["TaskItem"]);
        // ⚠ And the negative: OutboxEvent.CreatedAt is a plain DateTime, so it is NOT a violation however
        // many other entities call a DateTimeOffset "CreatedAt". A name-only guard condemned it.
        Assert.DoesNotContain("CreatedAt", propertiesByType["OutboxEvent"]);

        var violations = MongoSortSourceScanner
            .AscendingSortSites(productionSources, root)
            .Where(site => propertiesByType.GetValueOrDefault(site.EntityType)?.Contains(site.Key) == true)
            .Select(site => new
            {
                Site = site,
                Key = $"{site.RelativePath.Replace(Path.DirectorySeparatorChar, '/')}|{site.EntityType}.{site.Key}",
            })
            .Where(x => !MachineStampedAscendingSorts.Contains(x.Key))
            .DistinctBy(x => x.Key)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            violations.Count == 0,
            "An ASCENDING MongoDB sort on a DateTimeOffset does not order by time — the value is stored as "
            + "[localTicks, offsetMinutes] and ascending compares the SMALLEST element, which is the offset. "
            + "The rows come back ordered by TIME ZONE, with no error (BL-030)."
            + Environment.NewLine
            + Environment.NewLine
            + "Order these in memory by the true instant instead — see TaskItemRepository.ByDueDate — or, if "
            + "the field is stamped only ever from DateTimeOffset.UtcNow, add it to "
            + $"{nameof(MachineStampedAscendingSorts)} WITH the reason:"
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                violations.Select(v => $"  {v.Key}  (line {v.Site.Line}, via {v.Site.Form})")));
    }

    [Fact]
    public void Allow_list_has_no_stale_entries()
    {
        // An allow-list nobody prunes is how exceptions outlive the code they excused: the next reader cannot
        // tell a live exemption from a fossil, so they trust none of them.
        var root = RepoPaths.Services();
        var live = MongoSortSourceScanner
            .AscendingSortSites(MongoSortSourceScanner.ProductionSources(root), root)
            .Select(s => $"{s.RelativePath.Replace(Path.DirectorySeparatorChar, '/')}|{s.EntityType}.{s.Key}")
            .ToHashSet(StringComparer.Ordinal);

        var stale = MachineStampedAscendingSorts.Where(entry => !live.Contains(entry)).OrderBy(x => x).ToList();

        Assert.True(
            stale.Count == 0,
            "These allow-list entries no longer match any ascending sort in the tree. The sort was fixed or "
            + "moved; delete the entry:" + Environment.NewLine + string.Join(Environment.NewLine, stale));
    }

    // The original guard, unchanged in intent: TWO DateTimeOffset keys in one server-side sort is rejected by
    // the server outright. Kept separate from the ascending check because the failure mode is different — this
    // one throws rather than lying.
    [Fact]
    public void No_repository_sorts_on_two_date_time_offset_keys()
    {
        var root = RepoPaths.Services();
        var propertiesByType = MongoSortSourceScanner.DateTimeOffsetPropertiesByType(MongoSortSourceScanner.AllSources(root));

        // Test code is exempt on purpose: WorkflowInstanceLookupMongoTests issues the forbidden two-key sort
        // deliberately, to assert the server still rejects it and thus that the in-memory ordering is required.
        var violations = MongoSortSourceScanner
            .AllSortChains(MongoSortSourceScanner.ProductionSources(root), root)
            .Select(chain => new
            {
                chain.RelativePath,
                chain.Line,
                DateKeys = chain.Keys
                    .Where(k => propertiesByType.GetValueOrDefault(chain.EntityType)?.Contains(k) == true)
                    .ToList(),
            })
            .Where(x => x.DateKeys.Count >= 2)
            .Select(x => $"{x.RelativePath}:{x.Line}: sorts on {string.Join(" + ", x.DateKeys)}")
            .ToList();

        Assert.True(
            violations.Count == 0,
            "MongoDB cannot sort on two DateTimeOffset keys while BL-030 is open — every DateTimeOffset is "
            + "stored as a BSON array and the server rejects the query with \"cannot sort with keys that are "
            + "parallel arrays\". Order these results in memory instead (see "
            + "WorkflowInstanceRepository.GetLatestByObjectRefAsync):"
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }
}
