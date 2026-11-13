using Diten.InventoryService.Application.Features.WavePlanning;

namespace Diten.InventoryService.Application.Features.Waves;

// WMS-WAVE-BUNDLE v1 draft §3 — every DTO of the DL-1 wave surface (Golden Slim: one models file). Empty optional members are not written
// (draft §1 rule 4; the service's serializer skips nulls).

/// <summary>Draft §3.1 <c>limitWarnings[]</c> — an exceeded business limit (written, never refused — D-7).</summary>
public sealed record WaveLimitWarningDto(string Kind, int Limit, int Actual);

/// <summary>Draft §3.1 <c>Wave</c>.</summary>
public sealed record WaveDto
{
    public required string WaveId { get; init; }
    public required string Number { get; init; }
    public required string WarehouseId { get; init; }
    public required string WarehouseCode { get; init; }
    public required string WaveType { get; init; }
    public required string CutoffLocal { get; init; }
    public required string CutoffAtUtc { get; init; }
    public required string TimeZoneId { get; init; }

    /// <summary>Written only when true (the cutoff was an ambiguous local hour; the earlier offset was taken).</summary>
    public bool? CutoffAmbiguousResolved { get; init; }

    public string? ShipToFilter { get; init; }
    public int? MaxOrders { get; init; }
    public int? MaxLines { get; init; }
    public string? ResponsibleUserId { get; init; }
    public string? Note { get; init; }
    public required string Status { get; init; }
    public required int OrderCount { get; init; }
    public required int LineCount { get; init; }
    public required int PlannedTaskCount { get; init; }

    /// <summary>Written only while a business limit is exceeded.</summary>
    public IReadOnlyList<WaveLimitWarningDto>? LimitWarnings { get; init; }

    public required string CreatedBy { get; init; }
    public required string CreatedAt { get; init; }
    public string? UpdatedAt { get; init; }
    public string? ReleasedBy { get; init; }
    public string? ReleasedAt { get; init; }
    public string? CancelledBy { get; init; }
    public string? CancelledAt { get; init; }
    public string? CancelReason { get; init; }
    public string? CompletedAt { get; init; }
    public required int Version { get; init; }
    public string ContractVersion { get; init; } = WaveContract.ContractVersion;
}

/// <summary>Draft §3.2 <c>WaveOrder</c>.</summary>
public sealed record WaveOrderDto
{
    public required string WaveId { get; init; }
    public required string OutboundId { get; init; }
    public required string OutboundNumber { get; init; }
    public required int LineCount { get; init; }
    public required int AllocationCount { get; init; }
    public required int PlannedTaskCount { get; init; }
    public required string State { get; init; }
    public required string AddedBy { get; init; }
    public required string AddedAt { get; init; }
    public required string AddedVia { get; init; }
    public string? ClosedBy { get; init; }
    public string? ClosedAt { get; init; }
    public string? CloseReason { get; init; }
    public required int Version { get; init; }
}

/// <summary>Draft §3.3 <c>WaveEvent</c> — the actor is a user (<see cref="ActorUserId"/>) or the system (<see cref="System"/> = true).</summary>
public sealed record WaveEventDto
{
    public required string EventId { get; init; }
    public required string WaveId { get; init; }
    public required int Seq { get; init; }
    public required string Kind { get; init; }
    public string? ActorUserId { get; init; }
    public bool? System { get; init; }
    public required string AtUtc { get; init; }
    public required IReadOnlyDictionary<string, string> Data { get; init; }
}

/// <summary>Draft §3.4 <c>stuckOrders[]</c> (DL-2 fills it; DL-1 always answers an empty list).</summary>
public sealed record WaveStuckOrderDto(string OutboundId, string OutboundNumber, string Code, string FirstFailedAtUtc);

/// <summary>Draft §3.4 <c>WaveWarehouseSettings</c> — no record yet = the defaults with <c>version: 0</c>.</summary>
public sealed record WaveWarehouseSettingsDto
{
    public required string WarehouseId { get; init; }
    public required bool WaveManaged { get; init; }
    public string? TimeZoneId { get; init; }
    public required int RiskMinutesBeforeCutoff { get; init; }
    public required int RiskProgressPercent { get; init; }
    public required int PendingTaskMinutes { get; init; }
    public required long DeferredOrderCount { get; init; }
    public required long ReleasingOrderCount { get; init; }
    public required IReadOnlyList<WaveStuckOrderDto> StuckOrders { get; init; }
    public required int Version { get; init; }
    public string ContractVersion { get; init; } = WaveContract.ContractVersion;
}

public sealed record WaveShipToDto
{
    public required string Name { get; init; }
    public required string Country { get; init; }
    public string? City { get; init; }
    public string? PostalCode { get; init; }
}

/// <summary>Draft §3.5 <c>WaveCandidate</c> — a row of the planner's left list and of "Suggest".</summary>
public sealed record WaveCandidateDto
{
    public required string OutboundId { get; init; }
    public required string OutboundNumber { get; init; }
    public string? OrderRef { get; init; }
    public required WaveShipToDto ShipTo { get; init; }
    public required int LineCount { get; init; }
    public required int AllocationCount { get; init; }
    public required int PlannedTaskCount { get; init; }
    public required string CreatedAt { get; init; }
    public required bool Eligible { get; init; }
    public string? IneligibleReason { get; init; }
}

/// <summary>Draft §3.6 — one row of an add / remove answer.</summary>
public sealed record WaveOrderRowDto(string OutboundId, string Outcome, WaveOrderDto? Order = null);

/// <summary>Draft §3.6 <c>WaveOrdersResult</c>.</summary>
public sealed record WaveOrdersResultDto(WaveDto Wave, IReadOnlyList<WaveOrderRowDto> Orders, string ContractVersion = WaveContract.ContractVersion);

public sealed record WaveLoadZoneDto
{
    public required string ZoneKey { get; init; }
    public string? Aisle { get; init; }
    public required int PlannedTaskCount { get; init; }
    public required int OrderCount { get; init; }
}

public sealed record WaveLoadPickerDto(string UserId, int TaskCount);

/// <summary>Draft §3.7 <c>WaveLoad</c>.</summary>
public sealed record WaveLoadDto(IReadOnlyList<WaveLoadZoneDto> Zones, IReadOnlyList<WaveLoadPickerDto> Pickers, string ContractVersion = WaveContract.ContractVersion);

public sealed record WavePageDto(IReadOnlyList<WaveDto> Items, long Total, int Page, int PageSize, string ContractVersion = WaveContract.ContractVersion);

public sealed record WaveOrderPageDto(IReadOnlyList<WaveOrderDto> Items, long Total, int Page, int PageSize, string ContractVersion = WaveContract.ContractVersion);

public sealed record WaveEventPageDto(IReadOnlyList<WaveEventDto> Items, long Total, int Page, int PageSize, string ContractVersion = WaveContract.ContractVersion);

public sealed record WaveCandidatePageDto(IReadOnlyList<WaveCandidateDto> Items, long Total, int Page, int PageSize,
    string ContractVersion = WaveContract.ContractVersion);

/// <summary>Draft §4.1 <c>listWaveSuggestions</c> — what "Suggest" proposes; nothing is written.</summary>
public sealed record WaveSuggestionsDto(IReadOnlyList<WaveCandidateDto> Items, string LimitedBy, string ContractVersion = WaveContract.ContractVersion);

/// <summary>The Draft form (draft §4.1 create / update body without the key): the eight form fields (pack §4.1, form_field_count = 8).</summary>
public sealed record WaveFormInput(Guid WarehouseId, string WaveType, string CutoffLocal, string? ShipToFilter, int? MaxOrders, int? MaxLines,
    Guid? ResponsibleUserId, string? Note);

/// <summary>The warehouse setting a user writes (draft §4.1 <c>updateWaveWarehouseSettings</c> without key and reason).</summary>
public sealed record WaveSettingsInput(bool WaveManaged, string? TimeZoneId, int RiskMinutesBeforeCutoff, int RiskProgressPercent, int PendingTaskMinutes);
