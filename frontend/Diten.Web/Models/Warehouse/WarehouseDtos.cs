namespace Diten.Web.Models.Warehouse;

// The WMS-BUNDLE draft wire shapes the two S1 screens read (InventoryService PutawayTaskModels / WarehouseLocationRoleModels) — only the
// members the screens use; an absent optional member is null (the service never writes null, WMS draft §1 rule 4).

public sealed class WmsPersonDto
{
    public string? UserId { get; set; }
    public string? Name { get; set; }
}

public sealed class WmsItemDto
{
    public string? ItemId { get; set; }
    public string? SkuId { get; set; }
    public string? SkuLevel { get; set; }
}

public sealed class WmsDisabledActionDto
{
    public string? Action { get; set; }
    public string? ReasonCode { get; set; }
}

public sealed class WmsErrorDto
{
    public string? Code { get; set; }
    public string? Message { get; set; }
}

public sealed class WmsConfirmationDto
{
    public int Seq { get; set; }
    public string? Quantity { get; set; }
    public string? ToLocationId { get; set; }
    public string? Status { get; set; }
    public string? PostingDate { get; set; }
    public WmsPersonDto? ConfirmedBy { get; set; }
    public string? ConfirmedAtUtc { get; set; }
    public int Attempts { get; set; }
    public string? DeviationReason { get; set; }
    public string? TransactionNumber { get; set; }
    public WmsErrorDto? Error { get; set; }
    public string? Resolution { get; set; }
    public string? IntegrityResolution { get; set; }
    public string? LastAttemptAtUtc { get; set; }
    public string? InFlightUntilUtc { get; set; }
}

public sealed class WmsTaskDto
{
    public string? TaskId { get; set; }
    public string? Number { get; set; }
    public string? WarehouseCode { get; set; }
    public string? Status { get; set; }
    public string? Origin { get; set; }
    public string? WarehouseId { get; set; }
    public WmsItemDto? Item { get; set; }
    public string? LotId { get; set; }
    public string? LotNumber { get; set; }
    public string? StockStatus { get; set; }
    public string? FromLocationId { get; set; }
    public string? ToLocationId { get; set; }
    public string? SuggestedToLocationId { get; set; }
    public string? TargetSource { get; set; }
    public string? PlannedQuantity { get; set; }
    public string? ConfirmedQuantity { get; set; }
    public string? PendingQuantity { get; set; }
    public string? RemainingQuantity { get; set; }
    public string? BaseUomId { get; set; }
    public WmsPersonDto? Assignee { get; set; }
    public string? Priority { get; set; }
    public string? Note { get; set; }
    public bool SerialControlled { get; set; }
    public List<WmsConfirmationDto>? Confirmations { get; set; }
    public string? IntegrationFlag { get; set; }
    public List<string>? AllowedActions { get; set; }
    public List<WmsDisabledActionDto>? DisabledActions { get; set; }
    public string? ShortCloseReason { get; set; }
    public string? CancelReason { get; set; }
    public int Version { get; set; }
}

public sealed class WmsTaskPageDto
{
    public List<WmsTaskDto>? Items { get; set; }
    public long? Total { get; set; }
}

public sealed class WmsTaskEventDto
{
    public int Seq { get; set; }
    public string? Kind { get; set; }
    public WmsPersonDto? Actor { get; set; }
    public bool System { get; set; }
    public string? AtUtc { get; set; }
    public Dictionary<string, string?>? Data { get; set; }
}

public sealed class WmsTaskEventPageDto
{
    public List<WmsTaskEventDto>? Items { get; set; }
}

public sealed class WmsBlockingDocumentDto
{
    public string? Kind { get; set; }
    public string? Id { get; set; }
    public string? Number { get; set; }
}

public sealed class WmsCandidateDto
{
    public string? CandidateKey { get; set; }
    public string? WarehouseId { get; set; }
    public string? LocationId { get; set; }
    public string? LocationCode { get; set; }
    public WmsItemDto? Item { get; set; }
    public string? StockStatus { get; set; }
    public string? OpenQuantity { get; set; }
    public string? BaseUomId { get; set; }
    public bool Eligible { get; set; }
    public string? LotNumber { get; set; }
    public string? NotEligibleReason { get; set; }
    public WmsBlockingDocumentDto? BlockingDocument { get; set; }
    /// <summary>The product's PRODUCT-MASTER conditions (draft §3.3): absent = UNKNOWN (§11.3a badge), empty = no condition.</summary>
    public List<string>? StorageConditions { get; set; }
}

public sealed class WmsCandidatePageDto
{
    public List<WmsCandidateDto>? Items { get; set; }
}

public sealed class WmsBulkAssignResultDto
{
    public List<WmsBulkRowDto>? Results { get; set; }
}

public sealed class WmsBulkRowDto
{
    public string? TaskId { get; set; }
    public string? Outcome { get; set; }
    public string? RoleId { get; set; }
    public string? Result { get; set; }
}

public sealed class WmsBulkDeleteResultDto
{
    public List<WmsBulkRowDto>? Items { get; set; }
}

public sealed class WmsEffectiveConditionsDto
{
    public List<string>? Codes { get; set; }
    public string? InheritedFromLocationId { get; set; }
    public string? InheritedFromLocationCode { get; set; }
}

public sealed class WmsLocationRoleDto
{
    public string? RoleId { get; set; }
    public string? WarehouseId { get; set; }
    public string? LocationId { get; set; }
    public string? LocationCode { get; set; }
    public string? LocationType { get; set; }
    public List<string>? Roles { get; set; }
    public List<string>? AcceptedStorageConditions { get; set; }
    public WmsEffectiveConditionsDto? EffectiveStorageConditions { get; set; }
    public string? Note { get; set; }
    public int Version { get; set; }
}

public sealed class WmsLocationRolePageDto
{
    public List<WmsLocationRoleDto>? Items { get; set; }
    public long? Total { get; set; }
}

/// <summary>A write from the putaway screen: the members each action reads (the controller checks which it needs).</summary>
public sealed class PutawayWriteRequest
{
    public string? IdempotencyKey { get; set; }
    public string? IfMatch { get; set; }
    public string? CandidateKey { get; set; }
    public string? PlannedQuantity { get; set; }
    public string? ToLocationId { get; set; }
    public string? TargetSource { get; set; }
    public string? AssigneeUserId { get; set; }
    public string? Priority { get; set; }
    public string? Note { get; set; }
    public string? Quantity { get; set; }
    public string? DeviationReason { get; set; }
    public string? Reason { get; set; }
    /// <summary>Edit: the members the reader changed (absent = unchanged). <c>assigneeUserId</c> / <c>toLocationId</c> / <c>note</c> may be null = cleared.</summary>
    public System.Text.Json.JsonElement Changes { get; set; }
    public List<WriteItem>? Items { get; set; }
}

public sealed class WriteItem
{
    public string? Id { get; set; }
    public int Version { get; set; }
}

/// <summary>A write from the location-role screen.</summary>
public sealed class LocationRoleWriteRequest
{
    public string? IdempotencyKey { get; set; }
    public string? IfMatch { get; set; }
    public string? LocationId { get; set; }
    public List<string>? Roles { get; set; }
    public List<string>? AcceptedStorageConditions { get; set; }
    public string? Note { get; set; }
    public List<WriteItem>? Items { get; set; }
}
