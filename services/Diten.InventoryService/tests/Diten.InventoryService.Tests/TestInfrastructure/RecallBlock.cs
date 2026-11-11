using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>
/// MOD-0173-FU03 AC-6 / AC-15: an ACTIVE recall exit block on one lot, written as the MOD-0177 document the production
/// <c>IRecallLotStatusReader</c> reads (tenant-wide, CT-R7) — the reservation rule is measured, not the recall workflow. The document carries
/// the test tenant, so the factory's tenant clean-up would not reach it (recall_* is not a ledger collection): <see cref="RemoveAsync"/> does.
/// </summary>
public static class RecallBlock
{
    public static async Task<Guid> AddAsync(InventoryApiFactory factory, Guid tenant, Guid legalEntity, string lotId)
    {
        var id = Guid.NewGuid();
        await factory.Collection("recall_lot_blocks").InsertOneAsync(new BsonDocument
        {
            { "_id", id.ToString() }, { "TenantId", tenant.ToString() }, { "LegalEntityId", legalEntity.ToString() }, { "IsDeleted", false },
            { "LotId", lotId }, { "CaseId", Guid.NewGuid().ToString() }, { "CaseNumber", "RC-TEST-0001" }, { "Revision", 1 }, { "Active", true },
            { "ActivatedAtUtc", DateTime.UtcNow }, { "CreatedAt", DateTime.UtcNow }, { "Version", 1 }
        });
        return id;
    }

    public static Task RemoveAsync(InventoryApiFactory factory, Guid tenant)
        => factory.Collection("recall_lot_blocks").DeleteManyAsync(new BsonDocument("TenantId", tenant.ToString()));
}
