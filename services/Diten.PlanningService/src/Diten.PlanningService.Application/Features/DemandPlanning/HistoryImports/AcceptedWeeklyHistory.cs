namespace Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;

public enum AcceptedWeekState
{
    VerifiedDemand,
    VerifiedZeroDemand,
    Unknown,
    StockoutUnresolved,
    IncompleteAcceptance,
    Quarantined
}

public sealed record AcceptedWeekEvent(Guid AcceptedRowId, DateOnly OccurredOn, decimal Quantity);

public sealed record AcceptedWeekObservation(DateOnly WeekStart, AcceptedWeekState State,
    string? UomId, bool UnitVerificationPending, string? CoverageFileSha256,
    IReadOnlyList<AcceptedWeekEvent> Events);

public sealed record AcceptedSeriesHistory(Guid TenantId, Guid LegalEntityId, Guid SkuId,
    string SkuLevel, string WarehouseId, DateOnly AsOfWeekStart,
    IReadOnlyList<AcceptedWeekObservation> Weeks);

public interface IAcceptedWeeklyHistoryReader
{
    Task<AcceptedSeriesHistory> ReadAsync(Guid tenantId, Guid legalEntityId,
        Guid skuId, string skuLevel, string warehouseId, DateOnly asOfWeekStart,
        int weekCount, CancellationToken cancellationToken);
}
