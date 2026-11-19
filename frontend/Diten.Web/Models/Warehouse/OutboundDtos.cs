using System.Text.Json;

namespace Diten.Web.Models.Warehouse;

// MOD-0178 DL-6 — the WMS-BUNDLE draft wire shapes the three S2 screens read (InventoryService OutboundShipmentModels / PutawayTaskModels, DL-4 /
// DL-5) — only the members the screens use; an absent optional member is null (the service never writes null, WMS draft §1 rule 4). The pick
// task answer is the S1 WarehouseTask (WmsTaskDto) plus its outbound; no pick / package answer carries the ship-to (Q-10a).

public sealed class WmsOutboundAddressDto
{
    public string? Name { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? Line1 { get; set; }
    public string? Line2 { get; set; }
}

public sealed class WmsPlannedBinDto
{
    public string? LocationId { get; set; }
    public string? LocationCode { get; set; }
    public string? Quantity { get; set; }
}

public sealed class WmsAllocationDto
{
    public int AllocSeq { get; set; }
    public string? LotId { get; set; }
    public string? LotNumber { get; set; }
    public string? Quantity { get; set; }
    public int? FefoRank { get; set; }
    public string? FefoBypassReason { get; set; }
    public List<WmsPlannedBinDto>? PlannedBins { get; set; }
    public string? ReservationReleaseState { get; set; }
    public string? PickedQuantity { get; set; }
    public string? PackedQuantity { get; set; }
}

public sealed class WmsLotOverrideDto
{
    public string? LotId { get; set; }
    public string? FefoBypassReason { get; set; }
}

public sealed class WmsOutboundLineDto
{
    public int LineSeq { get; set; }
    public string? OrderLineId { get; set; }
    public WmsItemDto? Item { get; set; }
    public string? Quantity { get; set; }
    public string? UomId { get; set; }
    public WmsLotOverrideDto? LotOverride { get; set; }
    public List<WmsAllocationDto>? Allocations { get; set; }
}

public sealed class WmsOutboundPickTaskDto
{
    public string? TaskId { get; set; }
    public string? Number { get; set; }
    public string? Status { get; set; }
    public int LineSeq { get; set; }
    public int AllocSeq { get; set; }
    public string? FromLocationId { get; set; }
    public string? FromLocationCode { get; set; }
    public string? LotNumber { get; set; }
    public string? PlannedQuantity { get; set; }
    public string? ConfirmedQuantity { get; set; }
}

public sealed class WmsPackageContentDto
{
    public int AllocSeq { get; set; }
    public string? Quantity { get; set; }
}

public sealed class WmsPackageDto
{
    public string? PackageId { get; set; }
    public string? PackageNumber { get; set; }
    public string? OutboundId { get; set; }
    public string? Status { get; set; }
    public List<WmsPackageContentDto>? Contents { get; set; }
    public string? WeightKg { get; set; }
    public string? DimensionsCm { get; set; }
    public int Version { get; set; }
}

public sealed class WmsShipmentLinkDto
{
    public string? ShipmentId { get; set; }
    public string? ShipmentNumber { get; set; }
    public string? Status { get; set; }
    public string? ObservedAt { get; set; }
}

public sealed class WmsShortDispatchDto
{
    public string? SkuId { get; set; }
    public string? LotId { get; set; }
    public string? Expected { get; set; }
    public string? Issued { get; set; }
    public string? Reason { get; set; }
}

/// <summary><c>getOutboundShipmentView</c> (and the create / update / cancel / release answers).</summary>
public sealed class WmsOutboundViewDto
{
    public string? OutboundId { get; set; }
    public string? OutboundNumber { get; set; }
    public string? WarehouseId { get; set; }
    public string? WarehouseCode { get; set; }
    public string? StagingLocationId { get; set; }
    public string? StagingLocationCode { get; set; }
    public string? OrderRef { get; set; }
    public WmsOutboundAddressDto? ShipTo { get; set; }
    public string? Note { get; set; }
    public string? Status { get; set; }
    public bool ItemLevel { get; set; }
    public List<WmsOutboundLineDto>? Lines { get; set; }
    public List<WmsOutboundPickTaskDto>? PickTasks { get; set; }
    public string? ReadyAt { get; set; }
    public string? CreatedAtUtc { get; set; }
    public WmsPersonDto? CreatedBy { get; set; }
    public int Version { get; set; }
    public WmsShipmentLinkDto? Shipment { get; set; }
    public List<WmsPackageDto>? Packages { get; set; }
    public string? CancelReason { get; set; }
    public List<WmsShortDispatchDto>? ShortDispatchUnresolved { get; set; }
}

public sealed class WmsOutboundSummaryDto
{
    public string? OutboundId { get; set; }
    public string? OutboundNumber { get; set; }
    public string? Status { get; set; }
    public string? WarehouseId { get; set; }
    public string? WarehouseCode { get; set; }
    public string? OrderRef { get; set; }
    public string? ShipToName { get; set; }
    public int LineCount { get; set; }
    public bool ItemLevel { get; set; }
    public string? CreatedAtUtc { get; set; }
    public int Version { get; set; }
}

public sealed class WmsOutboundPageDto
{
    public List<WmsOutboundSummaryDto>? Items { get; set; }
    public long? Total { get; set; }
}

public sealed class WmsSyncDispatchDto
{
    public bool DispatchSeen { get; set; }
    public string? ShipmentStatus { get; set; }
    public WmsOutboundViewDto? Outbound { get; set; }
}

/// <summary>The pick task's outbound (<c>outboundId</c>, <c>outboundNumber</c>, <c>status</c>) — never the ship-to.</summary>
public sealed class WmsPickOutboundDto
{
    public string? OutboundId { get; set; }
    public string? OutboundNumber { get; set; }
    public string? Status { get; set; }
}

/// <summary>A pick task: the S1 task shape (<see cref="WmsTaskDto"/>) + its outbound and allocation.</summary>
public sealed class WmsPickTaskDto
{
    public string? TaskId { get; set; }
    public string? Number { get; set; }
    public string? WarehouseCode { get; set; }
    public string? Status { get; set; }
    public string? Origin { get; set; }
    public string? WarehouseId { get; set; }
    public WmsItemDto? Item { get; set; }
    public string? LotNumber { get; set; }
    public string? FromLocationId { get; set; }
    public string? ToLocationId { get; set; }
    public string? PlannedQuantity { get; set; }
    public string? ConfirmedQuantity { get; set; }
    public string? PendingQuantity { get; set; }
    public string? RemainingQuantity { get; set; }
    public string? BaseUomId { get; set; }
    public WmsPersonDto? Assignee { get; set; }
    public string? Priority { get; set; }
    public bool SerialControlled { get; set; }
    public List<WmsConfirmationDto>? Confirmations { get; set; }
    public string? IntegrationFlag { get; set; }
    public List<string>? AllowedActions { get; set; }
    public List<WmsDisabledActionDto>? DisabledActions { get; set; }
    public string? ShortCloseReason { get; set; }
    public WmsPickOutboundDto? Outbound { get; set; }
    public int? AllocSeq { get; set; }
    public int Version { get; set; }
}

public sealed class WmsPickTaskPageDto
{
    public List<WmsPickTaskDto>? Items { get; set; }
    public long? Total { get; set; }
}

public sealed class WmsPackingOutboundDto
{
    public string? OutboundId { get; set; }
    public string? OutboundNumber { get; set; }
    public string? Status { get; set; }
    public int Version { get; set; }
}

public sealed class WmsPackingAllocationDto
{
    public int AllocSeq { get; set; }
    public WmsItemDto? Item { get; set; }
    public string? LotNumber { get; set; }
    public string? Picked { get; set; }
    public string? Packed { get; set; }
}

/// <summary><c>listPackages</c> — the Packing page's one read (r21): outbound summary, allocation summary, packages; no ship-to.</summary>
public sealed class WmsPackingViewDto
{
    public WmsPackingOutboundDto? Outbound { get; set; }
    public List<WmsPackingAllocationDto>? Allocations { get; set; }
    public List<WmsPackageDto>? Packages { get; set; }
}

// ── writes ───────────────────────────────────────────────────────────────────────────────────────────

public sealed class OutboundLotOverrideRequest
{
    public string? LotId { get; set; }
    public string? FefoBypassReason { get; set; }
}

public sealed class OutboundLineRequest
{
    public string? OrderLineId { get; set; }
    public string? ItemId { get; set; }
    public string? SkuId { get; set; }
    public string? SkuLevel { get; set; }
    public string? UomId { get; set; }
    public string? Quantity { get; set; }
    public OutboundLotOverrideRequest? LotOverride { get; set; }
}

public sealed class OutboundAddressRequest
{
    public string? Name { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? Line1 { get; set; }
    public string? Line2 { get; set; }
}

public sealed class OutboundReleaseReason
{
    public int AllocSeq { get; set; }
    public string? Reason { get; set; }
}

/// <summary>A write from the outbound screens: the members each action reads (the controller checks which it needs).</summary>
public sealed class OutboundWriteRequest
{
    public string? IdempotencyKey { get; set; }
    public string? IfMatch { get; set; }
    public string? WarehouseId { get; set; }
    public string? StagingLocationId { get; set; }
    public string? OrderRef { get; set; }
    public OutboundAddressRequest? ShipTo { get; set; }
    public string? Note { get; set; }
    public List<OutboundLineRequest>? Lines { get; set; }
    public string? Reason { get; set; }
    public List<OutboundReleaseReason>? FefoBypassReasons { get; set; }

    /// <summary>Edit: the header members the reader changed (absent = unchanged) — <c>orderRef</c>, <c>shipTo</c>, <c>note</c>.</summary>
    public JsonElement Changes { get; set; }
}

/// <summary>A write from the pick screen.</summary>
public sealed class PickWriteRequest
{
    public string? IdempotencyKey { get; set; }
    public string? IfMatch { get; set; }
    public string? Quantity { get; set; }
    public string? Reason { get; set; }
    public string? AssigneeUserId { get; set; }
    public List<WriteItem>? Items { get; set; }
}

public sealed class PackageContentRequest
{
    public int AllocSeq { get; set; }
    public string? Quantity { get; set; }
}

/// <summary>A write from the Packing page: <c>weightKg?</c>, <c>dimensionsCm?</c> (<c>LxWxH</c>), <c>contents?</c>.</summary>
public sealed class PackageWriteRequest
{
    public string? IdempotencyKey { get; set; }
    public string? IfMatch { get; set; }
    public string? WeightKg { get; set; }
    public string? DimensionsCm { get; set; }
    public List<PackageContentRequest>? Contents { get; set; }
}

/// <summary>
/// The Compact form's header (pack §9: depo, Staging, sipariş ref., alıcı adı, ülke, şehir, posta kodu, adres 1, adres 2, not — the lines are the
/// form's table, written by form.js). Display only: the proxy builds the WMS body and the service validates it.
/// </summary>
public sealed class OutboundShipmentFormViewModel
{
    public string? Id { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    public string? WarehouseId { get; set; }

    public string? StagingLocationId { get; set; }

    public string? OrderRef { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    public string? ShipToName { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    public string? ShipToCountry { get; set; }

    public string? ShipToCity { get; set; }

    public string? ShipToPostalCode { get; set; }

    public string? ShipToLine1 { get; set; }

    public string? ShipToLine2 { get; set; }

    public string? Note { get; set; }
}
