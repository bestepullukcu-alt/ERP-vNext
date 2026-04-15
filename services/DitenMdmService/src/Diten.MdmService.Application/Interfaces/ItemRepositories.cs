using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Application.Interfaces;

/// <summary>
/// Repository for Item operations.
/// All queries automatically filter by TenantId and IsDeleted=false.
/// </summary>
public interface IItemRepository : IRepository<Item>
{
    // Item-specific methods only — standard CRUD inherited from IRepository<Item>
    Task<int> BulkDeleteAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default);
    Task ReplaceAttributeValuesAsync(Guid itemId, IEnumerable<ItemAttributeValue> values, CancellationToken ct = default);
    Task<IReadOnlyList<ItemAttributeValue>> GetAttributeValuesAsync(Guid itemId, CancellationToken ct = default);
    Task ReplaceVariantsAsync(Guid itemId, IEnumerable<ItemVariant> variants, CancellationToken ct = default);
    Task<IReadOnlyList<ItemVariant>> GetVariantsAsync(Guid itemId, CancellationToken ct = default);
}

/// <summary>
/// Repository for ItemCategory operations.
/// All queries automatically filter by TenantId and IsDeleted=false.
/// </summary>
public interface IItemCategoryRepository : IRepository<ItemCategory>
{
    // ItemCategory-specific methods only — standard CRUD inherited from IRepository<ItemCategory>
    Task<int> BulkDeleteAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default);
    Task<bool> WouldCreateCycleAsync(Guid categoryId, Guid? parentCategoryId, CancellationToken ct = default);
}

/// <summary>
/// Repository for ItemVariantModel operations.
/// All queries automatically filter by TenantId and IsDeleted=false.
/// </summary>
public interface IItemVariantModelRepository : IRepository<ItemVariantModel>
{
    // ItemVariantModel-specific methods only — standard CRUD inherited from IRepository<ItemVariantModel>
    Task<int> BulkDeleteAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default);
    Task ReplaceTemplatesAsync(
        Guid variantModelId,
        IEnumerable<AttributeDefinition> definitions,
        IEnumerable<AttributeTemplate> templates,
        CancellationToken ct = default);
    Task<IReadOnlyList<AttributeTemplate>> GetTemplatesAsync(Guid variantModelId, CancellationToken ct = default);
    Task<IReadOnlyList<AttributeDefinition>> GetAttributeDefinitionsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<IReadOnlyList<AttributeDefinition>> GetAllAttributeDefinitionsAsync(CancellationToken ct = default);
}

/// <summary>
/// Repository for lookup/reference data (ItemType, LifecycleState, TrackingPolicy, etc.).
/// This is a composite lookup repository and does NOT extend IRepository&lt;T&gt; since it
/// serves multiple lookup entity types.
/// </summary>
public interface IItemLookupRepository
{
    Task EnsureSeedDataAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ItemType>> GetItemTypesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TrackingPolicy>> GetTrackingPoliciesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<LifecycleState>> GetLifecycleStatesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<UnitOfMeasure>> GetUnitOfMeasuresAsync(CancellationToken ct = default);
    Task<ItemType?> GetItemTypeByIdAsync(Guid id, CancellationToken ct = default);
    Task<TrackingPolicy?> GetTrackingPolicyByIdAsync(Guid id, CancellationToken ct = default);
    Task<LifecycleState?> GetLifecycleStateByIdAsync(Guid id, CancellationToken ct = default);
    Task<LifecycleState?> GetLifecycleStateByCodeAsync(string code, CancellationToken ct = default);
    Task<UnitOfMeasure?> GetUnitOfMeasureByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DosageForm>> GetDosageFormsAsync(CancellationToken ct = default);
    Task<DosageForm?> GetDosageFormByIdAsync(Guid id, CancellationToken ct = default);
}
