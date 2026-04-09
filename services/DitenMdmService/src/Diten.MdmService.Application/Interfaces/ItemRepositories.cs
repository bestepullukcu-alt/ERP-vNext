using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Application.Interfaces;

public interface IItemRepository
{
    Task<Item> CreateAsync(Item entity, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Item entity, CancellationToken cancellationToken = default);
    Task<Item?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Item>> GetAllAsync(CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> BulkDeleteAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task ReplaceAttributeValuesAsync(Guid itemId, IEnumerable<ItemAttributeValue> values, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ItemAttributeValue>> GetAttributeValuesAsync(Guid itemId, CancellationToken cancellationToken = default);
    Task ReplaceVariantsAsync(Guid itemId, IEnumerable<ItemVariant> variants, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ItemVariant>> GetVariantsAsync(Guid itemId, CancellationToken cancellationToken = default);
}

public interface IItemCategoryRepository
{
    Task<ItemCategory> CreateAsync(ItemCategory entity, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(ItemCategory entity, CancellationToken cancellationToken = default);
    Task<ItemCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ItemCategory>> GetAllAsync(CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> BulkDeleteAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task<bool> WouldCreateCycleAsync(Guid categoryId, Guid? parentCategoryId, CancellationToken cancellationToken = default);
}

public interface IItemVariantModelRepository
{
    Task<ItemVariantModel> CreateAsync(ItemVariantModel entity, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(ItemVariantModel entity, CancellationToken cancellationToken = default);
    Task<ItemVariantModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ItemVariantModel>> GetAllAsync(CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> BulkDeleteAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task ReplaceTemplatesAsync(
        Guid variantModelId,
        IEnumerable<AttributeDefinition> definitions,
        IEnumerable<AttributeTemplate> templates,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AttributeTemplate>> GetTemplatesAsync(Guid variantModelId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AttributeDefinition>> GetAttributeDefinitionsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AttributeDefinition>> GetAllAttributeDefinitionsAsync(CancellationToken cancellationToken = default);
}

public interface IItemLookupRepository
{
    Task EnsureSeedDataAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ItemType>> GetItemTypesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrackingPolicy>> GetTrackingPoliciesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LifecycleState>> GetLifecycleStatesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UnitOfMeasure>> GetUnitOfMeasuresAsync(CancellationToken cancellationToken = default);
    Task<ItemType?> GetItemTypeByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TrackingPolicy?> GetTrackingPolicyByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<LifecycleState?> GetLifecycleStateByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<LifecycleState?> GetLifecycleStateByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<UnitOfMeasure?> GetUnitOfMeasureByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
