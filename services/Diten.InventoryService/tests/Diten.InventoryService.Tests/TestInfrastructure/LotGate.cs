using Diten.InventoryService.Domain.Quarantine.Entities;
using Diten.InventoryService.Persistence.Quarantine;
using MongoDB.Driver;

namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>
/// MOD-0175 Q4 (KQ-I, CT-Q10): the company switch <c>LotReleaseRequiresApproval</c> — default ON, so a manual Draft / Blocked → Released
/// answers 202 and waits for approval. Test worlds that are not about the gate (MOD-0174 / ledger / quarantine stock scenarios)
/// switch it OFF for their company — the same stored setting a QA admin saves (<c>quarantine_settings</c>), not a code path; the gate
/// tests switch it ON again. Requires the host to be started (BSON class maps), which every scenario does before calling this.
/// </summary>
public static class LotGate
{
    public static void Off(IMongoDatabase database, Guid tenant, Guid legalEntity) => Set(database, tenant, legalEntity, false);

    public static void On(IMongoDatabase database, Guid tenant, Guid legalEntity) => Set(database, tenant, legalEntity, true);

    public static void Set(IMongoDatabase database, Guid tenant, Guid legalEntity, bool requiresApproval)
    {
        var settings = database.GetCollection<QuarantineSettings>(QuarantineCollections.Settings);
        var f = Builders<QuarantineSettings>.Filter;
        var scope = f.Eq(x => x.TenantId, tenant) & f.Eq(x => x.LegalEntityId, legalEntity) & f.Eq(x => x.IsDeleted, false);
        var updated = settings.UpdateOne(scope, Builders<QuarantineSettings>.Update
            .Set(x => x.LotReleaseRequiresApproval, requiresApproval).Inc(x => x.Version, 1));
        if (updated.MatchedCount == 0)
        {
            settings.InsertOne(new QuarantineSettings
            {
                TenantId = tenant,
                LegalEntityId = legalEntity,
                Version = 1,
                LotReleaseRequiresApproval = requiresApproval
            });
        }
    }
}
