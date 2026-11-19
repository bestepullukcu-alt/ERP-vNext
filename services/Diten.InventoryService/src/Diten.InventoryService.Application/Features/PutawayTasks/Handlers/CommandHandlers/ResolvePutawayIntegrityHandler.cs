using System.Globalization;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Inventory.Ports;
using Diten.InventoryService.Application.Features.PutawayTasks.Commands;
using Diten.InventoryService.Application.Features.PutawayTasks.Services;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using Diten.InventoryService.Domain.Warehouse.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.PutawayTasks.Handlers.CommandHandlers;

/// <summary>
/// <c>resolvePutawayIntegrity</c> — "Uzlaştır", the only exit of an IntegrityError confirmation (pack §8.3 rows 13 / 14 / 22–24; inspection 03 F-2,
/// 04 F-1; AC-65, AC-67, AC-68, AC-69). Supervisor only (<c>warehouse.putaway-tasks.resolve</c>), a reason, the confirmation IntegrityError
/// (else 409 WMS_CONFIRMATION_NOT_RESOLVABLE), no Pending on the task (409). The ledger is read again and the holder of the key decides:
/// (a) THIS confirmation's own movement, not reversed → Posted with the LEDGER's values (quantity, target, lot, serials), the confirmed
/// quantity grows by the ledger quantity (may exceed planned), a deviation row when they differ / OverPlanned / PostedAfterCancel;
/// (b) foreign, not committed, or reversed → an unresolved Refused (RefusalSource Resolve). The task's status is never closed nor re-opened
/// here (a terminal task stays terminal); closing it is a separate Cancel / CloseShort. No upstream call, no posting.
/// </summary>
public sealed class ResolvePutawayIntegrityHandler : PutawayCommandHandler, IRequestHandler<ResolvePutawayIntegrityCommand, Response<WarehouseTaskDto>>
{
    public ResolvePutawayIntegrityHandler(IWarehouseTaskRepository repository, PutawayTaskReads reads, PutawayTaskSupport support, PutawayPostingEngine engine)
        : base(repository, reads, support, engine)
    {
    }

    public async Task<Response<WarehouseTaskDto>> Handle(ResolvePutawayIntegrityCommand request, CancellationToken ct)
    {
        var reason = WarehouseSupport.Trimmed(request.Reason);
        if (!PutawayTaskRules.IsReason(reason)) return PutawayTaskSupport.ReasonRequired<WarehouseTaskDto>("reason");
        var fingerprint = WarehouseSupport.Fingerprint(("op", WarehouseContract.OpResolvePutawayIntegrity), ("taskId", request.TaskId.ToString("D")),
            ("seq", PutawayTaskSupport.Int(request.Seq)), ("reason", reason));
        if (await ReplayAsync(request.TaskId, WarehouseContract.OpResolvePutawayIntegrity, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        var task = await Repository.FindAsync(request.TaskId, ct);
        if (task is null) return PutawayTaskSupport.TaskNotFound<WarehouseTaskDto>();
        if (task.Version != request.ExpectedVersion) return Conflict(task);
        var c = task.Confirmations.SingleOrDefault(x => x.Seq == request.Seq);
        if (c is null) return PutawayTaskSupport.TaskNotFound<WarehouseTaskDto>();
        if (c.Status != ConfirmationStatus.IntegrityError)
        {
            return PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.ConfirmationNotResolvable, "Only an integrity error is resolved.", 409, Details(task, c.Seq));
        }

        if (PutawayTaskRules.HasPending(task)) return PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.ConfirmationPending,
            "A confirmation of this task is waiting for the stock answer; resolve after it.", 409, Details(task));

        var found = await Engine.RawLookupAsync(task, c, ct);
        var holder = found.Holder;
        var own = found.State != PostingKeyState.NotCommitted && holder is not null && holder.ReversedByTransactionId is null
                  && holder.SourceModule == PutawayPostingEngine.SourceModule && holder.SourceType == PutawayPostingEngine.SourceType
                  && holder.SourceDocumentId == task.Id.ToString("D") && holder.SourceLineId == c.Seq.ToString(CultureInfo.InvariantCulture);

        var now = Support.Now();
        var copy = task.Copy();
        var target = copy.Confirmations.Single(x => x.Seq == c.Seq);
        target.HolderMovement = holder is null ? null : PutawayPostingEngine.Holder(holder);
        target.ResolvedBy = Support.UserId;
        target.ResolvedAtUtc = now;
        target.ResolveReason = reason;
        copy.UpdatedBy = Support.UserId;
        copy.UpdatedByName = Support.ActorName;
        var data = new Dictionary<string, string?>
        {
            ["seq"] = PutawayTaskSupport.Int(c.Seq), ["reason"] = reason, ["holderTransactionId"] = holder?.TransactionId.ToString("D"),
            ["holderSourceModule"] = holder?.SourceModule, ["holderSourceDocumentId"] = holder?.SourceDocumentId, ["holderSourceLineId"] = holder?.SourceLineId
        };
        var rows = new List<WarehouseTaskEvent>();

        if (own)
        {
            // (a) the ledger is right; the confirmation follows it
            var deviations = new Dictionary<string, string?>(StringComparer.Ordinal);
            if (holder!.BaseQuantity != target.Quantity) deviations["quantity"] = $"{PutawayTaskSupport.Q(target.Quantity)}→{PutawayTaskSupport.Q(holder.BaseQuantity)}";
            if (holder.ToLocationId is { } ledgerTo && ledgerTo != target.ToLocationId.ToString("D")) deviations["toLocationId"] = $"{target.ToLocationId:D}→{ledgerTo}";
            if (!string.Equals(holder.LotId, copy.LotId, StringComparison.Ordinal)) deviations["lotId"] = $"{copy.LotId}→{holder.LotId}";
            var ledgerSerials = holder.SerialIds?.ToList();
            if (!(ledgerSerials ?? []).Order(StringComparer.Ordinal).SequenceEqual((target.SerialIds ?? []).Order(StringComparer.Ordinal), StringComparer.Ordinal))
                deviations["serialIds"] = PutawayTaskSupport.Json(ledgerSerials);

            target.Status = ConfirmationStatus.Posted;
            target.TransactionId = holder.TransactionId.ToString("D");
            target.TransactionNumber = holder.TransactionNumber;
            target.Quantity = holder.BaseQuantity;
            if (Guid.TryParse(holder.ToLocationId, out var ledgerTarget)) target.ToLocationId = ledgerTarget;
            target.SerialIds = ledgerSerials;
            target.IntegrityResolution = ConfirmationIntegrityResolution.ResolvedOwnMovement;
            PutawayTaskRules.ClearLock(target);

            var wasTerminal = PutawayTaskRules.IsTerminal(copy.Status);
            copy.ConfirmedQuantity += holder.BaseQuantity;
            var superseded = PutawayTaskRules.Supersede(copy, c.Seq);
            var before = copy.Status;
            copy.Status = PutawayTaskRules.Derive(copy);
            if (PutawayTaskRules.IsTerminal(copy.Status) && !wasTerminal) copy.OpenSerialSlot = null;
            if (copy.ConfirmedQuantity > copy.PlannedQuantity) deviations["overPlanned"] = "true";
            if (copy.Status == WarehouseTaskStatus.Cancelled) deviations["postedAfterCancel"] = "true";

            data["integrityResolution"] = nameof(ConfirmationIntegrityResolution.ResolvedOwnMovement);
            data["superseded"] = superseded.Count == 0 ? null : PutawayTaskSupport.Seqs(superseded);
            rows.Add(Support.Event(copy, WarehouseTaskEventKind.IntegrityResolved, now, data, request.IdempotencyKey, WarehouseContract.OpResolvePutawayIntegrity, fingerprint));
            if (deviations.Count > 0)
            {
                deviations["seq"] = PutawayTaskSupport.Int(c.Seq);
                rows.Add(Support.Event(copy, WarehouseTaskEventKind.ConfirmationDeviation, now, deviations));
            }

            rows.AddRange(superseded.Select(s => Support.Event(copy, WarehouseTaskEventKind.ConfirmationSuperseded, now,
                new Dictionary<string, string?> { ["seq"] = PutawayTaskSupport.Int(s), ["by"] = PutawayTaskSupport.Int(c.Seq) })));
            if (before != WarehouseTaskStatus.Completed && copy.Status == WarehouseTaskStatus.Completed)
            {
                rows.Add(Support.Event(copy, WarehouseTaskEventKind.Completed, now, new Dictionary<string, string?>
                    { ["planned"] = PutawayTaskSupport.Q(copy.PlannedQuantity), ["confirmed"] = PutawayTaskSupport.Q(copy.ConfirmedQuantity) }));
            }
        }
        else
        {
            // (b) no valid movement of this confirmation — back to an unresolved Refused; the task stays as it is
            target.Status = ConfirmationStatus.Refused;
            target.RefusalSource = RefusalSource.Resolve;
            target.RefusedOnRetry = false;
            target.RefusedAtUtc = now;
            target.Resolution = null;
            target.IntegrityResolution = ConfirmationIntegrityResolution.ReturnedToRefused;
            PutawayTaskRules.ClearLock(target);
            data["integrityResolution"] = nameof(ConfirmationIntegrityResolution.ReturnedToRefused);
            rows.Add(Support.Event(copy, WarehouseTaskEventKind.IntegrityResolved, now, data, request.IdempotencyKey, WarehouseContract.OpResolvePutawayIntegrity, fingerprint));
        }

        var (written, twin) = await SaveKeyedAsync(copy, task.Version, rows, WarehouseContract.OpResolvePutawayIntegrity, request.IdempotencyKey, fingerprint, ct);
        if (written) return Response<WarehouseTaskDto>.Success(Reads.Dto(copy));
        return twin ?? Conflict(await Repository.FindAsync(task.Id, ct), c.Seq);
    }
}
