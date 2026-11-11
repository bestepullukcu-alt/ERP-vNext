using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Application.Features.PutawayTasks.Commands;
using Diten.InventoryService.Application.Features.PutawayTasks.Services;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Application.Features.Warehouse.Ports;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Traceability.Enums;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using Diten.InventoryService.Domain.Warehouse.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.PutawayTasks.Handlers.CommandHandlers;

/// <summary>
/// <c>createPutawayTask</c> (pack §8.2, §12, §8.16; AC-2, AC-7, AC-8, AC-16, AC-37, AC-38, AC-51, AC-52, AC-57, AC-61, AC-62). Order: the request
/// shape (400); the caller's create key (replay / 400); the source bucket — AVAILABLE only, a Receiving node (own or inherited role) of the
/// task's warehouse, stock there; the lot (Blocked / Expired) and recall; the PRODUCT-MASTER serial flag (Gsku: the product's); a serial
/// bucket whole and unmixed; the open quantity (optimistic — the ledger is the final guard); the target (§12, §8.12); the company prefix
/// (MDM <c>Code</c>, M-2); then the number from the temporary counter and ONE transaction (task + Created [+ SuggestionApplied]).
/// </summary>
public sealed class CreatePutawayTaskHandler : PutawayCommandHandler, IRequestHandler<CreatePutawayTaskCommand, Response<WarehouseTaskDto>>
{
    private readonly ILocationRepository _locations;
    private readonly LocationRoleReads _roles;
    private readonly PutawayChecks _checks;
    private readonly IWarehouseLotReader _lots;
    private readonly IWarehouseStockReader _stock;
    private readonly ILegalEntityMasterClient _legalEntities;
    private readonly IWarehouseRecordCodeAllocator _numbers;

    public CreatePutawayTaskHandler(IWarehouseTaskRepository repository, PutawayTaskReads reads, PutawayTaskSupport support, PutawayPostingEngine engine,
        ILocationRepository locations, LocationRoleReads roles, PutawayChecks checks, IWarehouseLotReader lots, IWarehouseStockReader stock,
        ILegalEntityMasterClient legalEntities, IWarehouseRecordCodeAllocator numbers)
        : base(repository, reads, support, engine)
    {
        _locations = locations;
        _roles = roles;
        _checks = checks;
        _lots = lots;
        _stock = stock;
        _legalEntities = legalEntities;
        _numbers = numbers;
    }

    public async Task<Response<WarehouseTaskDto>> Handle(CreatePutawayTaskCommand request, CancellationToken ct)
    {
        var input = request.Task;
        var source = input.Source;
        var note = WarehouseSupport.Trimmed(input.Note);
        if (!InventoryQuantity.TryParsePositive(input.PlannedQuantity, out var planned))
            return WarehouseSupport.Invalid<WarehouseTaskDto>("plannedQuantity must be a positive decimal with at most 6 fraction digits.");
        if (note is { Length: > PutawayTaskRules.MaxNoteLength }) return WarehouseSupport.Invalid<WarehouseTaskDto>("note must be at most 1000 characters.");
        if (input.TargetSource == WarehouseTargetSource.SuggestionApplied && input.ToLocationId is null)
            return WarehouseSupport.Invalid<WarehouseTaskDto>("targetSource SuggestionApplied needs toLocationId.");

        var fingerprint = WarehouseSupport.Fingerprint(("op", WarehouseContract.OpCreatePutawayTask), ("warehouseId", source.WarehouseId.ToString("D")),
            ("fromLocationId", source.FromLocationId.ToString("D")), ("itemId", source.ItemId?.ToString("D")), ("skuId", source.SkuId.ToString("D")),
            ("skuLevel", WireNames.ToWire(source.SkuLevel)), ("lotId", source.LotId), ("stockStatus", WireNames.ToWire(source.StockStatus)),
            ("plannedQuantity", PutawayTaskSupport.Q(planned)), ("toLocationId", input.ToLocationId?.ToString("D")), ("targetSource", input.TargetSource.ToString()),
            ("assigneeUserId", input.AssigneeUserId?.ToString("D")), ("priority", input.Priority.ToString()), ("note", note));
        if (await CreateReplayAsync(request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;
        if (await _checks.AssigneeAsync<WarehouseTaskDto>(input.AssigneeUserId, ct) is { } assignee) return assignee;

        // ── the source bucket (§8.4, §12) ──
        if (source.StockStatus != StockStatus.Available)
        {
            return PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.StockNotPutawayEligible,
                "Only AVAILABLE stock is put away; held stock waits for its quality decision.", 422,
                new Dictionary<string, object?> { ["stockStatus"] = WireNames.ToWire(source.StockStatus) });
        }

        var from = await _roles.ViewAsync(source.FromLocationId, ct);
        if (from is null || !from.Location.AncestorIds.Contains(source.WarehouseId))
        {
            return PutawayTaskSupport.Fail<WarehouseTaskDto>(InventoryErrorCodes.UnknownLocation, "The source location does not exist under this warehouse of the selected company.",
                422, new Dictionary<string, object?> { ["locationId"] = source.FromLocationId.ToString("D") });
        }

        if (!from.EffectiveRoles.Contains(WarehouseRole.Receiving))
        {
            return PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.SourceNotReceiving, "The source location is not a Receiving location (own or inherited role).", 422);
        }

        IReadOnlyList<WarehouseStockRow> rows;
        try
        {
            rows = await _checks.BucketRowsAsync(source.WarehouseId, source.FromLocationId, source.SkuId, source.SkuLevel, source.LotId, ct);
        }
        catch (WarehouseInventoryUnavailableException)
        {
            return PutawayTaskSupport.Fail<WarehouseTaskDto>(InventoryErrorCodes.InventoryUnavailable, "The stock could not be read; nothing was written.", 503);
        }

        var bucket = rows.FirstOrDefault(r => r.StockStatus == StockStatus.Available && (source.ItemId is null || r.ItemId == source.ItemId));
        if (bucket is null)
        {
            return PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.SourceStockNotFound, "The company holds no AVAILABLE stock of this item / lot at the source location.", 422);
        }

        // ── the lot and recall (§8.4, §8.5) ──
        string? lotNumber = null;
        if (source.LotId is { } lotId)
        {
            var lot = (await _lots.LotsAsync([lotId], ct)).GetValueOrDefault(lotId);
            lotNumber = lot?.LotNumber ?? lotId;
            if (lot?.Status == LotStatus.Blocked)
                return PutawayTaskSupport.Fail<WarehouseTaskDto>(InventoryErrorCodes.LotBlocked, "The lot is blocked.", 422, new Dictionary<string, object?> { ["lotId"] = lotId });
            if (lot?.Status == LotStatus.Expired)
                return PutawayTaskSupport.Fail<WarehouseTaskDto>(InventoryErrorCodes.LotExpired, "The lot is expired.", 422, new Dictionary<string, object?> { ["lotId"] = lotId });
        }

        if (await _checks.RecallAsync<WarehouseTaskDto>(source.LotId, lotNumber, ct) is { } recalled) return recalled;

        // ── PRODUCT-MASTER serial flag (§4.1 OD-10) and the serial bucket (§8.16) ──
        var (flagRefusal, serial) = await _checks.SerialFlagAsync<WarehouseTaskDto>(bucket.ItemId, source.SkuId, source.SkuLevel, ct);
        if (flagRefusal is not null) return flagRefusal;
        if (serial)
        {
            if (rows.Any(r => r.StockStatus != StockStatus.Available)) return PutawayChecks.MixedStatus<WarehouseTaskDto>();
            if (planned != bucket.OnHand)
            {
                return PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.SerialPartialNotSupported,
                    "A serial-controlled SKU is put away whole: the planned quantity is the whole bucket.", 422,
                    new Dictionary<string, object?> { ["onHand"] = PutawayTaskSupport.Q(bucket.OnHand) });
            }
        }

        // ── the open quantity (§8.2; optimistic, the ledger's negative-stock rule is the final guard — ASSUMPTION-0178-03) ──
        var open = (await Repository.OpenTasksFromAsync([source.FromLocationId], ct))
            .Where(t => t.WarehouseId == source.WarehouseId && t.SkuId == source.SkuId && t.SkuLevel == source.SkuLevel
                        && string.Equals(t.LotId, source.LotId, StringComparison.Ordinal) && t.StockStatus == StockStatus.Available)
            .Sum(PutawayTaskRules.OpenHold);
        var openQuantity = bucket.OnHand - open;
        if (planned > openQuantity)
        {
            return PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.QuantityExceedsOpen, "The planned quantity exceeds the open quantity of the bucket.", 422,
                new Dictionary<string, object?> { ["openQuantity"] = PutawayTaskSupport.Q(Math.Max(0m, openQuantity)) });
        }

        // ── the target (§12, §8.12) ──
        if (input.ToLocationId is { } to)
        {
            if (await _checks.TargetAsync<WarehouseTaskDto>(source.WarehouseId, source.FromLocationId, to, ct) is { } target) return target;
            if (await _checks.StorageFitAsync<WarehouseTaskDto>(source.SkuId, source.SkuLevel, to, ct) is { } fit) return fit;
        }

        // ── the company prefix (M-2) and the number (Q-07 B) ──
        LegalEntityName? company;
        try
        {
            company = await _legalEntities.GetLegalEntityNameAsync(Support.LegalEntityId, ct);
        }
        catch (LegalEntityMasterUnavailableException)
        {
            company = null;
        }

        if (company is null)
        {
            return PutawayTaskSupport.Fail<WarehouseTaskDto>(InventoryErrorCodes.LegalEntityMasterUnavailable,
                "The company's code could not be read from MDM; no task was opened.", 503);
        }

        if (!WarehouseRecordCodeRules.IsValidCompanyCode(company.Code))
        {
            return PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.CompanyCodeInvalid,
                "Correct the company code (2-5 capital letters A-Z) before opening a task.", 422, new Dictionary<string, object?> { ["companyCode"] = company.Code });
        }

        var suggestion = await SuggestAsync(source, ct);
        string number;
        try
        {
            number = await _numbers.NextAsync(company.Code, WarehouseRecordCodeRules.PutawayTask, ct);
        }
        catch (WarehouseRecordCodeUnavailableException)
        {
            return PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.RecordCodeUnavailable, "The task number could not be issued; no task was opened.", 503);
        }

        var warehouse = await _locations.FindAsync(source.WarehouseId, ct);
        var now = Support.Now();
        var task = new WarehouseTask
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            Version = 1,
            Number = number,
            CompanyCodePrefix = company.Code,
            WarehouseCode = warehouse?.Code,
            WarehouseId = source.WarehouseId,
            ItemId = bucket.ItemId,
            SkuId = source.SkuId,
            SkuLevel = source.SkuLevel,
            LotId = source.LotId,
            LotNumber = lotNumber,
            SerialControlled = serial,
            StockStatus = StockStatus.Available,
            FromLocationId = source.FromLocationId,
            ToLocationId = input.ToLocationId,
            SuggestedToLocationId = suggestion,
            TargetSource = input.TargetSource,
            PlannedQuantity = planned,
            BaseUomId = bucket.BaseUomId,
            AssigneeUserId = input.AssigneeUserId,
            Priority = input.Priority,
            Note = note,
            CreatedBy = Support.UserId,
            CreatedByName = Support.ActorName,
            IdempotencyKey = request.IdempotencyKey,
            RequestFingerprint = fingerprint,
            CorrelationId = Support.CorrelationId,
            OpenSerialSlot = serial ? true : null
        };
        var events = new List<WarehouseTaskEvent>
        {
            Support.Event(task, WarehouseTaskEventKind.Created, now, new Dictionary<string, string?>
            {
                ["number"] = number, ["fromLocationId"] = task.FromLocationId.ToString("D"), ["lotId"] = task.LotId,
                ["plannedQuantity"] = PutawayTaskSupport.Q(planned), ["toLocationId"] = task.ToLocationId?.ToString("D"),
                ["assigneeUserId"] = task.AssigneeUserId?.ToString("D"), ["priority"] = task.Priority.ToString(), ["note"] = note, ["origin"] = task.Origin.ToString()
            })
        };
        if (input.TargetSource == WarehouseTargetSource.SuggestionApplied)
        {
            events.Add(Support.Event(task, WarehouseTaskEventKind.SuggestionApplied, now, new Dictionary<string, string?>
                { ["suggestedToLocationId"] = suggestion?.ToString("D"), ["toLocationId"] = task.ToLocationId?.ToString("D") }));
        }

        try
        {
            await Repository.InTransactionAsync(async (session, tx) =>
            {
                await session.InsertTaskAsync(task, tx);
                await session.InsertEventsAsync(events, tx);
                return new WarehouseWriteOutcome(1, task.Id);
            }, ct);
        }
        catch (WarehouseTaskDuplicateException duplicate)
        {
            // whichever unique index refused it — the create key or the serial slot — the key is re-read first (independent review 11 F-3 / 12 F-2)
            if (await CreateReplayAsync(request.IdempotencyKey, fingerprint, ct) is { } twin) return twin;
            return duplicate.Index == WarehouseTaskUniqueIndex.SerialSlot
                ? PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.QuantityExceedsOpen, "The serial bucket already has an open task.", 422,
                    new Dictionary<string, object?> { ["openQuantity"] = PutawayTaskSupport.Q(0m) })
                : PutawayTaskSupport.Fail<WarehouseTaskDto>(WarehouseErrorCodes.RecordCodeUnavailable, "The task could not be stored; retry.", 503);
        }

        return Response<WarehouseTaskDto>.Success(Reads.Dto(task), 201);
    }

    private async Task<Response<WarehouseTaskDto>?> CreateReplayAsync(string key, string fingerprint, CancellationToken ct)
    {
        var earlier = await Repository.FindByCreateKeyAsync(Support.UserId, key, ct);
        if (earlier is null) return null;
        return earlier.RequestFingerprint == fingerprint
            ? Response<WarehouseTaskDto>.Success(Reads.Dto(earlier))
            : WarehouseSupport.KeyReused<WarehouseTaskDto>();
    }

    /// <summary>
    /// §8.6: the most recently stocked bin of this SKU (lot first) in the warehouse that passes every target rule and the storage-condition
    /// fit; none → no suggestion. Information only — the form is never filled with it.
    /// </summary>
    private async Task<Guid?> SuggestAsync(TaskSourceInput source, CancellationToken ct)
    {
        try
        {
            var candidates = (await _stock.LastStockedLocationsAsync(source.WarehouseId, source.SkuId, source.SkuLevel, source.LotId, ct))
                .Concat(source.LotId is null ? [] : await _stock.LastStockedLocationsAsync(source.WarehouseId, source.SkuId, source.SkuLevel, null, ct))
                .Distinct().Where(l => l != source.FromLocationId).Take(10);
            foreach (var location in candidates)
            {
                if (await _checks.TargetAsync<bool>(source.WarehouseId, source.FromLocationId, location, ct) is null
                    && await _checks.StorageFitAsync<bool>(source.SkuId, source.SkuLevel, location, ct) is null)
                {
                    return location;
                }
            }
        }
        catch (WarehouseInventoryUnavailableException)
        {
            // a suggestion is information only — no read, no suggestion
        }

        return null;
    }
}
