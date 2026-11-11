using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.PickTasks.Commands;
using Diten.InventoryService.Application.Features.PutawayTasks;
using Diten.InventoryService.Application.Features.PutawayTasks.Services;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using Diten.InventoryService.Domain.Warehouse.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.PickTasks.Handlers.CommandHandlers;

/// <summary>
/// <c>bulkAssignPickTasks</c> (WMS draft §11 — the S1 <c>bulkAssignPutawayTasks</c> shape): the key among the caller's key events of THIS operation
/// (same fingerprint → replay, other → 400), the assignee checked with AUTH, every PICK task assigned at its own version in ONE transaction with
/// its <c>Assigned</c> key event; a putaway task id is <c>NotFound</c> here. The central event is enlisted only when a row was written.
/// </summary>
public sealed class BulkAssignPickTaskHandler : IRequestHandler<BulkAssignPickTaskCommand, Response<BulkAssignResultDto>>
{
    private readonly IWarehouseTaskRepository _repository;
    private readonly PutawayTaskSupport _support;
    private readonly PutawayChecks _checks;

    public BulkAssignPickTaskHandler(IWarehouseTaskRepository repository, PutawayTaskSupport support, PutawayChecks checks)
    {
        _repository = repository;
        _support = support;
        _checks = checks;
    }

    public async Task<Response<BulkAssignResultDto>> Handle(BulkAssignPickTaskCommand request, CancellationToken ct)
    {
        if (request.Items.Count is 0 or > WarehouseContract.MaxBulkAssignItems)
            return WarehouseSupport.Invalid<BulkAssignResultDto>($"items must hold 1-{WarehouseContract.MaxBulkAssignItems} rows.");
        if (request.Items.Select(i => i.TaskId).Distinct().Count() != request.Items.Count)
            return WarehouseSupport.Invalid<BulkAssignResultDto>("items must not repeat a taskId.");

        var fingerprint = WarehouseSupport.Fingerprint(("op", WarehouseContract.OpBulkAssignPickTasks), ("assigneeUserId", request.AssigneeUserId.ToString("D")),
            ("items", WarehouseSupport.List(request.Items.Select(i => $"{i.TaskId:D}:{i.Version}").OrderBy(i => i, StringComparer.Ordinal))));
        if (await ReplayAsync(request, fingerprint, ct) is { } replay) return replay;
        if (await _checks.AssigneeAsync<BulkAssignResultDto>(request.AssigneeUserId, ct) is { } refused) return refused;

        var live = (await _repository.FindManyOfKindAsync(request.Items.Select(i => i.TaskId).ToList(), WarehouseTaskKind.Pick, ct)).ToDictionary(t => t.Id);
        var now = _support.Now();
        var rows = new Dictionary<Guid, BulkAssignRowDto>();
        WarehouseWriteOutcome outcome;
        try
        {
            outcome = await _repository.InTransactionAsync(async (session, tx) =>
            {
                rows.Clear();
                var assigned = 0;
                foreach (var item in request.Items)
                {
                    var id = item.TaskId.ToString("D");
                    if (!live.TryGetValue(item.TaskId, out var stored))
                    {
                        rows[item.TaskId] = new BulkAssignRowDto(id, WarehouseContract.RowNotFound);
                        continue;
                    }

                    var refusal = stored.Version != item.Version ? WarehouseContract.RowConflict
                        : !PutawayTaskRules.IsOpen(stored) || PutawayTaskRules.HasIntegrityError(stored) ? WarehouseContract.RowInvalidTransition
                        : PutawayTaskRules.HasPending(stored) ? WarehouseContract.RowPendingConfirmation
                        : null;
                    if (refusal is not null)
                    {
                        rows[item.TaskId] = new BulkAssignRowDto(id, refusal);
                        continue;
                    }

                    var copy = stored.Copy();
                    var before = copy.AssigneeUserId;
                    copy.AssigneeUserId = request.AssigneeUserId;
                    copy.AssigneeName = null;
                    copy.UpdatedBy = _support.UserId;
                    copy.UpdatedByName = _support.ActorName;
                    copy.UpdatedAt = now;
                    copy.Version = item.Version + 1;
                    var e = _support.Event(copy, WarehouseTaskEventKind.Assigned, now, new Dictionary<string, string?>
                    {
                        ["before.assigneeUserId"] = before?.ToString("D"), ["after.assigneeUserId"] = request.AssigneeUserId.ToString("D")
                    }, request.IdempotencyKey, WarehouseContract.OpBulkAssignPickTasks, fingerprint);
                    if (!await session.ReplaceTaskAsync(copy, item.Version, tx))
                    {
                        rows[item.TaskId] = new BulkAssignRowDto(id, WarehouseContract.RowConflict);
                        continue;
                    }

                    await session.InsertEventsAsync([e], tx);
                    rows[item.TaskId] = new BulkAssignRowDto(id, WarehouseContract.RowAssigned, copy.Version);
                    assigned++;
                }

                return assigned == 0 ? WarehouseWriteOutcome.None : new WarehouseWriteOutcome(assigned, null);
            }, ct);
        }
        catch (WarehouseTaskDuplicateException)
        {
            outcome = WarehouseWriteOutcome.None;
        }

        if (outcome.Affected == 0 && await ReplayAsync(request, fingerprint, ct) is { } twin) return twin;
        return Response<BulkAssignResultDto>.Success(new BulkAssignResultDto(
            request.Items.Select(i => rows.TryGetValue(i.TaskId, out var row) ? row : new BulkAssignRowDto(i.TaskId.ToString("D"), WarehouseContract.RowConflict)).ToList()));
    }

    private async Task<Response<BulkAssignResultDto>?> ReplayAsync(BulkAssignPickTaskCommand request, string fingerprint, CancellationToken ct)
    {
        var earlier = await _repository.FindBulkKeyEventsAsync(_support.UserId, WarehouseContract.OpBulkAssignPickTasks, request.IdempotencyKey, ct);
        if (earlier.Count == 0) return null;
        if (earlier.Any(e => e.RequestFingerprint != fingerprint)) return WarehouseSupport.KeyReused<BulkAssignResultDto>();
        var written = earlier.Select(e => e.TaskId).ToHashSet();
        var current = (await _repository.FindManyOfKindAsync(request.Items.Select(i => i.TaskId).ToList(), WarehouseTaskKind.Pick, ct)).ToDictionary(t => t.Id);
        return Response<BulkAssignResultDto>.Success(new BulkAssignResultDto(request.Items.Select(i =>
        {
            var id = i.TaskId.ToString("D");
            return !current.TryGetValue(i.TaskId, out var task) ? new BulkAssignRowDto(id, WarehouseContract.RowNotFound)
                : written.Contains(i.TaskId) ? new BulkAssignRowDto(id, WarehouseContract.RowAssigned, task.Version)
                : new BulkAssignRowDto(id, WarehouseContract.RowConflict);
        }).ToList()));
    }
}
