using Diten.PlanningService.Application.Features.Mrp;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using Diten.PlanningService.Persistence.Features.DemandPlanning;
using MongoDB.Driver;

namespace Diten.PlanningService.Persistence.Features.Mrp;

public sealed class MrpPublishedDemandReader(DemandPlanningMongoContext context,
    PublishedRevisionMongoStore published) : IMrpPublishedDemandReader
{
    public async Task<MrpPublishedDemand?> ReadAsync(Guid tenantId, Guid legalEntityId,
        string planningPeriodKey, Guid skuId, Guid warehouseId, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || legalEntityId == Guid.Empty || skuId == Guid.Empty ||
            warehouseId == Guid.Empty || string.IsNullOrWhiteSpace(planningPeriodKey))
            return null;
        var slots = await context.PublishedBaselineSlots.Find(x =>
            x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
            x.PlanningPeriodKey == planningPeriodKey && !x.IsDeleted)
            .Limit(2).ToListAsync(cancellationToken);
        if (slots.Count == 0) return null;
        if (slots.Count != 1 || !slots[0].IsAvailable)
            throw new InvalidDataException("Current demand baseline is unavailable or ambiguous.");
        var slot = slots[0];
        var snapshot = await published.ReadSnapshotAsync(tenantId, legalEntityId,
            slot.RevisionId, cancellationToken);
        if (snapshot is null || snapshot.Value.Manifest.State != DemandRevisionState.Published ||
            snapshot.Value.Manifest.PlanningPeriodKey != planningPeriodKey)
            throw new InvalidDataException("Current demand baseline is not a verified Published revision.");
        var manifest = snapshot.Value.Manifest;
        var warehouse = warehouseId.ToString("D");
        var part = snapshot.Value.Parts.SingleOrDefault(x =>
            x.SkuId == skuId && x.WarehouseId == warehouse);
        if (part is null) return null;
        var weeks = part.Rows.Select(row =>
        {
            if (row.ValueKind != DraftWeekValueKind.Known || row.Quantity is null or < 0)
                throw new InvalidDataException("Demand contains an untrusted week.");
            return new MrpDemandWeek(row.WeekStart, row.Quantity.Value);
        }).ToArray();
        var after = await context.PublishedBaselineSlots.Find(x =>
            x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
            x.PlanningPeriodKey == planningPeriodKey && !x.IsDeleted)
            .Limit(2).ToListAsync(cancellationToken);
        if (after.Count != 1 || !after[0].IsAvailable ||
            after[0].RevisionId != slot.RevisionId || after[0].Version != slot.Version)
            throw new InvalidDataException("Demand baseline changed during MRP read.");
        return new(tenantId, legalEntityId, manifest.RevisionId, manifest.StateVersion,
            manifest.Checksum, skuId, warehouseId, part.BaseUomId, weeks);
    }
}
