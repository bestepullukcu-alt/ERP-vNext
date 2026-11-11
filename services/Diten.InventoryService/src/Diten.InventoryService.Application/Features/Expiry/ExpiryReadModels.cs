namespace Diten.InventoryService.Application.Features.Expiry;

// WP-0176-F4 — the FEFO-BUNDLE read shapes (fefo.openapi.yaml 1.1.0: DashboardView, LotExpiryPage / LotExpiryRow, FefoOrder). Quantities
// are numbers (no money values — pack §8.8, D-13); "Kullanılabilir" (AVAILABLE) and "Bekleyen" (any other status) stay apart (ASSUMPTION-0176-09).

public sealed record ExpiryOnHandDto(decimal Available, decimal Held, string BaseUomId);

public sealed record LotExpiryRowDto(
    string LotId,
    string LotNumber,
    string LotStatus,
    ExpiryItemRefDto Item,
    ExpiryWarehouseRefDto Warehouse,
    string ExpiryDate,
    string? RetestDate,
    string? ManufacturingDate,
    int? DaysToExpiry,
    int? DaysSinceExpiry,
    string Severity,
    decimal? RemainingShelfLifePct,
    int? ShelfLifeDays,
    ExpiryOnHandDto OnHand,
    IReadOnlyList<string>? HeldReasons,
    string? OpenExceptionId);

public sealed record LotExpiryPageDto(IReadOnlyList<LotExpiryRowDto> Items, long Total, int Page, int PageSize);

public sealed record ExpiryLotCountDto(int Lots, decimal Available, decimal Held);

public sealed record ExpiryExpiredCountDto(int Lots, decimal Quantity);

public sealed record ExpiryDashboardKpisDto(
    decimal? FefoCompliancePct,
    int FefoIssues,
    int FefoBypasses,
    ExpiryLotCountDto Critical,
    ExpiryLotCountDto Approaching,
    ExpiryExpiredCountDto ExpiredOnHand,
    int OpenExceptions,
    int LateExceptions,
    decimal? AvgResolutionHours);

public sealed record ExpiryMonthDto(string Month, decimal Available, decimal Held);

public sealed record ExpiryPeriodDto(string From, string To);

public sealed record ExpiryDashboardDto(
    string AsOf,
    ExpiryPeriodDto Period,
    string? WarehouseId,
    ExpiryDashboardKpisDto Kpis,
    IReadOnlyList<ExpiryMonthDto> ExpiryByMonth,
    IReadOnlyList<LotExpiryRowDto> TopRisk);

public sealed record FefoEligibleLotDto(
    int Rank,
    string LotId,
    string LotNumber,
    string? ExpiryDate,
    string? ManufacturingDate,
    string? RetestDate,
    decimal Available,
    string Severity,
    decimal? Allocated);

public sealed record FefoExcludedLotDto(string LotId, string LotNumber, string? ExpiryDate, string Reason, decimal OnHand);

public sealed record FefoOrderDto(
    string LegalEntityId,
    string WarehouseId,
    string ItemId,
    string SkuId,
    string SkuLevel,
    string AsOf,
    string Basis,
    decimal SkuAvailable,
    IReadOnlyList<FefoEligibleLotDto> Eligible,
    IReadOnlyList<FefoExcludedLotDto> Excluded,
    decimal? RequestedQuantity,
    decimal? AllocatedQuantity,
    decimal? Shortfall);
