using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.OutboundShipments.Commands;
using Diten.InventoryService.Application.Features.OutboundShipments.Services;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using Diten.InventoryService.Domain.Warehouse.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.OutboundShipments.Handlers.CommandHandlers;

/// <summary>
/// <c>updateOutboundShipment</c> (pack §8.18 state × command table, §12, §21; AC-73). Order: the request shape (400); the caller's key on this
/// outbound + operation (replay 200 + the current outbound / 400 another body) BEFORE If-Match (WMS draft §1 rule 5–6); the outbound (an
/// internal state, another company or tenant → 404 <c>UNKNOWN_OUTBOUND</c>); <c>ReadyToShip</c> / <c>Shipped</c> → 409
/// <c>WMS_OUTBOUND_IMMUTABLE</c>; <c>Cancelled</c> → 409 <c>WMS_INVALID_TRANSITION</c>; a stale If-Match → 409 <c>WMS_CONFLICT</c>; then one CAS
/// write with the <c>Updated</c> key event (before / after of order ref and note; the ship-to only as a "changed" mark — KVKK).
/// </summary>
public sealed class UpdateOutboundShipmentHandler : IRequestHandler<UpdateOutboundShipmentCommand, Response<OutboundShipmentViewDto>>
{
    private readonly IOutboundShipmentRepository _outbounds;
    private readonly OutboundShipmentReads _reads;
    private readonly WarehouseSupport _support;

    public UpdateOutboundShipmentHandler(IOutboundShipmentRepository outbounds, OutboundShipmentReads reads, WarehouseSupport support)
    {
        _outbounds = outbounds;
        _reads = reads;
        _support = support;
    }

    public async Task<Response<OutboundShipmentViewDto>> Handle(UpdateOutboundShipmentCommand request, CancellationToken ct)
    {
        var change = request.Change;
        if (!change.HasOrderRef && !change.HasShipTo && !change.HasNote)
            return WarehouseSupport.Invalid<OutboundShipmentViewDto>("Send at least one of orderRef, shipTo, note.");
        if (change.OrderRef is { Length: > OutboundAllocationRules.MaxOrderRefLength }) return WarehouseSupport.Invalid<OutboundShipmentViewDto>("orderRef must be at most 100 characters.");
        if (change.Note is { Length: > OutboundAllocationRules.MaxNoteLength }) return WarehouseSupport.Invalid<OutboundShipmentViewDto>("note must be at most 1000 characters.");
        if (change.HasShipTo)
        {
            if (change.ShipTo is null) return WarehouseSupport.Invalid<OutboundShipmentViewDto>("shipTo cannot be removed.");
            if (OutboundAllocationPlanner.AddressError(change.ShipTo) is { } address) return WarehouseSupport.Invalid<OutboundShipmentViewDto>(address);
        }

        var s = change.ShipTo;
        var fingerprint = WarehouseSupport.Fingerprint(("op", WarehouseContract.OpUpdateOutboundShipment), ("outboundId", request.OutboundId.ToString("D")),
            ("orderRef", change.HasOrderRef ? "1:" + WarehouseSupport.Trimmed(change.OrderRef) : null), ("note", change.HasNote ? "1:" + WarehouseSupport.Trimmed(change.Note) : null),
            ("shipTo", s is null ? null : string.Join("|", s.Name?.Trim(), s.Country?.Trim(), s.City?.Trim(), s.PostalCode?.Trim(), s.Line1?.Trim(), s.Line2?.Trim())));
        if (await ReplayAsync(request, fingerprint, ct) is { } replay) return replay;

        var outbound = await _reads.FindVisibleAsync(request.OutboundId, ct);
        if (outbound is null) return OutboundShipmentReads.UnknownOutbound<OutboundShipmentViewDto>();
        if (outbound.Status is OutboundStatus.ReadyToShip or OutboundStatus.Shipped)
        {
            return WarehouseSupport.Fail<OutboundShipmentViewDto>(WarehouseErrorCodes.OutboundImmutable,
                "A ready-to-ship outbound does not change any more.", 409, new Dictionary<string, object?> { ["status"] = outbound.Status.ToString() });
        }

        if (outbound.Status == OutboundStatus.Cancelled)
        {
            return WarehouseSupport.Fail<OutboundShipmentViewDto>(WarehouseErrorCodes.InvalidTransition, "A cancelled outbound cannot be edited.", 409);
        }

        if (outbound.Version != request.ExpectedVersion) return WarehouseSupport.Conflict<OutboundShipmentViewDto>();

        var now = _support.Now();
        var copy = outbound.Copy();
        if (change.HasOrderRef) copy.OrderRef = WarehouseSupport.Trimmed(change.OrderRef);
        if (change.HasNote) copy.Note = WarehouseSupport.Trimmed(change.Note);
        if (change.HasShipTo)
        {
            copy.ShipTo = new OutboundAddress
            {
                Name = s!.Name!.Trim(), Country = s.Country!.Trim(), City = WarehouseSupport.Trimmed(s.City), PostalCode = WarehouseSupport.Trimmed(s.PostalCode),
                Line1 = WarehouseSupport.Trimmed(s.Line1), Line2 = WarehouseSupport.Trimmed(s.Line2)
            };
        }

        copy.Version = outbound.Version + 1;
        copy.UpdatedAt = now;
        copy.UpdatedBy = _support.UserId;
        copy.UpdatedByName = _support.ActorName;
        var updated = _reads.NewEvent(copy, OutboundEventKind.Updated, now, new Dictionary<string, string?>
        {
            ["orderRefBefore"] = outbound.OrderRef, ["orderRefAfter"] = copy.OrderRef,
            ["noteBefore"] = outbound.Note, ["noteAfter"] = copy.Note,
            ["shipToChanged"] = outbound.ShipTo.SameAs(copy.ShipTo) ? "false" : "true"
        }, request.IdempotencyKey, WarehouseContract.OpUpdateOutboundShipment, fingerprint);

        WarehouseWriteOutcome outcome;
        try
        {
            outcome = await _outbounds.InTransactionAsync(async (session, tx) =>
            {
                if (!await session.ReplaceOutboundAsync(copy, outbound.Version, tx)) return WarehouseWriteOutcome.None;
                await session.InsertEventsAsync([updated], tx);
                return new WarehouseWriteOutcome(1, copy.Id);
            }, ct);
        }
        catch (OutboundDuplicateException)
        {
            outcome = WarehouseWriteOutcome.None; // a concurrent twin committed its key event
        }

        if (outcome.Affected > 0) return Response<OutboundShipmentViewDto>.Success(await _reads.ViewAsync(copy, ct));
        return await ReplayAsync(request, fingerprint, ct) ?? WarehouseSupport.Conflict<OutboundShipmentViewDto>();
    }

    private async Task<Response<OutboundShipmentViewDto>?> ReplayAsync(UpdateOutboundShipmentCommand request, string fingerprint, CancellationToken ct)
    {
        var earlier = await _outbounds.FindKeyEventAsync(request.OutboundId, _support.UserId, WarehouseContract.OpUpdateOutboundShipment, request.IdempotencyKey, ct);
        if (earlier is null) return null;
        if (earlier.RequestFingerprint != fingerprint) return WarehouseSupport.KeyReused<OutboundShipmentViewDto>();
        var current = await _reads.FindVisibleAsync(request.OutboundId, ct);
        return current is null
            ? OutboundShipmentReads.UnknownOutbound<OutboundShipmentViewDto>()
            : Response<OutboundShipmentViewDto>.Success(await _reads.ViewAsync(current, ct));
    }
}
