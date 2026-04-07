using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MediatR;

namespace Diten.MdmService.Application.Features.ItemCategories;

public sealed class GetAllItemCategoriesQueryHandler : IRequestHandler<GetAllItemCategoriesQuery, IReadOnlyList<ItemCategoryDto>>
{
    private readonly IItemCategoryRepository _categoryRepository;
    private readonly IItemLookupRepository _lookupRepository;

    public GetAllItemCategoriesQueryHandler(IItemCategoryRepository categoryRepository, IItemLookupRepository lookupRepository)
    {
        _categoryRepository = categoryRepository;
        _lookupRepository = lookupRepository;
    }

    public async Task<IReadOnlyList<ItemCategoryDto>> Handle(GetAllItemCategoriesQuery request, CancellationToken cancellationToken)
    {
        await _lookupRepository.EnsureSeedDataAsync(cancellationToken);

        var categories = await _categoryRepository.GetAllAsync(cancellationToken);
        var itemTypes = await _lookupRepository.GetItemTypesAsync(cancellationToken);
        var itemTypeMap = itemTypes.ToDictionary(x => x.Id);
        var categoryMap = categories.ToDictionary(x => x.Id);

        return categories.Select(category => ItemCategoryMapping.ToDto(category, itemTypeMap, categoryMap)).ToList();
    }
}

public sealed class GetItemCategoryByIdQueryHandler : IRequestHandler<GetItemCategoryByIdQuery, ItemCategoryDto?>
{
    private readonly IItemCategoryRepository _categoryRepository;
    private readonly IItemLookupRepository _lookupRepository;

    public GetItemCategoryByIdQueryHandler(IItemCategoryRepository categoryRepository, IItemLookupRepository lookupRepository)
    {
        _categoryRepository = categoryRepository;
        _lookupRepository = lookupRepository;
    }

    public async Task<ItemCategoryDto?> Handle(GetItemCategoryByIdQuery request, CancellationToken cancellationToken)
    {
        await _lookupRepository.EnsureSeedDataAsync(cancellationToken);

        var category = await _categoryRepository.GetByIdAsync(request.Id, cancellationToken);
        if (category is null)
        {
            return null;
        }

        var categories = await _categoryRepository.GetAllAsync(cancellationToken);
        var itemTypes = await _lookupRepository.GetItemTypesAsync(cancellationToken);
        return ItemCategoryMapping.ToDto(category, itemTypes.ToDictionary(x => x.Id), categories.ToDictionary(x => x.Id));
    }
}

public sealed class CreateItemCategoryRequestHandler : IRequestHandler<CreateItemCategoryRequest, Guid>
{
    private readonly IItemCategoryRepository _categoryRepository;
    private readonly IItemLookupRepository _lookupRepository;

    public CreateItemCategoryRequestHandler(IItemCategoryRepository categoryRepository, IItemLookupRepository lookupRepository)
    {
        _categoryRepository = categoryRepository;
        _lookupRepository = lookupRepository;
    }

    public async Task<Guid> Handle(CreateItemCategoryRequest request, CancellationToken cancellationToken)
    {
        await _lookupRepository.EnsureSeedDataAsync(cancellationToken);
        await ItemCategoryValidation.ValidateAsync(request, null, _categoryRepository, _lookupRepository, cancellationToken);

        var entity = new ItemCategory
        {
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            ItemTypeId = request.ItemTypeId,
            ParentCategoryId = request.ParentCategoryId,
            IsActive = request.IsActive
        };

        var created = await _categoryRepository.CreateAsync(entity, cancellationToken);
        return created.Id;
    }
}

public sealed class UpdateItemCategoryRequestHandler : IRequestHandler<UpdateItemCategoryRequest, bool>
{
    private readonly IItemCategoryRepository _categoryRepository;
    private readonly IItemLookupRepository _lookupRepository;

    public UpdateItemCategoryRequestHandler(IItemCategoryRepository categoryRepository, IItemLookupRepository lookupRepository)
    {
        _categoryRepository = categoryRepository;
        _lookupRepository = lookupRepository;
    }

    public async Task<bool> Handle(UpdateItemCategoryRequest request, CancellationToken cancellationToken)
    {
        await _lookupRepository.EnsureSeedDataAsync(cancellationToken);

        var existing = await _categoryRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Category not found.");

        await ItemCategoryValidation.ValidateAsync(request, request.Id, _categoryRepository, _lookupRepository, cancellationToken);

        existing.Code = request.Code.Trim();
        existing.Name = request.Name.Trim();
        existing.Description = request.Description?.Trim();
        existing.ItemTypeId = request.ItemTypeId;
        existing.ParentCategoryId = request.ParentCategoryId;
        existing.IsActive = request.IsActive;
        return await _categoryRepository.UpdateAsync(existing, cancellationToken);
    }
}

public sealed class DeleteItemCategoryRequestHandler : IRequestHandler<DeleteItemCategoryRequest, bool>
{
    private readonly IItemCategoryRepository _categoryRepository;

    public DeleteItemCategoryRequestHandler(IItemCategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async Task<bool> Handle(DeleteItemCategoryRequest request, CancellationToken cancellationToken)
    {
        await _categoryRepository.DeleteAsync(request.Id, cancellationToken);
        return true;
    }
}

public sealed class BulkDeleteItemCategoriesRequestHandler : IRequestHandler<BulkDeleteItemCategoriesRequest, BulkDeleteItemCategoriesResponse>
{
    private readonly IItemCategoryRepository _categoryRepository;

    public BulkDeleteItemCategoriesRequestHandler(IItemCategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async Task<BulkDeleteItemCategoriesResponse> Handle(BulkDeleteItemCategoriesRequest request, CancellationToken cancellationToken)
    {
        var deleted = await _categoryRepository.BulkDeleteAsync(request.Ids, cancellationToken);
        return new BulkDeleteItemCategoriesResponse { DeletedCount = deleted };
    }
}

internal static class ItemCategoryValidation
{
    public static async Task ValidateAsync(
        ItemCategoryUpsertRequestBase request,
        Guid? excludeId,
        IItemCategoryRepository categoryRepository,
        IItemLookupRepository lookupRepository,
        CancellationToken cancellationToken)
    {
        if (await categoryRepository.ExistsByCodeAsync(request.Code.Trim(), excludeId, cancellationToken))
        {
            throw new InvalidOperationException("Category code must be unique within the tenant.");
        }

        _ = await lookupRepository.GetItemTypeByIdAsync(request.ItemTypeId, cancellationToken)
            ?? throw new KeyNotFoundException("Item type not found.");

        if (request.ParentCategoryId.HasValue)
        {
            var parent = await categoryRepository.GetByIdAsync(request.ParentCategoryId.Value, cancellationToken)
                ?? throw new KeyNotFoundException("Parent category not found.");

            if (parent.ItemTypeId != request.ItemTypeId)
            {
                throw new InvalidOperationException("Parent category must belong to the same item type.");
            }

            if (excludeId.HasValue && await categoryRepository.WouldCreateCycleAsync(excludeId.Value, request.ParentCategoryId, cancellationToken))
            {
                throw new InvalidOperationException("Category hierarchy cannot contain a cycle.");
            }
        }
    }
}

internal static class ItemCategoryMapping
{
    public static ItemCategoryDto ToDto(
        ItemCategory entity,
        IReadOnlyDictionary<Guid, ItemType> itemTypes,
        IReadOnlyDictionary<Guid, ItemCategory> categories)
    {
        itemTypes.TryGetValue(entity.ItemTypeId, out var itemType);
        ItemCategory? parent = null;
        if (entity.ParentCategoryId.HasValue)
        {
            categories.TryGetValue(entity.ParentCategoryId.Value, out parent);
        }

        return new ItemCategoryDto
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            Description = entity.Description,
            ItemTypeId = entity.ItemTypeId,
            ItemType = itemType?.Name ?? string.Empty,
            ParentCategoryId = entity.ParentCategoryId,
            ParentCategory = parent?.Name,
            IsActive = entity.IsActive
        };
    }
}
