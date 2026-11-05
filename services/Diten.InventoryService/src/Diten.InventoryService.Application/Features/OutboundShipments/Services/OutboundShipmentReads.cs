using System.Globalization;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using Diten.InventoryService.Domain.Warehouse.Rules;

namespace Diten.InventoryService.Application.Features.OutboundShipments.Services;

/// <summary>
/// The wire shapes of an outbound (WMS draft §11) and the timeline-row factory of <c>wms_outbound_events</c> (a write's rows take the outbound's
/// next <c>Seq</c> values in the same compare-and-set write). An internal state (<c>Allocating</c> / <c>AllocationFailed</c>) is never shown:
/// the readers answer 404 <c>UNKNOWN_OUTBOUND</c> for it (§8.18).
/// </summary>
public sealed class OutboundShipmentReads
{
    public const string DateTimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    private readonly IOutboundShipmentRepository _outbounds;
    private readonly ILocationRepository _locations;
    private readonly Warehouse.Services.WarehouseSupport _support;

    public OutboundShipmentReads(IOutboundShipmentRepository outbounds, ILocationRepository locations, Warehouse.Services.WarehouseSupport support)
    {
        _outbounds = outbounds;
        _locations = locations;
        _support = support;
    }

    public static string Wire(DateTimeOffset value) => value.UtcDateTime.ToString(DateTimeFormat, CultureInfo.InvariantCulture);

    public static string Q(decimal value) => InventoryQuantity.ToWire(value);

    public static Response<T> UnknownOutbound<T>() => Response<T>.Fail(WarehouseErrorCodes.UnknownOutbound, "Outbound shipment not found.", 404);

    /// <summary>The visible outbound or null (another company / tenant, unknown id, or an internal state).</summary>
    public async Task<OutboundShipment?> FindVisibleAsync(Guid outboundId, CancellationToken ct)
        => await _outbounds.FindAsync(outboundId, ct) is { } outbound && OutboundAllocationRules.IsVisible(outbound.Status) ? outbound : null;

    public async Task<OutboundShipmentViewDto> ViewAsync(OutboundShipment outbound, CancellationToken ct)
    {
        var tasks = await _outbounds.PickTasksAsync(outbound.Id, ct);
        var locationIds = outbound.AllAllocations().SelectMany(a => a.PlannedBins).Select(b => b.LocationId)
            .Concat(tasks.Select(t => t.FromLocationId)).Distinct().ToList();
        var codes = locationIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _locations.FindManyAsync(locationIds, ct)).ToDictionary(l => l.Id, l => l.Code);
        return View(outbound, tasks, codes);
    }

    public static OutboundShipmentViewDto View(OutboundShipment o, IReadOnlyList<WarehouseTask> tasks, IReadOnlyDictionary<Guid, string> codes)
        => new(
            o.Id.ToString("D"),
            o.OutboundNumber,
            (o.LegalEntityId ?? Guid.Empty).ToString("D"),
            o.WarehouseId.ToString("D"),
            o.WarehouseCode,
            o.StagingLocationId.ToString("D"),
            o.StagingLocationCode,
            o.OrderRef,
            new OutboundAddressDto(o.ShipTo.Name, o.ShipTo.Country, o.ShipTo.City, o.ShipTo.PostalCode, o.ShipTo.Line1, o.ShipTo.Line2),
            o.Note,
            o.Status.ToString(),
            o.IsItemLevel(),
            o.Lines.Select(l => new OutboundLineDto(
                l.LineSeq,
                l.OrderLineId,
                new OutboundItemDto(l.ItemId.ToString("D"), l.SkuId.ToString("D"), WireNames.ToWire(l.SkuLevel)),
                Q(l.RequestedQuantity),
                l.BaseUomId,
                l.LotOverride is null ? null : new OutboundLotOverrideDto(l.LotOverride.LotId, l.LotOverride.FefoBypassReason),
                l.Allocations.Select(a => new OutboundAllocationDto(
                    a.AllocSeq, a.LotId, a.LotNumber, Q(a.Quantity), a.FefoRank, a.FefoBypassReason,
                    a.PlannedBins.Select(b => new OutboundPlannedBinDto(b.LocationId.ToString("D"), codes.GetValueOrDefault(b.LocationId), Q(b.Quantity))).ToList(),
                    a.ReservationId?.ToString("D"), a.ReservationReleaseState.ToString())).ToList())).ToList(),
            tasks.OrderBy(t => t.AllocSeq).ThenBy(t => t.Number, StringComparer.Ordinal).Select(t => new OutboundPickTaskDto(
                t.Id.ToString("D"), t.Number, t.Status.ToString(), t.LineSeq ?? 0, t.AllocSeq ?? 0, t.FromLocationId.ToString("D"),
                codes.GetValueOrDefault(t.FromLocationId), (t.ToLocationId ?? Guid.Empty).ToString("D"), t.LotId, t.LotNumber, Q(t.PlannedQuantity),
                Q(t.ConfirmedQuantity), t.AssigneeUserId?.ToString("D"), t.Version)).ToList(),
            o.ReadyAtUtc is { } ready ? Wire(ready) : null,
            Wire(o.CreatedAt),
            new OutboundPersonDto(o.CreatedBy.ToString("D"), o.CreatedByName),
            o.Version,
            WarehouseContract.ContractVersion);

    public static OutboundShipmentSummaryDto Summary(OutboundShipment o)
        => new(o.Id.ToString("D"), o.OutboundNumber, o.Status.ToString(), o.WarehouseId.ToString("D"), o.WarehouseCode, o.OrderRef, o.ShipTo.Name,
            o.Lines.Count, o.IsItemLevel(), Wire(o.CreatedAt), o.Version);

    public static OutboundEventDto Event(OutboundShipmentEvent e)
        => new(e.Seq, e.Kind.ToString(), e.ActorUserId is { } actor ? new OutboundPersonDto(actor.ToString("D"), e.ActorName) : null, e.System, Wire(e.AtUtc),
            e.Data.Where(d => d.Value is not null).ToDictionary(d => d.Key, d => d.Value, StringComparer.Ordinal), e.CorrelationId.ToString("D"));

    // ── timeline rows ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A timeline row of <paramref name="outbound"/> by the caller (its <c>Seq</c> = the outbound's next event seq).</summary>
    public OutboundShipmentEvent NewEvent(OutboundShipment outbound, OutboundEventKind kind, DateTimeOffset at, IReadOnlyDictionary<string, string?>? data = null,
        string? key = null, string? op = null, string? fingerprint = null) => new()
    {
        Id = Guid.NewGuid(),
        CreatedAt = at,
        OutboundId = outbound.Id,
        Seq = ++outbound.EventSeq,
        Kind = kind,
        ActorUserId = _support.UserId,
        ActorName = _support.ActorName,
        AtUtc = at,
        Data = data is null ? [] : new Dictionary<string, string?>(data, StringComparer.Ordinal),
        CorrelationId = _support.CorrelationId,
        IdempotencyKey = key,
        RequestOp = op,
        RequestFingerprint = fingerprint
    };
}
