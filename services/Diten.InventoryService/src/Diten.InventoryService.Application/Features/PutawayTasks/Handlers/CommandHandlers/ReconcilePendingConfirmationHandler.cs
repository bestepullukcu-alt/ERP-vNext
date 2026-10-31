using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.PutawayTasks.Commands;
using Diten.InventoryService.Application.Features.PutawayTasks.Services;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.PutawayTasks.Handlers.CommandHandlers;

/// <summary>
/// The reconciler's internal command (pack §8.3 "Sweeper", flow rows 4 / 10 / 18 / 21; AC-66, AC-67 (3), AC-68 (ii), AC-70), run per task in the
/// task's own scope with the confirmation's <c>ConfirmedBy</c> as actor. Ledger key lookups ONLY — no pre-check, no posting, no upstream call:
/// normal branch, committed → Posted (<c>ConfirmationReconciled</c>, central 1); another request under the key / a reversed holder →
/// IntegrityError (central 0: the command fails); defensive branch, a late commit behind a refused retry → IntegrityError (StatusDiverged,
/// a system row; central 0). The write is a compare-and-set whose filter is "Pending and lock empty or expired": a user who took the lock in
/// between makes it write nothing.
/// </summary>
public sealed class ReconcilePendingConfirmationHandler : IRequestHandler<ReconcilePendingConfirmationCommand, Response<bool>>
{
    private readonly IWarehouseTaskRepository _tasks;
    private readonly PutawayPostingEngine _engine;
    private readonly PutawayTaskSupport _support;

    public ReconcilePendingConfirmationHandler(IWarehouseTaskRepository tasks, PutawayPostingEngine engine, PutawayTaskSupport support)
    {
        _tasks = tasks;
        _engine = engine;
        _support = support;
    }

    public async Task<Response<bool>> Handle(ReconcilePendingConfirmationCommand request, CancellationToken ct)
    {
        var task = await _tasks.FindAsync(request.TaskId, ct);
        if (task is null) return Response<bool>.Success(false);
        var posted = false;
        var integrity = false;
        var now = _support.Now();

        var targets = (request.DefensiveBranch ? PutawayReconcilePlanner.DefensiveTargets(task, now) : PutawayReconcilePlanner.NormalTargets(task, now))
            .Select(c => c.Seq).ToList();
        foreach (var seq in targets)
        {
            var c = task.Confirmations.Single(x => x.Seq == seq);
            var found = await _engine.LookupAsync(task, c, ct);
            if (found is null) continue;
            if (request.DefensiveBranch)
            {
                if (found.Kind != PostingResultKind.Posted) continue;
                var diverged = await _engine.MarkDivergedAsync(task, seq, ct, system: true);
                task = diverged.Task;
                integrity |= diverged.Kind == ResultWriteKind.IntegrityError;
                continue;
            }

            var written = await _engine.WriteResultAsync(task, seq, attemptId: null, found, ResultWriteMode.Reconcile, ct);
            task = written.Task;
            posted |= written.Kind == ResultWriteKind.Posted;
            integrity |= written.Kind == ResultWriteKind.IntegrityError;
        }

        if (posted) return Response<bool>.Success(true);
        return integrity
            ? Response<bool>.Fail(WarehouseErrorCodes.PostingIntegrityError, "The reconciler recorded an integrity error.", 409)
            : Response<bool>.Success(false);
    }
}
