namespace Diten.PlanningService.Application.Features.Mrp;

public sealed record MrpDemandWeek(DateOnly WeekStart, decimal Quantity);

public sealed record MrpPublishedDemand(Guid TenantId, Guid LegalEntityId,
    Guid RevisionId, int StateVersion, string Checksum, Guid SkuId, Guid WarehouseId,
    string BaseUomId, IReadOnlyList<MrpDemandWeek> Weeks);

public sealed record MrpAvailableStock(Guid TenantId, Guid LegalEntityId,
    Guid SkuId, Guid WarehouseId, string BaseUomId, decimal Available,
    DateTimeOffset AsOf);

public sealed record MrpApprovedLeadTime(Guid TenantId, Guid LegalEntityId,
    Guid SkuId, Guid WarehouseId, string RevisionId, int Days,
    DateOnly EffectiveFrom, DateOnly? EffectiveUntil);

public enum MrpSafetyStockStatus { Found, NotFound, Unavailable }

public sealed record MrpApprovedSafetyStock(decimal Quantity, string BaseUomId,
    Guid PolicyRevisionId, DateOnly EffectiveFrom, DateOnly EffectiveUntil,
    long RecordVersion, string Source);

public sealed record MrpSafetyStockRead(MrpSafetyStockStatus Status,
    MrpApprovedSafetyStock? Value = null);

public interface IMrpApprovedSafetyStockReader
{
    Task<MrpSafetyStockRead> ReadAsync(Guid tenantId, Guid legalEntityId,
        Guid skuId, Guid warehouseId, DateOnly asOf, CancellationToken cancellationToken);
}

public interface IMrpPublishedDemandReader
{
    Task<MrpPublishedDemand?> ReadAsync(Guid tenantId, Guid legalEntityId,
        string planningPeriodKey, Guid skuId, Guid warehouseId, CancellationToken cancellationToken);
}

public interface IMrpInventoryReader
{
    Task<MrpAvailableStock?> ReadAsync(Guid tenantId, Guid legalEntityId,
        Guid skuId, Guid warehouseId, CancellationToken cancellationToken);
}

public interface IMrpApprovedLeadTimeReader
{
    Task<MrpApprovedLeadTime?> ReadAsync(Guid tenantId, Guid legalEntityId,
        Guid skuId, Guid warehouseId, DateOnly horizonStart,
        DateOnly horizonEnd, CancellationToken cancellationToken);
}
