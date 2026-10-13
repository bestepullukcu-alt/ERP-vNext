namespace Diten.Web.Models.Traceability;

// TRACE-BUNDLE v1 + v1.1 wire shapes (trace.openapi.yaml 1.1.0) as the MVC reads them — read-only DTOs, camelCase on the
// wire (InventoryGatewayClient.Json). Only the members the P3 screens use.

public sealed class LotStockSummaryDto
{
    public string? OnHand { get; set; }
    public string? BaseUomId { get; set; }
    public string? LegalEntityId { get; set; }
    public string? AsOf { get; set; }
}

public class LotRefDto
{
    public string? LotId { get; set; }
    public string? LotNumber { get; set; }
    public string? ItemId { get; set; }
    public string? ManufacturerLot { get; set; }
    public string? SupplierLot { get; set; }
    public string? ManufacturingDate { get; set; }
    public string? ExpiryDate { get; set; }
    public string? RetestDate { get; set; }
    public string? CountryOfOrigin { get; set; }
    public string? Status { get; set; }
    public string? CoaReference { get; set; }
    public string? SkuId { get; set; }
    public string? SkuLevel { get; set; }
    public int? Version { get; set; }
    public string? LegalEntityId { get; set; }
    public LotStockSummaryDto? StockSummary { get; set; }
    public string? ExpiryState { get; set; }
    public bool? RetestDue { get; set; }
    public string? TrackingMode { get; set; }
}

public sealed class LotSourceDto
{
    public string? Kind { get; set; }
    public string? DocumentNumber { get; set; }
    public string? PartyName { get; set; }
    public string? Date { get; set; }
}

public sealed class LotDetailDto : LotRefDto
{
    public LotSourceDto? Source { get; set; }
    public long? SerialCount { get; set; }
}

public sealed class LotListDto
{
    public List<LotRefDto>? Items { get; set; }
    public long? Total { get; set; }
}

public sealed class LotChangeDto
{
    public string? EntryId { get; set; }
    public string? Action { get; set; }
    public string? Field { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Reason { get; set; }
    public string? ActorUserId { get; set; }
    public string? ActorDisplay { get; set; }
    public string? OccurredAt { get; set; }
    public string? CorrelationId { get; set; }
    public string? Source { get; set; }
}

public sealed class LotHistoryPageDto
{
    public List<LotChangeDto>? Items { get; set; }
    public long? Total { get; set; }
}

public sealed class SerialRefDto
{
    public string? SerialId { get; set; }
    public string? SerialNumber { get; set; }
    public string? ItemId { get; set; }
    public string? LotId { get; set; }
    public string? LotNumber { get; set; }
    public string? Status { get; set; }
    public string? SkuId { get; set; }
    public string? SkuLevel { get; set; }
    public string? WarehouseId { get; set; }
    public string? LocationId { get; set; }
    public string? LastTransactionId { get; set; }
    public string? LastMovementAt { get; set; }
}

public sealed class SerialPageDto
{
    public List<SerialRefDto>? Items { get; set; }
    public long? Total { get; set; }
}

public sealed class GenealogyItemDto
{
    public string? ParentLotId { get; set; }
    public string? ChildLotId { get; set; }
    public string? TransformationReference { get; set; }
    public string? SourceModule { get; set; }
}

public sealed class GenealogyDto
{
    public string? LotId { get; set; }
    public List<GenealogyItemDto>? Parents { get; set; }
    public List<GenealogyItemDto>? Children { get; set; }
}

public sealed class Gs1CheckDto
{
    public string? Field { get; set; }
    public string? Result { get; set; }
    public string? Expected { get; set; }
    public string? Actual { get; set; }
}

public sealed class Gs1CheckResultDto
{
    public string? SerialNumber { get; set; }
    public string? Gtin { get; set; }
    public string? LotNumber { get; set; }
    public string? Expiry { get; set; }
    public List<Gs1CheckDto>? Checks { get; set; }
    public bool? Accepted { get; set; }
    public string? GtinSkippedReason { get; set; }
}
