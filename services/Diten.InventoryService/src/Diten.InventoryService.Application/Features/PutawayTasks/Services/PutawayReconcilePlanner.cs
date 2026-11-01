using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using Diten.InventoryService.Domain.Warehouse.Rules;

namespace Diten.InventoryService.Application.Features.PutawayTasks.Services;

/// <summary>
/// The reconciler's unmarked pre-read (pack §8.3 "Sweeper", §8.8): which confirmations of a task are in its branch, and does the ledger give
/// it something to write? Normal branch — Pending confirmations whose lock is empty or expired; defensive branch — Refused confirmations
/// refused ON A RETRY by a pre-check or a ledger 4xx within the last 30 minutes, whatever their Resolution and the task's status. Only
/// those keys are looked up (the first confirmation's ledger 4xx and the resolve's return are never asked about — 07 F-3). Ledger reads
/// only: no upstream call, no posting. The internal command is sent only when this answers true (NotCommitted → nothing at all).
/// </summary>
public sealed class PutawayReconcilePlanner
{
    private readonly IWarehouseTaskRepository _tasks;
    private readonly PutawayPostingEngine _engine;
    private readonly PutawayTaskSupport _support;

    public PutawayReconcilePlanner(IWarehouseTaskRepository tasks, PutawayPostingEngine engine, PutawayTaskSupport support)
    {
        _tasks = tasks;
        _engine = engine;
        _support = support;
    }

    public static IEnumerable<TaskConfirmation> NormalTargets(WarehouseTask task, DateTimeOffset now)
        => task.Confirmations.Where(c => c.Status == ConfirmationStatus.Pending && !PutawayTaskRules.LockValid(c, now));

    public static IEnumerable<TaskConfirmation> DefensiveTargets(WarehouseTask task, DateTimeOffset now)
        => task.Confirmations.Where(c => c.Status == ConfirmationStatus.Refused && c.RefusedOnRetry
                                         && c.RefusalSource is RefusalSource.PreCheck or RefusalSource.Ledger4xx
                                         && c.RefusedAtUtc is { } at && at >= now - PutawayTaskRules.DefensiveWindow);

    public async Task<bool> NeedsWriteAsync(Guid taskId, bool defensive, CancellationToken ct)
    {
        var task = await _tasks.FindAsync(taskId, ct);
        if (task is null) return false;
        var now = _support.Now();
        foreach (var c in defensive ? DefensiveTargets(task, now) : NormalTargets(task, now))
        {
            var found = await _engine.LookupAsync(task, c, ct);
            if (defensive ? found?.Kind == PostingResultKind.Posted : found is not null) return true;
        }

        return false;
    }
}
