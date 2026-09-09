using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Seed;

/// <summary>
/// Idempotent demo legal-entity hierarchy for the default tenant: a root holding with two subsidiaries.
/// Additive foundation step for legal-entity scoping. Runs at startup and is a no-op once seeded.
/// </summary>
public static class LegalEntityHierarchySeed
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private const string CollectionName = "mdm_legal_entities";

    // Deterministic demo ids so auth (F2) and all services can reference the hierarchy by fixed GUID.
    private static readonly Guid HoldingId = Guid.Parse("1e9a1000-0000-0000-0000-000000000001");
    private static readonly Guid MedikalId = Guid.Parse("1e9a1000-0000-0000-0000-000000000002");
    private static readonly Guid TeknolojiId = Guid.Parse("1e9a1000-0000-0000-0000-000000000003");

    public static async Task EnsureSeededAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        var collection = database.GetCollection<LegalEntity>(CollectionName);

        // Idempotent + self-healing: once the fixed-id root exists we are done. Otherwise, remove any
        // pre-existing GRAND-* rows (e.g. earlier random-id seed) for the default tenant and insert the
        // deterministic set. Hierarchy/guard code is untouched — only the seed ids are pinned.
        var fixedRootExists = await collection.Find(Builders<LegalEntity>.Filter.And(
            Builders<LegalEntity>.Filter.Eq(x => x.TenantId, DefaultTenantId),
            Builders<LegalEntity>.Filter.Eq(x => x.Id, HoldingId))).AnyAsync(cancellationToken);

        if (fixedRootExists)
        {
            return; // already seeded with deterministic ids — no-op
        }

        var demoCodes = new[] { "GRAND-HOLDING", "GRAND-MEDIKAL", "GRAND-TEKNOLOJI" };
        await collection.DeleteManyAsync(Builders<LegalEntity>.Filter.And(
            Builders<LegalEntity>.Filter.Eq(x => x.TenantId, DefaultTenantId),
            Builders<LegalEntity>.Filter.In(x => x.Code, demoCodes)), cancellationToken);

        var now = DateTimeOffset.UtcNow;

        var holding = new LegalEntity
        {
            Id = HoldingId,
            TenantId = DefaultTenantId,
            Code = "GRAND-HOLDING",
            LegalName = "GRAND HOLDING",
            DisplayName = "GRAND HOLDING",
            ParentId = null,
            LifecycleStatus = LegalEntityLifecycleStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false
        };

        var medikal = new LegalEntity
        {
            Id = MedikalId,
            TenantId = DefaultTenantId,
            Code = "GRAND-MEDIKAL",
            LegalName = "GRAND MEDIKAL",
            DisplayName = "GRAND MEDIKAL",
            ParentId = HoldingId,
            LifecycleStatus = LegalEntityLifecycleStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false
        };

        var teknoloji = new LegalEntity
        {
            Id = TeknolojiId,
            TenantId = DefaultTenantId,
            Code = "GRAND-TEKNOLOJI",
            LegalName = "GRAND TEKNOLOJİ",
            DisplayName = "GRAND TEKNOLOJİ",
            ParentId = HoldingId,
            LifecycleStatus = LegalEntityLifecycleStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false
        };

        await collection.InsertManyAsync(new[] { holding, medikal, teknoloji }, cancellationToken: cancellationToken);
    }
}
