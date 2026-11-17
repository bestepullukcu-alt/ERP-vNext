using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.WavePlanning.Enums;

namespace Diten.InventoryService.Domain.WavePlanning.Entities;

/// <summary>
/// MOD-0180 wave (pack §3, §4.1) — the mutable head of one pick wave: the eight form fields, the cutoff and the state. It is NOT stock and it
/// writes no stock, reservation, task or outbound (pack §2.3): no member carries a stock total. Every change is a compare-and-set on
/// <see cref="EntityBase.Version"/>. A wave is never deleted — a Draft is cancelled (<see cref="EntityBase.IsDeleted"/> is never true).
/// </summary>
public sealed class Wave : EntityBase
{
    /// <summary><c>{COMPANY}-WAV-{000001}</c> (RCS-001; MOD-0178 temporary counter, type WAV — Q-4 = A). Unique tenant-wide; never changes.</summary>
    public string Number { get; set; } = string.Empty;

    public string CompanyCodePrefix { get; set; } = string.Empty;

    /// <summary>LOCATION warehouse of the company; never changes after creation (pack §4.1).</summary>
    public Guid WarehouseId { get; set; }

    public string? WarehouseCode { get; set; }

    public WaveType WaveType { get; set; }

    /// <summary>The cutoff in the warehouse's own time zone, <c>yyyy-MM-ddTHH:mm</c> (D-3) — what the user typed.</summary>
    public string CutoffLocal { get; set; } = string.Empty;

    /// <summary><see cref="CutoffLocal"/> in <see cref="TimeZoneId"/>, as a UTC instant (pack §8.4) — sorting, risk and KPIs use this.</summary>
    public DateTimeOffset CutoffAtUtc { get; set; }

    /// <summary>IANA id copied from the warehouse setting at creation; never changes after.</summary>
    public string TimeZoneId { get; set; } = string.Empty;

    /// <summary>The local cutoff was an ambiguous (repeated) hour and the earlier offset was taken (pack §8.4).</summary>
    public bool CutoffAmbiguousResolved { get; set; }

    public string? ShipToFilter { get; set; }

    public int? MaxOrders { get; set; }

    public int? MaxLines { get; set; }

    public Guid? ResponsibleUserId { get; set; }

    public string? Note { get; set; }

    public WaveStatus Status { get; set; } = WaveStatus.Draft;

    public Guid CreatedBy { get; set; }

    public Guid? ReleasedBy { get; set; }

    public DateTimeOffset? ReleasedAt { get; set; }

    public Guid? CancelledBy { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    public string? CancelReason { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>The create request's key (8–100) and fingerprint — unique per (tenant, company, creator, key).</summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    public string RequestFingerprint { get; set; } = string.Empty;

    /// <summary>The last timeline <c>Seq</c> of this wave's stream (a write's events take the next values in the same compare-and-set write).</summary>
    public int EventSeq { get; set; }

    public Guid CorrelationId { get; set; }
}

/// <summary>
/// One wave–outbound membership (pack §4.2). The order enters WHOLE (D-8); the counts are copied from the MOD-0178 outbound when it is added
/// (lines, allocations, planned bins) and feed the limit warnings only — they are not stock. A partial unique index on
/// (tenant, company, outbound) where <see cref="Active"/> keeps an order in at most one active wave.
/// </summary>
public sealed class WaveOrder : EntityBase
{
    public Guid WaveId { get; set; }

    public Guid WarehouseId { get; set; }

    public Guid OutboundId { get; set; }

    public string OutboundNumber { get; set; } = string.Empty;

    public int LineCount { get; set; }

    public int AllocationCount { get; set; }

    public int PlannedTaskCount { get; set; }

    public WaveOrderState State { get; set; } = WaveOrderState.Planned;

    /// <summary>True while <see cref="State"/> is Planned or Issued; false otherwise.</summary>
    public bool Active { get; set; } = true;

    public Guid AddedBy { get; set; }

    public DateTimeOffset AddedAt { get; set; }

    public WaveOrderAddedVia AddedVia { get; set; }

    /// <summary>The user who closed the membership; absent on a system close.</summary>
    public Guid? ClosedBy { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public string? CloseReason { get; set; }
}

/// <summary>
/// Insert-only timeline (pack §4.3), written in the same transaction as the change it records; never deleted. <see cref="WaveId"/> is the
/// STREAM id: the wave's id on a wave event, the setting record's id on a setting event (<see cref="Stream"/> tells them apart). The key event of
/// a command carries its request key, operation and fingerprint.
/// </summary>
public sealed class WaveEvent : EntityBase
{
    public Guid WaveId { get; set; }

    public WaveEventStream Stream { get; set; }

    public int Seq { get; set; }

    public WaveEventKind Kind { get; set; }

    public Guid? ActorUserId { get; set; }

    public bool System { get; set; }

    public DateTimeOffset At { get; set; }

    public string? RequestOp { get; set; }

    public string? IdempotencyKey { get; set; }

    public string? RequestFingerprint { get; set; }

    public Dictionary<string, string?> Data { get; set; } = [];

    public Guid CorrelationId { get; set; }
}

/// <summary>
/// The wave setting of one warehouse (pack §4.5): one record per (tenant, company, warehouse). No record = not wave managed, default
/// thresholds, no time zone. <see cref="GateSeq"/> is a SYSTEM counter raised by every membership write of the warehouse in its own transaction
/// (pack §8.19 "Sıralama") — it is not <see cref="EntityBase.Version"/> and does not disturb the user's If-Match.
/// </summary>
public sealed class WaveWarehouseSettings : EntityBase
{
    public Guid WarehouseId { get; set; }

    public bool WaveManaged { get; set; }

    public string? TimeZoneId { get; set; }

    public int RiskMinutesBeforeCutoff { get; set; }

    public int RiskProgressPercent { get; set; }

    public int PendingTaskMinutes { get; set; }

    public int GateSeq { get; set; }

    /// <summary>The last timeline <c>Seq</c> of this setting's stream.</summary>
    public int EventSeq { get; set; }
}

/// <summary>The shipping address members a wave candidate shows (WAREHOUSE-OUTBOUND <c>Address</c> without its street lines).</summary>
public sealed class WaveOutboundAddress
{
    public string Name { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public string? City { get; set; }

    public string? PostalCode { get; set; }
}

public sealed class WaveOutboundPlannedBin
{
    public Guid LocationId { get; set; }
}

public sealed class WaveOutboundAllocation
{
    public int AllocSeq { get; set; }

    public List<WaveOutboundPlannedBin> PlannedBins { get; set; } = [];
}

public sealed class WaveOutboundLine
{
    public int LineSeq { get; set; }

    public List<WaveOutboundAllocation> Allocations { get; set; } = [];
}

/// <summary>MOD-0178's pick issuance mark on an outbound (pack §5.1 X-1, written by MOD-0178 from DL-2 on): <c>Deferred</c> or <c>Issued</c>.</summary>
public sealed class WaveOutboundPickIssuance
{
    public string? State { get; set; }

    public Guid? WaveId { get; set; }
}

/// <summary>
/// MOD-0180's READ-ONLY view of a MOD-0178 outbound document (<c>wms_outbound_shipments</c>): only the members a wave needs. MOD-0180 never
/// writes this collection (pack §2.3 rule 2) — it is read with <c>Find</c> only; every other member of the document is ignored.
/// </summary>
public sealed class WaveOutboundDocument : EntityBase
{
    public string OutboundNumber { get; set; } = string.Empty;

    public Guid WarehouseId { get; set; }

    public string? OrderRef { get; set; }

    public WaveOutboundAddress ShipTo { get; set; } = new();

    /// <summary>MOD-0178 <c>OutboundStatus</c> by name.</summary>
    public string Status { get; set; } = string.Empty;

    public List<WaveOutboundLine> Lines { get; set; } = [];

    public WaveOutboundPickIssuance? PickIssuance { get; set; }
}
