using Diten.PlanningService.Application.Features.Mrp;
using MongoDB.Driver;

namespace Diten.PlanningService.Persistence.Features.Mrp;

public sealed class MrpApprovedLeadTimeReader(LeadTimeMongoContext context) : IMrpApprovedLeadTimeReader
{
    public async Task<MrpApprovedLeadTime?> ReadAsync(Guid tenantId, Guid legalEntityId,
        Guid skuId, Guid warehouseId, DateOnly horizonStart, DateOnly horizonEnd,
        CancellationToken cancellationToken)
    {
        var policies = await context.Policies.Find(p =>
            p.TenantId == tenantId.ToString("D") &&
            p.LegalEntityId == legalEntityId.ToString("D") &&
            p.SkuId == skuId.ToString("D") &&
            p.WarehouseId == warehouseId.ToString("D") && !p.IsDeleted)
            .Limit(2).ToListAsync(cancellationToken);
        if (policies.Count != 1) return null;
        var policy = policies[0];
        var revision = policy.Revisions.SingleOrDefault(r => r.Id == policy.CurrentRevisionId);
        if (revision is null || revision.State != "Approved" || revision.Days <= 0) return null;
        var from = DateOnly.FromDateTime(revision.EffectiveFromUtc);
        var until = revision.EffectiveUntilUtc is null ? (DateOnly?)null :
            DateOnly.FromDateTime(revision.EffectiveUntilUtc.Value);
        if (from > horizonStart || (until is not null && until < horizonEnd)) return null;
        return new(tenantId, legalEntityId, skuId, warehouseId, revision.Id,
            revision.Days, from, until);
    }
}
