using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Application.Features.OutboundShipments;

// MOD-0178 S2 outbound — request inputs (parsed by the API reader, decided by the handlers) and the wire bodies (WMS draft §11; rule 4: an
// empty optional member is NOT written — the service's JSON options drop nulls). No body carries a stock total (pack §2 boundary rule 1).

public sealed record OutboundAddressInput(string? Name, string? Country, string? City, string? PostalCode, string? Line1, string? Line2);

public sealed record OutboundLotOverrideInput(string? LotId, string? FefoBypassReason);

/// <summary>One requested line: <c>item { itemId, skuId?, skuLevel }</c> — <c>skuId</c> optional only at <c>Item</c> (= itemId, pack §8.24).</summary>
public sealed record OutboundLineInput(
    string? OrderLineId,
    Guid ItemId,
    Guid? SkuId,
    SkuLevel SkuLevel,
    string? UomId,
    string? Quantity,
    OutboundLotOverrideInput? LotOverride);

/// <summary><c>createOutboundShipment</c> / <c>previewOutboundAllocation</c> body (the preview has no key and may omit <c>shipTo</c>).</summary>
public sealed record CreateOutboundShipmentInput(
    Guid WarehouseId,
    Guid? StagingLocationId,
    string? OrderRef,
    OutboundAddressInput? ShipTo,
    string? Note,
    IReadOnlyList<OutboundLineInput> Lines);

/// <summary><c>updateOutboundShipment</c> — header only (order ref, ship-to, note); the allocation never changes. Has* = the member was sent.</summary>
public sealed record UpdateOutboundShipmentInput(bool HasOrderRef, string? OrderRef, bool HasShipTo, OutboundAddressInput? ShipTo, bool HasNote, string? Note);

// ── wire ─────────────────────────────────────────────────────────────────────────────────────────────────

public sealed record OutboundItemDto(string ItemId, string SkuId, string SkuLevel);

public sealed record OutboundAddressDto(string Name, string Country, string? City, string? PostalCode, string? Line1, string? Line2);

public sealed record OutboundPersonDto(string UserId, string? Name);

public sealed record OutboundPlannedBinDto(string LocationId, string? LocationCode, string Quantity);

public sealed record OutboundAllocationDto(
    int AllocSeq,
    string? LotId,
    string? LotNumber,
    string Quantity,
    int? FefoRank,
    string? FefoBypassReason,
    IReadOnlyList<OutboundPlannedBinDto> PlannedBins,
    string? ReservationId,
    string ReservationReleaseState,
    string? PickedQuantity = null,
    string? PackedQuantity = null);

public sealed record OutboundLotOverrideDto(string LotId, string? FefoBypassReason);

public sealed record OutboundLineDto(
    int LineSeq,
    string OrderLineId,
    OutboundItemDto Item,
    string Quantity,
    string UomId,
    OutboundLotOverrideDto? LotOverride,
    IReadOnlyList<OutboundAllocationDto> Allocations);

/// <summary>A pick task of the outbound as the view shows it (no ship-to — Q-10a).</summary>
public sealed record OutboundPickTaskDto(
    string TaskId,
    string Number,
    string Status,
    int LineSeq,
    int AllocSeq,
    string FromLocationId,
    string? FromLocationCode,
    string ToLocationId,
    string? LotId,
    string? LotNumber,
    string PlannedQuantity,
    string ConfirmedQuantity,
    string? AssigneeUserId,
    int Version);

/// <summary><c>getOutboundShipmentView</c> / create / update answer (WMS draft §11): the outbound with allocations and pick tasks.</summary>
public sealed record OutboundShipmentViewDto(
    string OutboundId,
    string OutboundNumber,
    string LegalEntityId,
    string WarehouseId,
    string? WarehouseCode,
    string StagingLocationId,
    string? StagingLocationCode,
    string? OrderRef,
    OutboundAddressDto ShipTo,
    string? Note,
    string Status,
    bool ItemLevel,
    IReadOnlyList<OutboundLineDto> Lines,
    IReadOnlyList<OutboundPickTaskDto> PickTasks,
    string? ReadyAt,
    string CreatedAtUtc,
    OutboundPersonDto CreatedBy,
    int Version,
    string ContractVersion,
    OutboundShipmentLinkDto? Shipment = null,
    IReadOnlyList<PackageDto>? Packages = null,
    string? CancelReason = null,
    IReadOnlyList<ShortDispatchUnresolvedDto>? ShortDispatchUnresolved = null);

// ── DL-5 ─────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary><c>/view</c> only (never the frozen detail — SOURCE_DRIFT, Q-M6-1): the MVP-6 shipment the dispatch sync saw.</summary>
public sealed record OutboundShipmentLinkDto(string ShipmentId, string? ShipmentNumber, string Status, string ObservedAt);

public sealed record ShortDispatchUnresolvedDto(string SkuId, string? LotId, string Expected, string Issued, string Reason);

public sealed record PackageContentDto(int AllocSeq, string Quantity);

/// <summary>A package (WMS draft §11) — no ship-to (Q-10a); <c>packageNumber</c> is the frozen <c>packageId</c>.</summary>
public sealed record PackageDto(string PackageId, string PackageNumber, string OutboundId, string Status, IReadOnlyList<PackageContentDto> Contents, string? WeightKg,
    string? DimensionsCm, int Version, string ContractVersion = WarehouseContract.ContractVersion);

public sealed record PackingOutboundDto(string OutboundId, string OutboundNumber, string Status, int Version);

public sealed record PackingAllocationDto(int AllocSeq, OutboundItemDto Item, string? LotId, string? LotNumber, string Picked, string Packed);

/// <summary><c>listPackages</c> — the Packing page's one read (r21): the outbound summary, the allocation summary, the packages; no ship-to.</summary>
public sealed record PackingViewDto(PackingOutboundDto Outbound, IReadOnlyList<PackingAllocationDto> Allocations, IReadOnlyList<PackageDto> Packages,
    string ContractVersion = WarehouseContract.ContractVersion);

/// <summary><c>createPackage</c> / <c>updatePackage</c> body (Has* = the member was sent).</summary>
public sealed record PackageInput(bool HasWeight, string? WeightKg, bool HasDimensions, string? DimensionsCm, bool HasContents, IReadOnlyList<PackageContentInput>? Contents);

public sealed record PackageContentInput(int AllocSeq, string? Quantity);

/// <summary><c>releaseOutboundShipment</c> body: the FEFO bypass reasons per allocation (§8.21).</summary>
public sealed record ReleaseReasonInput(int AllocSeq, string? Reason);

/// <summary><c>syncOutboundDispatch</c> answer: whether a dispatch was seen, the shipment's status when one was found, the outbound.</summary>
public sealed record SyncDispatchResultDto(bool DispatchSeen, string? ShipmentStatus, OutboundShipmentViewDto Outbound,
    string ContractVersion = WarehouseContract.ContractVersion);

/// <summary>One row of <c>searchOutboundShipments</c> (the screen list; not the frozen list).</summary>
public sealed record OutboundShipmentSummaryDto(
    string OutboundId,
    string OutboundNumber,
    string Status,
    string WarehouseId,
    string? WarehouseCode,
    string? OrderRef,
    string ShipToName,
    int LineCount,
    bool ItemLevel,
    string CreatedAtUtc,
    int Version);

public sealed record OutboundShipmentPageDto(IReadOnlyList<OutboundShipmentSummaryDto> Items, long Total, int Page, int PageSize, string ContractVersion);

public sealed record OutboundEventDto(
    int Seq,
    string Kind,
    OutboundPersonDto? Actor,
    bool System,
    string AtUtc,
    IReadOnlyDictionary<string, string?> Data,
    string CorrelationId);

public sealed record OutboundEventPageDto(IReadOnlyList<OutboundEventDto> Items, long Total, int Page, int PageSize, string ContractVersion);

// ── allocation preview (pack §8.17 step 7) ─────────────────────────────────────────────────────────────────

/// <summary>A FREE lot of the line's SKU in FEFO order (rank among the lots with an allocatable quantity &gt; 0; Q-01 = A).</summary>
public sealed record PreviewLotDto(string LotId, string LotNumber, string? ExpiryDate, int FefoRank, string Allocatable);

public sealed record PreviewAllocationDto(string? LotId, string? LotNumber, int? FefoRank, string Quantity, IReadOnlyList<OutboundPlannedBinDto> PlannedBins);

/// <summary>The chosen lot is not the first free lot: what the company's FEFO mode asks for it (§8.17 step 3).</summary>
public sealed record PreviewDeviationDto(string FirstEligibleLotId, string FirstEligibleLotNumber, string? FirstEligibleExpiry, string Mode);

public sealed record PreviewLineDto(
    int LineSeq,
    string OrderLineId,
    OutboundItemDto Item,
    string Requested,
    string Allocated,
    string? FailureCode,
    string? Allocatable,
    IReadOnlyList<PreviewAllocationDto> Allocations,
    IReadOnlyList<PreviewLotDto> Lots,
    PreviewDeviationDto? Deviation);

public sealed record OutboundAllocationPreviewDto(
    string WarehouseId,
    string StagingLocationId,
    string FefoOutboundMode,
    IReadOnlyList<PreviewLineDto> Lines,
    string ContractVersion);
