using Diten.InventoryService.Application.Features.WavePlanning.Ports;
using Diten.InventoryService.Application.Features.Waves;
using Diten.InventoryService.Domain.WavePlanning.Entities;
using Diten.InventoryService.Domain.WavePlanning.Rules;

namespace Diten.InventoryService.Application.Features.WavePlanning.Services;

/// <summary>Stored record → wire (draft §3). The totals and the limit warnings are computed here from the wave's active memberships.</summary>
public static class WaveReads
{
    public static WaveDto Dto(Wave w, WaveTotals totals)
    {
        var warnings = WaveRules.LimitWarnings(w.MaxOrders, w.MaxLines, totals);
        return new WaveDto
        {
            WaveId = w.Id.ToString("D"),
            Number = w.Number,
            WarehouseId = w.WarehouseId.ToString("D"),
            WarehouseCode = w.WarehouseCode ?? string.Empty,
            WaveType = w.WaveType.ToString(),
            CutoffLocal = w.CutoffLocal,
            CutoffAtUtc = WaveSupport.Wire(w.CutoffAtUtc),
            TimeZoneId = w.TimeZoneId,
            CutoffAmbiguousResolved = w.CutoffAmbiguousResolved ? true : null,
            ShipToFilter = w.ShipToFilter,
            MaxOrders = w.MaxOrders,
            MaxLines = w.MaxLines,
            ResponsibleUserId = w.ResponsibleUserId?.ToString("D"),
            Note = w.Note,
            Status = w.Status.ToString(),
            OrderCount = totals.OrderCount,
            LineCount = totals.LineCount,
            PlannedTaskCount = totals.PlannedTaskCount,
            LimitWarnings = warnings.Count == 0 ? null : warnings.Select(x => new WaveLimitWarningDto(x.Kind.ToString(), x.Limit, x.Actual)).ToList(),
            CreatedBy = w.CreatedBy.ToString("D"),
            CreatedAt = WaveSupport.Wire(w.CreatedAt),
            UpdatedAt = WaveSupport.Wire(w.UpdatedAt),
            ReleasedBy = w.ReleasedBy?.ToString("D"),
            ReleasedAt = WaveSupport.Wire(w.ReleasedAt),
            CancelledBy = w.CancelledBy?.ToString("D"),
            CancelledAt = WaveSupport.Wire(w.CancelledAt),
            CancelReason = w.CancelReason,
            CompletedAt = WaveSupport.Wire(w.CompletedAt),
            Version = w.Version
        };
    }

    public static WaveOrderDto Order(WaveOrder o) => new()
    {
        WaveId = o.WaveId.ToString("D"),
        OutboundId = o.OutboundId.ToString("D"),
        OutboundNumber = o.OutboundNumber,
        LineCount = o.LineCount,
        AllocationCount = o.AllocationCount,
        PlannedTaskCount = o.PlannedTaskCount,
        State = o.State.ToString(),
        AddedBy = o.AddedBy.ToString("D"),
        AddedAt = WaveSupport.Wire(o.AddedAt),
        AddedVia = o.AddedVia.ToString(),
        ClosedBy = o.ClosedBy?.ToString("D"),
        ClosedAt = WaveSupport.Wire(o.ClosedAt),
        CloseReason = o.CloseReason,
        Version = o.Version
    };

    public static WaveEventDto Event(WaveEvent e) => new()
    {
        EventId = e.Id.ToString("D"),
        WaveId = e.WaveId.ToString("D"),
        Seq = e.Seq,
        Kind = e.Kind.ToString(),
        ActorUserId = e.System ? null : e.ActorUserId?.ToString("D"),
        System = e.System ? true : null,
        AtUtc = WaveSupport.Wire(e.At),
        Data = e.Data.Where(kv => kv.Value is not null).ToDictionary(kv => kv.Key, kv => kv.Value!, StringComparer.Ordinal)
    };

    /// <summary>Draft §3.5: eligible ⇔ Allocated with deferred pick issuance; the only ineligible row listed is an Allocating outbound.</summary>
    public static WaveCandidateDto Candidate(WaveOutbound o)
    {
        var eligible = IsEligible(o);
        return new WaveCandidateDto
        {
            OutboundId = o.Id.ToString("D"),
            OutboundNumber = o.Number,
            OrderRef = o.OrderRef,
            ShipTo = new WaveShipToDto { Name = o.ShipTo.Name, Country = o.ShipTo.Country, City = o.ShipTo.City, PostalCode = o.ShipTo.PostalCode },
            LineCount = o.LineCount,
            AllocationCount = o.AllocationCount,
            PlannedTaskCount = o.PlannedTaskCount,
            CreatedAt = WaveSupport.Wire(o.CreatedAt),
            Eligible = eligible,
            IneligibleReason = eligible ? null : WaveContract.AllocationPending
        };
    }

    /// <summary>Pack §8.2 rule 2 (without the membership part, which is the wave store's): Allocated AND pick issuance deferred.</summary>
    public static bool IsEligible(WaveOutbound o)
        => o.Status == WaveRules.EligibleOutboundStatus && o.PickIssuanceState == WaveRules.DeferredPickIssuance;

    /// <summary>The setting as stored, or — no record — the defaults with version 0 (draft §3.4).</summary>
    public static WaveWarehouseSettingsDto Settings(Guid warehouseId, WaveWarehouseSettings? s, long deferredOrderCount, long releasingOrderCount) => new()
    {
        WarehouseId = warehouseId.ToString("D"),
        WaveManaged = s?.WaveManaged ?? false,
        TimeZoneId = s?.TimeZoneId,
        RiskMinutesBeforeCutoff = s?.RiskMinutesBeforeCutoff ?? WaveRules.DefaultRiskMinutesBeforeCutoff,
        RiskProgressPercent = s?.RiskProgressPercent ?? WaveRules.DefaultRiskProgressPercent,
        PendingTaskMinutes = s?.PendingTaskMinutes ?? WaveRules.DefaultPendingTaskMinutes,
        DeferredOrderCount = deferredOrderCount,
        ReleasingOrderCount = releasingOrderCount,
        StuckOrders = [],
        Version = s?.Version ?? 0
    };
}
