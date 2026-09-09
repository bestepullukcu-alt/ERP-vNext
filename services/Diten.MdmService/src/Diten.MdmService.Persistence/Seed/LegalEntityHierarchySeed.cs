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

    private const string RootCode = "GRAND-HOLDING";

    public static async Task EnsureSeededAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        var collection = database.GetCollection<LegalEntity>(CollectionName);

        var existsFilter = Builders<LegalEntity>.Filter.And(
            Builders<LegalEntity>.Filter.Eq(x => x.TenantId, DefaultTenantId),
            Builders<LegalEntity>.Filter.Eq(x => x.Code, RootCode),
            Builders<LegalEntity>.Filter.Eq(x => x.IsDeleted, false));

        if (await collection.Find(existsFilter).AnyAsync(cancellationToken))
        {
            return; // already seeded — idempotent no-op
        }

        var now = DateTimeOffset.UtcNow;

        var holding = new LegalEntity
        {
            Id = Guid.NewGuid(),
            TenantId = DefaultTenantId,
            Code = RootCode,
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
            Id = Guid.NewGuid(),
            TenantId = DefaultTenantId,
            Code = "GRAND-MEDIKAL",
            LegalName = "GRAND MEDIKAL",
            DisplayName = "GRAND MEDIKAL",
            ParentId = holding.Id,
            LifecycleStatus = LegalEntityLifecycleStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false
        };

        var teknoloji = new LegalEntity
        {
            Id = Guid.NewGuid(),
            TenantId = DefaultTenantId,
            Code = "GRAND-TEKNOLOJI",
            LegalName = "GRAND TEKNOLOJİ",
            DisplayName = "GRAND TEKNOLOJİ",
            ParentId = holding.Id,
            LifecycleStatus = LegalEntityLifecycleStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false
        };

        await collection.InsertManyAsync(new[] { holding, medikal, teknoloji }, cancellationToken: cancellationToken);
    }
}
