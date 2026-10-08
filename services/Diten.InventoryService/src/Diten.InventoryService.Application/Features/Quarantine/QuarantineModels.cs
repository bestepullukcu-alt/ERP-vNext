namespace Diten.InventoryService.Application.Features.Quarantine;

// QUARANTINE-BUNDLE v1 wire schemas (contract §2). Null members are omitted on the wire (Program.cs JSON options), so an
// optional member the slice cannot fill yet (a master-data name, a signature id) is simply absent — never invented.

public sealed record ItemRefDto(string ItemId, string SkuId, string SkuLevel, string? SkuCode = null, string? Name = null);

public sealed record LotInfoDto(string LotId, string? LotNumber, string? ExpiryDate, string? RetestDate, string? CoaReference, string? LotStatus);

public sealed record WarehouseRefDto(string Id, string? Code = null, string? Name = null);

public sealed record LocationRefDto(string Id, string? Code = null);

public sealed record UomRefDto(string Id, string? Code = null);

public sealed record UserRefDto(string UserId, string? Name = null);

public sealed record SourceDocumentDto(string? Module, string? Type, string? Id, string? Number, string? Text);

public sealed record ActiveDecisionDto(string DecisionId, string DecisionNumber, string State);

public sealed record DecisionRefDto(string DecisionId, string DecisionNumber, string Kind, string State);

public sealed record AttachmentDto(string Name, string DmsRef);

public sealed record HistoryEntryDto(string AtUtc, UserRefDto By, string Action, string? Reason, string? DecisionId, string? SignatureId, string? ApproverSignatureId);

public sealed record MovementLinkDto(
    string TransactionId,
    string TransactionNumber,
    string MovementType,
    string AtUtc,
    string? FromStockStatus,
    string? ToStockStatus,
    string? FromLocationId,
    string? ToLocationId,
    decimal Quantity,
    string? ByUserName = null);

/// <summary>Contract §2.1 <c>HoldRef</c>.</summary>
public class HoldRefDto
{
    public string HoldId { get; init; } = string.Empty;
    public string HoldNumber { get; init; } = string.Empty;
    public string LegalEntityId { get; init; } = string.Empty;
    public ItemRefDto Item { get; init; } = null!;
    public LotInfoDto? Lot { get; init; }
    public WarehouseRefDto Warehouse { get; init; } = null!;
    public LocationRefDto Location { get; init; } = null!;
    public string StockStatus { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public UomRefDto BaseUom { get; init; } = null!;
    public string Source { get; init; } = string.Empty;
    public SourceDocumentDto? SourceDocument { get; init; }
    public string EnteredAtUtc { get; init; } = string.Empty;
    public UserRefDto CreatedBy { get; init; } = null!;
    public string ReviewState { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int WaitDays { get; init; }
    public int TargetDays { get; init; }
    public bool Late { get; init; }
    public ActiveDecisionDto? ActiveDecision { get; init; }
    public int Version { get; init; }
}

/// <summary>Contract §2.2 <c>HoldDetail</c> = HoldRef + quality facts, history, movements and decisions.</summary>
public sealed class HoldDetailDto : HoldRefDto
{
    public string? AnalysisNote { get; init; }
    public IReadOnlyList<AttachmentDto> Attachments { get; init; } = [];
    public string? ReviewStartedAtUtc { get; init; }
    public UserRefDto? ReviewStartedBy { get; init; }
    public string? DecidedAtUtc { get; init; }
    public IReadOnlyList<HistoryEntryDto> History { get; init; } = [];
    public IReadOnlyList<MovementLinkDto> Movements { get; init; } = [];
    public IReadOnlyList<DecisionRefDto> Decisions { get; init; } = [];
}

public sealed record QuantityByUomDto(string UomCode, decimal Quantity);

public sealed record HoldKpisDto(int OpenCount, int InReviewCount, IReadOnlyList<QuantityByUomDto> QuantityByUom, int LateCount, int ExpirySoonCount);

public sealed record HoldListDto(IReadOnlyList<HoldRefDto> Items, long Total, HoldKpisDto Kpis);

public sealed record DecisionLineDto(int LineNo, string Decision, decimal Quantity, string ToStockStatus);

public sealed record DispositionDto(
    string Kind,
    decimal Quantity,
    string? SupplierId,
    string? ReturnDocumentNo,
    string? DestructionMethod,
    string? DestructionDate,
    string? SampleLocationId);

public sealed record PlannedMovementDto(
    int LineNo,
    string MovementType,
    string? FromStockStatus,
    string? ToStockStatus,
    string FromLocationId,
    string? ToLocationId,
    decimal Quantity);

public sealed record WorkflowInfoDto(
    string? InstanceId,
    string TemplateCode,
    string? Outcome,
    UserRefDto? Approver,
    string? ApproverSignatureId,
    string? CompletedAtUtc);

public sealed record ErrorInfoDto(string Code, string? Message);

/// <summary>Contract §2.6 <c>Decision</c>.</summary>
public sealed class DecisionDto
{
    public string DecisionId { get; init; } = string.Empty;
    public string DecisionNumber { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public string? HoldId { get; init; }
    public string? BlockedStockId { get; init; }
    public string? LotId { get; init; }
    public string LegalEntityId { get; init; } = string.Empty;
    public string FromStockStatus { get; init; } = string.Empty;
    public IReadOnlyList<DecisionLineDto> Lines { get; init; } = [];
    public string? BlockReasonCode { get; init; }
    public string Reason { get; init; } = string.Empty;
    public bool CoaChecked { get; init; }
    public string? CoaReference { get; init; }
    public string? NewRetestDate { get; init; }
    public DispositionDto? Disposition { get; init; }
    public string? LotStatusTarget { get; init; }
    public IReadOnlyList<PlannedMovementDto> PlannedMovements { get; init; } = [];
    public UserRefDto Decider { get; init; } = null!;
    public string DecidedAtUtc { get; init; } = string.Empty;
    public string? DeciderSignatureId { get; init; }
    public string? EsignActionCode { get; init; }
    public bool? SignatureRequired { get; init; }
    public string State { get; init; } = string.Empty;
    public WorkflowInfoDto? Workflow { get; init; }
    public IReadOnlyList<string> AppliedMovementIds { get; init; } = [];
    public ErrorInfoDto? Error { get; init; }
}

public sealed record BlockReasonRefDto(string Code, string? Name);

public sealed record OriginRefDto(string? HoldNumber, string? DecisionNumber);

/// <summary>Contract §2.7 <c>BlockedStockRef</c>.</summary>
public class BlockedStockRefDto
{
    public string? BlockedId { get; init; }
    public string? BlockedNumber { get; init; }
    public string LegalEntityId { get; init; } = string.Empty;
    public ItemRefDto Item { get; init; } = null!;
    public LotInfoDto? Lot { get; init; }
    public WarehouseRefDto Warehouse { get; init; } = null!;
    public LocationRefDto Location { get; init; } = null!;
    public decimal Quantity { get; init; }
    public UomRefDto BaseUom { get; init; } = null!;
    public BlockReasonRefDto? BlockReason { get; init; }
    public string? Note { get; init; }
    public string BlockedAtUtc { get; init; } = string.Empty;
    public string Origin { get; init; } = string.Empty;
    public OriginRefDto? OriginRef { get; init; }
    public bool PendingDisposition { get; init; }
    public bool Reviewed { get; init; }
    public string Status { get; init; } = string.Empty;
    public IReadOnlyList<string> AllowedActions { get; init; } = [];
    public ActiveDecisionDto? ActiveDecision { get; init; }
    public int Version { get; init; }
}

/// <summary>Contract §4.13 <c>getBlocked</c>: BlockedStockRef + history + movements + decisions.</summary>
public sealed class BlockedStockDetailDto : BlockedStockRefDto
{
    public IReadOnlyList<HistoryEntryDto> History { get; init; } = [];
    public IReadOnlyList<MovementLinkDto> Movements { get; init; } = [];
    public IReadOnlyList<DecisionRefDto> Decisions { get; init; } = [];
}

public sealed record BlockedListDto(IReadOnlyList<BlockedStockRefDto> Items, long Total);

public sealed record WorkflowTemplateDto(string Code, int? PublishedVersion, string Status);

/// <summary>Contract §2.9 <c>Settings</c>.</summary>
public sealed class SettingsDto
{
    public string? LegalEntityId { get; init; }
    public int WaitTargetDays { get; init; }
    public bool FourEyes { get; init; }
    public IReadOnlyList<string> ApproverCandidates { get; init; } = [];
    public IReadOnlyDictionary<string, string> SampleLocations { get; init; } = new Dictionary<string, string>();
    public bool LotReleaseRequiresApproval { get; init; }
    public int ReconcileAfterSeconds { get; init; }
    public WorkflowTemplateDto? WorkflowTemplate { get; init; }
    public int Version { get; init; }
}

/// <summary>Contract §2.10 <c>BlockReason</c>.</summary>
public sealed record BlockReasonDto(string Code, IReadOnlyDictionary<string, string> Names, bool IsActive, bool IsLocked);

/// <summary>Contract §4.4 / §4.15 reservation entry of <c>QUARANTINE_STOCK_RESERVED</c> details.</summary>
public sealed record ReservationLineDto(string ReservationId, string SourceModule, string? SourceDocumentId, decimal RemainingQuantity);

/// <summary>Contract <c>RetestItem</c> (listRetest, KQ-H).</summary>
public sealed record RetestItemDto(
    string LotId,
    string? LotNumber,
    ItemRefDto Item,
    string RetestDate,
    int DaysRelative,
    string State,
    decimal AvailableQuantity,
    UomRefDto BaseUom,
    IReadOnlyList<RetestBucketDto> Buckets,
    IReadOnlyList<string> HoldIds);

public sealed record RetestBucketDto(string WarehouseId, string LocationId, decimal Quantity);
