namespace Diten.InventoryService.Application.Features.PutawayTasks;

// WMS-BUNDLE draft §3.1–§3.4 / §3.9 wire shapes of the putaway tasks (unwrapped success bodies; an empty optional member is omitted, never
// written as null — WMS draft §1 rule 4, the service's JSON options). Quantities are decimal strings in the SKU's base unit; times UTC.

public sealed record TaskItemDto(string ItemId, string SkuId, string SkuLevel);

public sealed record TaskPersonDto(string UserId, string? Name = null);

public sealed record TaskStampDto(string UserId, string AtUtc, string? Name = null);

public sealed record TaskSourceRefDto(string SourceModule, string? SourceType, string? SourceDocumentId, string? SourceLineId, string? TransactionId);

public sealed record TaskErrorDto(string? Code, string? Message, object? Details, int? Status, string? Reason);

public sealed record TaskHolderMovementDto(
    string TransactionId,
    string TransactionNumber,
    string SourceModule,
    string SourceType,
    string? SourceDocumentId,
    string? SourceLineId,
    string Quantity,
    string UomId,
    string BaseQuantity,
    string? FromWarehouseId,
    string? FromLocationId,
    string? ToWarehouseId,
    string? ToLocationId,
    string PostingDate,
    string? LotId,
    IReadOnlyList<string>? SerialIds,
    string? FromStockStatus,
    string? ToStockStatus,
    string? ReversedByTransactionId);

/// <summary>§3.2 <c>TaskConfirmation</c>.</summary>
public sealed record TaskConfirmationDto(
    int Seq,
    string Quantity,
    string ToLocationId,
    string Status,
    string PostingKey,
    string TransactionDate,
    string PostingDate,
    string Channel,
    TaskPersonDto ConfirmedBy,
    string ConfirmedAtUtc,
    int Attempts,
    string? DeviationReason = null,
    IReadOnlyList<string>? SerialIds = null,
    string? TransactionId = null,
    string? TransactionNumber = null,
    TaskErrorDto? Error = null,
    string? Resolution = null,
    string? PriorResolution = null,
    string? ResolutionReason = null,
    string? ScanRef = null,
    string? LastAttemptAtUtc = null,
    string? RefusalSource = null,
    bool? RefusedOnRetry = null,
    string? RefusedAtUtc = null,
    string? InFlightUntilUtc = null,
    string? IntegrityResolution = null,
    string? ResolveReason = null,
    TaskHolderMovementDto? HolderMovement = null);

public sealed record DisabledActionDto(string Action, string ReasonCode);

/// <summary>
/// §3.1 <c>WarehouseTask</c>. <see cref="ConfirmationSeq"/> is present only on the answer of a confirmation / retry (§3.9
/// <c>ConfirmPutawayTaskResult</c> = WarehouseTask + confirmationSeq) — the request's own confirmation, a replay included.
/// </summary>
public sealed record WarehouseTaskDto
{
    public required string TaskId { get; init; }
    public required string Number { get; init; }
    public required string CompanyCodePrefix { get; init; }
    public string? WarehouseCode { get; init; }
    public required string Kind { get; init; }
    public required string Status { get; init; }
    public required string Origin { get; init; }
    public TaskSourceRefDto? SourceRef { get; init; }
    public string? SourceSystem { get; init; }
    public string? ExternalRef { get; init; }
    public required string LegalEntityId { get; init; }
    public required string WarehouseId { get; init; }
    public required TaskItemDto Item { get; init; }
    public string? LotId { get; init; }
    public string? LotNumber { get; init; }
    public required string StockStatus { get; init; }
    public required string FromLocationId { get; init; }
    public string? ToLocationId { get; init; }
    public string? SuggestedToLocationId { get; init; }
    public required string TargetSource { get; init; }
    public required string PlannedQuantity { get; init; }
    public required string ConfirmedQuantity { get; init; }
    public required string PendingQuantity { get; init; }
    public required string RemainingQuantity { get; init; }
    public required string BaseUomId { get; init; }
    public TaskPersonDto? Assignee { get; init; }
    public required string Priority { get; init; }
    public string? Note { get; init; }
    public required bool SerialControlled { get; init; }
    public required IReadOnlyList<TaskConfirmationDto> Confirmations { get; init; }
    public required string IntegrationFlag { get; init; }
    public required IReadOnlyList<string> AllowedActions { get; init; }
    public required IReadOnlyList<DisabledActionDto> DisabledActions { get; init; }
    public string? ShortCloseReason { get; init; }
    public string? CancelReason { get; init; }
    public required TaskStampDto Created { get; init; }
    public TaskStampDto? Updated { get; init; }
    public required int Version { get; init; }
    public int? ConfirmationSeq { get; init; }
    public string ContractVersion { get; init; } = Warehouse.WarehouseContract.ContractVersion;
}

public sealed record WarehouseTaskPageDto(IReadOnlyList<WarehouseTaskDto> Items, long Total, int Page, int PageSize,
    string ContractVersion = Warehouse.WarehouseContract.ContractVersion);

/// <summary>§3.4 <c>WarehouseTaskEvent</c> — the request key / operation / fingerprint of a key event never reach the wire.</summary>
public sealed record WarehouseTaskEventDto(int Seq, string Kind, TaskPersonDto? Actor, bool System, string AtUtc, IReadOnlyDictionary<string, string?> Data,
    string CorrelationId);

public sealed record WarehouseTaskEventPageDto(IReadOnlyList<WarehouseTaskEventDto> Items, long Total, int Page, int PageSize,
    string ContractVersion = Warehouse.WarehouseContract.ContractVersion);

public sealed record BlockingDocumentDto(string Kind, string Id, string? Number = null);

/// <summary>§3.3 <c>PutawayCandidate</c> — computed from the INVENTORY balance at read time, never stored.</summary>
public sealed record PutawayCandidateDto(
    string CandidateKey,
    string WarehouseId,
    string LocationId,
    string? LocationCode,
    TaskItemDto Item,
    string StockStatus,
    string OnHand,
    string OpenTaskQuantity,
    string OpenQuantity,
    string BaseUomId,
    bool Eligible,
    string? LotId = null,
    string? LotNumber = null,
    string? LotStatus = null,
    string? NotEligibleReason = null,
    BlockingDocumentDto? BlockingDocument = null,
    string? LastPostingDate = null);

public sealed record PutawayCandidatePageDto(IReadOnlyList<PutawayCandidateDto> Items, long Total, int Page, int PageSize,
    string ContractVersion = Warehouse.WarehouseContract.ContractVersion);

public sealed record BulkAssignRowDto(string TaskId, string Outcome, int? Version = null);

public sealed record BulkAssignResultDto(IReadOnlyList<BulkAssignRowDto> Results, string ContractVersion = Warehouse.WarehouseContract.ContractVersion);

/// <summary>
/// The source bucket of a new task, as the body names it: the explicit members, or a candidate's <c>candidateKey</c> (which carries no item —
/// <see cref="ItemId"/> is then null and the handler takes it from the bucket's balance row).
/// </summary>
public sealed record TaskSourceInput(Guid WarehouseId, Guid FromLocationId, Guid? ItemId, Guid SkuId, Domain.Enums.SkuLevel SkuLevel, string? LotId,
    Domain.Enums.StockStatus StockStatus);

public sealed record CreatePutawayTaskInput(
    TaskSourceInput Source,
    string? PlannedQuantity,
    Guid? ToLocationId,
    Domain.Warehouse.Enums.WarehouseTargetSource TargetSource,
    Guid? AssigneeUserId,
    Domain.Warehouse.Enums.WarehouseTaskPriority Priority,
    string? Note);

/// <summary>An update: a member not sent is null (kept); <see cref="NoteSent"/> / <see cref="ToLocationSent"/> tell "absent" from "cleared".</summary>
public sealed record UpdatePutawayTaskInput(
    string? PlannedQuantity,
    bool ToLocationSent,
    Guid? ToLocationId,
    Domain.Warehouse.Enums.WarehouseTargetSource? TargetSource,
    bool AssigneeSent,
    Guid? AssigneeUserId,
    Domain.Warehouse.Enums.WarehouseTaskPriority? Priority,
    bool NoteSent,
    string? Note);

public sealed record ConfirmPutawayTaskInput(string? Quantity, Guid ToLocationId, string? DeviationReason, Domain.Warehouse.Enums.ConfirmationChannel Channel,
    string? ScanRef);

public sealed record BulkAssignItem(Guid TaskId, int Version);
