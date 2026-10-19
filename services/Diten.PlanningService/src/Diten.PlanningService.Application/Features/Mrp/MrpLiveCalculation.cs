namespace Diten.PlanningService.Application.Features.Mrp;

public enum MrpCalculationStatus { Found, Invalid, Forbidden, Unavailable }
public sealed record MrpCalculationResult(MrpCalculationStatus Status, MrpResult? Plan = null);

public sealed class MrpLiveCalculation(ILegalEntityScopeReader companies,
    IMrpPublishedDemandReader demand, IMrpInventoryReader inventory,
    IMrpApprovedLeadTimeReader leadTimes)
{
    public async Task<MrpCalculationResult> CalculateAsync(Guid tenantId, Guid legalEntityId,
        string period, Guid skuId, Guid warehouseId, CancellationToken ct)
    {
        if (tenantId == Guid.Empty || legalEntityId == Guid.Empty || skuId == Guid.Empty ||
            warehouseId == Guid.Empty || string.IsNullOrWhiteSpace(period))
            return new(MrpCalculationStatus.Invalid);
        try
        {
            var company = await companies.ValidateAsync(tenantId, legalEntityId, ct);
            if (company == LegalEntityReferenceResult.NotFound)
                return new(MrpCalculationStatus.Forbidden);
            if (company != LegalEntityReferenceResult.Active)
                return new(MrpCalculationStatus.Unavailable);

            var published = await demand.ReadAsync(tenantId, legalEntityId, period, skuId, warehouseId, ct);
            if (published is null || published.TenantId != tenantId ||
                published.LegalEntityId != legalEntityId || published.SkuId != skuId ||
                published.WarehouseId != warehouseId || published.RevisionId == Guid.Empty ||
                published.StateVersion <= 0 || string.IsNullOrWhiteSpace(published.Checksum) ||
                published.Weeks.Count != 52)
                return new(MrpCalculationStatus.Unavailable);
            var stock = await inventory.ReadAsync(tenantId, legalEntityId, skuId, warehouseId, ct);
            if (stock is null || stock.TenantId != tenantId || stock.LegalEntityId != legalEntityId ||
                stock.SkuId != skuId || stock.WarehouseId != warehouseId ||
                stock.BaseUomId != published.BaseUomId || stock.AsOf == default ||
                DateOnly.FromDateTime(stock.AsOf.UtcDateTime) > published.Weeks[0].WeekStart)
                return new(MrpCalculationStatus.Unavailable);
            var leadTime = await leadTimes.ReadAsync(tenantId, legalEntityId, skuId, warehouseId,
                published.Weeks[0].WeekStart, published.Weeks[^1].WeekStart.AddDays(6), ct);
            if (leadTime is null || leadTime.TenantId != tenantId ||
                leadTime.LegalEntityId != legalEntityId || leadTime.SkuId != skuId ||
                leadTime.WarehouseId != warehouseId)
                return new(MrpCalculationStatus.Unavailable);

            var stillPublished = await demand.ReadAsync(tenantId, legalEntityId, period,
                skuId, warehouseId, ct);
            if (stillPublished is null || stillPublished.RevisionId != published.RevisionId ||
                stillPublished.StateVersion != published.StateVersion ||
                stillPublished.Checksum != published.Checksum)
                return new(MrpCalculationStatus.Unavailable);

            var weeks = published.Weeks.Select(w =>
                new MrpWeek(w.WeekStart, w.Quantity, 0m, MrpWeekQuality.Reliable)).ToArray();
            var input = new MrpInput(tenantId, legalEntityId, skuId, warehouseId,
                published.BaseUomId, published.RevisionId.ToString("D"),
                published.StateVersion, published.Checksum, stock.AsOf, "",
                leadTime.RevisionId, leadTime.Days, stock.Available, weeks);
            return new(MrpCalculationStatus.Found, MrpNetting.Calculate(input));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { return new(MrpCalculationStatus.Unavailable); }
    }
}
