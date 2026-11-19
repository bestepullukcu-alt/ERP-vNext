using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;

namespace Diten.InventoryService.Domain.Warehouse.Entities;

// MOD-0178 S2 packing (pack §4.5 HandlingUnit, §8.20) and the outbound outbox row (§8.9, §8.21). A package keeps NO stock: its content is the
// quantity of a picked allocation laid into it; the stock stays on the Staging node, AVAILABLE and reserved (packing writes no movement).

/// <summary>§4.5 <c>Contents[] { allocSeq, quantity }</c> — how much of one allocation of the outbound lies in the package.</summary>
public sealed class HandlingUnitContent
{
    public int AllocSeq { get; set; }

    public decimal Quantity { get; set; }

    public HandlingUnitContent Copy() => (HandlingUnitContent)MemberwiseClone();
}

/// <summary>
/// §4.5 <c>HandlingUnit</c> (<c>wms_handling_units</c>) — a package of a <c>Picked</c> outbound. <see cref="PackageNumber"/> is the frozen
/// WAREHOUSE-OUTBOUND <c>packageId</c> (a string). Weight and dimensions are typed by hand (Q-MDM-1; no packaging-level conversion,
/// FU-0178-P9). Never deleted: a cancelled outbound cancels its packages (§8.22). Every write is a compare-and-set on <c>Version</c>.
/// </summary>
public sealed class HandlingUnit : EntityBase
{
    /// <summary>RCS-001 <c>{COMPANY}-PKG-{000001}</c> from the temporary counter (Q-07 B); written once.</summary>
    public string PackageNumber { get; set; } = string.Empty;

    public Guid OutboundId { get; set; }

    public List<HandlingUnitContent> Contents { get; set; } = [];

    /// <summary>Kilograms, &gt; 0, at most 3 fraction digits (§12); optional.</summary>
    public decimal? WeightKg { get; set; }

    /// <summary><c>^\d{1,4}x\d{1,4}x\d{1,4}$</c> (§12); optional.</summary>
    public string? DimensionsCm { get; set; }

    public HandlingUnitStatus Status { get; set; } = HandlingUnitStatus.Open;

    public Guid CreatedBy { get; set; }

    public string? CreatedByName { get; set; }

    public Guid? UpdatedBy { get; set; }

    /// <summary>The create key (unique per company + <see cref="CreatedBy"/>) and its request fingerprint (§4.5 "yaratma anahtarı kayıtta").</summary>
    public string? IdempotencyKey { get; set; }

    public string? RequestFingerprint { get; set; }

    public Guid CorrelationId { get; set; }

    public HandlingUnit Copy()
    {
        var copy = (HandlingUnit)MemberwiseClone();
        copy.Contents = Contents.Select(c => c.Copy()).ToList();
        return copy;
    }
}

/// <summary>
/// §8.9 — the outbound's outbox row (<c>wms_outbox</c>): <c>OutboundShipmentReady</c> written in the SAME transaction as <c>ReadyToShip</c>
/// (§8.21, AC-30 K2). The transport is not wired (plan §3 Dalga 3): rows stay <c>Pending</c>; MVP-6's frozen alternative is the poll of
/// <c>GET /outbound-shipments?status=ReadyToShip</c>. The status vocabulary is the service outbox's (<c>InventoryOutboxMessage</c>); the payload
/// is the event body as JSON text (WAREHOUSE-OUTBOUND <c>OutboundShipmentReadyEvent</c> + the WMS draft §2.3 members).
/// </summary>
public sealed class WarehouseOutboxMessage : EntityBase
{
    public const string PendingStatus = "Pending";

    public const string OutboundShipmentReadyEventName = "OutboundShipmentReady";

    public string EventName { get; set; } = OutboundShipmentReadyEventName;

    public int EventVersion { get; set; } = 1;

    public Guid AggregateId { get; set; }

    public Guid EventId { get; set; }

    public Guid CorrelationId { get; set; }

    public string Status { get; set; } = PendingStatus;

    public int AttemptCount { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public string PayloadJson { get; set; } = "{}";
}
