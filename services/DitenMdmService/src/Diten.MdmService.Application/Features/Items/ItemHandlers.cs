using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MediatR;

namespace Diten.MdmService.Application.Features.Items;

public sealed class GetAllItemsQueryHandler : IRequestHandler<GetAllItemsQuery, IReadOnlyList<ItemListItemDto>>
{
    private readonly IItemRepository _itemRepository;
    private readonly IItemCategoryRepository _categoryRepository;
    private readonly IItemVariantModelRepository _variantModelRepository;
    private readonly IItemLookupRepository _lookupRepository;

    public GetAllItemsQueryHandler(
        IItemRepository itemRepository,
        IItemCategoryRepository categoryRepository,
        IItemVariantModelRepository variantModelRepository,
        IItemLookupRepository lookupRepository)
    {
        _itemRepository = itemRepository;
        _categoryRepository = categoryRepository;
        _variantModelRepository = variantModelRepository;
        _lookupRepository = lookupRepository;
    }

    public async Task<IReadOnlyList<ItemListItemDto>> Handle(GetAllItemsQuery request, CancellationToken cancellationToken)
    {
        await _lookupRepository.EnsureSeedDataAsync(cancellationToken);

        var items = await _itemRepository.GetAllAsync(cancellationToken);
        var categories = await _categoryRepository.GetAllAsync(cancellationToken);
        var variantModels = await _variantModelRepository.GetAllAsync(cancellationToken);
        var itemTypes = await _lookupRepository.GetItemTypesAsync(cancellationToken);
        var trackingPolicies = await _lookupRepository.GetTrackingPoliciesAsync(cancellationToken);
        var lifecycleStates = await _lookupRepository.GetLifecycleStatesAsync(cancellationToken);
        var uoms = await _lookupRepository.GetUnitOfMeasuresAsync(cancellationToken);

        var categoryMap = categories.ToDictionary(x => x.Id);
        var variantModelMap = variantModels.ToDictionary(x => x.Id);
        var itemTypeMap = itemTypes.ToDictionary(x => x.Id);
        var trackingMap = trackingPolicies.ToDictionary(x => x.Id);
        var lifecycleMap = lifecycleStates.ToDictionary(x => x.Id);
        var uomMap = uoms.ToDictionary(x => x.Id);

        return items
            .Select(item => ItemMapping.ToListDto(item, categoryMap, variantModelMap, itemTypeMap, trackingMap, lifecycleMap, uomMap))
            .ToList();
    }
}

public sealed class GetItemByIdQueryHandler : IRequestHandler<GetItemByIdQuery, ItemDetailDto?>
{
    private readonly IItemRepository _itemRepository;
    private readonly IItemCategoryRepository _categoryRepository;
    private readonly IItemVariantModelRepository _variantModelRepository;
    private readonly IItemLookupRepository _lookupRepository;

    public GetItemByIdQueryHandler(
        IItemRepository itemRepository,
        IItemCategoryRepository categoryRepository,
        IItemVariantModelRepository variantModelRepository,
        IItemLookupRepository lookupRepository)
    {
        _itemRepository = itemRepository;
        _categoryRepository = categoryRepository;
        _variantModelRepository = variantModelRepository;
        _lookupRepository = lookupRepository;
    }

    public async Task<ItemDetailDto?> Handle(GetItemByIdQuery request, CancellationToken cancellationToken)
    {
        await _lookupRepository.EnsureSeedDataAsync(cancellationToken);

        var item = await _itemRepository.GetByIdAsync(request.Id, cancellationToken);
        if (item is null)
        {
            return null;
        }

        var categories = await _categoryRepository.GetAllAsync(cancellationToken);
        var variantModels = await _variantModelRepository.GetAllAsync(cancellationToken);
        var itemTypes = await _lookupRepository.GetItemTypesAsync(cancellationToken);
        var trackingPolicies = await _lookupRepository.GetTrackingPoliciesAsync(cancellationToken);
        var lifecycleStates = await _lookupRepository.GetLifecycleStatesAsync(cancellationToken);
        var uoms = await _lookupRepository.GetUnitOfMeasuresAsync(cancellationToken);
        var attributeValues = await _itemRepository.GetAttributeValuesAsync(item.Id, cancellationToken);
        var variants = await _itemRepository.GetVariantsAsync(item.Id, cancellationToken);

        var categoryMap = categories.ToDictionary(x => x.Id);
        var variantModelMap = variantModels.ToDictionary(x => x.Id);
        var itemTypeMap = itemTypes.ToDictionary(x => x.Id);
        var trackingMap = trackingPolicies.ToDictionary(x => x.Id);
        var lifecycleMap = lifecycleStates.ToDictionary(x => x.Id);
        var uomMap = uoms.ToDictionary(x => x.Id);

        var templates = item.VariantModelId.HasValue
            ? await _variantModelRepository.GetTemplatesAsync(item.VariantModelId.Value, cancellationToken)
            : [];

        var definitionIds = templates.Select(x => x.AttributeDefinitionId)
            .Concat(attributeValues.Select(x => x.AttributeDefinitionId))
            .Concat(variants.SelectMany(x => x.AttributeValues.Select(v => v.AttributeDefinitionId)))
            .Distinct()
            .ToList();

        var definitions = await _variantModelRepository.GetAttributeDefinitionsAsync(definitionIds, cancellationToken);
        var definitionMap = definitions.ToDictionary(x => x.Id);

        return ItemMapping.ToDetailDto(
            item,
            attributeValues,
            variants,
            templates,
            definitionMap,
            categoryMap,
            variantModelMap,
            itemTypeMap,
            trackingMap,
            lifecycleMap,
            uomMap);
    }
}

public sealed class CreateItemRequestHandler : IRequestHandler<CreateItemRequest, Guid>
{
    private readonly IItemRepository _itemRepository;
    private readonly IItemCategoryRepository _categoryRepository;
    private readonly IItemVariantModelRepository _variantModelRepository;
    private readonly IItemLookupRepository _lookupRepository;

    public CreateItemRequestHandler(
        IItemRepository itemRepository,
        IItemCategoryRepository categoryRepository,
        IItemVariantModelRepository variantModelRepository,
        IItemLookupRepository lookupRepository)
    {
        _itemRepository = itemRepository;
        _categoryRepository = categoryRepository;
        _variantModelRepository = variantModelRepository;
        _lookupRepository = lookupRepository;
    }

    public async Task<Guid> Handle(CreateItemRequest request, CancellationToken cancellationToken)
    {
        await _lookupRepository.EnsureSeedDataAsync(cancellationToken);
        await ItemValidation.ValidateAsync(request, null, _itemRepository, _categoryRepository, _variantModelRepository, _lookupRepository, cancellationToken);

        var entity = ItemValidation.ToEntity(request, null);
        var created = await _itemRepository.CreateAsync(entity, cancellationToken);
        await ItemValidation.SaveChildrenAsync(created.Id, request, _itemRepository, cancellationToken);
        return created.Id;
    }
}

public sealed class UpdateItemRequestHandler : IRequestHandler<UpdateItemRequest, bool>
{
    private readonly IItemRepository _itemRepository;
    private readonly IItemCategoryRepository _categoryRepository;
    private readonly IItemVariantModelRepository _variantModelRepository;
    private readonly IItemLookupRepository _lookupRepository;

    public UpdateItemRequestHandler(
        IItemRepository itemRepository,
        IItemCategoryRepository categoryRepository,
        IItemVariantModelRepository variantModelRepository,
        IItemLookupRepository lookupRepository)
    {
        _itemRepository = itemRepository;
        _categoryRepository = categoryRepository;
        _variantModelRepository = variantModelRepository;
        _lookupRepository = lookupRepository;
    }

    public async Task<bool> Handle(UpdateItemRequest request, CancellationToken cancellationToken)
    {
        await _lookupRepository.EnsureSeedDataAsync(cancellationToken);

        var existing = await _itemRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Item not found.");

        await ItemValidation.ValidateAsync(request, request.Id, _itemRepository, _categoryRepository, _variantModelRepository, _lookupRepository, cancellationToken);

        var entity = ItemValidation.ToEntity(request, existing);
        var updated = await _itemRepository.UpdateAsync(entity, cancellationToken);
        if (!updated)
        {
            return false;
        }

        await ItemValidation.SaveChildrenAsync(entity.Id, request, _itemRepository, cancellationToken);
        return true;
    }
}

public sealed class PatchItemStatusRequestHandler : IRequestHandler<PatchItemStatusRequest, bool>
{
    private readonly IItemRepository _itemRepository;

    public PatchItemStatusRequestHandler(IItemRepository itemRepository)
    {
        _itemRepository = itemRepository;
    }

    public async Task<bool> Handle(PatchItemStatusRequest request, CancellationToken cancellationToken)
    {
        var existing = await _itemRepository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        existing.IsActive = request.IsActive;
        return await _itemRepository.UpdateAsync(existing, cancellationToken);
    }
}

public sealed class DeleteItemRequestHandler : IRequestHandler<DeleteItemRequest, bool>
{
    private readonly IItemRepository _itemRepository;

    public DeleteItemRequestHandler(IItemRepository itemRepository)
    {
        _itemRepository = itemRepository;
    }

    public async Task<bool> Handle(DeleteItemRequest request, CancellationToken cancellationToken)
    {
        await _itemRepository.DeleteAsync(request.Id, cancellationToken);
        return true;
    }
}

public sealed class BulkDeleteItemsRequestHandler : IRequestHandler<BulkDeleteItemsRequest, BulkDeleteItemsResponse>
{
    private readonly IItemRepository _itemRepository;

    public BulkDeleteItemsRequestHandler(IItemRepository itemRepository)
    {
        _itemRepository = itemRepository;
    }

    public async Task<BulkDeleteItemsResponse> Handle(BulkDeleteItemsRequest request, CancellationToken cancellationToken)
    {
        var deleted = await _itemRepository.BulkDeleteAsync(request.Ids, cancellationToken);
        return new BulkDeleteItemsResponse { DeletedCount = deleted };
    }
}

internal static class ItemValidation
{
    public static async Task ValidateAsync(
        ItemUpsertRequestBase request,
        Guid? excludeId,
        IItemRepository itemRepository,
        IItemCategoryRepository categoryRepository,
        IItemVariantModelRepository variantModelRepository,
        IItemLookupRepository lookupRepository,
        CancellationToken cancellationToken)
    {
        if (await itemRepository.ExistsByCodeAsync(request.Code.Trim(), excludeId, cancellationToken))
        {
            throw new InvalidOperationException("Item code must be unique within the tenant.");
        }

        var category = await categoryRepository.GetByIdAsync(request.CategoryId, cancellationToken)
            ?? throw new KeyNotFoundException("Category not found.");
        var itemType = await lookupRepository.GetItemTypeByIdAsync(request.ItemTypeId, cancellationToken)
            ?? throw new KeyNotFoundException("Item type not found.");
        _ = await lookupRepository.GetUnitOfMeasureByIdAsync(request.BaseUomId, cancellationToken)
            ?? throw new KeyNotFoundException("Base UOM not found.");
        _ = await lookupRepository.GetTrackingPolicyByIdAsync(request.TrackingPolicyId, cancellationToken)
            ?? throw new KeyNotFoundException("Tracking policy not found.");
        _ = await lookupRepository.GetLifecycleStateByIdAsync(request.LifecycleStateId, cancellationToken)
            ?? throw new KeyNotFoundException("Lifecycle state not found.");

        if (category.ItemTypeId != request.ItemTypeId)
        {
            throw new InvalidOperationException("Selected category must belong to the same item type.");
        }

        var isServiceType = string.Equals(itemType.Code, "SERVICE_ITEM", StringComparison.OrdinalIgnoreCase);
        if (request.ServiceItem != isServiceType)
        {
            throw new InvalidOperationException("Service item flag must match the selected item type.");
        }

        if (request.VariantModelId.HasValue)
        {
            var variantModel = await variantModelRepository.GetByIdAsync(request.VariantModelId.Value, cancellationToken)
                ?? throw new KeyNotFoundException("Variant model not found.");
            if (variantModel.ItemTypeId != request.ItemTypeId)
            {
                throw new InvalidOperationException("Variant model must belong to the same item type.");
            }

            await ValidateVariantPayloadAsync(request, variantModelRepository, cancellationToken);
        }
        else if (request.AttributeValues.Count > 0 || request.Variants.Count > 0)
        {
            throw new InvalidOperationException("Attribute values and variants require a selected variant model.");
        }
    }

    public static Item ToEntity(ItemUpsertRequestBase request, Item? existing)
    {
        var entity = existing ?? new Item();
        entity.Code = request.Code.Trim();
        entity.Name = request.Name.Trim();
        entity.ShortDescription = request.ShortDescription?.Trim();
        entity.ItemTypeId = request.ItemTypeId;
        entity.CategoryId = request.CategoryId;
        entity.BaseUomId = request.BaseUomId;
        entity.Stockable = request.Stockable;
        entity.Purchasable = request.Purchasable;
        entity.Sellable = request.Sellable;
        entity.ServiceItem = request.ServiceItem;
        entity.TrackingPolicyId = request.TrackingPolicyId;
        entity.LifecycleStateId = request.LifecycleStateId;
        entity.IsActive = request.IsActive;
        entity.VariantModelId = request.VariantModelId;
        return entity;
    }

    public static async Task SaveChildrenAsync(
        Guid itemId,
        ItemUpsertRequestBase request,
        IItemRepository itemRepository,
        CancellationToken cancellationToken)
    {
        var attributeValues = request.AttributeValues
            .Select(x => new ItemAttributeValue
            {
                ItemId = itemId,
                AttributeDefinitionId = x.AttributeDefinitionId,
                Value = x.Value.Trim()
            })
            .ToList();

        var variants = request.Variants
            .Select(x => new ItemVariant
            {
                ItemId = itemId,
                Code = x.Code.Trim(),
                Name = x.Name.Trim(),
                IsActive = x.IsActive,
                AttributeValues = x.AttributeValues
                    .Select(value => new ItemVariantAttributeValue
                    {
                        AttributeDefinitionId = value.AttributeDefinitionId,
                        Value = value.Value.Trim()
                    })
                    .ToList()
            })
            .ToList();

        await itemRepository.ReplaceAttributeValuesAsync(itemId, attributeValues, cancellationToken);
        await itemRepository.ReplaceVariantsAsync(itemId, variants, cancellationToken);
    }

    private static async Task ValidateVariantPayloadAsync(
        ItemUpsertRequestBase request,
        IItemVariantModelRepository variantModelRepository,
        CancellationToken cancellationToken)
    {
        if (!request.VariantModelId.HasValue)
        {
            return;
        }

        var templates = await variantModelRepository.GetTemplatesAsync(request.VariantModelId.Value, cancellationToken);
        var definitions = await variantModelRepository.GetAttributeDefinitionsAsync(templates.Select(x => x.AttributeDefinitionId), cancellationToken);
        var definitionMap = definitions.ToDictionary(x => x.Id);
        var templateMap = templates.ToDictionary(x => x.AttributeDefinitionId);

        if (templateMap.Count == 0 && (request.AttributeValues.Count > 0 || request.Variants.Count > 0))
        {
            throw new InvalidOperationException("Selected variant model does not define any attributes.");
        }

        var requestAttributeIds = request.AttributeValues.Select(x => x.AttributeDefinitionId).ToHashSet();
        if (requestAttributeIds.Any(id => !templateMap.TryGetValue(id, out var template) || template.IsVariantAxis))
        {
            throw new InvalidOperationException("Item attribute values must belong to the selected model and cannot use variant-only attributes.");
        }

        foreach (var template in templates.Where(x => x.IsRequired && !x.IsVariantAxis))
        {
            if (!requestAttributeIds.Contains(template.AttributeDefinitionId))
            {
                var name = definitionMap.TryGetValue(template.AttributeDefinitionId, out var definition) ? definition.Name : template.AttributeDefinitionId.ToString();
                throw new InvalidOperationException($"Required item attribute is missing: {name}.");
            }
        }

        var variantAxisIds = templates.Where(x => x.IsVariantAxis).Select(x => x.AttributeDefinitionId).ToHashSet();
        if (variantAxisIds.Count > 0 && request.Variants.Count == 0)
        {
            throw new InvalidOperationException("Selected variant model requires at least one variant.");
        }

        foreach (var variant in request.Variants)
        {
            var ids = variant.AttributeValues.Select(x => x.AttributeDefinitionId).ToHashSet();
            if (ids.Any(id => !variantAxisIds.Contains(id)))
            {
                throw new InvalidOperationException("Variant attributes must belong to the selected model's variant axes.");
            }

            foreach (var requiredId in templates.Where(x => x.IsRequired && x.IsVariantAxis).Select(x => x.AttributeDefinitionId))
            {
                if (!ids.Contains(requiredId))
                {
                    var name = definitionMap.TryGetValue(requiredId, out var definition) ? definition.Name : requiredId.ToString();
                    throw new InvalidOperationException($"Required variant attribute is missing: {name}.");
                }
            }
        }
    }
}

internal static class ItemMapping
{
    public static ItemListItemDto ToListDto(
        Item item,
        IReadOnlyDictionary<Guid, ItemCategory> categories,
        IReadOnlyDictionary<Guid, ItemVariantModel> variantModels,
        IReadOnlyDictionary<Guid, ItemType> itemTypes,
        IReadOnlyDictionary<Guid, TrackingPolicy> trackingPolicies,
        IReadOnlyDictionary<Guid, LifecycleState> lifecycleStates,
        IReadOnlyDictionary<Guid, UnitOfMeasure> uoms)
    {
        categories.TryGetValue(item.CategoryId, out var category);
        itemTypes.TryGetValue(item.ItemTypeId, out var itemType);
        trackingPolicies.TryGetValue(item.TrackingPolicyId, out var trackingPolicy);
        lifecycleStates.TryGetValue(item.LifecycleStateId, out var lifecycleState);
        uoms.TryGetValue(item.BaseUomId, out var uom);
        var variantModel = item.VariantModelId.HasValue && variantModels.TryGetValue(item.VariantModelId.Value, out var model) ? model : null;

        return new ItemListItemDto
        {
            Id = item.Id,
            Code = item.Code,
            Name = item.Name,
            ItemTypeId = item.ItemTypeId,
            ItemType = itemType?.Name ?? string.Empty,
            CategoryId = item.CategoryId,
            Category = category?.Name ?? string.Empty,
            BaseUomId = item.BaseUomId,
            BaseUom = uom?.Name ?? string.Empty,
            TrackingPolicyId = item.TrackingPolicyId,
            TrackingPolicy = trackingPolicy?.Name ?? string.Empty,
            LifecycleStateId = item.LifecycleStateId,
            LifecycleState = lifecycleState?.Name ?? string.Empty,
            VariantModelId = item.VariantModelId,
            VariantModel = variantModel?.Name,
            Stockable = item.Stockable,
            Purchasable = item.Purchasable,
            Sellable = item.Sellable,
            ServiceItem = item.ServiceItem,
            IsActive = item.IsActive
        };
    }

    public static ItemDetailDto ToDetailDto(
        Item item,
        IReadOnlyList<ItemAttributeValue> attributeValues,
        IReadOnlyList<ItemVariant> variants,
        IReadOnlyList<AttributeTemplate> templates,
        IReadOnlyDictionary<Guid, AttributeDefinition> definitions,
        IReadOnlyDictionary<Guid, ItemCategory> categories,
        IReadOnlyDictionary<Guid, ItemVariantModel> variantModels,
        IReadOnlyDictionary<Guid, ItemType> itemTypes,
        IReadOnlyDictionary<Guid, TrackingPolicy> trackingPolicies,
        IReadOnlyDictionary<Guid, LifecycleState> lifecycleStates,
        IReadOnlyDictionary<Guid, UnitOfMeasure> uoms)
    {
        var dto = new ItemDetailDto
        {
            ShortDescription = item.ShortDescription
        };

        var baseDto = ToListDto(item, categories, variantModels, itemTypes, trackingPolicies, lifecycleStates, uoms);
        dto.Id = baseDto.Id;
        dto.Code = baseDto.Code;
        dto.Name = baseDto.Name;
        dto.ItemTypeId = baseDto.ItemTypeId;
        dto.ItemType = baseDto.ItemType;
        dto.CategoryId = baseDto.CategoryId;
        dto.Category = baseDto.Category;
        dto.BaseUomId = baseDto.BaseUomId;
        dto.BaseUom = baseDto.BaseUom;
        dto.TrackingPolicyId = baseDto.TrackingPolicyId;
        dto.TrackingPolicy = baseDto.TrackingPolicy;
        dto.LifecycleStateId = baseDto.LifecycleStateId;
        dto.LifecycleState = baseDto.LifecycleState;
        dto.VariantModelId = baseDto.VariantModelId;
        dto.VariantModel = baseDto.VariantModel;
        dto.Stockable = baseDto.Stockable;
        dto.Purchasable = baseDto.Purchasable;
        dto.Sellable = baseDto.Sellable;
        dto.ServiceItem = baseDto.ServiceItem;
        dto.IsActive = baseDto.IsActive;

        dto.AttributeValues = attributeValues
            .Select(value =>
            {
                definitions.TryGetValue(value.AttributeDefinitionId, out var definition);
                return new ItemAttributeValueDto
                {
                    AttributeDefinitionId = value.AttributeDefinitionId,
                    AttributeCode = definition?.Code ?? string.Empty,
                    AttributeName = definition?.Name ?? string.Empty,
                    Value = value.Value,
                    IsVariantAxis = definition?.IsVariantAxis ?? false
                };
            })
            .ToList();

        dto.Variants = variants
            .Select(variant => new ItemVariantDto
            {
                Id = variant.Id,
                Code = variant.Code,
                Name = variant.Name,
                IsActive = variant.IsActive,
                AttributeValues = variant.AttributeValues
                    .Select(value =>
                    {
                        definitions.TryGetValue(value.AttributeDefinitionId, out var definition);
                        return new ItemAttributeValueDto
                        {
                            AttributeDefinitionId = value.AttributeDefinitionId,
                            AttributeCode = definition?.Code ?? string.Empty,
                            AttributeName = definition?.Name ?? string.Empty,
                            Value = value.Value,
                            IsVariantAxis = definition?.IsVariantAxis ?? false
                        };
                    })
                    .ToList()
            })
            .ToList();

        dto.VariantTemplates = templates
            .Select(template =>
            {
                definitions.TryGetValue(template.AttributeDefinitionId, out var definition);
                return new ItemVariantTemplateDto
                {
                    AttributeDefinitionId = template.AttributeDefinitionId,
                    AttributeCode = definition?.Code ?? string.Empty,
                    AttributeName = definition?.Name ?? string.Empty,
                    DataType = definition?.DataType ?? string.Empty,
                    IsRequired = template.IsRequired,
                    IsVariantAxis = template.IsVariantAxis,
                    SortOrder = template.SortOrder
                };
            })
            .OrderBy(x => x.SortOrder)
            .ToList();

        return dto;
    }
}
