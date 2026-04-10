using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class ItemLookupRepository : RepositoryBase<ItemType>, IItemLookupRepository
{
    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<ItemType> _itemTypes;
    private readonly IMongoCollection<TrackingPolicy> _trackingPolicies;
    private readonly IMongoCollection<LifecycleState> _lifecycleStates;
    private readonly IMongoCollection<UnitOfMeasure> _unitOfMeasures;
    private readonly IMongoCollection<DosageForm> _dosageForms;

    private static readonly DosageForm[] DefaultDosageForms =
    [
        new() { Id = Guid.Parse("60000000-0000-0000-0000-000000000001"), Code = "TABLET", Name = "Tablet", SortOrder = 10 },
        new() { Id = Guid.Parse("60000000-0000-0000-0000-000000000002"), Code = "CAPSULE", Name = "Capsule", SortOrder = 20 },
        new() { Id = Guid.Parse("60000000-0000-0000-0000-000000000003"), Code = "INJECTION", Name = "Injection", SortOrder = 30 },
        new() { Id = Guid.Parse("60000000-0000-0000-0000-000000000004"), Code = "SYRUP", Name = "Syrup", SortOrder = 40 },
        new() { Id = Guid.Parse("60000000-0000-0000-0000-000000000005"), Code = "CREAM", Name = "Cream", SortOrder = 50 },
        new() { Id = Guid.Parse("60000000-0000-0000-0000-000000000006"), Code = "POWDER", Name = "Powder", SortOrder = 60 }
    ];

    private static readonly ItemType[] DefaultItemTypes =
    [
        new() { Id = Guid.Parse("20000000-0000-0000-0000-000000000001"), Code = "FINISHED_PRODUCT", Name = "Finished Product", SortOrder = 10 },
        new() { Id = Guid.Parse("20000000-0000-0000-0000-000000000002"), Code = "SEMI_FINISHED_PRODUCT", Name = "Semi Finished Product", SortOrder = 20 },
        new() { Id = Guid.Parse("20000000-0000-0000-0000-000000000003"), Code = "RAW_MATERIAL", Name = "Raw Material", SortOrder = 30 },
        new() { Id = Guid.Parse("20000000-0000-0000-0000-000000000004"), Code = "PACKAGING_MATERIAL", Name = "Packaging Material", SortOrder = 40 },
        new() { Id = Guid.Parse("20000000-0000-0000-0000-000000000005"), Code = "EXCIPIENT", Name = "Excipient", SortOrder = 50 },
        new() { Id = Guid.Parse("20000000-0000-0000-0000-000000000006"), Code = "ACTIVE_INGREDIENT", Name = "Active Ingredient", SortOrder = 60 },
        new() { Id = Guid.Parse("20000000-0000-0000-0000-000000000007"), Code = "SERVICE_ITEM", Name = "Service Item", SortOrder = 70 }
    ];

    private static readonly TrackingPolicy[] DefaultTrackingPolicies =
    [
        new() { Id = Guid.Parse("30000000-0000-0000-0000-000000000001"), Code = "NONE", Name = "None", SortOrder = 10 },
        new() { Id = Guid.Parse("30000000-0000-0000-0000-000000000002"), Code = "BATCH", Name = "Batch", SortOrder = 20 },
        new() { Id = Guid.Parse("30000000-0000-0000-0000-000000000003"), Code = "SERIAL", Name = "Serial", SortOrder = 30 },
        new() { Id = Guid.Parse("30000000-0000-0000-0000-000000000004"), Code = "BATCH_AND_EXPIRY", Name = "BatchAndExpiry", SortOrder = 40 }
    ];

    private static readonly LifecycleState[] DefaultLifecycleStates =
    [
        new() { Id = Guid.Parse("40000000-0000-0000-0000-000000000001"), Code = "DRAFT", Name = "Draft", SortOrder = 10 },
        new() { Id = Guid.Parse("40000000-0000-0000-0000-000000000002"), Code = "ACTIVE", Name = "Active", SortOrder = 20 },
        new() { Id = Guid.Parse("40000000-0000-0000-0000-000000000003"), Code = "BLOCKED", Name = "Blocked", SortOrder = 30 },
        new() { Id = Guid.Parse("40000000-0000-0000-0000-000000000004"), Code = "OBSOLETE", Name = "Obsolete", SortOrder = 40 }
    ];

    private static readonly UnitOfMeasure[] DefaultUnits =
    [
        new() { Id = Guid.Parse("50000000-0000-0000-0000-000000000001"), Code = "EA", Name = "EA", SortOrder = 10 },
        new() { Id = Guid.Parse("50000000-0000-0000-0000-000000000002"), Code = "KG", Name = "KG", SortOrder = 20 },
        new() { Id = Guid.Parse("50000000-0000-0000-0000-000000000003"), Code = "G", Name = "g", SortOrder = 30 },
        new() { Id = Guid.Parse("50000000-0000-0000-0000-000000000004"), Code = "L", Name = "L", SortOrder = 40 },
        new() { Id = Guid.Parse("50000000-0000-0000-0000-000000000005"), Code = "ML", Name = "ml", SortOrder = 50 },
        new() { Id = Guid.Parse("50000000-0000-0000-0000-000000000006"), Code = "BOX", Name = "BOX", SortOrder = 60 },
        new() { Id = Guid.Parse("50000000-0000-0000-0000-000000000007"), Code = "PACK", Name = "PACK", SortOrder = 70 },
        new() { Id = Guid.Parse("50000000-0000-0000-0000-000000000008"), Code = "SERVICE", Name = "SERVICE", SortOrder = 80 },
        new() { Id = Guid.Parse("50000000-0000-0000-0000-000000000009"), Code = "MG", Name = "mg", SortOrder = 90 },
        new() { Id = Guid.Parse("50000000-0000-0000-0000-00000000000A"), Code = "MCG", Name = "mcg", SortOrder = 100 },
        new() { Id = Guid.Parse("50000000-0000-0000-0000-00000000000B"), Code = "PERCENT", Name = "%", SortOrder = 110 },
        new() { Id = Guid.Parse("50000000-0000-0000-0000-00000000000C"), Code = "IU", Name = "IU", SortOrder = 120 }
    ];

    public ItemLookupRepository(IMongoDatabase database, ITenantContext tenantContext)
        : base(database, tenantContext, "item_types")
    {
        _database = database;
        _itemTypes = database.GetCollection<ItemType>("item_types");
        _trackingPolicies = database.GetCollection<TrackingPolicy>("tracking_policies");
        _lifecycleStates = database.GetCollection<LifecycleState>("lifecycle_states");
        _unitOfMeasures = database.GetCollection<UnitOfMeasure>("unit_of_measures");
        _dosageForms = database.GetCollection<DosageForm>("dosage_forms");
    }

    public async Task EnsureSeedDataAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLookupSeedAsync(_itemTypes, DefaultItemTypes, cancellationToken);
        await EnsureLookupSeedAsync(_trackingPolicies, DefaultTrackingPolicies, cancellationToken);
        await EnsureLookupSeedAsync(_lifecycleStates, DefaultLifecycleStates, cancellationToken);
        await EnsureLookupSeedAsync(_unitOfMeasures, DefaultUnits, cancellationToken);
        await EnsureLookupSeedAsync(_dosageForms, DefaultDosageForms, cancellationToken);

        var collections = await _database.ListCollectionNames().ToListAsync(cancellationToken);
        if (!collections.Contains("uom_conversions"))
        {
            await _database.CreateCollectionAsync("uom_conversions", cancellationToken: cancellationToken);
        }
    }

    public async Task<IReadOnlyList<ItemType>> GetItemTypesAsync(CancellationToken cancellationToken = default)
    {
        return await _itemTypes.Find(TenantFilterFor<ItemType>()).SortBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TrackingPolicy>> GetTrackingPoliciesAsync(CancellationToken cancellationToken = default)
    {
        return await _trackingPolicies.Find(TenantFilterFor<TrackingPolicy>()).SortBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LifecycleState>> GetLifecycleStatesAsync(CancellationToken cancellationToken = default)
    {
        return await _lifecycleStates.Find(TenantFilterFor<LifecycleState>()).SortBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UnitOfMeasure>> GetUnitOfMeasuresAsync(CancellationToken cancellationToken = default)
    {
        return await _unitOfMeasures.Find(TenantFilterFor<UnitOfMeasure>()).SortBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task<ItemType?> GetItemTypeByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _itemTypes.Find(ByIdFilter<ItemType>(id)).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<TrackingPolicy?> GetTrackingPolicyByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _trackingPolicies.Find(ByIdFilter<TrackingPolicy>(id)).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<LifecycleState?> GetLifecycleStateByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _lifecycleStates.Find(ByIdFilter<LifecycleState>(id)).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<LifecycleState?> GetLifecycleStateByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var filter = Builders<LifecycleState>.Filter.And(
            TenantFilterFor<LifecycleState>(),
            Builders<LifecycleState>.Filter.Eq(x => x.Code, code));
        return await _lifecycleStates.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<UnitOfMeasure?> GetUnitOfMeasureByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _unitOfMeasures.Find(ByIdFilter<UnitOfMeasure>(id)).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DosageForm>> GetDosageFormsAsync(CancellationToken cancellationToken = default)
    {
        return await _dosageForms.Find(TenantFilterFor<DosageForm>()).SortBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task<DosageForm?> GetDosageFormByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dosageForms.Find(ByIdFilter<DosageForm>(id)).FirstOrDefaultAsync(cancellationToken);
    }

    private async Task EnsureLookupSeedAsync<TEntity>(
        IMongoCollection<TEntity> collection,
        IEnumerable<TEntity> items,
        CancellationToken cancellationToken)
        where TEntity : LookupEntityBase
    {
        foreach (var item in items)
        {
            item.TenantId = TenantContext.TenantId;
            item.IsDeleted = false;

            var filter = Builders<TEntity>.Filter.And(
                Builders<TEntity>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
                Builders<TEntity>.Filter.Eq(x => x.Code, item.Code));

            await collection.ReplaceOneAsync(filter, item, new ReplaceOptions { IsUpsert = true }, cancellationToken);
        }
    }

    private FilterDefinition<TEntity> TenantFilterFor<TEntity>() where TEntity : EntityBase
    {
        return Builders<TEntity>.Filter.And(
            Builders<TEntity>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<TEntity>.Filter.Eq(x => x.IsDeleted, false));
    }

    private FilterDefinition<TEntity> ByIdFilter<TEntity>(Guid id) where TEntity : EntityBase
    {
        return Builders<TEntity>.Filter.And(
            TenantFilterFor<TEntity>(),
            Builders<TEntity>.Filter.Eq(x => x.Id, id));
    }
}
