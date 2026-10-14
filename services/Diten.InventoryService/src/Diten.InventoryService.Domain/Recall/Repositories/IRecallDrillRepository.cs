using Diten.InventoryService.Domain.Recall.Entities;
using Diten.InventoryService.Domain.Recall.Enums;

namespace Diten.InventoryService.Domain.Recall.Repositories;

public sealed record RecallDrillRunQuery(RecallDrillRunStatus? Status, DateOnly? From, DateOnly? To, int Page, int PageSize);

public sealed record RecallDrillRunPage(IReadOnlyList<RecallDrillRun> Items, long Total);

/// <summary>
/// WP-0177-R5 — the drill records (pack §4.11 / §4.12, §8.10). The READ-ONLY guarantee of KR-G is structural: this is the ONLY store a drill
/// handler may use, and its write session can write nothing but drill plans, drill runs, drill events, a trace snapshot of purpose
/// <c>Drill</c>, the recall counters and audit rows — no case, scope item, lot block, quarantine request, notification, consumption, return
/// link or movement (architecture-tested: the <c>…Recall.Drills</c> namespace never references <see cref="IRecallRepository"/> /
/// <see cref="IRecallWriteSession"/>). Every member is bound to the caller's Tenant + LegalEntity.
/// </summary>
public interface IRecallDrillRepository
{
    /// <summary>The company's recall settings (the target hours / warning days / trace limits a drill reads), null = the defaults.</summary>
    Task<RecallSettings?> FindSettingsAsync(CancellationToken cancellationToken = default);

    Task<RecallDrillPlan?> FindPlanAsync(Guid planId, CancellationToken cancellationToken = default);

    Task<RecallDrillPlan?> FindPlanByCreateKeyAsync(Guid createdBy, string idempotencyKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecallDrillPlan>> ListPlansAsync(CancellationToken cancellationToken = default);

    Task<RecallDrillRun?> FindRunAsync(Guid runId, CancellationToken cancellationToken = default);

    Task<RecallDrillRun?> FindRunByCreateKeyAsync(Guid startedBy, string idempotencyKey, CancellationToken cancellationToken = default);

    Task<RecallDrillRun?> FindRunningAsync(CancellationToken cancellationToken = default);

    Task<RecallDrillRunPage> ListRunsAsync(RecallDrillRunQuery query, CancellationToken cancellationToken = default);

    /// <summary>The company's most recently completed run (the frequency warning).</summary>
    Task<RecallDrillRun?> LastCompletedAsync(CancellationToken cancellationToken = default);

    /// <summary>The caller's earlier event on this plan / run with this idempotency key (replay).</summary>
    Task<RecallDrillEvent?> FindEventByKeyAsync(Guid subjectId, Guid actor, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>The id names a plan or run of ANOTHER company of the caller's tenant (→ 403, not 404).</summary>
    Task<bool> ExistsInAnotherCompanyAsync(Guid id, CancellationToken cancellationToken = default);

    Task<T> InTransactionAsync<T>(Func<IRecallDrillWriteSession, CancellationToken, Task<T>> work, CancellationToken cancellationToken = default);
}

/// <summary>The writes of one drill transaction — drill records, a <c>Drill</c> snapshot, the counter, audit rows. Nothing else exists here.</summary>
public interface IRecallDrillWriteSession
{
    /// <summary>Next <c>DP-NN</c> of the company.</summary>
    Task<string> NextPlanCodeAsync(CancellationToken cancellationToken = default);

    /// <summary>Next <c>RC-{yyyy}-D{nnn}</c> of the tenant (the R1 drill counter of <c>recall_counters</c>).</summary>
    Task<string> NextRunNumberAsync(int year, CancellationToken cancellationToken = default);

    Task InsertPlanAsync(RecallDrillPlan plan, CancellationToken cancellationToken = default);

    Task<bool> ReplacePlanAsync(RecallDrillPlan plan, int expectedVersion, CancellationToken cancellationToken = default);

    Task InsertRunAsync(RecallDrillRun run, CancellationToken cancellationToken = default);

    Task<bool> ReplaceRunAsync(RecallDrillRun run, int expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>A trace snapshot of purpose <c>Drill</c> only (any other purpose is refused — drills never seal a case trace).</summary>
    Task InsertDrillSnapshotAsync(RecallTraceSnapshot snapshot, CancellationToken cancellationToken = default);

    Task InsertEventAsync(RecallDrillEvent drillEvent, CancellationToken cancellationToken = default);

    Task InsertAuditAsync(RecallAuditRecord record, CancellationToken cancellationToken = default);
}

/// <summary>The drill unique index a handler tells apart (one Running run per company → 409 <c>RECALL_DRILL_ALREADY_RUNNING</c>).</summary>
public static class RecallDrillIndexes
{
    public const string Running = "ux_recall_drill_running";
}
