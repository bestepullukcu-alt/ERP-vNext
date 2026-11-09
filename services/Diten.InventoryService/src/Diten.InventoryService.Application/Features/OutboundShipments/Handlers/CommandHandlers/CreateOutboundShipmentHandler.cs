using System.Text.Json;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Application.Features.OutboundShipments.Commands;
using Diten.InventoryService.Application.Features.OutboundShipments.Services;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Application.Features.Warehouse.Ports;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using Diten.InventoryService.Domain.Warehouse.Rules;
using MediatR;
using Microsoft.Extensions.Options;

namespace Diten.InventoryService.Application.Features.OutboundShipments.Handlers.CommandHandlers;

/// <summary>
/// <c>createOutboundShipment</c> (pack §8.17, §8.24, §12 S2 rows; AC-26, AC-28, AC-33 (i)(iii)(iv), AC-71, AC-73, AC-75). Order: the request shape
/// (400 — a FEFO bypass reason over 1000 included, r24); the caller's create key (replay 200 / 409 while <c>Allocating</c> / 422 replay of a
/// failure / 400 another body); steps 1–4 read only (warehouse, Staging, company prefix, lines, plan) — any refusal writes nothing; then the
/// number, transaction A (the outbound <c>Allocating</c> with its whole plan + the <c>Created</c> key event + the central enlist), and the FU03
/// batch with transaction B / <c>AllocationFailed</c> / unknown outcome (<see cref="OutboundAllocationService"/>).
/// </summary>
public sealed class CreateOutboundShipmentHandler : IRequestHandler<CreateOutboundShipmentCommand, Response<OutboundShipmentViewDto>>
{
    private readonly IOutboundShipmentRepository _outbounds;
    private readonly OutboundAllocationPlanner _planner;
    private readonly OutboundAllocationService _allocation;
    private readonly OutboundShipmentReads _reads;
    private readonly ILegalEntityMasterClient _legalEntities;
    private readonly IWarehouseRecordCodeAllocator _numbers;
    private readonly WarehouseSupport _support;
    private readonly WarehouseOutboundOptions _options;

    public CreateOutboundShipmentHandler(IOutboundShipmentRepository outbounds, OutboundAllocationPlanner planner, OutboundAllocationService allocation,
        OutboundShipmentReads reads, ILegalEntityMasterClient legalEntities, IWarehouseRecordCodeAllocator numbers, WarehouseSupport support,
        IOptions<WarehouseOutboundOptions> options)
    {
        _outbounds = outbounds;
        _planner = planner;
        _allocation = allocation;
        _reads = reads;
        _legalEntities = legalEntities;
        _numbers = numbers;
        _support = support;
        _options = options.Value;
    }

    public async Task<Response<OutboundShipmentViewDto>> Handle(CreateOutboundShipmentCommand request, CancellationToken ct)
    {
        var input = request.Outbound;
        if (OutboundAllocationPlanner.ShapeError(input, requireShipTo: true) is { } shape) return WarehouseSupport.Invalid<OutboundShipmentViewDto>(shape);

        var fingerprint = Fingerprint(input);
        if (await ReplayAsync(request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        // ── step 1: warehouse, Staging, company prefix (M-2) ──
        var (staging, stagingRefusal) = await _planner.StagingAsync(input.WarehouseId, input.StagingLocationId, ct);
        if (stagingRefusal is not null) return stagingRefusal.As<OutboundShipmentViewDto>();

        LegalEntityName? company;
        try
        {
            company = await _legalEntities.GetLegalEntityNameAsync(_support.LegalEntityId, ct);
        }
        catch (LegalEntityMasterUnavailableException)
        {
            company = null;
        }

        if (company is null)
        {
            return WarehouseSupport.Fail<OutboundShipmentViewDto>(InventoryErrorCodes.LegalEntityMasterUnavailable,
                "The company's code could not be read from MDM; no outbound was opened.", 503);
        }

        if (!WarehouseRecordCodeRules.IsValidCompanyCode(company.Code))
        {
            return WarehouseSupport.Fail<OutboundShipmentViewDto>(WarehouseErrorCodes.CompanyCodeInvalid,
                "Correct the company code (2-5 capital letters A-Z) before opening an outbound.", 422, new Dictionary<string, object?> { ["companyCode"] = company.Code });
        }

        // ── steps 1 (lines) – 4: the plan (read only) ──
        var outboundId = Guid.NewGuid();
        var plan = await _planner.PlanAsync(input, outboundId, staging!, preview: false, ct);
        if (plan.Refusal is not null) return plan.Refusal.As<OutboundShipmentViewDto>();
        if (plan.Failures.Count > 0) return OutboundAllocationService.PlanFailureResponse<OutboundShipmentViewDto>(plan.Failures);

        string number;
        try
        {
            number = await _numbers.NextAsync(company.Code, WarehouseRecordCodeRules.OutboundShipment, ct);
        }
        catch (WarehouseRecordCodeUnavailableException)
        {
            return WarehouseSupport.Fail<OutboundShipmentViewDto>(WarehouseErrorCodes.RecordCodeUnavailable, "The outbound number could not be issued; nothing was opened.", 503);
        }

        // ── step 5a: transaction A — the whole plan persisted, Allocating with the recovery lock ──
        var now = _support.Now();
        var shipTo = input.ShipTo!;
        var outbound = new OutboundShipment
        {
            Id = outboundId,
            CreatedAt = now,
            Version = 1,
            OutboundNumber = number,
            CompanyCodePrefix = company.Code,
            WarehouseId = staging!.Warehouse.Id,
            WarehouseCode = staging.Warehouse.Code,
            StagingLocationId = staging.Staging.Id,
            StagingLocationCode = staging.Staging.Code,
            OrderRef = WarehouseSupport.Trimmed(input.OrderRef),
            ShipTo = new OutboundAddress
            {
                Name = shipTo.Name!.Trim(), Country = shipTo.Country!.Trim(), City = WarehouseSupport.Trimmed(shipTo.City),
                PostalCode = WarehouseSupport.Trimmed(shipTo.PostalCode), Line1 = WarehouseSupport.Trimmed(shipTo.Line1), Line2 = WarehouseSupport.Trimmed(shipTo.Line2)
            },
            Note = WarehouseSupport.Trimmed(input.Note),
            Status = OutboundStatus.Allocating,
            AllocationLeaseUntilUtc = now.AddSeconds(_options.AllocationLeaseSeconds),
            Lines = plan.Lines,
            CreatedBy = _support.UserId,
            CreatedByName = _support.ActorName,
            IdempotencyKey = request.IdempotencyKey,
            RequestFingerprint = fingerprint,
            CorrelationId = _support.CorrelationId
        };
        var created = _reads.NewEvent(outbound, OutboundEventKind.Created, now, new Dictionary<string, string?>
        {
            ["outboundNumber"] = number,
            ["warehouseId"] = outbound.WarehouseId.ToString("D"),
            ["stagingLocationId"] = outbound.StagingLocationId.ToString("D"),
            ["orderRef"] = outbound.OrderRef,
            ["note"] = outbound.Note,
            ["shipToEntered"] = "true", // KVKK: the address itself never enters the timeline (pack §21)
            ["lines"] = JsonSerializer.Serialize(outbound.Lines.Select(l => new
            {
                l.LineSeq, l.OrderLineId, itemId = l.ItemId.ToString("D"), skuId = l.SkuId.ToString("D"), skuLevel = WireNames.ToWire(l.SkuLevel),
                quantity = InventoryQuantity.ToWire(l.RequestedQuantity), uomId = l.BaseUomId, lotOverride = l.LotOverride?.LotId
            })),
            ["allocations"] = JsonSerializer.Serialize(outbound.AllAllocations().Select(a => new
            {
                a.AllocSeq, a.LotId, quantity = InventoryQuantity.ToWire(a.Quantity), a.FefoRank, a.FefoBypassReason, a.ReservationKey,
                bins = a.PlannedBins.Select(b => new { locationId = b.LocationId.ToString("D"), quantity = InventoryQuantity.ToWire(b.Quantity) })
            }))
        }, request.IdempotencyKey, WarehouseContract.OpCreateOutboundShipment, fingerprint);

        try
        {
            await _outbounds.InTransactionAsync(async (session, tx) =>
            {
                await session.InsertOutboundAsync(outbound, tx);
                await session.InsertEventsAsync([created], tx);
                return new WarehouseWriteOutcome(1, outbound.Id);
            }, ct);
        }
        catch (OutboundDuplicateException)
        {
            // a concurrent twin with the same key committed first (independent review 11 F-3): its answer, never a 409 of the index
            if (await ReplayAsync(request.IdempotencyKey, fingerprint, ct) is { } twin) return twin;
            return WarehouseSupport.Fail<OutboundShipmentViewDto>(WarehouseErrorCodes.RecordCodeUnavailable, "The outbound could not be stored; retry.", 503);
        }

        // ── steps 5b–5c ──
        return await _allocation.ReserveAndCompleteAsync(outbound, 201, ct);
    }

    /// <summary>
    /// §8.17 step 6 "Kullanıcının aynı Idempotency-Key ile yeniden gönderimi": <c>Allocating</c> → 409 (no write); <c>AllocationFailed</c> → the
    /// stored 422; any visible state → 200 + the current outbound; another body → 400.
    /// </summary>
    private async Task<Response<OutboundShipmentViewDto>?> ReplayAsync(string key, string fingerprint, CancellationToken ct)
    {
        var earlier = await _outbounds.FindByCreateKeyAsync(_support.UserId, key, ct);
        if (earlier is null) return null;
        if (earlier.RequestFingerprint != fingerprint) return WarehouseSupport.KeyReused<OutboundShipmentViewDto>();
        return earlier.Status switch
        {
            OutboundStatus.Allocating => WarehouseSupport.Fail<OutboundShipmentViewDto>(WarehouseErrorCodes.OutboundAllocationInProgress,
                "This outbound is still being allocated; try again shortly.", 409),
            OutboundStatus.AllocationFailed => OutboundAllocationService.FailureResponse<OutboundShipmentViewDto>(earlier.AllocationFailure ?? new OutboundAllocationFailure()),
            _ => Response<OutboundShipmentViewDto>.Success(await _reads.ViewAsync(earlier, ct))
        };
    }

    /// <summary>§8.1: operation + normalized body (the key and the headers do not enter).</summary>
    public static string Fingerprint(CreateOutboundShipmentInput input)
    {
        var lines = input.Lines.Select(l =>
        {
            var quantity = InventoryQuantity.TryParsePositive(l.Quantity, out var q) ? InventoryQuantity.ToWire(q) : l.Quantity;
            return string.Join("|", l.OrderLineId?.Trim(), l.ItemId.ToString("D"), l.SkuId?.ToString("D"), WireNames.ToWire(l.SkuLevel), l.UomId?.Trim(), quantity,
                l.LotOverride?.LotId?.Trim(), OutboundAllocationRules.Reason(l.LotOverride?.FefoBypassReason));
        });
        var s = input.ShipTo;
        return WarehouseSupport.Fingerprint(("op", WarehouseContract.OpCreateOutboundShipment), ("warehouseId", input.WarehouseId.ToString("D")),
            ("stagingLocationId", input.StagingLocationId?.ToString("D")), ("orderRef", WarehouseSupport.Trimmed(input.OrderRef)),
            ("shipTo", s is null ? null : string.Join("|", s.Name?.Trim(), s.Country?.Trim(), s.City?.Trim(), s.PostalCode?.Trim(), s.Line1?.Trim(), s.Line2?.Trim())),
            ("note", WarehouseSupport.Trimmed(input.Note)), ("lines", WarehouseSupport.List(lines.Select(x => x ?? string.Empty))));
    }
}
